using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public partial class DeclarationExpressionSyntax
    {
        public bool HasExplicitReturnType()
        {
            var noExplicitReturnType = Type.Kind() == SyntaxKind.IdentifierName && Type.Width == 0;
            return !noExplicitReturnType;
        }
    }
}
