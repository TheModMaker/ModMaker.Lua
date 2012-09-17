<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser
{
    class ReturnItem : IParseItem
    {
        List<IParseItem> children;

        public ReturnItem()
        {
            children = new List<IParseItem>();
        }

        public ParseType Type { get { return ParseType.ExpList; } }

        public void GenerateIL(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            LocalBuilder loc = gen.DeclareLocal(typeof(List<object>));
            if (children.Count == 1 && children[0] is FuncCallItem)
            {
                (children[0] as FuncCallItem).TailCall = true;
                children[0].GenerateIL(eb);
                return;
            }

            // loc = new List<object>();
            gen.Emit(OpCodes.Newobj, typeof(List<object>).GetConstructor(new Type[0]));
            gen.Emit(OpCodes.Stloc, loc);

            foreach (var item in children)
            {
                // loc.Add({item});
                gen.Emit(OpCodes.Ldloc, loc);
                item.GenerateIL(eb);
                gen.Emit(OpCodes.Call, typeof(List<object>).GetMethod("Add"));
            }

            //! push new MultipleReturn(loc)
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Newobj, typeof(MultipleReturn).GetConstructor(new Type[] { typeof(List<object>)}));
        }
        public void AddItem(IParseItem item)
        {
            children.Add(item);
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            foreach (var item in children)
                item.ResolveLabels(cb, tree);
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
    class ReturnItem : IParseItem
    {
        List<IParseItem> children;

        public ReturnItem()
        {
            children = new List<IParseItem>();
        }

        public ParseType Type { get { return ParseType.ExpList; } }

        public void GenerateIL(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            LocalBuilder loc = gen.DeclareLocal(typeof(List<object>));
            if (children.Count == 1 && children[0] is FuncCallItem)
            {
                (children[0] as FuncCallItem).TailCall = true;
                children[0].GenerateIL(eb);
                return;
            }

            // loc = new List<object>();
            gen.Emit(OpCodes.Newobj, typeof(List<object>).GetConstructor(new Type[0]));
            gen.Emit(OpCodes.Stloc, loc);

            foreach (var item in children)
            {
                // loc.Add({item});
                gen.Emit(OpCodes.Ldloc, loc);
                item.GenerateIL(eb);
                gen.Emit(OpCodes.Call, typeof(List<object>).GetMethod("Add"));
            }

            //! push new MultipleReturn(loc)
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Newobj, typeof(MultipleReturn).GetConstructor(new Type[] { typeof(List<object>)}));
        }
        public void AddItem(IParseItem item)
        {
            children.Add(item);
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            foreach (var item in children)
                item.ResolveLabels(cb, tree);
        }
    }
}
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
