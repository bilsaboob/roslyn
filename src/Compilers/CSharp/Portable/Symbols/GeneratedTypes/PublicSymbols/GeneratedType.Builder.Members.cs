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
        internal abstract class TypeMemberBuilder
        {
            internal GeneratedTypesManager Manager { get; set; }

            internal abstract Symbol Build(GeneratedTypeSymbol type, GeneratedTypeDescriptor td, int memberIndex, DiagnosticBag diagnostics);
        }

        class SymbolTypeMemberBuilder : TypeMemberBuilder
        {
            private Func<GeneratedTypeSymbol, GeneratedTypeDescriptor, int, DiagnosticBag, Symbol> _buildFn;

            internal SymbolTypeMemberBuilder(Func<GeneratedTypeSymbol, GeneratedTypeDescriptor, int, DiagnosticBag, Symbol> buildFn)
            {
                _buildFn = buildFn;
            }

            internal override Symbol Build(GeneratedTypeSymbol type, GeneratedTypeDescriptor td, int memberIndex, DiagnosticBag diagnostics)
                => _buildFn(type, td, memberIndex, diagnostics);
        }
    }
}
