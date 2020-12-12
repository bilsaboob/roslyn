using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.Cci;
using static Microsoft.CodeAnalysis.CSharp.Symbols.GeneratedTypesManager;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal static class RSharpSystemAttributeGenerator
    {
        internal static GeneratedTypeSymbol GetOrGenerate(CSharpCompilation compilation, string attributeName)
        {
            var attrType = compilation.GeneratedTypesManager.GetRSharpAttributeType(attributeName);
            if (attrType is null)
                attrType = Generate(compilation, attributeName);
            return attrType;
        }

        private static GeneratedTypeSymbol Generate(CSharpCompilation compilation, string attributeName)
        {
            var diagnostics = DiagnosticBag.GetInstance();

            try
            {
                var tb = compilation.GeneratedTypesManager.GetRSharpAttributeTypeBuilder(attributeName, diagnostics);

                // extends the attribute base type
                tb.WithBaseType(compilation.GetWellKnownType(WellKnownType.System_Attribute));

                // add a default constructor
                tb.WithDefaultConstructor();

                var type = tb.ConstructType();
                return type;
            }
            finally
            {
                diagnostics.Free();
            }
        }
    }

    internal static class RSharpParamLambdaWithThisScopeAttributeGenerator
    {
        public const string ATTRIBUTE_TYPE_NAME = "RSharpParamLambdaWithThisScopeAttribute";

        internal static GeneratedTypeSymbol GetOrGenerate(CSharpCompilation compilation)
            => RSharpSystemAttributeGenerator.GetOrGenerate(compilation, ATTRIBUTE_TYPE_NAME);
    }

    internal static class RSharpParamSpreadAttributeGenerator
    {
        public const string ATTRIBUTE_TYPE_NAME = "RSharpParamSpreadAttribute";

        internal static GeneratedTypeSymbol GetOrGenerate(CSharpCompilation compilation)
            => RSharpSystemAttributeGenerator.GetOrGenerate(compilation, ATTRIBUTE_TYPE_NAME);
    }
}
