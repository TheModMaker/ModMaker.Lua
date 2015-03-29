using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Reflection;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        /// <summary>
        /// Contains the standard Lua library functions.
        /// </summary>
        static class Standard
        {
            public static void Initialize(ILuaEnvironment E)
            {
                var table = E.GlobalsTable;
                table.SetItemRaw("assert", new assert(E));
                table.SetItemRaw("collectgarbage", new collectgarbage(E));
                table.SetItemRaw("error", new error(E));
                table.SetItemRaw("getmetatable", new getmetatable(E));
                table.SetItemRaw("ipairs", new ipairs(E));
                table.SetItemRaw("next", new next(E));
                table.SetItemRaw("pairs", new pairs(E));
                table.SetItemRaw("pcall", new pcall(E));
                table.SetItemRaw("print", new print(E));
                table.SetItemRaw("rawequal", new rawequal(E));
                table.SetItemRaw("rawget", new rawget(E));
                table.SetItemRaw("rawlen", new rawlen(E));
                table.SetItemRaw("rawset", new rawset(E));
                table.SetItemRaw("select", new select(E));
                table.SetItemRaw("setmetatable", new setmetatable(E));
                table.SetItemRaw("tonumber", new tonumber(E));
                table.SetItemRaw("tostring", new tostring(E));
                table.SetItemRaw("type", new type(E));
                table.SetItemRaw("_VERSION", Standard._VERSION);
                table.SetItemRaw("_NET", Standard._NET);
                table.SetItemRaw("_G", table);
            }

            const string _VERSION = "Lua 5.2";
            const string _NET = ".NET 4.0";

            sealed class assert : LuaFrameworkMethod
            {
                public assert(ILuaEnvironment E) : base(E, "assert") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'assert'.");

                    object obj = args[0];
                    string message = "Assertion failed: '" + (args.Length > 1 ? args[1] : null) as string + "'.";

                    if (obj == null || obj as bool? == false)
                        throw new AssertException(message);
                    else
                        return new MultipleReturn(obj);
                }
            }
            sealed class collectgarbage : LuaFrameworkMethod
            {
                public collectgarbage(ILuaEnvironment E) : base(E, "collectgarbage") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'collectgarbage'.");
                    string opt = args[0] as string;
                    if (opt == null)
                        throw new ArgumentException("First argument to 'collectgarbage' must be a string.");
                    object arg = args.Length > 1 ? args[1] : null;

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
                            throw new ArgumentException(
                                "Second argument to 'collectgarbage' with opt as 'collect' must be a 0, 1 or 2.");

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
                    else if (opt == "stop" || opt == "restart" || opt == "step" || opt == "setpause" ||
                        opt == "setstepmul" || opt == "generational" || opt == "incremental")
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
            }
            sealed class error : LuaFrameworkMethod
            {
                public error(ILuaEnvironment E) : base(E, "error") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'error'.");

                    string message = args[0] as string;

                    if (message == null)
                        throw new ArgumentException("First argument to function 'error' must be a string.");

                    throw new AssertException(message);
                }
            }
            sealed class getmetatable : LuaFrameworkMethod
            {
                public getmetatable(ILuaEnvironment E) : base(E, "getmetatable") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'getmetatable'.");

                    ILuaTable table = args[0] as ILuaTable;

                    if (table == null)
                        return new MultipleReturn();

                    ILuaTable meta = table.MetaTable;
                    if (meta != null)
                    {
                        object meth = meta.GetItemRaw("__metatable");
                        if (meth != null)
                            return new MultipleReturn(meth);
                    }

                    return new MultipleReturn(meta);
                }
            }
            sealed class ipairs : LuaFrameworkMethod
            {
                public ipairs(ILuaEnvironment E) : base(E, "ipairs") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args == null || args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'ipairs'.");

                    ILuaTable table = args[0] as ILuaTable;

                    if (table == null)
                        throw new ArgumentException("First argument to 'iparis' must be a table.");

                    ILuaTable meta = table.MetaTable;
                    if (meta != null)
                    {
                        IMethod meth = meta.GetItemRaw("__ipairs") as IMethod;
                        if (meth != null)
                        {
                            var ret = meth.Invoke(table, true, null, new object[0]);
                            return ret.AdjustResults(3);
                        }
                    }

                    return new MultipleReturn(new _ipairs_itr(Environment), table, 0);
                }
            }
            sealed class next : LuaFrameworkMethod
            {
                public next(ILuaEnvironment E) : base(E, "next") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'next'.");

                    ILuaTable table = args[0] as ILuaTable;
                    object index = args.Length > 1 ? args[1] : null;
                    if (table == null)
                        throw new ArgumentException("First parameter to 'next' must be a table.");

                    var t = LuaTableNet.GetNext(table, index);
                    return new MultipleReturn(t.Item1, t.Item2);
                }
            }
            sealed class overload : LuaFrameworkMethod
            {
                public overload(ILuaEnvironment E) : base(E, "overload") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting at least two arguments to function 'overload'.");

                    IMethod meth = args[0] as IMethod;
                    object obj = args[1];

                    if (meth == null)
                        throw new ArgumentException("First argument to function 'overload' must be a method.");
                    if (obj == null || !(obj is double) || ((double)obj % 1 != 0))
                        throw new ArgumentException("Second argument to function 'overload' must be an integer.");

                    int i = Convert.ToInt32((double)obj);

                    return meth.Invoke(null, false, i, null, args.Skip(2).ToArray());
                }
            }
            sealed class pairs : LuaFrameworkMethod
            {
                public pairs(ILuaEnvironment E) : base(E, "pairs") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'pairs'.");

                    object t = args[0];
                    if (t is ILuaTable)
                    {
                        ILuaTable table = t as ILuaTable;
                        ILuaTable meta = table.MetaTable;
                        if (meta != null)
                        {
                            IMethod p = meta.GetItemRaw("__pairs") as IMethod;
                            if (p != null)
                            {
                                var ret = p.Invoke(table, true, null, new object[0]);
                                return ret.AdjustResults(3);
                            }
                        }

                        return new MultipleReturn(new next(Environment), table, null);
                    }
                    else
                        throw new ArgumentException("First argument to 'pairs' must be a table.");
                }
            }
            sealed class pcall : LuaFrameworkMethod
            {
                public pcall(ILuaEnvironment E) : base(E, "pcall") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'pcall'.");

                    object func = args[0];
                    if (func is IMethod)
                    {
                        try
                        {
                            var ret = (func as IMethod).Invoke(null, false, null, args.Skip(1).ToArray());
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
            }
            sealed class print : LuaFrameworkMethod
            {
                public print(ILuaEnvironment E) : base(E, "print") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    StringBuilder str = new StringBuilder();
                    if (args != null)
                    {
                        for (int i = 0; i < args.Length; i++)
                        {
                            object temp = args[i];
                            if (temp is LuaUserData)
                                temp = ((LuaUserData)temp).Backing;
                            str.Append((temp ?? "").ToString());
                            str.Append('\t');
                        }
                        str.Append("\n");
                    }

                    Stream s = Environment.Settings.Stdout;
                    byte[] txt = (Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(str.ToString());
                    s.Write(txt, 0, txt.Length);

                    return new MultipleReturn();
                }
            }
            sealed class rawequal : LuaFrameworkMethod
            {
                public rawequal(ILuaEnvironment E) : base(E, "rawequal") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args == null || args.Length < 2)
                        throw new ArgumentException("Expecting two arguments to function 'rawget'.");

                    object v1 = args[0];
                    object v2 = args[1];

                    return new MultipleReturn(object.Equals(v1, v2));
                }
            }
            sealed class rawget : LuaFrameworkMethod
            {
                public rawget(ILuaEnvironment E) : base(E, "rawget") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting two arguments to function 'rawget'.");

                    object table = args[0];
                    object index = args[1];

                    if (table is ILuaTable)
                        return new MultipleReturn((table as ILuaTable).GetItemRaw(index));
                    else
                        throw new ArgumentException("First argument to function 'rawget' must be a table.");
                }
            }
            sealed class rawlen : LuaFrameworkMethod
            {
                public rawlen(ILuaEnvironment E) : base(E, "rawlen") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'rawlen'.");

                    object table = args[0];

                    if (table is string)
                        return new MultipleReturn((double)(table as string).Length);
                    else if (table is ILuaTable)
                        return new MultipleReturn((double)(table as ILuaTable).Length);
                    else
                        throw new ArgumentException("Argument to 'rawlen' must be a string or table.");
                }
            }
            sealed class rawset : LuaFrameworkMethod
            {
                public rawset(ILuaEnvironment E) : base(E, "rawset") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 3)
                        throw new ArgumentException("Expecting three arguments to function 'rawset'.");

                    object table = args[0];
                    object index = args[1];
                    object value = args[2];

                    if (!(table is ILuaTable))
                        throw new ArgumentException("First argument to 'rawset' must be a table.");
                    if (index == null)
                        throw new ArgumentException("Second argument to 'rawset' cannot be nil.");

                    ((ILuaTable)table).SetItemRaw(index, value);

                    return new MultipleReturn(table);
                }
            }
            sealed class select : LuaFrameworkMethod
            {
                public select(ILuaEnvironment E) : base(E, "select") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'select'.");

                    object index = args[0];

                    if (index as string == "#")
                    {
                        return new MultipleReturn((double)(args.Length - 1));
                    }
                    else if (index is double)
                    {
                        double d = (double)index;
                        if (d < 0)
                            d = args.Length + d;
                        return new MultipleReturn(args.Skip((int)d));
                    }
                    else
                        throw new ArgumentException("First argument to function 'select' must be a number or the string '#'.");
                }
            }
            sealed class setmetatable : LuaFrameworkMethod
            {
                public setmetatable(ILuaEnvironment E) : base(E, "setmetatable") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting two arguments to function 'setmetatable'.");

                    ILuaTable table = args[0] as ILuaTable;
                    object metatable = args[1];

                    if (table == null)
                        throw new ArgumentException("First argument to function 'setmetatable' must be a table.");

                    if (metatable == null)
                        table.MetaTable = null;
                    else if (metatable is ILuaTable)
                        table.MetaTable = (ILuaTable)metatable;
                    else
                        throw new ArgumentException("Attempt to set metatable to a '" + _type(metatable) + "' type.");

                    return new MultipleReturn(table);
                }
            }
            sealed class tonumber : LuaFrameworkMethod
            {
                public tonumber(ILuaEnvironment E) : base(E, "tonumber") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'tonumber'.");

                    double? d = Environment.Runtime.ToNumber(args[0]);
                    if (d.HasValue)
                        return new MultipleReturn(d.Value);
                    else
                        return new MultipleReturn();
                }
            }
            sealed class tostring : LuaFrameworkMethod
            {
                public tostring(ILuaEnvironment E) : base(E, "tostring") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'tostring'.");

                    object val = args[0];
                    if (val is ILuaTable)
                    {
                        ILuaTable tab = (ILuaTable)val;
                        var meta = tab.MetaTable;
                        if (meta != null)
                        {
                            var m = meta.GetItemRaw("__tostring");
                            if (m != null && m is IMethod)
                            {
                                var result = (m as IMethod).Invoke(val, true, null, new object[0]);
                                return new MultipleReturn((object)result[0].ToString());
                            }
                        }

                        return new MultipleReturn((object)val.ToString());
                    }
                    else if (val is LuaUserData)
                        return new MultipleReturn((object)((LuaUserData)val).Backing.ToString());
                    else
                        return new MultipleReturn((object)(val ?? "").ToString());
                }
            }
            sealed class type : LuaFrameworkMethod
            {
                public type(ILuaEnvironment E) : base(E, "type") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'type'.");

                    object value = args[0];
                    return new MultipleReturn((object)_type(value));
                }
            }

            sealed class _ipairs_itr : LuaFrameworkMethod
            {
                public _ipairs_itr(ILuaEnvironment E) : base(E, "_ipairs_itr") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting two arguments to 'ipairs iterator'.");

                    ILuaTable table = args[0] as ILuaTable;
                    double index = args[1] as double? ?? -1;

                    if (table == null)
                        throw new ArgumentException("First argument to function 'ipairs iterator' must be a table.");
                    if (index < 0 || index % 1 != 0)
                        throw new ArgumentException("Second argument to function 'ipairs iterator' must be a positive integer.");
                    index++;

                    var ret = table.GetItemRaw(index);
                    if (ret == null)
                        return new MultipleReturn();
                    else
                        return new MultipleReturn(index, ret);
                }
            }
        }
    }
}
