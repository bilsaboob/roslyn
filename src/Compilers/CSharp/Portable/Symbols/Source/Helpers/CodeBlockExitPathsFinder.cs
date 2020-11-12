using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Source.Helpers
{
    internal sealed class CodeBlockExitPathsFinder : BoundTreeWalker
    {
        private readonly ArrayBuilder<(BoundNode, TypeWithAnnotations?)> _builder;

        private CodeBlockExitPathsFinder(ArrayBuilder<(BoundNode, TypeWithAnnotations?)> builder)
        {
            _builder = builder;
        }

        public static void GetExitPaths(ArrayBuilder<(BoundNode, TypeWithAnnotations?)> builder, BoundNode node)
        {
            var visitor = new CodeBlockExitPathsFinder(builder);
            visitor.Visit(node);
        }

        public static bool HasAnyTaskReturnTypes(BoundNode node, CSharpCompilation compilation)
        {
            var exitPaths = ArrayBuilder<(BoundNode, TypeWithAnnotations?)>.GetInstance();
            try
            {
                var visitor = new CodeBlockExitPathsFinder(exitPaths);
                visitor.Visit(node);
                return exitPaths.Any(p => p.Item2.HasValue && p.Item2.Value.Type?.IsAsyncTaskType(compilation) == true);
            }
            finally
            {
                exitPaths.Free();
            }
        }

        public override BoundNode Visit(BoundNode node)
        {
            if (!(node is BoundExpression))
            {
                return base.Visit(node);
            }

            return null;
        }

        protected override BoundExpression VisitExpressionWithoutStackGuard(BoundExpression node)
        {
            throw ExceptionUtilities.Unreachable;
        }

        public override BoundNode VisitLocalFunctionStatement(BoundLocalFunctionStatement node)
        {
            // Do not recurse into local functions; we don't want their returns.
            return null;
        }

        public override BoundNode VisitReturnStatement(BoundReturnStatement node)
        {
            var expression = node.ExpressionOpt;
            TypeSymbol? type = (expression is null) ? null : expression.Type?.SetUnknownNullabilityForReferenceTypes();
            if (type is null)
            {
                _builder.Add((node, null));
            }
            else
            {
                _builder.Add((node, TypeWithAnnotations.Create(type)));
            }

            return null;
        }
    }
}
