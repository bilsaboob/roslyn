using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using System;
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.CodeAnalysis.CSharp.Formatting
{
    internal class CSharpNewlineFormattingRule : BaseFormattingRule
    {
        internal CSharpNewlineFormattingRule()
        {
        }

        public override AdjustNewLinesOperation GetAdjustNewLinesOperation(in SyntaxToken previousToken, in SyntaxToken currentToken, in NextGetAdjustNewLinesOperation nextOperation, FormattingReason reason)
        {
            var op = EvalNewlineForBraces(currentToken);
            if (op != null) return op;

            op = EvalNewlineForSemicolon(previousToken, currentToken, reason);
            if (op != null) return op;

            op = EvalNewlineForTopStatements(previousToken, currentToken);
            if (op != null) return op;

            op = base.GetAdjustNewLinesOperation(previousToken, currentToken, nextOperation, reason);
            return op;
        }

        private AdjustNewLinesOperation EvalNewlineForBraces(SyntaxToken currentToken)
        {
            // only consider "real tokens" ... which have some actual length ... otherwise it may be "fake tokens"
            if (currentToken.Width() == 0) return null;

            if (currentToken.IsKind(
                SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken,
                SyntaxKind.OpenBracketToken, SyntaxKind.CloseBracketToken,
                SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken,
                SyntaxKind.LessThanToken, SyntaxKind.GreaterThanToken
                ))
            {
                // don't adjust anything for brace pairs
                return CreateAdjustNewLinesOperation(0, AdjustNewLinesOption.PreserveLines);
            }

            return null;
        }

        private AdjustNewLinesOperation EvalNewlineForSemicolon(SyntaxToken previousToken, SyntaxToken currentToken, FormattingReason reason)
        {
            if (reason != FormattingReason.CopyPasteAction && reason != FormattingReason.CommandAction) return null;

            var currentDecl = currentToken.Parent?.GetAncestorOrThis(n => IsTopDeclaration(n));
            // we only handle for supported top statements
            if (currentDecl is null) return null;

            // we only handle special case for copy & paste actions
            if (previousToken.Width() == 0) previousToken = currentToken.GetPreviousToken();
            var prevDecl = previousToken.Parent?.GetAncestorOrThis(n => IsTopDeclaration(n));

            // calculate the line diff
            var lineDiff = GetLineDiff(previousToken, currentToken);
            var onSameLine = lineDiff == 0;

            // if it's a fake semicomma token and it's on a newline... force it back on the previous line so that it stays together with the declaration
            if (!onSameLine && currentToken.IsKind(SyntaxKind.SemicolonToken) && currentToken.Width() == 0 && prevDecl == currentDecl)
            {
                // preserve the lines, but decrease with 1
                var lines = 0;
                var options = AdjustNewLinesOption.ForceLines;

                var nextToken = currentToken.GetNextToken();
                var nextLineDiff = GetLineDiff(currentToken, nextToken);
                if (nextLineDiff > 0)
                {
                    // keep additional lines if the next token isn't on the same line
                    lines = lineDiff;
                    options = AdjustNewLinesOption.PreserveLines;
                }

                // for members / variables, we don't do anything
                switch (currentDecl)
                {
                    case NamespaceDeclarationSyntax:
                    case UsingDirectiveSyntax:
                    case AttributeSyntax:
                        return CreateAdjustNewLinesOperation(lines, options);
                }
            }

            return null;
        }

        private AdjustNewLinesOperation EvalNewlineForTopStatements(SyntaxToken previousToken, SyntaxToken currentToken)
        {
            var currentDecl = currentToken.Parent?.GetAncestorOrThis(n => IsTopDeclaration(n));
            // we only handle for supported top statements
            if (currentDecl is null) return null;

            // don't apply this to "members" that are not directly beneath a namespace
            if (!(currentDecl.Parent is NamespaceDeclarationSyntax))
                return null;

            // if we have a "fake token" ... check the previous "visible token" instead
            if (previousToken.Width() == 0) previousToken = currentToken.GetPreviousToken();
            var prevDecl = previousToken.Parent?.GetAncestorOrThis(n => IsTopDeclaration(n));

            // calculate the line diff
            var lineDiff = GetLineDiff(previousToken, currentToken);
            var onSameLine = lineDiff == 0;
            var linesCount = 0;

            // if the first token of the declaration (any of the ones) is the current token - it should always be on a newline
            var currentDeclFirstToken = currentDecl?.GetFirstToken();
            if (currentToken == currentDeclFirstToken)
            {
                linesCount += 1;

                if (currentDecl is UsingDirectiveSyntax && prevDecl is NamespaceDeclarationSyntax)
                {
                    // import following a namespace should have 1 additional line space
                    linesCount += 1;
                }
                else if (IsGlobalMember(currentDecl))
                {
                    // a global member that has a previous neighbour that is a global member ... no space is needed
                    if (IsGlobalMember(prevDecl)) return null;

                    // previous is import or namespace?
                    if (IsUsingOrNamespace(prevDecl))
                    {
                        // a "member" that has a namespace or using directive before, should have an additional line
                        if (lineDiff <= 1)
                            linesCount += 1;
                    }
                }
            }

            // no need to force anything if no expected line count
            if (linesCount <= 0) return null;

            return CreateAdjustNewLinesOperation(linesCount, AdjustNewLinesOption.PreserveLines);
        }

        private bool IsUsingOrNamespace(SyntaxNode n)
        {
            return
                n is UsingDirectiveSyntax ||
                n is NamespaceDeclarationSyntax;
        }

        private bool IsGlobalMember(SyntaxNode n)
        {
            return
                n is MethodDeclarationSyntax ||
                n is PropertyDeclarationSyntax ||
                n is FieldDeclarationSyntax;
        }

        private bool IsTopDeclaration(SyntaxNode n)
        {
            switch(n.Kind())
            {
                case SyntaxKind.UsingDirective:
                case SyntaxKind.NamespaceDeclaration:

                case SyntaxKind.ClassDeclaration:
                case SyntaxKind.InterfaceDeclaration:
                case SyntaxKind.StructDeclaration:
                case SyntaxKind.EnumDeclaration:
                case SyntaxKind.DelegateDeclaration:

                case SyntaxKind.Attribute:
                case SyntaxKind.FieldDeclaration:
                case SyntaxKind.PropertyDeclaration:
                case SyntaxKind.MethodDeclaration:
                    return true;
            }

            return false;
        }

        private static int GetLineDiff(SyntaxToken t1, SyntaxToken t2)
        {
            if (t1.IsNull || t2.IsNull) return 0;

            var t1StartLine = t1.GetLocation().GetLineSpan().StartLinePosition.Line;
            var t2StartLine = t2.GetLocation().GetLineSpan().StartLinePosition.Line;
            return Math.Abs(t1StartLine - t2StartLine);
        }
    }
}
