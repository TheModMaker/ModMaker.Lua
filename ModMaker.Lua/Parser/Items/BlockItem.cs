using System;
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
        public void WaitOne()
        {
            if (Return != null)
            {
                Return.WaitOne();
                if (Return is AsyncItem)
                    Return = ((AsyncItem)Return).Item;
            }

            for (int i = 0; i < children.Count; i++)
            {
                children[i].WaitOne();
                if (children[i] is AsyncItem)
                    children[i] = ((AsyncItem)children[i]).Item;
            }
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            foreach (var item in children)
                item.ResolveLabels(cb, tree);

            if (Return != null)
                Return.ResolveLabels(cb, tree);
        }
        public void GenerateILNew(ChunkBuilderNew cb)
        {
        cb.StartBlock();
            foreach (IParseItem child in children)
                child.GenerateILNew(cb);

            if (Return != null)
            {
                Return.GenerateILNew(cb);

                cb.CurrentGenerator.Emit(OpCodes.Ret);
            }
        cb.EndBlock();
        }
        public bool HasNested()
        {
            bool b = false;
            foreach (var item in children)
                b = b || item.HasNested();

            return b || (Return != null && Return.HasNested());
        }
    }
}
