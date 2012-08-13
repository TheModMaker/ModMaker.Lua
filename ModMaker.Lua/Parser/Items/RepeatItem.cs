using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser.Items
{
    class RepeatItem : IParseItem
    {
        Label? end;

        public RepeatItem()
        {
        }

        public ParseType Type { get { return ParseType.Statement; } }
        public IParseItem Block { get; set; }
        public IParseItem Exp { get; set; }

        public void GenerateILNew(ChunkBuilderNew eb)
        {
            if (end == null)
                end = eb.CurrentGenerator.DefineLabel();

            ILGenerator gen = eb.CurrentGenerator;
            Label start = gen.DefineLabel();
            gen.MarkLabel(start);
            Block.GenerateILNew(eb);
            Exp.GenerateILNew(eb);
            gen.Emit(OpCodes.Callvirt, typeof(RuntimeHelper).GetMethod("IsTrue"));
            gen.Emit(OpCodes.Brfalse, start);
            gen.MarkLabel(end.Value);
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to RepeatItem.");
        }
        public void WaitOne()
        {
            Block.WaitOne();
            if (Block is AsyncItem)
                Block = (Block as AsyncItem).Item;

            Exp.WaitOne();
            if (Exp is AsyncItem)
                Exp = (Exp as AsyncItem).Item;
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            var b = tree.StartBlock(true);
                end = cb.CurrentGenerator.DefineLabel();
                Block.ResolveLabels(cb, tree);
                Exp.ResolveLabels(cb, tree);
                tree.DefineLabel("<break>", end.Value);
            tree.EndBlock(b);
        }
        public bool HasNested()
        {
            return Block.HasNested() || Exp.HasNested();
        }
    }
}
