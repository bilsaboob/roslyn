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
        internal class GeneratedTypeParameterSymbol : TypeParameterSymbol
        {
            private int _ordinal;
            private Symbol _containingSymbol;
            private TypeParameterKind _typeParameterKind;

            internal GeneratedTypeParameterSymbol(GeneratedTypeParameterDescriptor descriptor, int ordinal)
            {
                Descriptor = descriptor;
                _ordinal = ordinal;
            }

            protected GeneratedTypeParameterDescriptor Descriptor { get; }

            public GeneratedTypeParameterSymbol Build(Symbol containingSymbol)
            {
                _containingSymbol = containingSymbol;

                switch (containingSymbol.Kind)
                {
                    case SymbolKind.Method:
                        _typeParameterKind = TypeParameterKind.Method;
                        break;
                    case SymbolKind.NamedType:
                        _typeParameterKind = TypeParameterKind.Type;
                        break;
                    default:
                        break;
                }

                return this;
            }

            public override TypeParameterKind TypeParameterKind => _typeParameterKind;

            public override string Name => Descriptor.Name;

            public override int Ordinal => _ordinal;

            public override bool HasConstructorConstraint => Descriptor.HasConstructorConstraint;
            public override bool HasReferenceTypeConstraint => Descriptor.HasReferenceTypeConstraint;
            public override bool HasNotNullConstraint => Descriptor.HasNotNullConstraint;
            public override bool HasValueTypeConstraint => Descriptor.HasValueTypeConstraint;
            public override bool HasUnmanagedTypeConstraint => Descriptor.HasUnmanagedTypeConstraint;

            public override VarianceKind Variance => Descriptor.Variance;

            public override Symbol ContainingSymbol => _containingSymbol;

            public override ImmutableArray<Location> Locations
                => Descriptor.Locations ?? ImmutableArray<Location>.Empty;

            public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
                => Descriptor.DeclaringSyntaxReferences ?? ImmutableArray<SyntaxReference>.Empty;

            internal override bool? IsNotNullable => null;

            internal override bool? ReferenceTypeConstraintIsNullable => null;

            internal override void EnsureAllConstraintsAreResolved()
            {
            }

            internal override ImmutableArray<TypeWithAnnotations> GetConstraintTypes(ConsList<TypeParameterSymbol> inProgress)
                => Descriptor.ConstraintTypes ?? ImmutableArray<TypeWithAnnotations>.Empty;

            internal override TypeSymbol GetDeducedBaseType(ConsList<TypeParameterSymbol> inProgress)
                => null;

            internal override NamedTypeSymbol GetEffectiveBaseClass(ConsList<TypeParameterSymbol> inProgress)
                => null;

            internal override ImmutableArray<NamedTypeSymbol> GetInterfaces(ConsList<TypeParameterSymbol> inProgress)
                => ImmutableArray<NamedTypeSymbol>.Empty;
        }
    }
}
