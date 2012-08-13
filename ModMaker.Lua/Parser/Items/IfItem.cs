using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser.Items
{
    class IfItem : IParseItem
    {
        List<Tuple<IParseItem, IParseItem>> _elses = new List<Tuple<IParseItem, IParseItem>>();

        public IfItem()
        {
        }

        public IParseItem Exp { get; set; }
        public IParseItem Block { get; set; }
        public IParseItem ElseBlock { get; set; }
        public ParseType Type { get { return ParseType.Statement; } }

        public void GenerateILNew(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            Label next = gen.DefineLabel(), end = gen.DefineLabel();

            Exp.GenerateILNew(eb);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("IsTrue"));
            gen.Emit(OpCodes.Brfalse, next);
            Block.GenerateILNew(eb);
            gen.Emit(OpCodes.Br, end);
            gen.MarkLabel(next);
            foreach (var item in _elses)
            {
                next = gen.DefineLabel();
                item.Item1.GenerateILNew(eb);
                gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("IsTrue"));
                gen.Emit(OpCodes.Brfalse, next);
                item.Item2.GenerateILNew(eb);
                gen.Emit(OpCodes.Br, end);
                gen.MarkLabel(next);
            }
            if (ElseBlock != null)
                ElseBlock.GenerateILNew(eb);
            gen.MarkLabel(end);
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to IfItem.");
        }
        public void AddItem(IParseItem exp, IParseItem block)
        {
            _elses.Add(new Tuple<IParseItem, IParseItem>(exp, block));
        }
        public void WaitOne()
        {
            Block.WaitOne();
            if (Block is AsyncItem)
                Block = (Block as AsyncItem).Item;
            if (ElseBlock != null)
            {
                ElseBlock.WaitOne();
                if (ElseBlock is AsyncItem)
                    ElseBlock = (ElseBlock as AsyncItem).Item;
            }

            Exp.WaitOne();
            if (Exp is AsyncItem)
                Exp = (Exp as AsyncItem).Item;

            for (int i = 0; i < _elses.Count; i++ )
            {
                IParseItem i1, i2;
                _elses[i].Item1.WaitOne();
                if (_elses[i].Item1 is AsyncItem)
                    i1 = (_elses[i].Item1 as AsyncItem).Item;
                else
                    i1 = _elses[i].Item1;

                _elses[i].Item2.WaitOne();
                if (_elses[i].Item2 is AsyncItem)
                    i2 = (_elses[i].Item2 as AsyncItem).Item;
                else
                    i2 = _elses[i].Item2;

                _elses[i] = new Tuple<IParseItem, IParseItem>(i1, i2);
            }
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            Exp.ResolveLabels(cb, tree);

            var b = tree.StartBlock(true);
                Block.ResolveLabels(cb, tree);
            tree.EndBlock(b);

            for (int i = 0; i < _elses.Count; i++)
            {
                b = tree.StartBlock(true);
                    _elses[i].Item2.ResolveLabels(cb, tree);
                tree.EndBlock(b);
            }

            if (ElseBlock != null)
            {
                b = tree.StartBlock(true);
                    ElseBlock.ResolveLabels(cb, tree);
                tree.EndBlock(b);
            }
        }
        public bool HasNested()
        {
            bool b = false;
            foreach (var item in _elses)
                b = b || item.Item1.HasNested() || item.Item2.HasNested();

            return b || Exp.HasNested() || Block.HasNested() || (ElseBlock != null && ElseBlock.HasNested());
        }
    }
}
