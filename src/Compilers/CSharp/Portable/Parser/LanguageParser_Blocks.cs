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
        #region Blocks - statements
        private BlockSyntax ParseArrowStatementBlock(SyntaxList<AttributeListSyntax> attributes = default, bool semicolonRequired = false, bool isSimpleExpr = true)
        {
            var arrowToken = EatToken(SyntaxKind.EqualsGreaterThanToken);

            var stat = ParseStatementCore(
                attributes,
                semicolonRequired: semicolonRequired,
                isSimpleExpr: isSimpleExpr
            );

            // skip the arrow, it's not part of the syntax
            stat = AddLeadingSkippedSyntax(stat, arrowToken);

            var block = SyntaxFactory.FakeBlock(
                _syntaxFactory,
                statements: SyntaxFactory.List<StatementSyntax>(stat)
            );

            return block;
        }

        private BlockSyntax ParseStatementBlock(SyntaxList<AttributeListSyntax> attributes = default, bool semicolonRequired = false, bool isSimpleExpr = true)
        {
            var stat = ParseStatementCore(
                attributes,
                semicolonRequired: semicolonRequired,
                isSimpleExpr: isSimpleExpr,
                allowLambdaExpr: !isSimpleExpr
            );

            return SyntaxFactory.FakeBlock(
                _syntaxFactory,
                statements: SyntaxFactory.List<StatementSyntax>(stat)
            );
        }

        private StatementSyntax ParseSimpleStatement(SyntaxList<AttributeListSyntax> attributes = default, bool semicolonRequired = false, bool isSimpleExpr = true)
        {
            return ParseStatementCore(
                attributes,
                semicolonRequired: semicolonRequired,
                isSimpleExpr: isSimpleExpr,
                allowLambdaExpr: !isSimpleExpr
            );
        }
        #endregion

        #region Blocks - expression statements
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
            var didAllowLambdaExpression = AllowLambdaExpression;
            IsSimpleExpression = simpleExpr;
            AllowLambdaExpression = !simpleExpr;
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
                AllowLambdaExpression = didAllowLambdaExpression;
            }
        }
        #endregion
    }
}
