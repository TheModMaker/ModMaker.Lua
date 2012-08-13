using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser.Items
{
    class ReturnItem : IParseItem
    {
        List<IParseItem> children;

        public ReturnItem()
        {
            children = new List<IParseItem>();
        }

        public ParseType Type { get { return ParseType.ExpList; } }

        public void GenerateILNew(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            LocalBuilder loc = gen.DeclareLocal(typeof(List<object>));
            if (children.Count == 1 && children[0] is FuncCallItem)
            {
                (children[0] as FuncCallItem).TailCall = true;
                children[0].GenerateILNew(eb);
                return;
            }

            gen.Emit(OpCodes.Newobj, typeof(List<object>).GetConstructor(new Type[0]));
            gen.Emit(OpCodes.Stloc, loc);
            foreach (var item in children)
            {
                gen.Emit(OpCodes.Ldloc, loc);
                item.GenerateILNew(eb);
                gen.Emit(OpCodes.Call, typeof(List<object>).GetMethod("Add"));
            }
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Newobj, typeof(MultipleReturn).GetConstructor(new Type[] { typeof(List<object>)}));
        }
        public void AddItem(IParseItem item)
        {
            children.Add(item);
        }
        public void WaitOne()
        {
            for (int i = 0; i < children.Count; i++)
            {
                children[i].WaitOne();
                if (children[i] is AsyncItem)
                    children[i] = (children[i] as AsyncItem).Item;
            }
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            foreach (var item in children)
                item.ResolveLabels(cb, tree);
        }
        public bool HasNested()
        {
            bool b = false;
            foreach (var item in children)
                b = b || item.HasNested();

            return b;
        }
    }
}
