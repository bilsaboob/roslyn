// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis.Completion.Providers;
using System;
using Microsoft.CodeAnalysis.ErrorReporting;
using System.Composition;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Completion.Providers
{
    [ExportCompletionProvider(nameof(NamedParameterCompletionProvider), LanguageNames.CSharp)]
    [ExtensionOrder(After = nameof(AttributeNamedParameterCompletionProvider))]
    [Shared]
    internal partial class NamedParameterCompletionProvider : LSPCompletionProvider, IEqualityComparer<IParameterSymbol>
    {
        private const string ColonString = ":";

        // Explicitly remove ":" from the set of filter characters because (by default)
        // any character that appears in DisplayText gets treated as a filter char.
        private static readonly CompletionItemRules s_rules = CompletionItemRules.Default
            .WithFilterCharacterRule(CharacterSetModificationRule.Create(CharacterSetModificationKind.Remove, ':'));

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public NamedParameterCompletionProvider()
        {
        }

        internal override bool IsInsertionTrigger(SourceText text, int characterPosition, OptionSet options)
            => CompletionUtilities.IsTriggerCharacter(text, characterPosition, options);

        internal override ImmutableHashSet<char> TriggerCharacters { get; } = CompletionUtilities.CommonTriggerCharacters;

        public override async Task ProvideCompletionsAsync(CompletionContext context)
        {
            // cache all spread parameters available in the parameter lists
            TypeCache spreadParamTypes = null;

            try
            {
                var document = context.Document;
                var position = context.Position;
                var cancellationToken = context.CancellationToken;

                var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                if (syntaxTree.IsInNonUserCode(position, cancellationToken))
                {
                    return;
                }

                var token = syntaxTree
                    .FindTokenOnLeftOfPosition(position, cancellationToken)
                    .GetPreviousTokenIfTouchingWord(position);

                if (!token.IsKind(SyntaxKind.OpenParenToken, SyntaxKind.OpenBracketToken, SyntaxKind.CommaToken))
                {
                    return;
                }

                if (!(token.Parent is BaseArgumentListSyntax argumentList))
                {
                    return;
                }

                var semanticModel = await document.ReuseExistingSpeculativeModelAsync(argumentList, cancellationToken).ConfigureAwait(false);
                var parameterLists = GetParameterLists(semanticModel, position, argumentList.Parent, cancellationToken);
                if (parameterLists == null)
                {
                    return;
                }

                // keep the spread parameter information in cache
                spreadParamTypes = new TypeCache();

                var existingNamedParameters = GetExistingNamedParameters(argumentList, position);

                var unspecifiedParameters = parameterLists
                    .Where(pl => IsValid(pl, existingNamedParameters, spreadParamTypes))
                    .SelectMany(pl => pl)
                    .Where(p => !existingNamedParameters.Contains(p.Name))
                    .Distinct(this)
                    .ToList();

                if (unspecifiedParameters.Count == 0)
                    return;

                // Consider refining this logic to mandate completion with an argument name, if preceded by an out-of-position name
                // See https://github.com/dotnet/roslyn/issues/20657
                var languageVersion = ((CSharpParseOptions)document.Project.ParseOptions).LanguageVersion;
                if (languageVersion < LanguageVersion.CSharp7_2 && token.IsMandatoryNamedParameterPosition())
                {
                    context.IsExclusive = true;
                }

                var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

                var workspace = document.Project.Solution.Workspace;

                foreach (var parameter in unspecifiedParameters)
                {
                    // Note: the filter text does not include the ':'.  We want to ensure that if 
                    // the user types the name exactly (up to the colon) that it is selected as an
                    // exact match.
                    var escapedName = parameter.Name.ToIdentifierToken().ToString();

                    context.AddItem(SymbolCompletionItem.CreateWithSymbolId(
                        displayText: escapedName,
                        displayTextSuffix: ColonString,
                        symbols: ImmutableArray.Create(parameter),
                        rules: s_rules.WithMatchPriority(SymbolMatchPriority.PreferNamedArgument),
                        contextPosition: token.SpanStart,
                        filterText: escapedName));
                }

                var allParameterNames = parameterLists.SelectMany(pl => pl).Select(p => p.Name).ToDictionary(n => n);

                // add spread parameter symbols
                foreach (var parameter in unspecifiedParameters)
                {
                    if (!parameter.IsSpread) continue;

                    var spreadType = parameter.Type;

                    var members = SpreadParamHelpers.GetPossibleSpreadParamMembers(spreadType);
                    foreach (var member in members)
                    {
                        var escapedName = member.Name.ToIdentifierToken().ToString();
                        if (existingNamedParameters.Contains(escapedName) || allParameterNames.ContainsKey(escapedName)) continue;

                        var item = SymbolCompletionItem.CreateWithSymbolId(
                            displayText: escapedName,
                            displayTextSuffix: ColonString,
                            symbols: ImmutableArray.Create(new SpreadParamSymbol(member, parameter) as ISymbol, parameter),
                            rules: s_rules.WithMatchPriority(SymbolMatchPriority.PreferNamedArgument),
                            contextPosition: token.SpanStart,
                            filterText: escapedName
                        );

                        context.AddItem(item);
                    }
                }
            }
            catch (Exception e) when (FatalError.ReportWithoutCrashUnlessCanceled(e))
            {
                // nop
            }
            finally
            {
                spreadParamTypes?.Free();
            }
        }

        protected override Task<CompletionDescription> GetDescriptionWorkerAsync(Document document, CompletionItem item, CancellationToken cancellationToken)
            => SymbolCompletionItem.GetDescriptionAsync(item, document, cancellationToken);

        private static bool IsValid(ImmutableArray<IParameterSymbol> parameterList, ISet<string> existingNamedParameters, TypeCache typeCache)
        {
            // A parameter list is valid if it has parameters that match in name all the existing named parameters that have been provided.
            var matches = new HashSet<string>();

            // remove the params from the remaining
            foreach (var p in parameterList)
            {
                var name = p.Name;
                if (existingNamedParameters.Contains(name))
                    matches.Add(name);
            }

            // all existing parameters must have been found in the parameter list for it to be valid ... so the match count should equal
            var isMatch = matches.Count == existingNamedParameters.Count;
            if (isMatch)
            {
                matches.Clear();
                return true;
            }

            // now check for spread params
            foreach (var p in parameterList)
            {
                if (!p.IsSpread) continue;

                foreach(var name in existingNamedParameters)
                {
                    var spreadParamMember = SpreadParamHelpers.GetFirstPossibleSpreadParamMember(name, p.Type, typeCache);
                    if (spreadParamMember != null)
                    {
                        matches.Add(name);

                        if (matches.Count == existingNamedParameters.Count)
                        {
                            matches.Clear();
                            return true;
                        }
                    }
                }
            }

            isMatch = matches.Count == existingNamedParameters.Count;
            matches.Clear();

            return isMatch;
        }

        private static ISet<string> GetExistingNamedParameters(BaseArgumentListSyntax argumentList, int position)
        {
            var existingArguments = argumentList.Arguments.Where(a => a.Span.End <= position && a.NameColon != null)
                                                          .Select(a => a.NameColon.Name.Identifier.ValueText);

            return existingArguments.ToSet();
        }

        private static IEnumerable<ImmutableArray<IParameterSymbol>> GetParameterLists(
            SemanticModel semanticModel,
            int position,
            SyntaxNode invocableNode,
            CancellationToken cancellationToken)
        {
            switch (invocableNode)
            {
                case InvocationExpressionSyntax invocationExpression: return GetInvocationExpressionParameterLists(semanticModel, position, invocationExpression, cancellationToken);
                case ConstructorInitializerSyntax constructorInitializer: return GetConstructorInitializerParameterLists(semanticModel, position, constructorInitializer, cancellationToken);
                case ElementAccessExpressionSyntax elementAccessExpression: return GetElementAccessExpressionParameterLists(semanticModel, position, elementAccessExpression, cancellationToken);
                case BaseObjectCreationExpressionSyntax objectCreationExpression: return GetObjectCreationExpressionParameterLists(semanticModel, position, objectCreationExpression, cancellationToken);
                default: return null;
            }
        }

        private static IEnumerable<ImmutableArray<IParameterSymbol>> GetObjectCreationExpressionParameterLists(
            SemanticModel semanticModel,
            int position,
            BaseObjectCreationExpressionSyntax objectCreationExpression,
            CancellationToken cancellationToken)
        {
            var within = semanticModel.GetEnclosingNamedType(position, cancellationToken);
            if (semanticModel.GetTypeInfo(objectCreationExpression, cancellationToken).Type is INamedTypeSymbol type && within != null && type.TypeKind != TypeKind.Delegate)
            {
                return type.InstanceConstructors.Where(c => c.IsAccessibleWithin(within))
                                                .Select(c => c.Parameters);
            }

            return null;
        }

        private static IEnumerable<ImmutableArray<IParameterSymbol>> GetElementAccessExpressionParameterLists(
            SemanticModel semanticModel,
            int position,
            ElementAccessExpressionSyntax elementAccessExpression,
            CancellationToken cancellationToken)
        {
            var expressionSymbol = semanticModel.GetSymbolInfo(elementAccessExpression.Expression, cancellationToken).GetAnySymbol();
            var expressionType = semanticModel.GetTypeInfo(elementAccessExpression.Expression, cancellationToken).Type;

            if (expressionSymbol != null && expressionType != null)
            {
                var indexers = semanticModel.LookupSymbols(position, expressionType, WellKnownMemberNames.Indexer).OfType<IPropertySymbol>();
                var within = semanticModel.GetEnclosingNamedTypeOrAssembly(position, cancellationToken);
                if (within != null)
                {
                    return indexers.Where(i => i.IsAccessibleWithin(within, throughType: expressionType))
                                   .Select(i => i.Parameters);
                }
            }

            return null;
        }

        private static IEnumerable<ImmutableArray<IParameterSymbol>> GetConstructorInitializerParameterLists(
            SemanticModel semanticModel,
            int position,
            ConstructorInitializerSyntax constructorInitializer,
            CancellationToken cancellationToken)
        {
            var within = semanticModel.GetEnclosingNamedType(position, cancellationToken);
            if (within != null &&
                (within.TypeKind == TypeKind.Struct || within.TypeKind == TypeKind.Class))
            {
                var type = constructorInitializer.Kind() == SyntaxKind.BaseConstructorInitializer
                    ? within.BaseType
                    : within;

                if (type != null)
                {
                    return type.InstanceConstructors.Where(c => c.IsAccessibleWithin(within))
                                                    .Select(c => c.Parameters);
                }
            }

            return null;
        }

        private static IEnumerable<ImmutableArray<IParameterSymbol>> GetInvocationExpressionParameterLists(
            SemanticModel semanticModel,
            int position,
            InvocationExpressionSyntax invocationExpression,
            CancellationToken cancellationToken)
        {
            var within = semanticModel.GetEnclosingNamedTypeOrAssembly(position, cancellationToken);
            if (within != null)
            {
                var methodGroup = semanticModel.GetMemberGroup(invocationExpression.Expression, cancellationToken).OfType<IMethodSymbol>();
                var expressionType = semanticModel.GetTypeInfo(invocationExpression.Expression, cancellationToken).Type as INamedTypeSymbol;

                if (methodGroup.Any())
                {
                    return methodGroup.Where(m => m.IsAccessibleWithin(within))
                                      .Select(m => m.Parameters);
                }
                else if (expressionType.IsDelegateType())
                {
                    var delegateType = expressionType;
                    return SpecializedCollections.SingletonEnumerable(delegateType.DelegateInvokeMethod.Parameters);
                }
            }

            return null;
        }

        bool IEqualityComparer<IParameterSymbol>.Equals(IParameterSymbol x, IParameterSymbol y)
            => x.Name.Equals(y.Name);

        int IEqualityComparer<IParameterSymbol>.GetHashCode(IParameterSymbol obj)
            => obj.Name.GetHashCode();

        protected override Task<TextChange?> GetTextChangeAsync(CompletionItem selectedItem, char? ch, CancellationToken cancellationToken)
        {
            return Task.FromResult<TextChange?>(new TextChange(
                selectedItem.Span,
                // Insert extra colon if committing with '(' only: "method(parameter:(" is preferred to "method(parameter(".
                // In all other cases, do not add extra colon. Note that colon is already added if committing with ':'.
                ch == '(' ? selectedItem.GetEntireDisplayText() : selectedItem.DisplayText));
        }
    }
}
