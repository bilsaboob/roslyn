﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Collections;
using Roslyn.Utilities;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.CodeAnalysis.Emit
{
    internal struct AnonymousTypeKeyField : IEquatable<AnonymousTypeKeyField>
    {
        internal readonly string Name;
        internal readonly bool IsKey;

        internal AnonymousTypeKeyField(string name, bool isKey)
        {
            this.Name = name;
            this.IsKey = isKey;
        }

        public bool Equals(AnonymousTypeKeyField other)
        {
            return (this.Name == other.Name) && (this.IsKey == other.IsKey);
        }

        public override bool Equals(object obj)
        {
            return this.Equals((AnonymousTypeKeyField)obj);
        }

        public override int GetHashCode()
        {
            return Hash.Combine(this.Name.GetHashCode(), this.IsKey.GetHashCode());
        }

        public override string ToString()
        {
            return this.Name + (this.IsKey ? "+" : "-");
        }
    }

    [DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
    internal struct AnonymousTypeKey : IEquatable<AnonymousTypeKey>
    {
        public readonly bool IsDelegate;
        public readonly ImmutableArray<AnonymousTypeKeyField> Fields;

        public AnonymousTypeKey(ImmutableArray<string> names)
        {
            throw new NotImplementedException();
        }

        public AnonymousTypeKey(ImmutableArray<AnonymousTypeKeyField> fields, bool isDelegate = false)
        {
            this.IsDelegate = isDelegate;
            this.Fields = fields;
        }

        public bool Equals(AnonymousTypeKey other)
        {
            return (this.IsDelegate == other.IsDelegate) && this.Fields.SequenceEqual(other.Fields);
        }

        public override bool Equals(object obj)
        {
            return this.Equals((AnonymousTypeKey)obj);
        }

        public override int GetHashCode()
        {
            return Hash.Combine(this.IsDelegate.GetHashCode(), Hash.CombineValues(this.Fields));
        }

        private string GetDebuggerDisplay()
        {
            var pooledBuilder = PooledStringBuilder.GetInstance();
            var builder = pooledBuilder.Builder;
            for (int i = 0; i < this.Fields.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("|");
                }
                builder.Append(this.Fields[i]);
            }
            return pooledBuilder.ToStringAndFree();
        }
    }
}
