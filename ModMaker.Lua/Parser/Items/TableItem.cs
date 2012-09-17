using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using ModMaker.Lua.Runtime;
using System.Reflection.Emit;
using System.Reflection;

namespace ModMaker.Lua.Parser
{
    [DebuggerDisplay("Count = {_fields.Count}")]
    class TableItem : IParseItem
    {
        List<Tuple<IParseItem, IParseItem>> _fields;
        double i = 1;

        public TableItem()
        {
            this._fields = new List<Tuple<IParseItem, IParseItem>>();
        }

        public ParseType Type { get { return ParseType.Expression | ParseType.Args; } }

        public void GenerateIL(ChunkBuilderNew eb)
        {
            var gen = eb.CurrentGenerator;
            var loc = gen.DeclareLocal(typeof(LuaTable));

            // loc = new LuaTable();
            gen.Emit(OpCodes.Newobj, typeof(LuaTable).GetConstructor(new Type[0]));
            gen.Emit(OpCodes.Stloc, loc);

            foreach (var item in _fields)
            {
                // loc.SetItemRaw({item.Item1}, {item.Item2});
                gen.Emit(OpCodes.Ldloc, loc);
                item.Item1.GenerateIL(eb);
                item.Item2.GenerateIL(eb);
                gen.Emit(OpCodes.Callvirt, typeof(LuaTable).GetMethod("SetItemRaw"));
            }

            //! push loc;
            gen.Emit(OpCodes.Ldloc, loc);
        }
        public void AddItem(IParseItem index, IParseItem exp)
        {
            if (index == null)
                index = new LiteralItem(i++);
            _fields.Add(new Tuple<IParseItem, IParseItem>(index, exp));
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to TableItem.");
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            foreach (var item in _fields)
            {
                item.Item1.ResolveLabels(cb, tree);
                item.Item2.ResolveLabels(cb, tree);
            }
        }
    }
}
