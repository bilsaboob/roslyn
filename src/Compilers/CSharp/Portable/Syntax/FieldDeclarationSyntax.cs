using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public partial class FieldDeclarationSyntax
    {
        public TypeSyntax Type => this.Declaration.Type;

        public bool HasExplicitReturnType()
        {
            var noExplicitReturnType = this.Declaration.Type.Kind() == SyntaxKind.IdentifierName && this.Declaration.Type.Width == 0;
            return !noExplicitReturnType;
        }
    }
}
