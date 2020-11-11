// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.LanguageServices;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Indentation;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Indentation
{
    internal partial class CSharpIndentationService
    {
        protected override bool ShouldUseTokenIndenter(Indenter indenter, out SyntaxToken syntaxToken)
            => ShouldUseSmartTokenFormatterInsteadOfIndenter(
                indenter.Rules, indenter.Root, indenter.LineToBeIndented, indenter.OptionService, indenter.OptionSet, out syntaxToken);

        protected override ISmartTokenFormatter CreateSmartTokenFormatter(Indenter indenter)
        {
            var workspace = indenter.Document.Project.Solution.Workspace;
            var formattingRuleFactory = workspace.Services.GetService<IHostDependentFormattingRuleFactoryService>();
            var rules = formattingRuleFactory.CreateRule(indenter.Document.Document, indenter.LineToBeIndented.Start).Concat(Formatter.GetDefaultFormattingRules(indenter.Document.Document));

            return new CSharpSmartTokenFormatter(indenter.OptionSet, rules, indenter.Root);
        }

        protected override IndentationResult? GetDesiredIndentationWorker(Indenter indenter, SyntaxToken? tokenOpt, SyntaxTrivia? triviaOpt)
        {
            IndentationResult? result = null;

            // always try from the token first if it's available
            if (tokenOpt != null)
            {
                result = TryGetDesiredIndentation(indenter, tokenOpt);
            }

            if (result != null) return result;

            // fallback to trivia if we have such
            if (triviaOpt != null)
            {
                result = TryGetDesiredIndentation(indenter, triviaOpt);
            }

            return result;
        }

        private static IndentationResult? TryGetDesiredIndentation(Indenter indenter, SyntaxTrivia? triviaOpt)
        {
            // If we have a // comment, and it's the only thing on the line, then if we hit enter, we should align to
            // that.  This helps for cases like:
            //
            //          int goo; // this comment
            //                   // continues
            //                   // onwards
            //
            // The user will have to manually indent `// continues`, but we'll respect that indentation from that point on.

            if (triviaOpt == null)
                return null;

            var trivia = triviaOpt.Value;
            if (!trivia.IsSingleOrMultiLineComment() && !trivia.IsDocComment())
                return null;

            var line = indenter.Text.Lines.GetLineFromPosition(trivia.FullSpan.Start);
            if (line.GetFirstNonWhitespacePosition() != trivia.FullSpan.Start)
                return null;

            // Previous line just contained this single line comment.  Align us with it.
            return new IndentationResult(trivia.FullSpan.Start, 0);
        }

        private static IndentationResult? TryGetDesiredIndentation(Indenter indenter, SyntaxToken? tokenOpt)
        {
            if (tokenOpt == null)
                return null;

            return GetIndentationBasedOnToken(indenter, tokenOpt.Value);
        }

        private static SyntaxNode GetAncestorStatementOrDeclaration(SyntaxToken token)
        {
            return token.GetAncestor(n => n is StatementSyntax || n is MemberDeclarationSyntax || n is TypeDeclarationSyntax);
        }

        private static bool IsLikelyExpressionContinuationToken(SyntaxToken token)
        {
            var kind = token.Kind();
            if (SyntaxFacts.IsBinaryExpressionOperatorToken(kind))
                return true;

            switch(kind)
            {
                case SyntaxKind.DotToken: return true;
                case SyntaxKind.DotDotToken: return true;
                case SyntaxKind.QuestionToken: return true;
                case SyntaxKind.QuestionQuestionToken: return true;
                case SyntaxKind.QuestionQuestionEqualsToken: return true;
                case SyntaxKind.OpenParenToken: return true;
                case SyntaxKind.CloseParenToken: return true;
                case SyntaxKind.OpenBracketToken: return true;
                case SyntaxKind.CloseBracketToken: return true;
            }

            return false;
        }

        private static IndentationResult? GetIndentationForArrowToken(Indenter indenter, SyntaxToken token, SyntaxToken prevToken)
        {
            var isTokenArrow = !token.IsNull && token.IsKind(SyntaxKind.EqualsGreaterThanToken);
            if (isTokenArrow)
            {
                // always adjust indentation according to the previous token if the current is an "=>"
                return GetIndentationFromTokenLine(indenter, prevToken);
            }

            var isPrevTokenArrow = !prevToken.IsNull && prevToken.IsKind(SyntaxKind.EqualsGreaterThanToken);
            if (isPrevTokenArrow)
            {
                // always adjust indentation according to the previous "=>" 
                return GetIndentationFromTokenLine(indenter, prevToken);
            }

            // check if the previous "skipped" token is an arrow "=>"?... for error recovery scenarios...
            var prevSkippedToken = token.GetPreviousToken(includeSkipped: true);
            var isPrevSkippedTokenArrow = !prevSkippedToken.IsNull && prevSkippedToken.IsKind(SyntaxKind.EqualsGreaterThanToken);
            if (isPrevSkippedTokenArrow)
            {
                if (indenter.IsIndentedToken(prevSkippedToken))
                {
                    // on the same line as the line that is being indented ... so it's actually the "next token"
                    return GetIndentationFromTokenLine(indenter, prevToken);
                }
                else
                {
                    // always adjust indentation according to the previous "=>" 
                    return GetIndentationFromTokenLine(indenter, prevSkippedToken);
                }
            }

            return null;
        }

        private static IndentationResult? GetIndentationForMemberAccess(Indenter indenter, SyntaxToken token, SyntaxToken prevToken)
        {
            bool isMemberAccessToken(SyntaxToken t)
            {
                switch (t.Kind())
                {
                    case SyntaxKind.DotToken:
                        return true;
                    case SyntaxKind.QuestionToken:
                        {
                            var nextToken = token.GetNextToken();
                            if (nextToken.Kind() == SyntaxKind.DotToken)
                            {
                                // ?.
                                return true;
                            }

                            return false;
                        }
                }

                return false;
            }

            if (isMemberAccessToken(token))
                return GetIndentationFromTopExpression(indenter, token, alignSpace: 4);

            if (isMemberAccessToken(prevToken))
                return GetIndentationFromTopExpression(indenter, prevToken, alignSpace: 4);

            return null;
        }

        private static IndentationResult? GetIndentationForBinaryExpression(Indenter indenter, SyntaxToken token, SyntaxToken prevToken)
        {
            bool isBinaryExpression(SyntaxToken t)
            {
                if (SyntaxFacts.IsBinaryExpression(t.Kind())) return true;
                return false;
            }

            if (isBinaryExpression(token))
                return GetIndentationFromTopExpression(indenter, token);

            if (isBinaryExpression(prevToken))
                return GetIndentationFromTopExpression(indenter, prevToken);

            return null;
        }

        private static IndentationResult? GetIndentationForAssignment(Indenter indenter, SyntaxToken token, SyntaxToken prevToken)
        {
            bool isAssignmentExpression(SyntaxToken t)
            {
                if (SyntaxFacts.IsAssignmentExpressionOperatorToken(t.Kind())) return true;
                if (t.IsKind(SyntaxKind.ColonEqualsToken)) return true;
                return false;
            }

            if (isAssignmentExpression(token))
                return GetIndentationFromTopExpression(indenter, token, alignSpace: 4);

            if (isAssignmentExpression(prevToken))
                return GetIndentationFromTopExpression(indenter, token, alignSpace: 4);

            return null;
        }

        private static SyntaxNode GetContainingParentClosestToToken(Indenter indenter, SyntaxToken token)
        {
            SyntaxNode parent = null;

            // get the parent of the token - however only if it's on the "same line"! ... otherwise use the previous line parent!
            if (!indenter.LineToBeIndented.Span.Contains(token.Span))
            {
                // we need to find the parent of the first token included in the line that is being indented
                var tokenInIndentedLine = token;

                if (tokenInIndentedLine.SpanStart > indenter.LineToBeIndented.Span.End)
                    tokenInIndentedLine = token.GetPreviousToken(t => t.SpanStart <= indenter.LineToBeIndented.Span.End);

                if (!tokenInIndentedLine.IsNull)
                    parent = tokenInIndentedLine.Parent;

                // bubble up until we have a parent that actually contains the token!
                while (parent != null)
                {
                    if (parent.Span.Contains(token.Span))
                        break;
                    parent = parent.Parent;
                }

                if (parent != null && !parent.Span.IntersectsWith(indenter.LineToBeIndented.Span) && parent.Parent != null)
                {
                    // parent doesn't contain the line that is being indented - so move to the parent again as a final attempt
                    parent = parent.Parent;
                }
            }

            // fallback to using the token parent
            if (parent == null)
                parent = token.Parent;

            return parent;
        }

        private static IndentationResult? GetIndentationBasedOnSemanticsForNewLine(Indenter indenter, SyntaxToken token, SyntaxToken prevToken, SyntaxNode parent = null)
        {
            // Try indent with special handling for the "=>" token
            var result = GetIndentationForArrowToken(indenter, token, prevToken);
            if (result != null) return result;

            // Try indent for "member access"
            result = GetIndentationForMemberAccess(indenter, token, prevToken);
            if (result != null) return result;

            // Try indent for "binary expression"
            result = GetIndentationForBinaryExpression(indenter, token, prevToken);
            if (result != null) return result;

            // Try indent for "assignment" operator
            result = GetIndentationForAssignment(indenter, token, prevToken);
            if (result != null) return result;

            // if we are in class / function / member ... we should follow the indentation of that
            // - always indent in member on newline

            // if we are in top of the namespace, we should follow the namespace
            // - flat namepace = no indentation
            // - namespace with braces = indentation

            // we need to have a valid parent
            if (parent == null)
                parent = GetContainingParentClosestToToken(indenter, token);

            var containerSyntax = parent.GetAncestorOrThis(a => {
                switch (a.Kind())
                {
                    case SyntaxKind.NamespaceDeclaration:
                    // types
                    case SyntaxKind.ClassDeclaration:
                    case SyntaxKind.InterfaceDeclaration:
                    case SyntaxKind.StructDeclaration:
                    case SyntaxKind.EnumDeclaration:
                    case SyntaxKind.RecordDeclaration:
                    // methods & members
                    case SyntaxKind.MethodDeclaration:
                    case SyntaxKind.LocalFunctionStatement:
                    case SyntaxKind.PropertyDeclaration:
                    case SyntaxKind.GetAccessorDeclaration:
                    case SyntaxKind.SetAccessorDeclaration:
                    case SyntaxKind.EventDeclaration:
                    case SyntaxKind.AddAccessorDeclaration:
                    case SyntaxKind.RemoveAccessorDeclaration:
                    case SyntaxKind.InitAccessorDeclaration:
                    case SyntaxKind.UnknownAccessorDeclaration:
                    // parameters
                    case SyntaxKind.Parameter:
                    case SyntaxKind.ParameterList:
                    // call args
                    case SyntaxKind.Argument:
                    case SyntaxKind.ArgumentList:
                    // lambdas
                    case SyntaxKind.SimpleLambdaExpression:
                    case SyntaxKind.ParenthesizedLambdaExpression:
                    // object initializers
                    case SyntaxKind.ObjectInitializerExpression:
                    case SyntaxKind.CollectionInitializerExpression:
                    case SyntaxKind.ComplexElementInitializerExpression:
                    case SyntaxKind.WithInitializerExpression:
                    case SyntaxKind.ArrayInitializerExpression:
                    // statements
                    case SyntaxKind.TryStatement:
                    case SyntaxKind.IfStatement:
                    case SyntaxKind.ElseClause:
                    case SyntaxKind.ForEachStatement:
                    case SyntaxKind.WhileStatement:
                    case SyntaxKind.SwitchStatement:
                    case SyntaxKind.SwitchSection:
                        return true;
                }

                return false;
            });

            if (containerSyntax != null)
            {
                // ok we are in one of the above mentioned syntaxes
                SyntaxToken startToken;

                // handle namespace 
                if (containerSyntax is NamespaceDeclarationSyntax nsDecl)
                {
                    if (token == nsDecl.NamespaceKeyword && nsDecl.Parent != null)
                    {
                        // use indentation from "parent scope" ... basically the same as the current token
                        return GetIndentationFromTokenLine(indenter, token, additionalSpace: 0);
                    }

                    startToken = nsDecl.GetFirstToken();
                    var flatNamespace = nsDecl.OpenBraceToken.Width() == 0;
                    if (flatNamespace)
                        return GetIndentationFromTokenLine(indenter, startToken, additionalSpace: 0);
                    else
                        return GetIndentationFromTokenLine(indenter, startToken);
                }
                else if (containerSyntax is PropertyDeclarationSyntax propDecl)
                {
                    result = GetIndentationFromMemberModifiers(indenter, propDecl, token);
                    if (result != null) return result;

                    result = GetIndentationFromBodyNode(indenter, propDecl, token);
                    if (result != null) return result;
                }
                else if (containerSyntax is AccessorDeclarationSyntax accessorDecl)
                {
                    result = GetIndentationFromBodyNode(indenter, accessorDecl, token, accessorDecl.Body, accessorDecl.ExpressionBody);
                    if (result != null) return result;
                }
                else if (containerSyntax is MethodDeclarationSyntax methodDecl)
                {
                    result = GetIndentationFromMemberModifiers(indenter, methodDecl, token);
                    if (result != null) return result;

                    result = GetIndentationFromBodyNode(indenter, methodDecl, token);
                    if (result != null) return result;
                }
                else if (containerSyntax is ParameterSyntax param)
                {
                    var paramsList = token.GetAncestor(n => n is ParameterListSyntax) as ParameterListSyntax;
                    return GetIndentationFromParamsList(indenter, param, paramsList, token);
                }
                else if (containerSyntax is ParameterListSyntax paramsList)
                {
                    // indent relative to the "previous parameter" otherwise
                    var paramOrList = token.GetAncestor(n => n is ParameterSyntax || n is ParameterListSyntax);
                    if (paramOrList is ParameterSyntax p)
                        return GetIndentationFromParamsList(indenter, p, paramsList, token);

                    return GetIndentationFromParamsList(indenter, null, paramsList, token);
                }
                else if (containerSyntax is ArgumentSyntax arg)
                {
                    var argsList = token.GetAncestor(n => n is ArgumentListSyntax) as ArgumentListSyntax;
                    return GetIndentationFromArgsList(indenter, arg, argsList, token);
                }
                else if (containerSyntax is ArgumentListSyntax argsList)
                {
                    // indent relative to the "previous parameter" otherwise
                    var argOrList = token.GetAncestor(n => n is ArgumentSyntax || n is ArgumentListSyntax);
                    if (argOrList is ArgumentSyntax a)
                        return GetIndentationFromArgsList(indenter, a, argsList, token);

                    return GetIndentationFromArgsList(indenter, null, argsList, token);
                }
                else if (containerSyntax is TypeDeclarationSyntax typeDecl)
                {
                    result = GetIndentationFromMemberModifiers(indenter, typeDecl, token);
                    if (result != null) return result;

                    result = GetIndentationFromBodyNode(indenter, typeDecl, token);
                    if (result != null) return result;
                }
                else if (containerSyntax is LocalFunctionStatementSyntax localFunc)
                {
                    result = GetIndentationFromMemberModifiers(indenter, localFunc, token);
                    if (result != null) return result;

                    result = GetIndentationFromBodyNode(indenter, localFunc, token, localFunc.Body, localFunc.ExpressionBody);
                    if (result != null) return result;
                }
                else if (containerSyntax is SimpleLambdaExpressionSyntax simpleLambda)
                {
                    result = GetIndentationFromBodyNode(indenter, simpleLambda, token, simpleLambda.Block, simpleLambda.ExpressionBody);
                    if (result != null) return result;
                }
                else if (containerSyntax is ParenthesizedLambdaExpressionSyntax parenLambda)
                {
                    result = GetIndentationFromBodyNode(indenter, parenLambda, token, parenLambda.Block, parenLambda.ExpressionBody);
                    if (result != null) return result;
                }
                else if (containerSyntax is EventDeclarationSyntax evntDecl)
                {
                    result = GetIndentationFromMemberModifiers(indenter, evntDecl, token);
                    if (result != null) return result;

                    result = GetIndentationFromBodyNode(indenter, evntDecl, token);
                    if (result != null) return result;
                }
                else if (containerSyntax is TryStatementSyntax tryStat)
                {
                    var firstBlockToken = tryStat.GetFirstTokenOrFirstPrevious(tryStat.TryKeyword, includeSelf: false);

                    // try keyword
                    if (indenter.IsIndentedToken(token, tryStat.TryKeyword))
                        return GetIndentationFromNodeLine(indenter, tryStat, tryStat.TryKeyword, 0);

                    // try indentation on any of the try statement blocks
                    result = GetIndentationFromBlockNode(indenter, tryStat.Block, token);
                    if (result != null) return result;

                    // finally
                    if (tryStat.Finally?.Span.Contains(token.Span) == true)
                    {
                        // finally keyword
                        if (indenter.IsIndentedToken(token, tryStat.Finally?.FinallyKeyword))
                            return GetIndentationFromTokenLine(indenter, tryStat.TryKeyword, 0);

                        // try indentation on the finally block
                        result = GetIndentationFromBlockNode(indenter, tryStat.Finally?.Block, token);
                        if (result != null) return result;

                        // indent relative to the block without any additional
                        return GetIndentationFromTokenLine(indenter, firstBlockToken, 0);
                    }

                    // catch blocks
                    if (tryStat.Catches.Span.Contains(token.Span) == true)
                    {
                        // try indentation based on any of the catch blocks
                        result = GetIndentationFromAny(indenter, tryStat.Catches, token, n => {
                            // catch keyword
                            if (indenter.IsIndentedToken(token, n.CatchKeyword))
                                return GetIndentationFromTokenLine(indenter, tryStat.TryKeyword, 0);

                            // catch block
                            var r = GetIndentationFromBlockNode(indenter, n.Block, token);
                            if (r != null) return r;
                            return null;
                        });
                        if (result != null) return result;

                        // indent relative to the block without any additional
                        return GetIndentationFromTokenLine(indenter, firstBlockToken, 0);
                    }
                }
                else if (containerSyntax is IfStatementSyntax ifStat)
                {
                    // if keyword
                    if (indenter.IsIndentedToken(token, ifStat.IfKeyword))
                        return GetIndentationFromNodeLine(indenter, ifStat, ifStat.IfKeyword);

                    // if block statement
                    result = GetIndentationFromStatementNode(indenter, ifStat.Statement, token);
                    if (result != null) return result;
                }
                else if (containerSyntax is ElseClauseSyntax elseClause)
                {
                    // else keyword
                    if (indenter.IsIndentedToken(token, elseClause.ElseKeyword))
                        return GetIndentationFromNodeLine(indenter, elseClause, elseClause.ElseKeyword);

                    // else block statement
                    result = GetIndentationFromStatementNode(indenter, elseClause.Statement, token);
                    if (result != null) return result;
                }
                else if (containerSyntax is ForEachStatementSyntax foreachStat)
                {
                    // foreach keyword
                    if (indenter.IsIndentedToken(token, foreachStat.ForEachKeyword))
                        return GetIndentationFromNodeLine(indenter, foreachStat, foreachStat.ForEachKeyword);

                    // foreach block
                    result = GetIndentationFromStatementNode(indenter, foreachStat.Statement, token);
                    if (result != null) return result;
                }
                else if (containerSyntax is WhileStatementSyntax whileStat)
                {
                    // while keyword
                    if (indenter.IsIndentedToken(token, whileStat.WhileKeyword))
                        return GetIndentationFromNodeLine(indenter, whileStat, whileStat.WhileKeyword);

                    // while block
                    result = GetIndentationFromStatementNode(indenter, whileStat.Statement, token);
                    if (result != null) return result;
                }
                else if (containerSyntax is SwitchSectionSyntax switchSection)
                {
                    // any of the statement blocks
                    result = GetIndentationFromAny(indenter, switchSection.Statements, token, stat => {
                        var r = GetIndentationFromStatementNode(indenter, stat, token);
                        if (r != null) return r;
                        return null;
                    });
                    if (result != null) return result;
                }
                else if (containerSyntax is InitializerExpressionSyntax initializer)
                {
                    return GetIndentationFromTokenLine(indenter, initializer.OpenBraceToken);
                }

                // fallback to indentation from the parent "block"
                containerSyntax = parent.GetAncestorOrThis(a => a.IsKind(SyntaxKind.Block));
                if (containerSyntax is BlockSyntax block)
                {
                    GetFirstTokenFromNode(block, token, out startToken);
                    return GetIndentationFromTokenLine(indenter, startToken);
                }

                // if still no success, just indent based on the start of the parent
                var parentFirstToken = parent.GetFirstToken();
                return GetIndentationFromTokenLine(indenter, parentFirstToken);
            }

            return null;
        }

        private static IndentationResult? GetIndentationFromAny<T>(Indenter indenter, SyntaxList<T> list, SyntaxToken token, Func<T, IndentationResult?> predicate)
            where T : CSharpSyntaxNode
        {
            if (!list.Span.Contains(token.Span)) return null;

            var hasEnteredRange = false;
            foreach (var node in list)
            {
                if (!node.Span.Contains(token.Span))
                {
                    if (!hasEnteredRange) continue;
                    return null;
                }

                hasEnteredRange = true;
                var result = predicate(node);
                if (result != null) return result;
            }

            return null;
        }

        private static IndentationResult? GetIndentationFromParamsList(Indenter indenter, ParameterSyntax param, ParameterListSyntax paramsList, SyntaxToken token)
        {
            if (indenter.IsIndentedToken(token, paramsList.OpenParenToken))
            {
                // on the open paren, indent based on the start of the params list without indentation... its generally a "bad construct"... but anyway...
                var beforeListToken = paramsList.GetFirstTokenOrFirstPrevious(paramsList.OpenParenToken, includeSelf: false);
                return GetIndentationFromTokenLine(indenter, beforeListToken, 0);
            }

            if (indenter.IsIndentedToken(token, paramsList.CloseParenToken))
            {
                // on the close paren, indent based on the start of the params list without indentation
                return GetIndentationFromTokenLine(indenter, paramsList.OpenParenToken, 0);
            }

            if (token.IsKind(SyntaxKind.CommaToken))
            {
                // we are at a comma... so indent relative to the previous param
                var prevParamToken = token.GetPreviousTokenWhile(t => t.IsKind(SyntaxKind.CommaToken), movePast: true);
                return GetIndentationFromParam(indenter, paramsList, prevParamToken);
            }

            //if empty... then indent based on the open paren
            if (paramsList.Parameters.Count == 0)
                return GetIndentationFromTokenLine(indenter, paramsList.OpenParenToken);

            if (param != null)
            {
                var paramFirstToken = param.GetFirstToken();
                return GetIndentationFromParam(indenter, paramsList, paramFirstToken);
            }

            return GetIndentationFromParam(indenter, paramsList, token);
        }

        private static IndentationResult? GetIndentationFromParam(Indenter indenter, ParameterListSyntax paramsList, SyntaxToken paramToken)
        {
            // if the prev param is on same line as the first param of the list... align with the first parameter
            var firstParamToken = paramsList.Parameters.FirstOrDefault()?.GetFirstToken();
            if (firstParamToken != null && firstParamToken?.Line == paramToken.Line)
            {
                if (firstParamToken == paramToken)
                {
                    // we need to indent relative to the open paren
                    return GetIndentationFromTokenLine(indenter, paramsList.OpenParenToken);
                }

                // align with the first parameter
                return GetIndentationFromToken(indenter, firstParamToken.Value, 0);
            }

            // find the parameter and try indentig relative to that
            var paramIndex = paramsList.Parameters.IndexOf(p => p.Span.Contains(paramToken.Span));
            if (paramIndex != -1)
            {
                if (paramIndex == 0)
                    return GetIndentationFromTokenLine(indenter, paramsList.OpenParenToken);

                // indent relative to the previous parameter however align with the first token on that line!
                var prevParam = paramsList.Parameters[paramIndex - 1];
                var prevParamFirstToken = prevParam.GetFirstToken();

                SyntaxToken? firstTokenOnPrevParamLine = prevParamFirstToken;
                if (!prevParamFirstToken.IsFirstTokenOnLine())
                    firstTokenOnPrevParamLine = prevParamFirstToken.FindFirstTokenOnLine();

                // use the first param token if it's not within the params list
                if (firstTokenOnPrevParamLine != null && !paramsList.Span.Contains(firstTokenOnPrevParamLine.Value.Span))
                    firstTokenOnPrevParamLine = firstParamToken;

                return GetIndentationFromToken(indenter, firstTokenOnPrevParamLine ?? prevParamFirstToken, 0);
            }

            // not sure what to align with... simply align with the previous parameter token
            return GetIndentationFromToken(indenter, paramToken, 0);
        }

        private static IndentationResult? GetIndentationFromArgsList(Indenter indenter, ArgumentSyntax arg, ArgumentListSyntax argsList, SyntaxToken token)
        {
            if (indenter.IsIndentedToken(token, argsList.OpenParenToken))
            {
                // on the open paren, indent based on the start of the args list without indentation... its generally a "bad construct"... but anyway...
                var beforeListToken = argsList.GetFirstTokenOrFirstPrevious(argsList.OpenParenToken, includeSelf: false);
                return GetIndentationFromTokenLine(indenter, beforeListToken, 0);
            }

            if (indenter.IsIndentedToken(token, argsList.CloseParenToken))
            {
                // on the close paren, indent based on the start of the args list without indentation
                return GetIndentationFromTokenLine(indenter, argsList.OpenParenToken, 0);
            }

            if (token.IsKind(SyntaxKind.CommaToken))
            {
                // we are at a comma... so indent relative to the previous param
                var prevParamToken = token.GetPreviousTokenWhile(t => t.IsKind(SyntaxKind.CommaToken), movePast: true);
                return GetIndentationFromArg(indenter, argsList, prevParamToken);
            }

            //if empty... then indent based on the open paren
            if (argsList.Arguments.Count == 0)
                return GetIndentationFromTokenLine(indenter, argsList.OpenParenToken);

            if (arg != null)
            {
                var paramFirstToken = arg.GetFirstToken();
                return GetIndentationFromArg(indenter, argsList, paramFirstToken);
            }

            return GetIndentationFromArg(indenter, argsList, token);
        }

        private static IndentationResult? GetIndentationFromArg(Indenter indenter, ArgumentListSyntax argsList, SyntaxToken argToken)
        {
            // if the prev param is on same line as the first param of the list... align with the first parameter
            var firstArgToken = argsList.Arguments.FirstOrDefault()?.GetFirstToken();
            if (firstArgToken != null && firstArgToken?.Line == argToken.Line)
            {
                if (firstArgToken == argToken)
                {
                    // we need to indent relative to the open paren
                    return GetIndentationFromTokenLine(indenter, argsList.OpenParenToken);
                }

                // align with the first parameter
                return GetIndentationFromToken(indenter, firstArgToken.Value, 0);
            }

            // find the parameter and try indentig relative to that
            var argIndex = argsList.Arguments.IndexOf(p => p.Span.Contains(argToken.Span));
            if (argIndex != -1)
            {
                if (argIndex == 0)
                    return GetIndentationFromTokenLine(indenter, argsList.OpenParenToken);

                // indent relative to the previous argument however align with the first token on that line!
                var prevArg = argsList.Arguments[argIndex - 1];
                var prevArgFirstToken = prevArg.GetFirstToken();

                SyntaxToken? firstTokeOnPrevArgLine = prevArgFirstToken;
                if (!prevArgFirstToken.IsFirstTokenOnLine())
                    firstTokeOnPrevArgLine = prevArgFirstToken.FindFirstTokenOnLine();

                // use the first arg token if it's not within the args list
                if (firstTokeOnPrevArgLine != null && !argsList.Span.Contains(firstTokeOnPrevArgLine.Value.Span))
                    firstTokeOnPrevArgLine = firstArgToken;

                return GetIndentationFromToken(indenter, firstTokeOnPrevArgLine ?? prevArgFirstToken, 0);
            }

            // not sure what to align with... simply align with the previous arg token
            return GetIndentationFromToken(indenter, argToken, 0);
        }

        private static IndentationResult? GetIndentationFromTopExpression(Indenter indenter, SyntaxToken token, bool? align = null, int? alignSpace = null)
        {
            var topExpr = token.Parent.GetTopAncestorOrThisWhile(n => n is ExpressionSyntax);
            if (topExpr == null)
            {
                // we allow certain statements too... only if it's the immediate parent
                if (token.Parent is ArrowExpressionClauseSyntax || token.Parent is EqualsValueClauseSyntax)
                    topExpr = token.Parent;

                if (topExpr == null) return null;
            }

            var firstStatToken = topExpr.GetFirstToken();

            // cannot align on the first token if the current token is the one being indented... then we will get 0 indentation... then pick the "previous one"
            if (firstStatToken == token)
                firstStatToken = firstStatToken.GetPreviousToken();

            int? additionalSpace = null;

            // don't align for expression statements and certain others - however, do apply indentation!
            var parent = topExpr.GetParentOrThis(n => {
                switch (n)
                {
                    case ExpressionStatementSyntax:
                        {
                            align = false;
                            return true;
                        }
                    case ArrowExpressionClauseSyntax:
                    case EqualsValueClauseSyntax:
                        {
                            align = true;
                            additionalSpace = alignSpace;
                            return true;
                        }
                }
                return false;
            });

            // if previous & next token is string literal AND first token is a string literal, force alignment with the previous string token
            if (IsStringLiteral(firstStatToken))
            {
                var prevToken = token.GetPreviousToken();
                if (IsStringLiteral(prevToken))
                {
                    var nextToken = token.GetNextToken();
                    if (IsStringLiteral(nextToken))
                    {
                        align = true;
                        firstStatToken = prevToken;
                        additionalSpace = 0;

                        if (prevToken.Parent is InterpolatedStringExpressionSyntax strExpr)
                            firstStatToken = strExpr.StringStartToken;
                    }
                }
            }

            // align per default
            align ??= true;
            if (align == true)
                return GetIndentationFromToken(indenter, firstStatToken, additionalSpace ?? 0);

            return GetIndentationFromTokenLine(indenter, firstStatToken, additionalSpace);
        }

        private static bool IsStringLiteral(SyntaxToken token)
        {
            switch (token.Kind())
            {
                case SyntaxKind.StringLiteralToken:
                case SyntaxKind.CharacterLiteralToken:
                case SyntaxKind.InterpolatedStringTextToken:
                case SyntaxKind.InterpolatedStringToken:
                case SyntaxKind.InterpolatedVerbatimStringStartToken:
                case SyntaxKind.InterpolatedStringEndToken:
                case SyntaxKind.InterpolatedStringStartToken:
                    return true;
            }

            return false;
        }

        private static IndentationResult? GetIndentationForPrefixModifiers(Indenter indenter, CSharpSyntaxNode node, SyntaxToken token, Func<SyntaxToken, bool> isModifierPredicate)
        {
            // this is only possible if the token is part of the line that is being indented!
            if (!indenter.LineToBeIndented.Span.Contains(token.Span)) return null;

            // find the first sibling modifier - keep doing as long as we are on a modifier
            SyntaxToken? firstModifier = null;
            var current = token;
            while (true)
            {
                if (isModifierPredicate(current))
                {
                    firstModifier = current;
                    current = current.GetPreviousToken();
                }
                else if (firstModifier == null)
                {
                    current = current.GetPreviousToken();
                    if (!isModifierPredicate(current)) break;
                    firstModifier = current;
                    current = current.GetPreviousToken();
                }
                else
                {
                    break;
                }
            }

            if (firstModifier == token)
            {
                // the token itself is the first modifier, so use the indentation of the parent and indenting "as usual"
                var firstParentToken = node.Parent.GetFirstToken();

                // if we have a flat namespace, just return without indentation
                if (node.Parent is NamespaceDeclarationSyntax nsDecl && nsDecl.OpenBraceToken.Width() == 0)
                    return GetIndentationFromTokenLine(indenter, firstParentToken, 0);

                return GetIndentationFromTokenLine(indenter, firstParentToken);
            }
            else if (firstModifier != null)
            {
                // use indentation from other modifier - without any additional indentation
                return GetIndentationFromTokenLine(indenter, firstModifier.Value, 0);
            }

            // nothing based on modifier could be made
            return null;
        }

        private static IndentationResult? GetIndentationFromMemberModifiers(Indenter indenter, CSharpSyntaxNode node, SyntaxToken token)
        {
            bool isModifer(SyntaxToken token)
            {
                switch (token.Kind())
                {
                    // accessibility
                    case SyntaxKind.PrivateKeyword:
                    case SyntaxKind.ProtectedKeyword:
                    case SyntaxKind.InternalKeyword:
                    case SyntaxKind.PublicKeyword:
                    // types
                    case SyntaxKind.InterfaceKeyword:
                    case SyntaxKind.NamespaceKeyword:
                    case SyntaxKind.ClassKeyword:
                    case SyntaxKind.StructKeyword:
                    case SyntaxKind.DelegateKeyword:
                    case SyntaxKind.EnumKeyword:
                    case SyntaxKind.RecordKeyword:
                    // modifiers
                    case SyntaxKind.StaticKeyword:
                    case SyntaxKind.OverrideKeyword:
                    case SyntaxKind.VirtualKeyword:
                    case SyntaxKind.NewKeyword:
                        return true;
                }
                return false;
            }

            return GetIndentationForPrefixModifiers(indenter, node, token, isModifer);
        }

        private static IndentationResult? GetIndentationFromStatementNode(Indenter indenter, StatementSyntax statement, SyntaxToken token)
        {
            if (statement == null) return null;

            if (!statement.Span.Contains(token.Span)) return null;

            if (statement is BlockSyntax block) return GetIndentationFromBlockNode(indenter, block, token);

            return null;
        }

        private static IndentationResult? GetIndentationFromBlockNode(Indenter indenter, BlockSyntax block, SyntaxToken token)
        {
            if (block == null) return null;
            if (!block.Span.Contains(token.Span)) return null;

            return GetIndentationFromBlock(indenter, block, block.OpenBraceToken, block.CloseBraceToken, token);
        }

        private static IndentationResult? GetIndentationFromBlock(Indenter indenter, CSharpSyntaxNode node, SyntaxToken openBraceToken, SyntaxToken closeBraceToken, SyntaxToken token)
        {
            // only if within th braces
            var isWithinBraces = token.Span.End <= closeBraceToken.Span.End && token.Span.Start >= openBraceToken.Span.Start;
            if (!isWithinBraces) return null;

            // get the token from where the indentation will be calculated, usually either the "target token" or the first token of the "node" ... could also be an explicit token starting the node that is known "beforehand" ...
            var nodeStartToken = node.GetFirstTokenOrFirstPrevious(token, includeSelf: false);

            if (indenter.IsIndentedToken(token, openBraceToken))
            {
                // if the token being indented is the {, we don't add any additional indentation level!
                return GetIndentationFromTokenLine(indenter, nodeStartToken, 0);
            }

            if (indenter.IsIndentedToken(token, closeBraceToken))
            {
                // if the token being indented is the }, we don't add any additional indentation level - and use indentation of the opening {
                return GetIndentationFromTokenLine(indenter, openBraceToken, 0);
            }

            // we should indent relative to the opening brace for the block!
            return GetIndentationFromTokenLine(indenter, openBraceToken);
        }

        private static IndentationResult? GetIndentationFromBodyNode(Indenter indenter, MemberDeclarationSyntax memberDecl, SyntaxToken token)
        {
            var nodeStartToken = memberDecl.GetFirstTokenOrFirstPrevious(token);

            // try indenting based on the body blocks
            var result = GetIndentationFromBodyNode(indenter, memberDecl, token, memberDecl.GetBody(), memberDecl.GetExpressionBody(), nodeStartToken);
            if (result != null) return result;

            // try indenting based on the explicit open / close braces
            var (openBrace, closeBrace) = memberDecl.GetBraces();
            if (!openBrace.IsNull && !closeBrace.IsNull)
            {
                if (token.SpanStart >= openBrace.SpanStart && token.SpanStart <= closeBrace.SpanStart)
                {
                    // get the token from where the indentation will be calculated, usually either the "target token" or the first token of the "node" ... could also be an explicit token starting the node that is known "beforehand" ...
                    if (indenter.IsIndentedToken(token, openBrace))
                    {
                        // if the token being indented is the {, we don't add any additional indentation level!
                        return GetIndentationFromTokenLine(indenter, nodeStartToken, 0);
                    }

                    if (indenter.IsIndentedToken(token, closeBrace))
                    {
                        // if the token being indented is the }, we don't add any additional indentation level - and use indentation of the opening {
                        return GetIndentationFromTokenLine(indenter, openBrace, 0);
                    }

                    // we should indent relative to the opening brace for the block!
                    return GetIndentationFromTokenLine(indenter, openBrace);
                }
            }

            // it's not within the body - indent relative to the first token
            return GetIndentationFromTokenLine(indenter, nodeStartToken);
        }

        private static IndentationResult? GetIndentationFromBodyNode(Indenter indenter, CSharpSyntaxNode node, SyntaxToken token, BlockSyntax blockBody, CSharpSyntaxNode exprBody)
        {
            var nodeStartToken = node.GetFirstTokenOrFirstPrevious(token);

            // try indenting based on the body blocks
            var result = GetIndentationFromBodyNode(indenter, node, token, blockBody, exprBody, nodeStartToken);
            if (result != null) return result;

            // it's not within the body - indent relative to the first token
            return GetIndentationFromTokenLine(indenter, nodeStartToken);
        }

        private static IndentationResult? GetIndentationFromBodyNode(Indenter indenter, CSharpSyntaxNode node, SyntaxToken token, BlockSyntax blockBody, CSharpSyntaxNode exprBody, SyntaxToken? preferredIndentStartToken)
        {
            // check indentation for block body - if the token is located within
            if (blockBody != null && blockBody.Span.Contains(token.Span))
            {
                var indentStartToken = preferredIndentStartToken ?? node.GetFirstTokenOrFirstPrevious(token);

                // get the token from where the indentation will be calculated, usually either the "target token" or the first token of the "node" ... could also be an explicit token starting the node that is known "beforehand" ...
                if (indenter.IsIndentedToken(token, blockBody.OpenBraceToken))
                {
                    // if the token being indented is the {, we don't add any additional indentation level!
                    return GetIndentationFromTokenLine(indenter, indentStartToken, 0);
                }

                if (indenter.IsIndentedToken(token, blockBody.CloseBraceToken))
                {
                    // if the token being indented is the }, we don't add any additional indentation level - and use indentation of the opening {
                    return GetIndentationFromTokenLine(indenter, blockBody.OpenBraceToken, 0);
                }

                // we should indent relative to the opening brace for the block!
                return GetIndentationFromTokenLine(indenter, blockBody.OpenBraceToken);
            }

            // check indentation for expression body - if the token is located within
            if (exprBody != null && exprBody.Span.Contains(token.Span))
            {
                // get the token from where the indentation will be calculated, usually either the "target token" or the first token of the "node" ... could also be an explicit token starting the node that is known "beforehand" ...
                var indentStartToken = preferredIndentStartToken ?? node.GetFirstTokenOrFirstPrevious(token);

                var expr = exprBody;

                if (exprBody is ArrowExpressionClauseSyntax arrowExpr)
                    expr = arrowExpr.Expression;

                // get relative to the expression if possible
                var firstExprToken = expr.GetFirstToken();
                if (firstExprToken == token)
                {
                    // if the token being indented is the first start token of the expression, then we should indent relative to the previous
                    return GetIndentationFromTokenLine(indenter, indentStartToken);
                }

                // otherwise indent based on the token immediately previous to the expression
                var firstBlockToken = exprBody.GetFirstTokenOrFirstPrevious(token);
                return GetIndentationFromTokenLine(indenter, firstBlockToken);
            }

            return null;
        }

        private static IndentationResult? GetIndentationBasedOnTokenAndNode(Indenter indenter, SyntaxToken prevToken)
        {
            var sourceText = indenter.LineToBeIndented.Text;

            var tokenOpt = indenter.TryGetCurrentVisibleToken(out var isTokenWithin);
            var token = tokenOpt ?? prevToken;

            var isTokenOnNewline = false;

            // if we have a token, we can check for some "special cases"
            if (tokenOpt != null)
            {
                // if the token is on a new line, we can handle some special cases
                isTokenOnNewline = token.IsFirstTokenOnLine(sourceText);
                if (isTokenOnNewline)
                {
                    var semanticResult = GetIndentationBasedOnSemanticsForNewLine(indenter, tokenOpt.Value, prevToken);
                    if (semanticResult != null) return semanticResult;
                }
            }
            else
            {
                // if we don't have a token, we can check for the prev token and try to get the expected indentation based on that
                // if the token is on a new line, we can handle some special cases
                isTokenOnNewline = prevToken.IsLastTokenOnLine(sourceText);
                if (isTokenOnNewline)
                {
                    var semanticResult = GetIndentationBasedOnSemanticsForNewLine(indenter, prevToken, default);
                    if (semanticResult != null) return semanticResult;
                }
            }

            // for the remainder we really need the token
            if (tokenOpt == null) return null;

            var isPrevTokenOnNewline = prevToken.IsFirstTokenOnLine(sourceText);

            var prevStatement = GetAncestorStatementOrDeclaration(prevToken);
            var currentStatement = GetAncestorStatementOrDeclaration(token);
            var isOnSameStatement = prevStatement == currentStatement;

            // if the current token is on a new line we have 2 cases:
            // 1. the token is a continuation of an expression/statement from the previous line
            // 2. the token is the start of a new statement
            var useIndentationFromStatement = !isOnSameStatement;

            // if not on the same statement, but there is an error and the previous token looks like a continuation of an expression... we should do the indentation!
            if (!isOnSameStatement && prevStatement?.HasErrors == true && IsLikelyExpressionContinuationToken(prevToken))
                useIndentationFromStatement = true;

            if (useIndentationFromStatement)
            {
                // start of a new statement, should have the same indentation as the previous statement
                return GetDefaultIndentationFromTokenLine(indenter, prevStatement?.GetFirstToken(includeZeroWidth: true) ?? prevToken, additionalSpace: 0);
            }

            // handle special cases based on the "current token"
            switch (token.Kind())
            {
                case SyntaxKind.CloseBraceToken:
                    {
                        if (isTokenOnNewline && token.Parent is BlockSyntax block && block.CloseBraceToken == token)
                        {
                            // we probably made a newline on the closing brace {|}

                            // check if the block is part of a lambda
                            if (block.Parent is LambdaExpressionSyntax lambda)
                            {
                                if (lambda.Parent is ArgumentSyntax arg)
                                {
                                    // it's an argument... so indent according to the method call
                                    if (arg.Parent is ArgumentListSyntax argList)
                                    {
                                        var isLastArg = argList.Arguments.Last() == arg;
                                        // so... this is a {...|} block and we have made a "newline"

                                        // if we are part of a variable declaration, and the last argument... then we put it at the same level as the var decl
                                        var varDecl = argList.GetFirstToken().GetAncestor(n => n is LocalDeclarationStatementSyntax) as LocalDeclarationStatementSyntax;

                                        // if it's in an invocation... follow the invocation indentation
                                        var argListParent = argList.Parent;
                                        if (isLastArg && (argListParent is InvocationExpressionSyntax || argListParent is MemberAccessExpressionSyntax))
                                        {
                                            if (IsOnSameLine(argListParent, varDecl))
                                            {
                                                int? additionalSpace = 0;
                                                if (prevToken.Kind() == SyntaxKind.OpenBraceToken && GetLineDiff(token, prevToken) > 1 && !isTokenWithin)
                                                {
                                                    // {|} - there is already a newline between those... so do an indentation
                                                    additionalSpace = null;
                                                }
                                                // use the indentation of the var decl when on same line
                                                return GetDefaultIndentationFromTokenLine(indenter, varDecl.GetFirstToken(includeZeroWidth: true), additionalSpace: additionalSpace);
                                            }

                                            // not on the same line... follow the indentation of the invocation syntax
                                            return GetDefaultIndentationFromTokenLine(indenter, argListParent.GetFirstToken(includeZeroWidth: true), additionalSpace: 0);
                                        }

                                        // otherwise just follow the indentation of the args
                                        return GetDefaultIndentationFromTokenLine(indenter, argList.GetFirstToken(includeZeroWidth: true), additionalSpace: 0);
                                    }
                                }
                            }
                        }

                        break;
                    }

                case SyntaxKind.DotToken:
                    {
                        if (isTokenOnNewline && token.Parent is MemberAccessExpressionSyntax memberAccess)
                        {
                            // we made a newline BEFORE a "." ... which could be a "member access" ... in which case we want to indent based on that indentation
                            var lastMemberToken = memberAccess.GetLastToken(predicate: t => {
                                // skip dot tokens
                                if (t.Kind() == SyntaxKind.DotToken) return false;

                                // skip tokens that are after our dot token
                                if (t.SpanStart > token.SpanStart) return false;

                                return true;
                            });

                            return GetIndentationFromTokenLine(indenter, lastMemberToken);
                        }

                        break;
                    }
            }

            // handle special cases base on the "previous token"
            switch (prevToken.Kind())
            {
                case SyntaxKind.DotToken:
                    {
                        if (isTokenOnNewline && prevToken.Parent is MemberAccessExpressionSyntax memberAccess)
                        {
                            // we made a newline AFTER a "." ... which could be a "member access" ... in which case we want to indent based on that indentation
                            var lastMemberToken = memberAccess.GetLastToken(predicate: t => {
                                // skip dot tokens
                                if (t.Kind() == SyntaxKind.DotToken) return false;

                                // skip tokens that are after our dot token
                                if (t.SpanStart > prevToken.SpanStart) return false;

                                return true;
                            });

                            return GetIndentationFromTokenLine(indenter, lastMemberToken);
                        }

                        break;
                    }
                case SyntaxKind.EqualsGreaterThanToken:
                    {
                        if (isTokenOnNewline)
                        {
                            // we made a newline AFTER a "=>" ... which means it's a lambda / member declaration of some sort and we should indent based on that
                            return GetIndentationFromTokenLine(indenter, prevToken);
                        }

                        break;
                    }
            }

            return null;
        }

        private static bool GetFirstTokenFromAnyNode<TNode>(SyntaxList<TNode> list, SyntaxToken token, out SyntaxToken firstToken, Func<TNode, SyntaxToken, bool> nodeCheckFn = null)
            where TNode : SyntaxNode
        {
            firstToken = default;
            if (!list.Span.Contains(token.Span)) return false;

            foreach (var n in list)
            {
                if (nodeCheckFn != null)
                {
                    if (nodeCheckFn(n, token))
                    {
                        firstToken = n.GetFirstToken(includeZeroWidth: true);
                        return !firstToken.IsNull;
                    }

                    return false;
                }
                else
                {
                    if (n.Span.Contains(token.Span))
                    {
                        firstToken = n.GetFirstToken(includeZeroWidth: true);
                        return !firstToken.IsNull;
                    }
                }
            }

            return false;
        }

        private static bool GetFirstTokenFromNode(StatementSyntax stat, SyntaxToken token, out SyntaxToken firstToken)
        {
            if (stat is BlockSyntax block)
                return GetFirstTokenFromNode(block, token, out firstToken);

            firstToken = default;
            if (stat?.Span.Contains(token.Span) == true)
            {
                // we are inside the block
                firstToken = stat.GetFirstToken(includeZeroWidth: true);
                return !firstToken.IsNull;
            }

            return false;
        }

        private static bool GetFirstTokenFromNode(BlockSyntax block, SyntaxToken token, out SyntaxToken firstToken)
        {
            firstToken = default;
            if (block?.Span.Contains(token.Span) == true)
            {
                // we are inside the block
                firstToken = block.GetFirstToken(includeZeroWidth: true);
                return !firstToken.IsNull;
            }

            return false;
        }

        private static bool GetFirstTokenFromNode(CSharpSyntaxNode body, SyntaxToken token, out SyntaxToken firstToken)
        {
            firstToken = default;
            if (body.GetLocation()?.SourceSpan.Contains(token.Span) == true)
            {
                // inside the body of the property, so indent according to the first token of that
                firstToken = body.GetFirstToken(includeZeroWidth: true);
                return !firstToken.IsNull;
            }

            return false;
        }

        private static bool GetFirstTokenFromBody(MemberDeclarationSyntax memberDecl, SyntaxToken token, out SyntaxToken firstToken)
        {
            firstToken = default;

            // indentation depends on whether we are within one of the body nodes or not!
            if (memberDecl.GetBody()?.GetLocation()?.SourceSpan.Contains(token.Span) == true)
            {
                // inside the body of the property, so indent according to the first token of that
                firstToken = memberDecl.GetBody().GetFirstToken(includeZeroWidth: true);
                return !firstToken.IsNull;
            }

            if (memberDecl.GetExpressionBody()?.GetLocation()?.SourceSpan.Contains(token.Span) == true)
            {
                // inside the expression body of the property, so indent according to the first token of that
                firstToken = memberDecl.GetExpressionBody().GetFirstToken(includeZeroWidth: true);
                return !firstToken.IsNull;
            }

            return false;
        }

        private static bool IsOnSameLine(SyntaxNode n1, SyntaxNode n2)
        {
            if (n1 == null || n2 == null) return false;

            var t1 = n1.GetFirstToken();
            var t2 = n2.GetFirstToken();

            return IsOnSameLine(t1, t2);
        }

        private static bool IsOnSameLine(SyntaxToken t1, SyntaxToken t2)
        {
            var t1Loc = t1.GetLocation().GetLineSpan();
            var t2Loc = t2.GetLocation().GetLineSpan();

            return t1Loc.StartLinePosition.Line == t2Loc.StartLinePosition.Line;
        }

        private static int GetLineDiff(SyntaxToken t1, SyntaxToken t2)
        {
            var t1Loc = t1.GetLocation().GetLineSpan();
            var t2Loc = t2.GetLocation().GetLineSpan();

            return System.Math.Abs(t1Loc.StartLinePosition.Line - t2Loc.StartLinePosition.Line);
        }

        private static IndentationResult GetIndentationBasedOnToken(Indenter indenter, SyntaxToken token)
        {
            Contract.ThrowIfNull(indenter.Tree);
            Contract.ThrowIfTrue(token.Kind() == SyntaxKind.None);

            // special cases
            // case 1: token belongs to verbatim token literal
            // case 2: $@"$${0}"
            // case 3: $@"Comment$$ in-between{0}"
            // case 4: $@"{0}$$"
            if (token.IsVerbatimStringLiteral() ||
                token.IsKind(SyntaxKind.InterpolatedVerbatimStringStartToken) ||
                token.IsKind(SyntaxKind.InterpolatedStringTextToken) ||
                (token.IsKind(SyntaxKind.CloseBraceToken) && token.Parent.IsKind(SyntaxKind.Interpolation)))
            {
                return indenter.IndentFromStartOfLine(0);
            }

            // if previous statement belong to labeled statement, don't follow label's indentation
            // but its previous one.
            if (token.Parent is LabeledStatementSyntax || token.IsLastTokenInLabelStatement())
            {
                token = token.GetAncestor<LabeledStatementSyntax>().GetFirstToken(includeZeroWidth: true).GetPreviousToken(includeZeroWidth: true);
            }

            var customResult = GetIndentationBasedOnTokenAndNode(indenter, token);
            if (customResult != null) return customResult.Value;

            var position = indenter.GetCurrentPositionNotBelongToEndOfFileToken(indenter.LineToBeIndented.Start);

            // first check operation service to see whether we can determine indentation from it
            var indentation = indenter.Finder.FromIndentBlockOperations(indenter.Tree, token, position, indenter.CancellationToken);
            if (indentation.HasValue)
            {
                return indenter.IndentFromStartOfLine(indentation.Value);
            }

            var alignmentTokenIndentation = indenter.Finder.FromAlignTokensOperations(indenter.Tree, token);
            if (alignmentTokenIndentation.HasValue)
            {
                return indenter.IndentFromStartOfLine(alignmentTokenIndentation.Value);
            }

            // if we couldn't determine indentation from the service, use heuristic to find indentation.
            var sourceText = indenter.LineToBeIndented.Text;

            // If this is the last token of an embedded statement, walk up to the top-most parenting embedded
            // statement owner and use its indentation.
            //
            // cases:
            //   if (true)
            //     if (false)
            //       Goo();
            //
            //   if (true)
            //     { }

            if (token.IsSemicolonOfEmbeddedStatement() ||
                token.IsCloseBraceOfEmbeddedBlock())
            {
                Debug.Assert(
                    token.Parent != null &&
                    (token.Parent.Parent is StatementSyntax || token.Parent.Parent is ElseClauseSyntax));

                var embeddedStatementOwner = token.Parent.Parent;
                while (embeddedStatementOwner.IsEmbeddedStatement())
                {
                    embeddedStatementOwner = embeddedStatementOwner.Parent;
                }

                return indenter.GetIndentationOfLine(sourceText.Lines.GetLineFromPosition(embeddedStatementOwner.GetFirstToken(includeZeroWidth: true).SpanStart));
            }

            switch (token.Kind())
            {
                case SyntaxKind.SemicolonToken:
                    {
                        // special cases
                        if (token.IsSemicolonInForStatement())
                        {
                            return GetDefaultIndentationFromToken(indenter, token);
                        }

                        return indenter.IndentFromStartOfLine(indenter.Finder.GetIndentationOfCurrentPosition(indenter.Tree, token, position, indenter.CancellationToken));
                    }

                case SyntaxKind.CloseBraceToken:
                    {
                        if (token.Parent.IsKind(SyntaxKind.AccessorList) &&
                            token.Parent.Parent.IsKind(SyntaxKind.PropertyDeclaration))
                        {
                            if (token.GetNextToken().IsEqualsTokenInAutoPropertyInitializers())
                            {
                                return GetDefaultIndentationFromToken(indenter, token);
                            }
                        }

                        return indenter.IndentFromStartOfLine(indenter.Finder.GetIndentationOfCurrentPosition(indenter.Tree, token, position, indenter.CancellationToken));
                    }

                case SyntaxKind.OpenBraceToken:
                    {
                        return indenter.IndentFromStartOfLine(indenter.Finder.GetIndentationOfCurrentPosition(indenter.Tree, token, position, indenter.CancellationToken));
                    }

                case SyntaxKind.ColonToken:
                    {
                        var nonTerminalNode = token.Parent;
                        Contract.ThrowIfNull(nonTerminalNode, @"Malformed code or bug in parser???");

                        if (nonTerminalNode is SwitchLabelSyntax)
                        {
                            return indenter.GetIndentationOfLine(sourceText.Lines.GetLineFromPosition(nonTerminalNode.GetFirstToken(includeZeroWidth: true).SpanStart), indenter.OptionSet.GetOption(FormattingOptions.IndentationSize, token.Language));
                        }

                        goto default;
                    }

                case SyntaxKind.CloseBracketToken:
                    {
                        var nonTerminalNode = token.Parent;
                        Contract.ThrowIfNull(nonTerminalNode, @"Malformed code or bug in parser???");

                        // if this is closing an attribute, we shouldn't indent.
                        if (nonTerminalNode is AttributeListSyntax)
                        {
                            return indenter.GetIndentationOfLine(sourceText.Lines.GetLineFromPosition(nonTerminalNode.GetFirstToken(includeZeroWidth: true).SpanStart));
                        }

                        goto default;
                    }

                case SyntaxKind.XmlTextLiteralToken:
                    {
                        return indenter.GetIndentationOfLine(sourceText.Lines.GetLineFromPosition(token.SpanStart));
                    }

                case SyntaxKind.CommaToken:
                    {
                        return GetIndentationFromCommaSeparatedList(indenter, token);
                    }

                case SyntaxKind.CloseParenToken:
                    {
                        if (token.Parent.IsKind(SyntaxKind.ArgumentList))
                        {
                            return GetDefaultIndentationFromToken(indenter, token.Parent.GetFirstToken(includeZeroWidth: true));
                        }

                        goto default;
                    }

                default:
                    {
                        return GetDefaultIndentationFromToken(indenter, token);
                    }
            }
        }

        private static IndentationResult GetIndentationFromCommaSeparatedList(Indenter indenter, SyntaxToken token)
            => token.Parent switch
            {
                BaseArgumentListSyntax argument => GetIndentationFromCommaSeparatedList(indenter, argument.Arguments, token),
                BaseParameterListSyntax parameter => GetIndentationFromCommaSeparatedList(indenter, parameter.Parameters, token),
                TypeArgumentListSyntax typeArgument => GetIndentationFromCommaSeparatedList(indenter, typeArgument.Arguments, token),
                TypeParameterListSyntax typeParameter => GetIndentationFromCommaSeparatedList(indenter, typeParameter.Parameters, token),
                EnumDeclarationSyntax enumDeclaration => GetIndentationFromCommaSeparatedList(indenter, enumDeclaration.Members, token),
                InitializerExpressionSyntax initializerSyntax => GetIndentationFromCommaSeparatedList(indenter, initializerSyntax.Expressions, token),
                _ => GetDefaultIndentationFromToken(indenter, token),
            };

        private static IndentationResult GetIndentationFromCommaSeparatedList<T>(
            Indenter indenter, SeparatedSyntaxList<T> list, SyntaxToken token) where T : SyntaxNode
        {
            var index = list.GetWithSeparators().IndexOf(token);
            if (index < 0)
            {
                return GetDefaultIndentationFromToken(indenter, token);
            }

            // find node that starts at the beginning of a line
            var sourceText = indenter.LineToBeIndented.Text;
            for (var i = (index - 1) / 2; i >= 0; i--)
            {
                var node = list[i];
                var firstToken = node.GetFirstToken(includeZeroWidth: true);

                if (firstToken.IsFirstTokenOnLine(sourceText))
                {
                    return indenter.GetIndentationOfLine(sourceText.Lines.GetLineFromPosition(firstToken.SpanStart));
                }
            }

            // smart indenter has a special indent block rule for comma separated list, so don't
            // need to add default additional space for multiline expressions
            return GetDefaultIndentationFromTokenLine(indenter, token, additionalSpace: 0);
        }

        private static IndentationResult GetDefaultIndentationFromToken(Indenter indenter, SyntaxToken token)
        {
            if (IsPartOfQueryExpression(token))
            {
                return GetIndentationForQueryExpression(indenter, token);
            }

            return GetDefaultIndentationFromTokenLine(indenter, token);
        }

        private static IndentationResult GetIndentationForQueryExpression(Indenter indenter, SyntaxToken token)
        {
            // find containing non terminal node
            var queryExpressionClause = GetQueryExpressionClause(token);
            if (queryExpressionClause == null)
            {
                return GetDefaultIndentationFromTokenLine(indenter, token);
            }

            // find line where first token of the node is
            var sourceText = indenter.LineToBeIndented.Text;
            var firstToken = queryExpressionClause.GetFirstToken(includeZeroWidth: true);
            var firstTokenLine = sourceText.Lines.GetLineFromPosition(firstToken.SpanStart);

            // find line where given token is
            var givenTokenLine = sourceText.Lines.GetLineFromPosition(token.SpanStart);

            if (firstTokenLine.LineNumber != givenTokenLine.LineNumber)
            {
                // do default behavior
                return GetDefaultIndentationFromTokenLine(indenter, token);
            }

            // okay, we are right under the query expression.
            // align caret to query expression
            if (firstToken.IsFirstTokenOnLine(sourceText))
            {
                return indenter.GetIndentationOfToken(firstToken);
            }

            // find query body that has a token that is a first token on the line
            if (!(queryExpressionClause.Parent is QueryBodySyntax queryBody))
            {
                return indenter.GetIndentationOfToken(firstToken);
            }

            // find preceding clause that starts on its own.
            var clauses = queryBody.Clauses;
            for (var i = clauses.Count - 1; i >= 0; i--)
            {
                var clause = clauses[i];
                if (firstToken.SpanStart <= clause.SpanStart)
                {
                    continue;
                }

                var clauseToken = clause.GetFirstToken(includeZeroWidth: true);
                if (clauseToken.IsFirstTokenOnLine(sourceText))
                {
                    return indenter.GetIndentationOfToken(clauseToken);
                }
            }

            // no query clause start a line. use the first token of the query expression
            return indenter.GetIndentationOfToken(queryBody.Parent.GetFirstToken(includeZeroWidth: true));
        }

        private static SyntaxNode GetQueryExpressionClause(SyntaxToken token)
        {
            var clause = token.GetAncestors<SyntaxNode>().FirstOrDefault(n => n is QueryClauseSyntax || n is SelectOrGroupClauseSyntax);

            if (clause != null)
            {
                return clause;
            }

            // If this is a query continuation, use the last clause of its parenting query.
            var body = token.GetAncestor<QueryBodySyntax>();
            if (body != null)
            {
                if (body.SelectOrGroup.IsMissing)
                {
                    return body.Clauses.LastOrDefault();
                }
                else
                {
                    return body.SelectOrGroup;
                }
            }

            return null;
        }

        private static bool IsPartOfQueryExpression(SyntaxToken token)
        {
            var queryExpression = token.GetAncestor<QueryExpressionSyntax>();
            return queryExpression != null;
        }

        private static IndentationResult GetDefaultIndentationFromTokenLine(
            Indenter indenter, SyntaxToken token, int? additionalSpace = null)
        {
            var spaceToAdd = additionalSpace ?? indenter.OptionSet.GetOption(FormattingOptions.IndentationSize, token.Language);

            var sourceText = indenter.LineToBeIndented.Text;

            // find line where given token is
            var givenTokenLine = sourceText.Lines.GetLineFromPosition(token.SpanStart);

            // find right position
            var position = indenter.GetCurrentPositionNotBelongToEndOfFileToken(indenter.LineToBeIndented.Start);

            // find containing non expression node
            var nonExpressionNode = token.GetAncestors<SyntaxNode>().FirstOrDefault(n => n is StatementSyntax);
            if (nonExpressionNode == null)
            {
                // well, I can't find any non expression node. use default behavior
                return indenter.IndentFromStartOfLine(indenter.Finder.GetIndentationOfCurrentPosition(indenter.Tree, token, position, spaceToAdd, indenter.CancellationToken));
            }

            // find line where first token of the node is
            var firstTokenLine = sourceText.Lines.GetLineFromPosition(nonExpressionNode.GetFirstToken(includeZeroWidth: true).SpanStart);

            // single line expression
            if (firstTokenLine.LineNumber == givenTokenLine.LineNumber)
            {
                return indenter.IndentFromStartOfLine(indenter.Finder.GetIndentationOfCurrentPosition(indenter.Tree, token, position, spaceToAdd, indenter.CancellationToken));
            }

            // okay, looks like containing node is written over multiple lines, in that case, give same indentation as given token
            return indenter.GetIndentationOfLine(givenTokenLine);
        }

        private static IndentationResult GetIndentationFromNodeLine(Indenter indenter, CSharpSyntaxNode node, SyntaxToken token, int? additionalSpace = null, bool includeSelf = false)
        {
            var relToken = node.GetFirstTokenOrFirstPrevious(token, includeSelf: includeSelf);
            return GetIndentationFromTokenLine(indenter, relToken, additionalSpace: additionalSpace);
        }

        private static IndentationResult GetIndentationFromTokenLine(Indenter indenter, SyntaxToken token, int? additionalSpace = null)
        {
            var spaceToAdd = additionalSpace ?? indenter.OptionSet.GetOption(FormattingOptions.IndentationSize, token.Language);

            var sourceText = indenter.LineToBeIndented.Text;

            // find line where given token is
            var givenTokenLine = sourceText.Lines.GetLineFromPosition(token.SpanStart);

            // find line where first token of the node is
            var firstTokenLine = sourceText.Lines.GetLineFromPosition(token.Parent.GetFirstToken(includeZeroWidth: true).SpanStart);

            if (firstTokenLine.LineNumber != givenTokenLine.LineNumber &&
                firstTokenLine.Span.IntersectsWith(token.Span) &&
                givenTokenLine.Span.IntersectsWith(token.Span))
            {
                // token spans over several lines... use same indentation as previous line
                return indenter.GetIndentationOfLine(givenTokenLine);
            }

            // use standard indentation otherwise
            var indent = firstTokenLine.GetColumnOfFirstNonWhitespaceCharacterOrEndOfLine(indenter.OptionSet.GetOption(FormattingOptions.IndentationSize, token.Language));
            return indenter.IndentFromStartOfLine(indent + spaceToAdd);
        }

        private static IndentationResult GetIndentationFromToken(Indenter indenter, SyntaxToken token, int? additionalSpace = null)
        {
            var spaceToAdd = additionalSpace ?? indenter.OptionSet.GetOption(FormattingOptions.IndentationSize, token.Language);

            // align indentation to match the token
            var indent = token.GetLocation().GetLineSpan().StartLinePosition.Character;

            return indenter.IndentFromStartOfLine(indent + spaceToAdd);
        }
    }
}
