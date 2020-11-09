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
        internal class GeneratedParameterSymbol : ParameterSymbol
        {
            private int _ordinal;

            internal GeneratedParameterSymbol(GeneratedParameterDescriptor descriptor, int ordinal)
            {
                Descriptor = descriptor;

                _ordinal = ordinal;
            }

            protected GeneratedParameterDescriptor Descriptor { get; }

            public virtual GeneratedParameterSymbol Build(MethodSymbol method)
            {
                MethodSymbol = method;
                return this;
            }

            public MethodSymbol MethodSymbol { get; protected set; }

            public override string Name
                => Descriptor.Name;

            public override TypeWithAnnotations TypeWithAnnotations
                => Descriptor.Type;

            public override RefKind RefKind
                => Descriptor.RefKind ?? RefKind.None;

            public override bool IsDiscard
                => false;

            public override ImmutableArray<CustomModifier> RefCustomModifiers
                => Descriptor.RefCustomModifiers ?? ImmutableArray<CustomModifier>.Empty;

            public override int Ordinal
                => _ordinal;

            public override bool IsParams
                => Descriptor.IsParams;

            public override Symbol ContainingSymbol
                => MethodSymbol;

            public override ImmutableArray<Location> Locations
                => Descriptor.Locations ?? ImmutableArray<Location>.Empty;

            public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
                => Descriptor.DeclaringSyntaxReferences ?? ImmutableArray<SyntaxReference>.Empty;

            internal override MarshalPseudoCustomAttributeData MarshallingInformation
                => null;

            internal override bool IsMetadataOptional
                => Descriptor.IsOptional;

            internal override bool IsMetadataIn
                => Descriptor.IsIn;

            internal override bool IsMetadataOut
                => Descriptor.IsOut;

            internal override ConstantValue ExplicitDefaultConstantValue
                => Descriptor.ExplicitDefaultConstantValue;

            internal override bool IsIDispatchConstant
                => false;

            internal override bool IsIUnknownConstant
                => false;

            internal override bool IsCallerFilePath
                => false;

            internal override bool IsCallerLineNumber
                => false;

            internal override bool IsCallerMemberName
                => false;

            internal override FlowAnalysisAnnotations FlowAnalysisAnnotations
                => FlowAnalysisAnnotations.None;

            internal override ImmutableHashSet<string> NotNullIfParameterNotNull
                => ImmutableHashSet<string>.Empty;
        }
    }
}
