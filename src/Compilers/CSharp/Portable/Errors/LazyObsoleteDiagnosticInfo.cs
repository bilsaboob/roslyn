// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed class LazyObsoleteDiagnosticInfo : DiagnosticInfo
    {
        private static HashSet<Symbol> _resolving = new HashSet<Symbol>();

        private DiagnosticInfo _lazyActualObsoleteDiagnostic;

        private readonly object _symbolOrSymbolWithAnnotations;
        private readonly Symbol _containingSymbol;
        private readonly BinderFlags _binderFlags;

        internal LazyObsoleteDiagnosticInfo(object symbol, Symbol containingSymbol, BinderFlags binderFlags)
            : base(CSharp.MessageProvider.Instance, (int)ErrorCode.Unknown)
        {
            Debug.Assert(symbol is Symbol || symbol is TypeWithAnnotations);
            _symbolOrSymbolWithAnnotations = symbol;
            _containingSymbol = containingSymbol;
            _binderFlags = binderFlags;
            _lazyActualObsoleteDiagnostic = null;
        }

        internal override DiagnosticInfo GetResolvedInfo()
        {
            if (_lazyActualObsoleteDiagnostic == null)
            {
                // A symbol's Obsoleteness may not have been calculated yet if the symbol is coming
                // from a different compilation's source. In that case, force completion of attributes.
                var symbol = (_symbolOrSymbolWithAnnotations as Symbol) ?? ((TypeWithAnnotations)_symbolOrSymbolWithAnnotations).Type;
                
                var kind = ObsoleteAttributeHelpers.GetObsoleteDiagnosticKind(symbol, _containingSymbol, forceComplete: false);
                var info = (kind == ObsoleteDiagnosticKind.Diagnostic) ?
                    ObsoleteAttributeHelpers.CreateObsoleteDiagnostic(symbol, _binderFlags) :
                    null;

                Interlocked.Exchange(ref _lazyActualObsoleteDiagnostic, info ?? CSDiagnosticInfo.VoidDiagnosticInfo);
                
                if (_resolving.Add(symbol))
                {
                    // force complete and do again
                    symbol.ForceCompleteObsoleteAttribute();

                    kind = ObsoleteAttributeHelpers.GetObsoleteDiagnosticKind(symbol, _containingSymbol, forceComplete: true);
                    Debug.Assert(kind != ObsoleteDiagnosticKind.Lazy);
                    Debug.Assert(kind != ObsoleteDiagnosticKind.LazyPotentiallySuppressed);

                    info = (kind == ObsoleteDiagnosticKind.Diagnostic) ? ObsoleteAttributeHelpers.CreateObsoleteDiagnostic(symbol, _binderFlags) : null;

                    // If this symbol is not obsolete or is in an obsolete context, we don't want to report any diagnostics.
                    // Therefore make this a Void diagnostic.
                    Interlocked.Exchange(ref _lazyActualObsoleteDiagnostic, info ?? CSDiagnosticInfo.VoidDiagnosticInfo);

                    _resolving.Remove(symbol);
                }
            }

            return _lazyActualObsoleteDiagnostic;
        }
    }
}
