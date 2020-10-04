using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System;

namespace Microsoft.CodeAnalysis.CSharp.Rewriters
{
    internal abstract class BoundTreeRewriter : BoundTreeRewriterWithStackGuard
    {
        protected BoundTreeRewriter()
        {
            VisitBinaryRecursive = false;
        }

        #region Block
        protected BoundBlock PrevBlock { get; set; }
        protected BoundBlock CurrentBlock { get; set; }
        protected bool IncrementalBlockUpdate { get; set; }

        public override BoundNode VisitBlock(BoundBlock node)
        {
            var prevBlock = PrevBlock;
            var currentBlock = CurrentBlock;

            // set the new current and prev
            PrevBlock = currentBlock;
            CurrentBlock = node;

            try
            {
                if (!IncrementalBlockUpdate) return base.VisitBlock(node);
                return VisitBlockIncrementally(node);
            }
            finally
            {
                PrevBlock = prevBlock;
                CurrentBlock = currentBlock;
            }
        }

        private BoundBlock VisitBlockIncrementally(BoundBlock node)
        {
            // must have statements to continue
            var statements = node.Statements;
            if (statements.IsDefault || statements.Length == 0)
                return node;

            var newBlock = CurrentBlock;
            var newStatements = ArrayBuilder<BoundStatement>.GetInstance(statements.Length);

            for (int i = 0; i < statements.Length; i++)
            {
                var statement = statements[i];

                // visit and create the new statement
                var newStatement = (BoundStatement)Visit(statement);

                // if the "CurrentBlock" was upated during the visit for some reason, we will use the updated one
                if (newBlock != CurrentBlock)
                {
                    newBlock = CurrentBlock;
                    var newBlockStatements = newBlock.Statements;

                    // check if we have another amount of statements ... we need to adjust the loop with this in mind
                    var diff = newBlockStatements.Length - statements.Length;
                    i += diff;

                    statements = newBlockStatements;

                    // clear and replace the statements
                    newStatements.Clear();
                    newStatements.AddRange(newBlockStatements, i);
                }

                newStatements.Add(newStatement);

                // update the block between every statement if specified
                newBlock = newBlock.Update(node.Locals, node.LocalFunctions, newStatements.ToImmutable());
            }

            // release the builder
            newStatements.Free();
            return newBlock;
        }
        #endregion

        #region Binary expressions
        protected bool VisitBinaryRecursive { get; set; }

        public sealed override BoundNode VisitBinaryOperator(BoundBinaryOperator node)
        {
            if (!VisitBinaryRecursive) return VisitBinaryOperatorNonRecursive(node);
            return base.VisitBinaryOperator(node);
        }

        private BoundNode VisitBinaryOperatorNonRecursive(BoundBinaryOperator node)
        {
            BoundExpression child = node.Left;

            if (child.Kind != BoundKind.BinaryOperator)
            {
                return base.VisitBinaryOperator(node);
            }

            var stack = ArrayBuilder<BoundBinaryOperator>.GetInstance();
            stack.Push(node);

            BoundBinaryOperator binary = (BoundBinaryOperator)child;

            while (true)
            {
                stack.Push(binary);
                child = binary.Left;

                if (child.Kind != BoundKind.BinaryOperator)
                {
                    break;
                }

                binary = (BoundBinaryOperator)child;
            }

            var left = (BoundExpression?)this.Visit(child);
            Debug.Assert(left is { });

            do
            {
                binary = stack.Pop();
                var right = (BoundExpression?)this.Visit(binary.Right);
                Debug.Assert(right is { });
                var type = this.VisitType(binary.Type);
                left = binary.Update(binary.OperatorKind, binary.ConstantValueOpt, binary.MethodOpt, binary.ResultKind, binary.OriginalUserDefinedOperatorsOpt, left, right, type);
            }
            while (stack.Count > 0);

            Debug.Assert((object)binary == node);
            stack.Free();

            return left;
        }
        #endregion
    }

    internal abstract class BoundTreeMethodRewriter : BoundTreeRewriter
    {
        protected SyntheticBoundNodeFactory _F;
        protected DiagnosticBag _diagnostics;

        protected virtual void _ctor(
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

        public SyntheticBoundNodeFactory NodeFactory => _F;
    }

    internal abstract class BoundTreeMethodRewriter<TRewriter> : BoundTreeMethodRewriter
        where TRewriter : BoundTreeMethodRewriter<TRewriter>, new()
    {
        public static TNode Rewrite<TNode>(
            TNode node,
            MethodSymbol containingSymbol,
            NamedTypeSymbol containingType,
            TypeCompilationState compilationState,
            DiagnosticBag diagnostics)
            where TNode : BoundNode
        {
            // prepare the factory
            var factory = new SyntheticBoundNodeFactory(containingSymbol, node.Syntax, compilationState, diagnostics);

            // create and init the rewriter
            var rewriter = new TRewriter();
            rewriter._ctor(containingSymbol, containingType, factory, diagnostics);

            // apply the rewrite
            var newNode = rewriter.Visit(node) as TNode;
            return newNode;
        }

        protected BoundTreeMethodRewriter() { }
    }
}
