using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Cci;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal partial class GeneratedTypesManager
    {
        internal abstract class SynthesizedMethodBase : SynthesizedInstanceMethodSymbol
        {
            protected NamedTypeSymbol _containingType;

            public SynthesizedMethodBase(GeneratedMethodMemberDescriptor descriptor)
            {
                Descriptor = descriptor;
            }

            public GeneratedMethodMemberDescriptor Descriptor { get; protected set; }

            protected GeneratedTypesManager Manager
            {
                get
                {
                    var generatedType = _containingType as GeneratedTypeSymbol;
                    return ((object)generatedType != null) ? generatedType.Manager : ((GeneratedTypeSymbol)_containingType).Manager;
                }
            }

            #region Implementation

            // Parent namespace / type
            public override Symbol ContainingSymbol
                => _containingType;

            public override NamedTypeSymbol ContainingType
                => _containingType;

            public override bool HidesBaseMethodsByName
                => false;

            // Visibility
            public override Accessibility DeclaredAccessibility
                => Descriptor.Accessibility ?? Accessibility.Internal;

            // Modifiers
            public override bool IsStatic
                => Descriptor.IsStatic;

            public override bool IsAbstract
                => Descriptor.IsAbstract;

            public override bool IsSealed
                => Descriptor.IsSealed;

            public override bool IsVirtual
                => Descriptor.IsVirtual;

            public override bool IsOverride
                => Descriptor.IsOverride;

            public override bool IsAsync
                => Descriptor.IsAsync;

            internal override bool IsExplicitInterfaceImplementation
                => Descriptor.IsExplicitInterfaceImplementation;

            public override bool IsVararg
                => Descriptor.IsVararg;

            public override bool IsExtern
                => Descriptor.IsExtern;

            // Type parameters
            public override ImmutableArray<TypeParameterSymbol> TypeParameters
                => Descriptor.TypeParameters ?? ImmutableArray<TypeParameterSymbol>.Empty;

            public override ImmutableArray<TypeWithAnnotations> TypeArgumentsWithAnnotations
                => Descriptor.TypeArguments ?? ImmutableArray<TypeWithAnnotations>.Empty;

            // Name
            public override string Name
                => Descriptor.Name;

            internal override bool HasSpecialName
                => false;

            // Syntax declaration
            public override ImmutableArray<Location> Locations
                => Descriptor.Locations ?? ImmutableArray<Location>.Empty;

            public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
                => Descriptor.DeclaringSyntaxReferences ?? ImmutableArray<SyntaxReference>.Empty;

            #endregion

            #region Helpers
            protected SyntheticBoundNodeFactory CreateBoundNodeFactory(TypeCompilationState compilationState, DiagnosticBag diagnostics)
            {
                var F = new SyntheticBoundNodeFactory(this, this.GetNonNullSyntaxNode(), compilationState, diagnostics);
                F.CurrentFunction = this;
                return F;
            }
            #endregion

            #region Default overrides

            public override RefKind RefKind
                => RefKind.None;

            internal override bool GenerateDebugInfo
                => false;

            public override int Arity
                => 0;

            internal override System.Reflection.MethodImplAttributes ImplementationAttributes
                => default;

            internal override Cci.CallingConvention CallingConvention
                => Cci.CallingConvention.HasThis;

            public override bool IsExtensionMethod
                => false;

            public override FlowAnalysisAnnotations ReturnTypeFlowAnalysisAnnotations
                => FlowAnalysisAnnotations.None;

            public override ImmutableHashSet<string> ReturnNotNullIfParameterNotNull
                => ImmutableHashSet<string>.Empty;

            public override ImmutableArray<MethodSymbol> ExplicitInterfaceImplementations
                => ImmutableArray<MethodSymbol>.Empty;

            // methods on classes are never 'readonly'
            internal override bool IsDeclaredReadOnly
                => Descriptor.IsReadOnly;

            internal override bool IsInitOnly
                => Descriptor.IsInitOnly;

            public override ImmutableArray<CustomModifier> RefCustomModifiers
                => ImmutableArray<CustomModifier>.Empty;

            public override Symbol AssociatedSymbol
                => null;

            internal override bool IsMetadataNewSlot(bool ignoreInterfaceImplementationChanges = false)
                => Descriptor.IsInterfaceImplementation;

            internal override bool IsMetadataVirtual(bool ignoreInterfaceImplementationChanges = false)
                => Descriptor.IsVirtual;

            internal override bool IsMetadataFinal
                => Descriptor.IsFinal;

            internal override void AddSynthesizedAttributes(PEModuleBuilder moduleBuilder, ref ArrayBuilder<SynthesizedAttributeData> attributes)
            {
                base.AddSynthesizedAttributes(moduleBuilder, ref attributes);

                if (!Descriptor.IsDebuggerBrowsable)
                {
                    AddSynthesizedAttribute(ref attributes, Manager.Compilation.TrySynthesizeAttribute(
                        WellKnownMember.System_Diagnostics_DebuggerBrowsableAttribute__ctor,
                        ImmutableArray.Create(new TypedConstant(Manager.System_Diagnostics_DebuggerBrowsableState, TypedConstantKind.Enum, DebuggerBrowsableState.Never)))
                    );
                }

                if (Descriptor.IsDebuggerHidden)
                {
                    AddSynthesizedAttribute(ref attributes, Manager.Compilation.TrySynthesizeAttribute(WellKnownMember.System_Diagnostics_DebuggerHiddenAttribute__ctor));
                }
            }

            internal override bool RequiresSecurityObject
                => false;

            public override DllImportData GetDllImportData()
                => null;

            internal override MarshalPseudoCustomAttributeData ReturnValueMarshallingInformation
                => null;

            internal override bool HasDeclarativeSecurity
                => false;

            internal override IEnumerable<Cci.SecurityAttribute> GetSecurityInformation()
                => throw ExceptionUtilities.Unreachable;

            internal override ImmutableArray<string> GetAppliedConditionalSymbols()
                => ImmutableArray<string>.Empty;

            internal override bool SynthesizesLoweredBoundBody
                => true;

            internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree)
                => throw ExceptionUtilities.Unreachable;

            #endregion
        }
    }
}
