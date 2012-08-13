using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser.Items
{
    class WhileItem : IParseItem
    {
        Label? end;

        public WhileItem()
        {
            this.end = null;
        }

        public IParseItem Exp { get; set; }
        public IParseItem Block { get; set; }
        public ParseType Type { get { return ParseType.Statement; } }

        public void GenerateILNew(ChunkBuilderNew eb)
        {
            if (end == null)
                end = eb.CurrentGenerator.DefineLabel();

            ILGenerator gen = eb.CurrentGenerator;
            Label start = gen.DefineLabel();
            gen.MarkLabel(start);
                Exp.GenerateILNew(eb);
                gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("IsTrue"));
                gen.Emit(OpCodes.Brfalse, end.Value);
            
                Block.GenerateILNew(eb);
                gen.Emit(OpCodes.Br, start);
            gen.MarkLabel(end.Value);
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to WhileItem.");
        }
        public void WaitOne()
        {
            Exp.WaitOne();
            if (Exp is AsyncItem)
                Exp = (Exp as AsyncItem).Item;

            Block.WaitOne();
            if (Block is AsyncItem)
                Block = (Block as AsyncItem).Item;
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            var b = tree.StartBlock(true);
                Block.ResolveLabels(cb, tree);
                Exp.ResolveLabels(cb, tree);
                end = cb.CurrentGenerator.DefineLabel();
                tree.DefineLabel("<break>", end.Value);
            tree.EndBlock(b);
        }
        public bool HasNested()
        {
            return Exp.HasNested() || Block.HasNested();
        }
    }
}
