using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    internal sealed class AwaitExpressionsInternalSyntaxFinder : CSharpInternalSyntaxWalker
    {
        public bool HasValidAwaitExpressions { get; private set; }

        public static bool HasNonNestedAwaitExpressions(CSharpSyntaxNode node)
        {
            if (node == null) return false;
            var visitor = new AwaitExpressionsInternalSyntaxFinder();
            visitor.Visit(node);
            return visitor.HasValidAwaitExpressions;
        }

        public override void Visit(CSharpSyntaxNode node)
        {
            if (node is LambdaExpressionSyntax) return;

            base.Visit(node);
        }

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            // Do not returse into local lambdas ... 
        }

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            // Do not returse into local lambdas ... 
        }

        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            // Do not returse into local lambdas ... 
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            // Do not recurse into local functions ...
        }

        public override void VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            HasValidAwaitExpressions = true;
        }
    }
}
