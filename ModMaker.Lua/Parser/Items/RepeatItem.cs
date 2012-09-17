<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser
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

        public void GenerateIL(ChunkBuilderNew eb)
        {
            if (end == null)
                end = eb.CurrentGenerator.DefineLabel();

            ILGenerator gen = eb.CurrentGenerator;
            Label start = gen.DefineLabel();

            // start:
            gen.MarkLabel(start);

            // {Block}
            Block.GenerateIL(eb);

            // if (!RuntimeHelper.IsTrue({Exp}) goto start;
            Exp.GenerateIL(eb);
            gen.Emit(OpCodes.Callvirt, typeof(RuntimeHelper).GetMethod("IsTrue"));
            gen.Emit(OpCodes.Brfalse, start);

            // end:
            gen.MarkLabel(end.Value);
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to RepeatItem.");
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
    class RepeatItem : IParseItem
    {
        Label? end;

        public RepeatItem()
        {
        }

        public ParseType Type { get { return ParseType.Statement; } }
        public IParseItem Block { get; set; }
        public IParseItem Exp { get; set; }

        public void GenerateIL(ChunkBuilderNew eb)
        {
            if (end == null)
                end = eb.CurrentGenerator.DefineLabel();

            ILGenerator gen = eb.CurrentGenerator;
            Label start = gen.DefineLabel();

            // start:
            gen.MarkLabel(start);

            // {Block}
            Block.GenerateIL(eb);

            // if (!RuntimeHelper.IsTrue({Exp}) goto start;
            Exp.GenerateIL(eb);
            gen.Emit(OpCodes.Callvirt, typeof(RuntimeHelper).GetMethod("IsTrue"));
            gen.Emit(OpCodes.Brfalse, start);

            // end:
            gen.MarkLabel(end.Value);
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to RepeatItem.");
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
    }
}
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
