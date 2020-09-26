// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Indentation;
using Microsoft.CodeAnalysis.Shared.Extensions;
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
            => TryGetDesiredIndentation(indenter, triviaOpt) ??
               TryGetDesiredIndentation(indenter, tokenOpt);

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

        private static IndentationResult? GetIndentationBasedOnTokenAndNode(Indenter indenter, SyntaxToken prevToken)
        {
            var sourceText = indenter.LineToBeIndented.Text;

            var tokenOpt = indenter.TryGetCurrentVisibleToken(out var isTokenWithin);
            var token = tokenOpt ?? prevToken;

            // if the token is on a new line, we can handle some special cases
            var isTokenOnNewline = prevToken.IsLastTokenOnLine(sourceText);
            if (isTokenOnNewline)
            {
                if (token.Parent?.Parent?.IsKind(SyntaxKind.NamespaceDeclaration) == true)
                {
                    // since we are directly in a namespace, check if it's a flat namespace, and in that case don't indent
                    var nsToken = token.Parent.Parent.GetFirstToken().GetNextToken();
                    var openBraceToken = nsToken.FindNextToken(t => t.IsKind(SyntaxKind.OpenBraceToken) || t.IsFirstTokenOnLine(sourceText));
                    if (openBraceToken?.Kind() != SyntaxKind.OpenBraceToken)
                        return GetIndentationFromTokenLine(indenter, token, additionalSpace: 0);
                }
            }

            // for the remainder we really need the token
            if (tokenOpt == null) return null;
            isTokenOnNewline = token.IsFirstTokenOnLine(sourceText);
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

        private static IndentationResult GetIndentationFromTokenLine(Indenter indenter, SyntaxToken token, int? additionalSpace = null)
        {
            var spaceToAdd = additionalSpace ?? indenter.OptionSet.GetOption(FormattingOptions.IndentationSize, token.Language);

            var sourceText = indenter.LineToBeIndented.Text;

            // find line where given token is
            var givenTokenLine = sourceText.Lines.GetLineFromPosition(token.SpanStart);

            // find line where first token of the node is
            var firstTokenLine = sourceText.Lines.GetLineFromPosition(token.Parent.GetFirstToken(includeZeroWidth: true).SpanStart);

            // single line expression
            if (firstTokenLine.LineNumber == givenTokenLine.LineNumber)
            {
                var indent = firstTokenLine.GetColumnOfFirstNonWhitespaceCharacterOrEndOfLine(indenter.OptionSet.GetOption(FormattingOptions.IndentationSize, token.Language));
                return indenter.IndentFromStartOfLine(indent + spaceToAdd);
            }

            // okay, looks like containing node is written over multiple lines, in that case, give same indentation as given token
            return indenter.GetIndentationOfLine(givenTokenLine);
        }
    }
}
