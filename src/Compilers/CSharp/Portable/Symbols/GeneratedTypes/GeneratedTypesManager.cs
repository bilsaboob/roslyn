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
        private ConcurrentDictionary<string, GeneratedTypeSymbol> _generatedTypesByKey;

        internal GeneratedTypesManager(
            CSharpCompilation compilation
            )
        {
            Compilation = compilation;

            _generatedTypesByKey = new ConcurrentDictionary<string, GeneratedTypeSymbol>();
        }

        #region Compilation
        public CSharpCompilation Compilation { get; protected set; }

        public void CompileGeneratedTypes(MethodCompiler compiler, PEModuleBuilder moduleBeingBuilt, DiagnosticBag diagnostics)
        {
            var generatedTypes = GetGeneratedTypes();
            foreach (var type in generatedTypes)
            {
                compiler.Visit(type, null);
            }
        }
        #endregion

        #region Type construction
        internal ImmutableArray<NamedTypeSymbol> GetGeneratedTypes()
        {
            return _generatedTypesByKey.Values.ToImmutableArray<NamedTypeSymbol>();
        }

        internal GeneratedDefaultInterfaceTypeSymbol GetDefaultInterfaceType(TypeSymbol interfaceType)
        {
            var name = $"__DefaultImpl_{interfaceType.Name}";
            if (!_generatedTypesByKey.TryGetValue(name, out var generatedType))
                return null;
            return generatedType as GeneratedDefaultInterfaceTypeSymbol;
        }

        internal GeneratedTypeBuilder GetDefaultInterfaceTypeBuilder(TypeSymbol interfaceType, DiagnosticBag diagnostics)
        {
            // prepare the type descriptor
            var td = new GeneratedTypeDescriptor();
            td.Name = $"__DefaultImpl_{interfaceType.Name}";
            td.TypeKind = TypeKind.Class;

            // return a new builder
            return new GeneratedTypeBuilder(this, td, GeneratedTypeKind.DefaultInterface, diagnostics);
        }

        internal GeneratedTypeSymbol GetExistingOrConstructType(GeneratedTypeBuilder typeBuilder)
        {
            return _generatedTypesByKey.GetOrAdd(typeBuilder.TypeDescriptor.Name, _ => typeBuilder.Build());
        }
        #endregion

        #region Member construction
        internal PropertyMemberBuilder GetPropertyMemberBuilder()
        {
            var pd = new GeneratedPropertyMemberDescriptor();
            return new PropertyMemberBuilder(pd);
        }
        #endregion

        #region Known symbols
        public Symbol CodeGenNamespace
            => Compilation.SourceModule.GlobalNamespace;

        public NamedTypeSymbol System_Object
            => Compilation.GetSpecialType(SpecialType.System_Object);

        public NamedTypeSymbol System_Void
            => Compilation.GetSpecialType(SpecialType.System_Void);

        public NamedTypeSymbol System_Diagnostics_DebuggerBrowsableState
            => Compilation.GetWellKnownType(WellKnownType.System_Diagnostics_DebuggerBrowsableState);
        #endregion
    }
}
