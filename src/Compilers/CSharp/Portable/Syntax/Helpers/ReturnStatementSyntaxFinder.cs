using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    internal class ReturnStatementSyntaxFinder : CSharpSyntaxWalker
    {
        private ExpressionSyntax BodyExpressionSyntax { get; set; }
        private List<ReturnStatementSyntax> ReturnStatements { get; set; } = new List<ReturnStatementSyntax>();

        public static List<ReturnStatementSyntax> FindReturnStatements(CSharpSyntaxNode node, bool includeExpressionBody)
        {
            var finder = new ReturnStatementSyntaxFinder();
            finder.Visit(node);
            var returnStats = finder.ReturnStatements;
            if (returnStats.Count == 0 && finder.BodyExpressionSyntax != null && includeExpressionBody)
                returnStats.Add(SyntaxFactory.ReturnStatement(finder.BodyExpressionSyntax));

            return returnStats;
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            BodyExpressionSyntax = node.GetExpressionBodySyntax()?.Expression;
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            BodyExpressionSyntax = node.GetExpressionBodySyntax()?.Expression;
            base.VisitIndexerDeclaration(node);
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            BodyExpressionSyntax = node.GetExpressionBodySyntax()?.Expression;
            base.VisitEventDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            BodyExpressionSyntax = node.GetExpressionBodySyntax()?.Expression;
            base.VisitMethodDeclaration(node);
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            ReturnStatements.Add(node);
        }
    }
}
