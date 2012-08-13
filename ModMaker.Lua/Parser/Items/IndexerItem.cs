﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;
using System.Reflection;

namespace ModMaker.Lua.Parser.Items
{
    class IndexerItem : IParseItem
    {
        public IndexerItem(IParseItem prefix, IParseItem exp)
        {
            if (!prefix.Type.HasFlag(ParseType.PrefixExp))
                throw new ArgumentException("The prefix of an indexer must be a PrefixExp.");
            if (!exp.Type.HasFlag(ParseType.Expression))
                throw new ArgumentException("The expression of an indexer must be an Expression.");

            this.Prefix = prefix;
            this.Expression = exp;
        }
        public IndexerItem(IParseItem prefix, string name)
        {
            if (!prefix.Type.HasFlag(ParseType.PrefixExp))
                throw new ArgumentException("The prefix of an indexer must be a PrefixExp.");

            this.Prefix = prefix;
            this.Expression = new LiteralItem(name);
        }

        public IParseItem Prefix { get; private set; }
        public IParseItem Expression { get; private set; }
        public ParseType Type { get { return ParseType.Expression | ParseType.PrefixExp | ParseType.Variable; } }
        public bool Set { get; set; }

        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to IndexerItem.");
        }
        public void GenerateILNew(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            eb.PushEnv();
            Prefix.GenerateILNew(eb);
            Expression.GenerateILNew(eb);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("Indexer"));
            if (Set)
            {
                LocalBuilder loc = gen.DeclareLocal(typeof(object));
                gen.Emit(OpCodes.Stloc, loc);
                gen.Emit(OpCodes.Ldloca, loc);
            }
        }
        public void WaitOne()
        {
            Prefix.WaitOne();
            if (Prefix is AsyncItem)
               Prefix = ((AsyncItem)Prefix).Item;
            Expression.WaitOne();
            if (Expression is AsyncItem)
                Expression = ((AsyncItem)Expression).Item;
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            Prefix.ResolveLabels(cb, tree);
            Expression.ResolveLabels(cb, tree);
        }
        public bool HasNested()
        {
            return Prefix.HasNested() || Expression.HasNested();
        }
    }
}