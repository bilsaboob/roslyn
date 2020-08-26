﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CSharp
{
    public partial class SyntaxFactory
    {
        // Preserved as shipped public API for binary compatibility
        public static LocalFunctionStatementSyntax LocalFunctionStatement(SyntaxTokenList modifiers, SyntaxToken identifier, TypeParameterListSyntax typeParameterList, ParameterListSyntax parameterList, TypeSyntax returnType, SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses, BlockSyntax body, ArrowExpressionClauseSyntax expressionBody)
        {
            return LocalFunctionStatement(attributeLists: default, modifiers, identifier, typeParameterList, parameterList, returnType, constraintClauses, body, expressionBody, semicolonToken: default);
        }

        // Preserved as shipped public API for binary compatibility
        public static LocalFunctionStatementSyntax LocalFunctionStatement(SyntaxTokenList modifiers, SyntaxToken identifier, TypeParameterListSyntax typeParameterList, ParameterListSyntax parameterList, TypeSyntax returnType, SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses, BlockSyntax body, ArrowExpressionClauseSyntax expressionBody, SyntaxToken semicolonToken)
        {
            return LocalFunctionStatement(attributeLists: default, modifiers, identifier, typeParameterList, parameterList, returnType, constraintClauses, body, expressionBody, semicolonToken);
        }
    }
}

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public partial class LocalFunctionStatementSyntax
    {
        // Preserved as shipped public API for binary compatibility
        public LocalFunctionStatementSyntax Update(SyntaxTokenList modifiers, SyntaxToken identifier, TypeParameterListSyntax typeParameterList, ParameterListSyntax parameterList, TypeSyntax returnType, SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses, BlockSyntax body, ArrowExpressionClauseSyntax expressionBody, SyntaxToken semicolonToken)
        {
            return Update(attributeLists: default, modifiers, identifier, typeParameterList, parameterList, returnType, constraintClauses, body, expressionBody, semicolonToken);
        }

        public bool HasExplicitReturnType()
        {
            var noExplicitReturnType = ReturnType.Kind() == SyntaxKind.IdentifierName && ReturnType.Width == 0;
            return !noExplicitReturnType;
        }
    }
}
