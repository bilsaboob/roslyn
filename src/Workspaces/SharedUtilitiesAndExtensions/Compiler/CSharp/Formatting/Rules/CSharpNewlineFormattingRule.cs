using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using System;

namespace Microsoft.CodeAnalysis.CSharp.Formatting
{
    internal class CSharpNewlineFormattingRule : BaseFormattingRule
    {
        internal CSharpNewlineFormattingRule()
        {
        }

        public override AdjustNewLinesOperation GetAdjustNewLinesOperation(in SyntaxToken previousToken, in SyntaxToken currentToken, in NextGetAdjustNewLinesOperation nextOperation)
        {
            var prevToken = currentToken.GetPreviousToken(includeZeroWidth: false, includeSkipped: false);
            var nextToken = currentToken.GetNextToken(includeZeroWidth: false, includeSkipped: false);

            var op = EvalNewline(prevToken, currentToken, nextToken);
            if (op != null) return op;

            return base.GetAdjustNewLinesOperation(previousToken, currentToken, nextOperation);
        }

        private AdjustNewLinesOperation EvalNewline(SyntaxToken prevToken, SyntaxToken currentToken, SyntaxToken nextToken)
        {
            var currentNode = currentToken.Parent?.FirstAncestorOrSelf(n => n is UsingDirectiveSyntax || n is TypeDeclarationSyntax || n is NamespaceDeclarationSyntax);

            // handle "import import" scenarion - text is injected by the editor right after a previous import at the same line...
            if (currentToken.IsKind(SyntaxKind.SemicolonToken))
            {
                // we are at the semicomma

                // handle the case where an import / namespace was pasted
                if (nextToken.IsKind(SyntaxKind.ImportKeyword) ||
                    nextToken.IsKind(SyntaxKind.UsingKeyword) ||
                    nextToken.IsKind(SyntaxKind.NamespaceKeyword))
                {
                    if (GetLineDiff(currentToken, nextToken) == 0)
                        return Newlines(1);
                    return null;
                }

                // next we do additional checks only if the semicomma is part of a using statement
                if (currentNode is UsingDirectiveSyntax)
                {
                    // check for following statements of interest
                    var nextNode = nextToken.Parent?.FirstAncestorOrSelf(n =>
                        n is TypeDeclarationSyntax ||
                        n is MethodDeclarationSyntax ||
                        n is LocalDeclarationStatementSyntax ||
                        n is StatementSyntax
                    );

                    if (nextNode != null)
                    {
                        // semicomma is followed by a statement ... we add an additional line!
                        var lineDiff = GetLineDiff(currentToken, nextToken);
                        if (lineDiff <= 1)
                            return Newlines(2);
                        if (lineDiff == 1)
                            return Newlines(1);
                        else 
                            return null;
                    }
                }
            }

            return null;
        }

        private static int GetLineDiff(SyntaxToken t1, SyntaxToken t2)
        {
            if (t1.IsNull || t2.IsNull) return 0;

            var t1StartLine = t1.GetLocation().GetLineSpan().StartLinePosition.Line;
            var t2StartLine = t2.GetLocation().GetLineSpan().StartLinePosition.Line;
            return Math.Abs(t1StartLine - t2StartLine);
        }

        private static AdjustNewLinesOperation Newlines(int numLines, AdjustNewLinesOption option = AdjustNewLinesOption.ForceLines)
            => CreateAdjustNewLinesOperation(numLines, option);
    }
}
