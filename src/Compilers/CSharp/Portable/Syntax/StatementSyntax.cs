using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public static partial class StatementSyntaxNodeExtensions
    {
        public static bool IsInlineStatement(this SyntaxNode node)
        {
            if (node is TryStatementSyntax tryStatement)
            {
                return tryStatement.IsInlineBlockTryCatchStatement();
            }

            return false;
        }
    }
}
