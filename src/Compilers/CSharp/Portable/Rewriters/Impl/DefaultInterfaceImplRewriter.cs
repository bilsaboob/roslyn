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

            var boundNode = TryRewriteInterfaceObjectCreation(node);
            if (boundNode != null) return boundNode;

            return base.VisitObjectCreationExpression(node);
        }

        private BoundNode TryRewriteInterfaceObjectCreation(BoundObjectCreationExpression node)
        {
            var interfaceType = node.Type as NamedTypeSymbol;
            if (interfaceType is null) return null;

            // get or build the default interface implementation class
            var defaultImplType = DefaultInterfaceImplTypeGenerator.GetOrGenerate(Compilation, interfaceType, _diagnostics);
            if (defaultImplType is null) return null;

            // generate a new object creation but also convert it to the initial interface type
            var newObjectCreation = _F.New(defaultImplType, ImmutableArray<BoundExpression>.Empty, node.Syntax);
            var newConversion = _F.Convert(interfaceType, newObjectCreation, node.Syntax);
            return newConversion;
        }
    }
}
