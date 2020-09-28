// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public partial class BlockSyntax
    {
        public BlockSyntax Update(SyntaxToken openBraceToken, SyntaxList<StatementSyntax> statements, SyntaxToken closeBraceToken)
            => Update(attributeLists: default, openBraceToken, statements, closeBraceToken);

        public bool IsInlineBlockStatement()
        {
            return OpenBraceToken.Width == 0 && CloseBraceToken.Width == 0 && Statements.Count <= 1;
        }

        public bool TryGetInlineBlockStatement(out SyntaxNode statement)
        {
            statement = null;
            if (!IsInlineBlockStatement()) return false;
            if (Statements.Count == 1)
                statement = Statements[0];
            else
                statement = this;
            return true;
        }
    }
}

namespace Microsoft.CodeAnalysis.CSharp
{
    public partial class SyntaxFactory
    {
        public static BlockSyntax Block(SyntaxToken openBraceToken, SyntaxList<StatementSyntax> statements, SyntaxToken closeBraceToken)
            => Block(attributeLists: default, openBraceToken, statements, closeBraceToken);
    }
}
