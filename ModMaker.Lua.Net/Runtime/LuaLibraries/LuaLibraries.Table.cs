using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        class Table
        {
            public Table(ILuaEnvironment E)
            {
                this.E = E;
            }

            public void Initialize()
            {
                ILuaValue table = E.Runtime.CreateTable();
                Register(E, table, (Func<ILuaTable, string, int, int, string>)concat);
                Register(E, table, (Action<ILuaTable, ILuaValue, ILuaValue>)insert);
                Register(E, table, (Func<ILuaValue[], ILuaValue>)pack);
                Register(E, table, (Func<ILuaTable, int?, ILuaValue>)remove);
                Register(E, table, (Action<ILuaTable, ILuaValue>)sort);
                Register(E, table, (Func<ILuaTable, int, int?, IEnumerable<ILuaValue>>)unpack);

                E.GlobalsTable.SetItemRaw(E.Runtime.CreateValue("table"), table);
            }

            readonly ILuaEnvironment E;

            string concat(ILuaTable table, string sep = null, int i = 1, int j = -1)
            {
                CheckNotNull("table.concat", table);
                int len = (int)(table.Length().AsDouble() ?? 0);
                if (i >= len)
                    return "";

                i = normalizeIndex_(len, i);
                j = normalizeIndex_(len, j);

                StringBuilder str = new StringBuilder();
                for (; i <= j; i++)
                {
                    ILuaValue temp = table.GetItemRaw(E.Runtime.CreateValue(i));
                    if (temp.ValueType != LuaValueType.String && temp.ValueType != LuaValueType.Number)
                    {
                        throw new ArgumentException(
                            "Invalid '" + temp.ValueType + "' value for function 'table.concat'.");
                    }

                    if (str.Length > 0)
                        str.Append(sep);
                    str.Append(temp);
                }

                return str.ToString();
            }
            void insert(ILuaTable table, ILuaValue pos, ILuaValue value = null)
            {
                CheckNotNull("table.insert", table);
                CheckNotNull("table.insert", pos);
                double i;
                double len = table.Length().AsDouble() ?? 0;
                if (value == null)
                {
                    value = pos;
                    i = len + 1;
                }
                else
                {
                    i = pos.AsDouble() ?? 0;
                }

                if (i > len + 1 || i < 1 || i % 1 != 0)
                {
                    throw new ArgumentException(
                            "Position given to function 'table.insert' is outside valid range.");
                }

                for (double d = len; d >= i; d--)
                {
                    var temp = table.GetItemRaw(E.Runtime.CreateValue(d));
                    table.SetItemRaw(E.Runtime.CreateValue(d + 1), temp);
                }
                table.SetItemRaw(pos, value);
            }
            ILuaValue pack(params ILuaValue[] args)
            {
                ILuaTable ret = E.Runtime.CreateTable();
                for (int i = 0; i < args.Length; i++)
                {
                    ret.SetItemRaw(E.Runtime.CreateValue(i + 1), args[i]);
                }
                ret.SetItemRaw(E.Runtime.CreateValue("n"), E.Runtime.CreateValue(args.Length));
                return ret;
            }
            ILuaValue remove(ILuaTable table, int? pos = null)
            {
                CheckNotNull("table.remove", table);

                double len = table.Length().AsDouble() ?? 0;
                pos = pos ?? (int)len;
                if (pos > len + 1 || pos < 1)
                {
                    throw new ArgumentException(
                        "Position given to function 'table.remove' is outside valid range.");
                }

                ILuaValue prev = LuaNil.Nil;
                for (double d = len; d >= pos; d--)
                {
                    ILuaValue ind = E.Runtime.CreateValue(d);
                    ILuaValue temp = table.GetItemRaw(ind);
                    table.SetItemRaw(ind, prev);
                    prev = temp;
                }
                return prev;
            }
            void sort(ILuaTable table, ILuaValue comp = null)
            {
                CheckNotNull("table.sort", table);
                var comparer = new SortComparer(E, comp);

                ILuaValue[] elems = unpack(table).OrderBy(k => k, comparer).ToArray();
                for (int i = 0; i < elems.Length; i++)
                {
                    ILuaValue ind = E.Runtime.CreateValue(i + 1);
                    table.SetItemRaw(ind, elems[i]);
                }
            }
            [MultipleReturn]
            IEnumerable<ILuaValue> unpack(ILuaTable table, int i = 1, int? jOrNull = null)
            {
                CheckNotNull("table.unpack", table);
                int len = (int)(table.Length().AsDouble() ?? 0);
                int j = jOrNull ?? len;
                for (; i <= j; i++)
                {
                    yield return table.GetItemRaw(E.Runtime.CreateValue(i));
                }
            }

            class SortComparer : IComparer<ILuaValue>
            {
                public SortComparer(ILuaEnvironment E, ILuaValue method)
                {
                    if (method != null && method.ValueType != LuaValueType.Function)
                    {
                        throw new ArgumentException(
                            "Invalid '" + method.ValueType + "' value for function 'table.sort'.");
                    }
                    method_ = method;
                    E_ = E;
                }

                public int Compare(ILuaValue x, ILuaValue y)
                {
                    if (method_ != null)
                    {
                        ILuaMultiValue ret = method_.Invoke(
                            LuaNil.Nil, false, -1, E_.Runtime.CreateMultiValueFromObj(x, y));
                        return ret.IsTrue ? -1 : 1;
                    }

                    return x.CompareTo(y);
                }

                ILuaEnvironment E_;
                ILuaValue method_;
            }
        }
    }
}
