// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    internal abstract partial class TypeSyntax
    {
        public bool IsVar => IsIdentifierName("var");
        public bool IsUnmanaged => IsIdentifierName("unmanaged");
        public bool IsNotNull => IsIdentifierName("notnull");
        public bool IsNint => IsIdentifierName("nint");
        public bool IsNuint => IsIdentifierName("nuint");

        private bool IsIdentifierName(string id) => this is IdentifierNameSyntax name && name.Identifier.ToString() == id;

        public bool IsRef => Kind == SyntaxKind.RefType;

        internal virtual TypeSyntax ShareAsFake()
        {
            var shared = Clone();
            if (shared == null) return null;
            shared.IsFake = true;
            shared.FullWidth = 0;
            return shared;
        }

        internal virtual TypeSyntax Clone()
        {
            return null;
        }
    }

    internal sealed partial class IdentifierNameSyntax
    {
        internal override TypeSyntax Clone()
        {
            return new IdentifierNameSyntax(this.Kind, this.identifier, GetDiagnostics(), GetAnnotations());
        }
    }

    internal sealed partial class QualifiedNameSyntax
    {
        internal override TypeSyntax Clone()
        {
            return new QualifiedNameSyntax(this.Kind, this.left, this.dotToken, this.right, GetDiagnostics(), GetAnnotations());
        }
    }

    internal sealed partial class GenericNameSyntax
    {
        internal override TypeSyntax Clone()
        {
            return new GenericNameSyntax(this.Kind, this.identifier, this.typeArgumentList, GetDiagnostics(), GetAnnotations());
        }
    }

    internal sealed partial class PredefinedTypeSyntax
    {
        internal override TypeSyntax Clone()
        {
            return new PredefinedTypeSyntax(this.Kind, this.keyword, GetDiagnostics(), GetAnnotations());
        }
    }

    internal sealed partial class ArrayTypeSyntax
    {
        internal override TypeSyntax Clone()
        {
            return new ArrayTypeSyntax(this.Kind, this.elementType, this.rankSpecifiers, GetDiagnostics(), GetAnnotations());
        }
    }

    internal sealed partial class PointerTypeSyntax
    {
        internal override TypeSyntax Clone()
        {
            return new PointerTypeSyntax(this.Kind, this.elementType, this.asteriskToken, GetDiagnostics(), GetAnnotations());
        }
    }

    internal sealed partial class FunctionPointerTypeSyntax
    {
        internal override TypeSyntax Clone()
        {
            return new FunctionPointerTypeSyntax(this.Kind, this.delegateKeyword, this.asteriskToken, this.callingConvention, this.lessThanToken, this.parameters, this.greaterThanToken, GetDiagnostics(), GetAnnotations());
        }
    }

    internal sealed partial class NullableTypeSyntax
    {
        internal override TypeSyntax Clone()
        {
            return new NullableTypeSyntax(this.Kind, this.elementType, this.questionToken, GetDiagnostics(), GetAnnotations());
        }
    }

    internal sealed partial class TupleTypeSyntax
    {
        internal override TypeSyntax Clone()
        {
            return new TupleTypeSyntax(this.Kind, this.openParenToken, this.elements, this.closeParenToken, GetDiagnostics(), GetAnnotations());
        }
    }

    internal sealed partial class OmittedTypeArgumentSyntax
    {
        internal override TypeSyntax Clone()
        {
            return new OmittedTypeArgumentSyntax(this.Kind, this.omittedTypeArgumentToken, GetDiagnostics(), GetAnnotations());
        }
    }

    internal sealed partial class RefTypeSyntax
    {
        internal override TypeSyntax Clone()
        {
            return new RefTypeSyntax(this.Kind, this.refKeyword, this.readOnlyKeyword, this.type, GetDiagnostics(), GetAnnotations());
        }
    }
}
