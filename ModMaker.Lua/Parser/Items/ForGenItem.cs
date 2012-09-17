using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser
{
    class ForGenItem : IParseItem
    {
        List<IParseItem> _exps;
        List<string> _names;
        Label? end;

        public ForGenItem(List<string> names)
        {
            this._names = names;
            this._exps = new List<IParseItem>();
        }

        public IParseItem Block { get; set; }
        public ParseType Type { get { return ParseType.Statement; } }

        public void GenerateIL(ChunkBuilderNew cb)
        {
            ILGenerator gen = cb.CurrentGenerator;
            Label start = gen.DefineLabel();
            LocalBuilder f = gen.DeclareLocal(typeof(object));
            LocalBuilder s = gen.DeclareLocal(typeof(object));
            LocalBuilder var = gen.DeclareLocal(typeof(object));
            LocalBuilder temp = gen.DeclareLocal(typeof(List<object>));
            LocalBuilder ret = gen.DeclareLocal(typeof(MultipleReturn));
            LocalBuilder args = gen.DeclareLocal(typeof(object[]));
            if (end == null)
                end = gen.DefineLabel();

        cb.StartBlock();
            // temp = new List<object>();
            gen.Emit(OpCodes.Newobj, typeof(List<object>).GetConstructor(new Type[0]));
            gen.Emit(OpCodes.Stloc, temp);

            foreach (var item in _exps)
            {
                // temp.Add({item});
                gen.Emit(OpCodes.Ldloc, temp);
                item.GenerateIL(cb);
                gen.Emit(OpCodes.Callvirt, typeof(List<object>).GetMethod("Add"));
            }

            // RuntimeHelper.ForGenStart(temp, ref f, ref s, ref var);
            gen.Emit(OpCodes.Ldloc, temp);
            gen.Emit(OpCodes.Ldloca, f);
            gen.Emit(OpCodes.Ldloca, s);
            gen.Emit(OpCodes.Ldloca, var);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("ForGenStart"));

            // start:
            gen.MarkLabel(start);

            // args = new object[2]
            gen.Emit(OpCodes.Ldc_I4_2);
            gen.Emit(OpCodes.Newarr, typeof(object));
            gen.Emit(OpCodes.Stloc, args);

            // args[0] = s
            gen.Emit(OpCodes.Ldloc, args);
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Ldloc, s);
            gen.Emit(OpCodes.Stelem, typeof(object));

            // args[1] = var
            gen.Emit(OpCodes.Ldloc, args);
            gen.Emit(OpCodes.Ldc_I4_1);
            gen.Emit(OpCodes.Ldloc, var);
            gen.Emit(OpCodes.Stelem, typeof(object));

            // ret = RuntimeHelper.Invoke(E, f, args)
            cb.PushEnv();
            gen.Emit(OpCodes.Ldloc, f);
            gen.Emit(OpCodes.Ldloc, args);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("Invoke"));
            gen.Emit(OpCodes.Stloc, ret);

            // var = ret[0]
            gen.Emit(OpCodes.Ldloc, ret);
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Callvirt, typeof(MultipleReturn).GetMethod("get_Item"));
            gen.Emit(OpCodes.Stloc, var);

            // if (var == null) goto end;
            gen.Emit(OpCodes.Ldloc, var);
            gen.Emit(OpCodes.Brfalse, end.Value);
           
            // var_1 = var           
            cb.DefineLocal(_names[0], true);
            gen.Emit(OpCodes.Ldloc, var);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("SetValue"));

            for (int i = 1; i < _names.Count; i++)
            {
                // RuntimeHelper.SetValue(ref {_names[i]}, ret[{i}]);
                cb.DefineLocal(_names[i], true);
                gen.Emit(OpCodes.Ldloc, ret);
                gen.Emit(OpCodes.Ldc_I4, i);
                gen.Emit(OpCodes.Callvirt, typeof(MultipleReturn).GetMethod("get_Item"));
                gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("SetValue"));
            }

            // {Block}
            Block.GenerateIL(cb);

            // goto start;
            gen.Emit(OpCodes.Br, start);

            // end:
            gen.MarkLabel(end.Value);
        cb.EndBlock();
        }
        public void AddItem(IParseItem item)
        {
            _exps.Add(item);
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            ILGenerator gen = cb.CurrentGenerator;
            var b = tree.StartBlock(true);
                end = gen.DefineLabel();
                Block.ResolveLabels(cb, tree);
                foreach (var item in _exps)
                    item.ResolveLabels(cb, tree);
                tree.DefineLabel("<break>", end.Value);
            tree.EndBlock(b);
        }
    }
}
