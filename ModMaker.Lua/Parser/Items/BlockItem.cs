<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser
{
    class BlockItem : IParseItem
    {
        List<IParseItem> children;

        public BlockItem()
        {
            this.children = new List<IParseItem>();
        }

        public IParseItem Return { get; private set; }
        public ParseType Type { get { return ParseType.Block | ParseType.Statement; } }
        public bool Root { get; set; }

        public void AddItem(IParseItem child)
        {
            if (!child.Type.HasFlag(ParseType.Statement))
            {
                if (child is ReturnItem)
                {
                    if (Return == null)
                        Return = child;
                    return;
                }
                throw new ArgumentException("The child of a block must be a Statement.");
            }

            this.children.Add(child);
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            foreach (var item in children)
                item.ResolveLabels(cb, tree);

            if (Return != null)
                Return.ResolveLabels(cb, tree);
        }
        public void GenerateIL(ChunkBuilderNew cb)
        {
        cb.StartBlock();
            foreach (IParseItem child in children)
                child.GenerateIL(cb);

            if (Return != null)
            {
                // return {Return};
                Return.GenerateIL(cb);
                cb.CurrentGenerator.Emit(OpCodes.Ret);
            }
        cb.EndBlock();
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser.Items
{
    class BlockItem : IParseItem
    {
        List<IParseItem> children;

        public BlockItem()
        {
            this.children = new List<IParseItem>();
        }

        public IParseItem Return { get; private set; }
        public ParseType Type { get { return ParseType.Block | ParseType.Statement; } }
        public bool Root { get; set; }

        public void AddItem(IParseItem child)
        {
            if (!child.Type.HasFlag(ParseType.Statement))
            {
                if (child is ReturnItem)
                {
                    if (Return == null)
                        Return = child;
                    return;
                }
                throw new ArgumentException("The child of a block must be a Statement.");
            }

            this.children.Add(child);
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            foreach (var item in children)
                item.ResolveLabels(cb, tree);

            if (Return != null)
                Return.ResolveLabels(cb, tree);
        }
        public void GenerateIL(ChunkBuilderNew cb)
        {
        cb.StartBlock();
            foreach (IParseItem child in children)
                child.GenerateIL(cb);

            if (Return != null)
            {
                // return {Return};
                Return.GenerateIL(cb);
                cb.CurrentGenerator.Emit(OpCodes.Ret);
            }
        cb.EndBlock();
        }
    }
}
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
