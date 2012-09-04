using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;
using System.Reflection;
using System.Collections;

namespace ModMaker.Lua.Parser.Items
{
    class VarInitItem : IParseItem
    {
        List<IParseItem> exps;
        List<IParseItem> names;

        public VarInitItem(bool local)
        {
            this.names = new List<IParseItem>();
            this.exps = new List<IParseItem>();
            this.Local = local;
        }

        public bool Local { get; private set; }
        public ParseType Type { get { return ParseType.Statement; } }

        public void GenerateIL(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            LocalBuilder loc1 = gen.DeclareLocal(typeof(List<object>));
            LocalBuilder loc2 = gen.DeclareLocal(typeof(object[]));

            // loc1 = new List<object>();
            gen.Emit(OpCodes.Newobj, typeof(List<object>).GetConstructor(new Type[0]));
            gen.Emit(OpCodes.Stloc, loc1);

            foreach (var item in exps)
            {
                // loc1.Add({item});
                gen.Emit(OpCodes.Ldloc, loc1);
                item.GenerateIL(eb);
                gen.Emit(OpCodes.Callvirt, typeof(List<object>).GetMethod("Add"));
            }

            // loc2 = RuntimeHelper.SetValues(loc1, {names.Count});
            gen.Emit(OpCodes.Ldloc, loc1);
            gen.Emit(OpCodes.Ldc_I4, names.Count);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("SetValues", BindingFlags.Public | BindingFlags.Static));
            gen.Emit(OpCodes.Stloc, loc2);

            for (int i=0; i<names.Count; i++)
            {
                if (Local)
                    eb.DefineLocal((names[i] as NameItem).Name, true);
                else
                {
                    if (names[i] is NameItem)
                        (names[i] as NameItem).Set = true;
                    else
                        (names[i] as IndexerItem).Set = true;
                    names[i].GenerateIL(eb);
                }

                // RuntimeHelper.SetValue(ref {names[i]}, loc2[{i}]);
                gen.Emit(OpCodes.Ldloc, loc2);
                gen.Emit(OpCodes.Ldc_I4, i);
                gen.Emit(OpCodes.Ldelem, typeof(object));
                gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("SetValue", BindingFlags.Public | BindingFlags.Static));
            }
        }
        public void AddName(IParseItem name)
        {
            if (!name.Type.HasFlag(ParseType.Variable))
                throw new ArgumentException("An varlist must contain only Variables.");

            names.Add(name);
        }
        public void AddItem(IParseItem item)
        {
            if (!item.Type.HasFlag(ParseType.Expression))
                throw new ArgumentException("An explist must contain only Expressions.");

            exps.Add(item);
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            if (Local)
                tree.DefineLocal();

            foreach (var item in exps)
                item.ResolveLabels(cb, tree);
            foreach (var item in names)
                item.ResolveLabels(cb, tree);
        }
    }
}
