using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.Source.Helpers
{
    internal class CodeBlockReturnTypeResolver
    {
        public static (TypeWithAnnotations? resolvedType, bool isVoidType) TryResolveReturnTypeFromSyntax(CSharpSyntaxNode node, Binder binder, ConversionsBase conversions, bool includeExpressionBody = true)
        {
            var returnStatements = Syntax.ReturnStatementSyntaxFinder.FindReturnStatements(node, includeExpressionBody);
            var exitPaths = ArrayBuilder<(BoundNode, TypeWithAnnotations?)>.GetInstance();
            var tmpDiagnostics = DiagnosticBag.GetInstance();
            try
            {
                foreach (var returnStat in returnStatements)
                {
                    try
                    {
                        var boundStat = binder.BindStatement(returnStat, tmpDiagnostics);

                        TypeSymbol? statType = null;
                        if (boundStat is BoundReturnStatement boundReturnStat)
                        {
                            statType = boundReturnStat.ExpressionOpt?.Type?.SetUnknownNullabilityForReferenceTypes();
                        }
                        else if (boundStat is BoundExpressionStatement boundExprStat)
                        {
                            statType = boundExprStat.Expression?.Type?.SetUnknownNullabilityForReferenceTypes();
                        }

                        if (statType is null || statType.IsErrorType())
                        {
                            exitPaths.Add((boundStat, null));
                        }
                        else
                        {
                            exitPaths.Add((boundStat, TypeWithAnnotations.Create(statType)));
                        }
                    }
                    catch (System.Exception)
                    {
                        // ignore this... we are doing "our best" here...
                    }
                }

                return TryResolveReturnType(exitPaths, conversions, out _);
            }
            finally
            {
                exitPaths.Free();
                tmpDiagnostics.Free();
            }
        }

        public static (TypeWithAnnotations? resolvedType, bool isVoidType) TryResolveReturnType(BoundNode? boundNode, ConversionsBase conversions)
        {
            return TryResolveReturnType(boundNode, conversions, out var _);
        }

        public static (TypeWithAnnotations? resolvedType, bool isVoidType) TryResolveReturnType(BoundNode? boundNode, ConversionsBase conversions, out HashSet<DiagnosticInfo> useSiteDiagnostics)
        {
            var exitPaths = ArrayBuilder<(BoundNode, TypeWithAnnotations?)>.GetInstance();
            try
            {
                CodeBlockExitPathsFinder.GetExitPaths(exitPaths, boundNode);
                return TryResolveReturnType(exitPaths, conversions, out useSiteDiagnostics);
            }
            finally
            {
                exitPaths.Free();
            }
        }

        public static (TypeWithAnnotations? resolvedType, bool isVoidType) TryResolveReturnType(ArrayBuilder<(BoundNode, TypeWithAnnotations?)> exitPaths, ConversionsBase conversions, out HashSet<DiagnosticInfo> useSiteDiagnostics)
        {
            bool isInferrableReturnStatement(BoundNode node)
            {
                if(node is BoundReturnStatement ret)
                {
                    return true;
                }

                return false;
            }

            useSiteDiagnostics = null;
            // get the exit paths that can be infered from
            var inferrableExitPaths = exitPaths.Where(p => isInferrableReturnStatement(p.Item1)).ToList();

            TypeWithAnnotations? resolvedType = null;
            if (inferrableExitPaths.Count > 1)
            {
                var types = ArrayBuilder<TypeSymbol>.GetInstance();
                // try finding the best common type
                try
                {
                    TypeWithAnnotations? lastType = null;
                    foreach (var p in inferrableExitPaths)
                    {
                        if (p.Item1 == null) continue;

                        var type = p.Item2;
                        var t = type?.Type;
                        if (ReferenceEquals(t, null)) continue;

                        lastType = type;
                        types.Add(t);
                    }

                    // no valid type to resolve from - must be void then
                    if (types.Count == 0) return (null, true);

                    // if we have more than one type to resolve from...
                    if (types.Count > 1)
                    {
                        useSiteDiagnostics = new HashSet<DiagnosticInfo>();
                        var bestType = BestTypeInferrer.GetBestType(types, conversions, ref useSiteDiagnostics);

                        // if no best type ... then we don't know ...
                        if ((object)bestType == null) return (null, false);

                        // we have a resolved type
                        resolvedType = TypeWithAnnotations.Create(false, bestType);
                        return (resolvedType, false);
                    }

                    // there is only a single type to resolve from
                    if (!ReferenceEquals(lastType?.Type, null))
                        return (lastType, false);

                    // no valid type... so must be void
                    return (null, true);
                }
                finally
                {
                    types.Free();
                }
            }
            else
            {
                var lastExitPath = inferrableExitPaths.LastOrDefault();
                if (lastExitPath.Item1 != null)
                {
                    // there is some return, so lets use the last return statement
                    var exitPathReturnType = lastExitPath.Item2;

                    // if the return type is null... then there is some issue... we only use the return type if we have a valid one...
                    if (!ReferenceEquals(exitPathReturnType?.Type, null))
                        return (exitPathReturnType, false);

                    return (null, false);
                }
                else
                {
                    // there is no return, so lets make it "void" per default
                    return (null, true);
                }
            }
        }
    }
}
