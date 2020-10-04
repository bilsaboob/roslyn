using System;
using System.Collections.Immutable;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Rewriters
{
    internal sealed class MethodInlineTryCatchBlockRewriter : TryCatchLocalsScopeRewriterBase<MethodInlineTryCatchBlockRewriter>
    {
        public override BoundNode VisitBlock(BoundBlock node)
        {
            // only the first visited block can be the target of the rewrite - it's only allowed on method level!
            if (node.Statements.Length != 1) return node;

            // first statement must be another block
            var statement = node.Statements[0] as BoundTryStatement;
            if (statement == null) return node;

            // handle special case of Method declaration with an appended catch / finally - it's always a wrapped try/catch with a missing "try" keyword
            var tryStatementSyntax = statement.Syntax as Syntax.TryStatementSyntax;
            if (tryStatementSyntax == null || !tryStatementSyntax.IsInlineBlockTryCatchStatement()) return node;

            var methodDecl = tryStatementSyntax.Parent?.Parent as Syntax.MethodDeclarationSyntax;
            if (methodDecl == null) return node;

            // OK, we can rewrite the locals
            var (tryStatement, outerBlock) = RewriteTryBlockLocals(statement, node);
            return outerBlock;
        }
    }
}
