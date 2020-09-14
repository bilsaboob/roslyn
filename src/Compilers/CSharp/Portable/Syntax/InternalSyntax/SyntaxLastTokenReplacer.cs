// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    internal class SyntaxLastTokenReplacer : CSharpSyntaxRewriter
    {
        private readonly SyntaxToken _oldToken;
        private readonly SyntaxToken _newToken;
        private int _count = 1;
        private bool _found;

        private SyntaxLastTokenReplacer(SyntaxToken oldToken, SyntaxToken newToken)
        {
            _oldToken = oldToken;
            _newToken = newToken;
        }

        internal static TRoot Replace<TRoot>(TRoot root, SyntaxToken newToken, SyntaxToken oldToken = null)
            where TRoot : CSharpSyntaxNode
        {
            oldToken ??= root.GetLastNonZeroWidthToken();
            var replacer = new SyntaxLastTokenReplacer(oldToken, newToken);
            var newRoot = (TRoot)replacer.Visit(root);
            Debug.Assert(replacer._found);
            return newRoot;
        }

        private static int CountNonNullSlots(CSharpSyntaxNode node)
        {
            // count nodes that have Width > 0
            var count = 0;
            var nodes = node.ChildNodesAndTokens();

            foreach (var n in nodes)
            {
                if (n.Width > 0)
                    ++count;
            }

            return count;
        }

        public override CSharpSyntaxNode Visit(CSharpSyntaxNode node)
        {
            if (node != null && !_found)
            {
                if (node.Width > 0) _count--;

                var token = node as SyntaxToken;
                if (token != null)
                {
                    if (token == _oldToken)
                    {
                        _found = true;
                        return _newToken;
                    }
                }

                if (_count == 0)
                {
                    _count += CountNonNullSlots(node);
                    return base.Visit(node);
                }
            }

            return node;
        }
    }
}
