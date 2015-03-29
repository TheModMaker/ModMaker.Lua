using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        static class Table
        {
            public static ILuaTable Initialize(ILuaEnvironment E)
            {
                ILuaTable table = new LuaTableNet();
                table.SetItemRaw("concat", new concat(E));
                table.SetItemRaw("insert", new insert(E));
                table.SetItemRaw("pack", new pack(E));
                table.SetItemRaw("remove", new remove(E));
                table.SetItemRaw("sort", new sort(E));
                table.SetItemRaw("unpack", new unpack(E));

                return table;
            }

            class SortHelper : IComparer<object>
            {
                IMethod meth;

                public SortHelper(IMethod/*!*/ meth)
                {
                    this.meth = meth;
                }

                public int Compare(object x, object y)
                {
                    if (meth != null)
                    {
                        var ret = meth.Invoke(null, false, null, new[] { x, y });
                        object o = ret[0];
                        if (!(o is bool))
                            throw new InvalidOperationException("Invalid comparer function.");

                        bool b = (bool)o;
                        return b ? -1 : 1;
                    }
                    else
                        return Comparer.Default.Compare(x, y);
                }
            }

            sealed class concat : LuaFrameworkMethod
            {
                public concat(ILuaEnvironment E) : base(E, "table.concat") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'table.concat'.");

                    ILuaTable table = args[0] as ILuaTable;
                    string sep = args.Length > 1 ? args[1] as string : "";
                    double i = ((args.Length > 2 ? args[2] : null) as double? ?? 1);

                    if (table == null)
                        throw new ArgumentException("First argument to function 'table.concat' must be a table.");

                    double len = table.GetLength();
                    double j = ((args.Length > 3 ? args[3] : null) as double? ?? len);

                    if (j < i)
                        return new MultipleReturn((object)"");

                    StringBuilder str = new StringBuilder();
                    object temp = table.GetItemRaw(i);
                    if (temp == null)
                        throw new ArgumentException("Invalid 'nil' value for function 'table.concat'.");
                    str.Append(temp);
                    for (double ii = i + 1; ii < j; ii++)
                    {
                        temp = table.GetItemRaw(ii);
                        if (temp == null)
                            throw new ArgumentException("Invalid 'nil' value for function 'table.concat'.");
                        str.Append(sep);
                        str.Append(temp);
                    }

                    return new MultipleReturn((object)str.ToString());
                }
            }
            sealed class insert : LuaFrameworkMethod
            {
                public insert(ILuaEnvironment E) : base(E, "table.insert") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting at least two arguments to function 'table.insert'.");

                    ILuaTable table = args[0] as ILuaTable;
                    if (table == null)
                        throw new ArgumentException("First argument to function 'table.insert' must be a table.");

                    double len = table.GetLength();

                    object value;
                    double pos;
                    if (args.Length == 2)
                    {
                        pos = len + 1;
                        value = args[1];
                    }
                    else
                    {
                        object temp = args[1];
                        if (!(temp is double))
                            throw new ArgumentException("Second argument to function 'table.insert' must be a number.");
                        pos = (double)temp;
                        if (pos > len + 1 || pos < 1 || pos % 1 != 0)
                            throw new ArgumentException("Position given to function 'table.insert' is outside valid range.");

                        value = args[2];
                    }

                    for (double d = len; d > pos; d--)
                    {
                        table.SetItemRaw(d, table.GetItemRaw(d - 1));
                    }
                    table.SetItemRaw(pos, value);

                    return new MultipleReturn();
                }
            }
            sealed class pack : LuaFrameworkMethod
            {
                public pack(ILuaEnvironment E) : base(E, "table.pack") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    ILuaTable table = new LuaTableNet();
                    double d = 1;

                    if (args != null)
                    {
                        for (int i = 0; i < args.Length; i++)
                        {
                            object obj = args[i];
                            table.SetItemRaw(d, obj);
                            d += 1;
                        }
                    }
                    table.SetItemRaw("index", d - 1);

                    return new MultipleReturn((object)table);
                }
            }
            sealed class remove : LuaFrameworkMethod
            {
                public remove(ILuaEnvironment E) : base(E, "table.remove") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'table.remove'.");

                    ILuaTable table = args[0] as ILuaTable;
                    if (table == null)
                        throw new ArgumentException("First argument to function 'table.remove' must be a table.");

                    double len = table.GetLength();

                    double pos;
                    if (args.Length == 1)
                    {
                        pos = len + 1;
                    }
                    else
                    {
                        object temp = args[1];
                        if (!(temp is double))
                            throw new ArgumentException("Second argument to function 'table.remove' must be a number.");
                        pos = (double)temp;
                        if (pos > len + 1 || pos < 1 || pos % 1 != 0)
                            throw new ArgumentException("Position given to function 'table.remove' is outside valid range.");
                    }

                    object value = table.GetItemRaw(pos);
                    for (double d = pos; d < len; d++)
                    {
                        table.SetItemRaw(d, table.GetItemRaw(d + 1));
                    }
                    table.SetItemRaw(len, null);

                    return new MultipleReturn(value);
                }
            }
            sealed class sort : LuaFrameworkMethod
            {
                public sort(ILuaEnvironment E) : base(E, "table.sort") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'table.sort'.");

                    ILuaTable table = args[0] as ILuaTable;
                    if (table == null)
                        throw new ArgumentException("First argument to function 'table.sort' must be a table.");

                    IComparer<object> comp;
                    if (args.Length > 1)
                    {
                        IMethod meth = args[0] as IMethod;
                        if (meth == null)
                            throw new ArgumentException("Second argument to function 'table.sort' must be a function.");

                        comp = new SortHelper(meth);
                    }
                    else
                        comp = new SortHelper(null);

                    var temp = table.Where(k => k.Key is double).Select(k => k.Value).OrderBy(k => k, comp).ToArray();

                    for (double d = 1; d < temp.Length + 1; d++)
                    {
                        table.SetItemRaw(d, temp[(int)d - 1]);
                    }
                    return new MultipleReturn();
                }
            }
            sealed class unpack : LuaFrameworkMethod
            {
                public unpack(ILuaEnvironment E) : base(E, "table.unpack") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'table.unpack'.");

                    ILuaTable table = args[0] as ILuaTable;
                    double i = ((args.Length > 1 ? args[1] : null) as double? ?? 1);

                    if (table == null)
                        throw new ArgumentException("First argument to function 'table.unpack' must be a table.");

                    double len = table.GetLength();
                    double j = ((args.Length > 2 ? args[2] : null) as double? ?? len);

                    return new MultipleReturn(
                        table
                            .Where(obj => obj.Key is double && (double)obj.Key >= i && (double)obj.Key <= j)
                            .OrderBy(k => (double)k.Key)
                            .Select(k => k.Value)
                        );
                }
            }
        }
    }
}
