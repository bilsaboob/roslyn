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
        internal class GeneratedDefaultInterfaceTypeSymbol : GeneratedTypeSymbol
        {
            internal GeneratedDefaultInterfaceTypeSymbol(
                GeneratedTypesManager manager
                )
                : base(manager)
            {
            }

            public SynthesizedInstanceConstructor SynthesizedInterfaceConstructor { get; set; }
            public NamedTypeSymbol InterfaceType { get; set; }
        }

        internal class GeneratedTypeSymbol : NamedTypeSymbol
        {
            private GeneratedTypeDescriptor _typeDescriptor;

            private HashSet<string> _memberNames;
            private MultiDictionary<string, Symbol> _membersByName;
            private ImmutableArray<NamedTypeSymbol> _typeMembers;

            internal GeneratedTypeSymbol(GeneratedTypesManager manager)
            {
                Manager = manager;
            }

            internal GeneratedTypesManager Manager { get; }

            internal void Build(GeneratedTypeDescriptor typeDescriptor)
            {
                _typeDescriptor = typeDescriptor;
                _memberNames = new HashSet<string>();
                _membersByName = new MultiDictionary<string, Symbol>();

                var typeMembersBuilder = ArrayBuilder<NamedTypeSymbol>.GetInstance();
                if (_typeDescriptor.Members != null)
                {
                    foreach (var member in _typeDescriptor.Members)
                    {
                        _memberNames.Add(member.Name);
                        _membersByName.Add(member.Name, member);

                        if (member is NamedTypeSymbol typeMember)
                            typeMembersBuilder.Add(typeMember);
                    }
                }
                _typeMembers = typeMembersBuilder.ToImmutableAndFree();
            }

            #region Implementation

            // Type kind
            public override TypeKind TypeKind => _typeDescriptor.TypeKind;
            internal override bool IsInterface => TypeKind == TypeKind.Interface;

            // Parent namespace / type
            public override Symbol ContainingSymbol
                => _typeDescriptor.ContainingSymbol ?? Manager.KnownSymbols.CodeGenNamespace;

            // Visibility
            public override Accessibility DeclaredAccessibility
                => _typeDescriptor.Accessibility ?? Accessibility.Internal;

            // Modifiers
            public override bool IsStatic
                => _typeDescriptor.IsStatic;

            public override bool IsAbstract
                => _typeDescriptor.IsAbstract;

            public override bool IsSealed
                => _typeDescriptor.IsSealed;

            public override bool IsReadOnly
                => _typeDescriptor.IsReadOnly;

            // Name
            public override string Name => _typeDescriptor.Name;
            public override string MetadataName => string.Empty;
            internal override bool MangleName => false;
            internal override bool HasSpecialName => false;

            // Type parameters
            public override ImmutableArray<TypeParameterSymbol> TypeParameters
                => _typeDescriptor.TypeParameters ?? ImmutableArray<TypeParameterSymbol>.Empty;

            internal override ImmutableArray<TypeWithAnnotations> TypeArgumentsWithAnnotationsNoUseSiteDiagnostics
                => ImmutableArray<TypeWithAnnotations>.Empty;

            // Base type & interfaces
            internal override NamedTypeSymbol BaseTypeNoUseSiteDiagnostics
                => _typeDescriptor.BaseType ?? Manager.KnownSymbols.System_Object;

            internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved)
                => _typeDescriptor.BaseType ?? Manager.KnownSymbols.System_Object;

            internal override ImmutableArray<NamedTypeSymbol> GetDeclaredInterfaces(ConsList<TypeSymbol> basesBeingResolved)
                => _typeDescriptor.Interfaces ?? ImmutableArray<NamedTypeSymbol>.Empty;

            internal override ImmutableArray<NamedTypeSymbol> InterfacesNoUseSiteDiagnostics(ConsList<TypeSymbol> basesBeingResolved = null)
                => _typeDescriptor.Interfaces ?? ImmutableArray<NamedTypeSymbol>.Empty;

            internal override ImmutableArray<NamedTypeSymbol> GetInterfacesToEmit()
                => InterfacesNoUseSiteDiagnostics();

            // Members
            public override IEnumerable<string> MemberNames => _memberNames;

            public override ImmutableArray<Symbol> GetMembers()
                => _typeDescriptor.Members ?? ImmutableArray<Symbol>.Empty;

            public override ImmutableArray<Symbol> GetMembers(string name)
            {
                var symbols = _membersByName[name];
                var builder = ArrayBuilder<Symbol>.GetInstance(symbols.Count);

                foreach (var symbol in symbols)
                    builder.Add(symbol);

                return builder.ToImmutableAndFree();
            }

            public override ImmutableArray<NamedTypeSymbol> GetTypeMembers()
                => _typeMembers;

            public override ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name)
                => _typeMembers.IsEmpty ? ImmutableArray<NamedTypeSymbol>.Empty : _typeMembers.Where(m => m.Name == name).ToImmutableArray();

            public override ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name, int arity)
                => _typeMembers.IsEmpty ? ImmutableArray<NamedTypeSymbol>.Empty : _typeMembers.Where(m => m.Name == name && m.Arity == arity).ToImmutableArray();

            internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers()
                => GetMembersUnordered();

            internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers(string name)
                => GetMembers(name);

            internal override IEnumerable<FieldSymbol> GetFieldsToEmit()
                => GetMembers().Where(m => m.Kind == SymbolKind.Field).Cast<FieldSymbol>();

            public override bool AreLocalsZeroed
            {
                get
                {
                    switch (TypeKind)
                    {
                        case TypeKind.Interface:
                        case TypeKind.Delegate:
                        case TypeKind.Enum:
                        case TypeKind.Module:
                        case TypeKind.Array:
                        case TypeKind.Pointer:
                        case TypeKind.FunctionPointer:
                        case TypeKind.Error:
                        case TypeKind.TypeParameter:
                        case TypeKind.Unknown:
                            return false;
                        default:
                            return true;
                    }
                }
            }

            // Syntax declaration
            public override ImmutableArray<Location> Locations
                => _typeDescriptor.Locations ?? ImmutableArray<Location>.Empty;

            public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
                => _typeDescriptor.DeclaringSyntaxReferences ?? ImmutableArray<SyntaxReference>.Empty;

            #endregion

            #region Default overrides
            public override int Arity => 0;

            public override NamedTypeSymbol ConstructedFrom => this;

            public override bool MightContainExtensionMethods => false;

            public override bool IsSerializable => false;

            public override bool IsRefLikeType => false;

            internal override bool HasCodeAnalysisEmbeddedAttribute => false;

            internal override bool IsComImport => false;

            internal override bool IsWindowsRuntimeImport => false;

            internal override bool ShouldAddWinRTMembers => false;

            internal override TypeLayout Layout => default;

            internal override CharSet MarshallingCharSet => DefaultMarshallingCharSet;

            internal override bool HasDeclarativeSecurity => false;

            internal override NamedTypeSymbol NativeIntegerUnderlyingType => null;

            internal override ObsoleteAttributeData ObsoleteAttributeData => null;

            protected override NamedTypeSymbol WithTupleDataCore(TupleExtraData newData) => null;

            internal override NamedTypeSymbol AsNativeInteger() => null;

            internal override ImmutableArray<string> GetAppliedConditionalSymbols() => ImmutableArray<string>.Empty;

            internal override AttributeUsageInfo GetAttributeUsageInfo() => AttributeUsageInfo.Null;

            internal override IEnumerable<SecurityAttribute> GetSecurityInformation()
                => Enumerable.Empty<SecurityAttribute>();

            #endregion
        }
    }
}
