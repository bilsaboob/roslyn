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
using Microsoft.CodeAnalysis.Symbols;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal partial class GeneratedTypesManager
    {
        private class GeneratedFieldSymbol : FieldSymbol
        {
            private NamedTypeSymbol _containingType;
            private ConstantValue _constantValue;

            public GeneratedFieldSymbol(GeneratedFieldMemberDescriptor descriptor)
            {
                Descriptor = descriptor;
            }

            protected GeneratedFieldMemberDescriptor Descriptor { get; }

            public virtual GeneratedFieldSymbol Build(NamedTypeSymbol containingType, Symbol ownerMemberSymbol, ConstantValue constantValue)
            {
                // the member that owns this field - the "associated" member / the member to which this field is "attached"
                // - for "backing fields" this would be the "property symbol"
                OwnerMemberSymbol = ownerMemberSymbol;
                _containingType = containingType;
                return this;
            }

            protected GeneratedTypesManager Manager
            {
                get
                {
                    var generatedType = _containingType as GeneratedTypeSymbol;
                    return ((object)generatedType != null) ? generatedType.Manager : ((GeneratedTypeSymbol)_containingType).Manager;
                }
            }

            public Symbol OwnerMemberSymbol { get; internal set; }

            internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound)
                => Descriptor.Type;

            public override string Name
                => Descriptor.Name;

            public override FlowAnalysisAnnotations FlowAnalysisAnnotations
                => FlowAnalysisAnnotations.None;

            internal override bool HasSpecialName
                => false;

            internal override bool HasRuntimeSpecialName
                => false;

            internal override bool IsNotSerialized
                => false;

            internal override MarshalPseudoCustomAttributeData MarshallingInformation
                => null;

            internal override int? TypeLayoutOffset
                => null;

            public override Symbol AssociatedSymbol
                => OwnerMemberSymbol;

            public override bool IsReadOnly
                => Descriptor.IsReadOnly;

            public override bool IsVolatile
                => Descriptor.IsVolatile;

            public override bool IsConst
                => Descriptor.IsConst;

            internal sealed override ObsoleteAttributeData ObsoleteAttributeData
                => null;

            internal override ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress, bool earlyDecodingWellKnownAttributes)
                => _constantValue;

            public override Symbol ContainingSymbol
                => _containingType;

            public override NamedTypeSymbol ContainingType
                => _containingType;

            public override ImmutableArray<Location> Locations
                => Descriptor.Locations ?? ImmutableArray<Location>.Empty;

            public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
                => Descriptor.DeclaringSyntaxReferences ?? ImmutableArray<SyntaxReference>.Empty;

            public override Accessibility DeclaredAccessibility
                => Descriptor.Accessibility ?? Accessibility.Internal;

            public override bool IsStatic
                => Descriptor.IsStatic;

            public override bool IsImplicitlyDeclared
                => true;

            internal override void AddSynthesizedAttributes(PEModuleBuilder moduleBuilder, ref ArrayBuilder<SynthesizedAttributeData> attributes)
            {
                base.AddSynthesizedAttributes(moduleBuilder, ref attributes);

                if (!Descriptor.IsDebuggerBrowsable)
                {
                    AddSynthesizedAttribute(ref attributes, Manager.Compilation.TrySynthesizeAttribute(
                        WellKnownMember.System_Diagnostics_DebuggerBrowsableAttribute__ctor,
                        ImmutableArray.Create(new TypedConstant(Manager.KnownSymbols.System_Diagnostics_DebuggerBrowsableState, TypedConstantKind.Enum, DebuggerBrowsableState.Never)))
                    );
                }

                if (Descriptor.IsDebuggerHidden)
                {
                    AddSynthesizedAttribute(ref attributes, Manager.Compilation.TrySynthesizeAttribute(WellKnownMember.System_Diagnostics_DebuggerHiddenAttribute__ctor));
                }
            }
        }
    }
}
