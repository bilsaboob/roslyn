// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal static class RSharpBuiltInSystemTypes
    {
        internal static bool IsGeneratedType(NamedTypeSymbol type)
        {
            if (type.Name == RSharpParamLambdaWithThisScopeAttributeGenerator.ATTRIBUTE_TYPE_NAME) return true;
            if (type.Name == RSharpParamSpreadAttributeGenerator.ATTRIBUTE_TYPE_NAME) return true;

            return false;
        }

        internal static void GenerateTypes(CSharpCompilation compilation)
        {
            // generate the param decoration attributes
            RSharpParamLambdaWithThisScopeAttributeGenerator.GetOrGenerate(compilation);
            RSharpParamSpreadAttributeGenerator.GetOrGenerate(compilation);
        }
    }
}
