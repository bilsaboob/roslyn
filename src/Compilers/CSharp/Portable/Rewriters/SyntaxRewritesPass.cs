using System;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Rewriters
{
    internal class SyntaxRewritesPass
    {
        public static BoundBlock Rewrite(
            MethodSymbol method,
            BoundBlock block,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics,
            bool hasTrailingExpression,
            bool originalBodyNested)
        {
            // rewrite try/catch statements to promote variable declarations to the outer scope
            block = MethodInlineTryCatchBlockRewriter.Rewrite(
                method,
                method.ContainingType,
                block,
                compilationState,
                diagnostics
            );

            block = ImplicitIfConditionRewriter.Rewrite(
                method,
                method.ContainingType,
                block,
                compilationState,
                diagnostics
            );

            return block;
        }
    }
}
