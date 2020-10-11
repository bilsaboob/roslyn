// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    public class TypeCache
    {
        private Dictionary<string, Entry> _types;

        public TypeCache()
        {
        }

        public void Add(ITypeSymbol type)
        {
            if (_types == null) _types = new Dictionary<string, Entry>();

            var id = type.ToString();
            if (!_types.TryGetValue(id, out var entry))
            {
                entry = new Entry() { Type = type };
                _types[id] = entry;
            }
        }

        public T TryGetMetadata<T>(string metadataId, ITypeSymbol type, Func<ITypeSymbol, T> getMetadataFn)
        {
            Add(type);

            var id = type.ToString();
            var entry = _types[id];

            return (T)(object)entry.TryGetMetadata(metadataId, getMetadataFn);
        }

        public void Free()
        {
            if (_types != null)
            {
                foreach (var entry in _types.Values)
                    entry.Free();

                _types.Clear();
            }
            _types = null;
        }

        class Entry
        {
            private Dictionary<string, object> _metadata;

            public ITypeSymbol Type { get; set; }

            public object TryGetMetadata<T>(string metadataId, Func<ITypeSymbol, T> getMetadataFn)
            {
                if (_metadata == null) _metadata = new Dictionary<string, object>();

                if (!_metadata.TryGetValue(metadataId, out var value))
                {
                    value = getMetadataFn(Type);
                    _metadata[metadataId] = value;
                }

                return value;
            }

            public void Free()
            {
                if (_metadata != null)
                {
                    _metadata.Clear();
                    _metadata = null;
                }

                Type = null;
            }
        }
    }

    public static partial class TypeHelpers
    {
        internal static ImmutableArray<Symbol> GetAllMembers(TypeSymbol type, Func<Symbol, bool> memberFilter = null)
        {
            var membersByName = new Dictionary<string, Symbol>();

            var t = type;

            while (!(t is null) && IsUserDefinedType(t))
            {
                // collect the members from the type - only add if don't already exist
                foreach (var m in t.GetMembersUnordered())
                {
                    if (memberFilter?.Invoke(m) == false) continue;
                    if (!membersByName.ContainsKey(m.Name))
                        membersByName[m.Name] = m;
                }

                // collect the members from any of the interfaces
                foreach (var i in t.AllInterfacesNoUseSiteDiagnostics)
                {
                    foreach (var m in i.GetMembersUnordered())
                    {
                        if (memberFilter?.Invoke(m) == false) continue;
                        if (!membersByName.ContainsKey(m.Name))
                            membersByName[m.Name] = m;
                    }
                }

                // go to the base type and repeat
                t = t.BaseTypeNoUseSiteDiagnostics;
            }

            return membersByName.Values.ToImmutableArray();
        }

        internal static ImmutableArray<ISymbol> GetAllMembers(ITypeSymbol type, Func<ISymbol, bool> memberFilter = null)
        {
            var membersByName = new Dictionary<string, ISymbol>();

            var t = type;

            while (!(t is null) && IsUserDefinedType(t))
            {
                // collect the members from the type - only add if don't already exist
                foreach (var m in t.GetMembers())
                {
                    if (memberFilter?.Invoke(m) == false) continue;
                    if (!membersByName.ContainsKey(m.Name))
                        membersByName[m.Name] = m;
                }

                // collect the members from any of the interfaces
                foreach (var i in t.AllInterfaces)
                {
                    foreach (var m in i.GetMembers())
                    {
                        if (memberFilter?.Invoke(m) == false) continue;
                        if (!membersByName.ContainsKey(m.Name))
                            membersByName[m.Name] = m;
                    }
                }

                // go to the base type and repeat
                t = t.BaseType;
            }

            return membersByName.Values.ToImmutableArray();
        }

        internal static bool IsUserDefinedType(TypeSymbol type)
        {
            if (type is null) return false;

            var name = type.Name.ToLowerInvariant();

            if (IsPredefinedType(name)) return false;

            return true;
        }

        public static bool IsUserDefinedType(ITypeSymbol type)
        {
            if (type is null) return false;

            var name = type.Name.ToLowerInvariant();

            if (IsPredefinedType(name)) return false;

            return true;
        }

        private static bool IsPredefinedType(string name)
        {
            switch (name)
            {
                case "object":
                case "string":
                case "bool":
                case "int":
                case "int32":
                case "uint":
                case "char":
                case "void":
                case "long":
                case "float":
                case "double":
                case "boolean":
                case "byte":
                case "int64":
                case "uint32":
                case "uint64":
                case "sbyte":
                case "short":
                case "ulong":
                case "decimal":
                    return true;
                default:
                    return false;
            }
        }
    }

    public static partial class SpreadParamHelpers
    {
        internal static bool IsValidSpreadArgType(TypeSymbol type)
        {
            // must have a default constructor
            var defaultConstructor = (type as NamedTypeSymbol)?.InstanceConstructors.FirstOrDefault(ctor => ctor.ParameterCount == 0);
            return defaultConstructor != null;
        }

        public static IEnumerable<ISymbol> GetPossibleSpreadParamMembers(ITypeSymbol type)
        {
            return TypeHelpers.GetAllMembers(type, IsPossibleSpreadMember).Where(m => m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property);
        }

        internal static Symbol GetFirstPossibleSpreadParamMember(IEnumerable<Symbol> members, string name)
        {
            return members.FirstOrDefault(m => IsPossibleSpreadMember(m) && m.Name == name);
        }

        internal static ISymbol GetFirstPossibleSpreadParamMember(IEnumerable<ISymbol> members, string name)
        {
            return members.FirstOrDefault(m => IsPossibleSpreadMember(m) && m.Name == name);
        }

        internal static Symbol GetFirstPossibleSpreadParamMember(TypeSymbol type, string name)
        {
            if (type is null) return null;
            return GetFirstPossibleSpreadParamMember(TypeHelpers.GetAllMembers(type, IsPossibleSpreadMember), name);
        }

        internal static (Symbol, int?) GetFirstMatchingSpreadParam(ImmutableArray<ParameterSymbol> parameters, string name)
        {
            // if now matching named parameter, we can try with the spread parameters
            for (int p = 0; p < parameters.Length; ++p)
            {
                var param = parameters[p];
                if (!param.IsSpread) continue;

                // match the parameter to the spread
                var spreadMember = GetFirstPossibleSpreadParamMember(TypeHelpers.GetAllMembers(param.Type, IsPossibleSpreadMember), name);
                if (spreadMember != null)
                {
                    return (param, p);
                }
            }

            return (null, null);
        }

        internal static (Symbol, Symbol) GetFirstMatchingSpreadParamAndMember(ImmutableArray<ParameterSymbol> parameters, string name)
        {
            // if now matching named parameter, we can try with the spread parameters
            for (int p = 0; p < parameters.Length; ++p)
            {
                var param = parameters[p];
                if (!param.IsSpread) continue;

                // match the parameter to the spread
                var spreadMember = GetFirstPossibleSpreadParamMember(TypeHelpers.GetAllMembers(param.Type, IsPossibleSpreadMember), name);
                if (spreadMember != null)
                {
                    return (param, spreadMember);
                }
            }

            return (null, null);
        }

        internal static bool IsPossibleSpreadMember(Symbol member) => member.Kind == SymbolKind.Field || member.Kind == SymbolKind.Property;

        internal static bool IsPossibleSpreadMember(ISymbol member) => member.Kind == SymbolKind.Field || member.Kind == SymbolKind.Property;

        public static ISymbol GetFirstPossibleSpreadParamMember(string name, ITypeSymbol type, TypeCache typeCache)
        {
            return typeCache.TryGetMetadata($"SpreadParamMember_{name}", type, t => {
                return GetFirstPossibleSpreadParamMember(TypeHelpers.GetAllMembers(type, IsPossibleSpreadMember), name);
            });
        }
    }
}
