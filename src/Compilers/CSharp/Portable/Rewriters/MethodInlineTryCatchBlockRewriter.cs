using System;
using System.Collections.Immutable;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Rewriters
{
    internal sealed class MethodInlineTryCatchBlockRewriter : BoundTreeRewriterWithStackGuardWithoutRecursionOnTheLeftOfBinaryOperator
    {
        private readonly SyntheticBoundNodeFactory _F;
        private readonly DiagnosticBag _diagnostics;

        private MethodInlineTryCatchBlockRewriter(
            MethodSymbol containingMethod,
            NamedTypeSymbol containingType,
            SyntheticBoundNodeFactory factory,
            DiagnosticBag diagnostics)
        {
            _F = factory;
            _F.CurrentType = containingType;
            _F.CurrentFunction = containingMethod;
            _diagnostics = diagnostics;
        }

        public static BoundBlock Rewrite(
            MethodSymbol containingSymbol,
            NamedTypeSymbol containingType,
            BoundBlock block,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics)
        {
            Debug.Assert(containingSymbol != null);
            Debug.Assert((object)containingType != null);
            Debug.Assert(block != null);
            Debug.Assert(compilationState != null);
            Debug.Assert(diagnostics != null);

            var factory = new SyntheticBoundNodeFactory(containingSymbol, block.Syntax, compilationState, diagnostics);
            var rewriter = new MethodInlineTryCatchBlockRewriter(containingSymbol, containingType, factory, diagnostics);
            var newBlock = rewriter.Visit(block) as BoundBlock;
            return newBlock;
        }

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
            return RewriteTryBlockLocals(statement, node);
        }

        private BoundNode RewriteTryBlockLocals(BoundTryStatement tryStatement, BoundBlock outerBlock)
        {
            // only if there are any locals in the try block
            var tryBlock = tryStatement.TryBlock;

            // get the locals - we only need to rewrite if there are actual local in the "try" block
            LocalsDictionary locals = null;
            if (!LocalsFinder.GetLocalsWithDeclarations(tryBlock, ref locals)) return outerBlock;

            // the outer block may have it's own locals - those should be appended before
            locals.AppendLocals(outerBlock.Locals);

            // only if the locals are referenced from any of the catch/finally blocks

            // build the new statements: try/catch statement + local declarations
            var newOuterStatements = ImmutableArray.CreateBuilder<BoundStatement>();

            //1. collect the locals in the "try block"
            //2. create declarations statements that will be put into the outer block - so that they are accessible in both "catch" and "finally" blocks
            //3. replace the locals declarations in the "try block" with "assignment statements" instea

            // create declarations statements for each of the locals - and put into the outer block statements
            var localsOuterDeclarationStatements = BuildLocalDeclStatements(locals);
            newOuterStatements.AddRange(localsOuterDeclarationStatements);

            // replace the old try block with the new try block
            var newTryStatement = RewriteTryStatement(tryStatement, locals);
            newOuterStatements.Add(newTryStatement);

            // now build the new outer block
            var newOuterBlock = outerBlock.Update(
                locals: locals.GetLocalsByDeclOrder().ToImmutableArray(),
                localFunctions: outerBlock.LocalFunctions,
                statements: newOuterStatements.ToImmutable()
            );
            return newOuterBlock;
        }

        private ImmutableArray<BoundStatement> BuildLocalDeclStatements(LocalsDictionary locals)
        {
            var statements = ImmutableArray.CreateBuilder<BoundStatement>();
            foreach (var l in locals.GetLocalsWithDeclByDeclOrder())
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
