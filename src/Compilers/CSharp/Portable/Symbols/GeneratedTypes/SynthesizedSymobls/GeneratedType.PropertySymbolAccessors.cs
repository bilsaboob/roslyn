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
        internal class GeneratedTypePropertyGetAccessorSymbol : GeneratedTypePropertyAccessorSymbol
        {
            internal GeneratedTypePropertyGetAccessorSymbol(GeneratedPropertyMemberDescriptor propDescriptor)
                : base(propDescriptor, MethodKind.PropertyGet)
            {
            }
        }

        internal class GeneratedTypePropertySetAccessorSymbol : GeneratedTypePropertyAccessorSymbol
        {
            internal GeneratedTypePropertySetAccessorSymbol(
                GeneratedPropertyMemberDescriptor propDescriptor)
                : base(propDescriptor, MethodKind.PropertySet)
            {
            }

            public GeneratedParameterSymbol ValueParameter { get; protected set; }

            public override GeneratedTypePropertyAccessorSymbol Build(
                NamedTypeSymbol containingType,
                GeneratedTypePropertySymbol property,
                Func<SyntheticBoundNodeFactory, BoundStatement> bodyGenerator
                )
            {
                base.Build(
                    containingType,
                    property,
                    bodyGenerator
                );

                // build the "value" parameter for the setter
                var pd = new GeneratedParameterDescriptor()
                {
                    Name = "value",
                    Type = ReturnTypeWithAnnotations
                };
                ValueParameter = new GeneratedParameterSymbol(pd, 0);
                ValueParameter.Build(this);

                // add the paramter
                _parameters = ImmutableArray.Create<ParameterSymbol>(ValueParameter);

                // set void type for the return type
                Descriptor.Type = TypeWithAnnotations.Create(((GeneratedTypeSymbol)this.ContainingSymbol).Manager.System_Void);

                return this;
            }
        }

        /// <summary>
        /// Represents a getter for generated type property.
        /// </summary>
        internal class GeneratedTypePropertyAccessorSymbol : SynthesizedMethodBase
        {
            private MethodKind _methodKind;
            private Func<SyntheticBoundNodeFactory, BoundStatement> _bodyGenerator;
            protected ImmutableArray<ParameterSymbol> _parameters;

            internal GeneratedTypePropertyAccessorSymbol(
                GeneratedPropertyMemberDescriptor propDescriptor,
                MethodKind methodKind
                )
                // winmdobj output only effects setters, so we can always set this to false
                : base(GetDescriptor(propDescriptor, methodKind))
            {
                _methodKind = methodKind;
                _parameters = ImmutableArray<ParameterSymbol>.Empty;
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

                return d;
            }

            public virtual GeneratedTypePropertyAccessorSymbol Build(
                NamedTypeSymbol containingType,
                GeneratedTypePropertySymbol property,
                Func<SyntheticBoundNodeFactory, BoundStatement> bodyGenerator
                )
            {
                _containingType = containingType;
                PropertySymbol = property;

                _bodyGenerator = bodyGenerator;
                return this;
            }

            public GeneratedTypePropertySymbol PropertySymbol { get; protected set; }

            internal override bool IsMetadataNewSlot(bool ignoreInterfaceImplementationChanges = false)
                => Descriptor.IsInterfaceImplementation;

            public override bool IsVirtual
                => Descriptor.IsVirtual;

            public override MethodKind MethodKind
                => _methodKind;

            public override bool ReturnsVoid
                => MethodKind == MethodKind.PropertySet;

            public override RefKind RefKind
                => RefKind.None;

            public override TypeWithAnnotations ReturnTypeWithAnnotations
                => Descriptor.Type;

            public override ImmutableArray<ParameterSymbol> Parameters
                => _parameters;

            public override Symbol AssociatedSymbol
                => PropertySymbol;

            public override ImmutableArray<Location> Locations
                => Descriptor.Locations ?? ImmutableArray<Location>.Empty;

            public override bool IsOverride
                => Descriptor.IsOverride;

            internal sealed override bool IsMetadataVirtual(bool ignoreInterfaceImplementationChanges = false)
                => Descriptor.IsVirtual;

            internal override bool IsMetadataFinal
                => Descriptor.IsFinal;

            internal override bool HasSpecialName
                => true;

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
