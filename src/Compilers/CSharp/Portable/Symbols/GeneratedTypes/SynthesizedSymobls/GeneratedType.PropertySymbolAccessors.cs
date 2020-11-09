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
        internal class GeneratedPropertyGetterAccessor : GeneratedProperteyAccessor
        {
            internal GeneratedPropertyGetterAccessor(GeneratedPropertyMemberDescriptor propDescriptor)
                : base(propDescriptor, MethodKind.PropertyGet)
            {
            }

            public new GeneratedPropertyGetterAccessor Build(
                NamedTypeSymbol containingType,
                GeneratedPropertyMember property,
                Func<SyntheticBoundNodeFactory, BoundStatement> bodyGenerator
                )
            {
                base.Build(containingType, property, bodyGenerator);
                return this;
            }

        }

        internal class GeneratedPropertySetterAccessor : GeneratedProperteyAccessor
        {
            internal GeneratedPropertySetterAccessor(
                GeneratedPropertyMemberDescriptor propDescriptor)
                : base(propDescriptor, MethodKind.PropertySet)
            {
            }

            public GeneratedParameterSymbol ValueParameter { get; protected set; }

            public new GeneratedPropertySetterAccessor Build(
                NamedTypeSymbol containingType,
                GeneratedPropertyMember property,
                Func<SyntheticBoundNodeFactory, BoundStatement> bodyGenerator
                )
            {
                // build the "value" parameter for the setter
                var pd = new GeneratedParameterDescriptor()
                {
                    Name = "value",
                    Type = ReturnTypeWithAnnotations
                };
                ValueParameter = new GeneratedParameterSymbol(pd, 0);
                ValueParameter.Build(this);

                Descriptor.Parameters = ImmutableArray.Create<ParameterSymbol>(ValueParameter);

                // set void type for the return type
                Descriptor.Type = TypeWithAnnotations.Create(((GeneratedTypeSymbol)containingType).Manager.KnownSymbols.System_Void);

                base.Build(
                    containingType,
                    property,
                    bodyGenerator
                );

                return this;
            }
        }

        /// <summary>
        /// Represents a getter for generated type property.
        /// </summary>
        internal abstract class GeneratedProperteyAccessor : SynthesizedMethodBase
        {
            private Func<SyntheticBoundNodeFactory, BoundStatement> _bodyGenerator;

            internal GeneratedProperteyAccessor(
                GeneratedPropertyMemberDescriptor propDescriptor,
                MethodKind methodKind
                )
                // winmdobj output only effects setters, so we can always set this to false
                : base(GetDescriptor(propDescriptor, methodKind), methodKind)
            {
                _methodKind = methodKind;
            }

            protected static GeneratedMethodMemberDescriptor GetDescriptor(GeneratedPropertyMemberDescriptor propDescriptor, MethodKind methodKind)
            {
                var d = new GeneratedMethodMemberDescriptor();
                var isGetter = methodKind == MethodKind.PropertyGet;

                d.Name = SourcePropertyAccessorSymbol.GetAccessorName(propDescriptor.Name, getNotSet: isGetter, isWinMdOutput: false);

                d.Type = propDescriptor.Type;
                d.Accessibility = propDescriptor.Accessibility;
                d.Locations = propDescriptor.Locations;
                d.DeclaringSyntaxReferences = propDescriptor.DeclaringSyntaxReferences;

                // some default values that are needed when implementing an interface
                d.Interface = propDescriptor.Interface;
                d.IsInterfaceImplementation = propDescriptor.IsInterfaceImplementation;
                d.IsVirtual = propDescriptor.IsVirtual;
                d.IsOverride = propDescriptor.IsOverride;
                d.IsFinal = propDescriptor.IsFinal;

                if (d.IsInterfaceImplementation)
                {
                    d.IsFinal = true;
                    d.IsVirtual = true;
                }

                // build an explicit interface name
                if (propDescriptor.ExplicitInterfaceMember != null)
                {
                    var name = ExplicitInterfaceHelpers.GetMemberNameWithoutInterfaceName(propDescriptor.Name);
                    name = SourcePropertyAccessorSymbol.GetAccessorName(name, getNotSet: isGetter, isWinMdOutput: false);
                    d.Name = ExplicitInterfaceHelpers.GetMemberName(name, (TypeSymbol)propDescriptor.Interface, null);
                    d.Accessibility = Accessibility.Private;

                    if (propDescriptor.ExplicitInterfaceMember is PropertySymbol prop)
                    {
                        if (isGetter)
                            d.ExplicitInterfaceMember = prop.GetMethod;
                        else
                            d.ExplicitInterfaceMember = prop.SetMethod;
                    }
                }

                return d;
            }

            protected virtual GeneratedProperteyAccessor Build(
                NamedTypeSymbol containingType,
                GeneratedPropertyMember property,
                Func<SyntheticBoundNodeFactory, BoundStatement> bodyGenerator
                )
            {
                base.Build(containingType);

                _containingType = containingType;
                PropertySymbol = property;

                _bodyGenerator = bodyGenerator;
                return this;
            }

            public GeneratedPropertyMember PropertySymbol { get; protected set; }

            internal override bool HasSpecialName
                => true;

            public override bool ReturnsVoid
                => MethodKind == MethodKind.PropertySet;

            public override Symbol AssociatedSymbol
                => PropertySymbol;

            internal override void AddSynthesizedAttributes(PEModuleBuilder moduleBuilder, ref ArrayBuilder<SynthesizedAttributeData> attributes)
            {
                // Do not call base.AddSynthesizedAttributes.
                // Dev11 does not emit DebuggerHiddenAttribute in property accessors
            }

            internal override void GenerateMethodBody(TypeCompilationState compilationState, DiagnosticBag diagnostics)
            {
                var factory = CreateBoundNodeFactory(compilationState, diagnostics);
                var body = _bodyGenerator(factory);
                factory.CloseMethod(body);
            }
        }
    }
}
