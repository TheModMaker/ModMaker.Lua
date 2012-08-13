﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser.Items
{
    class ForNumItem : IParseItem
    {
        Label? end;
        string name;
        public ForNumItem(string name)
        {
            this.name = name;
        }

        public IParseItem Start { get; set; }
        public IParseItem Limit { get; set; }
        public IParseItem Step { get; set; }
        public IParseItem Block { get; set; }
        public ParseType Type { get { return ParseType.Statement; } }

        public void GenerateILNew(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            Label start = gen.DefineLabel();
            Label sj = gen.DefineLabel(), err = gen.DefineLabel();
            LocalBuilder d = gen.DeclareLocal(typeof(double?));
            LocalBuilder val = gen.DeclareLocal(typeof(double));
            LocalBuilder step = gen.DeclareLocal(typeof(double));
            LocalBuilder limit = gen.DeclareLocal(typeof(double));
            if (end == null)
                end = gen.DefineLabel();

        eb.StartBlock();
            /* get the start */
            Start.GenerateILNew(eb);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("ToNumber"));
            gen.Emit(OpCodes.Stloc, d);
            gen.Emit(OpCodes.Ldloca, d);
            gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_HasValue"));
            gen.Emit(OpCodes.Brtrue, sj);
            gen.MarkLabel(err);
            gen.Emit(OpCodes.Ldstr, "The Start, Limit, and Step of a for loop must result in numbers.");
            gen.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(new Type[] { typeof(string) }));
            gen.Emit(OpCodes.Throw);
            gen.MarkLabel(sj);
            gen.Emit(OpCodes.Ldloca, d);
            gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_Value"));
            gen.Emit(OpCodes.Stloc, val);

            /* get the step */
            if (Step != null)
            {
                Step.GenerateILNew(eb);
                gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("ToNumber"));
                gen.Emit(OpCodes.Stloc, d);
                gen.Emit(OpCodes.Ldloca, d);
                gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_HasValue"));
                gen.Emit(OpCodes.Brfalse, err);
                gen.Emit(OpCodes.Ldloca, d);
                gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_Value"));
            }
            else
            {
                gen.Emit(OpCodes.Ldc_R8, 1.0);
            }
            gen.Emit(OpCodes.Stloc, step);

            /* get the limit */
            Limit.GenerateILNew(eb);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("ToNumber"));
            gen.Emit(OpCodes.Stloc, d);
            gen.Emit(OpCodes.Ldloca, d);
            gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_HasValue"));
            gen.Emit(OpCodes.Brfalse, err);
            gen.Emit(OpCodes.Ldloca, d);
            gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_Value"));
            gen.Emit(OpCodes.Stloc, limit);

            gen.MarkLabel(start);

            // if ((step > 0) ^ (val > limit)) goto end
            gen.Emit(OpCodes.Ldloc, step);
            gen.Emit(OpCodes.Ldc_R8, 0.0);
            gen.Emit(OpCodes.Cgt);
            gen.Emit(OpCodes.Ldloc, val);
            gen.Emit(OpCodes.Ldloc, limit);
            gen.Emit(OpCodes.Cgt);
            gen.Emit(OpCodes.Ldc_I4_1);
            gen.Emit(OpCodes.Xor);
            gen.Emit(OpCodes.And);
            gen.Emit(OpCodes.Ldloc, step);
            gen.Emit(OpCodes.Ldc_R8, 0.0);
            gen.Emit(OpCodes.Cgt);
            gen.Emit(OpCodes.Ldc_I4_1);
            gen.Emit(OpCodes.Xor);
            gen.Emit(OpCodes.Ldloc, val);
            gen.Emit(OpCodes.Ldloc, limit);
            gen.Emit(OpCodes.Clt);
            gen.Emit(OpCodes.Ldc_I4_1);
            gen.Emit(OpCodes.Xor);
            gen.Emit(OpCodes.And);
            gen.Emit(OpCodes.Or);
            gen.Emit(OpCodes.Brfalse, end.Value);

            eb.DefineLocal(name, true);
            gen.Emit(OpCodes.Ldloc, val);
            gen.Emit(OpCodes.Box, typeof(double));
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("SetValue"));

            Block.GenerateILNew(eb);

            // _E.ForLoopInc
            gen.Emit(OpCodes.Ldloc, val);
            gen.Emit(OpCodes.Ldloc, step);
            gen.Emit(OpCodes.Add);
            gen.Emit(OpCodes.Stloc, val);
            gen.Emit(OpCodes.Br, start);

            // end:
            gen.MarkLabel(end.Value);

        eb.EndBlock();
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to ForNumItem.");
        }
        public void WaitOne()
        {
            Block.WaitOne();
            if (Block is AsyncItem)
                Block = (Block as AsyncItem).Item;

            Start.WaitOne();
            if (Start is AsyncItem)
                Start = (Start as AsyncItem).Item;

            Limit.WaitOne();
            if (Limit is AsyncItem)
                Limit = (Limit as AsyncItem).Item;

            if (Step != null)
            {
                Step.WaitOne();
                if (Step is AsyncItem)
                    Step = (Step as AsyncItem).Item;
            }
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            ILGenerator gen = cb.CurrentGenerator;
            var b = tree.StartBlock(true);
                end = gen.DefineLabel();
                Block.ResolveLabels(cb, tree);
                if (Start != null)
                    Start.ResolveLabels(cb, tree);
                if (Limit != null)
                    Limit.ResolveLabels(cb, tree);
                if (Step != null)
                    Step.ResolveLabels(cb, tree);
                tree.DefineLabel("<break>", end.Value);
            tree.EndBlock(b);
        }
        public bool HasNested()
        {
            return Start.HasNested() || Limit.HasNested() || (Step != null && Step.HasNested()) || Block.HasNested();
        }
    }
}