﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    using Microsoft.CodeAnalysis.Syntax.InternalSyntax;

    internal static partial class SyntaxFactory
    {
        private const string CrLf = "\r\n";
        internal static readonly SyntaxTrivia CarriageReturnLineFeed = EndOfLine(CrLf);
        internal static readonly SyntaxTrivia LineFeed = EndOfLine("\n");
        internal static readonly SyntaxTrivia CarriageReturn = EndOfLine("\r");
        internal static readonly SyntaxTrivia Space = Whitespace(" ");
        internal static readonly SyntaxTrivia Tab = Whitespace("\t");

        internal static readonly SyntaxTrivia ElasticCarriageReturnLineFeed = EndOfLine(CrLf, elastic: true);
        internal static readonly SyntaxTrivia ElasticLineFeed = EndOfLine("\n", elastic: true);
        internal static readonly SyntaxTrivia ElasticCarriageReturn = EndOfLine("\r", elastic: true);
        internal static readonly SyntaxTrivia ElasticSpace = Whitespace(" ", elastic: true);
        internal static readonly SyntaxTrivia ElasticTab = Whitespace("\t", elastic: true);

        internal static readonly SyntaxTrivia ElasticZeroSpace = Whitespace(string.Empty, elastic: true);

        private static SyntaxToken s_xmlCarriageReturnLineFeed;
        private static SyntaxToken XmlCarriageReturnLineFeed
        {
            get
            {
                return s_xmlCarriageReturnLineFeed ?? (s_xmlCarriageReturnLineFeed = XmlTextNewLine(CrLf));
            }
        }


        // NOTE: it would be nice to have constants for OmittedArraySizeException and OmittedTypeArgument,
        // but it's non-trivial to introduce such constants, since they would make this class take a dependency
        // on the static fields of SyntaxToken (specifically, TokensWithNoTrivia via SyntaxToken.Create).  That
        // could cause unpredictable behavior, since SyntaxToken's static constructor already depends on the 
        // static fields of this class (specifically, ElasticZeroSpace).

        internal static SyntaxTrivia EndOfLine(string text, bool elastic = false)
        {
            SyntaxTrivia trivia = null;

            // use predefined trivia
            switch (text)
            {
                case "\r":
                    trivia = elastic ? SyntaxFactory.ElasticCarriageReturn : SyntaxFactory.CarriageReturn;
                    break;
                case "\n":
                    trivia = elastic ? SyntaxFactory.ElasticLineFeed : SyntaxFactory.LineFeed;
                    break;
                case "\r\n":
                    trivia = elastic ? SyntaxFactory.ElasticCarriageReturnLineFeed : SyntaxFactory.CarriageReturnLineFeed;
                    break;
            }

            // note: predefined trivia might not yet be defined during initialization
            if (trivia != null)
            {
                return trivia;
            }

            trivia = SyntaxTrivia.Create(SyntaxKind.EndOfLineTrivia, text);
            if (!elastic)
            {
                return trivia;
            }

            return trivia.WithAnnotationsGreen(new[] { SyntaxAnnotation.ElasticAnnotation });
        }

        internal static SyntaxTrivia Whitespace(string text, bool elastic = false)
        {
            var trivia = SyntaxTrivia.Create(SyntaxKind.WhitespaceTrivia, text);
            if (!elastic)
            {
                return trivia;
            }

            return trivia.WithAnnotationsGreen(new[] { SyntaxAnnotation.ElasticAnnotation });
        }

        internal static SyntaxTrivia Comment(string text)
        {
            if (text.StartsWith("/*", StringComparison.Ordinal))
            {
                return SyntaxTrivia.Create(SyntaxKind.MultiLineCommentTrivia, text);
            }
            else
            {
                return SyntaxTrivia.Create(SyntaxKind.SingleLineCommentTrivia, text);
            }
        }

        internal static SyntaxTrivia ConflictMarker(string text)
            => SyntaxTrivia.Create(SyntaxKind.ConflictMarkerTrivia, text);

        internal static SyntaxTrivia DisabledText(string text)
        {
            return SyntaxTrivia.Create(SyntaxKind.DisabledTextTrivia, text);
        }

        internal static SyntaxTrivia PreprocessingMessage(string text)
        {
            return SyntaxTrivia.Create(SyntaxKind.PreprocessingMessageTrivia, text);
        }

        public static SyntaxToken Token(SyntaxKind kind)
        {
            return SyntaxToken.Create(kind);
        }

        internal static SyntaxToken Token(GreenNode leading, SyntaxKind kind, GreenNode trailing)
        {
            return SyntaxToken.Create(kind, leading, trailing);
        }

        internal static SyntaxToken FakeToken(SyntaxKind kind, string value = null, bool allowTrivia = false)
        {
            return SyntaxToken.CreateFake(kind, value, allowTrivia);
        }

        internal static IdentifierNameSyntax FakeTypeIdentifier(ContextAwareSyntax syntaxFactory = null, bool isVar = false)
        {
            var token = SyntaxFactory.Token(SyntaxKind.IdentifierToken);
            var type = syntaxFactory?.IdentifierName(token) ?? SyntaxFactory.IdentifierName(token);
            if (isVar) type.IsVar = true;
            return type;
        }

        internal static IdentifierNameSyntax FakeIdentifier(ContextAwareSyntax syntaxFactory = null)
        {
            var token = SyntaxFactory.Token(SyntaxKind.IdentifierToken);
            var type = syntaxFactory?.IdentifierName(token) ?? SyntaxFactory.IdentifierName(token);
            return type;
        }

        internal static SyntaxToken Token(GreenNode leading, SyntaxKind kind, string text, string valueText, GreenNode trailing)
        {
            Debug.Assert(SyntaxFacts.IsAnyToken(kind));
            Debug.Assert(kind != SyntaxKind.IdentifierToken);
            Debug.Assert(kind != SyntaxKind.CharacterLiteralToken);
            Debug.Assert(kind != SyntaxKind.NumericLiteralToken);

            string defaultText = SyntaxFacts.GetText(kind);
            return kind >= SyntaxToken.FirstTokenWithWellKnownText && kind <= SyntaxToken.LastTokenWithWellKnownText && text == defaultText && valueText == defaultText
                ? Token(leading, kind, trailing)
                : SyntaxToken.WithValue(kind, leading, text, valueText, trailing);
        }

        internal static SyntaxToken MissingToken(SyntaxKind kind)
        {
            return SyntaxToken.CreateMissing(kind, null, null);
        }

        internal static SyntaxToken MissingToken(GreenNode leading, SyntaxKind kind, GreenNode trailing)
        {
            return SyntaxToken.CreateMissing(kind, leading, trailing);
        }

        internal static SyntaxToken Identifier(string text)
        {
            return Identifier(SyntaxKind.IdentifierToken, null, text, text, null);
        }

        internal static SyntaxToken Identifier(GreenNode leading, string text, GreenNode trailing)
        {
            return Identifier(SyntaxKind.IdentifierToken, leading, text, text, trailing);
        }

        internal static SyntaxToken Identifier(SyntaxKind contextualKind, GreenNode leading, string text, string valueText, GreenNode trailing)
        {
            return SyntaxToken.Identifier(contextualKind, leading, text, valueText, trailing);
        }

        internal static SyntaxToken Literal(GreenNode leading, string text, int value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.NumericLiteralToken, leading, text, value, trailing);
        }

        internal static SyntaxToken Literal(GreenNode leading, string text, uint value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.NumericLiteralToken, leading, text, value, trailing);
        }

        internal static SyntaxToken Literal(GreenNode leading, string text, long value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.NumericLiteralToken, leading, text, value, trailing);
        }

        internal static SyntaxToken Literal(GreenNode leading, string text, ulong value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.NumericLiteralToken, leading, text, value, trailing);
        }

        internal static SyntaxToken Literal(GreenNode leading, string text, float value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.NumericLiteralToken, leading, text, value, trailing);
        }

        internal static SyntaxToken Literal(GreenNode leading, string text, double value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.NumericLiteralToken, leading, text, value, trailing);
        }

        internal static SyntaxToken Literal(GreenNode leading, string text, decimal value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.NumericLiteralToken, leading, text, value, trailing);
        }

        internal static SyntaxToken Literal(GreenNode leading, string text, string value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.StringLiteralToken, leading, text, value, trailing);
        }

        internal static SyntaxToken Literal(GreenNode leading, string text, SyntaxKind kind, string value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(kind, leading, text, value, trailing);
        }

        internal static SyntaxToken Literal(GreenNode leading, string text, char value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.CharacterLiteralToken, leading, text, value, trailing);
        }

        internal static SyntaxToken BadToken(GreenNode leading, string text, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.BadToken, leading, text, text, trailing);
        }

        internal static SyntaxToken XmlTextLiteral(GreenNode leading, string text, string value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.XmlTextLiteralToken, leading, text, value, trailing);
        }

        internal static SyntaxToken XmlTextNewLine(GreenNode leading, string text, string value, GreenNode trailing)
        {
            if (leading == null && trailing == null && text == CrLf && value == CrLf)
            {
                return XmlCarriageReturnLineFeed;
            }

            return SyntaxToken.WithValue(SyntaxKind.XmlTextLiteralNewLineToken, leading, text, value, trailing);
        }

        internal static SyntaxToken XmlTextNewLine(string text)
        {
            return SyntaxToken.WithValue(SyntaxKind.XmlTextLiteralNewLineToken, null, text, text, null);
        }

        internal static SyntaxToken XmlEntity(GreenNode leading, string text, string value, GreenNode trailing)
        {
            return SyntaxToken.WithValue(SyntaxKind.XmlEntityLiteralToken, leading, text, value, trailing);
        }

        internal static SyntaxTrivia DocumentationCommentExteriorTrivia(string text)
        {
            return SyntaxTrivia.Create(SyntaxKind.DocumentationCommentExteriorTrivia, text);
        }

        public static SyntaxList<TNode> List<TNode>() where TNode : CSharpSyntaxNode
        {
            return default(SyntaxList<TNode>);
        }

        public static SyntaxList<TNode> List<TNode>(TNode node) where TNode : CSharpSyntaxNode
        {
            return new SyntaxList<TNode>(SyntaxList.List(node));
        }

        public static SyntaxList<TNode> List<TNode>(TNode node0, TNode node1) where TNode : CSharpSyntaxNode
        {
            return new SyntaxList<TNode>(SyntaxList.List(node0, node1));
        }

        internal static GreenNode ListNode(CSharpSyntaxNode node0, CSharpSyntaxNode node1)
        {
            return SyntaxList.List(node0, node1);
        }

        public static SyntaxList<TNode> List<TNode>(TNode node0, TNode node1, TNode node2) where TNode : CSharpSyntaxNode
        {
            return new SyntaxList<TNode>(SyntaxList.List(node0, node1, node2));
        }

        internal static GreenNode ListNode(CSharpSyntaxNode node0, CSharpSyntaxNode node1, CSharpSyntaxNode node2)
        {
            return SyntaxList.List(node0, node1, node2);
        }

        public static SyntaxList<TNode> List<TNode>(params TNode[] nodes) where TNode : CSharpSyntaxNode
        {
            if (nodes != null)
            {
                return new SyntaxList<TNode>(SyntaxList.List(nodes));
            }

            return default(SyntaxList<TNode>);
        }

        internal static GreenNode ListNode(params ArrayElement<GreenNode>[] nodes)
        {
            return SyntaxList.List(nodes);
        }

        public static SeparatedSyntaxList<TNode> SeparatedList<TNode>(TNode node) where TNode : CSharpSyntaxNode
        {
            return new SeparatedSyntaxList<TNode>(new SyntaxList<CSharpSyntaxNode>(node));
        }

        public static SeparatedSyntaxList<TNode> SeparatedList<TNode>(SyntaxToken token) where TNode : CSharpSyntaxNode
        {
            return new SeparatedSyntaxList<TNode>(new SyntaxList<CSharpSyntaxNode>(token));
        }

        public static SeparatedSyntaxList<TNode> SeparatedList<TNode>(TNode node1, SyntaxToken token, TNode node2) where TNode : CSharpSyntaxNode
        {
            return new SeparatedSyntaxList<TNode>(new SyntaxList<CSharpSyntaxNode>(SyntaxList.List(node1, token, node2)));
        }

        public static SeparatedSyntaxList<TNode> SeparatedList<TNode>(params CSharpSyntaxNode[] nodes) where TNode : CSharpSyntaxNode
        {
            if (nodes != null)
            {
                return new SeparatedSyntaxList<TNode>(SyntaxList.List(nodes));
            }

            return default(SeparatedSyntaxList<TNode>);
        }

        internal static IEnumerable<SyntaxTrivia> GetWellKnownTrivia()
        {
            yield return CarriageReturnLineFeed;
            yield return LineFeed;
            yield return CarriageReturn;
            yield return Space;
            yield return Tab;

            yield return ElasticCarriageReturnLineFeed;
            yield return ElasticLineFeed;
            yield return ElasticCarriageReturn;
            yield return ElasticSpace;
            yield return ElasticTab;

            yield return ElasticZeroSpace;
        }

        internal static IEnumerable<SyntaxToken> GetWellKnownTokens()
        {
            return SyntaxToken.GetWellKnownTokens();
        }
    }

    internal static partial class SyntaxFactory
    {
        public static TryStatementSyntax FakeTryStatement(
            ContextAwareSyntax syntaxFactory = null,
            Microsoft.CodeAnalysis.Syntax.InternalSyntax.SyntaxList<AttributeListSyntax> attributeLists = default,
            SyntaxToken tryToken = null,
            BlockSyntax tryBlock = null,
            Microsoft.CodeAnalysis.Syntax.InternalSyntax.SyntaxList<CatchClauseSyntax> catchClauses = default,
            SyntaxToken finallyToken = null,
            BlockSyntax finallyBlock = null
            )
        {
            tryToken ??= SyntaxFactory.FakeToken(SyntaxKind.TryKeyword, "try");

            FinallyClauseSyntax finallyClause = null;
            if (finallyBlock != null)
            {
                if (syntaxFactory != null)
                    finallyClause = syntaxFactory.FinallyClause(finallyToken, finallyBlock);
                else
                    SyntaxFactory.FinallyClause(finallyToken, finallyBlock);
            }

            if (syntaxFactory != null)
                return syntaxFactory.TryStatement(attributeLists: attributeLists, tryToken, tryBlock, catches: catchClauses, finallyClause);
            else
                return SyntaxFactory.TryStatement(attributeLists: attributeLists, tryToken, tryBlock, catches: catchClauses, finallyClause);
        }

        public static BlockSyntax FakeBlock(
            ContextAwareSyntax syntaxFactory = null,
            Microsoft.CodeAnalysis.Syntax.InternalSyntax.SyntaxList<AttributeListSyntax> attributeLists = default,
            SyntaxToken openBraceToken = null,
            Microsoft.CodeAnalysis.Syntax.InternalSyntax.SyntaxList<StatementSyntax> statements = default,
            SyntaxToken closeBraceToken = null)
        {
            openBraceToken ??= SyntaxFactory.FakeToken(SyntaxKind.OpenBraceToken, "{");
            closeBraceToken ??= SyntaxFactory.FakeToken(SyntaxKind.CloseBraceToken, "}");
            if (syntaxFactory != null)
                return syntaxFactory.Block(attributeLists, openBraceToken, statements, closeBraceToken);
            return SyntaxFactory.Block(attributeLists, openBraceToken, statements, closeBraceToken);
        }

        public static BlockSyntax MissingBlock(
            ContextAwareSyntax syntaxFactory = null,
            Microsoft.CodeAnalysis.Syntax.InternalSyntax.SyntaxList<AttributeListSyntax> attributeLists = default,
            SyntaxToken openBraceToken = null,
            Microsoft.CodeAnalysis.Syntax.InternalSyntax.SyntaxList<StatementSyntax> statements = default,
            SyntaxToken closeBraceToken = null)
        {
            openBraceToken ??= SyntaxFactory.MissingToken(SyntaxKind.OpenBraceToken);
            closeBraceToken ??= SyntaxFactory.MissingToken(SyntaxKind.CloseBraceToken);
            if (syntaxFactory != null)
                return syntaxFactory.Block(attributeLists, openBraceToken, statements, closeBraceToken);
            return SyntaxFactory.Block(attributeLists, openBraceToken, statements, closeBraceToken);
        }

        public static EmptyStatementSyntax EmptyStatement(SyntaxToken semicolonToken = null)
        {
            Microsoft.CodeAnalysis.Syntax.InternalSyntax.SyntaxList<AttributeListSyntax> attributeLists = default;
            semicolonToken ??= SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken);

            int hash;
            var cached = SyntaxNodeCache.TryGetNode((int)SyntaxKind.EmptyStatement, attributeLists.Node, semicolonToken, out hash);
            if (cached != null) return (EmptyStatementSyntax)cached;

            var result = new EmptyStatementSyntax(SyntaxKind.EmptyStatement, attributeLists.Node, semicolonToken);
            if (hash >= 0)
            {
                SyntaxNodeCache.AddNode(result, hash);
            }

            return result;
        }

        public static AccessorListSyntax FakeAccessorList(bool getter = true, bool setter = true)
        {
            SyntaxList<AccessorDeclarationSyntax> accessors = null;

            if (getter && setter)
            {
                accessors = SyntaxFactory.List(
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, default, default, SyntaxFactory.FakeToken(SyntaxKind.GetKeyword), null, null, SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration, default, default, SyntaxFactory.FakeToken(SyntaxKind.SetKeyword), null, null, SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken))
                );
            }
            else if (getter)
            {
                accessors = SyntaxFactory.List(
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, default, default, SyntaxFactory.FakeToken(SyntaxKind.GetKeyword), null, null, SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken))
                );
            }
            else if(setter)
            {
                accessors = SyntaxFactory.List(
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration, default, default, SyntaxFactory.FakeToken(SyntaxKind.SetKeyword), null, null, SyntaxFactory.FakeToken(SyntaxKind.SemicolonToken))
                );
            }

            return AccessorList(FakeToken(SyntaxKind.OpenBraceToken), accessors, FakeToken(SyntaxKind.CloseBraceToken));
        }
    }
}
