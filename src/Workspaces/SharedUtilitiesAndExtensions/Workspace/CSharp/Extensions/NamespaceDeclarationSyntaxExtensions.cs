// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CSharp.Extensions
{
    internal static class NamespaceDeclarationSyntaxExtensions
    {
        public static NamespaceDeclarationSyntax AddUsingDirectives(
            this NamespaceDeclarationSyntax namespaceDeclaration,
            IList<UsingDirectiveSyntax> usingDirectives,
            bool placeSystemNamespaceFirst,
            params SyntaxAnnotation[] annotations)
        {
            if (usingDirectives.Count == 0)
            {
                return namespaceDeclaration;
            }

            var newUsings = new List<UsingDirectiveSyntax>();
            newUsings.AddRange(namespaceDeclaration.Usings);
            newUsings.AddRange(usingDirectives);

            newUsings.SortUsingDirectives(namespaceDeclaration.Usings, placeSystemNamespaceFirst);

            // make sure to update the "inserted" using to have a newline if they are the first using
            var lastAddedUsingIndex = -1;
            for (var i = 0; i < newUsings.Count; ++i)
            {
                var usingDecl = newUsings[i];
                var isAddedUsing = !namespaceDeclaration.Usings.Contains(usingDecl);
                if (isAddedUsing)
                {
                    lastAddedUsingIndex = i;
                }
                else
                {
                    // if the current using doesn't have a leading trivia, add a newline to the previously added using declaration
                    if (lastAddedUsingIndex != -1)
                    {
                        var lastAddedUsingDecl = newUsings[lastAddedUsingIndex];
                        var hasLineBreak = usingDecl.GetLeadingTrivia().Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
                        if (!hasLineBreak)
                            newUsings[lastAddedUsingIndex] = AddTrailingNewlineToUsing(lastAddedUsingDecl);
                        lastAddedUsingIndex = -1;
                    }
                }
            }

            // if the last using is an added one - then also append a newline trivia
            var lastUsing = newUsings.LastOrDefault();
            if (lastUsing != null)
            {
                var isAddedUsing = !namespaceDeclaration.Usings.Contains(lastUsing);
                if (isAddedUsing)
                    newUsings[newUsings.Count - 1] = AddTrailingNewlineToUsing(lastUsing);
            }

            newUsings = newUsings.Select(u => u.WithAdditionalAnnotations(annotations)).ToList();
            var newNamespace = namespaceDeclaration.WithUsings(newUsings.ToSyntaxList());
            return newNamespace;
        }

        private static UsingDirectiveSyntax AddTrailingNewlineToUsing(UsingDirectiveSyntax usingDecl)
        {
            return usingDecl.Update(
                usingDecl.ImportKeyword,
                usingDecl.UsingKeyword,
                usingDecl.StaticKeyword,
                usingDecl.Alias,
                usingDecl.Name.WithTrailingTrivia(SyntaxFactory.EndOfLine(System.Environment.NewLine)),
                usingDecl.SemicolonToken
            );
        }
    }
}
