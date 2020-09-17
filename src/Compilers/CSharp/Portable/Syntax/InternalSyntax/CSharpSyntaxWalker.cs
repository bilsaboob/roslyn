using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    internal abstract class CSharpInternalSyntaxWalker : CSharpSyntaxVisitor
    {
        protected SyntaxWalkerDepth Depth { get; }

        protected CSharpInternalSyntaxWalker(SyntaxWalkerDepth depth = SyntaxWalkerDepth.Node)
        {
            this.Depth = depth;
        }

        private int _recursionDepth;

        public override void Visit(CSharpSyntaxNode node)
        {
            if (node != null)
            {
                _recursionDepth++;
                StackGuard.EnsureSufficientExecutionStack(_recursionDepth);

                node.Accept(this);

                _recursionDepth--;
            }
        }

        public override void DefaultVisit(CSharpSyntaxNode node)
        {
            if (node == null) return;

            foreach (var child in node.ChildNodesAndTokens())
            {
                if (child.IsToken && child is SyntaxToken token)
                {
                    this.VisitToken(token);
                }
                else if (child is CSharpSyntaxNode childNode)
                {
                    this.Visit(childNode);
                }
            }
        }

        public override void VisitToken(SyntaxToken token)
        {
        }

        public override void VisitTrivia(SyntaxTrivia trivia)
        {
        }
    }
}
