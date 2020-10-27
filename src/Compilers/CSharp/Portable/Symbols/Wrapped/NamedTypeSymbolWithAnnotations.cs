// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal class NamedTypeSymbolWithAnnotations : NamedTypeSymbol
    {
        /// <summary>
        /// The underlying NamedTypeSymbol.
        /// </summary>
        protected readonly NamedTypeSymbol _underlyingType;

        public NamedTypeSymbolWithAnnotations(NamedTypeSymbol underlyingType, TypeSymbol annotationType, TypeAnnotationKind annotationTypeKind)
            : base(underlyingType.TupleData)
        {
            _underlyingType = underlyingType;

            AnnotationType = annotationType;
            AnnotationTypeKind = annotationTypeKind;
        }

        public NamedTypeSymbol UnderlyingType => _underlyingType;

        internal override TypeSymbol Self => _underlyingType;

        #region Wrapped

        public override bool IsImplicitlyDeclared => _underlyingType.IsImplicitlyDeclared;

        public override int Arity => _underlyingType.Arity;

        public override bool MightContainExtensionMethods => _underlyingType.MightContainExtensionMethods;

        public override string Name => _underlyingType.Name;

        public override string MetadataName => _underlyingType.MetadataName;

        internal override bool HasSpecialName => _underlyingType.HasSpecialName;

        internal override bool MangleName => _underlyingType.MangleName;

        public override string GetDocumentationCommentXml(CultureInfo preferredCulture = null, bool expandIncludes = false, CancellationToken cancellationToken = default(CancellationToken))
            => _underlyingType.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken);

        public override Accessibility DeclaredAccessibility => _underlyingType.DeclaredAccessibility;

        public override TypeKind TypeKind => _underlyingType.TypeKind;

        internal override bool IsInterface => _underlyingType.IsInterface;

        public override ImmutableArray<Location> Locations => _underlyingType.Locations;

        public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences => _underlyingType.DeclaringSyntaxReferences;

        public override bool IsStatic => _underlyingType.IsStatic;

        public override bool IsAbstract => _underlyingType.IsAbstract;

        internal override bool IsMetadataAbstract => _underlyingType.IsMetadataAbstract;

        public override bool IsSealed => _underlyingType.IsSealed;

        internal override bool IsMetadataSealed => _underlyingType.IsMetadataSealed;

        internal override bool HasCodeAnalysisEmbeddedAttribute => _underlyingType.HasCodeAnalysisEmbeddedAttribute;

        internal override ObsoleteAttributeData ObsoleteAttributeData => _underlyingType.ObsoleteAttributeData;

        internal override bool ShouldAddWinRTMembers => _underlyingType.ShouldAddWinRTMembers;

        internal override bool IsWindowsRuntimeImport => _underlyingType.IsWindowsRuntimeImport;

        internal override TypeLayout Layout => _underlyingType.Layout;

        internal override CharSet MarshallingCharSet => _underlyingType.MarshallingCharSet;

        public override bool IsSerializable => _underlyingType.IsSerializable;

        public override bool IsRefLikeType => _underlyingType.IsRefLikeType;

        public override bool IsReadOnly => _underlyingType.IsReadOnly;

        internal override bool HasDeclarativeSecurity => _underlyingType.HasDeclarativeSecurity;

        internal override IEnumerable<Microsoft.Cci.SecurityAttribute> GetSecurityInformation()
            => _underlyingType.GetSecurityInformation();

        internal override ImmutableArray<string> GetAppliedConditionalSymbols()
            => _underlyingType.GetAppliedConditionalSymbols();

        internal override AttributeUsageInfo GetAttributeUsageInfo()
            => _underlyingType.GetAttributeUsageInfo();

        internal override bool GetGuidString(out string guidString)
            => _underlyingType.GetGuidString(out guidString);
        #endregion

        #region Abstract impl

        public override NamedTypeSymbol OriginalDefinition => _underlyingType.OriginalDefinition;

        internal override TypeMap TypeSubstitution => _underlyingType.TypeSubstitution;

        public override ImmutableArray<TypeParameterSymbol> TypeParameters => _underlyingType.TypeParameters;

        public override NamedTypeSymbol ConstructedFrom => _underlyingType.ConstructedFrom;

        public override IEnumerable<string> MemberNames => _underlyingType.MemberNames;

        public override bool AreLocalsZeroed => _underlyingType.AreLocalsZeroed;

        public override Symbol ContainingSymbol => _underlyingType.ContainingSymbol;

        internal override ImmutableArray<TypeWithAnnotations> TypeArgumentsWithAnnotationsNoUseSiteDiagnostics => _underlyingType.TypeArgumentsWithAnnotationsNoUseSiteDiagnostics;

        internal override bool IsComImport => _underlyingType.IsComImport;

        internal override NamedTypeSymbol NativeIntegerUnderlyingType => _underlyingType.NativeIntegerUnderlyingType;

        internal override NamedTypeSymbol BaseTypeNoUseSiteDiagnostics => _underlyingType.BaseTypeNoUseSiteDiagnostics;

        public override ImmutableArray<Symbol> GetMembers() => _underlyingType.GetMembers();

        public override ImmutableArray<Symbol> GetMembers(string name) => _underlyingType.GetMembers(name);

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers() => _underlyingType.GetTypeMembers();

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name) => _underlyingType.GetTypeMembers(name);

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name, int arity) => _underlyingType.GetTypeMembers(name, arity);

        protected override NamedTypeSymbol WithTupleDataCore(TupleExtraData newData) => new NamedTypeSymbolWithAnnotations(_underlyingType, AnnotationType, AnnotationTypeKind.Value);

        internal override NamedTypeSymbol AsNativeInteger() => _underlyingType.AsNativeInteger();

        internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) => _underlyingType.GetDeclaredBaseType(basesBeingResolved);

        internal override ImmutableArray<NamedTypeSymbol> GetDeclaredInterfaces(ConsList<TypeSymbol> basesBeingResolved) => _underlyingType.GetDeclaredInterfaces(basesBeingResolved);

        internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers() => _underlyingType.GetEarlyAttributeDecodingMembers();

        internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers(string name) => _underlyingType.GetEarlyAttributeDecodingMembers(name);

        internal override IEnumerable<FieldSymbol> GetFieldsToEmit() => _underlyingType.GetFieldsToEmit();

        internal override ImmutableArray<NamedTypeSymbol> GetInterfacesToEmit() => _underlyingType.GetInterfacesToEmit();

        internal override ImmutableArray<NamedTypeSymbol> InterfacesNoUseSiteDiagnostics(ConsList<TypeSymbol> basesBeingResolved = null) => _underlyingType.InterfacesNoUseSiteDiagnostics(basesBeingResolved);
        #endregion

        #region Additional overrides
        public override void Accept(CSharpSymbolVisitor visitor) => _underlyingType.Accept(visitor);

        internal override TResult Accept<TArgument, TResult>(CSharpSymbolVisitor<TArgument, TResult> visitor, TArgument argument) => _underlyingType.Accept(visitor, argument);

        public override TResult Accept<TResult>(CSharpSymbolVisitor<TResult> visitor) => _underlyingType.Accept(visitor);

        internal override void AddDeclarationDiagnostics(DiagnosticBag diagnostics) => _underlyingType.AddDeclarationDiagnostics(diagnostics);

        internal override void AddNullableTransforms(ArrayBuilder<byte> transforms) => _underlyingType.AddNullableTransforms(transforms);

        internal override void AddSynthesizedAttributes(PEModuleBuilder moduleBuilder, ref ArrayBuilder<SynthesizedAttributeData> attributes) => _underlyingType.AddSynthesizedAttributes(moduleBuilder, ref attributes);

        internal override void AfterAddingTypeMembersChecks(ConversionsBase conversions, DiagnosticBag diagnostics) => _underlyingType.AfterAddingTypeMembersChecks(conversions, diagnostics);

        internal override bool ApplyNullableTransforms(byte defaultTransformFlag, ImmutableArray<byte> transforms, ref int position, out TypeSymbol result) => _underlyingType.ApplyNullableTransforms(defaultTransformFlag, transforms, ref position, out result);

        internal override NamedTypeSymbol AsMember(NamedTypeSymbol newOwner) => _underlyingType.AsMember(newOwner);

        internal override NamedTypeSymbol ComImportCoClass => _underlyingType.ComImportCoClass;

        protected internal override NamedTypeSymbol ConstructCore(ImmutableArray<TypeWithAnnotations> typeArguments, bool unbound) => _underlyingType.ConstructCore(typeArguments, unbound);

        public override AssemblySymbol ContainingAssembly => _underlyingType.ContainingAssembly;

        internal override ModuleSymbol ContainingModule => _underlyingType.ContainingModule;

        public override NamespaceSymbol ContainingNamespace => _underlyingType.ContainingNamespace;

        public override NamedTypeSymbol ContainingType => _underlyingType.ContainingType;

        protected internal override ISymbol CreateISymbol() => _underlyingType.CreateISymbol();

        protected internal override ITypeSymbol CreateITypeSymbol(CodeAnalysis.NullableAnnotation nullableAnnotation) => _underlyingType.CreateITypeSymbol(nullableAnnotation);

        internal override CSharpCompilation DeclaringCompilation => _underlyingType.DeclaringCompilation;

        internal override void DecodeWellKnownAttribute(ref DecodeWellKnownAttributeArguments<AttributeSyntax, CSharpAttributeData, AttributeLocation> arguments) => _underlyingType.DecodeWellKnownAttribute(ref arguments);

        internal override CSharpAttributeData EarlyDecodeWellKnownAttribute(ref EarlyDecodeWellKnownAttributeArguments<EarlyWellKnownAttributeBinder, NamedTypeSymbol, AttributeSyntax, AttributeLocation> arguments)
            => _underlyingType.EarlyDecodeWellKnownAttribute(ref arguments);

        internal override void EarlyDecodeWellKnownAttributeType(NamedTypeSymbol attributeType, AttributeSyntax attributeSyntax) => _underlyingType.EarlyDecodeWellKnownAttributeType(attributeType, attributeSyntax);

        public override NamedTypeSymbol EnumUnderlyingType => _underlyingType.EnumUnderlyingType;

        internal override bool Equals(TypeSymbol t2, TypeCompareKind comparison, IReadOnlyDictionary<TypeParameterSymbol, bool> isValueTypeOverrideOpt = null)
        {
            if (t2 is NamedTypeSymbolWithAnnotations other)
            {
                return _underlyingType.Equals(other._underlyingType, comparison, isValueTypeOverrideOpt);
            }

            return _underlyingType.Equals(t2, comparison, isValueTypeOverrideOpt);
        }

        internal override FieldSymbol FixedElementField => _underlyingType.FixedElementField;

        internal override void ForceComplete(SourceLocation locationOpt, CancellationToken cancellationToken)
            => _underlyingType.ForceComplete(locationOpt, cancellationToken);

        protected internal override ImmutableArray<NamedTypeSymbol> GetAllInterfaces() => _underlyingType.GetAllInterfaces();

        public override ImmutableArray<CSharpAttributeData> GetAttributes() => _underlyingType.GetAttributes();

        internal override AttributeTargets GetAttributeTarget() => _underlyingType.GetAttributeTarget();

        internal override IEnumerable<CSharpAttributeData> GetCustomAttributesToEmit(PEModuleBuilder moduleBuilder) => _underlyingType.GetCustomAttributesToEmit(moduleBuilder);

        internal override string GetDebuggerDisplay() => _underlyingType.GetDebuggerDisplay();

        public override string GetDocumentationCommentId() => _underlyingType.GetDocumentationCommentId();

        internal override IEnumerable<EventSymbol> GetEventsToEmit() => _underlyingType.GetEventsToEmit();

        public override int GetHashCode() => _underlyingType.GetHashCode();

        internal override IEnumerable<Symbol> GetInstanceFieldsAndEvents() => _underlyingType.GetInstanceFieldsAndEvents();

        internal override LexicalSortKey GetLexicalSortKey() => _underlyingType.GetLexicalSortKey();

        internal override byte? GetLocalNullableContextValue() => _underlyingType.GetLocalNullableContextValue();

        internal override ManagedKind GetManagedKind(ref HashSet<DiagnosticInfo> useSiteDiagnostics) => _underlyingType.GetManagedKind(ref useSiteDiagnostics);

        internal override ImmutableArray<Symbol> GetMembersUnordered() => _underlyingType.GetMembersUnordered();

        internal override IEnumerable<MethodSymbol> GetMethodsToEmit() => _underlyingType.GetMethodsToEmit();

        internal override byte? GetNullableContextValue() => _underlyingType.GetNullableContextValue();

        internal override IEnumerable<PropertySymbol> GetPropertiesToEmit() => _underlyingType.GetPropertiesToEmit();

        internal override ImmutableArray<Symbol> GetSimpleNonTypeMembers(string name) => _underlyingType.GetSimpleNonTypeMembers(name);

        internal override ImmutableArray<NamedTypeSymbol> GetTypeMembersUnordered() => _underlyingType.GetTypeMembersUnordered();

        internal override bool GetUnificationUseSiteDiagnosticRecursive(ref DiagnosticInfo result, Symbol owner, ref HashSet<TypeSymbol> checkedTypes)
            => _underlyingType.GetUnificationUseSiteDiagnosticRecursive(ref result, owner, ref checkedTypes);

        internal override DiagnosticInfo GetUseSiteDiagnostic() => _underlyingType.GetUseSiteDiagnostic();

        internal override bool HasComplete(CompletionPart part) => _underlyingType.HasComplete(part);

        protected internal override int HighestPriorityUseSiteError => _underlyingType.HighestPriorityUseSiteError;

        public override bool IsAnonymousType => _underlyingType.IsAnonymousType;

        internal override bool IsDefinedInSourceTree(SyntaxTree tree, TextSpan? definedWithinSpan, CancellationToken cancellationToken = default)
            => _underlyingType.IsDefinedInSourceTree(tree, definedWithinSpan, cancellationToken);

        internal override bool IsDirectlyExcludedFromCodeCoverage => _underlyingType.IsDirectlyExcludedFromCodeCoverage;

        internal override bool IsExplicitDefinitionOfNoPiaLocalType => _underlyingType.IsExplicitDefinitionOfNoPiaLocalType;

        public override bool IsImplicitClass => _underlyingType.IsImplicitClass;

        internal override bool IsNativeIntegerType => _underlyingType.IsNativeIntegerType;

        public override bool IsReferenceType => _underlyingType.IsReferenceType;

        public override bool IsScriptClass => _underlyingType.IsScriptClass;

        public override bool IsUnboundGenericType => _underlyingType.IsUnboundGenericType;

        public override bool IsValueType => _underlyingType.IsValueType;

        public override SymbolKind Kind => _underlyingType.Kind;

        internal override bool KnownCircularStruct => _underlyingType.KnownCircularStruct;

        internal override NamedTypeSymbol LookupMetadataType(ref MetadataTypeName emittedTypeName) => _underlyingType.LookupMetadataType(ref emittedTypeName);

        protected internal override ImmutableArray<NamedTypeSymbol> MakeAllInterfaces() => _underlyingType.MakeAllInterfaces();

        internal override int? MemberIndexOpt => _underlyingType.MemberIndexOpt;

        internal override TypeSymbol MergeEquivalentTypes(TypeSymbol other, VarianceKind variance) => _underlyingType.MergeEquivalentTypes(other, variance);

        internal override void PostDecodeWellKnownAttributes(ImmutableArray<CSharpAttributeData> boundAttributes, ImmutableArray<AttributeSyntax> allAttributeSyntaxNodes, DiagnosticBag diagnostics, AttributeLocation symbolPart, WellKnownAttributeData decodedData)
            => _underlyingType.PostDecodeWellKnownAttributes(boundAttributes, allAttributeSyntaxNodes, diagnostics, symbolPart, decodedData);

        internal override void PostEarlyDecodeWellKnownAttributeTypes() => _underlyingType.PostEarlyDecodeWellKnownAttributeTypes();

        internal override bool RequiresCompletion => _underlyingType.RequiresCompletion;

        internal override TypeSymbol SetNullabilityForReferenceTypes(Func<TypeWithAnnotations, TypeWithAnnotations> transform) => _underlyingType.SetNullabilityForReferenceTypes(transform);

        public override SpecialType SpecialType => _underlyingType.SpecialType;
        #endregion
    }
}
