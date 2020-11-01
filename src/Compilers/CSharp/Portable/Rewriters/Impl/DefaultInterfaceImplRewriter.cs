using System;
using System.Collections.Immutable;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Rewriters
{
    internal sealed class InterfaceObjectCreationRewriter : BoundTreeMethodRewriter<InterfaceObjectCreationRewriter>
    {
        public InterfaceObjectCreationRewriter()
        {
            VisitBinaryRecursive = true;
        }

        public override BoundNode VisitObjectCreationExpression(BoundObjectCreationExpression node)
        {
            if (!node.Type.IsInterfaceType())
                return base.VisitObjectCreationExpression(node);

            // replace with an object creation of the default implementation type

            return base.VisitObjectCreationExpression(node);
        }
    }
}
