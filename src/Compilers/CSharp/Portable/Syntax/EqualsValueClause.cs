using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CSharp
{
    public partial class SyntaxFactory
    {
        /// <summary>Creates a new EqualsValueClauseSyntax instance.</summary>
        public static EqualsValueClauseSyntax EqualsValueClause(ExpressionSyntax value)
            => SyntaxFactory.EqualsValueClause(SyntaxFactory.Token(SyntaxKind.EqualsToken), value);
    }
}
