using System;
using System.Collections.Immutable;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Rewriters
{
    internal sealed class ImplicitIfConditionRewriter : BoundTreeMethodRewriter<ImplicitIfConditionRewriter>
    {
        public ImplicitIfConditionRewriter()
        {
            VisitBinaryRecursive = true;
        }

        protected bool IsInIfStatementCondition { get; set; }

        public override BoundNode VisitIfStatement(BoundIfStatement node)
        {
            // Special handling for:
            // 1. if condition is a local declaration
            // 2. everything else...

            var wasInIfStatementCondition = IsInIfStatementCondition;
            IsInIfStatementCondition = true;
            try
            {
                return base.VisitIfStatement(node);
            }
            finally
            {
                IsInIfStatementCondition = wasInIfStatementCondition;
            }
        }

        public override BoundNode VisitConversion(BoundConversion node)
        {
            var newNode = TryRewriteConditionExpression(node);
            if (newNode != null) return newNode;
            return base.VisitConversion(node);
        }

        private BoundNode TryRewriteConditionExpression(BoundConversion node)
        {
            if (!IsInIfStatementCondition) return null;

            // no need to do anything if we have a valid conversion
            if (node.Syntax?.IsExpectedRewrite() == false) return null;

            // we need to rewrite the operand expression into a "expr != null" if it's a nullable
            if (node.Operand.Type is null || node.Operand.Type.IsNonNullableValueType()) return null;

            // OK, it's a nullable, we can do the condition check
            var newCondition = _F.Binary(BinaryOperatorKind.NotEqual, node.Type, node.Operand, _F.Null(node.Operand.Type));
            var newConversion = _F.Convert(node.Type, newCondition);

            return newConversion;
        }
    }
}
