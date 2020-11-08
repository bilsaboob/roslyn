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
        internal class PropertyMemberBuilder : TypeMemberBuilder
        {
            private GeneratedPropertyMemberDescriptor _propDescriptor;

            internal PropertyMemberBuilder(GeneratedPropertyMemberDescriptor propDescriptor)
            {
                _propDescriptor = propDescriptor;
            }

            internal override Symbol Build(GeneratedTypeSymbol type, GeneratedTypeDescriptor td, int memberIndex, DiagnosticBag diagnostics)
            {
                GeneratedFieldSymbol backingField = null;
                GeneratedPropertyGetterAccessor getter = null;
                GeneratedPropertySetterAccessor setter = null;

                // create the backing field for the property
                if (_propDescriptor.IsAutoProperty)
                {
                    var backingFieldDescriptor = new GeneratedFieldMemberDescriptor();
                    backingFieldDescriptor.Name = GeneratedNames.MakeAnonymousTypeBackingFieldName(_propDescriptor.Name);
                    backingFieldDescriptor.Type = _propDescriptor.Type;
                    backingFieldDescriptor.Accessibility = Accessibility.Private;
                    backingField = new GeneratedFieldSymbol(backingFieldDescriptor);

                    getter = new GeneratedPropertyGetterAccessor(_propDescriptor);
                    setter = new GeneratedPropertySetterAccessor(_propDescriptor);
                }

                // create the property
                var prop = new GeneratedPropertyMember(
                    _propDescriptor
                );

                // finalize / build the property and the associated
                prop.Build(
                    containingType: type,
                    getter: getter,
                    setter: setter,
                    backingField: backingField
                );

                // build getter / setter for auto properies with backing fields
                if (_propDescriptor.IsAutoProperty)
                {
                    // build the backing field
                    backingField.Build(
                        containingType: type,
                        ownerMemberSymbol: prop,
                        constantValue: null
                    );

                    getter.Build(
                        containingType: type,
                        property: prop,
                        bodyGenerator: F => CreateBackingFieldGetterBody(F, backingField)
                    );
                    setter.Build(
                        containingType: type,
                        property: prop,
                        bodyGenerator: F => CreateBackingFieldSetterBody(F, backingField, setter.ValueParameter)
                    );
                }

                return prop;
            }

            public PropertyMemberBuilder FromSymbol(PropertySymbol prop)
            {
                _propDescriptor.Name = prop.Name;
                _propDescriptor.Type = prop.TypeWithAnnotations;
                _propDescriptor.Accessibility = prop.DeclaredAccessibility;
                return this;
            }

            public PropertyMemberBuilder WithAutoBackingField()
            {
                _propDescriptor.IsAutoProperty = true;
                return this;
            }

            public PropertyMemberBuilder ForInterface(NamedTypeSymbol interfaceType, bool isExplicit = false, Symbol interfaceMember = null)
            {
                _propDescriptor.IsAutoProperty = true;
                _propDescriptor.IsInterfaceImplementation = true;
                _propDescriptor.Interface = interfaceType;

                if (isExplicit)
                {
                    _propDescriptor.Accessibility = Accessibility.Private;
                    _propDescriptor.ExplicitInterfaceMember = interfaceMember;

                    // build an explicit interface member name
                    var name = _propDescriptor.Name;
                    if (string.IsNullOrEmpty(name))
                        name = ExplicitInterfaceHelpers.GetMemberNameWithoutInterfaceName(interfaceMember.Name);
                    name = ExplicitInterfaceHelpers.GetMemberName(name, interfaceType, null);
                    _propDescriptor.Name = name;
                }

                return this;
            }

            #region Bound node factories

            private BoundStatement CreateBackingFieldSetterBody(SyntheticBoundNodeFactory F, FieldSymbol field, ParameterSymbol valueParam)
            {
                var statements = ImmutableArray.Create<BoundStatement>(
                    F.Assignment(F.Field(F.This(), field), F.Parameter(valueParam)),
                    F.Return()
                );
                return F.Block(statements);
            }

            private BoundStatement CreateBackingFieldGetterBody(SyntheticBoundNodeFactory F, FieldSymbol field)
            {
                return F.Block(F.Return(F.Field(F.This(), field)));
            }

            #endregion
        }
    }
}
