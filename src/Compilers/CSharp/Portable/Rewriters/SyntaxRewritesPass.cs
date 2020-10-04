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
            // rewrite try/catch "inline method" statements to promote variable declarations to the outer scope
            block = MethodInlineTryCatchBlockRewriter.Rewrite(
                block,
                method,
                method.ContainingType,
                compilationState,
                diagnostics
            );

            // rewrite try/catch "local/normal" statements to promote variable declarations to the outer scope
            block = TryCatchLocalsScopeRewriter.Rewrite(
                block,
                method,
                method.ContainingType,
                compilationState,
                diagnostics
            );

            // rewrite if statement conditions to be converted to "boolean checked conditions" when possible 
            block = ImplicitIfConditionRewriter.Rewrite(
                block,
                method,
                method.ContainingType,
                compilationState,
                diagnostics
            );

            return block;
        }
    }
}
