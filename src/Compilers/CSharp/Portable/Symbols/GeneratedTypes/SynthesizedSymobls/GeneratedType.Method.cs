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
        internal class GeneratedOrdinaryMethodSymbol : SynthesizedMethodBase
        {
            private Func<SyntheticBoundNodeFactory, BoundStatement> _bodyGenerator;

            internal GeneratedOrdinaryMethodSymbol(
                GeneratedMethodMemberDescriptor descriptor,
                MethodKind methodKind = MethodKind.Ordinary
                )
                : base(descriptor, methodKind)
            {
                _methodKind = methodKind;
            }

            public new GeneratedMethodMemberDescriptor Descriptor => base.Descriptor as GeneratedMethodMemberDescriptor;

            public virtual GeneratedOrdinaryMethodSymbol Build(
                NamedTypeSymbol containingType,
                Func<SyntheticBoundNodeFactory, BoundStatement> bodyGenerator
                )
            {
                base.Build(containingType);
                _bodyGenerator = bodyGenerator;
                return this;
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
