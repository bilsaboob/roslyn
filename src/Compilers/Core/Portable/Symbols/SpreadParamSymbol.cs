using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Microsoft.CodeAnalysis.Symbols
{
    public class SpreadParamSymbol : WrappedSymbol, IFieldSymbol, IPropertySymbol
    {
        public SpreadParamSymbol(ISymbol memberSymbol, ISymbol paramSymbol)
            : base(memberSymbol)
        {
            ParamSymbol = paramSymbol;
        }

        public ISymbol ParamSymbol { get; }

        #region IPropertySymbol
        private IPropertySymbol AsPropertySymbol => Symbol as IPropertySymbol;

        bool IPropertySymbol.IsIndexer => AsPropertySymbol?.IsIndexer ?? false;

        bool IPropertySymbol.IsReadOnly => AsPropertySymbol?.IsReadOnly ?? false;

        bool IPropertySymbol.IsWriteOnly => AsPropertySymbol?.IsWriteOnly ?? false;

        bool IPropertySymbol.IsWithEvents => AsPropertySymbol?.IsWithEvents ?? false;

        bool IPropertySymbol.ReturnsByRef => AsPropertySymbol?.ReturnsByRef ?? false;

        bool IPropertySymbol.ReturnsByRefReadonly => AsPropertySymbol?.ReturnsByRefReadonly ?? false;

        RefKind IPropertySymbol.RefKind => AsPropertySymbol?.RefKind ?? RefKind.None;

        ITypeSymbol IPropertySymbol.Type => AsPropertySymbol?.Type;

        NullableAnnotation IPropertySymbol.NullableAnnotation => AsPropertySymbol?.NullableAnnotation ?? NullableAnnotation.None;

        ImmutableArray<IParameterSymbol> IPropertySymbol.Parameters => AsPropertySymbol?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty;

        IMethodSymbol IPropertySymbol.GetMethod => AsPropertySymbol?.GetMethod;

        IMethodSymbol IPropertySymbol.SetMethod => AsPropertySymbol?.SetMethod;

        IPropertySymbol IPropertySymbol.OverriddenProperty => AsPropertySymbol?.OverriddenProperty;

        ImmutableArray<IPropertySymbol> IPropertySymbol.ExplicitInterfaceImplementations => AsPropertySymbol?.ExplicitInterfaceImplementations ?? ImmutableArray<IPropertySymbol>.Empty;

        ImmutableArray<CustomModifier> IPropertySymbol.RefCustomModifiers => AsPropertySymbol?.RefCustomModifiers ?? ImmutableArray<CustomModifier>.Empty;

        ImmutableArray<CustomModifier> IPropertySymbol.TypeCustomModifiers => AsPropertySymbol?.TypeCustomModifiers ?? ImmutableArray<CustomModifier>.Empty;
        #endregion

        #region IFieldSymbol
        private IFieldSymbol AsFieldSymbol => Symbol as IFieldSymbol;

        ISymbol IFieldSymbol.AssociatedSymbol => AsFieldSymbol?.AssociatedSymbol;

        bool IFieldSymbol.IsConst => AsFieldSymbol?.IsConst ?? false;

        bool IFieldSymbol.IsReadOnly => AsFieldSymbol?.IsReadOnly ?? false;

        bool IFieldSymbol.IsVolatile => AsFieldSymbol?.IsVolatile ?? false;

        bool IFieldSymbol.IsFixedSizeBuffer => AsFieldSymbol?.IsFixedSizeBuffer ?? false;

        ITypeSymbol IFieldSymbol.Type => AsFieldSymbol?.Type;

        NullableAnnotation IFieldSymbol.NullableAnnotation => AsFieldSymbol?.NullableAnnotation ?? NullableAnnotation.None;

        bool IFieldSymbol.HasConstantValue => AsFieldSymbol?.HasConstantValue ?? false;

        object IFieldSymbol.ConstantValue => AsFieldSymbol?.ConstantValue;

        ImmutableArray<CustomModifier> IFieldSymbol.CustomModifiers => AsFieldSymbol?.CustomModifiers ?? ImmutableArray<CustomModifier>.Empty;

        IFieldSymbol IFieldSymbol.CorrespondingTupleField => AsFieldSymbol?.CorrespondingTupleField;

        IFieldSymbol IFieldSymbol.OriginalDefinition => AsFieldSymbol?.OriginalDefinition;

        IPropertySymbol IPropertySymbol.OriginalDefinition => throw new NotImplementedException();
        #endregion
    }
}
