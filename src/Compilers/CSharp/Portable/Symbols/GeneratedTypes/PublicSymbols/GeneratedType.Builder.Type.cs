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
    internal enum GeneratedTypeKind
    {
        DefaultInterface
    }

    internal partial class GeneratedTypesManager
    {
        internal class GeneratedTypeBuilder
        {
            private GeneratedTypesManager _manager;
            private GeneratedTypeSymbol _constructedType;
            private DiagnosticBag _diagnostics;

            private ArrayBuilder<TypeMemberBuilder> _constructors;
            private ArrayBuilder<TypeMemberBuilder> Constructors
            {
                get
                {
                    if (_constructors == null)
                        _constructors = new ArrayBuilder<TypeMemberBuilder>();
                    return _constructors;
                }
            }

            private ArrayBuilder<TypeMemberBuilder> _members;
            private ArrayBuilder<TypeMemberBuilder> Members
            {
                get
                {
                    if (_members == null)
                        _members = new ArrayBuilder<TypeMemberBuilder>();
                    return _members;
                }
            }

            internal GeneratedTypeBuilder(
                GeneratedTypesManager manager,
                GeneratedTypeDescriptor typeDescriptor,
                GeneratedTypeKind genTypeKind,
                DiagnosticBag diagnostics)
            {
                _manager = manager;
                _diagnostics = diagnostics;

                GenTypeKind = genTypeKind;
                TypeDescriptor = typeDescriptor;
            }

            public GeneratedTypeKind GenTypeKind { get; }
            public GeneratedTypeDescriptor TypeDescriptor { get; }

            public GeneratedTypeSymbol ConstructType()
            {
                _constructedType = _manager.GetExistingOrConstructType(this);
                return _constructedType;
            }

            #region Configuration helpers - common

            protected internal GeneratedTypeSymbol Build()
            {
                GeneratedTypeSymbol type = null;

                if (GenTypeKind == GeneratedTypeKind.DefaultInterface)
                {
                    type = new GeneratedDefaultInterfaceTypeSymbol(_manager);
                }
                else
                {
                    type = new GeneratedTypeSymbol(_manager);
                }

                // build the members
                var memberIndex = 0;
                var membersCount = _members?.Count ?? 0;
                var constructorsCount = _constructors?.Count ?? 0;
                var totalMembersCount = membersCount + constructorsCount;

                var members = ArrayBuilder<Symbol>.GetInstance(totalMembersCount);
                if (membersCount > 0)
                {
                    // build the members
                    foreach (var _member in _members)
                    {
                        var member = _member.Build(type, TypeDescriptor, memberIndex++, _diagnostics);

                        if (member is GeneratedPropertyMember propMember)
                        {
                            members.Add(propMember);
                            if (propMember.GetMethod != null)
                                members.Add(propMember.GetMethod);
                            if (propMember.SetMethod != null)
                                members.Add(propMember.SetMethod);
                            if (propMember.BackingField != null)
                                members.Add(propMember.BackingField);
                        }
                        else
                        {
                            members.Add(member);
                        }
                    }
                }

                // build the constructors
                if (_constructors != null)
                {
                    // build the members
                    foreach (var _constructor in _constructors)
                    {
                        var constructor = _constructor.Build(type, TypeDescriptor, memberIndex++, _diagnostics);
                        members.Add(constructor);
                    }
                }

                if(totalMembersCount > 0)
                    TypeDescriptor.Members = members.ToImmutableAndFree();

                type.Build(TypeDescriptor);
                return type;
            }

            public GeneratedTypeBuilder WithContainer(Symbol container)
            {
                TypeDescriptor.ContainingSymbol = container;
                return this;
            }

            public GeneratedTypeBuilder WithModifiers(
                bool? isStatic = null,
                bool? isAbstract = null,
                bool? isSealed = null,
                bool? isReadonly = null
                )
            {
                if (isStatic.HasValue) TypeDescriptor.IsStatic = isStatic.Value;
                if (isAbstract.HasValue) TypeDescriptor.IsAbstract = isAbstract.Value;
                if (isSealed.HasValue) TypeDescriptor.IsSealed = isSealed.Value;
                if (isReadonly.HasValue) TypeDescriptor.IsReadOnly = isReadonly.Value;
                return this;
            }

            public GeneratedTypeBuilder WithBaseType(NamedTypeSymbol baseType)
            {
                TypeDescriptor.BaseType = baseType;
                return this;
            }

            public GeneratedTypeBuilder WithInterfaces(params NamedTypeSymbol[] interfaces)
            {
                TypeDescriptor.Interfaces = interfaces.ToImmutableArray();
                return this;
            }

            public GeneratedTypeBuilder WithSyntax(SyntaxReference syntaxReference)
            {
                TypeDescriptor.DeclaringSyntaxReferences = ImmutableArray.Create(syntaxReference);
                TypeDescriptor.Locations = ImmutableArray.Create(syntaxReference.GetLocation());
                return this;
            }

            #endregion

            #region Configuration helpers - Members

            public GeneratedTypeBuilder WithDefaultConstructor()
            {
                return WithConstructor(new SymbolTypeMemberBuilder((t, td, memberIndex, diagnostics) => {
                    if (td.TypeKind == TypeKind.Submission)
                        return new SynthesizedSubmissionConstructor(t, diagnostics);
                    return new SynthesizedInstanceConstructor(t);
                }));
            }

            public GeneratedTypeBuilder WithConstructor(TypeMemberBuilder constructorBuilder)
            {
                Constructors.Add(constructorBuilder);
                return this;
            }

            public GeneratedTypeBuilder WithMember(TypeMemberBuilder memberBuilder)
            {
                Members.Add(memberBuilder);
                return this;
            }

            #endregion
        }
    }
}
