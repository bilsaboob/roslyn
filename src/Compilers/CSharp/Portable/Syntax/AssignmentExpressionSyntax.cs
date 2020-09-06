using System;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CSharp
{
    public partial class SyntaxFactory
    {
        /// <summary>Creates a new AssignmentExpressionSyntax instance.</summary>
        public static AssignmentExpressionSyntax AssignmentExpression(SyntaxKind kind, ExpressionSyntax left, ExpressionSyntax right)
            => SyntaxFactory.AssignmentExpression(kind, left, SyntaxFactory.Token(GetAssignmentExpressionOperatorTokenKind(kind)), right);

        private static SyntaxKind GetAssignmentExpressionOperatorTokenKind(SyntaxKind kind)
            => kind switch
            {
                SyntaxKind.SimpleAssignmentExpression => SyntaxKind.EqualsToken,
                SyntaxKind.AddAssignmentExpression => SyntaxKind.PlusEqualsToken,
                SyntaxKind.SubtractAssignmentExpression => SyntaxKind.MinusEqualsToken,
                SyntaxKind.MultiplyAssignmentExpression => SyntaxKind.AsteriskEqualsToken,
                SyntaxKind.DivideAssignmentExpression => SyntaxKind.SlashEqualsToken,
                SyntaxKind.ModuloAssignmentExpression => SyntaxKind.PercentEqualsToken,
                SyntaxKind.AndAssignmentExpression => SyntaxKind.AmpersandEqualsToken,
                SyntaxKind.ExclusiveOrAssignmentExpression => SyntaxKind.CaretEqualsToken,
                SyntaxKind.OrAssignmentExpression => SyntaxKind.BarEqualsToken,
                SyntaxKind.LeftShiftAssignmentExpression => SyntaxKind.LessThanLessThanEqualsToken,
                SyntaxKind.RightShiftAssignmentExpression => SyntaxKind.GreaterThanGreaterThanEqualsToken,
                SyntaxKind.CoalesceAssignmentExpression => SyntaxKind.QuestionQuestionEqualsToken,
                _ => throw new ArgumentOutOfRangeException(),
            };
    }
}
