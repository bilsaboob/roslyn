// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.ComponentModel;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public sealed partial class ArgumentSyntax
    {
        /// <summary>
        /// Pre C# 7.2 back-compat overload, which simply calls the replacement property <see cref="RefKindKeyword"/>.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public SyntaxToken RefOrOutKeyword
        {
            get => this.RefKindKeyword;
        }

        /// <summary>
        /// Pre C# 7.2 back-compat overload, which simply calls the replacement method <see cref="Update"/>.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ArgumentSyntax WithRefOrOutKeyword(SyntaxToken refOrOutKeyword)
        {
            return this.Update(this.NameColon, refOrOutKeyword, this.Expression);
        }
    }
}

namespace Microsoft.CodeAnalysis.CSharp
{
    public partial class SyntaxFactory
    {
        public static ArgumentSyntax ArgumentWithParent(ExpressionSyntax expression, SyntaxNode parent, int position)
        {
            return ArgumentWithParent(null, default, expression, parent, position);
        }

        /// <summary>Creates a new ArgumentSyntax instance.</summary>
        public static ArgumentSyntax ArgumentWithParent(NameColonSyntax? nameColon, SyntaxToken refKindKeyword, ExpressionSyntax expression, SyntaxNode parent, int position)
        {
            switch (refKindKeyword.Kind())
            {
                case SyntaxKind.RefKeyword:
                case SyntaxKind.OutKeyword:
                case SyntaxKind.InKeyword:
                case SyntaxKind.None: break;
                default: throw new ArgumentException(nameof(refKindKeyword));
            }
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            return (ArgumentSyntax)Syntax.InternalSyntax.SyntaxFactory.Argument(
                nameColon == null ? null : (Syntax.InternalSyntax.NameColonSyntax)nameColon.Green,
                (Syntax.InternalSyntax.SyntaxToken?)refKindKeyword.Node,
                (Syntax.InternalSyntax.ExpressionSyntax)expression.Green
            ).CreateRed(parent, position);
        }
    }
}

