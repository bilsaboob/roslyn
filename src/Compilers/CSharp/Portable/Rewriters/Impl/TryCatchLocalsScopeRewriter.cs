using System;
using System.Collections.Immutable;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.CSharp.Rewriters
{
    internal class TryCatchLocalsScopeRewriter : TryCatchLocalsScopeRewriterBase<TryCatchLocalsScopeRewriter>
    {
        public TryCatchLocalsScopeRewriter()
        {
            // incrementally update blocks - we will rewrite both the outer block and the statement in one go
            IncrementalBlockUpdate = true;
        }

        public override BoundNode VisitTryStatement(BoundTryStatement node)
        {
            var tryStatementSyntax = node.Syntax as Syntax.TryStatementSyntax;
            if (tryStatementSyntax == null) return node;

            // we don't rewrite "inline try block" statements - "try/catch" that has been generated without the "try" keyword - such as inline in method body ...
            if (tryStatementSyntax.IsInlineBlockTryCatchStatement())
            {
                var methodDecl = tryStatementSyntax.Parent?.Parent as Syntax.MethodDeclarationSyntax;
                if (methodDecl != null) return node;
            }

            // ok - rewrite the locals to the outer scope/block
            var (tryStatement, outerBlock) = RewriteTryBlockLocals(node, CurrentBlock);

            // update the outer block
            CurrentBlock = outerBlock;

            // update the try statement
            return tryStatement;
        }
    }

    internal class TryCatchLocalsScopeRewriterBase<TRewriter> : BoundTreeMethodRewriter<TRewriter>
        where TRewriter : TryCatchLocalsScopeRewriterBase<TRewriter>, new()
    {
        protected (BoundTryStatement, BoundBlock) RewriteTryBlockLocals(BoundTryStatement tryStatement, BoundBlock outerBlock)
        {
            // only if there are any locals in the try block
            var tryBlock = tryStatement.TryBlock;

            // get the locals - we only need to rewrite if there are actual local in the "try" block
            LocalsDictionary innerLocals = null;
            if (!LocalsFinder.GetLocalsWithDeclarations(tryBlock, ref innerLocals)) return (tryStatement, outerBlock);

            ////////////////////////////////////////////////////////////////////////////////////////////////////////
            // TODO: should only do this if the locals are referenced from any of the catch/finally blocks
            ////////////////////////////////////////////////////////////////////////////////////////////////////////

            // the outer block may have it's own locals
            LocalsDictionary outerLocals = null;
            LocalsFinder.GetLocalsWithDeclarations(outerBlock, ref outerLocals, excludeNode: tryStatement);

            // build the new statements: try/catch statement + local declarations
            var newOuterStatements = ImmutableArray.CreateBuilder<BoundStatement>();

            //1. collect the locals in the "try block"
            //2. create declarations statements that will be put into the outer block - so that they are accessible in both "catch" and "finally" blocks
            //3. replace the locals declarations in the "try block" with "assignment statements" instead

            BoundTryStatement newTryStatement = null;
            var newLocals = new LocalsDictionary();

            // new locals start with the outer declared - possibly overwritten by the new inner locals
            newLocals.AppendLocals(outerLocals);

            foreach (var outerStatement in outerBlock.Statements)
            {
                if (outerStatement != tryStatement)
                {
                    // keep the outer statement as is

                    //just append the previous outer statement... we need to keep the statements
                    newOuterStatements.Add(outerStatement);
                }
                else
                {
                    // rewrite the try statement

                    // create declarations statements for each of the locals - and put into the outer block statements
                    var localsNeedingDecl = innerLocals.GetLocalsWithDeclByDeclOrder().Where(n => !outerLocals.ContainsLocal(n.Item1.Name, hasDecl: true));
                    var localsOuterDeclarationStatements = BuildLocalDeclStatements(localsNeedingDecl);
                    newOuterStatements.AddRange(localsOuterDeclarationStatements);

                    // update the new locals with this new one
                    foreach (var localDecl in localsOuterDeclarationStatements)
                    {
                        var localSymbol = localDecl.LocalSymbol;
                        newLocals.SetLocal(localSymbol.Name, localSymbol, new LocalDecl(localDecl));
                    }

                    // replace the old try block with the new try block
                    newTryStatement = RewriteTryStatement(tryStatement, innerLocals);
                    newOuterStatements.Add(newTryStatement);
                }
            }

            // now build the new outer block
            var newOuterBlock = outerBlock.Update(
                locals: newLocals.GetLocalsByDeclOrder().ToImmutableArray(),
                localFunctions: outerBlock.LocalFunctions,
                statements: newOuterStatements.ToImmutable()
            );

            return (newTryStatement, newOuterBlock);
        }

        private ImmutableArray<BoundLocalDeclaration> BuildLocalDeclStatements(IEnumerable<(LocalSymbol, LocalDecl)> locals)
        {
            var statements = ImmutableArray.CreateBuilder<BoundLocalDeclaration>();
            foreach (var l in locals)
            {
                var local = l.Item1;
                var decl = l.Item2;

                var newLocalInitializer = _F.Default(local.Type);
                var newLocalStatement = _F.LocalDeclaration(local, decl.DeclaredTypeOpt, newLocalInitializer, decl.ArgumentsOpt, decl.InferredType, decl.HasErrors, syntax: decl.Syntax);
                statements.Add(newLocalStatement);
            }
            return statements.ToImmutable();
        }

        private BoundTryStatement RewriteTryStatement(BoundTryStatement tryStatement, LocalsDictionary newLocals)
        {
            // rewrite the try block
            var newTryBlock = RewriteTryBlock(tryStatement.TryBlock, newLocals);

            // now we have the new try statement
            return tryStatement.Update(newTryBlock, tryStatement.CatchBlocks, tryStatement.FinallyBlockOpt, tryStatement.FinallyLabelOpt, tryStatement.PreferFaultHandler);
        }

        private BoundBlock RewriteTryBlock(BoundBlock tryBlock, LocalsDictionary newLocals)
        {
            // rewrite the statements in the try block
            // 1. replace declarations of the locals with "assignments"
            // 2. replace references to the locals with the new locals
            var newTryStatements = ImmutableArray.CreateBuilder<BoundStatement>();

            foreach (var stat in tryBlock.Statements)
            {
                if (stat is BoundLocalDeclaration localDecl)
                {
                    var local = localDecl.LocalSymbol;
                    if (string.IsNullOrEmpty(local?.Name))
                    {
                        newTryStatements.Add(stat);
                        continue;
                    }

                    // we need to use the new local when creating the assignment
                    if (!newLocals.TryGetLocal(local.Name, out var newLocal))
                    {
                        newTryStatements.Add(stat);
                        continue;
                    }

                    // if there is no initializer ... then we can just ignore the declaration
                    if (localDecl.InitializerOpt == null)
                    {
                        continue;
                    }

                    // replace with an assignment instead of a declaration
                    var assignLocal = _F.Local(newLocal, syntax: local?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax());
                    var assignStatement = _F.Assignment(assignLocal, localDecl.InitializerOpt, syntax: localDecl.Syntax);
                    newTryStatements.Add(assignStatement);
                }
                else
                {
                    // rewrite the locals in the statement
                    newTryStatements.Add(stat);
                }
            }

            // build the new try block - no locals should exist in it - and we should replace the statements with the new rewritten ones
            return tryBlock.Update(ImmutableArray<LocalSymbol>.Empty, tryBlock.LocalFunctions, newTryStatements.ToImmutableArray());
        }
    }
}
