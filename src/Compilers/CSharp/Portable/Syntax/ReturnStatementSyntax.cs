// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Syntax.InternalSyntax;

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public partial class ReturnStatementSyntax
    {
        public ReturnStatementSyntax Update(SyntaxToken returnKeyword, ExpressionSyntax expression, SyntaxToken semicolonToken)
            => Update(attributeLists: default, returnKeyword, expression, semicolonToken);
    }
}

namespace Microsoft.CodeAnalysis.CSharp
{
    public partial class SyntaxFactory
    {
        public static ReturnStatementSyntax ReturnStatement(SyntaxToken returnKeyword, ExpressionSyntax expression, SyntaxToken semicolonToken)
            => ReturnStatement(attributeLists: default, returnKeyword, expression, semicolonToken);

        internal static ReturnStatementSyntax ReturnStatement(SyntaxToken returnKeyword, ExpressionSyntax expression, SyntaxToken semicolonToken, SyntaxNode parent, int position)
            => ReturnStatement(attributeLists: default, returnKeyword, expression, semicolonToken, parent, position);

        internal static ReturnStatementSyntax ReturnStatement(SyntaxList<AttributeListSyntax> attributeLists, SyntaxToken returnKeyword, ExpressionSyntax? expression, SyntaxToken semicolonToken, SyntaxNode parent, int position)
        {
            if (returnKeyword.Kind() != SyntaxKind.ReturnKeyword) throw new System.ArgumentException(nameof(returnKeyword));
            if (semicolonToken.Kind() != SyntaxKind.SemicolonToken) throw new System.ArgumentException(nameof(semicolonToken));
            return (ReturnStatementSyntax)Syntax.InternalSyntax.SyntaxFactory.ReturnStatement(attributeLists.Node.ToGreenList<Syntax.InternalSyntax.AttributeListSyntax>(), (Syntax.InternalSyntax.SyntaxToken)returnKeyword.Node!, expression == null ? null : (Syntax.InternalSyntax.ExpressionSyntax)expression.Green, (Syntax.InternalSyntax.SyntaxToken)semicolonToken.Node!).CreateRed(parent, position);
        }
    }
}
