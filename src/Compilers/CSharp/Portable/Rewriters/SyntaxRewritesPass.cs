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
            block = MethodInlineTryCatchBlockRewriter.Rewrite(
                        method,
                        method.ContainingType,
                        block,
                        compilationState,
                        diagnostics);

            return block;
        }
    }
}
