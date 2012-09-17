<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser
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

        public void GenerateIL(ChunkBuilderNew eb)
        {
            if (end == null)
                end = eb.CurrentGenerator.DefineLabel();

            ILGenerator gen = eb.CurrentGenerator;
            Label start = gen.DefineLabel();
                
            // start:
            gen.MarkLabel(start);

            // if (!RuntimeHelper.IsTrue({Exp}) goto end;
            Exp.GenerateIL(eb);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("IsTrue"));
            gen.Emit(OpCodes.Brfalse, end.Value);
                
            // {Block}
            Block.GenerateIL(eb);

            // goto start;
            gen.Emit(OpCodes.Br, start);

            // end:
            gen.MarkLabel(end.Value);
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to WhileItem.");
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

        public void GenerateIL(ChunkBuilderNew eb)
        {
            if (end == null)
                end = eb.CurrentGenerator.DefineLabel();

            ILGenerator gen = eb.CurrentGenerator;
            Label start = gen.DefineLabel();
                
            // start:
            gen.MarkLabel(start);

            // if (!RuntimeHelper.IsTrue({Exp}) goto end;
            Exp.GenerateIL(eb);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("IsTrue"));
            gen.Emit(OpCodes.Brfalse, end.Value);
                
            // {Block}
            Block.GenerateIL(eb);

            // goto start;
            gen.Emit(OpCodes.Br, start);

            // end:
            gen.MarkLabel(end.Value);
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to WhileItem.");
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
    }
}
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
