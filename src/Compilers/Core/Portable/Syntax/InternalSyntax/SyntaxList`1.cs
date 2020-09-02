// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Syntax.InternalSyntax
{
    internal partial struct SyntaxList<TNode> : IEquatable<SyntaxList<TNode>>
        where TNode : GreenNode
    {
        private readonly GreenNode? _node;

        internal SyntaxList(GreenNode? node)
        {
            _node = node;
        }

        internal GreenNode? Node => _node;

        public int Count
        {
            get
            {
                return _node == null ? 0 : (_node.IsList ? _node.SlotCount : 1);
            }
        }

        public TNode? this[int index]
        {
            get
            {
                if (_node == null)
                {
                    return null;
                }
                else if (_node.IsList)
                {
                    Debug.Assert(index >= 0);
                    Debug.Assert(index <= _node.SlotCount);

                    return (TNode?)_node.GetSlot(index);
                }
                else if (index == 0)
                {
                    return (TNode?)_node;
                }
                else
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }
        }

        internal TNode GetRequiredItem(int index)
        {
            var node = this[index];
            RoslynDebug.Assert(node is object);
            return node;
        }

        internal GreenNode? ItemUntyped(int index)
        {
            RoslynDebug.Assert(_node is object);
            var node = this._node;
            if (node.IsList)
            {
                return node.GetSlot(index);
            }

            Debug.Assert(index == 0);
            return node;
        }

        public bool Any()
        {
            return _node != null;
        }

        public bool Any(int kind)
        {
            foreach (var element in this)
            {
                if (element.RawKind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        public TNode? LastOrDefault(Func<TNode?, bool> predicate)
        {
            TNode? last = null;
            foreach (var element in this)
            {
                if (predicate(element))
                    last = element;
            }

            return last;
        }

        public int? GetFullWidth(Func<TNode?, bool> resetPredicate)
        {
            int? width = null;

            foreach (var element in this)
            {
                width = width ?? 0;
                width += element.FullWidth;

                if (resetPredicate?.Invoke(element) == true)
                {
                    width = 0;
                }
            }

            return width;
        }

        public int? GetFullWidthAfterLast(Func<TNode?, bool> predicate)
        {
            int? width = null;
            var appendWidth = false;

            foreach (var element in this)
            {
                if (appendWidth)
                    width += element.FullWidth;

                if (predicate(element))
                {
                    appendWidth = true;
                    width = 0;
                }
            }

            return width;
        }

        public int? GetFullWidthAfterFirst(Func<TNode?, bool> predicate)
        {
            int? width = null;
            var appendWidth = false;

            foreach (var element in this)
            {
                if (appendWidth)
                    width = width + element.FullWidth;

                if (predicate(element))
                {
                    appendWidth = true;
                    width = width ?? 0;
                }
            }

            return width;
        }

        public int GetFullWidthAfter(TNode node)
        {
            int width = 0;
            var appendWidth = false;

            foreach (var element in this)
            {
                if (appendWidth)
                {
                    width += element.FullWidth;
                }

                if (node == element)
                {
                    appendWidth = true;
                }
            }

            return width;
        }

        public int GetFullWidthBefore(TNode node)
        {
            int width = 0;

            foreach (var element in this)
            {
                if (node == element) break;

                width += element.FullWidth;
            }

            return width;
        }

        public TNode? FirstOrDefault(Func<TNode?, bool> predicate)
        {
            foreach (var element in this)
            {
                if (predicate(element)) return element;
            }

            return null;
        }

        internal TNode[] Nodes
        {
            get
            {
                var arr = new TNode[this.Count];
                for (int i = 0; i < this.Count; i++)
                {
                    arr[i] = GetRequiredItem(i);
                }
                return arr;
            }
        }

        public TNode? Last
        {
            get
            {
                RoslynDebug.Assert(_node is object);
                var node = this._node;
                if (node.IsList)
                {
                    return (TNode?)node.GetSlot(node.SlotCount - 1);
                }

                return (TNode?)node;
            }
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        internal void CopyTo(int offset, ArrayElement<GreenNode>[] array, int arrayOffset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                array[arrayOffset + i].Value = GetRequiredItem(i + offset);
            }
        }

        public static bool operator ==(SyntaxList<TNode> left, SyntaxList<TNode> right)
        {
            return left._node == right._node;
        }

        public static bool operator !=(SyntaxList<TNode> left, SyntaxList<TNode> right)
        {
            return left._node != right._node;
        }

        public bool Equals(SyntaxList<TNode> other)
        {
            return _node == other._node;
        }

        public override bool Equals(object? obj)
        {
            return (obj is SyntaxList<TNode>) && Equals((SyntaxList<TNode>)obj);
        }

        public override int GetHashCode()
        {
            return _node != null ? _node.GetHashCode() : 0;
        }

        public SeparatedSyntaxList<TOther> AsSeparatedList<TOther>() where TOther : GreenNode
        {
            return new SeparatedSyntaxList<TOther>(this);
        }

        public static implicit operator SyntaxList<TNode>(TNode node)
        {
            return new SyntaxList<TNode>(node);
        }

        public static implicit operator SyntaxList<TNode>(SyntaxList<GreenNode> nodes)
        {
            return new SyntaxList<TNode>(nodes._node);
        }

        public static implicit operator SyntaxList<GreenNode>(SyntaxList<TNode> nodes)
        {
            return new SyntaxList<GreenNode>(nodes.Node);
        }
    }
}
