// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal sealed class ConstructedMethodSymbol : SubstitutedMethodSymbol
    {
        private readonly ImmutableArray<TypeWithAnnotations> _typeArgumentsWithAnnotations;

        internal ConstructedMethodSymbol(MethodSymbol constructedFrom, ImmutableArray<TypeWithAnnotations> typeArgumentsWithAnnotations)
            : base(containingSymbol: constructedFrom.ContainingType,
                   map: new TypeMap(constructedFrom.ContainingType, ((MethodSymbol)constructedFrom.OriginalDefinition).TypeParameters, typeArgumentsWithAnnotations),
                   originalDefinition: (MethodSymbol)constructedFrom.OriginalDefinition,
                   constructedFrom: constructedFrom)
        {
            _typeArgumentsWithAnnotations = typeArgumentsWithAnnotations;

            // decorate with the annotation types
            /*var originalTypeParams = constructedFrom.TypeParameters;
            if (originalTypeParams.Length > 0 && originalTypeParams.Length == typeArgumentsWithAnnotations.Length)
            {
                var newTypeArgumentsWithAnnotations = ArrayBuilder<TypeWithAnnotations>.GetInstance();

                for (var i = 0; i < _typeArgumentsWithAnnotations.Length; ++i)
                {
                    var origTypeParam = originalTypeParams[i];
                    var newTypeArgument = _typeArgumentsWithAnnotations[i];

                    if (origTypeParam.AnnotationTypeKind != null && origTypeParam.AnnotationTypeKind != null)
                        newTypeArgument = newTypeArgument.WithAnnotationType(origTypeParam.AnnotationType, origTypeParam.AnnotationTypeKind.Value);

                    newTypeArgumentsWithAnnotations.Add(newTypeArgument);
                }

                _typeArgumentsWithAnnotations = newTypeArgumentsWithAnnotations.ToImmutableAndFree();
            }*/
        }

        public override ImmutableArray<TypeWithAnnotations> TypeArgumentsWithAnnotations
        {
            get
            {
                return _typeArgumentsWithAnnotations;
            }
        }
    }
}
