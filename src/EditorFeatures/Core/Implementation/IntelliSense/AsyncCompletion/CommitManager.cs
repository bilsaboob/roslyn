﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Text.Shared.Extensions;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Roslyn.Utilities;
using AsyncCompletionData = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using RoslynCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using VSCompletionItem = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem;

namespace Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense.AsyncCompletion
{
    internal sealed class CommitManager : ForegroundThreadAffinitizedObject, IAsyncCompletionCommitManager
    {
        private static readonly AsyncCompletionData.CommitResult CommitResultUnhandled =
            new AsyncCompletionData.CommitResult(isHandled: false, AsyncCompletionData.CommitBehavior.None);

        private readonly RecentItemsManager _recentItemsManager;
        private readonly ITextView _textView;

        public IEnumerable<char> PotentialCommitCharacters
        {
            get
            {
                if (_textView.Properties.TryGetProperty(CompletionSource.PotentialCommitCharacters, out ImmutableArray<char> potentialCommitCharacters))
                {
                    return potentialCommitCharacters;
                }
                else
                {
                    // If we were not initialized with a CompletionService or are called for a wrong textView, we should not make a commit.
                    return ImmutableArray<char>.Empty;
                }
            }
        }

        internal CommitManager(ITextView textView, RecentItemsManager recentItemsManager, IThreadingContext threadingContext) : base(threadingContext)
        {
            _recentItemsManager = recentItemsManager;
            _textView = textView;
        }

        /// <summary>
        /// The method performs a preliminarily filtering of commit availability.
        /// In case of a doubt, it should respond with true.
        /// We will be able to cancel later in 
        /// <see cref="TryCommit(IAsyncCompletionSession, ITextBuffer, VSCompletionItem, char, CancellationToken)"/> 
        /// based on <see cref="VSCompletionItem"/> item, e.g. based on <see cref="CompletionItemRules"/>.
        /// </summary>
        public bool ShouldCommitCompletion(
            IAsyncCompletionSession session,
            SnapshotPoint location,
            char typedChar,
            CancellationToken cancellationToken)
        {
            if (!PotentialCommitCharacters.Contains(typedChar))
            {
                return false;
            }

            return !(session.Properties.TryGetProperty(CompletionSource.ExcludedCommitCharacters, out ImmutableArray<char> excludedCommitCharacter)
                && excludedCommitCharacter.Contains(typedChar));
        }

        public AsyncCompletionData.CommitResult TryCommit(
            IAsyncCompletionSession session,
            ITextBuffer subjectBuffer,
            VSCompletionItem item,
            char typeChar,
            CancellationToken cancellationToken)
        {
            // We can make changes to buffers. We would like to be sure nobody can change them at the same time.
            AssertIsForeground();

            var document = subjectBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
            if (document == null)
            {
                return CommitResultUnhandled;
            }

            var completionService = document.GetLanguageService<CompletionService>();
            if (completionService == null)
            {
                return CommitResultUnhandled;
            }

            if (!item.Properties.TryGetProperty(CompletionSource.RoslynItem, out RoslynCompletionItem roslynItem))
            {
                // Roslyn should not be called if the item committing was not provided by Roslyn.
                return CommitResultUnhandled;
            }

            var filterText = session.ApplicableToSpan.GetText(session.ApplicableToSpan.TextBuffer.CurrentSnapshot) + typeChar;
            if (Helpers.IsFilterCharacter(roslynItem, typeChar, filterText))
            {
                // Returning Cancel means we keep the current session and consider the character for further filtering.
                return new AsyncCompletionData.CommitResult(isHandled: true, AsyncCompletionData.CommitBehavior.CancelCommit);
            }

            var serviceRules = completionService.GetRules();

            // We can be called before for ShouldCommitCompletion. However, that call does not provide rules applied for the completion item.
            // Now we check for the commit charcter in the context of Rules that could change the list of commit characters.

            if (!Helpers.IsStandardCommitCharacter(typeChar) && !IsCommitCharacter(serviceRules, roslynItem, typeChar, filterText))
            {
                // Returning None means we complete the current session with a void commit. 
                // The Editor then will try to trigger a new completion session for the character.
                return new AsyncCompletionData.CommitResult(isHandled: true, AsyncCompletionData.CommitBehavior.None);
            }

            if (!Helpers.TryGetInitialTriggerLocation(item, out var triggerLocation))
            {
                // Need the trigger snapshot to calculate the span when the commit changes to be applied.
                // They should always be available from VS. Just to be defensive, if it's not found here, Roslyn should not make a commit.
                return CommitResultUnhandled;
            }

            if (!session.Properties.TryGetProperty(CompletionSource.CompletionListSpan, out TextSpan completionListSpan))
            {
                return CommitResultUnhandled;
            }

            var triggerDocument = triggerLocation.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
            if (triggerDocument == null)
            {
                return CommitResultUnhandled;
            }

            // Telemetry
            if (session.TextView.Properties.TryGetProperty(CompletionSource.TypeImportCompletionEnabled, out bool isTyperImportCompletionEnabled) && isTyperImportCompletionEnabled)
            {
                AsyncCompletionLogger.LogCommitWithTypeImportCompletionEnabled();
            }

            if (session.TextView.Properties.TryGetProperty(CompletionSource.TargetTypeFilterExperimentEnabled, out bool isExperimentEnabled) && isExperimentEnabled)
            {
                // Capture the % of committed completion items that would have appeared in the "Target type matches" filter
                // (regardless of whether that filter button was active at the time of commit).
                AsyncCompletionLogger.LogCommitWithTargetTypeCompletionExperimentEnabled();
                if (item.Filters.Any(f => f.DisplayText == FeaturesResources.Target_type_matches))
                {
                    AsyncCompletionLogger.LogCommitItemWithTargetTypeFilter();
                }
            }

            // Commit with completion service assumes that null is provided is case of invoke. VS provides '\0' in the case.
            var commitChar = typeChar == '\0' ? null : (char?)typeChar;
            return Commit(
                triggerDocument, completionService, session.TextView, subjectBuffer,
                roslynItem, completionListSpan, commitChar, triggerLocation.Snapshot, serviceRules,
                filterText, cancellationToken);
        }

        private AsyncCompletionData.CommitResult Commit(
            Document document,
            CompletionService completionService,
            ITextView view,
            ITextBuffer subjectBuffer,
            RoslynCompletionItem roslynItem,
            TextSpan completionListSpan,
            char? commitCharacter,
            ITextSnapshot triggerSnapshot,
            CompletionRules rules,
            string filterText,
            CancellationToken cancellationToken)
        {
            AssertIsForeground();

            bool includesCommitCharacter;
            if (!subjectBuffer.CheckEditAccess())
            {
                // We are on the wrong thread.
                FatalError.ReportWithoutCrash(new InvalidOperationException("Subject buffer did not provide Edit Access"));
                return new AsyncCompletionData.CommitResult(isHandled: true, AsyncCompletionData.CommitBehavior.None);
            }

            if (subjectBuffer.EditInProgress)
            {
                FatalError.ReportWithoutCrash(new InvalidOperationException("Subject buffer is editing by someone else."));
                return new AsyncCompletionData.CommitResult(isHandled: true, AsyncCompletionData.CommitBehavior.None);
            }

            CompletionChange change;

            // We met an issue when external code threw an OperationCanceledException and the cancellationToken is not cancelled.
            // Catching this scenario for further investigations.
            // See https://github.com/dotnet/roslyn/issues/38455.
            try
            {
                change = completionService.GetChangeAsync(document, roslynItem, completionListSpan, commitCharacter, cancellationToken).WaitAndGetResult(cancellationToken);
            }
            catch (OperationCanceledException e) when (e.CancellationToken != cancellationToken && FatalError.ReportWithoutCrash(e))
            {
                return CommitResultUnhandled;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (GetCompletionProvider(completionService, roslynItem) is ICustomCommitCompletionProvider provider)
            {
                provider.Commit(roslynItem, view, subjectBuffer, triggerSnapshot, commitCharacter);
                return new AsyncCompletionData.CommitResult(isHandled: true, AsyncCompletionData.CommitBehavior.None);
            }

            var textChange = change.TextChange;
            var triggerSnapshotSpan = new SnapshotSpan(triggerSnapshot, textChange.Span.ToSpan());
            var mappedSpan = triggerSnapshotSpan.TranslateTo(subjectBuffer.CurrentSnapshot, SpanTrackingMode.EdgeInclusive);

            using (var edit = subjectBuffer.CreateEdit(EditOptions.DefaultMinimalChange, reiteratedVersionNumber: null, editTag: null))
            {
                edit.Replace(mappedSpan.Span, change.TextChange.NewText);

                // edit.Apply() may trigger changes made by extensions.
                // updatedCurrentSnapshot will contain changes made by Roslyn but not by other extensions.
                var updatedCurrentSnapshot = edit.Apply();

                if (change.NewPosition.HasValue)
                {
                    // Roslyn knows how to positionate the caret in the snapshot we just created.
                    // If there were more edits made by extensions, TryMoveCaretToAndEnsureVisible maps the snapshot point to the most recent one.
                    view.TryMoveCaretToAndEnsureVisible(new SnapshotPoint(updatedCurrentSnapshot, change.NewPosition.Value));
                }
                else
                {
                    // Or, If we're doing a minimal change, then the edit that we make to the 
                    // buffer may not make the total text change that places the caret where we 
                    // would expect it to go based on the requested change. In this case, 
                    // determine where the item should go and set the care manually.

                    // Note: we only want to move the caret if the caret would have been moved 
                    // by the edit.  i.e. if the caret was actually in the mapped span that 
                    // we're replacing.
                    var caretPositionInBuffer = view.GetCaretPoint(subjectBuffer);
                    if (caretPositionInBuffer.HasValue && mappedSpan.IntersectsWith(caretPositionInBuffer.Value))
                    {
                        view.TryMoveCaretToAndEnsureVisible(new SnapshotPoint(subjectBuffer.CurrentSnapshot, mappedSpan.Start.Position + textChange.NewText.Length));
                    }
                    else
                    {
                        view.Caret.EnsureVisible();
                    }
                }

                includesCommitCharacter = change.IncludesCommitCharacter;

                if (roslynItem.Rules.FormatOnCommit)
                {
                    // The edit updates the snapshot however other extensions may make changes there.
                    // Therefore, it is required to use subjectBuffer.CurrentSnapshot for further calculations rather than the updated current snapsot defined above.
                    document = subjectBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
                    var spanToFormat = triggerSnapshotSpan.TranslateTo(subjectBuffer.CurrentSnapshot, SpanTrackingMode.EdgeInclusive);
                    var formattingService = document?.GetLanguageService<IEditorFormattingService>();

                    if (formattingService != null)
                    {
                        var changes = formattingService.GetFormattingChangesAsync(
                            document, spanToFormat.Span.ToTextSpan(), CancellationToken.None).WaitAndGetResult(CancellationToken.None);
                        document.Project.Solution.Workspace.ApplyTextChanges(document.Id, changes, CancellationToken.None);
                    }
                }
            }

            _recentItemsManager.MakeMostRecentItem(roslynItem.FilterText);

            if (includesCommitCharacter)
            {
                return new AsyncCompletionData.CommitResult(isHandled: true, AsyncCompletionData.CommitBehavior.SuppressFurtherTypeCharCommandHandlers);
            }

            if (commitCharacter == '\n' && SendEnterThroughToEditor(rules, roslynItem, filterText))
            {
                return new AsyncCompletionData.CommitResult(isHandled: true, AsyncCompletionData.CommitBehavior.RaiseFurtherReturnKeyAndTabKeyCommandHandlers);
            }

            return new AsyncCompletionData.CommitResult(isHandled: true, AsyncCompletionData.CommitBehavior.None);
        }

        internal static bool IsCommitCharacter(CompletionRules completionRules, CompletionItem item, char ch, string textTypedSoFar)
        {
            // First see if the item has any specifc commit rules it wants followed.
            foreach (var rule in item.Rules.CommitCharacterRules)
            {
                switch (rule.Kind)
                {
                    case CharacterSetModificationKind.Add:
                        if (rule.Characters.Contains(ch))
                        {
                            return true;
                        }
                        continue;

                    case CharacterSetModificationKind.Remove:
                        if (rule.Characters.Contains(ch))
                        {
                            return false;
                        }
                        continue;

                    case CharacterSetModificationKind.Replace:
                        return rule.Characters.Contains(ch);
                }
            }

            // general rule: if the filtering text exactly matches the start of the item then it must be a filter character
            if (Helpers.TextTypedSoFarMatchesItem(item, textTypedSoFar))
            {
                return false;
            }

            var isCommitChar = IsCommitCharacter_(item, ch, textTypedSoFar);
            if (isCommitChar.HasValue) return isCommitChar.Value;

            // Fall back to the default rules for this language's completion service.
            return completionRules.DefaultCommitCharacters.IndexOf(ch) >= 0;
        }

        private static bool? IsCommitCharacter_(RoslynCompletionItem item, char ch, string textTypedSoFar)
        {
            // Handle space as a special commit character which only commits for selected kind of completion items and only in specific "semantic contexts"
            var isHandledChar = (
                ch == ' ' || ch == '\n' || ch == '\r' ||
                ch == '(' || ch == ')' || ch == '(' || ch == ')' ||
                ch == ',' || ch == ':' ||
                ch == '=' ||
                ch == '>' || ch == '<' || ch == '|' || ch == '&' ||
                ch == '+' || ch == '-' || ch == '/' ||
                ch == '.' || ch == '?'
            );

            bool isEnterCommitChar() => ch == '\n' || ch == '\r';
            bool isSpaceCommitChar() => ch == ' ';

            if (!isHandledChar) return null;

            // * Only allow completion of "symbols" and "type keywords"
            // * IGNORE commit of snippets and "other stuff" using "space" ... force explicitly using the "enter" key

            // don't allow completion on any characters other than the "space" and the "newline" characters
            if (!isSpaceCommitChar() && !isEnterCommitChar())
                return false;

            // must have some actual typed text that is not empty
            var typedText = textTypedSoFar?.Length > 1 ? textTypedSoFar.Substring(0, textTypedSoFar.Length - 1) : "";
            if (string.IsNullOrEmpty(typedText)) return false;

            // item text must not be empty
            var itemText = (item.DisplayText ?? "")?.Trim();
            if (string.IsNullOrEmpty(itemText)) return false;

            // evaluate based on the item type
            var requiredLength = 1;
            var itemType = GetCompletionItemType(item, itemText);
            switch (itemType)
            {
                case CompletionItemType.PredefinedType:
                    {
                        requiredLength = 2;

                        // for space we require to match 3 chars
                        if (isSpaceCommitChar())
                            requiredLength = 3;

                        // must match exactly on the starting characters
                        if (!MatchesStart(typedText, itemText, requiredLength)) return false;

                        // ok, make a commit
                        return true;
                    }
                case CompletionItemType.Keyword:
                    {
                        requiredLength = 3;

                        // allow the fuzzy commit on space only
                        if (!isSpaceCommitChar()) return false;

                        // must match exactly on the starting characters
                        if (!MatchesStart(typedText, itemText, requiredLength)) return false;

                        // ok, allow fuzzy completion
                        return true;
                    }
                case CompletionItemType.Symbol:
                    {
                        requiredLength = 3;

                        // allow the fuzzy commit on space only
                        if (!isSpaceCommitChar()) return false;

                        // must match exactly on the starting characters
                        if (!MatchesStart(typedText, itemText, requiredLength)) return false;

                        // ok, allow fuzzy completion
                        return true;
                    }
            }

            // anything else is not allowed as commit
            return false;
        }

        private static bool MatchesStart(string typedText, string itemText, int requiredLength)
        {
            if (typedText.Length <= requiredLength) return false;
            var startText = typedText.Substring(0, requiredLength);
            return itemText.StartsWith(startText, StringComparison.InvariantCulture);
        }

        enum CompletionItemType
        {
            Unknown,
            Symbol,
            Keyword,
            PredefinedType
        }

        private static CompletionItemType GetCompletionItemType(RoslynCompletionItem item, string itemText)
        {
            if (item.ProviderName.Contains("SymbolCompletionProvider"))
            {
                if (IsCommonKeyword(itemText))
                    return CompletionItemType.Keyword;

                return CompletionItemType.Symbol;
            }

            if (item.ProviderName.Contains("KeywordCompletionProvider"))
            {
                if (IsPredefinedType(itemText))
                    return CompletionItemType.PredefinedType;

                return CompletionItemType.Keyword;
            }

            return CompletionItemType.Unknown;
        }

        private static bool IsCommonKeyword(string text)
        {
            switch (text?.Trim() ?? "")
            {
                case "if":
                case "else":
                case "new":
                case "foreach":
                case "for":
                case "typeof":
                case "while":
                case "using":
                case "import":
                case "switch":
                case "case":
                case "namespace":
                case "class":
                case "interface":
                case "struct":
                    return true;
            }

            return false;
        }

        private static bool IsPredefinedType(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            switch (text.ToLowerInvariant())
            {
                case "bool": return true;
                case "byte": return true;
                case "sbyte": return true;
                case "int": return true;
                case "uint": return true;
                case "short": return true;
                case "ushort": return true;
                case "long": return true;
                case "ulong": return true;
                case "float": return true;
                case "double": return true;
                case "decimal": return true;
                case "string": return true;
                case "char": return true;
                case "object": return true;
                case "void": return true;
            }

            return false;
        }

        internal static bool SendEnterThroughToEditor(CompletionRules rules, RoslynCompletionItem item, string textTypedSoFar)
        {
            var rule = item.Rules.EnterKeyRule;
            if (rule == EnterKeyRule.Default)
            {
                rule = rules.DefaultEnterKeyRule;
            }

            switch (rule)
            {
                default:
                case EnterKeyRule.Default:
                case EnterKeyRule.Never:
                    return false;
                case EnterKeyRule.Always:
                    return true;
                case EnterKeyRule.AfterFullyTypedWord:
                    // textTypedSoFar is concatenated from individual chars typed.
                    // '\n' is the enter char.
                    // That is why, there is no need to check for '\r\n'.
                    if (textTypedSoFar.LastOrDefault() == '\n')
                    {
                        textTypedSoFar = textTypedSoFar.Substring(0, textTypedSoFar.Length - 1);
                    }

                    return item.GetEntireDisplayText() == textTypedSoFar;
            }
        }

        private static CompletionProvider GetCompletionProvider(CompletionService completionService, CompletionItem item)
        {
            if (completionService is CompletionServiceWithProviders completionServiceWithProviders)
            {
                return completionServiceWithProviders.GetProvider(item);
            }

            return null;
        }
    }
}
