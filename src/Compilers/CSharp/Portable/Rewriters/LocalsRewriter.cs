using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Rewriters
{
    internal sealed class LocalsDictionary
    {
        private Dictionary<string, Entry> _locals;

        internal LocalsDictionary()
        {
            _locals = new Dictionary<string, Entry>();
        }

        public int Count => _locals.Count;

        public IEnumerable<LocalSymbol> GetLocalsByDeclOrder() => _locals.Values.OrderBy(e => e.DeclOrder).Select(e => e.Local);
        public IEnumerable<(LocalSymbol, BoundLocalDeclaration)> GetLocalsWithDeclByDeclOrder() => _locals.Values.Where(e => e.Decl != null).OrderBy(e => e.DeclOrder).Select(e => (e.Local, e.Decl));

        public bool TryGetLocal(string name, out LocalSymbol local)
        {
            local = null;
            if (!_locals.TryGetValue(name, out var entry))
                return false;

            local = entry.Local;
            return true;
        }

        public void SetLocal(string name, LocalSymbol local, BoundLocalDeclaration decl = null)
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

        public bool TryGetDeclarataion(string name, out BoundLocalDeclaration decl)
        {
            decl = null;
            if (!_locals.TryGetValue(name, out var entry))
                return false;

            decl = entry.Decl;
            return true;
        }

        internal void SetLocalDeclaration(string name, BoundLocalDeclaration decl)
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

        public void AppendLocals(IEnumerable<LocalSymbol> otherLocals)
        {
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
            public BoundLocalDeclaration Decl { get; set; }
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

        private LocalsFinder(LocalsDictionary locals)
        {
            _locals = locals;
        }

        public static bool GetLocalsWithDeclarations(BoundBlock block, ref LocalsDictionary locals)
        {
            if (block?.Locals == null || block.Locals.Length == 0) return false;

            locals = new LocalsDictionary();
            FindLocals(block, locals);
            return locals.Count > 0;
        }

        public static void FindLocals(BoundNode node, LocalsDictionary locals)
        {
            var finder = new LocalsFinder(locals);
            finder.Visit(node);
        }

        public override BoundNode VisitLocalDeclaration(BoundLocalDeclaration node)
        {
            _locals.SetLocal(node.LocalSymbol?.Name, node.LocalSymbol, node);
            return base.VisitLocalDeclaration(node);
        }
    }
}
