using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public partial class VariableDeclarationSyntax
    {
        public TypeSyntax? Type
        {
            get
            {
                if (Variables.Count > 0)
                    return Variables[0].Type;

                return null;
            }
        }

        public VariableDeclarationSyntax WithType(TypeSyntax type)
        {
            var newVariables = new SeparatedSyntaxList<VariableDeclaratorSyntax>();
            for (var i = 0; i < Variables.Count; ++i)
            {
                newVariables.Add(Variables[i].WithType(type));
            }

            return WithVariables(Variables);
        }
    }
}
