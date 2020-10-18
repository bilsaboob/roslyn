// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Syntax.InternalSyntax;

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public partial class ParenthesizedLambdaExpressionSyntax
    {
        public new ParenthesizedLambdaExpressionSyntax WithBody(CSharpSyntaxNode body)
            => body is BlockSyntax block
                ? WithBlock(block).WithExpressionBody(null)
                : WithExpressionBody((ExpressionSyntax)body).WithBlock(null);

        public ParenthesizedLambdaExpressionSyntax Update(SyntaxToken asyncKeyword, ParameterListSyntax parameterList, SyntaxToken arrowToken, CSharpSyntaxNode body)
            => body is BlockSyntax block
                ? Update(asyncKeyword, parameterList, arrowToken, block, null)
                : Update(asyncKeyword, parameterList, arrowToken, null, (ExpressionSyntax)body);

        public override SyntaxToken AsyncKeyword
            => this.Modifiers.FirstOrDefault(SyntaxKind.AsyncKeyword);

        internal override AnonymousFunctionExpressionSyntax WithAsyncKeywordCore(SyntaxToken asyncKeyword)
            => WithAsyncKeyword(asyncKeyword);

        public new ParenthesizedLambdaExpressionSyntax WithAsyncKeyword(SyntaxToken asyncKeyword)
            => this.Update(asyncKeyword, this.ParameterList, this.ArrowToken, this.Block, this.ExpressionBody);

        public ParenthesizedLambdaExpressionSyntax Update(SyntaxToken asyncKeyword, ParameterListSyntax parameterList, SyntaxToken arrowToken, BlockSyntax? block, ExpressionSyntax? expressionBody)
            => Update(SyntaxFactory.TokenList(asyncKeyword), parameterList, arrowToken, block, expressionBody);

        public ParenthesizedLambdaExpressionSyntax WithAdjustedLambdaDefinitionAnnotation(int argIndex)
        {
            if (argIndex == -1) return this;
            return (ParenthesizedLambdaExpressionSyntax)WithAdditionalAnnotationsInternalWithParent(new[] { new SyntaxAnnotation("AdjustedLambdaDefinitionAnnotation",$"{argIndex}") });
        }

        public int? GetOriginalArgIndexForAdjustedLambdaDefinition()
        {
            var annotation = this.GetAnnotations().FirstOrDefault(a => a.Kind == "AdjustedLambdaDefinitionAnnotation");
            if (annotation == null || annotation.Data == null) return null;
            return int.Parse(annotation.Data);
        }
    }
}

namespace Microsoft.CodeAnalysis.CSharp
{
    public partial class SyntaxFactory
    {
        public static ParenthesizedLambdaExpressionSyntax ParenthesizedLambdaExpression(SyntaxToken asyncKeyword, ParameterListSyntax parameterList, SyntaxToken arrowToken, BlockSyntax? block, ExpressionSyntax? expressionBody)
            => ParenthesizedLambdaExpression(TokenList(asyncKeyword), parameterList, arrowToken, block, expressionBody);

        public static ParenthesizedLambdaExpressionSyntax ParenthesizedLambdaExpression(ParameterListSyntax parameterList, BlockSyntax? block, ExpressionSyntax? expressionBody, SyntaxNode parent = null, int position = 0)
            => ParenthesizedLambdaExpression(default(SyntaxTokenList), parameterList, block, expressionBody, parent: parent, position: position);

        /// <summary>Creates a new ParenthesizedLambdaExpressionSyntax instance.</summary>
        public static ParenthesizedLambdaExpressionSyntax ParenthesizedLambdaExpression(SyntaxTokenList modifiers, ParameterListSyntax parameterList, BlockSyntax? block, ExpressionSyntax? expressionBody, SyntaxNode? parent, int position)
            => SyntaxFactory.ParenthesizedLambdaExpression(modifiers, parameterList, SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken), block, expressionBody, parent: parent, position: position);

        /// <summary>Creates a new ParenthesizedLambdaExpressionSyntax instance.</summary>
        public static ParenthesizedLambdaExpressionSyntax ParenthesizedLambdaExpression(SyntaxTokenList modifiers, ParameterListSyntax parameterList, SyntaxToken arrowToken, BlockSyntax? block, ExpressionSyntax? expressionBody, SyntaxNode? parent, int position)
        {
            if (parameterList == null) throw new ArgumentNullException(nameof(parameterList));
            if (arrowToken.Kind() != SyntaxKind.EqualsGreaterThanToken) throw new ArgumentException(nameof(arrowToken));
            return (ParenthesizedLambdaExpressionSyntax)Syntax.InternalSyntax.SyntaxFactory.ParenthesizedLambdaExpression(modifiers.Node.ToGreenList<Syntax.InternalSyntax.SyntaxToken>(), (Syntax.InternalSyntax.ParameterListSyntax)parameterList.Green, (Syntax.InternalSyntax.SyntaxToken)arrowToken.Node!, block == null ? null : (Syntax.InternalSyntax.BlockSyntax)block.Green, expressionBody == null ? null : (Syntax.InternalSyntax.ExpressionSyntax)expressionBody.Green).CreateRed(parent, position);
        }
    }
}
