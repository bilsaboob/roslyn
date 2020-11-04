using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
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
        internal class GeneratedTypePropertySymbol : PropertySymbol
        {
            private NamedTypeSymbol _containingType;
            private int _memberIndex;

            private GeneratedTypePropertyAccessorSymbol _getter;
            private GeneratedTypePropertyAccessorSymbol _setter;
            private FieldSymbol _backingField;

            internal GeneratedTypePropertySymbol(
                GeneratedPropertyMemberDescriptor descriptor,
                int index
                )
            {
                PropDescriptor = descriptor;
                _memberIndex = index;
            }

            public virtual GeneratedTypePropertySymbol Build(
                NamedTypeSymbol containingType,
                GeneratedTypePropertyAccessorSymbol getter,
                GeneratedTypePropertyAccessorSymbol setter,
                FieldSymbol backingField
                )
            {
                _containingType = containingType;

                _getter = getter;
                _setter = setter;
                _backingField = backingField;

                return this;
            }

            internal GeneratedPropertyMemberDescriptor PropDescriptor { get; }

            #region Implementation

            internal override int? MemberIndexOpt => _memberIndex;

            // Parent namespace / type
            public override Symbol ContainingSymbol
                => _containingType;

            public override NamedTypeSymbol ContainingType
                => _containingType;

            // Visibility
            public override Accessibility DeclaredAccessibility
                => PropDescriptor.Accessibility ?? Accessibility.Internal;

            // Modifiers
            public override bool IsStatic
                => PropDescriptor.IsStatic;

            public override bool IsAbstract
                => PropDescriptor.IsAbstract;

            public override bool IsSealed
                => PropDescriptor.IsSealed;

            public sealed override bool IsVirtual
                => PropDescriptor.IsVirtual;

            public override bool IsOverride
                => PropDescriptor.IsOverride;

            internal sealed override bool IsExplicitInterfaceImplementation
                => PropDescriptor.IsExplicitInterfaceImplementation;

            public sealed override bool IsExtern
                => false;

            // Type
            public override TypeWithAnnotations TypeWithAnnotations
                => PropDescriptor.Type;

            // Name
            public sealed override string Name
                => PropDescriptor.Name;

            internal override bool HasSpecialName
                => false;

            // Explicit interface
            public override ImmutableArray<PropertySymbol> ExplicitInterfaceImplementations
                => ImmutableArray<PropertySymbol>.Empty;

            // Syntax declaration
            public override ImmutableArray<Location> Locations
                => PropDescriptor.Locations ?? ImmutableArray<Location>.Empty;

            public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
                => PropDescriptor.DeclaringSyntaxReferences ?? ImmutableArray<SyntaxReference>.Empty;

            // Getter & Setter
            public FieldSymbol BackingField
                => _backingField;

            public override MethodSymbol SetMethod
                => _setter;

            public override MethodSymbol GetMethod
                => _getter;

            #endregion

            #region Default overrides

            public override RefKind RefKind
                => RefKind.None;

            public override bool IsImplicitlyDeclared
                => true;

            public override bool IsIndexer
                => false;

            internal sealed override ObsoleteAttributeData ObsoleteAttributeData
                => null;

            public override ImmutableArray<ParameterSymbol> Parameters
                => ImmutableArray<ParameterSymbol>.Empty;

            public override ImmutableArray<CustomModifier> RefCustomModifiers
                => ImmutableArray<CustomModifier>.Empty;

            internal override Microsoft.Cci.CallingConvention CallingConvention
                => Microsoft.Cci.CallingConvention.HasThis;

            internal override bool MustCallMethodsDirectly
                => false;

            public override bool Equals(Symbol obj, TypeCompareKind compareKind)
            {
                if (obj == null)
                {
                    return false;
                }
                else if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                var other = obj as GeneratedTypePropertySymbol;
                if ((object)other == null)
                {
                    return false;
                }

                //  consider properties the same is the owning types are the same and 
                //  the names are equal
                return ((object)other != null) && other.Name == this.Name
                    && other.ContainingType.Equals(this.ContainingType, compareKind);
            }

            public override int GetHashCode()
            {
                return Hash.Combine(this.ContainingType.GetHashCode(), this.Name.GetHashCode());
            }
            #endregion
        }
    }
}
