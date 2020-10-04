using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CSharp.Rewriters
{
    internal class LocalDecl
    {
        public LocalDecl(BoundAssignmentOperator assign, BoundLocal local)
        {
            Syntax = assign.Syntax;
            LocalSymbol = local?.LocalSymbol;
            HasErrors = assign.HasErrors;
            DeclaredTypeOpt = null;
            InitializerOpt = assign.Right;
            ArgumentsOpt = ImmutableArray<BoundExpression>.Empty;
            InferredType = false;
        }

        public LocalDecl(BoundLocalDeclaration decl)
        {
            Syntax = decl.Syntax;
            LocalSymbol = decl.LocalSymbol;
            HasErrors = decl.HasErrors;
            DeclaredTypeOpt = decl.DeclaredTypeOpt;
            InitializerOpt = decl.InitializerOpt;
            ArgumentsOpt = decl.ArgumentsOpt;
            InferredType = decl.InferredType;
        }

        public SyntaxNode Syntax { get; set; }

        public bool HasErrors { get; set; }

        public LocalSymbol LocalSymbol { get; set; }

        public BoundTypeExpression? DeclaredTypeOpt { get; set; }

        public BoundExpression? InitializerOpt { get; set; }

        public ImmutableArray<BoundExpression> ArgumentsOpt { get; set; }

        public bool InferredType { get; set; }
    }

    internal sealed class LocalsDictionary
    {
        private Dictionary<string, Entry> _locals;

        internal LocalsDictionary()
        {
            _locals = new Dictionary<string, Entry>();
        }

        internal LocalsDictionary(IEnumerable<LocalSymbol> locals)
            : this()
        {
            AppendLocals(locals);
        }

        public int Count => _locals.Count;

        public IEnumerable<LocalSymbol> GetLocalsByDeclOrder() => _locals.Values.OrderBy(e => e.DeclOrder).Select(e => e.Local);
        public IEnumerable<(LocalSymbol, LocalDecl)> GetLocalsWithDeclByDeclOrder() => _locals.Values.Where(e => e.Decl != null).OrderBy(e => e.DeclOrder).Select(e => (e.Local, e.Decl));

        public bool TryGetLocal(string name, out LocalSymbol local)
        {
            local = null;
            if (!_locals.TryGetValue(name, out var entry))
                return false;

            local = entry.Local;
            return true;
        }

        public void SetLocal(string name, LocalSymbol local, LocalDecl decl = null)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (_locals.TryGetValue(name, out var entry))
            {
                entry.Local = local;
                if (decl != null) entry.Decl = decl;
                return;
            }

            var declOrder = _locals.Values.Count;
            _locals[name] = new Entry()
            {
                DeclOrder = declOrder,
                Name = name,
                Local = local,
                Decl = decl
            };
        }

        public bool ContainsLocal(string name, bool hasDecl = false)
        {
            if (!_locals.TryGetValue(name, out var entry))
                return false;

            if (!hasDecl) return true;

            return entry.Decl != null;
        }

        public bool TryGetDeclarataion(string name, out LocalDecl decl)
        {
            decl = null;
            if (!_locals.TryGetValue(name, out var entry))
                return false;

            decl = entry.Decl;
            return true;
        }

        internal void SetLocalDeclaration(string name, LocalDecl decl)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (_locals.TryGetValue(name, out var entry))
            {
                entry.Decl = decl;
                return;
            }

            var declOrder = _locals.Values.Count;
            _locals[name] = new Entry()
            {
                DeclOrder = declOrder,
                Name = name,
                Decl = decl
            };
        }

        public void AppendLocals(LocalsDictionary otherLocals)
        {
            if (otherLocals == null) return;

            foreach(var e in otherLocals._locals)
                _locals[e.Key] = e.Value;
        }

        public void AppendLocals(IEnumerable<LocalSymbol> otherLocals)
        {
            if (otherLocals == null) return;

            foreach (var otherLocal in otherLocals)
            {
                SetLocal(otherLocal.Name, otherLocal);
            }
        }

        class Entry
        {
            public int DeclOrder { get; set; }
            public string Name { get; set; }
            public LocalSymbol Local { get; set; }
            public LocalDecl Decl { get; set; }
        }
    }

    internal sealed class LocalsRewriter : BoundTreeRewriterWithStackGuard
    {
        private LocalsDictionary _newLocals;

        internal LocalsRewriter(LocalsDictionary newLocals)
        {
            _newLocals = newLocals;
        }

        public static BoundBlock RewriteBlockLocals(BoundBlock block, LocalsDictionary newLocals)
        {
            var newStatements = ImmutableArray.CreateBuilder<BoundStatement>();
            foreach (var stat in block.Statements)
            {
                // rewrite the locals in the statement
                var newStatement = LocalsRewriter.RewriteLocals(stat, newLocals);
                newStatements.Add(newStatement);
            }

            return block.Update(block.Locals, block.LocalFunctions, newStatements.ToImmutableArray());
        }

        public static TBoundNode RewriteLocals<TBoundNode>(TBoundNode node, LocalsDictionary newLocals)
            where TBoundNode : BoundNode
        {
            if (node == null) return node;
            var rewriter = new LocalsRewriter(newLocals);
            return rewriter.Visit(node) as TBoundNode;
        }

        public override BoundNode VisitLocal(BoundLocal node)
        {
            var name = node.LocalSymbol.Name;
            if (string.IsNullOrEmpty(name)) return node;

            if (!_newLocals.TryGetLocal(name, out var newLocal)) return node;

            return node.Update(newLocal, node.ConstantValueOpt, newLocal.Type);
        }
    }

    internal sealed class LocalsFinder : BoundTreeWalkerWithStackGuardWithoutRecursionOnTheLeftOfBinaryOperator
    {
        private LocalsDictionary _locals;
        private BoundNode _excludeNode;

        private LocalsFinder(LocalsDictionary locals, BoundNode excludeNode = null)
        {
            _locals = locals;
            _excludeNode = excludeNode;
        }

        public static bool GetLocalsWithDeclarations(BoundBlock block, ref LocalsDictionary locals, BoundNode excludeNode = null)
        {
            if (block == null) return false;

            locals = new LocalsDictionary();
            FindLocals(block, locals, excludeNode);
            return locals.Count > 0;
        }

        public static void FindLocals(BoundNode node, LocalsDictionary locals, BoundNode excludeNode = null)
        {
            var finder = new LocalsFinder(locals, excludeNode);
            finder.Visit(node);
        }

        public override BoundNode Visit(BoundNode node)
        {
            if (_excludeNode == node) return node;

            return base.Visit(node);
        }

        public override BoundNode VisitBlock(BoundBlock node)
        {
            if (_excludeNode == node) return node;

            return base.VisitBlock(node);
        }

        public override BoundNode VisitLocalDeclaration(BoundLocalDeclaration node)
        {
            _locals.SetLocal(node.LocalSymbol?.Name, node.LocalSymbol, new LocalDecl(node));
            return base.VisitLocalDeclaration(node);
        }

        public override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node)
        {
            if (node.Syntax is AssignmentExpressionSyntax assignExpr)
            {
                if (assignExpr.OperatorToken.Kind() == SyntaxKind.ColonEqualsToken && node.Left is BoundLocal boundLocal)
                {
                    // it's a declaration expression
                    _locals.SetLocal(boundLocal.LocalSymbol?.Name, boundLocal.LocalSymbol, new LocalDecl(node, boundLocal));
                    return node;
                }
            }

            return base.VisitAssignmentOperator(node);
        }
    }
}
