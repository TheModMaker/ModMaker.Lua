<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua.Parser;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Collections;
using System.Threading;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace ModMaker.Lua.Runtime
{
    static class LuaStaticLibraries
    {
        static class Standard
        {
            public const string _VERSION = "Lua 5.2";
            public const string _NET = ".NET 4.0";

            public static MultipleReturn assert(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'assert'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                string message = RuntimeHelper.GetValue(o[0]) as string ?? "assertion failed!";

                if (obj == null || obj as bool? == false)
                    throw new AssertException(message);
                else
                    return new MultipleReturn(obj);
            }
            public static MultipleReturn collectgarbage(LuaParameters o)
            {
                string opt = RuntimeHelper.GetValue(o[0]) as string;
                object arg = RuntimeHelper.GetValue(o[1]);

                if (string.IsNullOrEmpty(opt) && o.Count > 0)
                    throw new ArgumentException("First argument to 'collectgarbage' must be a string.");

                if (opt == "collect" || string.IsNullOrEmpty(opt))
                {
                    int gen = -1;
                    double? d = arg as double?;
                    if (d == 0)
                        gen = 0;
                    else if (d == 1)
                        gen = 1;
                    else if (d == 2)
                        gen = 2;
                    else
                        throw new ArgumentException("Second argument to 'collectgarbage' with opt as 'collect' must be a 0, 1 or 2.");

                    if (gen == -1)
                        GC.Collect();
                    else
                        GC.Collect(gen);

                    return new MultipleReturn();
                }
                else if (opt == "count")
                {
                    double mem = GC.GetTotalMemory(false);

                    return new MultipleReturn(mem, mem % 1024);
                }
                else if (opt == "stop" || opt == "restart" || opt == "step" || opt == "setpause" || opt == "setstepmul" || opt == "generational" || opt == "incremental")
                {
                    throw new ArgumentException("The option '" + opt + "' is not supported by this framework.");
                }
                else if (opt == "isrunning")
                {
                    return new MultipleReturn(true);
                }
                else
                {
                    throw new ArgumentException("The option '" + opt + "' is not recognized to function 'collectgarbage'.");
                }
            }
            public static MultipleReturn error(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'error'.");

                string message = RuntimeHelper.GetValue(o[0]) as string;

                if (message == null)
                    throw new ArgumentException("First argument to function 'error' must be a string.");

                throw new AssertException(message);
            }
            public static MultipleReturn getmetatable(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'getmetatable'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;

                if (table == null)
                    return new MultipleReturn();

                LuaTable meta = table.MetaTable;
                if (meta != null)
                {
                    object meth = meta._get("__metatable");
                    if (meth != null)
                        return new MultipleReturn(meth);
                }

                return new MultipleReturn(meta);
            }
            public static MultipleReturn ipairs(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'ipars'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;

                if (table == null)
                    throw new ArgumentException("First argument to 'iparis' must be a table.");

                LuaTable meta = table.MetaTable;
                if (meta != null)
                {
                    LuaMethod meth = meta._get("__ipairs") as LuaMethod;
                    if (meth != null)
                    {
                        var ret = meth.InvokeInternal(new object[] { table }, -1);
                        return ret.AdjustResults(3);
                    }
                }

                return new MultipleReturn(
                    new LuaMethod(typeof(Standard).GetMethod("_ipairs_itr"), null, "ipairs iterator", o.Environment),
                    table,
                    0);
            }
            public static MultipleReturn next(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'next'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                object index = RuntimeHelper.GetValue(o[1]);
                if (table == null)
                    throw new ArgumentException("First parameter to 'next' must be a table.");

                var t = table.GetNext(index);
                return new MultipleReturn(t.Item1, t.Item2);
            }
            public static MultipleReturn overload(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'overload'.");

                LuaMethod meth = RuntimeHelper.GetValue(o[0]) as LuaMethod;
                object obj = RuntimeHelper.GetValue(o[1]);

                if (meth == null)
                    throw new ArgumentException("First argument to function 'overload' must be a method.");
                if (obj == null || !(obj is double) || (System.Math.Floor((double)obj) != System.Math.Ceiling((double)obj)))
                    throw new ArgumentException("Second argument to function 'overload' must be an integer.");

                int i = Convert.ToInt32((double)obj);

                return meth.InvokeInternal(o.Cast<object>().Where((ob, ind) => ind > 1).ToArray(), i);
            }
            public static MultipleReturn pairs(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'pairs'.");

                object t = RuntimeHelper.GetValue(o[0]);
                if (t is LuaTable)
                {
                    LuaTable table = t as LuaTable;
                    LuaTable meta = table.MetaTable;
                    if (meta != null)
                    {
                        LuaMethod p = meta._get("__pairs") as LuaMethod;
                        if (p != null)
                        {
                            var ret = p.InvokeInternal(new object[] { table }, -1);
                            return ret.AdjustResults(3);
                        }
                    }

                    return new MultipleReturn(
                        new LuaMethod(typeof(Standard).GetMethod("next"), null, "next", o.Environment),
                        table,
                        null);
                }
                else
                    throw new ArgumentException("First argument to 'pairs' must be a table.");
            }
            public static MultipleReturn pcall(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'pcall'.");

                object func = RuntimeHelper.GetValue(o[0]);
                if (func is LuaMethod)
                {
                    try
                    {
                        var ret = (func as LuaMethod).InvokeInternal(o.Cast<object>().Where((obj, i) => i > 0).ToArray(), -1);
                        return new MultipleReturn(new object[] { true }.Union(ret));
                    }
                    catch (ThreadAbortException)
                    {
                        throw;
                    }
                    catch (ThreadInterruptedException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(false, e.Message, e);
                    }
                }
                else
                    throw new ArgumentException("First argument to 'pcall' must be a function.");
            }
            public static MultipleReturn print(LuaParameters o)
            {
                StringBuilder str = new StringBuilder();
                for (int i = 0; i < o.Count; i++)
                {
                    object oo = RuntimeHelper.GetValue(o[i]);
                    if (oo is LuaUserData)
                        oo = (oo as LuaUserData).Value;
                    str.Append((oo ?? "").ToString());
                    str.Append('\t');
                }
                str.Append("\n");

                Stream s = o.Environment.Settings.Stdout;
                byte[] txt = Encoding.UTF8.GetBytes(str.ToString());
                s.Write(txt, 0, txt.Length);

                return new MultipleReturn();
            }
            public static MultipleReturn rawequal(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'rawget'.");

                object v1 = RuntimeHelper.GetValue(o[0]);
                object v2 = RuntimeHelper.GetValue(o[1]);


                return new MultipleReturn(object.Equals(v1, v2));
            }
            public static MultipleReturn rawget(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'rawget'.");

                object table = RuntimeHelper.GetValue(o[0]);
                object index = RuntimeHelper.GetValue(o[1]);

                if (table is LuaTable)
                    return new MultipleReturn((table as LuaTable).GetItemRaw(index));
                else
                    throw new ArgumentException("First argument to function 'rawget' must be a table.");
            }
            public static MultipleReturn rawlen(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'rawlen'.");

                object table = RuntimeHelper.GetValue(o[0]);

                if (table is string)
                    return new MultipleReturn((double)(table as string).Length);
                else if (table is LuaTable)
                    return new MultipleReturn((double)(table as LuaTable).Length);
                else
                    throw new ArgumentException("Argument to 'rawlen' must be a string or table.");
            }
            public static MultipleReturn rawset(LuaParameters o)
            {
                if (o.Count < 3)
                    throw new ArgumentException("Expecting three arguments to function 'rawset'.");

                object table = RuntimeHelper.GetValue(o[0]);
                object index = RuntimeHelper.GetValue(o[1]);
                object value = RuntimeHelper.GetValue(o[2]);

                if (!(table is LuaTable))
                    throw new ArgumentException("First argument to 'rawset' must be a table.");
                if (index == null)
                    throw new ArgumentException("Second argument to 'rawset' cannot be nil.");

                (table as LuaTable).SetItemRaw(index, value);

                return new MultipleReturn(table);
            }
            public static MultipleReturn select(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'select'.");

                object index = RuntimeHelper.GetValue(o[0]);

                if (index as string == "#")
                {
                    return new MultipleReturn((double)(o.Count - 1));
                }
                else if (index is double)
                {
                    double d = (double)index;
                    if (d < 0)
                        d = o.Count + d;
                    return new MultipleReturn(o.Cast<object>().Where((obj, i) => i > d));
                }
                else
                    throw new ArgumentException("First argument to function 'select' must be a number or the string '#'.");
            }
            public static MultipleReturn setmetatable(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'setmetatable'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                object metatable = RuntimeHelper.GetValue(o[1]);

                if (table == null)
                    throw new ArgumentException("First argument to function 'setmetatable' must be a table.");

                if (metatable == null)
                    table.MetaTable = null;
                else if (metatable is LuaTable)
                    table.MetaTable = (metatable as LuaTable);
                else
                    throw new ArgumentException("Attempt to set metatable to a '" + _type(metatable) + "' type.");

                return new MultipleReturn(table);
            }
            public static MultipleReturn tonumber(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'tonumber'.");

                double? d = RuntimeHelper.ToNumber(o[0]);
                if (d.HasValue)
                    return new MultipleReturn(d.Value);
                else
                    return new MultipleReturn();
            }
            public static MultipleReturn tostring(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'tostring'.");

                object val = RuntimeHelper.GetValue(o[0]);
                if (val is LuaTable)
                {
                    LuaTable tab = val as LuaTable;
                    var meta = tab.MetaTable;
                    if (meta != null)
                    {
                        var m = meta._get("__tostring");
                        if (m != null && m is LuaMethod)
                        {
                            var result = (m as LuaMethod).InvokeInternal(new[] { val }, -1);
                            return new MultipleReturn((object)result[0].ToString());
                        }
                    }

                    return new MultipleReturn((object)val.ToString());
                }
                else if (val is LuaUserData)
                    return new MultipleReturn((object)(val as LuaUserData).Value.ToString());
                else
                    return new MultipleReturn((object)(val ?? "").ToString());
            }
            public static MultipleReturn type(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'type'.");

                object value = RuntimeHelper.GetValue(o[0]);

                return new MultipleReturn((object)_type(value));
            }

            static MultipleReturn _ipairs_itr(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to 'ipairs iterator'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                double index = RuntimeHelper.GetValue(o[1]) as double? ?? -1;

                if (table == null)
                    throw new ArgumentException("First argument to function 'ipairs iterator' must be a table.");
                if (index < 0 || System.Math.Floor(index) != System.Math.Ceiling(index))
                    throw new ArgumentException("Second argument to function 'ipairs iterator' must be a positive integer.");
                index++;

                var ret = table._get(index);
                if (ret == null)
                    return new MultipleReturn();
                else
                    return new MultipleReturn(index, ret);
            }
        }
        static class Module
        {
            public static MultipleReturn require(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'require'.");

                string str = RuntimeHelper.GetValue(o[0]) as string;
                if (str == null)
                    throw new ArgumentException("First argument to function 'require' must be a string.");

                var bind = o.Environment.Settings.ModuleBinder;
                object ret = bind.Loaded(str);
                if (ret == null)
                    ret = bind.Load(str, o.Environment);

                if (ret is object[])
                    return new MultipleReturn((IEnumerable)ret);
                else
                    return new MultipleReturn(ret);
            }
        }
        static class Table
        {
            class SortHelper : IComparer<object>
            {
                LuaMethod meth;

                public SortHelper(LuaMethod meth)
                {
                    this.meth = meth;
                }

                public int Compare(object x, object y)
                {
                    if (meth != null)
                    {
                        var ret = meth.InvokeInternal(new[] { x, y }, -1);
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

            public static MultipleReturn concat(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'table.concat'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                string sep = RuntimeHelper.GetValue(o[1]) as string ?? "";
                double i = (RuntimeHelper.GetValue(o[2]) as double? ?? 1);

                if (table == null)
                    throw new ArgumentException("First argument to function 'table.concat' must be a table.");

                double len = table.GetLength();
                double j = (RuntimeHelper.GetValue(o[3]) as double? ?? len);

                if (j > i)
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
            public static MultipleReturn insert(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'table.insert'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                if (table == null)
                    throw new ArgumentException("First argument to function 'table.insert' must be a table.");

                double len = table.GetLength();

                object value;
                double pos;
                if (o.Count == 2)
                {
                    pos = len + 1;
                    value = RuntimeHelper.GetValue(o[1]);
                }
                else
                {
                    object temp = RuntimeHelper.GetValue(o[1]);
                    if (!(temp is double))
                        throw new ArgumentException("Second argument to function 'table.insert' must be a number.");
                    pos = (double)temp;
                    if (pos > len + 1 || pos < 1 || System.Math.Ceiling(pos) != System.Math.Floor(pos))
                        throw new ArgumentException("Position given to function 'table.insert' is outside valid range.");

                    value = RuntimeHelper.GetValue(o[2]);
                }

                for (double d = len; d > pos; d--)
                {
                    table.SetItemRaw(d, table.GetItemRaw(d - 1));
                }
                table.SetItemRaw(pos, value);

                return new MultipleReturn();
            }
            public static MultipleReturn pack(LuaParameters o)
            {
                LuaTable table = new LuaTable();
                double d = 1;

                foreach (var item in o)
                {
                    object obj = RuntimeHelper.GetValue(item);
                    table.SetItemRaw(d, obj);
                }
                table.SetItemRaw("n", d - 1);

                return new MultipleReturn((object)table);
            }
            public static MultipleReturn remove(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'table.remove'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                if (table == null)
                    throw new ArgumentException("First argument to function 'table.remove' must be a table.");

                double len = table.GetLength();

                double pos;
                if (o.Count == 1)
                {
                    pos = len + 1;
                }
                else
                {
                    object temp = RuntimeHelper.GetValue(o[1]);
                    if (!(temp is double))
                        throw new ArgumentException("Second argument to function 'table.remove' must be a number.");
                    pos = (double)temp;
                    if (pos > len + 1 || pos < 1 || System.Math.Ceiling(pos) != System.Math.Floor(pos))
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
            public static MultipleReturn sort(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'table.sort'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                if (table == null)
                    throw new ArgumentException("First argument to function 'table.sort' must be a table.");

                double len = table.GetLength();

                IComparer<object> comp;
                if (o.Count > 1)
                {
                    LuaMethod meth = RuntimeHelper.GetValue(o[0]) as LuaMethod;
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
            public static MultipleReturn unpack(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'table.unpack'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                double i = (RuntimeHelper.GetValue(o[1]) as double? ?? 1);

                if (table == null)
                    throw new ArgumentException("First argument to function 'table.unpack' must be a table.");

                double len = table.GetLength();
                double j = (RuntimeHelper.GetValue(o[2]) as double? ?? len);

                return new MultipleReturn(
                    table
                        .Where(obj => obj.Key is double && (double)obj.Key >= i && (double)obj.Key <= j)
                        .OrderBy(k => (double)k.Key)
                        .Select(k => k.Value)
                    );
            }
        }
        static class Bit32
        {
            public static MultipleReturn arshift(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'bit32.arshift'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.arshift' must be a number.");
                int r = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.arshift' must be a number.");
                int i = (int)((double)obj % System.Math.Pow(2, 32));

                if (i < 0 || (r & (1 << 31)) == 0)
                {
                    i *= -1;

                    if (System.Math.Abs(i) > 31)
                        return new MultipleReturn(0.0);
                    else if (i >= 0)
                        return new MultipleReturn((double)(uint)(r << i));
                    else
                        return new MultipleReturn((double)(uint)(r >> -i));
                }
                else
                {
                    if (i >= 31)
                        r = -1;
                    else
                        r = ((r >> i) | ~(-1 >> i));

                    return new MultipleReturn((double)(uint)r);
                }
            }
            public static MultipleReturn band(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'bit32.band'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("Arguments to 'bit32.band' must be numbers.");

                uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                for (int i = 1; i < o.Count; i++)
                {
                    obj = RuntimeHelper.GetValue(o[i]);
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.band' must be numbers.");

                    ret &= (uint)((double)obj % System.Math.Pow(2, 32));
                }
                return new MultipleReturn((double)ret);
            }
            public static MultipleReturn bnot(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'bit32.bnot'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj is double)
                {
                    uint x = (uint)((double)obj % System.Math.Pow(2, 32));

                    return new MultipleReturn((double)(uint)((-1u - x) % System.Math.Pow(2, 32)));
                }
                else
                    throw new ArgumentException("First argument to function 'bit32.bnot' must be a number.");
            }
            public static MultipleReturn bor(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'bit32.bor'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("Arguments to 'bit32.bor' must be numbers.");

                uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                for (int i = 1; i < o.Count; i++)
                {
                    obj = RuntimeHelper.GetValue(o[i]);
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.bor' must be numbers.");

                    ret |= (uint)((double)obj % System.Math.Pow(2, 32));
                }
                return new MultipleReturn((double)ret);
            }
            public static MultipleReturn btest(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'bit32.btest'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("Arguments to 'bit32.btest' must be numbers.");

                uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                for (int i = 1; i < o.Count; i++)
                {
                    obj = RuntimeHelper.GetValue(o[i]);
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.btest' must be numbers.");

                    ret &= (uint)((double)obj % System.Math.Pow(2, 32));
                }
                return new MultipleReturn(ret != 0);
            }
            public static MultipleReturn bxor(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'bit32.bxor'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("Arguments to 'bit32.bxor' must be numbers.");

                uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                for (int i = 1; i < o.Count; i++)
                {
                    obj = RuntimeHelper.GetValue(o[i]);
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.bxor' must be numbers.");

                    ret ^= (uint)((double)obj % System.Math.Pow(2, 32));
                }
                return new MultipleReturn((double)ret);
            }
            public static MultipleReturn extract(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'bit32.extract'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.extract' must be a number.");
                int n = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.extract' must be a number.");
                int field = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[2]);
                int width = 1;
                if (obj is double)
                    width = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                if (field > 31 || width + field > 31)
                    throw new ArgumentException("Attempt to access bits outside the allowed range.");
                if (width < 1)
                    throw new ArgumentException("Cannot specify a zero width.");

                int m = (~((-1 << 1) << ((width - 1))));                
                return new MultipleReturn((double)((n >> field) & m));
            }
            public static MultipleReturn replace(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'bit32.replace'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.replace' must be a number.");
                int r = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.replace' must be a number.");
                int v = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[2]);
                if (!(obj is double))
                    throw new ArgumentException("Third arguments to 'bit32.replace' must be a number.");
                int field = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[3]);
                int width = 1;
                if (obj is double)
                    width = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                if (field > 31 || field < 0 || width < 1 || width + field > 31)
                    throw new ArgumentException("Attempt to access bits outside the allowed range.");

                int m = (~((-1 << 1) << ((width - 1))));
                v &= m;

                return new MultipleReturn((double)(uint)((r & ~(m << field)) | (v << field)));
            }
            public static MultipleReturn lrotate(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'bit32.lrotate'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.lrotate' must be a number.");
                int x = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.lrotate' must be a number.");
                int disp = (int)((double)obj % System.Math.Pow(2, 32));
                disp %= 32;

                if (disp >= 0)
                    return new MultipleReturn((double)((uint)(x << disp) | (uint)(x >> (32 - disp))));
                else
                    return new MultipleReturn((double)((uint)(x >> -disp) | (uint)(x << (32 + disp))));
            }
            public static MultipleReturn lshift(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'bit32.lshift'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.lshift' must be a number.");
                int x = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.lshift' must be a number.");
                int disp = (int)((double)obj % System.Math.Pow(2, 32));

                if (System.Math.Abs(disp) > 31)
                    return new MultipleReturn(0.0);
                else if (disp >= 0)
                    return new MultipleReturn((double)(uint)(x << disp));
                else
                    return new MultipleReturn((double)(uint)(x >> -disp));
            }
            public static MultipleReturn rrotate(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'bit32.rrotate'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.rrotate' must be a number.");
                int x = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.rrotate' must be a number.");
                int disp = (int)((double)obj % System.Math.Pow(2, 32));
                disp %= 32;

                if (disp < 0)
                    return new MultipleReturn((double)((uint)(x << -disp) | (uint)(x >> (32 + disp))));
                else
                    return new MultipleReturn((double)((uint)(x >> disp) | (uint)(x << (32 - disp))));
            }
            public static MultipleReturn rshift(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'bit32.rshift'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.rshift' must be a number.");
                int x = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.rshift' must be a number.");
                int disp = (int)((double)obj % System.Math.Pow(2, 32));

                if (System.Math.Abs(disp) > 31)
                    return new MultipleReturn(0.0);
                else if (disp >= 0)
                    return new MultipleReturn((double)(uint)(x >> disp));
                else
                    return new MultipleReturn((double)(uint)(x << -disp));
            }
        }
        static class String
        {
            class gmatchHelper
            {
                MatchCollection _col;
                int i = 0;

                public gmatchHelper(MatchCollection col)
                {
                    this._col = col;
                }

                public MultipleReturn Do(LuaParameters o)
                {
                    if (_col == null || _col.Count <= i)
                        return new MultipleReturn();
                    Match m = _col[i++];

                    return new MultipleReturn(m.Groups.Cast<Group>().Select(c => c.Value));
                }
            }
            class gsubHelper
            {
                string _string;
                LuaTable _table;
                LuaMethod _meth;

                public gsubHelper(object o)
                {
                    this._string = o as string;
                    this._table = o as LuaTable;
                    this._meth = o as LuaMethod;
                    if (_string == null && _table == null && _meth == null)
                        throw new ArgumentException("Third argument to function 'string.gsub' must be a string, table, or function.");
                }

                public string Match(Match match)
                {
                    string ret = null;

                    if (_string != null)
                    {
                        ret = Regex.Replace(_string, @"%[0-9%]", m =>
                        {
                            if (m.Value == "%%")
                                return "%";
                            int i = int.Parse(m.Groups[0].Value.Substring(1), CultureInfo.InvariantCulture);
                            return i == 0 ? match.Value : (match.Groups.Count > i ? match.Groups[i].Value : "");
                        });
                    }
                    else if (_table != null)
                    {
                        object o = _table.GetItemRaw(match.Value);
                        if (o != null && o as bool? != false)
                        {
                            ret = o.ToString();
                        }
                    }
                    else if (_meth != null)
                    {
                        var obj = _meth.InvokeInternal(match.Groups.Cast<Group>().Where((g, i) => i > 0).Select(c => c.Value).ToArray(), -1);
                        if (obj != null && obj.Count > 0)
                        {
                            object o = obj[0];
                            if (o != null && o as bool? != false)
                            {
                                ret = o.ToString();
                            }
                        }
                    }

                    return ret == null ? match.Value : ret;
                }
            }

            public static MultipleReturn Byte(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'string.byte'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                object obj = RuntimeHelper.GetValue(o[1]);

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.byte' must be a string.");
                if (obj != null && !(obj is double))
                    throw new ArgumentException("Second argument to function 'string.byte' must be an integer.");

                int i = Convert.ToInt32(obj as double? ?? 1);
                int j = Convert.ToInt32(RuntimeHelper.GetValue(o[2]) ?? i);

                i--;
                j--;
                if (i < 0)
                    i = s.Length + i;
                if (j < 0)
                    j = s.Length + j;
                if (i < 1)
                    i = 1;
                if (j >= s.Length)
                    j = s.Length - 1;
                if (i < j)
                    return new MultipleReturn();

                return new MultipleReturn(s.Substring(i, j - i).Select(c => (double)c));                
            }
            public static MultipleReturn Char(LuaParameters o)
            {
                StringBuilder str = new StringBuilder();

                foreach (var item in o)
                {
                    object obj = RuntimeHelper.GetValue(item);
                    if (!(obj is double))
                        throw new ArgumentException("All arguments to function 'string.char' must be numbers.");

                    int i = Convert.ToInt32((double)obj);
                    if (i > char.MaxValue)
                    {
                        int high = (i >> 16) & 0xff;
                        int low = i & 0xff;
                        
                        if (!char.IsSurrogatePair((char)high, (char)low))
                            throw new ArgumentException("Argument to function 'string.char' is outside the range of a char.");

                        str.Append((char)high);
                        str.Append((char)low);
                    }
                    else
                        str.Append((char)i);
                }

                return new MultipleReturn((object)str.ToString());
            }
            public static MultipleReturn find(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'string.find'.");

                string str = RuntimeHelper.GetValue(o[0]) as string;
                string pattern = RuntimeHelper.GetValue(o[1]) as string;
                double start = RuntimeHelper.GetValue(o[2]) as double? ?? 1;
                bool plain = RuntimeHelper.GetValue(o[3]) as bool? ?? false;
                if (str == null)
                    throw new ArgumentException("First argument to function 'string.find' must be a string.");
                if (pattern == null)
                    throw new ArgumentException("Second argument to function 'string.find' must be a string.");

                int startx = start > 0 ? (int)start - 1 : str.Length + (int)start;
                if (plain)
                {
                    int i = str.IndexOf(pattern, startx);
                    if (i == -1)
                        return new MultipleReturn();
                    else
                        return new MultipleReturn((double)i, (double)(i + pattern.Length));
                }

                Regex reg = new Regex(pattern);
                Match mat = reg.Match(str, startx);
                return mat == null ? new MultipleReturn() : 
                    new MultipleReturn(
                        new object[] { (double)mat.Index, (double)(mat.Index + mat.Length) }
                            .Then(mat.Captures.Cast<Capture>()
                            .Select(c => c.Value))
                        );
            }
            public static MultipleReturn foramt(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'string.format'");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                if (s == null)
                    throw new ArgumentException("First argument to function 'string.format' must be a string.");

                return new MultipleReturn((object)string.Format(s, o.Cast<object>().Where((obj, i) => i > 0).ToArray()));
            }
            public static MultipleReturn gmatch(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'string.gmatch'");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                string pattern = RuntimeHelper.GetValue(o[1]) as string;
                if (s == null)
                    throw new ArgumentException("First argument to function 'string.gmatch' must be a string.");
                if (pattern == null)
                    throw new ArgumentException("Second argument to function 'string.gmatch' must be a string.");

                return new MultipleReturn(new LuaMethod(new gmatchHelper(Regex.Matches(s, pattern)).Do, o.Environment));
            }
            public static MultipleReturn gsub(LuaParameters o)
            {
                if (o.Count < 3)
                    throw new ArgumentException("Expecting three arguments to function 'string.gsub'");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                string pattern = RuntimeHelper.GetValue(o[1]) as string;
                object repl = RuntimeHelper.GetValue(o[2]);
                double n = RuntimeHelper.GetValue(o[3]) as double? ?? -1;
                if (s == null)
                    throw new ArgumentException("First argument to function 'string.gsub' must be a string.");
                if (pattern == null)
                    throw new ArgumentException("Second argument to function 'string.gsub' must be a string.");

                return new MultipleReturn((object)Regex.Replace(s, pattern, new gsubHelper(repl).Match));
            }
            public static MultipleReturn len(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'string.len'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.len' must be a string.");

                return new MultipleReturn((double)s.Length);                
            }
            public static MultipleReturn lower(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'string.lower'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.lower' must be a string.");

                return new MultipleReturn((object)s.ToLower(CultureInfo.CurrentCulture));                
            }
            public static MultipleReturn match(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'string.match'.");

                string str = RuntimeHelper.GetValue(o[0]) as string;
                string pattern = RuntimeHelper.GetValue(o[1]) as string;
                double start = RuntimeHelper.GetValue(o[2]) as double? ?? 1;
                if (str == null)
                    throw new ArgumentException("First argument to function 'string.match' must be a string.");
                if (pattern == null)
                    throw new ArgumentException("Second argument to function 'string.match' must be a string.");

                Regex reg = new Regex(pattern);
                int startx = start > 0 ? (int)start - 1 : str.Length + (int)start;
                Match mat = reg.Match(str, startx);
                return mat == null ? new MultipleReturn() : new MultipleReturn(mat.Captures.Cast<Capture>().Select(c => c.Value));
            }
            public static MultipleReturn rep(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'string.rep'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                object obj = RuntimeHelper.GetValue(o[1]);
                string sep = RuntimeHelper.GetValue(o[2]) as string ?? "";

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.rep' must be a string.");
                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to function 'string.rep' must be a number.");

                StringBuilder str = new StringBuilder();
                double limit = (double)obj;
                for (int i = 0; i < limit - 1; i++)
                {
                    str.Append(s);
                    str.Append(sep);
                }
                str.Append(s);
                return new MultipleReturn((object)str.ToString());
            }
            public static MultipleReturn reverse(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'string.reverse'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.reverse' must be a string.");

                return new MultipleReturn((object)new string(s.Reverse().ToArray()));
            }
            public static MultipleReturn sub(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'string.sub'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                object obj = RuntimeHelper.GetValue(o[1]);

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.sub' must be a string.");
                if (obj == null || !(obj is double))
                    throw new ArgumentException("Second argument to function 'string.sub' must be an integer.");

                int i = Convert.ToInt32((double)obj);
                int j = Convert.ToInt32(RuntimeHelper.GetValue(o[2]) ?? i);

                i--;
                j--;
                if (i < 0)
                    i = s.Length + i;
                if (j < 0)
                    j = s.Length + j;
                if (i < 1)
                    i = 1;
                if (j >= s.Length)
                    j = s.Length - 1;
                if (i < j)
                    return new MultipleReturn((object)"");

                return new MultipleReturn((object)s.Substring(i, j - i));
            }
            public static MultipleReturn upper(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'string.upper'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.upper' must be a string.");

                return new MultipleReturn((object)s.ToUpper(CultureInfo.CurrentCulture));
            }
        }
        static class IO
        {
            static Stream _input = null, _output = null;

            public static void _Init(LuaEnvironment E)
            {
                _input = E.Settings.Stdin;
                _output = E.Settings.Stdout;
                E._globals.SetItemRaw("_STDIN", _CreateFile(E.Settings.Stdin, E));
                E._globals.SetItemRaw("_STDOUT", _CreateFile(E.Settings.Stdout, E));
            }

            class LinesHelper
            {
                StreamReader _stream;
                bool _close;
                int[] _ops; // -4 = *L, -3 = *l, -2 = *a, -1 = *n

                public LinesHelper(bool close, Stream stream, int[] ops)
                {
                    this._stream = new StreamReader(stream);
                    this._close = close;
                    this._ops = ops;
                }

                public MultipleReturn Do(LuaParameters o)
                {
                    if (_stream == null)
                        return new MultipleReturn(Enumerable.Range(1, _ops.Length).Select(i => (object)null));

                    var ret = _read(_ops, _stream);

                    if (_stream.EndOfStream)
                    {
                        if (_close)
                            _stream.Close();

                        _stream = null;
                    }

                    return ret;
                }
            }
            class Remove
            {
                List<Stream> st = new List<Stream>();
                static Remove instance = new Remove();
                static object _lock = new object();

                ~Remove()
                {
                    foreach (var item in st)
                        item.Close();

                    st.Clear();
                    st = null;
                }
                Remove()
                {

                }

                public static void Add(Stream s)
                {
                    lock (_lock)
                    {
                        if (!instance.st.Contains(s))
                            instance.st.Add(s);
                    }
                }
            }

            /* io functions*/
            public static MultipleReturn close(LuaParameters o)
            {
                object file = RuntimeHelper.GetValue(o[0]);
                Stream s;

                if (file == null)
                {
                    if (_output == null)
                        return new MultipleReturn(null, "No default output file set.");
                    s = _output;
                }
                else
                {
                    if (file is LuaTable)
                    {
                        s = (file as LuaTable).GetItemRaw("Stream") as Stream;
                        if (s == null)
                            return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                    }
                    else if (file is Stream)
                    {
                        s = file as Stream;
                    }
                    else
                        return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                }

                try
                {
                    s.Close();
                    return new MultipleReturn((object)_CreateFile(s, o.Environment));
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message, e);
                }
            }
            public static MultipleReturn flush(LuaParameters o)
            {
                object file = RuntimeHelper.GetValue(o[0]);
                Stream s;

                if (file == null)
                {
                    if (_output == null)
                        return new MultipleReturn(null, "No default output file set.");
                    s = _output;
                }
                else
                {
                    if (file is LuaTable)
                    {
                        s = (file as LuaTable).GetItemRaw("Stream") as Stream;
                        if (s == null)
                            return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                    }
                    else if (file is Stream)
                    {
                        s = file as Stream;
                    }
                    else
                        return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                }
                try
                {
                    _output.Flush();
                    return new MultipleReturn((object)_CreateFile(_output, o.Environment));
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message, e);
                }
            }
            public static MultipleReturn input(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj != null)
                {
                    if (obj is string)
                    {
                        Stream s = File.OpenRead(obj as string);
                        _input = s;
                    }
                    else if (obj is LuaTable)
                    {
                        Stream s = (obj as LuaTable).GetItemRaw("Stream") as Stream;
                        if (s == null)
                            throw new InvalidOperationException("First argument to function 'io.input' must be a file-stream or a string path.");

                        _input = s;
                    }
                    else if (obj is Stream)
                    {
                        _input = obj as Stream;
                    }
                    else
                        throw new InvalidOperationException("First argument to function 'io.input' must be a file-stream or a string path.");
                }

                return new MultipleReturn((object)_CreateFile(_input, o.Environment));
            }
            public static MultipleReturn lines(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);
                bool close;
                Stream s;
                int start = 0;

                if (obj is string)
                {
                    if ((obj as string)[0] != '*')
                    {
                        s = File.OpenRead(obj as string);
                        close = true;
                        start = 1;
                    }
                    else
                    {
                        s = _input;
                        close = false;
                        start = 0;
                    }
                }
                else if (obj is LuaTable)
                {
                    s = (obj as LuaTable)._get("Stream") as Stream;
                    if (s == null)
                        throw new ArgumentException("First argument to io.lines must be a file-stream or a file path, make sure to use file:lines.");
                    close = false;
                    start = 1;
                }
                else if (obj is Stream)
                {
                    s = obj as Stream;
                    close = false;
                    start = 1;
                }
                else
                {
                    s = _input;
                    close = false;
                    start = 0;
                }

                int[] a = _parse(o.Cast<object>().Where((o1, i1) => i1 >= start), "io.lines");

                return new MultipleReturn(new LuaMethod((new LinesHelper(close, s, a)).Do, o.Environment));
            }
            public static MultipleReturn open(LuaParameters o)
            {
                string s = RuntimeHelper.GetValue(o[0]) as string;
                string mode = RuntimeHelper.GetValue(o[1]) as string;
                FileMode fileMode;
                FileAccess access;
                bool seek = false;
                mode = mode == null ? null : mode.ToLower(CultureInfo.InvariantCulture);

                if (string.IsNullOrWhiteSpace(s))
                    return new MultipleReturn(null, "First argument must be a string filename.");

                switch (mode)
                {
                    case "r":
                    case "rb":
                    case "":
                    case null:
                        fileMode = FileMode.Open;
                        access = FileAccess.Read;
                        break;
                    case "w":
                    case "wb":
                        fileMode = FileMode.Create;
                        access = FileAccess.Write;
                        break;
                    case "a":
                    case "ab":
                        fileMode = FileMode.OpenOrCreate;
                        access = FileAccess.ReadWrite;
                        seek = true;
                        break;
                    case "r+":
                    case "r+b":
                        fileMode = FileMode.Open;
                        access = FileAccess.ReadWrite;
                        break;
                    case "w+":
                    case "w+b":
                        fileMode = FileMode.Create;
                        access = FileAccess.ReadWrite;
                        break;
                    case "a+":
                    case "a+b":
                        fileMode = FileMode.OpenOrCreate;
                        access = FileAccess.ReadWrite;
                        seek = true;
                        break;
                    default:
                        return new MultipleReturn(null, "Second argument must be a valid string mode.");
                }

                try
                {
                    using (Stream stream = File.Open(s, fileMode, access))
                    {
                        if (seek && stream.CanSeek)
                            stream.Seek(0, SeekOrigin.End);

                        return new MultipleReturn((object)_CreateFile(stream, o.Environment));
                    }
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message, e);
                }
            }
            public static MultipleReturn output(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj != null)
                {
                    if (obj is string)
                    {
                        Stream s = File.OpenRead(obj as string);
                        _output = s;
                    }
                    else if (obj is LuaTable)
                    {
                        Stream s = (obj as LuaTable).GetItemRaw("Stream") as Stream;
                        if (s == null)
                            throw new InvalidOperationException("First argument to function 'io.output' must be a file-stream or a string path.");

                        _output = s;
                    }
                    else if (obj is Stream)
                    {
                        _output = obj as Stream;
                    }
                    else
                        throw new InvalidOperationException("First argument to function 'io.output' must be a file-stream or a string path.");
                }

                return new MultipleReturn((object)_CreateFile(_output, o.Environment));
            }
            public static MultipleReturn read(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);
                Stream s;
                int start = 0;

                if (obj is LuaTable)
                {
                    s = (obj as LuaTable)._get("Stream") as Stream;
                    if (s == null)
                        throw new ArgumentException("First argument to io.read must be a file-stream or a file path, make sure to use file:read.");
                    start = 1;
                }
                else if (obj is Stream)
                {
                    s = obj as Stream;
                    start = 1;
                }
                else
                {
                    s = _input;
                    start = 0;
                }

                int[] a = _parse(o.Cast<object>().Where((o1, i1) => i1 >= start), "io.read");

                return _read(a, new StreamReader(s));
            }
            public static MultipleReturn seek(LuaParameters o)
            {
                Stream s = RuntimeHelper.GetValue(o[0]) as Stream;
                SeekOrigin origin = SeekOrigin.Current;
                long off = 0;

                if (s == null)
                {
                    LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                    if (table != null)
                        s = table._get("Stream") as Stream;

                    if (s == null)
                        throw new ArgumentException("First real argument to function file:seek must be a file-stream, make sure to use file:seek.");
                }

                if (o.Count > 1)
                {
                    string str = RuntimeHelper.GetValue(o[1]) as string;
                    if (str == "set")
                        origin = SeekOrigin.Begin;
                    else if (str == "cur")
                        origin = SeekOrigin.Current;
                    else if (str == "end")
                        origin = SeekOrigin.End;
                    else
                        throw new ArgumentException("First argument to function file:seek must be a string.");

                    if (o.Count > 2)
                    {
                        object obj = RuntimeHelper.GetValue(o[2]);
                        if (obj is double)
                            off = Convert.ToInt64((double)obj);
                        else
                            throw new ArgumentException("Second argument to function file:seek must be a number.");
                    }
                }

                if (!s.CanSeek)
                    return new MultipleReturn(null, "Specified stream cannot be seeked.");

                try
                {
                    return new MultipleReturn(Convert.ToDouble(s.Seek(off, origin)));
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message, e);
                }
            }
            public static MultipleReturn tmpfile(LuaParameters o)
            {
                string str = Path.GetTempFileName();
                Stream s = File.Open(str, FileMode.OpenOrCreate, FileAccess.ReadWrite);

                Remove.Add(s);
                return new MultipleReturn(_CreateFile(s, o.Environment));
            }
            public static MultipleReturn type(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj is Stream)
                {
                    return new MultipleReturn((object)"file");
                }
                else if (obj is LuaTable)
                {
                    Stream s = (obj as LuaTable)._get("Stream") as Stream;
                    return new MultipleReturn((object)(s == null ? null : "file"));
                }
                else
                    return new MultipleReturn(null);
            }
            public static MultipleReturn write(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);
                Stream s;
                int start = 0;

                if (obj is LuaTable)
                {
                    s = (obj as LuaTable)._get("Stream") as Stream;
                    if (s == null)
                        return new MultipleReturn(null, "First argument must be a file-stream or a file path.");
                    start = 1;
                }
                else if (obj is Stream)
                {
                    s = obj as Stream;
                    start = 1;
                }
                else
                {
                    s = _output;
                    start = 0;
                }

                try
                {
                    for (int i = start; i < o.Count; i++)
                    {
                        obj = RuntimeHelper.GetValue(o[i]);
                        if (obj is double)
                        {
                            var bt = (o.Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(((double)obj).ToString(CultureInfo.InvariantCulture));
                            s.Write(bt);
                        }
                        else if (obj is string)
                        {
                            var bt = (o.Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(obj as string);
                            s.Write(bt);
                        }
                        else
                            throw new ArgumentException("Arguments to io.write must be a string or number.");
                    }

                    return new MultipleReturn(_CreateFile(s, o.Environment));
                }
                catch (ArgumentException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message, e);
                }
            }

            /* helper functions */
            static int[] _parse(IEnumerable args, string func)
            {
                List<int> v = new List<int>();

                foreach (var item in args)
                {
                    object obj = RuntimeHelper.GetValue(item);
                    if (obj is double)
                    {
                        double d = (double)obj;
                        if (d < 0)
                            throw new ArgumentOutOfRangeException("Arguments to " + func + " must be a positive integer.", (Exception)null);

                        v.Add(Convert.ToInt32(d));
                    }
                    else if (obj is string)
                    {
                        string st = obj as string;

                        if (st == "*n")
                            v.Add(-1);
                        else if (st == "*a")
                            v.Add(-2);
                        else if (st == "*l")
                            v.Add(-3);
                        else if (st == "*L")
                            v.Add(-4);
                        else
                            throw new ArgumentException("Only the following strings are valid as arguments to " + func + ": '*n', '*a', '*l', or '*L'.");
                    }
                    else
                        throw new ArgumentException("Arguments to function " + func + " must be a number or a string.");
                }

                return v.ToArray();
            }
            static MultipleReturn _read(int[] opts, StreamReader s)
            {
                List<object> ret = new List<object>();

                foreach (var item in opts)
                {
                    switch (item)
                    {
                        case -4:
                            ret.Add(s.EndOfStream ? null : s.ReadLine() + "\n");
                            break;
                        case -3:
                            ret.Add(s.EndOfStream ? null : s.ReadLine());
                            break;
                        case -2:
                            ret.Add(s.EndOfStream ? null : s.ReadToEnd());
                            break;
                        case -1:
                            if (s.EndOfStream)
                                ret.Add(null);
                            else
                            {
                                long pos = 0;
                                double? d = RuntimeHelper.ReadNumber(s, null, 0, 0, ref pos);
                                if (d.HasValue)
                                    ret.Add(d.Value);
                                else
                                    ret.Add(null);
                            }
                            break;
                        default:
                            if (s.EndOfStream)
                                ret.Add(null);
                            else
                            {
                                char[] c = new char[item];
                                s.Read(c, 0, item);
                                ret.Add(new string(c));
                            }
                            break;
                    }
                }

                return new MultipleReturn(ret);
            }
            static LuaTable _CreateFile(Stream backing, LuaEnvironment E)
            {
                LuaTable ret = new LuaTable();
                ret.SetItemRaw("Stream", backing);
                ret.SetItemRaw("close", new LuaMethod(close, E));
                ret.SetItemRaw("flush", new LuaMethod(flush, E));
                ret.SetItemRaw("lines", new LuaMethod(lines, E));
                ret.SetItemRaw("read", new LuaMethod(read, E));
                ret.SetItemRaw("seek", new LuaMethod(seek, E));
                ret.SetItemRaw("write", new LuaMethod(write, E));

                return ret;
            }

            /* global functions */
            public static MultipleReturn dofile(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'dofile'.");

                string file = RuntimeHelper.GetValue(o[0]) as string;

                if (string.IsNullOrEmpty(file))
                    throw new ArgumentException("First argument to 'loadfile' must be a file path.");
                if (!File.Exists(file))
                    throw new FileNotFoundException("Unable to locate file at '" + file + "'.");

                string chunk = File.ReadAllText(file);
                var r = PlainParser.LoadChunk(o.Environment, chunk);
                return new MultipleReturn((IEnumerable)r.Execute());
            }
            public static MultipleReturn load(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'load'.");

                object ld = RuntimeHelper.GetValue(o[0]);
                LuaTable env = RuntimeHelper.GetValue(o[3]) as LuaTable;
                string chunk;

                if (ld is LuaMethod)
                {
                    chunk = "";
                    while (true)
                    {
                        var ret = (ld as LuaMethod).InvokeInternal(new object[0], -1);
                        if (ret[0] is string)
                        {
                            if (string.IsNullOrEmpty(ret[0] as string))
                                break;
                            else
                                chunk += ret[0] as string;
                        }
                        else
                            break;
                    }
                }
                else if (ld is string)
                {
                    chunk = ld as string;
                }
                else
                    throw new ArgumentException("First argument to 'load' must be a string or a method.");

                try
                {
                    return new MultipleReturn(PlainParser.LoadChunk(o.Environment, chunk).ToMethod());
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message);
                }
            }
            public static MultipleReturn loadfile(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'loadfile'.");

                string file = RuntimeHelper.GetValue(o[0]) as string;
                string mode = RuntimeHelper.GetValue(o[1]) as string;

                if (string.IsNullOrEmpty(file))
                    throw new ArgumentException("First argument to 'loadfile' must be a file path.");
                if (!File.Exists(file))
                    throw new FileNotFoundException("Unable to locate file at '" + file + "'.");
                if (string.IsNullOrEmpty(mode) && o.Count > 1)
                    throw new ArgumentException("Second argument to 'loadfile' must be a string mode.");
                if (mode != "t")
                    throw new ArgumentException("The only mode supported by loadfile is 't'.");

                string chunk = File.ReadAllText(file);
                try
                {
                    return new MultipleReturn(PlainParser.LoadChunk(o.Environment, chunk).ToMethod());
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message);
                }
            }
        }
        static class Math
        {
            public sealed class MathHelper
            {
                string name;
                Func<double, double> f1;
                Func<double, double, double> f2;

                MathHelper(string name, Func<double, double> func)
                {
                    this.name = name;
                    this.f1 = func;
                    this.f2 = null;
                }
                MathHelper(string name, Func<double, double, double> func)
                {
                    this.name = name;
                    this.f1 = null;
                    this.f2 = func;
                }

                MultipleReturn Do(LuaParameters o)
                {
                    if (o.Count < (f1 == null ? 2 : 1))
                        throw new ArgumentException("Expecting " + (f1 == null ? "two" : "one") + " argument to function '" + name + "'.");

                    object obj = RuntimeHelper.GetValue(o[0]);
                    object obj1 = RuntimeHelper.GetValue(o[1]);

                    if (obj == null || !(obj is double))
                        throw new ArgumentException("First argument to '" + name + "' must be a number.");
                    if ((obj1 == null || !(obj1 is double)) && f1 == null)
                        throw new ArgumentException("Second argument to '" + name + "' must be a number.");

                    return new MultipleReturn(f1 == null ? f2((double)obj, (double)obj1) : f1((double)obj));
                }

                public static LuaMethod Create(string name, Func<double, double> func, LuaEnvironment E)
                {
                    return new LuaMethod(new MathHelper(name, func).Do, E);
                }
                public static LuaMethod Create(string name, Func<double, double, double> func, LuaEnvironment E)
                {
                    return new LuaMethod(new MathHelper(name, func).Do, E);
                }
            }

            public static MultipleReturn deg(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'math.deg'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.deg' must be a number.");

                return new MultipleReturn(((double)obj * 180 / System.Math.PI));
            }
            public static MultipleReturn frexp(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'math.frexp'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.frexp' must be a number.");

                double d = (double)obj;
                double m, e;

                if (d == 0)
                {
                    m = 0;
                    e = 0;
                    return new MultipleReturn(m, e);
                }

                bool b = d < 0;
                d = b ? -d : d;
                e = System.Math.Ceiling(System.Math.Log(d, 2));
                m = d / System.Math.Pow(2, e);
                m = b ? -m : m;

                return new MultipleReturn(m, e);
            }
            public static MultipleReturn ldexp(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'math.ldexp'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                object obj2 = RuntimeHelper.GetValue(o[1]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.ldexp' must be a number.");
                if (obj2 == null || !(obj2 is double) || System.Math.Floor((double)obj2) != System.Math.Ceiling((double)obj2))
                    throw new ArgumentException("Second argument to 'math.ldexp' must be a integer.");

                return new MultipleReturn((double)obj * System.Math.Pow(2.0, (double)obj2));
            }
            public static MultipleReturn log(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'math.log'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                object obj2 = RuntimeHelper.GetValue(o[1]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.log' must be a number.");
                if (obj2 != null && !(obj2 is double))
                    throw new ArgumentException("Second argument to 'math.log' must be a number.");

                if (obj2 != null)
                    return new MultipleReturn(System.Math.Log((double)obj, (double)obj2));
                else
                    return new MultipleReturn(System.Math.Log((double)obj));
            }
            public static MultipleReturn max(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'math.max'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.max' must be a number.");

                double ret = (double)obj;

                for (int i = 1; i < o.Count; i++)
                {
                    object obj2 = RuntimeHelper.GetValue(o[0]);
                    if (obj2 == null || !(obj2 is double))
                        throw new ArgumentException("Argument number '" + i + "' to 'math.max' must be a number.");

                    double d = (double)obj2;
                    if (d > ret)
                        ret = d;
                }

                return new MultipleReturn(ret);
            }
            public static MultipleReturn min(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'math.min'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.min' must be a number.");

                double ret = (double)obj;

                for (int i = 1; i < o.Count; i++)
                {
                    object obj2 = RuntimeHelper.GetValue(o[0]);
                    if (obj2 == null || !(obj2 is double))
                        throw new ArgumentException("Argument number '" + i + "' to 'math.min' must be a number.");

                    double d = (double)obj2;
                    if (d < ret)
                        ret = d;
                }

                return new MultipleReturn(ret);
            }
            public static MultipleReturn modf(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'math.modf'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.modf' must be a number.");

                double d = (double)obj;
                return new MultipleReturn(System.Math.Floor(d), (d - System.Math.Floor(d)));
            }
            public static MultipleReturn rad(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'math.rad'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.rad' must be a number.");

                return new MultipleReturn(((double)obj * System.Math.PI / 180));
            }
            public static MultipleReturn random(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'math.random'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                object obj2 = RuntimeHelper.GetValue(o[1]);
                object obj3 = RuntimeHelper.GetValue(o[2]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.random' must be a number.");
                if (obj2 != null && !(obj2 is double))
                    throw new ArgumentException("Second argument to 'math.random' must be a number.");
                if (obj3 != null && !(obj3 is double))
                    throw new ArgumentException("Third argument to 'math.random' must be a number.");

                if (obj2 == null)
                {
                    lock (_randLock)
                        return new MultipleReturn(Rand.NextDouble());
                }
                else
                {
                    double m = (double)obj2;
                    if (obj3 == null)
                    {
                        lock (_randLock)
                            return new MultipleReturn(Rand.NextDouble() * m);
                    }
                    else
                    {
                        double n = (double)obj3;

                        lock (_randLock)
                            return new MultipleReturn(Rand.NextDouble() * (n - m) + m);
                    }
                }
            }
            public static MultipleReturn randomseed(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'math.randomseed'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double) || System.Math.Floor((double)obj) != System.Math.Ceiling((double)obj))
                    throw new ArgumentException("First argument to 'math.randomseed' must be an integer.");

                lock (_randLock)
                {
                    Rand = new Random((int)(double)obj);
                }
                return new MultipleReturn();
            }

            static Random Rand = new Random(Guid.NewGuid().GetHashCode());
            static object _randLock = new object();
        }
        static class Coroutine
        {
            sealed class WrapHelper
            {
                LuaThread thread;

                public WrapHelper(LuaThread thread)
                {
                    this.thread = thread;
                }

                public MultipleReturn Do(LuaParameters o)
                {
                    object[] obj = thread.ResumeInternal(o.Cast<object>().ToArray());
                    if (obj[0] as bool? == false)
                        throw (Exception)obj[2];
                    else
                        return new MultipleReturn(obj.Where((ob, i) => i > 0));
                }
            }

            public static MultipleReturn create(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'coroutine.create'.");

                LuaMethod meth = o[0] as LuaMethod;
                if (meth == null)
                    throw new ArgumentException("First argument to function 'coroutine.create' must be a function.");

                return new MultipleReturn(new LuaThread(meth));
            }
            public static MultipleReturn resume(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'coroutine.resume'.");

                LuaThread meth = o[0] as LuaThread;
                if (meth == null)
                    throw new ArgumentException("First argument to function 'coroutine.resume' must be a thread.");

                return new MultipleReturn((IEnumerable)meth.ResumeInternal(
                    o.Cast<object>()
                        .Where((ot,i) => i > 0)
                        .ToArray()
                    ));
            }
            public static MultipleReturn running(LuaParameters o)
            {
                LuaThread t;
                lock (LuaThread._cache)
                {
                    if (!LuaThread._cache.ContainsKey(Thread.CurrentThread.ManagedThreadId))
                        t = new LuaThread(Thread.CurrentThread);
                    else
                        t = LuaThread._cache[Thread.CurrentThread.ManagedThreadId];
                }

                return new MultipleReturn(t, !t.IsLua);
            }
            public static MultipleReturn status(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'coroutine.status'.");

                LuaThread meth = o[0] as LuaThread;
                if (meth == null)
                    throw new ArgumentException("First argument to function 'coroutine.status' must be a thread.");

                return new MultipleReturn((object)meth.Status.ToString().ToLower(CultureInfo.InvariantCulture));
            }
            public static MultipleReturn wrap(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'coroutine.wrap'.");

                LuaMethod meth = o[0] as LuaMethod;
                if (meth == null)
                    throw new ArgumentException("First argument to function 'coroutine.wrap' must be a function.");

                return new MultipleReturn(new LuaMethod(new WrapHelper(new LuaThread(meth)).Do, o.Environment));
            }
            public static MultipleReturn yield(LuaParameters o)
            {
                LuaThread t;
                lock (LuaThread._cache)
                {
                    if (!LuaThread._cache.ContainsKey(Thread.CurrentThread.ManagedThreadId))
                        throw new InvalidOperationException("Cannot yield the main thread.");

                    t = LuaThread._cache[Thread.CurrentThread.ManagedThreadId];
                }

                return new MultipleReturn((IEnumerable)t.Yield(o.Cast<object>().ToArray()));
            }
        }
        static class OS
        {
            static Stopwatch stop = Stopwatch.StartNew();

            public static MultipleReturn clock(LuaParameters o)
            {
                return new MultipleReturn(stop.Elapsed.TotalSeconds);
            }
            public static MultipleReturn date(LuaParameters o)
            {
                string format = RuntimeHelper.GetValue(o[0]) as string ?? "%c";
                object obj = RuntimeHelper.GetValue(o[1]);
                DateTimeOffset time;

                if (obj is double)
                    time = new DateTime(Convert.ToInt64((double)obj));
                else if (obj is DateTime)
                    time = (DateTime)obj;
                else if (obj is DateTimeOffset)
                    time = ((DateTimeOffset)obj);
                else
                    time = DateTimeOffset.Now;

                if (format.Length > 0 && format[0] == '!')
                {
                    format = format.Substring(1);
                    time = time.ToUniversalTime();
                }

                if (format == "*t")
                {
                    LuaTable tab = new LuaTable();
                    tab.SetItemRaw("year", Convert.ToDouble(time.Year));
                    tab.SetItemRaw("month", Convert.ToDouble(time.Month));
                    tab.SetItemRaw("day", Convert.ToDouble(time.Day));
                    tab.SetItemRaw("hour", Convert.ToDouble(time.Hour));
                    tab.SetItemRaw("min", Convert.ToDouble(time.Minute));
                    tab.SetItemRaw("sec", Convert.ToDouble(time.Second));
                    tab.SetItemRaw("wday", Convert.ToDouble(((int)time.DayOfWeek) + 1));
                    tab.SetItemRaw("yday", Convert.ToDouble(time.DayOfYear));

                    return new MultipleReturn((object)tab);
                }

                StringBuilder ret = new StringBuilder();
                for (int i = 0; i < format.Length; i++)
                {
                    if (format[i] == '%')
                    {
                        i++;
                        switch (format[i])
                        {
                            case 'a':
                                ret.Append(time.ToString("ddd", CultureInfo.CurrentCulture));
                                break;
                            case 'A':
                                ret.Append(time.ToString("dddd", CultureInfo.CurrentCulture));
                                break;
                            case 'b':
                                ret.Append(time.ToString("MMM", CultureInfo.CurrentCulture));
                                break;
                            case 'B':
                                ret.Append(time.ToString("MMMM", CultureInfo.CurrentCulture));
                                break;
                            case 'c':
                                ret.Append(time.ToString("F", CultureInfo.CurrentCulture));
                                break;
                            case 'd':
                                ret.Append(time.ToString("dd", CultureInfo.CurrentCulture));
                                break;
                            case 'H':
                                ret.Append(time.ToString("HH", CultureInfo.CurrentCulture));
                                break;
                            case 'I':
                                ret.Append(time.ToString("hh", CultureInfo.CurrentCulture));
                                break;
                            case 'j':
                                ret.Append(time.DayOfYear.ToString("d3", CultureInfo.CurrentCulture));
                                break;
                            case 'm':
                                ret.Append(time.Month.ToString("d2", CultureInfo.CurrentCulture));
                                break;
                            case 'M':
                                ret.Append(time.Minute.ToString("d2", CultureInfo.CurrentCulture));
                                break;
                            case 'p':
                                ret.Append(time.ToString("tt", CultureInfo.CurrentCulture));
                                break;
                            case 'S':
                                ret.Append(time.ToString("ss", CultureInfo.CurrentCulture));
                                break;
                            case 'U':
                                throw new NotSupportedException("The %U format specifier is not supported by this framework.");
                            case 'w':
                                ret.Append((int)time.DayOfWeek);
                                break;
                            case 'W':
                                throw new NotSupportedException("The %W format specifier is not supported by this framework.");
                            case 'x':
                                ret.Append(time.ToString("d", CultureInfo.CurrentCulture));
                                break;
                            case 'X':
                                ret.Append(time.ToString("T", CultureInfo.CurrentCulture));
                                break;
                            case 'y':
                                ret.Append(time.ToString("yy", CultureInfo.CurrentCulture));
                                break;
                            case 'Y':
                                ret.Append(time.ToString("yyyy", CultureInfo.CurrentCulture));
                                break;
                            case 'Z':
                                ret.Append(time.ToString("%K", CultureInfo.CurrentCulture));
                                break;
                            case '%':
                                ret.Append('%');
                                break;
                            default:
                                throw new ArgumentException("Unrecognised format specifier %" + format[i] + " in function 'os.date'.");
                        }
                    }
                    else
                        ret.Append(format[i]);
                }
                return new MultipleReturn((object)ret.ToString());
            }
            public static MultipleReturn difftime(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'os.difftime'.");

                object obj1 = RuntimeHelper.GetValue(o[0]);
                object obj2 = RuntimeHelper.GetValue(o[1]);

                if (obj1 == null || !(obj1 is double))
                    throw new ArgumentException("First argument to function 'os.difftime' must be a number.");
                if (obj2 == null || !(obj2 is double))
                    throw new ArgumentException("Second argument to function 'os.difftime' must be a number.");

                double d2 = (double)obj1, d1 = (double)obj2;

                return new MultipleReturn(d2 - d1);
            }
            public static MultipleReturn exit(LuaParameters o)
            {
                object code = RuntimeHelper.GetValue(o[0]);
                object close = RuntimeHelper.GetValue(o[1]);

                o.Environment.Settings.CallQuit(o.Environment, code, close);
                return new MultipleReturn();
            }
            public static MultipleReturn getenv(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'os.getenv'.");

                string var = RuntimeHelper.GetValue(o[0]) as string;

                if (var == null)
                    throw new ArgumentException("First argument to function 'os.getenv' must be a string.");

                var ret = Environment.GetEnvironmentVariable(var);
                return new MultipleReturn((object)ret);
            }
            public static MultipleReturn remove(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'os.remove'.");

                string var = RuntimeHelper.GetValue(o[0]) as string;

                if (var == null)
                    throw new ArgumentException("First argument to function 'os.remove' must be a string.");

                if (File.Exists(var))
                {
                    try
                    {
                        File.Delete(var);
                        return new MultipleReturn(true);
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
                else if (Directory.Exists(var))
                {
                    if (Directory.EnumerateFileSystemEntries(var).Count() > 0)
                        return new MultipleReturn(null, "Specified directory is not empty.");

                    try
                    {
                        Directory.Delete(var);
                        return new MultipleReturn(true);
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
                else
                    return new MultipleReturn(null, "Specified filename does not exist.");
            }
            public static MultipleReturn rename(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'os.rename'.");

                string old = RuntimeHelper.GetValue(o[0]) as string;
                string neww = RuntimeHelper.GetValue(o[1]) as string;

                if (old == null)
                    throw new ArgumentException("First argument to function 'os.rename' must be a string.");
                if (neww == null)
                    throw new ArgumentException("Second argument to function 'os.rename' must be a string.");

                if (File.Exists(old))
                {
                    try
                    {
                        File.Move(old, neww);
                        return new MultipleReturn(true);
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
                else if (Directory.Exists(old))
                {
                    try
                    {
                        Directory.Move(old, neww);
                        return new MultipleReturn(true);
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
                else
                    return new MultipleReturn(null, "Specified path does not exist.");
            }
            public static MultipleReturn setlocale(LuaParameters o)
            {
                string locale = RuntimeHelper.GetValue(o[0]) as string;

                if (locale == null)
                    return new MultipleReturn((object)Thread.CurrentThread.CurrentCulture.Name);
                else
                {
                    try
                    {
                        CultureInfo ci = CultureInfo.GetCultureInfo(locale);
                        if (ci == null)
                            return new MultipleReturn();

                        Thread.CurrentThread.CurrentCulture = ci;
                        return new MultipleReturn((object)ci.Name);
                    }
                    catch (Exception)
                    {
                        return new MultipleReturn();
                    }
                }
            }
            public static MultipleReturn time(LuaParameters o)
            {
                DateTime time;
                object table = RuntimeHelper.GetValue(o[0]);

                if (table != null)
                {
                    if (table is LuaTable)
                    {
                        LuaTable t = table as LuaTable;
                        int year, month, day, hour, min, sec;

                        object obj = RuntimeHelper.GetValue(t._get("year"));
                        if (!(obj is double))
                            throw new ArgumentException("First argument to function 'os.time' is not a valid time table.");
                        year = Convert.ToInt32((double)obj);

                        obj = RuntimeHelper.GetValue(t._get("month"));
                        if (!(obj is double))
                            throw new ArgumentException("First argument to function 'os.time' is not a valid time table.");
                        month = Convert.ToInt32((double)obj);

                        obj = RuntimeHelper.GetValue(t._get("day"));
                        if (!(obj is double))
                            throw new ArgumentException("First argument to function 'os.time' is not a valid time table.");
                        day = Convert.ToInt32((double)obj);

                        obj = RuntimeHelper.GetValue(t._get("hour"));
                        if (obj is double)
                            hour = Convert.ToInt32((double)obj);
                        else
                            hour = 12;

                        obj = RuntimeHelper.GetValue(t._get("min"));
                        if (obj is double)
                            min = Convert.ToInt32((double)obj);
                        else
                            min = 0;

                        obj = RuntimeHelper.GetValue(t._get("sec"));
                        if (obj is double)
                            sec = Convert.ToInt32((double)obj);
                        else
                            sec = 0;

                        time = new DateTime(year, month, day, hour, min, sec);
                    }
                    else if (table is DateTime)
                        time = (DateTime)table;
                    else if (table is DateTimeOffset)
                        time = ((DateTimeOffset)table).LocalDateTime;
                    else
                        throw new ArgumentException("First argument to function 'os.time' must be a table.");
                }
                else
                    time = DateTime.Now;

                return new MultipleReturn(Convert.ToDouble(time.Ticks));
            }
            public static MultipleReturn tmpname(LuaParameters o)
            {
                return new MultipleReturn((object)Path.GetTempFileName());
            }
        }

        public static void Initialize(LuaEnvironment E)
        {
            LuaLibraries libraries = E.Settings.Libraries;
            LuaTable _globals = E._globals;

            _globals.SetItemRaw("assert", new LuaMethod(Standard.assert, E));
            _globals.SetItemRaw("collectgarbage", new LuaMethod(Standard.collectgarbage, E));
            _globals.SetItemRaw("error", new LuaMethod(Standard.error, E));
            _globals.SetItemRaw("getmetatable", new LuaMethod(Standard.getmetatable, E));
            _globals.SetItemRaw("ipairs", new LuaMethod(Standard.ipairs, E));
            _globals.SetItemRaw("next", new LuaMethod(Standard.next, E));
            _globals.SetItemRaw("pairs", new LuaMethod(Standard.pairs, E));
            _globals.SetItemRaw("pcall", new LuaMethod(Standard.pcall, E));
            _globals.SetItemRaw("print", new LuaMethod(Standard.print, E));
            _globals.SetItemRaw("rawequal", new LuaMethod(Standard.rawequal, E));
            _globals.SetItemRaw("rawget", new LuaMethod(Standard.rawget, E));
            _globals.SetItemRaw("rawlen", new LuaMethod(Standard.rawlen, E));
            _globals.SetItemRaw("rawset", new LuaMethod(Standard.rawset, E));
            _globals.SetItemRaw("select", new LuaMethod(Standard.select, E));
            _globals.SetItemRaw("setmetatable", new LuaMethod(Standard.setmetatable, E));
            _globals.SetItemRaw("tonumber", new LuaMethod(Standard.tonumber, E));
            _globals.SetItemRaw("tostring", new LuaMethod(Standard.tostring, E));
            _globals.SetItemRaw("type", new LuaMethod(Standard.type, E));
            _globals.SetItemRaw("_VERSION", Standard._VERSION);
            _globals.SetItemRaw("_NET", Standard._NET);

            #region if LuaLibraries.IO
            if (libraries.HasFlag(LuaLibraries.IO))
            {
                IO._Init(E);
                LuaTable io = new LuaTable();

                io.SetItemRaw("close", new LuaMethod(IO.close, E));
                io.SetItemRaw("flush", new LuaMethod(IO.flush, E));
                io.SetItemRaw("input", new LuaMethod(IO.input, E));
                io.SetItemRaw("lines", new LuaMethod(IO.lines, E));
                io.SetItemRaw("open", new LuaMethod(IO.open, E));
                io.SetItemRaw("output", new LuaMethod(IO.output, E));
                io.SetItemRaw("read", new LuaMethod(IO.read, E));
                io.SetItemRaw("tmpfile", new LuaMethod(IO.tmpfile, E));
                io.SetItemRaw("type", new LuaMethod(IO.type, E));
                io.SetItemRaw("write", new LuaMethod(IO.write, E));

                _globals.SetItemRaw("dofile", new LuaMethod(IO.dofile, E));
                _globals.SetItemRaw("load", new LuaMethod(IO.load, E));
                _globals.SetItemRaw("loadfile", new LuaMethod(IO.loadfile, E));
                _globals.SetItemRaw("io", io);
            }
            #endregion
            #region if LuaLibraries.String
            if (libraries.HasFlag(LuaLibraries.String))
            {
                LuaTable str = new LuaTable();
                str.SetItemRaw("byte", new LuaMethod(String.Byte, E));
                str.SetItemRaw("char", new LuaMethod(String.Char, E));
                str.SetItemRaw("find", new LuaMethod(String.find, E));
                str.SetItemRaw("format", new LuaMethod(String.foramt, E));
                str.SetItemRaw("gmatch", new LuaMethod(String.gmatch, E));
                str.SetItemRaw("gsub", new LuaMethod(String.gsub, E));
                str.SetItemRaw("len", new LuaMethod(String.len, E));
                str.SetItemRaw("lower", new LuaMethod(String.lower, E));
                str.SetItemRaw("match", new LuaMethod(String.match, E));
                str.SetItemRaw("rep", new LuaMethod(String.rep, E));
                str.SetItemRaw("reverse", new LuaMethod(String.reverse, E));
                str.SetItemRaw("sub", new LuaMethod(String.sub, E));
                str.SetItemRaw("upper", new LuaMethod(String.upper, E));

                _globals.SetItemRaw("string", str);
            }
            #endregion
            #region if LuaLibraries.Math
            if (libraries.HasFlag(LuaLibraries.Math))
            {
                LuaTable math = new LuaTable();
                math.SetItemRaw("abs", Math.MathHelper.Create("math.abs", System.Math.Abs, E));
                math.SetItemRaw("acos", Math.MathHelper.Create("math.acos", System.Math.Acos, E));
                math.SetItemRaw("asin", Math.MathHelper.Create("math.asin", System.Math.Asin, E));
                math.SetItemRaw("atan", Math.MathHelper.Create("math.atan", System.Math.Atan, E));
                math.SetItemRaw("atan2", Math.MathHelper.Create("math.atan2", System.Math.Atan2, E));
                math.SetItemRaw("ceil", Math.MathHelper.Create("math.ceil", System.Math.Ceiling, E));
                math.SetItemRaw("cos", Math.MathHelper.Create("math.cos", System.Math.Cos, E));
                math.SetItemRaw("cosh", Math.MathHelper.Create("math.cosh", System.Math.Cosh, E));
                math.SetItemRaw("deg", new LuaMethod(Math.deg, E));
                math.SetItemRaw("exp", Math.MathHelper.Create("math.exp", System.Math.Exp, E));
                math.SetItemRaw("floor", Math.MathHelper.Create("math.floor", System.Math.Floor, E));
                math.SetItemRaw("fmod", Math.MathHelper.Create("math.fmod", System.Math.IEEERemainder, E));
                math.SetItemRaw("frexp", new LuaMethod(Math.frexp, E));
                math.SetItemRaw("huge", double.PositiveInfinity);
                math.SetItemRaw("ldexp", new LuaMethod(Math.ldexp, E));
                math.SetItemRaw("log", new LuaMethod(Math.log, E));
                math.SetItemRaw("max", new LuaMethod(Math.max, E));
                math.SetItemRaw("min", new LuaMethod(Math.min, E));
                math.SetItemRaw("modf", new LuaMethod(Math.modf, E));
                math.SetItemRaw("pi", System.Math.PI);
                math.SetItemRaw("pow", Math.MathHelper.Create("math.pow", System.Math.Pow, E));
                math.SetItemRaw("rad", new LuaMethod(Math.rad, E));
                math.SetItemRaw("random", new LuaMethod(Math.random, E));
                math.SetItemRaw("randomseed", new LuaMethod(Math.randomseed, E));
                math.SetItemRaw("sin", Math.MathHelper.Create("math.sin", System.Math.Sin, E));
                math.SetItemRaw("sinh", Math.MathHelper.Create("math.sinh", System.Math.Sinh, E));
                math.SetItemRaw("sqrt", Math.MathHelper.Create("math.sqrt", System.Math.Sqrt, E));
                math.SetItemRaw("tan", Math.MathHelper.Create("math.tan", System.Math.Tan, E));
                math.SetItemRaw("tanh", Math.MathHelper.Create("math.tanh", System.Math.Tanh, E));

                _globals.SetItemRaw("math", math);
            }
            #endregion
            #region if LuaLibraries.Coroutine
            if (libraries.HasFlag(LuaLibraries.Coroutine))
            {
                LuaTable coroutine = new LuaTable();
                coroutine.SetItemRaw("create", new LuaMethod(Coroutine.create, E));
                coroutine.SetItemRaw("resume", new LuaMethod(Coroutine.resume, E));
                coroutine.SetItemRaw("running", new LuaMethod(Coroutine.running, E));
                coroutine.SetItemRaw("status", new LuaMethod(Coroutine.status, E));
                coroutine.SetItemRaw("wrap", new LuaMethod(Coroutine.wrap, E));
                coroutine.SetItemRaw("yield", new LuaMethod(Coroutine.yield, E));

                _globals.SetItemRaw("coroutine", coroutine);
            }
            #endregion
            #region if LuaLibraries.OS
            if (libraries.HasFlag(LuaLibraries.OS))
            {
                LuaTable os = new LuaTable();
                os.SetItemRaw("clock", new LuaMethod(OS.clock, E));
                os.SetItemRaw("date", new LuaMethod(OS.date, E));
                os.SetItemRaw("difftime", new LuaMethod(OS.difftime, E));
                os.SetItemRaw("exit", new LuaMethod(OS.exit, E));
                os.SetItemRaw("getenv", new LuaMethod(OS.getenv, E));
                os.SetItemRaw("remove", new LuaMethod(OS.remove, E));
                os.SetItemRaw("rename", new LuaMethod(OS.rename, E));
                os.SetItemRaw("setlocale", new LuaMethod(OS.setlocale, E));
                os.SetItemRaw("time", new LuaMethod(OS.time, E));
                os.SetItemRaw("tmpname", new LuaMethod(OS.tmpname, E));

                _globals.SetItemRaw("os", os);
            }
            #endregion
            #region if LuaLibraries.Bit32
            if (libraries.HasFlag(LuaLibraries.Bit32))
            {
                LuaTable bit32 = new LuaTable();
                bit32.SetItemRaw("arshift", new LuaMethod(Bit32.arshift, E));
                bit32.SetItemRaw("band", new LuaMethod(Bit32.band, E));
                bit32.SetItemRaw("bnot", new LuaMethod(Bit32.bnot, E));
                bit32.SetItemRaw("bor", new LuaMethod(Bit32.bor, E));
                bit32.SetItemRaw("btest", new LuaMethod(Bit32.btest, E));
                bit32.SetItemRaw("bxor", new LuaMethod(Bit32.bxor, E));
                bit32.SetItemRaw("extract", new LuaMethod(Bit32.extract, E));
                bit32.SetItemRaw("replace", new LuaMethod(Bit32.replace, E));
                bit32.SetItemRaw("lrotate", new LuaMethod(Bit32.lrotate, E));
                bit32.SetItemRaw("lshift", new LuaMethod(Bit32.lshift, E));
                bit32.SetItemRaw("rrotate", new LuaMethod(Bit32.rrotate, E));
                bit32.SetItemRaw("rshift", new LuaMethod(Bit32.rshift, E));

                _globals.SetItemRaw("bit32", bit32);
            }
            #endregion
            #region if LuaLibraries.Table
            if (libraries.HasFlag(LuaLibraries.Table))
            {
                LuaTable table = new LuaTable();
                table.SetItemRaw("concat", new LuaMethod(Table.concat, E));
                table.SetItemRaw("insert", new LuaMethod(Table.insert, E));
                table.SetItemRaw("pack", new LuaMethod(Table.pack, E));
                table.SetItemRaw("remove", new LuaMethod(Table.remove, E));
                table.SetItemRaw("sort", new LuaMethod(Table.sort, E));
                table.SetItemRaw("unpack", new LuaMethod(Table.unpack, E));

                _globals.SetItemRaw("table", table);
            }
            #endregion
            #region if LuaLibraries.Module
            if (libraries.HasFlag(LuaLibraries.Modules))
            {
                _globals.SetItemRaw("require", new LuaMethod(Module.require, E));
            }
            #endregion
        }
        static string _type(object o)
        {
            object value = RuntimeHelper.GetValue(o);

            if (value == null)
                return "nil";
            else if (value is string)
                return "string";
            else if (value is double)
                return "number";
            else if (value is bool)
                return "boolean";
            else if (value is LuaTable)
                return "table";
            else if (value is LuaMethod)
                return "function";
            else if (value is LuaThread)
                return "thread";
            else
                return "userdata";
        }
    }
=======
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua.Parser;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Collections;
using System.Threading;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace ModMaker.Lua.Runtime
{
    static class LuaStaticLibraries
    {
        static class Standard
        {
            public const string _VERSION = "Lua 5.2";
            public const string _NET = ".NET 4.0";

            public static MultipleReturn assert(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'assert'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                string message = RuntimeHelper.GetValue(o[0]) as string ?? "assertion failed!";

                if (obj == null || obj as bool? == false)
                    throw new AssertException(message);
                else
                    return new MultipleReturn(obj);
            }
            public static MultipleReturn collectgarbage(LuaParameters o)
            {
                string opt = RuntimeHelper.GetValue(o[0]) as string;
                object arg = RuntimeHelper.GetValue(o[1]);

                if (string.IsNullOrEmpty(opt) && o.Count > 0)
                    throw new ArgumentException("First argument to 'collectgarbage' must be a string.");

                if (opt == "collect" || string.IsNullOrEmpty(opt))
                {
                    int gen = -1;
                    double? d = arg as double?;
                    if (d == 0)
                        gen = 0;
                    else if (d == 1)
                        gen = 1;
                    else if (d == 2)
                        gen = 2;
                    else
                        throw new ArgumentException("Second argument to 'collectgarbage' with opt as 'collect' must be a 0, 1 or 2.");

                    if (gen == -1)
                        GC.Collect();
                    else
                        GC.Collect(gen);

                    return new MultipleReturn();
                }
                else if (opt == "count")
                {
                    double mem = GC.GetTotalMemory(false);

                    return new MultipleReturn(mem, mem % 1024);
                }
                else if (opt == "stop" || opt == "restart" || opt == "step" || opt == "setpause" || opt == "setstepmul" || opt == "generational" || opt == "incremental")
                {
                    throw new ArgumentException("The option '" + opt + "' is not supported by this framework.");
                }
                else if (opt == "isrunning")
                {
                    return new MultipleReturn(true);
                }
                else
                {
                    throw new ArgumentException("The option '" + opt + "' is not recognized to function 'collectgarbage'.");
                }
            }
            public static MultipleReturn error(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'error'.");

                string message = RuntimeHelper.GetValue(o[0]) as string;

                if (message == null)
                    throw new ArgumentException("First argument to function 'error' must be a string.");

                throw new AssertException(message);
            }
            public static MultipleReturn getmetatable(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'getmetatable'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;

                if (table == null)
                    return new MultipleReturn();

                LuaTable meta = table.MetaTable;
                if (meta != null)
                {
                    object meth = meta._get("__metatable");
                    if (meth != null)
                        return new MultipleReturn(meth);
                }

                return new MultipleReturn(meta);
            }
            public static MultipleReturn ipairs(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'ipars'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;

                if (table == null)
                    throw new ArgumentException("First argument to 'iparis' must be a table.");

                LuaTable meta = table.MetaTable;
                if (meta != null)
                {
                    LuaMethod meth = meta._get("__ipairs") as LuaMethod;
                    if (meth != null)
                    {
                        var ret = meth.InvokeInternal(new object[] { table }, -1);
                        return ret.AdjustResults(3);
                    }
                }

                return new MultipleReturn(
                    new LuaMethod(typeof(Standard).GetMethod("_ipairs_itr"), null, "ipairs iterator", o.Environment),
                    table,
                    0);
            }
            public static MultipleReturn next(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'next'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                object index = RuntimeHelper.GetValue(o[1]);
                if (table == null)
                    throw new ArgumentException("First parameter to 'next' must be a table.");

                var t = table.GetNext(index);
                return new MultipleReturn(t.Item1, t.Item2);
            }
            public static MultipleReturn overload(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'overload'.");

                LuaMethod meth = RuntimeHelper.GetValue(o[0]) as LuaMethod;
                object obj = RuntimeHelper.GetValue(o[1]);

                if (meth == null)
                    throw new ArgumentException("First argument to function 'overload' must be a method.");
                if (obj == null || !(obj is double) || (System.Math.Floor((double)obj) != System.Math.Ceiling((double)obj)))
                    throw new ArgumentException("Second argument to function 'overload' must be an integer.");

                int i = Convert.ToInt32((double)obj);

                return meth.InvokeInternal(o.Cast<object>().Where((ob, ind) => ind > 1).ToArray(), i);
            }
            public static MultipleReturn pairs(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'pairs'.");

                object t = RuntimeHelper.GetValue(o[0]);
                if (t is LuaTable)
                {
                    LuaTable table = t as LuaTable;
                    LuaTable meta = table.MetaTable;
                    if (meta != null)
                    {
                        LuaMethod p = meta._get("__pairs") as LuaMethod;
                        if (p != null)
                        {
                            var ret = p.InvokeInternal(new object[] { table }, -1);
                            return ret.AdjustResults(3);
                        }
                    }

                    return new MultipleReturn(
                        new LuaMethod(typeof(Standard).GetMethod("next"), null, "next", o.Environment),
                        table,
                        null);
                }
                else
                    throw new ArgumentException("First argument to 'pairs' must be a table.");
            }
            public static MultipleReturn pcall(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'pcall'.");

                object func = RuntimeHelper.GetValue(o[0]);
                if (func is LuaMethod)
                {
                    try
                    {
                        var ret = (func as LuaMethod).InvokeInternal(o.Cast<object>().Where((obj, i) => i > 0).ToArray(), -1);
                        return new MultipleReturn(new object[] { true }.Union(ret));
                    }
                    catch (ThreadAbortException)
                    {
                        throw;
                    }
                    catch (ThreadInterruptedException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(false, e.Message, e);
                    }
                }
                else
                    throw new ArgumentException("First argument to 'pcall' must be a function.");
            }
            public static MultipleReturn print(LuaParameters o)
            {
                StringBuilder str = new StringBuilder();
                for (int i = 0; i < o.Count; i++)
                {
                    object oo = RuntimeHelper.GetValue(o[i]);
                    if (oo is LuaUserData)
                        oo = (oo as LuaUserData).Value;
                    str.Append((oo ?? "").ToString());
                    str.Append('\t');
                }
                str.Append("\n");

                Stream s = o.Environment.Settings.Stdout;
                byte[] txt = Encoding.UTF8.GetBytes(str.ToString());
                s.Write(txt, 0, txt.Length);

                return new MultipleReturn();
            }
            public static MultipleReturn rawequal(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'rawget'.");

                object v1 = RuntimeHelper.GetValue(o[0]);
                object v2 = RuntimeHelper.GetValue(o[1]);


                return new MultipleReturn(object.Equals(v1, v2));
            }
            public static MultipleReturn rawget(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'rawget'.");

                object table = RuntimeHelper.GetValue(o[0]);
                object index = RuntimeHelper.GetValue(o[1]);

                if (table is LuaTable)
                    return new MultipleReturn((table as LuaTable).GetItemRaw(index));
                else
                    throw new ArgumentException("First argument to function 'rawget' must be a table.");
            }
            public static MultipleReturn rawlen(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'rawlen'.");

                object table = RuntimeHelper.GetValue(o[0]);

                if (table is string)
                    return new MultipleReturn((double)(table as string).Length);
                else if (table is LuaTable)
                    return new MultipleReturn((double)(table as LuaTable).Length);
                else
                    throw new ArgumentException("Argument to 'rawlen' must be a string or table.");
            }
            public static MultipleReturn rawset(LuaParameters o)
            {
                if (o.Count < 3)
                    throw new ArgumentException("Expecting three arguments to function 'rawset'.");

                object table = RuntimeHelper.GetValue(o[0]);
                object index = RuntimeHelper.GetValue(o[1]);
                object value = RuntimeHelper.GetValue(o[2]);

                if (!(table is LuaTable))
                    throw new ArgumentException("First argument to 'rawset' must be a table.");
                if (index == null)
                    throw new ArgumentException("Second argument to 'rawset' cannot be nil.");

                (table as LuaTable).SetItemRaw(index, value);

                return new MultipleReturn(table);
            }
            public static MultipleReturn select(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'select'.");

                object index = RuntimeHelper.GetValue(o[0]);

                if (index as string == "#")
                {
                    return new MultipleReturn((double)(o.Count - 1));
                }
                else if (index is double)
                {
                    double d = (double)index;
                    if (d < 0)
                        d = o.Count + d;
                    return new MultipleReturn(o.Cast<object>().Where((obj, i) => i > d));
                }
                else
                    throw new ArgumentException("First argument to function 'select' must be a number or the string '#'.");
            }
            public static MultipleReturn setmetatable(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'setmetatable'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                object metatable = RuntimeHelper.GetValue(o[1]);

                if (table == null)
                    throw new ArgumentException("First argument to function 'setmetatable' must be a table.");

                if (metatable == null)
                    table.MetaTable = null;
                else if (metatable is LuaTable)
                    table.MetaTable = (metatable as LuaTable);
                else
                    throw new ArgumentException("Attempt to set metatable to a '" + _type(metatable) + "' type.");

                return new MultipleReturn(table);
            }
            public static MultipleReturn tonumber(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'tonumber'.");

                double? d = RuntimeHelper.ToNumber(o[0]);
                if (d.HasValue)
                    return new MultipleReturn(d.Value);
                else
                    return new MultipleReturn();
            }
            public static MultipleReturn tostring(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'tostring'.");

                object val = RuntimeHelper.GetValue(o[0]);
                if (val is LuaTable)
                {
                    LuaTable tab = val as LuaTable;
                    var meta = tab.MetaTable;
                    if (meta != null)
                    {
                        var m = meta._get("__tostring");
                        if (m != null && m is LuaMethod)
                        {
                            var result = (m as LuaMethod).InvokeInternal(new[] { val }, -1);
                            return new MultipleReturn((object)result[0].ToString());
                        }
                    }

                    return new MultipleReturn((object)val.ToString());
                }
                else if (val is LuaUserData)
                    return new MultipleReturn((object)(val as LuaUserData).Value.ToString());
                else
                    return new MultipleReturn((object)(val ?? "").ToString());
            }
            public static MultipleReturn type(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'type'.");

                object value = RuntimeHelper.GetValue(o[0]);

                return new MultipleReturn((object)_type(value));
            }

            static MultipleReturn _ipairs_itr(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to 'ipairs iterator'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                double index = RuntimeHelper.GetValue(o[1]) as double? ?? -1;

                if (table == null)
                    throw new ArgumentException("First argument to function 'ipairs iterator' must be a table.");
                if (index < 0 || System.Math.Floor(index) != System.Math.Ceiling(index))
                    throw new ArgumentException("Second argument to function 'ipairs iterator' must be a positive integer.");
                index++;

                var ret = table._get(index);
                if (ret == null)
                    return new MultipleReturn();
                else
                    return new MultipleReturn(index, ret);
            }
        }
        static class Module
        {
            public static MultipleReturn require(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'require'.");

                string str = RuntimeHelper.GetValue(o[0]) as string;
                if (str == null)
                    throw new ArgumentException("First argument to function 'require' must be a string.");

                var bind = o.Environment.Settings.ModuleBinder;
                object ret = bind.Loaded(str);
                if (ret == null)
                    ret = bind.Load(str, o.Environment);

                if (ret is object[])
                    return new MultipleReturn((IEnumerable)ret);
                else
                    return new MultipleReturn(ret);
            }
        }
        static class Table
        {
            class SortHelper : IComparer<object>
            {
                LuaMethod meth;

                public SortHelper(LuaMethod meth)
                {
                    this.meth = meth;
                }

                public int Compare(object x, object y)
                {
                    if (meth != null)
                    {
                        var ret = meth.InvokeInternal(new[] { x, y }, -1);
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

            public static MultipleReturn concat(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'table.concat'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                string sep = RuntimeHelper.GetValue(o[1]) as string ?? "";
                double i = (RuntimeHelper.GetValue(o[2]) as double? ?? 1);

                if (table == null)
                    throw new ArgumentException("First argument to function 'table.concat' must be a table.");

                double len = table.GetSequenceLen();
                if (len == -1)
                    throw new ArgumentException("Table given to function 'table.concat' is not a valid sequence.");
                double j = (RuntimeHelper.GetValue(o[3]) as double? ?? len);

                if (j > i)
                    return new MultipleReturn((object)"");

                StringBuilder str = new StringBuilder();
                str.Append(table.GetItemRaw(i));
                for (double ii = i + 1; ii < j; ii++)
                {
                    str.Append(sep);
                    str.Append(table.GetItemRaw(ii));
                }

                return new MultipleReturn((object)str.ToString());
            }
            public static MultipleReturn insert(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'table.insert'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                if (table == null)
                    throw new ArgumentException("First argument to function 'table.insert' must be a table.");

                double len = table.GetSequenceLen();
                if (len == -1)
                    throw new ArgumentException("Table given to function 'table.insert' is not a valid sequence.");

                object value;
                double pos;
                if (o.Count == 2)
                {
                    pos = len + 1;
                    value = RuntimeHelper.GetValue(o[1]);
                }
                else
                {
                    object temp = RuntimeHelper.GetValue(o[1]);
                    if (!(temp is double))
                        throw new ArgumentException("Second argument to function 'table.insert' must be a number.");
                    pos = (double)temp;
                    if (pos > len + 1 || pos < 1 || System.Math.Ceiling(pos) != System.Math.Floor(pos))
                        throw new ArgumentException("Position given to function 'table.insert' is outside valid range.");

                    value = RuntimeHelper.GetValue(o[2]);
                }

                for (double d = len; d > pos; d--)
                {
                    table.SetItemRaw(d, table.GetItemRaw(d - 1));
                }
                table.SetItemRaw(pos, value);

                return new MultipleReturn();
            }
            public static MultipleReturn pack(LuaParameters o)
            {
                LuaTable table = new LuaTable();
                double d = 1;

                foreach (var item in o)
                {
                    object obj = RuntimeHelper.GetValue(item);
                    table.SetItemRaw(d, obj);
                }
                table.SetItemRaw("n", d - 1);

                return new MultipleReturn((object)table);
            }
            public static MultipleReturn remove(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'table.remove'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                if (table == null)
                    throw new ArgumentException("First argument to function 'table.remove' must be a table.");

                double len = table.GetSequenceLen();
                if (len == -1)
                    throw new ArgumentException("Table given to function 'table.remove' is not a valid sequence.");

                double pos;
                if (o.Count == 1)
                {
                    pos = len + 1;
                }
                else
                {
                    object temp = RuntimeHelper.GetValue(o[1]);
                    if (!(temp is double))
                        throw new ArgumentException("Second argument to function 'table.remove' must be a number.");
                    pos = (double)temp;
                    if (pos > len + 1 || pos < 1 || System.Math.Ceiling(pos) != System.Math.Floor(pos))
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
            public static MultipleReturn sort(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'table.sort'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                if (table == null)
                    throw new ArgumentException("First argument to function 'table.sort' must be a table.");

                double len = table.GetSequenceLen();
                if (len == -1)
                    throw new ArgumentException("Table given to function 'table.sort' is not a valid sequence.");

                IComparer<object> comp;
                if (o.Count > 1)
                {
                    LuaMethod meth = RuntimeHelper.GetValue(o[0]) as LuaMethod;
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
            public static MultipleReturn unpack(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'table.unpack'.");

                LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                double i = (RuntimeHelper.GetValue(o[1]) as double? ?? 1);

                if (table == null)
                    throw new ArgumentException("First argument to function 'table.unpack' must be a table.");

                double len = table.GetSequenceLen();
                if (len == -1)
                    throw new ArgumentException("Table given to function 'table.unpack' is not a valid sequence.");
                double j = (RuntimeHelper.GetValue(o[2]) as double? ?? len);

                return new MultipleReturn(
                    table
                        .Where(obj => obj.Key is double && (double)obj.Key >= i && (double)obj.Key <= j)
                        .OrderBy(k => (double)k.Key)
                        .Select(k => k.Value)
                    );
            }
        }
        static class Bit32
        {
            public static MultipleReturn arshift(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'bit32.arshift'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.arshift' must be a number.");
                int r = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.arshift' must be a number.");
                int i = (int)((double)obj % System.Math.Pow(2, 32));

                if (i < 0 || (r & (1 << 31)) == 0)
                {
                    i *= -1;

                    if (System.Math.Abs(i) > 31)
                        return new MultipleReturn(0.0);
                    else if (i >= 0)
                        return new MultipleReturn((double)(uint)(r << i));
                    else
                        return new MultipleReturn((double)(uint)(r >> -i));
                }
                else
                {
                    if (i >= 31)
                        r = -1;
                    else
                        r = ((r >> i) | ~(-1 >> i));

                    return new MultipleReturn((double)(uint)r);
                }
            }
            public static MultipleReturn band(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'bit32.band'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("Arguments to 'bit32.band' must be numbers.");

                uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                for (int i = 1; i < o.Count; i++)
                {
                    obj = RuntimeHelper.GetValue(o[i]);
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.band' must be numbers.");

                    ret &= (uint)((double)obj % System.Math.Pow(2, 32));
                }
                return new MultipleReturn((double)ret);
            }
            public static MultipleReturn bnot(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'bit32.bnot'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj is double)
                {
                    uint x = (uint)((double)obj % System.Math.Pow(2, 32));

                    return new MultipleReturn((double)(uint)((-1u - x) % System.Math.Pow(2, 32)));
                }
                else
                    throw new ArgumentException("First argument to function 'bit32.bnot' must be a number.");
            }
            public static MultipleReturn bor(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'bit32.bor'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("Arguments to 'bit32.bor' must be numbers.");

                uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                for (int i = 1; i < o.Count; i++)
                {
                    obj = RuntimeHelper.GetValue(o[i]);
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.bor' must be numbers.");

                    ret |= (uint)((double)obj % System.Math.Pow(2, 32));
                }
                return new MultipleReturn((double)ret);
            }
            public static MultipleReturn btest(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'bit32.btest'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("Arguments to 'bit32.btest' must be numbers.");

                uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                for (int i = 1; i < o.Count; i++)
                {
                    obj = RuntimeHelper.GetValue(o[i]);
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.btest' must be numbers.");

                    ret &= (uint)((double)obj % System.Math.Pow(2, 32));
                }
                return new MultipleReturn(ret != 0);
            }
            public static MultipleReturn bxor(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'bit32.bxor'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("Arguments to 'bit32.bxor' must be numbers.");

                uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                for (int i = 1; i < o.Count; i++)
                {
                    obj = RuntimeHelper.GetValue(o[i]);
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.bxor' must be numbers.");

                    ret ^= (uint)((double)obj % System.Math.Pow(2, 32));
                }
                return new MultipleReturn((double)ret);
            }
            public static MultipleReturn extract(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'bit32.extract'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.extract' must be a number.");
                int n = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.extract' must be a number.");
                int field = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[2]);
                int width = 1;
                if (obj is double)
                    width = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                if (field > 31 || width + field > 31)
                    throw new ArgumentException("Attempt to access bits outside the allowed range.");
                if (width < 1)
                    throw new ArgumentException("Cannot specify a zero width.");

                int m = (~((-1 << 1) << ((width - 1))));                
                return new MultipleReturn((double)((n >> field) & m));
            }
            public static MultipleReturn replace(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'bit32.replace'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.replace' must be a number.");
                int r = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.replace' must be a number.");
                int v = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[2]);
                if (!(obj is double))
                    throw new ArgumentException("Third arguments to 'bit32.replace' must be a number.");
                int field = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[3]);
                int width = 1;
                if (obj is double)
                    width = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                if (field > 31 || field < 0 || width < 1 || width + field > 31)
                    throw new ArgumentException("Attempt to access bits outside the allowed range.");

                int m = (~((-1 << 1) << ((width - 1))));
                v &= m;

                return new MultipleReturn((double)(uint)((r & ~(m << field)) | (v << field)));
            }
            public static MultipleReturn lrotate(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'bit32.lrotate'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.lrotate' must be a number.");
                int x = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.lrotate' must be a number.");
                int disp = (int)((double)obj % System.Math.Pow(2, 32));
                disp %= 32;

                if (disp >= 0)
                    return new MultipleReturn((double)((uint)(x << disp) | (uint)(x >> (32 - disp))));
                else
                    return new MultipleReturn((double)((uint)(x >> -disp) | (uint)(x << (32 + disp))));
            }
            public static MultipleReturn lshift(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'bit32.lshift'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.lshift' must be a number.");
                int x = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.lshift' must be a number.");
                int disp = (int)((double)obj % System.Math.Pow(2, 32));

                if (System.Math.Abs(disp) > 31)
                    return new MultipleReturn(0.0);
                else if (disp >= 0)
                    return new MultipleReturn((double)(uint)(x << disp));
                else
                    return new MultipleReturn((double)(uint)(x >> -disp));
            }
            public static MultipleReturn rrotate(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'bit32.rrotate'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.rrotate' must be a number.");
                int x = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.rrotate' must be a number.");
                int disp = (int)((double)obj % System.Math.Pow(2, 32));
                disp %= 32;

                if (disp < 0)
                    return new MultipleReturn((double)((uint)(x << -disp) | (uint)(x >> (32 + disp))));
                else
                    return new MultipleReturn((double)((uint)(x >> disp) | (uint)(x << (32 - disp))));
            }
            public static MultipleReturn rshift(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'bit32.rshift'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                if (!(obj is double))
                    throw new ArgumentException("First arguments to 'bit32.rshift' must be a number.");
                int x = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                obj = RuntimeHelper.GetValue(o[1]);
                if (!(obj is double))
                    throw new ArgumentException("Second arguments to 'bit32.rshift' must be a number.");
                int disp = (int)((double)obj % System.Math.Pow(2, 32));

                if (System.Math.Abs(disp) > 31)
                    return new MultipleReturn(0.0);
                else if (disp >= 0)
                    return new MultipleReturn((double)(uint)(x >> disp));
                else
                    return new MultipleReturn((double)(uint)(x << -disp));
            }
        }
        static class String
        {
            class gmatchHelper
            {
                MatchCollection _col;
                int i = 0;

                public gmatchHelper(MatchCollection col)
                {
                    this._col = col;
                }

                public MultipleReturn Do(LuaParameters o)
                {
                    if (_col == null || _col.Count <= i)
                        return new MultipleReturn();
                    Match m = _col[i++];

                    return new MultipleReturn(m.Groups.Cast<Group>().Select(c => c.Value));
                }
            }
            class gsubHelper
            {
                string _string;
                LuaTable _table;
                LuaMethod _meth;

                public gsubHelper(object o)
                {
                    this._string = o as string;
                    this._table = o as LuaTable;
                    this._meth = o as LuaMethod;
                    if (_string == null && _table == null && _meth == null)
                        throw new ArgumentException("Third argument to function 'string.gsub' must be a string, table, or function.");
                }

                public string Match(Match match)
                {
                    string ret = null;

                    if (_string != null)
                    {
                        ret = Regex.Replace(_string, @"%[0-9%]", m =>
                        {
                            if (m.Value == "%%")
                                return "%";
                            int i = int.Parse(m.Groups[0].Value.Substring(1), CultureInfo.InvariantCulture);
                            return i == 0 ? match.Value : (match.Groups.Count > i ? match.Groups[i].Value : "");
                        });
                    }
                    else if (_table != null)
                    {
                        object o = _table.GetItemRaw(match.Value);
                        if (o != null && o as bool? != false)
                        {
                            ret = o.ToString();
                        }
                    }
                    else if (_meth != null)
                    {
                        var obj = _meth.InvokeInternal(match.Groups.Cast<Group>().Where((g, i) => i > 0).Select(c => c.Value).ToArray(), -1);
                        if (obj != null && obj.Count > 0)
                        {
                            object o = obj[0];
                            if (o != null && o as bool? != false)
                            {
                                ret = o.ToString();
                            }
                        }
                    }

                    return ret == null ? match.Value : ret;
                }
            }

            public static MultipleReturn Byte(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'string.byte'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                object obj = RuntimeHelper.GetValue(o[1]);

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.byte' must be a string.");
                if (obj != null && !(obj is double))
                    throw new ArgumentException("Second argument to function 'string.byte' must be an integer.");

                int i = Convert.ToInt32(obj as double? ?? 1);
                int j = Convert.ToInt32(RuntimeHelper.GetValue(o[2]) ?? i);

                i--;
                j--;
                if (i < 0)
                    i = s.Length + i;
                if (j < 0)
                    j = s.Length + j;
                if (i < 1)
                    i = 1;
                if (j >= s.Length)
                    j = s.Length - 1;
                if (i < j)
                    return new MultipleReturn();

                return new MultipleReturn(s.Substring(i, j - i).Select(c => (double)c));                
            }
            public static MultipleReturn Char(LuaParameters o)
            {
                StringBuilder str = new StringBuilder();

                foreach (var item in o)
                {
                    object obj = RuntimeHelper.GetValue(item);
                    if (!(obj is double))
                        throw new ArgumentException("All arguments to function 'string.char' must be numbers.");

                    int i = Convert.ToInt32((double)obj);
                    if (i > char.MaxValue)
                    {
                        int high = (i >> 16) & 0xff;
                        int low = i & 0xff;
                        
                        if (!char.IsSurrogatePair((char)high, (char)low))
                            throw new ArgumentException("Argument to function 'string.char' is outside the range of a char.");

                        str.Append((char)high);
                        str.Append((char)low);
                    }
                    else
                        str.Append((char)i);
                }

                return new MultipleReturn((object)str.ToString());
            }
            public static MultipleReturn find(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'string.find'.");

                string str = RuntimeHelper.GetValue(o[0]) as string;
                string pattern = RuntimeHelper.GetValue(o[1]) as string;
                double start = RuntimeHelper.GetValue(o[2]) as double? ?? 1;
                bool plain = RuntimeHelper.GetValue(o[3]) as bool? ?? false;
                if (str == null)
                    throw new ArgumentException("First argument to function 'string.find' must be a string.");
                if (pattern == null)
                    throw new ArgumentException("Second argument to function 'string.find' must be a string.");

                int startx = start > 0 ? (int)start - 1 : str.Length + (int)start;
                if (plain)
                {
                    int i = str.IndexOf(pattern, startx);
                    if (i == -1)
                        return new MultipleReturn();
                    else
                        return new MultipleReturn((double)i, (double)(i + pattern.Length));
                }

                Regex reg = new Regex(pattern);
                Match mat = reg.Match(str, startx);
                return mat == null ? new MultipleReturn() : 
                    new MultipleReturn(
                        new object[] { (double)mat.Index, (double)(mat.Index + mat.Length) }
                            .Then(mat.Captures.Cast<Capture>()
                            .Select(c => c.Value))
                        );
            }
            public static MultipleReturn foramt(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'string.format'");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                if (s == null)
                    throw new ArgumentException("First argument to function 'string.format' must be a string.");

                return new MultipleReturn((object)string.Format(s, o.Cast<object>().Where((obj, i) => i > 0).ToArray()));
            }
            public static MultipleReturn gmatch(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'string.gmatch'");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                string pattern = RuntimeHelper.GetValue(o[1]) as string;
                if (s == null)
                    throw new ArgumentException("First argument to function 'string.gmatch' must be a string.");
                if (pattern == null)
                    throw new ArgumentException("Second argument to function 'string.gmatch' must be a string.");

                return new MultipleReturn(new LuaMethod(new gmatchHelper(Regex.Matches(s, pattern)).Do, o.Environment));
            }
            public static MultipleReturn gsub(LuaParameters o)
            {
                if (o.Count < 3)
                    throw new ArgumentException("Expecting three arguments to function 'string.gsub'");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                string pattern = RuntimeHelper.GetValue(o[1]) as string;
                object repl = RuntimeHelper.GetValue(o[2]);
                double n = RuntimeHelper.GetValue(o[3]) as double? ?? -1;
                if (s == null)
                    throw new ArgumentException("First argument to function 'string.gsub' must be a string.");
                if (pattern == null)
                    throw new ArgumentException("Second argument to function 'string.gsub' must be a string.");

                return new MultipleReturn((object)Regex.Replace(s, pattern, new gsubHelper(repl).Match));
            }
            public static MultipleReturn len(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'string.len'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.len' must be a string.");

                return new MultipleReturn((double)s.Length);                
            }
            public static MultipleReturn lower(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'string.lower'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.lower' must be a string.");

                return new MultipleReturn((object)s.ToLower(CultureInfo.CurrentCulture));                
            }
            public static MultipleReturn match(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'string.match'.");

                string str = RuntimeHelper.GetValue(o[0]) as string;
                string pattern = RuntimeHelper.GetValue(o[1]) as string;
                double start = RuntimeHelper.GetValue(o[2]) as double? ?? 1;
                if (str == null)
                    throw new ArgumentException("First argument to function 'string.match' must be a string.");
                if (pattern == null)
                    throw new ArgumentException("Second argument to function 'string.match' must be a string.");

                Regex reg = new Regex(pattern);
                int startx = start > 0 ? (int)start - 1 : str.Length + (int)start;
                Match mat = reg.Match(str, startx);
                return mat == null ? new MultipleReturn() : new MultipleReturn(mat.Captures.Cast<Capture>().Select(c => c.Value));
            }
            public static MultipleReturn rep(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'string.rep'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                object obj = RuntimeHelper.GetValue(o[1]);
                string sep = RuntimeHelper.GetValue(o[2]) as string ?? "";

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.rep' must be a string.");
                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to function 'string.rep' must be a number.");

                StringBuilder str = new StringBuilder();
                double limit = (double)obj;
                for (int i = 0; i < limit - 1; i++)
                {
                    str.Append(s);
                    str.Append(sep);
                }
                str.Append(s);
                return new MultipleReturn((object)str.ToString());
            }
            public static MultipleReturn reverse(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'string.reverse'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.reverse' must be a string.");

                return new MultipleReturn((object)new string(s.Reverse().ToArray()));
            }
            public static MultipleReturn sub(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting at least two arguments to function 'string.sub'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;
                object obj = RuntimeHelper.GetValue(o[1]);

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.sub' must be a string.");
                if (obj == null || !(obj is double))
                    throw new ArgumentException("Second argument to function 'string.sub' must be an integer.");

                int i = Convert.ToInt32((double)obj);
                int j = Convert.ToInt32(RuntimeHelper.GetValue(o[2]) ?? i);

                i--;
                j--;
                if (i < 0)
                    i = s.Length + i;
                if (j < 0)
                    j = s.Length + j;
                if (i < 1)
                    i = 1;
                if (j >= s.Length)
                    j = s.Length - 1;
                if (i < j)
                    return new MultipleReturn((object)"");

                return new MultipleReturn((object)s.Substring(i, j - i));
            }
            public static MultipleReturn upper(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'string.upper'.");

                string s = RuntimeHelper.GetValue(o[0]) as string;

                if (s == null)
                    throw new ArgumentException("First argument to function 'string.upper' must be a string.");

                return new MultipleReturn((object)s.ToUpper(CultureInfo.CurrentCulture));
            }
        }
        static class IO
        {
            static Stream _input = null, _output = null;

            public static void _Init(LuaEnvironment E)
            {
                _input = E.Settings.Stdin;
                _output = E.Settings.Stdout;
                E._globals.SetItemRaw("_STDIN", _CreateFile(E.Settings.Stdin, E));
                E._globals.SetItemRaw("_STDOUT", _CreateFile(E.Settings.Stdout, E));
            }

            class LinesHelper
            {
                StreamReader _stream;
                bool _close;
                int[] _ops; // -4 = *L, -3 = *l, -2 = *a, -1 = *n

                public LinesHelper(bool close, Stream stream, int[] ops)
                {
                    this._stream = new StreamReader(stream);
                    this._close = close;
                    this._ops = ops;
                }

                public MultipleReturn Do(LuaParameters o)
                {
                    if (_stream == null)
                        return new MultipleReturn(Enumerable.Range(1, _ops.Length).Select(i => (object)null));

                    var ret = _read(_ops, _stream);

                    if (_stream.EndOfStream)
                    {
                        if (_close)
                            _stream.Close();

                        _stream = null;
                    }

                    return ret;
                }
            }
            class Remove
            {
                List<Stream> st = new List<Stream>();
                static Remove instance = new Remove();
                static object _lock = new object();

                ~Remove()
                {
                    foreach (var item in st)
                        item.Close();

                    st.Clear();
                    st = null;
                }
                Remove()
                {

                }

                public static void Add(Stream s)
                {
                    lock (_lock)
                    {
                        if (!instance.st.Contains(s))
                            instance.st.Add(s);
                    }
                }
            }

            /* io functions*/
            public static MultipleReturn close(LuaParameters o)
            {
                object file = RuntimeHelper.GetValue(o[0]);
                Stream s;

                if (file == null)
                {
                    if (_output == null)
                        return new MultipleReturn(null, "No default output file set.");
                    s = _output;
                }
                else
                {
                    if (file is LuaTable)
                    {
                        s = (file as LuaTable).GetItemRaw("Stream") as Stream;
                        if (s == null)
                            return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                    }
                    else if (file is Stream)
                    {
                        s = file as Stream;
                    }
                    else
                        return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                }

                try
                {
                    s.Close();
                    return new MultipleReturn((object)_CreateFile(s, o.Environment));
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message, e);
                }
            }
            public static MultipleReturn flush(LuaParameters o)
            {
                object file = RuntimeHelper.GetValue(o[0]);
                Stream s;

                if (file == null)
                {
                    if (_output == null)
                        return new MultipleReturn(null, "No default output file set.");
                    s = _output;
                }
                else
                {
                    if (file is LuaTable)
                    {
                        s = (file as LuaTable).GetItemRaw("Stream") as Stream;
                        if (s == null)
                            return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                    }
                    else if (file is Stream)
                    {
                        s = file as Stream;
                    }
                    else
                        return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                }
                try
                {
                    _output.Flush();
                    return new MultipleReturn((object)_CreateFile(_output, o.Environment));
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message, e);
                }
            }
            public static MultipleReturn input(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj != null)
                {
                    if (obj is string)
                    {
                        Stream s = File.OpenRead(obj as string);
                        _input = s;
                    }
                    else if (obj is LuaTable)
                    {
                        Stream s = (obj as LuaTable).GetItemRaw("Stream") as Stream;
                        if (s == null)
                            throw new InvalidOperationException("First argument to function 'io.input' must be a file-stream or a string path.");

                        _input = s;
                    }
                    else if (obj is Stream)
                    {
                        _input = obj as Stream;
                    }
                    else
                        throw new InvalidOperationException("First argument to function 'io.input' must be a file-stream or a string path.");
                }

                return new MultipleReturn((object)_CreateFile(_input, o.Environment));
            }
            public static MultipleReturn lines(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);
                bool close;
                Stream s;
                int start = 0;

                if (obj is string)
                {
                    if ((obj as string)[0] != '*')
                    {
                        s = File.OpenRead(obj as string);
                        close = true;
                        start = 1;
                    }
                    else
                    {
                        s = _input;
                        close = false;
                        start = 0;
                    }
                }
                else if (obj is LuaTable)
                {
                    s = (obj as LuaTable)._get("Stream") as Stream;
                    if (s == null)
                        throw new ArgumentException("First argument to io.lines must be a file-stream or a file path, make sure to use file:lines.");
                    close = false;
                    start = 1;
                }
                else if (obj is Stream)
                {
                    s = obj as Stream;
                    close = false;
                    start = 1;
                }
                else
                {
                    s = _input;
                    close = false;
                    start = 0;
                }

                int[] a = _parse(o.Cast<object>().Where((o1, i1) => i1 >= start), "io.lines");

                return new MultipleReturn(new LuaMethod((new LinesHelper(close, s, a)).Do, o.Environment));
            }
            public static MultipleReturn open(LuaParameters o)
            {
                string s = RuntimeHelper.GetValue(o[0]) as string;
                string mode = RuntimeHelper.GetValue(o[1]) as string;
                FileMode fileMode;
                FileAccess access;
                bool seek = false;
                mode = mode == null ? null : mode.ToLower(CultureInfo.InvariantCulture);

                if (string.IsNullOrWhiteSpace(s))
                    return new MultipleReturn(null, "First argument must be a string filename.");

                switch (mode)
                {
                    case "r":
                    case "rb":
                    case "":
                    case null:
                        fileMode = FileMode.Open;
                        access = FileAccess.Read;
                        break;
                    case "w":
                    case "wb":
                        fileMode = FileMode.Create;
                        access = FileAccess.Write;
                        break;
                    case "a":
                    case "ab":
                        fileMode = FileMode.OpenOrCreate;
                        access = FileAccess.ReadWrite;
                        seek = true;
                        break;
                    case "r+":
                    case "r+b":
                        fileMode = FileMode.Open;
                        access = FileAccess.ReadWrite;
                        break;
                    case "w+":
                    case "w+b":
                        fileMode = FileMode.Create;
                        access = FileAccess.ReadWrite;
                        break;
                    case "a+":
                    case "a+b":
                        fileMode = FileMode.OpenOrCreate;
                        access = FileAccess.ReadWrite;
                        seek = true;
                        break;
                    default:
                        return new MultipleReturn(null, "Second argument must be a valid string mode.");
                }

                try
                {
                    using (Stream stream = File.Open(s, fileMode, access))
                    {
                        if (seek && stream.CanSeek)
                            stream.Seek(0, SeekOrigin.End);

                        return new MultipleReturn((object)_CreateFile(stream, o.Environment));
                    }
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message, e);
                }
            }
            public static MultipleReturn output(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj != null)
                {
                    if (obj is string)
                    {
                        Stream s = File.OpenRead(obj as string);
                        _output = s;
                    }
                    else if (obj is LuaTable)
                    {
                        Stream s = (obj as LuaTable).GetItemRaw("Stream") as Stream;
                        if (s == null)
                            throw new InvalidOperationException("First argument to function 'io.output' must be a file-stream or a string path.");

                        _output = s;
                    }
                    else if (obj is Stream)
                    {
                        _output = obj as Stream;
                    }
                    else
                        throw new InvalidOperationException("First argument to function 'io.output' must be a file-stream or a string path.");
                }

                return new MultipleReturn((object)_CreateFile(_output, o.Environment));
            }
            public static MultipleReturn read(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);
                Stream s;
                int start = 0;

                if (obj is LuaTable)
                {
                    s = (obj as LuaTable)._get("Stream") as Stream;
                    if (s == null)
                        throw new ArgumentException("First argument to io.read must be a file-stream or a file path, make sure to use file:read.");
                    start = 1;
                }
                else if (obj is Stream)
                {
                    s = obj as Stream;
                    start = 1;
                }
                else
                {
                    s = _input;
                    start = 0;
                }

                int[] a = _parse(o.Cast<object>().Where((o1, i1) => i1 >= start), "io.read");

                return _read(a, new StreamReader(s));
            }
            public static MultipleReturn seek(LuaParameters o)
            {
                Stream s = RuntimeHelper.GetValue(o[0]) as Stream;
                SeekOrigin origin = SeekOrigin.Current;
                long off = 0;

                if (s == null)
                {
                    LuaTable table = RuntimeHelper.GetValue(o[0]) as LuaTable;
                    if (table != null)
                        s = table._get("Stream") as Stream;

                    if (s == null)
                        throw new ArgumentException("First real argument to function file:seek must be a file-stream, make sure to use file:seek.");
                }

                if (o.Count > 1)
                {
                    string str = RuntimeHelper.GetValue(o[1]) as string;
                    if (str == "set")
                        origin = SeekOrigin.Begin;
                    else if (str == "cur")
                        origin = SeekOrigin.Current;
                    else if (str == "end")
                        origin = SeekOrigin.End;
                    else
                        throw new ArgumentException("First argument to function file:seek must be a string.");

                    if (o.Count > 2)
                    {
                        object obj = RuntimeHelper.GetValue(o[2]);
                        if (obj is double)
                            off = Convert.ToInt64((double)obj);
                        else
                            throw new ArgumentException("Second argument to function file:seek must be a number.");
                    }
                }

                if (!s.CanSeek)
                    return new MultipleReturn(null, "Specified stream cannot be seeked.");

                try
                {
                    return new MultipleReturn(Convert.ToDouble(s.Seek(off, origin)));
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message, e);
                }
            }
            public static MultipleReturn tmpfile(LuaParameters o)
            {
                string str = Path.GetTempFileName();
                Stream s = File.Open(str, FileMode.OpenOrCreate, FileAccess.ReadWrite);

                Remove.Add(s);
                return new MultipleReturn(_CreateFile(s, o.Environment));
            }
            public static MultipleReturn type(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj is Stream)
                {
                    return new MultipleReturn((object)"file");
                }
                else if (obj is LuaTable)
                {
                    Stream s = (obj as LuaTable)._get("Stream") as Stream;
                    return new MultipleReturn((object)(s == null ? null : "file"));
                }
                else
                    return new MultipleReturn(null);
            }
            public static MultipleReturn write(LuaParameters o)
            {
                object obj = RuntimeHelper.GetValue(o[0]);
                Stream s;
                int start = 0;

                if (obj is LuaTable)
                {
                    s = (obj as LuaTable)._get("Stream") as Stream;
                    if (s == null)
                        return new MultipleReturn(null, "First argument must be a file-stream or a file path.");
                    start = 1;
                }
                else if (obj is Stream)
                {
                    s = obj as Stream;
                    start = 1;
                }
                else
                {
                    s = _output;
                    start = 0;
                }

                try
                {
                    for (int i = start; i < o.Count; i++)
                    {
                        obj = RuntimeHelper.GetValue(o[i]);
                        if (obj is double)
                        {
                            var bt = (o.Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(((double)obj).ToString(CultureInfo.InvariantCulture));
                            s.Write(bt);
                        }
                        else if (obj is string)
                        {
                            var bt = (o.Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(obj as string);
                            s.Write(bt);
                        }
                        else
                            throw new ArgumentException("Arguments to io.write must be a string or number.");
                    }

                    return new MultipleReturn(_CreateFile(s, o.Environment));
                }
                catch (ArgumentException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    return new MultipleReturn(null, e.Message, e);
                }
            }

            /* helper functions */
            static int[] _parse(IEnumerable args, string func)
            {
                List<int> v = new List<int>();

                foreach (var item in args)
                {
                    object obj = RuntimeHelper.GetValue(item);
                    if (obj is double)
                    {
                        double d = (double)obj;
                        if (d < 0)
                            throw new ArgumentOutOfRangeException("Arguments to " + func + " must be a positive integer.", (Exception)null);

                        v.Add(Convert.ToInt32(d));
                    }
                    else if (obj is string)
                    {
                        string st = obj as string;

                        if (st == "*n")
                            v.Add(-1);
                        else if (st == "*a")
                            v.Add(-2);
                        else if (st == "*l")
                            v.Add(-3);
                        else if (st == "*L")
                            v.Add(-4);
                        else
                            throw new ArgumentException("Only the following strings are valid as arguments to " + func + ": '*n', '*a', '*l', or '*L'.");
                    }
                    else
                        throw new ArgumentException("Arguments to function " + func + " must be a number or a string.");
                }

                return v.ToArray();
            }
            static MultipleReturn _read(int[] opts, StreamReader s)
            {
                List<object> ret = new List<object>();

                foreach (var item in opts)
                {
                    switch (item)
                    {
                        case -4:
                            ret.Add(s.EndOfStream ? null : s.ReadLine() + "\n");
                            break;
                        case -3:
                            ret.Add(s.EndOfStream ? null : s.ReadLine());
                            break;
                        case -2:
                            ret.Add(s.EndOfStream ? null : s.ReadToEnd());
                            break;
                        case -1:
                            if (s.EndOfStream)
                                ret.Add(null);
                            else
                            {
                                long pos = 0;
                                double? d = RuntimeHelper.ReadNumber(s, null, 0, 0, ref pos);
                                if (d.HasValue)
                                    ret.Add(d.Value);
                                else
                                    ret.Add(null);
                            }
                            break;
                        default:
                            if (s.EndOfStream)
                                ret.Add(null);
                            else
                            {
                                char[] c = new char[item];
                                s.Read(c, 0, item);
                                ret.Add(new string(c));
                            }
                            break;
                    }
                }

                return new MultipleReturn(ret);
            }
            static LuaTable _CreateFile(Stream backing, LuaEnvironment E)
            {
                LuaTable ret = new LuaTable();
                ret.SetItemRaw("Stream", backing);
                ret.SetItemRaw("close", new LuaMethod(close, E));
                ret.SetItemRaw("flush", new LuaMethod(flush, E));
                ret.SetItemRaw("lines", new LuaMethod(lines, E));
                ret.SetItemRaw("read", new LuaMethod(read, E));
                ret.SetItemRaw("seek", new LuaMethod(seek, E));
                ret.SetItemRaw("write", new LuaMethod(write, E));

                return ret;
            }

            /* global functions */
            public static MultipleReturn dofile(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'dofile'.");

                string file = RuntimeHelper.GetValue(o[0]) as string;

                if (string.IsNullOrEmpty(file))
                    throw new ArgumentException("First argument to 'loadfile' must be a file path.");
                if (!File.Exists(file))
                    throw new FileNotFoundException("Unable to locate file at '" + file + "'.");

                string chunk = File.ReadAllText(file);
                using (var c = new StringReader(chunk))
                {
                    var r = PlainParser.LoadChunk(o.Environment, c, SHA512.Create().ComputeHash(Encoding.Unicode.GetBytes(chunk)));
                    return new MultipleReturn((IEnumerable)r.Execute());
                }
            }
            public static MultipleReturn load(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'load'.");

                object ld = RuntimeHelper.GetValue(o[0]);
                LuaTable env = RuntimeHelper.GetValue(o[3]) as LuaTable;
                string chunk;

                if (ld is LuaMethod)
                {
                    chunk = "";
                    while (true)
                    {
                        var ret = (ld as LuaMethod).InvokeInternal(new object[0], -1);
                        if (ret[0] is string)
                        {
                            if (string.IsNullOrEmpty(ret[0] as string))
                                break;
                            else
                                chunk += ret[0] as string;
                        }
                        else
                            break;
                    }
                }
                else if (ld is string)
                {
                    chunk = ld as string;
                }
                else
                    throw new ArgumentException("First argument to 'load' must be a string or a method.");

                using (var c = new StringReader(chunk))
                {
                    try
                    {
                        return new MultipleReturn(PlainParser.LoadChunk(o.Environment, c, SHA512.Create().ComputeHash(Encoding.Unicode.GetBytes(chunk))).ToMethod());
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message);
                    }
                }
            }
            public static MultipleReturn loadfile(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'loadfile'.");

                string file = RuntimeHelper.GetValue(o[0]) as string;
                string mode = RuntimeHelper.GetValue(o[1]) as string;

                if (string.IsNullOrEmpty(file))
                    throw new ArgumentException("First argument to 'loadfile' must be a file path.");
                if (!File.Exists(file))
                    throw new FileNotFoundException("Unable to locate file at '" + file + "'.");
                if (string.IsNullOrEmpty(mode) && o.Count > 1)
                    throw new ArgumentException("Second argument to 'loadfile' must be a string mode.");
                if (mode != "t")
                    throw new ArgumentException("The only mode supported by loadfile is 't'.");

                string chunk = File.ReadAllText(file);
                using (var c = new StringReader(chunk))
                {
                    try
                    {
                        return new MultipleReturn(PlainParser.LoadChunk(o.Environment, c, SHA512.Create().ComputeHash(Encoding.Unicode.GetBytes(chunk))).ToMethod());
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message);
                    }
                }
            }
        }
        static class Math
        {
            public sealed class MathHelper
            {
                string name;
                Func<double, double> f1;
                Func<double, double, double> f2;

                MathHelper(string name, Func<double, double> func)
                {
                    this.name = name;
                    this.f1 = func;
                    this.f2 = null;
                }
                MathHelper(string name, Func<double, double, double> func)
                {
                    this.name = name;
                    this.f1 = null;
                    this.f2 = func;
                }

                MultipleReturn Do(LuaParameters o)
                {
                    if (o.Count < (f1 == null ? 2 : 1))
                        throw new ArgumentException("Expecting " + (f1 == null ? "two" : "one") + " argument to function '" + name + "'.");

                    object obj = RuntimeHelper.GetValue(o[0]);
                    object obj1 = RuntimeHelper.GetValue(o[1]);

                    if (obj == null || !(obj is double))
                        throw new ArgumentException("First argument to '" + name + "' must be a number.");
                    if ((obj1 == null || !(obj1 is double)) && f1 == null)
                        throw new ArgumentException("Second argument to '" + name + "' must be a number.");

                    return new MultipleReturn(f1 == null ? f2((double)obj, (double)obj1) : f1((double)obj));
                }

                public static LuaMethod Create(string name, Func<double, double> func, LuaEnvironment E)
                {
                    return new LuaMethod(new MathHelper(name, func).Do, E);
                }
                public static LuaMethod Create(string name, Func<double, double, double> func, LuaEnvironment E)
                {
                    return new LuaMethod(new MathHelper(name, func).Do, E);
                }
            }

            public static MultipleReturn deg(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'math.deg'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.deg' must be a number.");

                return new MultipleReturn(((double)obj * 180 / System.Math.PI));
            }
            public static MultipleReturn frexp(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'math.frexp'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.frexp' must be a number.");

                double d = (double)obj;
                double m, e;

                if (d == 0)
                {
                    m = 0;
                    e = 0;
                    return new MultipleReturn(m, e);
                }

                bool b = d < 0;
                d = b ? -d : d;
                e = System.Math.Ceiling(System.Math.Log(d, 2));
                m = d / System.Math.Pow(2, e);
                m = b ? -m : m;

                return new MultipleReturn(m, e);
            }
            public static MultipleReturn ldexp(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'math.ldexp'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                object obj2 = RuntimeHelper.GetValue(o[1]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.ldexp' must be a number.");
                if (obj2 == null || !(obj2 is double) || System.Math.Floor((double)obj2) != System.Math.Ceiling((double)obj2))
                    throw new ArgumentException("Second argument to 'math.ldexp' must be a integer.");

                return new MultipleReturn((double)obj * System.Math.Pow(2.0, (double)obj2));
            }
            public static MultipleReturn log(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'math.log'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                object obj2 = RuntimeHelper.GetValue(o[1]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.log' must be a number.");
                if (obj2 != null && !(obj2 is double))
                    throw new ArgumentException("Second argument to 'math.log' must be a number.");

                if (obj2 != null)
                    return new MultipleReturn(System.Math.Log((double)obj, (double)obj2));
                else
                    return new MultipleReturn(System.Math.Log((double)obj));
            }
            public static MultipleReturn max(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'math.max'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.max' must be a number.");

                double ret = (double)obj;

                for (int i = 1; i < o.Count; i++)
                {
                    object obj2 = RuntimeHelper.GetValue(o[0]);
                    if (obj2 == null || !(obj2 is double))
                        throw new ArgumentException("Argument number '" + i + "' to 'math.max' must be a number.");

                    double d = (double)obj2;
                    if (d > ret)
                        ret = d;
                }

                return new MultipleReturn(ret);
            }
            public static MultipleReturn min(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'math.min'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.min' must be a number.");

                double ret = (double)obj;

                for (int i = 1; i < o.Count; i++)
                {
                    object obj2 = RuntimeHelper.GetValue(o[0]);
                    if (obj2 == null || !(obj2 is double))
                        throw new ArgumentException("Argument number '" + i + "' to 'math.min' must be a number.");

                    double d = (double)obj2;
                    if (d < ret)
                        ret = d;
                }

                return new MultipleReturn(ret);
            }
            public static MultipleReturn modf(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'math.modf'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.modf' must be a number.");

                double d = (double)obj;
                return new MultipleReturn(System.Math.Floor(d), (d - System.Math.Floor(d)));
            }
            public static MultipleReturn rad(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'math.rad'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.rad' must be a number.");

                return new MultipleReturn(((double)obj * System.Math.PI / 180));
            }
            public static MultipleReturn random(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'math.random'.");

                object obj = RuntimeHelper.GetValue(o[0]);
                object obj2 = RuntimeHelper.GetValue(o[1]);
                object obj3 = RuntimeHelper.GetValue(o[2]);

                if (obj == null || !(obj is double))
                    throw new ArgumentException("First argument to 'math.random' must be a number.");
                if (obj2 != null && !(obj2 is double))
                    throw new ArgumentException("Second argument to 'math.random' must be a number.");
                if (obj3 != null && !(obj3 is double))
                    throw new ArgumentException("Third argument to 'math.random' must be a number.");

                if (obj2 == null)
                {
                    lock (_randLock)
                        return new MultipleReturn(Rand.NextDouble());
                }
                else
                {
                    double m = (double)obj2;
                    if (obj3 == null)
                    {
                        lock (_randLock)
                            return new MultipleReturn(Rand.NextDouble() * m);
                    }
                    else
                    {
                        double n = (double)obj3;

                        lock (_randLock)
                            return new MultipleReturn(Rand.NextDouble() * (n - m) + m);
                    }
                }
            }
            public static MultipleReturn randomseed(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'math.randomseed'.");

                object obj = RuntimeHelper.GetValue(o[0]);

                if (obj == null || !(obj is double) || System.Math.Floor((double)obj) != System.Math.Ceiling((double)obj))
                    throw new ArgumentException("First argument to 'math.randomseed' must be an integer.");

                lock (_randLock)
                {
                    Rand = new Random((int)(double)obj);
                }
                return new MultipleReturn();
            }

            static Random Rand = new Random(Guid.NewGuid().GetHashCode());
            static object _randLock = new object();
        }
        static class Coroutine
        {
            sealed class WrapHelper
            {
                LuaThread thread;

                public WrapHelper(LuaThread thread)
                {
                    this.thread = thread;
                }

                public MultipleReturn Do(LuaParameters o)
                {
                    object[] obj = thread.ResumeInternal(o.Cast<object>().ToArray());
                    if (obj[0] as bool? == false)
                        throw (Exception)obj[2];
                    else
                        return new MultipleReturn(obj.Where((ob, i) => i > 0));
                }
            }

            public static MultipleReturn create(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'coroutine.create'.");

                LuaMethod meth = o[0] as LuaMethod;
                if (meth == null)
                    throw new ArgumentException("First argument to function 'coroutine.create' must be a function.");

                return new MultipleReturn(new LuaThread(meth));
            }
            public static MultipleReturn resume(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'coroutine.resume'.");

                LuaThread meth = o[0] as LuaThread;
                if (meth == null)
                    throw new ArgumentException("First argument to function 'coroutine.resume' must be a thread.");

                return new MultipleReturn((IEnumerable)meth.ResumeInternal(
                    o.Cast<object>()
                        .Where((ot,i) => i > 0)
                        .ToArray()
                    ));
            }
            public static MultipleReturn running(LuaParameters o)
            {
                LuaThread t;
                lock (LuaThread._cache)
                {
                    if (!LuaThread._cache.ContainsKey(Thread.CurrentThread.ManagedThreadId))
                        t = new LuaThread(Thread.CurrentThread);
                    else
                        t = LuaThread._cache[Thread.CurrentThread.ManagedThreadId];
                }

                return new MultipleReturn(t, !t.IsLua);
            }
            public static MultipleReturn status(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting at least one argument to function 'coroutine.status'.");

                LuaThread meth = o[0] as LuaThread;
                if (meth == null)
                    throw new ArgumentException("First argument to function 'coroutine.status' must be a thread.");

                return new MultipleReturn((object)meth.Status.ToString().ToLower(CultureInfo.InvariantCulture));
            }
            public static MultipleReturn wrap(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'coroutine.wrap'.");

                LuaMethod meth = o[0] as LuaMethod;
                if (meth == null)
                    throw new ArgumentException("First argument to function 'coroutine.wrap' must be a function.");

                return new MultipleReturn(new LuaMethod(new WrapHelper(new LuaThread(meth)).Do, o.Environment));
            }
            public static MultipleReturn yield(LuaParameters o)
            {
                LuaThread t;
                lock (LuaThread._cache)
                {
                    if (!LuaThread._cache.ContainsKey(Thread.CurrentThread.ManagedThreadId))
                        throw new InvalidOperationException("Cannot yield the main thread.");

                    t = LuaThread._cache[Thread.CurrentThread.ManagedThreadId];
                }

                return new MultipleReturn((IEnumerable)t.Yield(o.Cast<object>().ToArray()));
            }
        }
        static class OS
        {
            static Stopwatch stop = Stopwatch.StartNew();

            public static MultipleReturn clock(LuaParameters o)
            {
                return new MultipleReturn(stop.Elapsed.TotalSeconds);
            }
            public static MultipleReturn date(LuaParameters o)
            {
                string format = RuntimeHelper.GetValue(o[0]) as string ?? "%c";
                object obj = RuntimeHelper.GetValue(o[1]);
                DateTimeOffset time;

                if (obj is double)
                    time = new DateTime(Convert.ToInt64((double)obj));
                else if (obj is DateTime)
                    time = (DateTime)obj;
                else if (obj is DateTimeOffset)
                    time = ((DateTimeOffset)obj);
                else
                    time = DateTimeOffset.Now;

                if (format.Length > 0 && format[0] == '!')
                {
                    format = format.Substring(1);
                    time = time.ToUniversalTime();
                }

                if (format == "*t")
                {
                    LuaTable tab = new LuaTable();
                    tab.SetItemRaw("year", Convert.ToDouble(time.Year));
                    tab.SetItemRaw("month", Convert.ToDouble(time.Month));
                    tab.SetItemRaw("day", Convert.ToDouble(time.Day));
                    tab.SetItemRaw("hour", Convert.ToDouble(time.Hour));
                    tab.SetItemRaw("min", Convert.ToDouble(time.Minute));
                    tab.SetItemRaw("sec", Convert.ToDouble(time.Second));
                    tab.SetItemRaw("wday", Convert.ToDouble(((int)time.DayOfWeek) + 1));
                    tab.SetItemRaw("yday", Convert.ToDouble(time.DayOfYear));

                    return new MultipleReturn((object)tab);
                }

                StringBuilder ret = new StringBuilder();
                for (int i = 0; i < format.Length; i++)
                {
                    if (format[i] == '%')
                    {
                        i++;
                        switch (format[i])
                        {
                            case 'a':
                                ret.Append(time.ToString("ddd", CultureInfo.CurrentCulture));
                                break;
                            case 'A':
                                ret.Append(time.ToString("dddd", CultureInfo.CurrentCulture));
                                break;
                            case 'b':
                                ret.Append(time.ToString("MMM", CultureInfo.CurrentCulture));
                                break;
                            case 'B':
                                ret.Append(time.ToString("MMMM", CultureInfo.CurrentCulture));
                                break;
                            case 'c':
                                ret.Append(time.ToString("F", CultureInfo.CurrentCulture));
                                break;
                            case 'd':
                                ret.Append(time.ToString("dd", CultureInfo.CurrentCulture));
                                break;
                            case 'H':
                                ret.Append(time.ToString("HH", CultureInfo.CurrentCulture));
                                break;
                            case 'I':
                                ret.Append(time.ToString("hh", CultureInfo.CurrentCulture));
                                break;
                            case 'j':
                                ret.Append(time.DayOfYear.ToString("d3", CultureInfo.CurrentCulture));
                                break;
                            case 'm':
                                ret.Append(time.Month.ToString("d2", CultureInfo.CurrentCulture));
                                break;
                            case 'M':
                                ret.Append(time.Minute.ToString("d2", CultureInfo.CurrentCulture));
                                break;
                            case 'p':
                                ret.Append(time.ToString("tt", CultureInfo.CurrentCulture));
                                break;
                            case 'S':
                                ret.Append(time.ToString("ss", CultureInfo.CurrentCulture));
                                break;
                            case 'U':
                                throw new NotSupportedException("The %U format specifier is not supported by this framework.");
                            case 'w':
                                ret.Append((int)time.DayOfWeek);
                                break;
                            case 'W':
                                throw new NotSupportedException("The %W format specifier is not supported by this framework.");
                            case 'x':
                                ret.Append(time.ToString("d", CultureInfo.CurrentCulture));
                                break;
                            case 'X':
                                ret.Append(time.ToString("T", CultureInfo.CurrentCulture));
                                break;
                            case 'y':
                                ret.Append(time.ToString("yy", CultureInfo.CurrentCulture));
                                break;
                            case 'Y':
                                ret.Append(time.ToString("yyyy", CultureInfo.CurrentCulture));
                                break;
                            case 'Z':
                                ret.Append(time.ToString("%K", CultureInfo.CurrentCulture));
                                break;
                            case '%':
                                ret.Append('%');
                                break;
                            default:
                                throw new ArgumentException("Unrecognised format specifier %" + format[i] + " in function 'os.date'.");
                        }
                    }
                    else
                        ret.Append(format[i]);
                }
                return new MultipleReturn((object)ret.ToString());
            }
            public static MultipleReturn difftime(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'os.difftime'.");

                object obj1 = RuntimeHelper.GetValue(o[0]);
                object obj2 = RuntimeHelper.GetValue(o[1]);

                if (obj1 == null || !(obj1 is double))
                    throw new ArgumentException("First argument to function 'os.difftime' must be a number.");
                if (obj2 == null || !(obj2 is double))
                    throw new ArgumentException("Second argument to function 'os.difftime' must be a number.");

                double d2 = (double)obj1, d1 = (double)obj2;

                return new MultipleReturn(d2 - d1);
            }
            public static MultipleReturn exit(LuaParameters o)
            {
                object code = RuntimeHelper.GetValue(o[0]);
                object close = RuntimeHelper.GetValue(o[1]);

                o.Environment.Settings.CallQuit(o.Environment, code, close);
                return new MultipleReturn();
            }
            public static MultipleReturn getenv(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'os.getenv'.");

                string var = RuntimeHelper.GetValue(o[0]) as string;

                if (var == null)
                    throw new ArgumentException("First argument to function 'os.getenv' must be a string.");

                var ret = Environment.GetEnvironmentVariable(var);
                return new MultipleReturn((object)ret);
            }
            public static MultipleReturn remove(LuaParameters o)
            {
                if (o.Count < 1)
                    throw new ArgumentException("Expecting one argument to function 'os.remove'.");

                string var = RuntimeHelper.GetValue(o[0]) as string;

                if (var == null)
                    throw new ArgumentException("First argument to function 'os.remove' must be a string.");

                if (File.Exists(var))
                {
                    try
                    {
                        File.Delete(var);
                        return new MultipleReturn(true);
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
                else if (Directory.Exists(var))
                {
                    if (Directory.EnumerateFileSystemEntries(var).Count() > 0)
                        return new MultipleReturn(null, "Specified directory is not empty.");

                    try
                    {
                        Directory.Delete(var);
                        return new MultipleReturn(true);
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
                else
                    return new MultipleReturn(null, "Specified filename does not exist.");
            }
            public static MultipleReturn rename(LuaParameters o)
            {
                if (o.Count < 2)
                    throw new ArgumentException("Expecting two arguments to function 'os.rename'.");

                string old = RuntimeHelper.GetValue(o[0]) as string;
                string neww = RuntimeHelper.GetValue(o[1]) as string;

                if (old == null)
                    throw new ArgumentException("First argument to function 'os.rename' must be a string.");
                if (neww == null)
                    throw new ArgumentException("Second argument to function 'os.rename' must be a string.");

                if (File.Exists(old))
                {
                    try
                    {
                        File.Move(old, neww);
                        return new MultipleReturn(true);
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
                else if (Directory.Exists(old))
                {
                    try
                    {
                        Directory.Move(old, neww);
                        return new MultipleReturn(true);
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
                else
                    return new MultipleReturn(null, "Specified path does not exist.");
            }
            public static MultipleReturn setlocale(LuaParameters o)
            {
                string locale = RuntimeHelper.GetValue(o[0]) as string;

                if (locale == null)
                    return new MultipleReturn((object)Thread.CurrentThread.CurrentCulture.Name);
                else
                {
                    try
                    {
                        CultureInfo ci = CultureInfo.GetCultureInfo(locale);
                        if (ci == null)
                            return new MultipleReturn();

                        Thread.CurrentThread.CurrentCulture = ci;
                        return new MultipleReturn((object)ci.Name);
                    }
                    catch (Exception)
                    {
                        return new MultipleReturn();
                    }
                }
            }
            public static MultipleReturn time(LuaParameters o)
            {
                DateTime time;
                object table = RuntimeHelper.GetValue(o[0]);

                if (table != null)
                {
                    if (table is LuaTable)
                    {
                        LuaTable t = table as LuaTable;
                        int year, month, day, hour, min, sec;

                        object obj = RuntimeHelper.GetValue(t._get("year"));
                        if (!(obj is double))
                            throw new ArgumentException("First argument to function 'os.time' is not a valid time table.");
                        year = Convert.ToInt32((double)obj);

                        obj = RuntimeHelper.GetValue(t._get("month"));
                        if (!(obj is double))
                            throw new ArgumentException("First argument to function 'os.time' is not a valid time table.");
                        month = Convert.ToInt32((double)obj);

                        obj = RuntimeHelper.GetValue(t._get("day"));
                        if (!(obj is double))
                            throw new ArgumentException("First argument to function 'os.time' is not a valid time table.");
                        day = Convert.ToInt32((double)obj);

                        obj = RuntimeHelper.GetValue(t._get("hour"));
                        if (obj is double)
                            hour = Convert.ToInt32((double)obj);
                        else
                            hour = 12;

                        obj = RuntimeHelper.GetValue(t._get("min"));
                        if (obj is double)
                            min = Convert.ToInt32((double)obj);
                        else
                            min = 0;

                        obj = RuntimeHelper.GetValue(t._get("sec"));
                        if (obj is double)
                            sec = Convert.ToInt32((double)obj);
                        else
                            sec = 0;

                        time = new DateTime(year, month, day, hour, min, sec);
                    }
                    else if (table is DateTime)
                        time = (DateTime)table;
                    else if (table is DateTimeOffset)
                        time = ((DateTimeOffset)table).LocalDateTime;
                    else
                        throw new ArgumentException("First argument to function 'os.time' must be a table.");
                }
                else
                    time = DateTime.Now;

                return new MultipleReturn(Convert.ToDouble(time.Ticks));
            }
            public static MultipleReturn tmpname(LuaParameters o)
            {
                return new MultipleReturn((object)Path.GetTempFileName());
            }
        }

        public static void Initialize(LuaEnvironment E)
        {
            LuaLibraries libraries = E.Settings.Libraries;
            LuaTable _globals = E._globals;

            _globals.SetItemRaw("assert", new LuaMethod(Standard.assert, E));
            _globals.SetItemRaw("collectgarbage", new LuaMethod(Standard.collectgarbage, E));
            _globals.SetItemRaw("error", new LuaMethod(Standard.error, E));
            _globals.SetItemRaw("getmetatable", new LuaMethod(Standard.getmetatable, E));
            _globals.SetItemRaw("ipairs", new LuaMethod(Standard.ipairs, E));
            _globals.SetItemRaw("next", new LuaMethod(Standard.next, E));
            _globals.SetItemRaw("pairs", new LuaMethod(Standard.pairs, E));
            _globals.SetItemRaw("pcall", new LuaMethod(Standard.pcall, E));
            _globals.SetItemRaw("print", new LuaMethod(Standard.print, E));
            _globals.SetItemRaw("rawequal", new LuaMethod(Standard.rawequal, E));
            _globals.SetItemRaw("rawget", new LuaMethod(Standard.rawget, E));
            _globals.SetItemRaw("rawlen", new LuaMethod(Standard.rawlen, E));
            _globals.SetItemRaw("rawset", new LuaMethod(Standard.rawset, E));
            _globals.SetItemRaw("select", new LuaMethod(Standard.select, E));
            _globals.SetItemRaw("setmetatable", new LuaMethod(Standard.setmetatable, E));
            _globals.SetItemRaw("tonumber", new LuaMethod(Standard.tonumber, E));
            _globals.SetItemRaw("tostring", new LuaMethod(Standard.tostring, E));
            _globals.SetItemRaw("type", new LuaMethod(Standard.type, E));
            _globals.SetItemRaw("_VERSION", Standard._VERSION);
            _globals.SetItemRaw("_NET", Standard._NET);

            #region if LuaLibraries.IO
            if (libraries.HasFlag(LuaLibraries.IO))
            {
                IO._Init(E);
                LuaTable io = new LuaTable();

                io.SetItemRaw("close", new LuaMethod(IO.close, E));
                io.SetItemRaw("flush", new LuaMethod(IO.flush, E));
                io.SetItemRaw("input", new LuaMethod(IO.input, E));
                io.SetItemRaw("lines", new LuaMethod(IO.lines, E));
                io.SetItemRaw("open", new LuaMethod(IO.open, E));
                io.SetItemRaw("output", new LuaMethod(IO.output, E));
                io.SetItemRaw("read", new LuaMethod(IO.read, E));
                io.SetItemRaw("tmpfile", new LuaMethod(IO.tmpfile, E));
                io.SetItemRaw("type", new LuaMethod(IO.type, E));
                io.SetItemRaw("write", new LuaMethod(IO.write, E));

                _globals.SetItemRaw("dofile", new LuaMethod(IO.dofile, E));
                _globals.SetItemRaw("load", new LuaMethod(IO.load, E));
                _globals.SetItemRaw("loadfile", new LuaMethod(IO.loadfile, E));
                _globals.SetItemRaw("io", io);
            }
            #endregion
            #region if LuaLibraries.String
            if (libraries.HasFlag(LuaLibraries.String))
            {
                LuaTable str = new LuaTable();
                str.SetItemRaw("byte", new LuaMethod(String.Byte, E));
                str.SetItemRaw("char", new LuaMethod(String.Char, E));
                str.SetItemRaw("find", new LuaMethod(String.find, E));
                str.SetItemRaw("format", new LuaMethod(String.foramt, E));
                str.SetItemRaw("gmatch", new LuaMethod(String.gmatch, E));
                str.SetItemRaw("gsub", new LuaMethod(String.gsub, E));
                str.SetItemRaw("len", new LuaMethod(String.len, E));
                str.SetItemRaw("lower", new LuaMethod(String.lower, E));
                str.SetItemRaw("match", new LuaMethod(String.match, E));
                str.SetItemRaw("rep", new LuaMethod(String.rep, E));
                str.SetItemRaw("reverse", new LuaMethod(String.reverse, E));
                str.SetItemRaw("sub", new LuaMethod(String.sub, E));
                str.SetItemRaw("upper", new LuaMethod(String.upper, E));

                _globals.SetItemRaw("string", str);
            }
            #endregion
            #region if LuaLibraries.Math
            if (libraries.HasFlag(LuaLibraries.Math))
            {
                LuaTable math = new LuaTable();
                math.SetItemRaw("abs", Math.MathHelper.Create("math.abs", System.Math.Abs, E));
                math.SetItemRaw("acos", Math.MathHelper.Create("math.acos", System.Math.Acos, E));
                math.SetItemRaw("asin", Math.MathHelper.Create("math.asin", System.Math.Asin, E));
                math.SetItemRaw("atan", Math.MathHelper.Create("math.atan", System.Math.Atan, E));
                math.SetItemRaw("atan2", Math.MathHelper.Create("math.atan2", System.Math.Atan2, E));
                math.SetItemRaw("ceil", Math.MathHelper.Create("math.ceil", System.Math.Ceiling, E));
                math.SetItemRaw("cos", Math.MathHelper.Create("math.cos", System.Math.Cos, E));
                math.SetItemRaw("cosh", Math.MathHelper.Create("math.cosh", System.Math.Cosh, E));
                math.SetItemRaw("deg", new LuaMethod(Math.deg, E));
                math.SetItemRaw("exp", Math.MathHelper.Create("math.exp", System.Math.Exp, E));
                math.SetItemRaw("floor", Math.MathHelper.Create("math.floor", System.Math.Floor, E));
                math.SetItemRaw("fmod", Math.MathHelper.Create("math.fmod", System.Math.IEEERemainder, E));
                math.SetItemRaw("frexp", new LuaMethod(Math.frexp, E));
                math.SetItemRaw("huge", double.PositiveInfinity);
                math.SetItemRaw("ldexp", new LuaMethod(Math.ldexp, E));
                math.SetItemRaw("log", new LuaMethod(Math.log, E));
                math.SetItemRaw("max", new LuaMethod(Math.max, E));
                math.SetItemRaw("min", new LuaMethod(Math.min, E));
                math.SetItemRaw("modf", new LuaMethod(Math.modf, E));
                math.SetItemRaw("pi", System.Math.PI);
                math.SetItemRaw("pow", Math.MathHelper.Create("math.pow", System.Math.Pow, E));
                math.SetItemRaw("rad", new LuaMethod(Math.rad, E));
                math.SetItemRaw("random", new LuaMethod(Math.random, E));
                math.SetItemRaw("randomseed", new LuaMethod(Math.randomseed, E));
                math.SetItemRaw("sin", Math.MathHelper.Create("math.sin", System.Math.Sin, E));
                math.SetItemRaw("sinh", Math.MathHelper.Create("math.sinh", System.Math.Sinh, E));
                math.SetItemRaw("sqrt", Math.MathHelper.Create("math.sqrt", System.Math.Sqrt, E));
                math.SetItemRaw("tan", Math.MathHelper.Create("math.tan", System.Math.Tan, E));
                math.SetItemRaw("tanh", Math.MathHelper.Create("math.tanh", System.Math.Tanh, E));

                _globals.SetItemRaw("math", math);
            }
            #endregion
            #region if LuaLibraries.Coroutine
            if (libraries.HasFlag(LuaLibraries.Coroutine))
            {
                LuaTable coroutine = new LuaTable();
                coroutine.SetItemRaw("create", new LuaMethod(Coroutine.create, E));
                coroutine.SetItemRaw("resume", new LuaMethod(Coroutine.resume, E));
                coroutine.SetItemRaw("running", new LuaMethod(Coroutine.running, E));
                coroutine.SetItemRaw("status", new LuaMethod(Coroutine.status, E));
                coroutine.SetItemRaw("wrap", new LuaMethod(Coroutine.wrap, E));
                coroutine.SetItemRaw("yield", new LuaMethod(Coroutine.yield, E));

                _globals.SetItemRaw("coroutine", coroutine);
            }
            #endregion
            #region if LuaLibraries.OS
            if (libraries.HasFlag(LuaLibraries.OS))
            {
                LuaTable os = new LuaTable();
                os.SetItemRaw("clock", new LuaMethod(OS.clock, E));
                os.SetItemRaw("date", new LuaMethod(OS.date, E));
                os.SetItemRaw("difftime", new LuaMethod(OS.difftime, E));
                os.SetItemRaw("exit", new LuaMethod(OS.exit, E));
                os.SetItemRaw("getenv", new LuaMethod(OS.getenv, E));
                os.SetItemRaw("remove", new LuaMethod(OS.remove, E));
                os.SetItemRaw("rename", new LuaMethod(OS.rename, E));
                os.SetItemRaw("setlocale", new LuaMethod(OS.setlocale, E));
                os.SetItemRaw("time", new LuaMethod(OS.time, E));
                os.SetItemRaw("tmpname", new LuaMethod(OS.tmpname, E));

                _globals.SetItemRaw("os", os);
            }
            #endregion
            #region if LuaLibraries.Bit32
            if (libraries.HasFlag(LuaLibraries.Bit32))
            {
                LuaTable bit32 = new LuaTable();
                bit32.SetItemRaw("arshift", new LuaMethod(Bit32.arshift, E));
                bit32.SetItemRaw("band", new LuaMethod(Bit32.band, E));
                bit32.SetItemRaw("bnot", new LuaMethod(Bit32.bnot, E));
                bit32.SetItemRaw("bor", new LuaMethod(Bit32.bor, E));
                bit32.SetItemRaw("btest", new LuaMethod(Bit32.btest, E));
                bit32.SetItemRaw("bxor", new LuaMethod(Bit32.bxor, E));
                bit32.SetItemRaw("extract", new LuaMethod(Bit32.extract, E));
                bit32.SetItemRaw("replace", new LuaMethod(Bit32.replace, E));
                bit32.SetItemRaw("lrotate", new LuaMethod(Bit32.lrotate, E));
                bit32.SetItemRaw("lshift", new LuaMethod(Bit32.lshift, E));
                bit32.SetItemRaw("rrotate", new LuaMethod(Bit32.rrotate, E));
                bit32.SetItemRaw("rshift", new LuaMethod(Bit32.rshift, E));

                _globals.SetItemRaw("bit32", bit32);
            }
            #endregion
            #region if LuaLibraries.Table
            if (libraries.HasFlag(LuaLibraries.Table))
            {
                LuaTable table = new LuaTable();
                table.SetItemRaw("concat", new LuaMethod(Table.concat, E));
                table.SetItemRaw("insert", new LuaMethod(Table.insert, E));
                table.SetItemRaw("pack", new LuaMethod(Table.pack, E));
                table.SetItemRaw("remove", new LuaMethod(Table.remove, E));
                table.SetItemRaw("sort", new LuaMethod(Table.sort, E));
                table.SetItemRaw("unpack", new LuaMethod(Table.unpack, E));

                _globals.SetItemRaw("table", table);
            }
            #endregion
            #region if LuaLibraries.Module
            if (libraries.HasFlag(LuaLibraries.Modules))
            {
                _globals.SetItemRaw("require", new LuaMethod(Module.require, E));
            }
            #endregion
        }
        static string _type(object o)
        {
            object value = RuntimeHelper.GetValue(o);

            if (value == null)
                return "nil";
            else if (value is string)
                return "string";
            else if (value is double)
                return "number";
            else if (value is bool)
                return "boolean";
            else if (value is LuaTable)
                return "table";
            else if (value is LuaMethod)
                return "function";
            else if (value is LuaThread)
                return "thread";
            else
                return "userdata";
        }
    }
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
}