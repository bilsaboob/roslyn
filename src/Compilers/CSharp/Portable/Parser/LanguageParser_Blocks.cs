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
        private BlockSyntax ParseArrowExprStatementBlock(SyntaxList<AttributeListSyntax> attributes = default, bool semicolonRequired = false, bool simpleExpr = true)
        {
            var wasSimpleExpression = IsSimpleExpression;
            IsSimpleExpression = simpleExpr;
            try
            {
                var arrowToken = EatToken(SyntaxKind.EqualsGreaterThanToken);

                var exprStat = ParseExpressionStatement(attributes, semicolonRequired: semicolonRequired);

                // skip the arrow, it's not part of the syntax
                exprStat = AddLeadingSkippedSyntax(exprStat, arrowToken);

                var block = SyntaxFactory.FakeBlock(
                    _syntaxFactory,
                    statements: SyntaxFactory.List<StatementSyntax>(exprStat)
                );

                return block;
            }
            finally
            {
                IsSimpleExpression = wasSimpleExpression;
            }
        }

        private BlockSyntax ParseExprStatementBlock(SyntaxList<AttributeListSyntax> attributes = default, bool semicolonRequired = false, bool simpleExpr = true)
        {
            var wasSimpleExpression = IsSimpleExpression;
            IsSimpleExpression = simpleExpr;
            try
            {
                var exprStat = ParseExpressionStatement(attributes, semicolonRequired: semicolonRequired);

                return SyntaxFactory.FakeBlock(
                    _syntaxFactory,
                    statements: SyntaxFactory.List<StatementSyntax>(exprStat)
                );
            }
            finally
            {
                IsSimpleExpression = wasSimpleExpression;
            }
        }
    }
}
