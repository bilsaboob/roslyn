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

            KnownSymbols = new ManagerKnownSymbols(this);
            KnownMembers = new ManagerKnownMembers(this);
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

        internal GeneratedTypeSymbol GetRSharpAttributeType(string attributeName)
        {
            _generatedTypesByKey.TryGetValue(attributeName, out var generatedType);
            return generatedType;
        }

        internal GeneratedTypeBuilder GetRSharpAttributeTypeBuilder(string attributeName, DiagnosticBag diagnostics)
        {
            // prepare the type descriptor
            var td = new GeneratedTypeDescriptor();
            td.Name = attributeName;
            td.TypeKind = TypeKind.Class;

            // return a new builder
            return new GeneratedTypeBuilder(this, td, GeneratedTypeKind.RSharpParamAttribute, diagnostics);
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
        internal PropertyMemberBuilder NewPropertyMember()
        {
            var pd = new GeneratedPropertyMemberDescriptor();
            return new PropertyMemberBuilder(pd) { Manager = this };
        }

        internal MethodMemberBuilder NewMethodMember()
        {
            var md = new GeneratedMethodMemberDescriptor();
            return new MethodMemberBuilder(md) { Manager = this };
        }
        #endregion

        #region Known symbols
        public ManagerKnownSymbols KnownSymbols { get; private set; }

        internal class ManagerKnownSymbols
        {
            internal ManagerKnownSymbols(GeneratedTypesManager manager)
            {
                Manager = manager;
            }

            private GeneratedTypesManager Manager { get; }
            private CSharpCompilation Compilation => Manager.Compilation;

            public Symbol CodeGenNamespace
            => Compilation.SourceModule.GlobalNamespace;

            public NamedTypeSymbol System_Object
                => Compilation.GetSpecialType(SpecialType.System_Object);

            public NamedTypeSymbol System_Void
                => Compilation.GetSpecialType(SpecialType.System_Void);

            public NamedTypeSymbol System_Diagnostics_DebuggerBrowsableState
                => Compilation.GetWellKnownType(WellKnownType.System_Diagnostics_DebuggerBrowsableState);

            public NamedTypeSymbol System_Threading_Tasks_Task
                => Compilation.GetWellKnownType(WellKnownType.System_Threading_Tasks_Task);

            public NamedTypeSymbol System_Threading_Tasks_Task_T
                => Compilation.GetWellKnownType(WellKnownType.System_Threading_Tasks_Task_T);
        }

        #endregion

        #region Known members

        public ManagerKnownMembers KnownMembers { get; private set; }

        internal class ManagerKnownMembers
        {
            internal ManagerKnownMembers(GeneratedTypesManager manager)
            {
                Manager = manager;
            }

            private GeneratedTypesManager Manager { get; }
            private CSharpCompilation Compilation => Manager.Compilation;

            public PropertySymbol System_Threading_Tasks_Task_CompletedTask
                => Manager.KnownSymbols.System_Threading_Tasks_Task.GetMembers("CompletedTask").FirstOrDefault(p => p is PropertySymbol) as PropertySymbol;

            public MethodSymbol System_Threading_Tasks_Task_FromResult
                => Manager.KnownSymbols.System_Threading_Tasks_Task.GetMembers("FromResult").FirstOrDefault(p => p is MethodSymbol) as MethodSymbol;
        }

        #endregion
    }
}
