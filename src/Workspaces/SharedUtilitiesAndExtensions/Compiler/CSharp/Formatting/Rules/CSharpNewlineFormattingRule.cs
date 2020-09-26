using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis.CSharp.Extensions;

namespace Microsoft.CodeAnalysis.CSharp.Formatting
{
    internal class CSharpNewlineFormattingRule : BaseFormattingRule
    {
        internal CSharpNewlineFormattingRule()
        {
        }

        public override AdjustNewLinesOperation GetAdjustNewLinesOperation(in SyntaxToken previousToken, in SyntaxToken currentToken, in NextGetAdjustNewLinesOperation nextOperation)
        {
            var nextToken = currentToken.GetNextToken(includeZeroWidth: false, includeSkipped: false);
            var prevToken = currentToken.GetPreviousToken(includeZeroWidth: false, includeSkipped: false);

            // newline before "import" keyword - if the previous line was another UsingDirective or a NamespaceDeclaration
            if (!currentToken.IsKind(SyntaxKind.IdentifierToken, SyntaxKind.DotToken))
            {
                if (currentToken.IsKeyword() && currentToken.Text == "import")
                {
                    var prevTokenOfInterest = currentToken.FindPreviousToken(t => t.IsKind(SyntaxKind.NamespaceKeyword) || t.Text == "import" || t.IsFirstTokenOnLine(), includeZeroWidth: false, includeSkipped: false);
                    if (prevTokenOfInterest?.IsKind(SyntaxKind.NamespaceKeyword) == true)
                        return CreateAdjustNewLinesOperation(2, AdjustNewLinesOption.PreserveLines);
                    else if (prevTokenOfInterest?.Text == "import")
                        return CreateAdjustNewLinesOperation(1, AdjustNewLinesOption.PreserveLines);
                }
                else if (nextToken.IsKeyword() && nextToken.Text == "import")
                {
                    var prevTokenOfInterest = currentToken.FindPreviousToken(t => t.IsKind(SyntaxKind.NamespaceKeyword) || t.Text == "import" || t.IsFirstTokenOnLine(), includeZeroWidth: false, includeSkipped: false);
                    if (prevTokenOfInterest?.IsKind(SyntaxKind.NamespaceKeyword) == true)
                        return CreateAdjustNewLinesOperation(2, AdjustNewLinesOption.PreserveLines);
                    else if (prevTokenOfInterest?.Text == "import")
                        return CreateAdjustNewLinesOperation(1, AdjustNewLinesOption.PreserveLines);
                }
                else if (currentToken.IsKind(SyntaxKind.NamespaceKeyword) && !currentToken.IsFirstTokenOnLine())
                {
                    return CreateAdjustNewLinesOperation(2, AdjustNewLinesOption.PreserveLines);
                }
            }

            if (nextToken.IsKind(SyntaxKind.ClassKeyword))
            {
                var prevTokenOfInterest = currentToken.FindPreviousToken(t => t.Text == "import" || t.IsFirstTokenOnLine(), includeZeroWidth: false, includeSkipped: false);
                if (prevTokenOfInterest?.Text == "import")
                    return CreateAdjustNewLinesOperation(1, AdjustNewLinesOption.PreserveLines);
            }

            return base.GetAdjustNewLinesOperation(previousToken, currentToken, nextOperation);
        }
    }
}
