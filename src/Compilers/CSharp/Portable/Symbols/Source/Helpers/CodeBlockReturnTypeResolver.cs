using System.Linq;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Source.Helpers
{
    internal class CodeBlockReturnTypeResolver
    {
        public static (TypeWithAnnotations? resolvedType, bool isVoidType) TryResolveReturnType(BoundNode? boundNode)
        {
            bool isInferrableReturnStatement(BoundNode node)
            {
                if(node is BoundReturnStatement ret)
                {
                    return true;
                }

                return false;
            }

            var exitPaths = ArrayBuilder<(BoundNode, TypeWithAnnotations)>.GetInstance();
            try
            {
                CodeBlockExitPathsFinder.GetExitPaths(exitPaths, boundNode);

                // get the exit paths that can be infered from
                var inferrableExitPaths = exitPaths.Where(p => isInferrableReturnStatement(p.Item1));

                // use the last exit path and try to infer from it
                var lastExitPath = inferrableExitPaths.LastOrDefault();
                if (lastExitPath.Item1 != null)
                {
                    // there is some return, so lets use the last return statement
                    var exitPathReturnType = lastExitPath.Item2;

                    // if the return type is null... then there is some issue... we only use the return type if we have a valid one...
                    if (!ReferenceEquals(exitPathReturnType.Type, null))
                        return (exitPathReturnType, false);

                    return (null, false);
                }
                else
                {
                    // there is no return, so lets make it "void" per default
                    return (null, true);
                }
            }
            finally
            {
                exitPaths.Free();
            }
        }
    }
}
