// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using static Microsoft.CodeAnalysis.CodeGeneration.CodeGenerationHelpers;
using static Microsoft.CodeAnalysis.CSharp.CodeGeneration.CSharpCodeGenerationHelpers;

namespace Microsoft.CodeAnalysis.CSharp.CodeGeneration
{
    internal static class MethodGenerator
    {
        internal static NamespaceDeclarationSyntax AddMethodTo(
            NamespaceDeclarationSyntax destination,
            IMethodSymbol method,
            CodeGenerationOptions options,
            IList<bool> availableIndices)
        {
            var declaration = GenerateMethodDeclaration(
                method, CodeGenerationDestination.Namespace, options,
                destination?.SyntaxTree.Options ?? options.ParseOptions);
            var members = Insert(destination.Members, declaration, options, availableIndices, after: LastMethod);
            return destination.WithMembers(members.ToSyntaxList());
        }

        internal static CompilationUnitSyntax AddMethodTo(
            CompilationUnitSyntax destination,
            IMethodSymbol method,
            CodeGenerationOptions options,
            IList<bool> availableIndices)
        {
            var declaration = GenerateMethodDeclaration(
                method, CodeGenerationDestination.CompilationUnit, options,
                destination?.SyntaxTree.Options ?? options.ParseOptions);
            var members = Insert(destination.Members, declaration, options, availableIndices, after: LastMethod);
            return destination.WithMembers(members.ToSyntaxList());
        }

        internal static TypeDeclarationSyntax AddMethodTo(
            TypeDeclarationSyntax destination,
            IMethodSymbol method,
            CodeGenerationOptions options,
            IList<bool> availableIndices)
        {
            var methodDeclaration = GenerateMethodDeclaration(
                method, GetDestination(destination), options,
                destination?.SyntaxTree.Options ?? options.ParseOptions);

            // Create a clone of the original type with the new method inserted. 
            var members = Insert(destination.Members, methodDeclaration, options, availableIndices, after: LastMethod);

            return AddMembersTo(destination, members);
        }

        public static MethodDeclarationSyntax GenerateMethodDeclaration(
            IMethodSymbol method, CodeGenerationDestination destination,
            CodeGenerationOptions options,
            ParseOptions parseOptions)
        {
            options ??= CodeGenerationOptions.Default;

            var reusableSyntax = GetReuseableSyntaxNodeForSymbol<MethodDeclarationSyntax>(method, options);
            if (reusableSyntax != null)
            {
                return reusableSyntax;
            }

            var declaration = GenerateMethodDeclarationWorker(
                method, destination, options, parseOptions);

            return AddAnnotationsTo(method,
                ConditionallyAddDocumentationCommentTo(declaration, method, options));
        }

        public static LocalFunctionStatementSyntax GenerateLocalFunctionDeclaration(
            IMethodSymbol method, CodeGenerationDestination destination,
            CodeGenerationOptions options,
            ParseOptions parseOptions)
        {
            options ??= CodeGenerationOptions.Default;

            var reusableSyntax = GetReuseableSyntaxNodeForSymbol<LocalFunctionStatementSyntax>(method, options);
            if (reusableSyntax != null)
            {
                return reusableSyntax;
            }

            var declaration = GenerateLocalFunctionDeclarationWorker(
                method, destination, options, parseOptions);

            return AddAnnotationsTo(method,
                ConditionallyAddDocumentationCommentTo(declaration, method, options));
        }

        private static MethodDeclarationSyntax GenerateMethodDeclarationWorker(
            IMethodSymbol method, CodeGenerationDestination destination,
            CodeGenerationOptions options, ParseOptions parseOptions)
        {
            // Don't rely on destination to decide if method body should be generated.
            // Users of this service need to express their intention explicitly, either by  
            // setting `CodeGenerationOptions.GenerateMethodBodies` to true, or making 
            // `method` abstract. This would provide more flexibility.
            var hasNoBody = !options.GenerateMethodBodies || method.IsAbstract;

            var explicitInterfaceSpecifier = GenerateExplicitInterfaceSpecifier(method.ExplicitInterfaceImplementations);

            var returnTypeSyntax = GenerateReturnType(method, out var isAsync, out var isVoidType).WithTrailingTrivia(SyntaxFactory.Whitespace(" "));

            var methodDeclaration = SyntaxFactory.MethodDeclaration(
                attributeLists: GenerateAttributes(method, options, explicitInterfaceSpecifier != null),
                modifiers: GenerateModifiers(method, destination, options),
                returnType: returnTypeSyntax,
                explicitInterfaceSpecifier: explicitInterfaceSpecifier,
                identifier: method.Name.ToIdentifierToken(),
                typeParameterList: GenerateTypeParameterList(method, options),
                parameterList: ParameterGenerator.GenerateParameterList(method.Parameters, explicitInterfaceSpecifier != null, options),
                constraintClauses: GenerateConstraintClauses(method),
                body: hasNoBody ? null : GenerateBody(method, isAsync, isVoidType),
                expressionBody: null,
                semicolonToken: default);

            methodDeclaration = UseExpressionBodyIfDesired(options, methodDeclaration, parseOptions);
            return AddFormatterAndCodeGeneratorAnnotationsTo(methodDeclaration);
        }

        private static BlockSyntax GenerateBody(IMethodSymbol method, bool isAsync, bool isVoidType)
        {
            var methodStatements = CodeGenerationMethodInfo.GetStatements(method);

            var statements = methodStatements.OfType<StatementSyntax>().ToList();

            if (statements?.Count > 0)
            {
                if (isAsync)
                {
                    // convert the last statement to an "await"
                    var lastStatement = statements[statements.Count - 1];
                    if (lastStatement is ReturnStatementSyntax returnStat)
                    {
                        if (!isVoidType)
                        {
                            statements[statements.Count - 1] = returnStat.WithExpression(SyntaxFactory.AwaitExpression(returnStat.Expression));
                        }
                        else
                        {
                            statements[statements.Count - 1] = SyntaxFactory.ExpressionStatement(SyntaxFactory.AwaitExpression(returnStat.Expression));
                        }
                    }
                    else if (lastStatement is ExpressionStatementSyntax exprStat)
                    {
                        statements[statements.Count - 1] = exprStat.WithExpression(SyntaxFactory.AwaitExpression(exprStat.Expression));
                    }
                }

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
            }

            return SyntaxFactory.Block(statements.ToSyntaxList());
        }

        private static TypeSyntax GenerateReturnType(IMethodSymbol method, out bool isAsync, out bool isVoidType)
        {
            isAsync = method.IsAsync;
            isVoidType = method.ReturnsVoid;

            if (method.IsOverride && method.GetOverriddenSymbolSyntax<MethodDeclarationSyntax>(out var overriddenMethodSyntax))
            {
                isAsync = overriddenMethodSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword));

                // make sure to trim away the explicit Tasks for async methods
                if (isAsync)
                {
                    var returnTypeName = method.ReturnType.ToString();
                    if (returnTypeName.StartsWith("Task<"))
                    {
                        if (!overriddenMethodSyntax.HasExplicitReturnType())
                            return SyntaxFactory.FakeTypeIdentifier();

                        var name = returnTypeName.Replace("Task<", "");
                        if (name.EndsWith("<")) name = name.Substring(0, name.Length - 1);
                        return SyntaxFactory.ParseTypeName(name);
                    }
                    else if (returnTypeName.StartsWith("System.Threading.Tasks.Task<"))
                    {
                        if (!overriddenMethodSyntax.HasExplicitReturnType())
                            return SyntaxFactory.FakeTypeIdentifier();

                        var name = returnTypeName.Replace("System.Threading.Tasks.Task<", "");
                        if (name.EndsWith("<")) name = name.Substring(0, name.Length - 1);
                        return SyntaxFactory.ParseTypeName(name);
                    }
                    else if (returnTypeName == "Task" || returnTypeName == "System.Threading.Tasks.Task")
                    {
                        isVoidType = true;
                        return SyntaxFactory.FakeTypeIdentifier();
                    }
                }

                if (!overriddenMethodSyntax.HasExplicitReturnType())
                {
                    return SyntaxFactory.FakeTypeIdentifier();
                }
            }

            return method.GenerateReturnTypeSyntax();
        }

        private static LocalFunctionStatementSyntax GenerateLocalFunctionDeclarationWorker(
            IMethodSymbol method, CodeGenerationDestination destination,
            CodeGenerationOptions options, ParseOptions parseOptions)
        {
            var localFunctionDeclaration = SyntaxFactory.LocalFunctionStatement(
                modifiers: GenerateModifiers(method, destination, options),
                returnType: method.GenerateReturnTypeSyntax(),
                identifier: method.Name.ToIdentifierToken(),
                typeParameterList: GenerateTypeParameterList(method, options),
                parameterList: ParameterGenerator.GenerateParameterList(method.Parameters, isExplicit: false, options),
                constraintClauses: GenerateConstraintClauses(method),
                body: StatementGenerator.GenerateBlock(method),
                expressionBody: null,
                semicolonToken: default);

            localFunctionDeclaration = UseExpressionBodyIfDesired(options, localFunctionDeclaration, parseOptions);
            return AddFormatterAndCodeGeneratorAnnotationsTo(localFunctionDeclaration);
        }

        private static MethodDeclarationSyntax UseExpressionBodyIfDesired(
            CodeGenerationOptions options, MethodDeclarationSyntax methodDeclaration, ParseOptions parseOptions)
        {
            if (methodDeclaration.ExpressionBody == null)
            {
                var expressionBodyPreference = options.Options.GetOption(CSharpCodeStyleOptions.PreferExpressionBodiedMethods).Value;
                if (methodDeclaration.Body.TryConvertToArrowExpressionBody(
                        methodDeclaration.Kind(), parseOptions, expressionBodyPreference,
                        out var expressionBody, out var semicolonToken))
                {
                    return methodDeclaration.WithBody(null)
                                            .WithExpressionBody(expressionBody)
                                            .WithSemicolonToken(semicolonToken);
                }
            }

            return methodDeclaration;
        }

        private static LocalFunctionStatementSyntax UseExpressionBodyIfDesired(
            CodeGenerationOptions options, LocalFunctionStatementSyntax localFunctionDeclaration, ParseOptions parseOptions)
        {
            if (localFunctionDeclaration.ExpressionBody == null)
            {
                var expressionBodyPreference = options.Options.GetOption(CSharpCodeStyleOptions.PreferExpressionBodiedLocalFunctions).Value;
                if (localFunctionDeclaration.Body.TryConvertToArrowExpressionBody(
                        localFunctionDeclaration.Kind(), parseOptions, expressionBodyPreference,
                        out var expressionBody, out var semicolonToken))
                {
                    return localFunctionDeclaration.WithBody(null)
                                                 .WithExpressionBody(expressionBody)
                                                 .WithSemicolonToken(semicolonToken);
                }
            }

            return localFunctionDeclaration;
        }

        private static SyntaxList<AttributeListSyntax> GenerateAttributes(
            IMethodSymbol method, CodeGenerationOptions options, bool isExplicit)
        {
            var attributes = new List<AttributeListSyntax>();

            if (!isExplicit)
            {
                attributes.AddRange(AttributeGenerator.GenerateAttributeLists(method.GetAttributes(), options));
                attributes.AddRange(AttributeGenerator.GenerateAttributeLists(method.GetReturnTypeAttributes(), options, SyntaxFactory.Token(SyntaxKind.ReturnKeyword)));
            }

            return attributes.ToSyntaxList();
        }

        private static SyntaxList<TypeParameterConstraintClauseSyntax> GenerateConstraintClauses(
            IMethodSymbol method)
        {
            return !method.ExplicitInterfaceImplementations.Any() && !method.IsOverride
                ? method.TypeParameters.GenerateConstraintClauses()
                : default;
        }

        private static TypeParameterListSyntax GenerateTypeParameterList(
            IMethodSymbol method, CodeGenerationOptions options)
        {
            return TypeParameterGenerator.GenerateTypeParameterList(method.TypeParameters, options);
        }

        private static SyntaxTokenList GenerateModifiers(
            IMethodSymbol method, CodeGenerationDestination destination, CodeGenerationOptions options)
        {
            var tokens = ArrayBuilder<SyntaxToken>.GetInstance();

            // Only "unsafe" modifier allowed if we're an explicit impl.
            if (method.ExplicitInterfaceImplementations.Any())
            {
                if (CodeGenerationMethodInfo.GetIsUnsafe(method))
                {
                    tokens.Add(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword));
                }
            }
            else
            {
                // If we're generating into an interface, then we don't use any modifiers.
                if (destination != CodeGenerationDestination.CompilationUnit &&
                    destination != CodeGenerationDestination.Namespace &&
                    destination != CodeGenerationDestination.InterfaceType)
                {
                    // special handling for "overrides"
                    if (method.IsOverride && method.GetOverriddenSymbolSyntax<MethodDeclarationSyntax>(out var overriddenMethodSyntax))
                    {
                        // include non fake modifiers and exclude the virtual keyword
                        var overriddenModifiers = overriddenMethodSyntax.Modifiers.Where(m => m.Width() > 0);

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

                            if (m.IsKind(SyntaxKind.ReadOnlyKeyword))
                            {
                                hasReadonlyModifier = true;
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
                        if (method.IsSealed && !hasSealedModifier)
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.SealedKeyword));

                        // add readonly before any other modifiers
                        if (method.IsReadOnly && (method.ContainingSymbol as INamedTypeSymbol)?.IsReadOnly != true && !hasReadonlyModifier)
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

                        // add override explicitly if it's an override
                        if (method.IsOverride && !hasOverrideModifier && !hasVirtualModifier && !hasVisibilityModifier)
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));

                        // add the remaining modifiers in order of declaration
                        foreach (var m in overriddenModifiers)
                        {
                            if (hasVirtualModifier && m.IsKind(SyntaxKind.VirtualKeyword))
                            {
                                // skip the virtual modifier and add override keyword at the same location
                                if (method.IsOverride)
                                {
                                    tokens.Add(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));
                                    continue;
                                }
                            }

                            tokens.Add(m);

                            if (hasVisibilityModifier && SyntaxFacts.IsAccessibilityModifier(m.Kind()))
                            {
                                // after the visibility we add the override if no explicit virtual / override is defined
                                if (method.IsOverride && !hasVirtualModifier && !hasOverrideModifier)
                                {
                                    tokens.Add(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));
                                }

                                continue;
                            }
                        }
                    }
                    else
                    {
                        AddAccessibilityModifiers(method.DeclaredAccessibility, tokens, options, Accessibility.Private);

                        if (method.IsAbstract)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.AbstractKeyword));
                        }

                        if (method.IsSealed)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.SealedKeyword));
                        }

                        if (method.IsStatic)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
                        }

                        // Don't show the readonly modifier if the containing type is already readonly
                        // ContainingSymbol is used to guard against methods which are not members of their ContainingType (e.g. lambdas and local functions)
                        if (method.IsReadOnly && (method.ContainingSymbol as INamedTypeSymbol)?.IsReadOnly != true)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
                        }

                        if (method.IsOverride)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.OverrideKeyword));
                        }

                        if (method.IsVirtual)
                        {
                            tokens.Add(SyntaxFactory.Token(SyntaxKind.VirtualKeyword));
                        }
                    }

                    if (CodeGenerationMethodInfo.GetIsPartial(method) && !method.IsAsync)
                    {
                        tokens.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
                    }
                }

                if (CodeGenerationMethodInfo.GetIsUnsafe(method))
                {
                    tokens.Add(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword));
                }

                if (CodeGenerationMethodInfo.GetIsNew(method))
                {
                    tokens.Add(SyntaxFactory.Token(SyntaxKind.NewKeyword));
                }
            }

            if (destination != CodeGenerationDestination.InterfaceType)
            {
                if (CodeGenerationMethodInfo.GetIsAsyncMethod(method))
                {
                    tokens.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
                }
            }

            if (CodeGenerationMethodInfo.GetIsPartial(method) && method.IsAsync)
            {
                tokens.Add(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
            }

            return tokens.ToSyntaxTokenListAndFree();
        }
    }
}
