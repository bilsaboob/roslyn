using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    using System.Linq.Expressions;
    using System.Text;
    using Microsoft.CodeAnalysis.Syntax.InternalSyntax;

    internal partial class LanguageParser
    {
        private SyntaxToken TryParseEndOfLineSemicolon(
            bool fakeAtStatementEnd = false,
            bool fakeAtNewline = false,
            bool optional = false
            )
        {
            if (CurrentToken.Kind == SyntaxKind.SemicolonToken)
            {
                return this.EatToken(SyntaxKind.SemicolonToken);
            }
            else if(optional)
            {
                return SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken, ";");
            }
            else if (fakeAtNewline && IsCurrentTokenOnNewline)
            {
                return SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken, ";");
            }
            else if (fakeAtStatementEnd && IsProbablyStatementEnd())
            {
                return SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken, ";");
            }

            // error: expected semicolon token
            return this.EatToken(SyntaxKind.SemicolonToken);
        }
    }
}
