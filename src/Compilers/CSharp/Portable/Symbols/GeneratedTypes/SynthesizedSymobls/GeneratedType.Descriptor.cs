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
using Microsoft.CodeAnalysis.Symbols;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal partial class GeneratedTypesManager
    {
        internal class GeneratedSymbolDescriptor
        {
            internal GeneratedSymbolDescriptor()
            {
                IsDebuggerHidden = true;
                IsDebuggerBrowsable = false;
            }

            public ImmutableArray<SyntaxReference>? DeclaringSyntaxReferences { get; set; }
            public ImmutableArray<Location>? Locations { get; set; }

            public bool IsDebuggerHidden { get; set; }
            public bool IsDebuggerBrowsable { get; set; }
        }

        internal class GeneratedTypeDescriptor : GeneratedSymbolDescriptor
        {
            public TypeKind TypeKind { get; set; }

            public string Name { get; set; }

            public Accessibility? Accessibility { get; set; }
            public bool IsStatic { get; set; }
            public bool IsAbstract { get; set; }
            public bool IsSealed { get; set; }
            public bool IsReadOnly { get; set; }

            public ImmutableArray<TypeParameterSymbol>? TypeParameters { get; set; }
            public NamedTypeSymbol? BaseType { get; set; }
            public ImmutableArray<NamedTypeSymbol>? Interfaces { get; set; }

            public Symbol ContainingSymbol { get; set; }
            public ImmutableArray<Symbol>? Members { get; set; }
        }


        internal class GeneratedParameterDescriptor : GeneratedSymbolDescriptor
        {
            public string Name { get; set; }
            public TypeWithAnnotations Type { get; set; }

            public bool IsIn { get; set; }
            public bool IsOut { get; set; }
            public bool IsOptional { get; set; }
            public bool IsParams { get; set; }
        }

        internal class GeneratedMemberDescriptor : GeneratedSymbolDescriptor
        {
            public string Name { get; set; }

            public Accessibility? Accessibility { get; set; }
            public bool IsExtern { get; set; }

            public bool IsStatic { get; set; }
            public bool IsAsync { get; set; }

            public bool IsAbstract { get; set; }
            public bool IsVirtual { get; set; }
            public bool IsOverride { get; set; }

            public bool IsFinal { get; set; }
            public bool IsSealed { get; set; }
            public bool IsReadOnly { get; set; }

            public bool IsVararg { get; set; }
            public bool IsInitOnly { get; set; }

            public bool IsVolatile { get; set; }
            public bool IsConst { get; set; }

            public bool IsExplicitInterfaceImplementation { get; set; }
            public Symbol ExplicitInterface { get; set; }

            public bool IsInterfaceImplementation { get; set; }
            public Symbol Interface { get; set; }

            public TypeWithAnnotations Type { get; set; }
            public ImmutableArray<TypeParameterSymbol>? TypeParameters { get; set; }
            public ImmutableArray<TypeWithAnnotations>? TypeArguments { get; set; }
        }

        internal class GeneratedMethodMemberDescriptor : GeneratedMemberDescriptor
        {
        }

        internal class GeneratedFieldMemberDescriptor : GeneratedMemberDescriptor
        {
        }

        internal class GeneratedPropertyMemberDescriptor : GeneratedMemberDescriptor
        {
            public bool IsAutoProperty { get; set; }
        }
    }
}
