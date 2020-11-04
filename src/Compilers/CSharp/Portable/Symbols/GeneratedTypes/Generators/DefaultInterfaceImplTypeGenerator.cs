using System;
using System.Collections.Generic;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.Symbols.GeneratedTypesManager;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal static class DefaultInterfaceImplTypeGenerator
    {
        internal static GeneratedDefaultInterfaceTypeSymbol Generate(CSharpCompilation compilation, NamedTypeSymbol interfaceType, DiagnosticBag diagnostics = null)
        {
            var ownsDiagnostics = diagnostics == null;
            if (ownsDiagnostics) diagnostics = DiagnosticBag.GetInstance();

            try
            {
                var tb = compilation.GeneratedTypesManager.GetDefaultInterfaceTypeBuilder(interfaceType, diagnostics);

                // set the interface
                tb.WithInterfaces(interfaceType);

                // add default constructor
                tb.WithDefaultConstructor();

                // add all members owned by the interface - but then also generate the members owned by sub members
                var interfaceMembers = interfaceType.GetMembers();
                foreach (var interfaceMember in interfaceMembers)
                {
                    var memberBuilder = GenerateMember(compilation, interfaceType, interfaceMember);
                    if (memberBuilder != null)
                        tb.WithMember(memberBuilder);
                }

                // construct the type
                var type = tb.ConstructType() as GeneratedDefaultInterfaceTypeSymbol;
                if (!(type is null))
                {
                    type.SynthesizedInterfaceConstructor ??= new SynthesizedInstanceConstructor(interfaceType);
                    type.InterfaceType ??= interfaceType;
                }
                return type;
            }
            finally
            {
                if (ownsDiagnostics) diagnostics.Free();
            }
        }

        private static TypeMemberBuilder GenerateMember(CSharpCompilation compilation, NamedTypeSymbol interfaceType, Symbol interfaceMember)
        {
            switch (interfaceMember.Kind)
            {
                case SymbolKind.Property:
                    return compilation.GeneratedTypesManager.GetPropertyMemberBuilder()
                        .FromSymbol((PropertySymbol)interfaceMember)
                        .ForInterface(interfaceType)
                        .WithAutoBackingField();
                case SymbolKind.Method:
                case SymbolKind.Field:
                case SymbolKind.Event:
                    break;
                default:
                    break;
            }

            return null;
        }

        internal static GeneratedDefaultInterfaceTypeSymbol GetOrGenerate(CSharpCompilation compilation, NamedTypeSymbol interfaceType, DiagnosticBag diagnostics = null)
        {
            var defaultImplType = compilation.GeneratedTypesManager.GetDefaultInterfaceType(interfaceType);
            if (defaultImplType is null)
                defaultImplType = Generate(compilation, interfaceType, diagnostics);
            return defaultImplType;
        }
    }
}
