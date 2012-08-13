using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using ModMaker.Lua.Runtime;
using System.Reflection.Emit;
using System.Reflection;

namespace ModMaker.Lua.Parser.Items
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

        public void GenerateILNew(ChunkBuilderNew eb)
        {
            var gen = eb.CurrentGenerator;
            var loc = gen.DeclareLocal(typeof(LuaTable));

            gen.Emit(OpCodes.Newobj, typeof(LuaTable).GetConstructor(new Type[0]));
            gen.Emit(OpCodes.Stloc, loc);
            foreach (var item in _fields)
            {
                gen.Emit(OpCodes.Ldloc, loc);
                item.Item1.GenerateILNew(eb);
                item.Item2.GenerateILNew(eb);
                gen.Emit(OpCodes.Callvirt, typeof(LuaTable).GetMethod("SetItemRaw"));
            }
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
        public void WaitOne()
        {
            for (int ind = 0; i < _fields.Count; i++)
            {
                IParseItem i1 = _fields[ind].Item1;
                IParseItem i2 = _fields[ind].Item2;

                if (i1 is AsyncItem)
                    i1 = (i1 as AsyncItem).Item;
                if (i2 is AsyncItem)
                    i2 = (i2 as AsyncItem).Item;

                _fields[ind] = new Tuple<IParseItem, IParseItem>(i1, i2);
            }
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            foreach (var item in _fields)
            {
                item.Item1.ResolveLabels(cb, tree);
                item.Item2.ResolveLabels(cb, tree);
            }
        }
        public bool HasNested()
        {
            bool b = false;
            foreach (var item in _fields)
                b = b || item.Item1.HasNested() || item.Item2.HasNested();

            return b;
        }
    }
}
