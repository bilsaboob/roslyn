// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

using static Microsoft.CodeAnalysis.CodeGeneration.CodeGenerationHelpers;
using static Microsoft.CodeAnalysis.CSharp.CodeGeneration.CSharpCodeGenerationHelpers;

namespace Microsoft.CodeAnalysis.CSharp.CodeGeneration
{
    internal static class PropertyGenerator
    {
        public static bool CanBeGenerated(IPropertySymbol property)
            => property.IsIndexer || property.Parameters.Length == 0;

        private static MemberDeclarationSyntax LastPropertyOrField(
            SyntaxList<MemberDeclarationSyntax> members)
        {
            var lastProperty = members.LastOrDefault(m => m is PropertyDeclarationSyntax);
            return lastProperty ?? LastField(members);
        }

        internal static CompilationUnitSyntax AddPropertyTo(
            CompilationUnitSyntax destination,
            IPropertySymbol property,
            CodeGenerationOptions options,
            IList<bool> availableIndices)
        {
            var declaration = GeneratePropertyOrIndexer(
                property, CodeGenerationDestination.CompilationUnit, options,
                destination?.SyntaxTree.Options ?? options.ParseOptions);

            var members = Insert(destination.Members, declaration, options,
                availableIndices, after: LastPropertyOrField, before: FirstMember);
            return destination.WithMembers(members);
        }

        internal static TypeDeclarationSyntax AddPropertyTo(
            TypeDeclarationSyntax destination,
            IPropertySymbol property,
            CodeGenerationOptions options,
            IList<bool> availableIndices)
        {
            var declaration = GeneratePropertyOrIndexer(property, GetDestination(destination),
                options, destination?.SyntaxTree.Options ?? options.ParseOptions);

            // Create a clone of the original type with the new method inserted. 
            var members = Insert(destination.Members, declaration, options,
                availableIndices, after: LastPropertyOrField, before: FirstMember);

            // Find the best place to put the field.  It should go after the last field if we already
            // have fields, or at the beginning of the file if we don't.
            return AddMembersTo(destination, members);
        }

        public static MemberDeclarationSyntax GeneratePropertyOrIndexer(
            IPropertySymbol property,
            CodeGenerationDestination destination,
            CodeGenerationOptions options,
            ParseOptions parseOptions)
        {
            var reusableSyntax = GetReuseableSyntaxNodeForSymbol<MemberDeclarationSyntax>(property, options);
            if (reusableSyntax != null)
            {
                return reusableSyntax;
            }

            var declaration = property.IsIndexer
                ? GenerateIndexerDeclaration(property, destination, options, parseOptions)
                : GeneratePropertyDeclaration(property, destination, options, parseOptions);

            return ConditionallyAddDocumentationCommentTo(declaration, property, options);
        }

        private static MemberDeclarationSyntax GenerateIndexerDeclaration(
            IPropertySymbol property,
            CodeGenerationDestination destination,
            CodeGenerationOptions options,
            ParseOptions parseOptions)
        {
            var explicitInterfaceSpecifier = GenerateExplicitInterfaceSpecifier(property.ExplicitInterfaceImplementations);

            var accessorList = GenerateAccessorList(property, destination, options, parseOptions, out var allowExpressionBody);

            accessorList = CleanupAccessorBodies(accessorList, includeNotImplementedStatement: false);

            var returnType = GenerateTypeSyntax(property);
            if (returnType?.Width() > 0)
                returnType = returnType.WithLeadingTrivia(SyntaxFactory.ElasticWhitespace(" "));

            var declaration = SyntaxFactory.IndexerDeclaration(
                    attributeLists: AttributeGenerator.GenerateAttributeLists(property.GetAttributes(), options),
                    modifiers: GenerateModifiers(property, destination, options),
                    type: returnType,
                    explicitInterfaceSpecifier: explicitInterfaceSpecifier,
                    parameterList: ParameterGenerator.GenerateBracketedParameterList(property.Parameters, explicitInterfaceSpecifier != null, options),
                    accessorList: accessorList?.WithLeadingTrivia(SyntaxFactory.ElasticWhitespace(" "))
            );

            if (allowExpressionBody)
                declaration = UseExpressionBodyIfDesired(options, declaration, parseOptions);

            return AddFormatterAndCodeGeneratorAnnotationsTo(
                AddAnnotationsTo(property, declaration));
        }

        private static MemberDeclarationSyntax GeneratePropertyDeclaration(
           IPropertySymbol property, CodeGenerationDestination destination,
           CodeGenerationOptions options, ParseOptions parseOptions)
        {
            var initializer = CodeGenerationPropertyInfo.GetInitializer(property) is ExpressionSyntax initializerNode
                ? SyntaxFactory.EqualsValueClause(initializerNode)
                : null;

            var explicitInterfaceSpecifier = GenerateExplicitInterfaceSpecifier(property.ExplicitInterfaceImplementations);

            var accessorList = GenerateAccessorList(property, destination, options, parseOptions, out var allowExpressionBody);

            accessorList = CleanupAccessorBodies(accessorList, includeNotImplementedStatement: false);

            var returnType = GenerateTypeSyntax(property);
            if (returnType?.Width() > 0)
                returnType = returnType.WithLeadingTrivia(SyntaxFactory.ElasticWhitespace(" "));

            var propertyDeclaration = SyntaxFactory.PropertyDeclaration(
                attributeLists: AttributeGenerator.GenerateAttributeLists(property.GetAttributes(), options),
                modifiers: GenerateModifiers(property, destination, options),
                explicitInterfaceSpecifier: explicitInterfaceSpecifier,
                identifier: property.Name.ToIdentifierToken().WithLeadingTrivia(SyntaxFactory.ElasticWhitespace(" ")),
                type: returnType,
                accessorList: accessorList?.WithLeadingTrivia(SyntaxFactory.ElasticWhitespace(" ")),
                expressionBody: null,
                initializer: initializer);

            if (allowExpressionBody)
                propertyDeclaration = UseExpressionBodyIfDesired(options, propertyDeclaration, parseOptions);

            if (returnType.Width() == 0 && propertyDeclaration.ExpressionBody != null)
            {
                propertyDeclaration = propertyDeclaration.WithExpressionBody(propertyDeclaration.ExpressionBody.WithLeadingTrivia(SyntaxFactory.ElasticWhitespace(" ")));
            }

            var str = propertyDeclaration.ToString();

            return AddFormatterAndCodeGeneratorAnnotationsTo(AddAnnotationsTo(property, propertyDeclaration));
        }

        private static TypeSyntax GenerateTypeSyntax(IPropertySymbol property)
        {
            if (property.IsOverride && property.GetOverriddenSymbolSyntax<PropertyDeclarationSyntax>(out var overriddenPropertySyntax))
            {
                if (!overriddenPropertySyntax.HasExplicitReturnType())
                {
                    return SyntaxFactory.FakeTypeIdentifier();
                }
            }

            var returnType = property.Type;

            if (property.ReturnsByRef)
            {
                return returnType.GenerateRefTypeSyntax();
            }
            else if (property.ReturnsByRefReadonly)
            {
                return returnType.GenerateRefReadOnlyTypeSyntax();
            }
            else
            {
                return returnType.GenerateTypeSyntax();
            }
        }

        private static bool TryGetExpressionBody(
            BasePropertyDeclarationSyntax baseProperty, ParseOptions options, ExpressionBodyPreference preference,
            out ArrowExpressionClauseSyntax arrowExpression, out SyntaxToken semicolonToken)
        {
            var accessorList = baseProperty.AccessorList;
            if (preference != ExpressionBodyPreference.Never &&
                accessorList.Accessors.Count == 1)
            {
                var accessor = accessorList.Accessors[0];
                if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                {
                    return TryGetArrowExpressionBody(
                        baseProperty.Kind(), accessor, options, preference,
                        out arrowExpression, out semicolonToken);
                }
            }

            arrowExpression = null;
            semicolonToken = default;
            return false;
        }

        private static PropertyDeclarationSyntax UseExpressionBodyIfDesired(
            CodeGenerationOptions options, PropertyDeclarationSyntax declaration, ParseOptions parseOptions)
        {
            if (declaration.ExpressionBody == null)
            {
                var expressionBodyPreference = options.Options.GetOption(CSharpCodeStyleOptions.PreferExpressionBodiedProperties).Value;
                if (declaration.Initializer == null)
                {
                    if (TryGetExpressionBody(
                            declaration, parseOptions, expressionBodyPreference,
                            out var expressionBody, out var semicolonToken))
                    {
                        declaration = declaration.WithAccessorList(null)
                                                 .WithExpressionBody(expressionBody)
                                                 .WithSemicolonToken(semicolonToken);
                    }
                }
            }

            return declaration;
        }

        private static IndexerDeclarationSyntax UseExpressionBodyIfDesired(
            CodeGenerationOptions options, IndexerDeclarationSyntax declaration, ParseOptions parseOptions)
        {
            if (declaration.ExpressionBody == null)
            {
                var expressionBodyPreference = options.Options.GetOption(CSharpCodeStyleOptions.PreferExpressionBodiedIndexers).Value;
                if (TryGetExpressionBody(
                        declaration, parseOptions, expressionBodyPreference,
                        out var expressionBody, out var semicolonToken))
                {
                    declaration = declaration.WithAccessorList(null)
                                             .WithExpressionBody(expressionBody)
                                             .WithSemicolonToken(semicolonToken);
                }
            }

            return declaration;
        }

        private static AccessorDeclarationSyntax UseExpressionBodyIfDesired(
            CodeGenerationOptions options, AccessorDeclarationSyntax declaration, ParseOptions parseOptions)
        {
            if (declaration.ExpressionBody == null)
            {
                var expressionBodyPreference = options.Options.GetOption(CSharpCodeStyleOptions.PreferExpressionBodiedAccessors).Value;
                if (declaration.Body.TryConvertToArrowExpressionBody(
                        declaration.Kind(), parseOptions, expressionBodyPreference,
                        out var expressionBody, out var semicolonToken))
                {
                    declaration = declaration.WithBody(null)
                                             .WithExpressionBody(expressionBody)
                                             .WithSemicolonToken(semicolonToken);
                }
            }

            return declaration;
        }

        private static bool TryGetArrowExpressionBody(
            SyntaxKind declaratoinKind, AccessorDeclarationSyntax accessor, ParseOptions options, ExpressionBodyPreference preference,
            out ArrowExpressionClauseSyntax arrowExpression, out SyntaxToken semicolonToken)
        {
            // If the accessor has an expression body already, then use that as the expression body
            // for the property.
            if (accessor.ExpressionBody != null)
            {
                arrowExpression = accessor.ExpressionBody;
                semicolonToken = accessor.SemicolonToken;
                return true;
            }

            return accessor.Body.TryConvertToArrowExpressionBody(
                declaratoinKind, options, preference, out arrowExpression, out semicolonToken);
        }

        private static AccessorListSyntax GenerateAccessorList(
            IPropertySymbol property, CodeGenerationDestination destination,
            CodeGenerationOptions options, ParseOptions parseOptions, out bool allowExpressionBody)
        {
            allowExpressionBody = true;

            var accessorList = GenerateAccessorList_(property, destination, options, parseOptions);

            // analyze the accessors
            var hasAnyAccessorBody = accessorList.Accessors.Any(a => a.Body != null || a.ExpressionBody != null);

            var getAccessor = accessorList.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
            var hasGetAccessor = getAccessor != null;

            var setAccessor = accessorList.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
            var hasSetAccessor = setAccessor != null;

            // check if we have info about the original symbol for which we are creating - if that symbol is defined in a certain way, we should do the same
            var sourceSymbol = (property as CodeGenerationSymbol)?.OriginalSymbol ?? property.GetOverriddenSymbol();

            // try fetching the source property syntax and check if the original definition has any bodies
            sourceSymbol.GetSourceSymbolSyntax<PropertyDeclarationSyntax>(out var sourcePropertySyntax);
            if (sourcePropertySyntax != null)
            {
                if (hasAnyAccessorBody && !property.IsOverride)
                    hasAnyAccessorBody = sourcePropertySyntax?.AccessorList?.Accessors.Any(a => a.Body != null || a.ExpressionBody != null) == true || sourcePropertySyntax.ExpressionBody?.Expression != null;

                if (hasGetAccessor)
                    hasGetAccessor = sourcePropertySyntax?.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) != null;

                if (hasSetAccessor)
                    hasSetAccessor = sourcePropertySyntax?.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)) != null;
            }

            if (!hasAnyAccessorBody)
            {
                // if no bodies are available, then don't generate any accessor at all, just leave it "empty"
                accessorList = SyntaxFactory.FakeAccessorList();
            }
            else if (hasGetAccessor && !hasSetAccessor)
            {
                // check if the base syntax is an inline block getter
                if (sourcePropertySyntax?.AccessorList?.OpenBraceToken.Width() == 0)
                {
                    accessorList = SyntaxFactory.AccessorList(
                        openBraceToken: SyntaxFactory.FakeToken(SyntaxKind.OpenBraceToken),
                        accessors: SyntaxFactory.List<AccessorDeclarationSyntax>(new[] { SyntaxFactory.GetInlineAccessorDeclaration(
                                kind: SyntaxKind.GetAccessorDeclaration,
                                attributeLists: getAccessor.AttributeLists,
                                modifiers: getAccessor.Modifiers,
                                SyntaxFactory.Block(getAccessor.Body?.Statements ?? SyntaxFactory.List<StatementSyntax>(new [] {SyntaxFactory.ReturnStatement(getAccessor.ExpressionBody?.Expression) }))
                                    .WithLeadingTrivia(SyntaxFactory.Whitespace(" "))
                            ) }),
                        closeBraceToken: SyntaxFactory.FakeToken(SyntaxKind.CloseBraceToken)
                    );

                    allowExpressionBody = false;
                }
            }
            else if (hasGetAccessor && hasSetAccessor)
            {
                accessorList = SyntaxFactory.AccessorList(
                    accessors: SyntaxFactory.List<AccessorDeclarationSyntax>(new[] {
                        getAccessor,
                        setAccessor.WithLeadingTrivia(SyntaxFactory.EndOfLine(System.Environment.NewLine))
                    })
                );
            }

            return accessorList;
        }

        private static AccessorListSyntax CleanupAccessorBodies(AccessorListSyntax accessorList, bool includeNotImplementedStatement = true)
        {
            if (accessorList.Accessors == null) return accessorList;

            var accessors = accessorList.Accessors.ToList();

            for (var a = 0; a < accessors.Count; ++a)
            {
                var accessor = accessors[a];
                var isGetter = accessor.IsKind(SyntaxKind.GetAccessorDeclaration, SyntaxKind.AddAccessorDeclaration, SyntaxKind.InitAccessorDeclaration);

                var bodyStatements = accessor.Body?.Statements;
                if (bodyStatements != null)
                {
                    var statements = bodyStatements.OfType<StatementSyntax>().ToList();
                    if (statements?.Count > 0)
                    {
                        // trim the semicolons
                        for (var i = 0; i < statements.Count; ++i)
                        {
                            var stat = statements[i];

                            if (stat is ReturnStatementSyntax returnStat)
                            {
                                statements[i] = returnStat.Update(returnStat.ReturnKeyword, returnStat.Expression.WithTrailingTrivia(SyntaxFactory.Whitespace(" ")), SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken));
                            }
                            else if (stat is ExpressionStatementSyntax exprStat)
                            {
                                statements[statements.Count - 1] = exprStat.Update(exprStat.Expression.WithTrailingTrivia(SyntaxFactory.Whitespace(" ")), SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken));
                            }
                        }

                        // remove the throw statements
                        if (!includeNotImplementedStatement && statements.Count == 1 && statements[0] is ThrowStatementSyntax)
                        {
                            statements.RemoveAt(0);
                            if (isGetter)
                            {
                                // add a default return instead for all "getter" accessors
                                statements.Add(SyntaxFactory.ReturnStatement(
                                    default,
                                    SyntaxFactory.Token(SyntaxKind.ReturnKeyword),
                                    SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression, SyntaxFactory.Token(SyntaxKind.DefaultKeyword))
                                        .WithTrailingTrivia(SyntaxFactory.Whitespace(" ")),
                                    SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken)
                                ));
                            }
                            else
                            {
                                // replace all setters with an empty body
                                statements.Add(SyntaxFactory.EmptyStatement());
                            }
                        }
                    }

                    accessors[a] = accessor.WithBody(
                        accessor.Body.WithStatements(SyntaxFactory.List(statements))
                    );
                    continue;
                }

                var bodyExpr = accessor.ExpressionBody?.Expression;
                if (bodyExpr != null)
                {
                    // trim the semicolon
                    accessor = accessor.WithSemicolonToken(SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken));

                    if (!includeNotImplementedStatement && bodyExpr is ThrowExpressionSyntax throwExpr)
                    {
                        // replace throw expression with an empty expression
                        if (isGetter)
                        {
                            // add a default return instead for all "getter" accessors
                            accessor = accessor.WithExpressionBody(accessor.ExpressionBody.WithExpression(
                                SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression, SyntaxFactory.Token(SyntaxKind.DefaultKeyword))
                                    .WithTrailingTrivia(SyntaxFactory.Whitespace(" "))
                            ));
                        }
                        else
                        {
                            // add an empty block body instead
                            accessor = accessor.WithExpressionBody(null).WithBody(
                                SyntaxFactory.Block(
                                    openBraceToken: SyntaxFactory.Token(SyntaxKind.OpenBraceToken),
                                    SyntaxFactory.List<StatementSyntax>(),
                                    closeBraceToken: SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                                )
                            ).WithLeadingTrivia(SyntaxFactory.EndOfLine(System.Environment.NewLine));
                        }
                    }

                    accessors[a] = accessor;
                    continue;
                }
            }

            return accessorList.WithAccessors(SyntaxFactory.List(accessors));
        }

        private static AccessorListSyntax GenerateAccessorList_(
            IPropertySymbol property, CodeGenerationDestination destination,
            CodeGenerationOptions options, ParseOptions parseOptions)
        {
            var setAccessorKind = property.SetMethod?.IsInitOnly == true ? SyntaxKind.InitAccessorDeclaration : SyntaxKind.SetAccessorDeclaration;
            var accessors = new List<AccessorDeclarationSyntax>
            {
                GenerateAccessorDeclaration(property, property.GetMethod, SyntaxKind.GetAccessorDeclaration, destination, options, parseOptions),
                GenerateAccessorDeclaration(property, property.SetMethod, setAccessorKind, destination, options, parseOptions),
            };

            return accessors[0] == null && accessors[1] == null
                ? null
                : SyntaxFactory.AccessorList(accessors.WhereNotNull().ToSyntaxList());
        }

        private static AccessorDeclarationSyntax GenerateAccessorDeclaration(
            IPropertySymbol property,
            IMethodSymbol accessor,
            SyntaxKind kind,
            CodeGenerationDestination destination,
            CodeGenerationOptions options,
            ParseOptions parseOptions)
        {
            var hasBody = options.GenerateMethodBodies && HasAccessorBodies(property, destination, accessor);
            return accessor == null
                ? null
                : GenerateAccessorDeclaration(property, accessor, kind, hasBody, options, parseOptions);
        }

        private static AccessorDeclarationSyntax GenerateAccessorDeclaration(
            IPropertySymbol property,
            IMethodSymbol accessor,
            SyntaxKind kind,
            bool hasBody,
            CodeGenerationOptions options,
            ParseOptions parseOptions)
        {
            var declaration = SyntaxFactory.AccessorDeclaration(kind)
                                           .WithModifiers(GenerateAccessorModifiers(property, accessor, options))
                                           .WithBody(hasBody ? GenerateBlock(accessor) : null)
                                           .WithSemicolonToken(hasBody ? default : SyntaxFactory.Token(SyntaxKind.SemicolonToken));

            declaration = UseExpressionBodyIfDesired(options, declaration, parseOptions);

            return AddAnnotationsTo(accessor, declaration);
        }

        private static BlockSyntax GenerateBlock(IMethodSymbol accessor)
        {
            return SyntaxFactory.Block(
                StatementGenerator.GenerateStatements(CodeGenerationMethodInfo.GetStatements(accessor)));
        }

        private static bool HasAccessorBodies(
            IPropertySymbol property,
            CodeGenerationDestination destination,
            IMethodSymbol accessor)
        {
            return destination != CodeGenerationDestination.InterfaceType &&
                !property.IsAbstract &&
                accessor != null &&
                !accessor.IsAbstract;
        }

        private static SyntaxTokenList GenerateAccessorModifiers(
            IPropertySymbol property,
            IMethodSymbol accessor,
            CodeGenerationOptions options)
        {
            var modifiers = ArrayBuilder<SyntaxToken>.GetInstance();

            if (accessor.DeclaredAccessibility != Accessibility.NotApplicable &&
                accessor.DeclaredAccessibility != property.DeclaredAccessibility)
            {
                AddAccessibilityModifiers(accessor.DeclaredAccessibility, modifiers, options, property.DeclaredAccessibility);
            }

            var hasNonReadOnlyAccessor = property.GetMethod?.IsReadOnly == false || property.SetMethod?.IsReadOnly == false;
            if (hasNonReadOnlyAccessor && accessor.IsReadOnly)
            {
                modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
            }

            return modifiers.ToSyntaxTokenListAndFree();
        }

        private static SyntaxTokenList GenerateModifiers(
            IPropertySymbol property, CodeGenerationDestination destination, CodeGenerationOptions options)
        {
            var tokens = ArrayBuilder<SyntaxToken>.GetInstance();

            // Most modifiers not allowed if we're an explicit impl.
            if (property.ExplicitInterfaceImplementations.Any())
            {
                if (CodeGenerationPropertyInfo.GetIsUnsafe(property))
                {
                    tokens.Add(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword));
                }
            }
            else
            {
                if (destination != CodeGenerationDestination.CompilationUnit &&
                    destination != CodeGenerationDestination.Namespace &&
                    destination != CodeGenerationDestination.InterfaceType)
                {
                    // special handling for "overrides"
                    if (property.IsOverride && property.GetOverriddenSymbolSyntax<PropertyDeclarationSyntax>(out var overriddenPropertySyntax))
                    {
                        // include non fake modifiers and exclude the virtual keyword
                        var overriddenModifiers = overriddenPropertySyntax.Modifiers.Where(m => m.Width() > 0);

                        // check which modifiers already exist
                        var hasVisibilityModifier = false;
                        var hasSealedModifier = false;
                        var hasReadonlyModifier = false;
                        var hasOverrideModifier = false;
                        var hasVirtualModifier = false;
                        foreach (var m in overriddenModifiers)
                        {
                            if (SyntaxFacts.IsAccessibilityModifier(m.Kind()))
                            {
                                hasVisibilityModifier = true;
                                continue;
                            }

                            if (m.IsKind(SyntaxKind.SealedKeyword))
                            {
                                hasSealedModifier = true;
                                continue;
                            }

                            if (m.IsKind(SyntaxKind.VirtualKeyword))
                            {
                                hasVirtualModifier = true;
                                continue;
                            }

                            if (m.IsKind(SyntaxKind.OverrideKeyword))
                            {
                                hasOverrideModifier = true;
                                continue;
                            }
                        }

                        // add sealed before any other modifiers
                        if (property.IsSealed && !hasSealedModifier)
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.SealedKeyword));

                        // add override explicitly if it's an override
                        if (property.IsOverride && !hasOverrideModifier && !hasVirtualModifier && !hasVisibilityModifier)
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));

                        // add the remaining modifiers in order of declaration
                        foreach (var m in overriddenModifiers)
                        {
                            if (hasVirtualModifier && m.IsKind(SyntaxKind.VirtualKeyword))
                            {
                                // skip the virtual modifier and add override keyword at the same location
                                if (property.IsOverride)
                                {
                                    tokens.Add(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));
                                    continue;
                                }
                            }

                            tokens.Add(m);

                            if (hasVisibilityModifier && SyntaxFacts.IsAccessibilityModifier(m.Kind()))
                            {
                                // after the visibility we add the override if no explicit virtual / override is defined
                                if (property.IsOverride && !hasVirtualModifier && !hasOverrideModifier)
                                {
                                    tokens.Add(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));
                                }

                                continue;
                            }
                        }
                    }
                    else
                    {
                        // for private/public, we don't need to add the accessibility modifier if the name follows the private/public naming convention
                        var addAccessibility = true;
                        if (property.DeclaredAccessibility.HasFlag(Accessibility.Private))
                        {
                            if (!SymbolHelpers.IsNameClassifiedAsPublic(property.Name))
                                addAccessibility = false;
                        }
                        else if (property.DeclaredAccessibility.HasFlag(Accessibility.Public))
                        {
                            if (SymbolHelpers.IsNameClassifiedAsPublic(property.Name))
                                addAccessibility = false;
                        }

                        if (addAccessibility)
                            AddAccessibilityModifiers(property.DeclaredAccessibility, tokens, options, Accessibility.Private);

                        if (property.IsAbstract)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.AbstractKeyword));
                        }

                        if (property.IsSealed)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.SealedKeyword));
                        }

                        if (property.IsStatic)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
                        }

                        // note: explicit interface impls are allowed to be 'readonly' but it never actually affects callers
                        // because of the boxing requirement in order to call the method.
                        // therefore it seems like a small oversight to leave out the keyword for an explicit impl from metadata.
                        var hasAllReadOnlyAccessors = property.GetMethod?.IsReadOnly != false && property.SetMethod?.IsReadOnly != false;
                        // Don't show the readonly modifier if the containing type is already readonly
                        if (hasAllReadOnlyAccessors && !property.ContainingType.IsReadOnly)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
                        }

                        if (property.IsOverride)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));
                        }

                        if (property.IsVirtual)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.VirtualKeyword));
                        }
                    }
                }

                if (CodeGenerationPropertyInfo.GetIsNew(property))
                {
                    tokens.Add(SyntaxFactory.Token(SyntaxKind.NewKeyword));
                }
            }

            return tokens.ToSyntaxTokenList();
        }
    }
}
