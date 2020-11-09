using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.Cci;
using static Microsoft.CodeAnalysis.CSharp.Symbols.GeneratedTypesManager;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal static class DefaultInterfaceImplTypeGenerator
    {
        private static readonly MemberSignatureComparer DefaultSignatureComparer = new MemberSignatureComparer(
            considerName: true,
            considerExplicitlyImplementedInterfaces: false,
            considerReturnType: true,
            considerTypeConstraints: true,
            considerCallingConvention: true,
            considerRefKindDifferences: true,
            typeComparison: TypeCompareKind.AllIgnoreOptions
        );

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

                // we need to include all members for each interface / base interface in order of priority:
                // 1. "left to right" in the order of declaration
                // 2. "top down" by inheritance

                // * members that have exactly "same" definition, can be represented by a single member
                // * members that match on name but definition differ, must be implemented by "explicit interface members"
                //   - these interfaces also need to be added in the interface list explicitly

                var memberGroupsByName = new Dictionary<string, MembersGroup>();
                var interfaces = ImmutableArray.Create(interfaceType).AddRange(interfaceType.GetAllInterfaces());
                foreach (var it in interfaces)
                    BuildMemberGroups(it, memberGroupsByName);

                NamedTypeSymbol implicitInterfaceType = null;

                // now build each member entry
                foreach (var membersGroup in memberGroupsByName.Values.OrderBy(e => e.SortOrder))
                {
                    // check if we can generate as "simple" without needing explicit implementation
                    if (membersGroup.MemberEntriesBySignature.Count == 0) continue;

                    if (membersGroup.MemberEntriesBySignature.Count == 1)
                    {
                        // pick the first member entry, since they all are "equally valid" to generate for
                        var membersEntry = membersGroup.MemberEntriesBySignature.Values.FirstOrDefault();
                        var memberEntry = membersEntry.Members.FirstOrDefault();
                        var member = memberEntry.MemberSymbol;

                        // only a single signature available for this named member group... so just generate a single member!
                        var memberBuilder = GenerateMember(compilation, interfaceType, member, isExplicitInterface: false);
                        if (memberBuilder != null)
                            tb.WithMember(memberBuilder);
                    }
                    else
                    {
                        var membersEntries = membersGroup.MemberEntriesBySignature.Values.OrderBy(me => me.SortOrder);

                        // pick the first entry / member declaring type as the interface that will be implicit, the remaining ones will be implemented as explicit
                        if (implicitInterfaceType is null)
                            implicitInterfaceType = membersEntries.Select(me => me.Members[0].DeclaringTypeSymbol).FirstOrDefault();

                        // we have multiple signatures available for this named member group... we need to implement the members explicitly for each interface!
                        foreach (var membersEntry in membersEntries)
                        {
                            // generate an explicit interface implementation for each of member / declaring types
                            // however, if the list contains a symbol from the "implicitInterfaceType" then this list of members is implemented implicit

                            var isExplicit = !membersEntry.Members.Any(me => me.DeclaringTypeSymbol.Equals(implicitInterfaceType));
                            foreach (var memberEntry in membersEntry.Members)
                            {
                                var member = memberEntry.MemberSymbol;
                                var declaringType = memberEntry.DeclaringTypeSymbol;
                                var memberBuilder = GenerateMember(compilation, declaringType, member, isExplicitInterface: isExplicit);
                                if (memberBuilder != null)
                                    tb.WithMember(memberBuilder);

                                // it's enough to implement the first member if not explicit
                                if (!isExplicit)
                                    break;
                            }
                        }
                    }
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

        private static void BuildMemberGroups(NamedTypeSymbol declaringType, Dictionary<string, MembersGroup> memberGroupsByName = null)
        {
            memberGroupsByName ??= new Dictionary<string, MembersGroup>();

            // analyze the member entries
            var members = declaringType.GetMembers();
            var memberSortOrder = 0;
            foreach (var member in members)
            {
                if (!IsSupportedMember(member))
                    continue;

                // group by name
                var name = member.Name;
                if (member.IsExplicitInterfaceImplementation())
                    name = ExplicitInterfaceHelpers.GetMemberNameWithoutInterfaceName(name);

                if (!memberGroupsByName.TryGetValue(name, out var membersGroup))
                {
                    membersGroup = new MembersGroup() { Name = name, SortOrder = ++memberSortOrder };
                    memberGroupsByName[name] = membersGroup;
                }

                // get the members entry that matches the signature of the member
                var membersEntry = membersGroup.GetOrAddMemberEntryBySignature(member);

                // add the member to the group
                membersEntry.AddMember(member, declaringType);
            }
        }

        private static bool IsSupportedMember(Symbol member)
        {
            switch (member.Kind)
            {
                case SymbolKind.Property:
                    return true;
                case SymbolKind.Method:
                    {
                        var method = (MethodSymbol)member;

                        switch (method.MethodKind)
                        {
                            case MethodKind.Ordinary:
                                break;
                            default:
                                return false;
                        }

                        if (!method.IsDefinition) return false;

                        if (method is SourceOrdinaryMethodSymbol ordinarySourceMethod)
                        {
                            var (blockBody, exprBody) = ordinarySourceMethod.Bodies;
                            if (blockBody != null || exprBody != null) return false;
                        }

                        return true;
                    }
                case SymbolKind.Field:
                case SymbolKind.Event:
                default:
                    return false;
            }
        }

        private static TypeMemberBuilder GenerateMember(CSharpCompilation compilation, NamedTypeSymbol interfaceType, Symbol interfaceMember, bool isExplicitInterface = false)
        {
            switch (interfaceMember.Kind)
            {
                case SymbolKind.Property:
                    return compilation.GeneratedTypesManager.NewPropertyMember()
                        .FromSymbol((PropertySymbol)interfaceMember)
                        .ForInterface(interfaceType, isExplicitInterface, interfaceMember)
                        .WithAutoBackingField();
                case SymbolKind.Method:
                    return compilation.GeneratedTypesManager.NewMethodMember()
                        .FromSymbol((MethodSymbol)interfaceMember)
                        .ForInterface(interfaceType, isExplicitInterface, interfaceMember);
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

        private class MembersGroup
        {
            private int _nextMembersEntrySortOrder;

            public MembersGroup()
            {
                MemberEntriesBySignature = new Dictionary<Symbol, MembersEntry>(DefaultSignatureComparer);
            }

            public string Name { get; set; }
            public int SortOrder { get; set; }

            public Dictionary<Symbol, MembersEntry> MemberEntriesBySignature { get; set; }

            internal MembersEntry GetOrAddMemberEntryBySignature(Symbol member)
            {
                if (!MemberEntriesBySignature.TryGetValue(member, out var membersEntry))
                {
                    membersEntry = new MembersEntry() { Name = member.Name, SortOrder = _nextMembersEntrySortOrder++ };
                    MemberEntriesBySignature[member] = membersEntry;
                }

                return membersEntry;
            }
        }

        private class MembersEntry
        {
            public MembersEntry()
            {
                Members = new List<MemberEntry>();
            }

            public string Name { get; set; }

            public int SortOrder { get; set; }

            public List<MemberEntry> Members { get; set; }

            internal void AddMember(Symbol memberSymbol, NamedTypeSymbol declaringType)
            {
                Members.Add(new MemberEntry() {
                    MemberSymbol = memberSymbol,
                    DeclaringTypeSymbol = declaringType
                });
            }
        }

        private class MemberEntry
        {
            public NamedTypeSymbol DeclaringTypeSymbol { get; set; }
            public Symbol MemberSymbol { get; set; }
        }
    }
}
