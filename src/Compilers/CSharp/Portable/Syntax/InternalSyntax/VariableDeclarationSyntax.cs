using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    internal sealed partial class VariableDeclarationSyntax
    {
        public TypeSyntax Type
        {
            get
            {
                if (Variables.Count > 0)
                    return Variables[0].Type;

                return default;
            }
        }
    }
}
