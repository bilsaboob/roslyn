using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Cci;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal partial class GeneratedTypesManager
    {
        internal class MethodMemberBuilder : TypeMemberBuilder
        {
            private GeneratedMethodMemberDescriptor _descriptor;

            internal MethodMemberBuilder(GeneratedMethodMemberDescriptor descriptor)
            {
                _descriptor = descriptor;
            }

            internal override Symbol Build(GeneratedTypeSymbol type, GeneratedTypeDescriptor td, int memberIndex, DiagnosticBag diagnostics)
            {
                // create the property
                var method = new GeneratedOrdinaryMethodSymbol(
                    _descriptor,
                    MethodKind.Ordinary
                );

                // finalize / build the method
                method.Build(
                    containingType: type,
                    bodyGenerator: F => CreateMethodBodyReturningDefaultValue(F, method)
                );

                return method;
            }

            public MethodMemberBuilder FromSymbol(MethodSymbol method)
            {
                _descriptor.Name = method.Name;
                _descriptor.Type = method.ReturnTypeWithAnnotations;
                _descriptor.Arity = method.Arity;

                _descriptor.Accessibility = method.DeclaredAccessibility;
                _descriptor.IsExtern = method.IsExtern;

                _descriptor.IsStatic = method.IsStatic;
                _descriptor.IsAsync = method.IsAsync;

                _descriptor.IsVirtual = method.IsVirtual;
                _descriptor.IsAbstract = method.IsAbstract;

                _descriptor.IsVararg = method.IsVararg;

                _descriptor.TypeParameters = method.TypeParameters;

                if (method.ParameterCount > 0)
                {
                    var paramsBuilder = ArrayBuilder<ParameterSymbol>.GetInstance();
                    for (var i = 0; i < method.ParameterCount; ++i)
                    {
                        var p = method.Parameters[i];

                        var pd = new GeneratedParameterDescriptor();
                        pd.Name = p.Name;
                        pd.Type = p.TypeWithAnnotations;
                        pd.IsOptional = p.IsOptional;
                        pd.IsIn = p.IsMetadataIn;
                        pd.IsOut = p.IsMetadataOut;

                        pd.RefKind = p.RefKind;
                        pd.RefCustomModifiers = p.RefCustomModifiers;

                        pd.ExplicitDefaultConstantValue = p.ExplicitDefaultConstantValue;

                        var param = new GeneratedParameterSymbol(pd, i);
                        paramsBuilder.Add(param);
                    }
                    _descriptor.Parameters = paramsBuilder.ToImmutableAndFree();
                }

                if (method.TypeParameters.Length > 0)
                {
                    _descriptor.IsFinal = true;

                    var paramsBuilder = ArrayBuilder<TypeParameterSymbol>.GetInstance();
                    for (var i = 0; i < method.TypeParameters.Length; ++i)
                    {
                        var p = method.TypeParameters[i];

                        var pd = new GeneratedTypeParameterDescriptor();
                        pd.Name = p.Name;
                        pd.Variance = p.Variance;
                        pd.ConstraintTypes = p.ConstraintTypesNoUseSiteDiagnostics;

                        pd.HasConstructorConstraint = p.HasConstructorConstraint;
                        pd.HasReferenceTypeConstraint = p.HasReferenceTypeConstraint;
                        pd.HasNotNullConstraint = p.HasNotNullConstraint;
                        pd.HasValueTypeConstraint = p.HasValueTypeConstraint;
                        pd.HasUnmanagedTypeConstraint = p.HasUnmanagedTypeConstraint;

                        var param = new GeneratedTypeParameterSymbol(pd, i);
                        paramsBuilder.Add(param);
                    }
                    _descriptor.TypeParameters = paramsBuilder.ToImmutableAndFree();
                }

                return this;
            }

            public MethodMemberBuilder ForInterface(NamedTypeSymbol interfaceType, bool isExplicit = false, Symbol interfaceMember = null)
            {
                _descriptor.IsInterfaceImplementation = true;
                _descriptor.Interface = interfaceType;
                // cannot be abstract for interface implementation
                _descriptor.IsAbstract = false;
                // interface implementation requires this
                _descriptor.IsVirtual = true;

                if (isExplicit)
                {
                    _descriptor.Accessibility = Accessibility.Private;
                    _descriptor.ExplicitInterfaceMember = interfaceMember;

                    // build an explicit interface member name
                    var name = _descriptor.Name;
                    if (string.IsNullOrEmpty(name))
                        name = ExplicitInterfaceHelpers.GetMemberNameWithoutInterfaceName(interfaceMember.Name);
                    name = ExplicitInterfaceHelpers.GetMemberName(name, interfaceType, null);
                    _descriptor.Name = name;
                }

                return this;
            }

            #region Bound node factories

            private BoundStatement CreateMethodBodyReturningDefaultValue(SyntheticBoundNodeFactory F, GeneratedOrdinaryMethodSymbol method)
            {
                var statements = ArrayBuilder<BoundStatement>.GetInstance();

                try
                {
                    // init parameters - out params
                    var paramStatements = CreateParamInitStatements(F, method);
                    statements.AddRange(paramStatements);

                    // finally add the return statement
                    var returnStat = CreateReturnStatement(F, method);
                    statements.Add(returnStat);

                    // method body block
                    return F.Block(statements.ToImmutable());
                }
                finally
                {
                    statements.Free();
                }
            }

            private ImmutableArray<BoundStatement> CreateParamInitStatements(SyntheticBoundNodeFactory F, GeneratedOrdinaryMethodSymbol method)
            {
                var outParams = method.Parameters.Where(p => p.IsMetadataOut).ToImmutableArray();
                if (outParams.Length == 0) return ImmutableArray<BoundStatement>.Empty;

                var statements = ArrayBuilder<BoundStatement>.GetInstance();
                try
                {
                    foreach (var outParam in outParams)
                    {
                        var defaultValue = CreateDefaultValueExpression(F, outParam.Type);
                        var assignStat = F.Assignment(F.Parameter(outParam), defaultValue);
                        statements.Add(assignStat);
                    }

                    return statements.ToImmutable();
                }
                finally
                {
                    statements.Free();
                }
            }

            private BoundReturnStatement CreateReturnStatement(SyntheticBoundNodeFactory F, GeneratedOrdinaryMethodSymbol method)
            {
                var returnType = method.ReturnType;
                if (Manager.KnownSymbols.System_Void.Equals(returnType))
                    return F.Return();

                TypeSymbol? forcedReturnType = null;

                // if the return type has an "original type" annotation, we should use it instead!
                if (!(_descriptor.Type.AnnotationType is null) && _descriptor.Type.AnnotationTypeKind == TypeAnnotationKind.OriginalType)
                {
                    returnType = _descriptor.Type.AnnotationType;

                    // we will have to force the method to accept this
                    forcedReturnType = returnType;

                    if (Manager.KnownSymbols.System_Void.Equals(returnType))
                        return F.Return();
                }

                // we need to return a default value for the method
                var defaultValue = CreateDefaultValueExpression(F, returnType);
                if (defaultValue != null)
                {
                    return F.Return(expression: defaultValue, forceType: forcedReturnType);
                }
                else
                {
                    return F.Return();
                }
            }

            private BoundExpression CreateDefaultValueExpression(SyntheticBoundNodeFactory F, TypeSymbol returnType)
            {
                // we need to return a default value for the method
                BoundExpression defaultValue = null;

                // handle special cases
                var hasOriginalDefinition = !(returnType.OriginalDefinition is null);

                if (hasOriginalDefinition && Manager.KnownSymbols.System_Threading_Tasks_Task_T.Equals(returnType.OriginalDefinition))
                {
                    // we have a task that returns a value... so return a static return type value
                    var tp = returnType.GetMemberTypeArgumentsNoUseSiteDiagnostics().FirstOrDefault();
                    var innerDefaultValue = CreateDefaultValueExpression(F, tp);

                    if (_descriptor.IsAsync)
                    {
                        defaultValue = innerDefaultValue;
                    }
                    else
                    {
                        // "Task.FromResult()"
                        var fromResultT = Manager.KnownMembers.System_Threading_Tasks_Task_FromResult.Construct(tp);
                        defaultValue = F.StaticCall(fromResultT, ImmutableArray.Create(innerDefaultValue));
                    }
                }
                else if (Manager.KnownSymbols.System_Threading_Tasks_Task.Equals(returnType))
                {
                    if (_descriptor.IsAsync)
                    {
                        defaultValue = null;
                    }
                    else
                    {
                        // we have a task... so return an invocation of the defalt of "Task.CompletedTask"
                        defaultValue = F.Property(null, Manager.KnownMembers.System_Threading_Tasks_Task_CompletedTask);
                    }
                }
                else if (returnType.IsNullableType())
                {
                    // nullable type - return default or null
                    defaultValue = F.NullOrDefault(returnType);
                }
                else
                {
                    // non nullable - return default
                    defaultValue = F.Default(returnType);
                }

                return defaultValue;
            }

            #endregion
        }
    }
}
