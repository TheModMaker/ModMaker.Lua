<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser
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

        public void GenerateIL(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            Label next = gen.DefineLabel(), end = gen.DefineLabel();

            // if (!RuntimeHelper.IsTrue({Exp}) goto next;
            Exp.GenerateIL(eb);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("IsTrue"));
            gen.Emit(OpCodes.Brfalse, next);

            // {Block}
            Block.GenerateIL(eb);

            // goto end;
            gen.Emit(OpCodes.Br, end);

            // next:
            gen.MarkLabel(next);
            foreach (var item in _elses)
            {
                // if (!RuntimeHelper.IsTrue({item.Item1}) goto next;
                next = gen.DefineLabel();
                item.Item1.GenerateIL(eb);
                gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("IsTrue"));
                gen.Emit(OpCodes.Brfalse, next);

                // {item.Item2}
                item.Item2.GenerateIL(eb);

                // goto end;
                gen.Emit(OpCodes.Br, end);

                // next:
                gen.MarkLabel(next);
            }
            if (ElseBlock != null)
                ElseBlock.GenerateIL(eb);

            // end:
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

        public void GenerateIL(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            Label next = gen.DefineLabel(), end = gen.DefineLabel();

            // if (!RuntimeHelper.IsTrue({Exp}) goto next;
            Exp.GenerateIL(eb);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("IsTrue"));
            gen.Emit(OpCodes.Brfalse, next);

            // {Block}
            Block.GenerateIL(eb);

            // goto end;
            gen.Emit(OpCodes.Br, end);

            // next:
            gen.MarkLabel(next);
            foreach (var item in _elses)
            {
                // if (!RuntimeHelper.IsTrue({item.Item1}) goto next;
                next = gen.DefineLabel();
                item.Item1.GenerateIL(eb);
                gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("IsTrue"));
                gen.Emit(OpCodes.Brfalse, next);

                // {item.Item2}
                item.Item2.GenerateIL(eb);

                // goto end;
                gen.Emit(OpCodes.Br, end);

                // next:
                gen.MarkLabel(next);
            }
            if (ElseBlock != null)
                ElseBlock.GenerateIL(eb);

            // end:
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
    }
}
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
