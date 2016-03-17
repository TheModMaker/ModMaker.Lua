using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Reflection;
using ModMaker.Lua.Runtime.LuaValues;

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
                Register(E, table, (Func<ILuaValue, ILuaValue, ILuaValue>)assert);
                Register(E, table, (Func<ILuaValue, string>)tostring);
                Register(E, table, (Func<string, int?, object[]>)collectgarbage);
                Register(E, table, (Action<string>)error);
                Register(E, table, (Func<ILuaValue, ILuaValue>)getmetatable);
                Register(E, table, (Func<ILuaTable, object[]>)ipairs);
                Register(E, table, (Func<ILuaTable, ILuaValue, object[]>)next);
                Register(E, table, (Func<ILuaTable, object[]>)pairs);
                Register(E, table, (Func<ILuaValue, ILuaValue, bool>)rawequal);
                Register(E, table, (Func<ILuaTable, ILuaValue, ILuaValue>)rawget);
                Register(E, table, (Func<ILuaTable, ILuaValue>)rawlen);
                Register(E, table, (Func<ILuaTable, ILuaValue, ILuaValue, ILuaValue>)rawset);
                Register(E, table, (Func<ILuaValue, object[], IEnumerable<object>>)select);
                Register(E, table, (Func<ILuaTable, ILuaTable, ILuaValue>)setmetatable);
                Register(E, table, (Func<ILuaTable, double?>)tonumber);
                Register(E, table, (Func<ILuaTable, string>)type);

                table.SetItemRaw(new LuaString("overload"), new overload(E));
                table.SetItemRaw(new LuaString("pcall"), new pcall(E));
                table.SetItemRaw(new LuaString("print"), new print(E));

                table.SetItemRaw(new LuaString("_VERSION"),
                                 E.Runtime.CreateValue(Standard._VERSION));
                table.SetItemRaw(new LuaString("_NET"),
                                 E.Runtime.CreateValue(Standard._NET));
                table.SetItemRaw(new LuaString("_G"), table);
            }

            static readonly string _VERSION = "Lua 5.2";
            static readonly string _NET = ".NET 4.0";

            static readonly ILuaValue _tostring = new LuaString("__tostring");
            static readonly ILuaValue _metamethod = new LuaString("__metatable");
            static readonly ILuaValue _ipairs = new LuaString("__ipairs");
            static readonly ILuaValue _pairs = new LuaString("__pairs");

            static ILuaValue assert(ILuaValue value, ILuaValue obj = null)
            {
                string message = "Assertion failed: '" + (obj != null ? obj.ToString() : "") + "'.";
                if (value.IsTrue)
                    return obj;
                else
                    throw new AssertException(message);
            }
            [MultipleReturn]
            static object[] collectgarbage(string operation = "", int? gen = null)
            {
                switch (operation)
                {
                case null:
                case "":
                case "collect":
                    if (gen < 0 || gen > 2)
                        throw new ArgumentException("Second argument to 'collectgarbage' with operation as 'collect' must be a 0, 1 or 2.");

                    if (!gen.HasValue)
                        GC.Collect();
                    else
                        GC.Collect(gen.Value);

                    return new object[0];
                case "count":
                    double mem = GC.GetTotalMemory(false);
                    return new object[] { mem, mem % 1024 };
                case "isrunning":
                    return new object[] { true };
                case "stop":
                case "restart":
                case "step":
                case "setpause":
                case "setstepmul":
                case "generational":
                case "incremental":
                    throw new ArgumentException(
                        "The option '" + operation + "' is not supported by this framework.");
                default:
                    throw new ArgumentException(
                        "The option '" + operation + "' is not recognized to function 'collectgarbage'.");
                }
            }
            static string tostring(ILuaValue value)
            {
                if (value.ValueType == LuaValueType.Table)
                {
                    var meta = ((ILuaTable)value).MetaTable;
                    if (meta != null)
                    {
                        var m = meta.GetItemRaw(_tostring);
                        if (m != null && m.ValueType == LuaValueType.Function)
                        {
                            var result = m.Invoke(value, true, -1, LuaMultiValue.Empty);
                            return result[0].ToString();
                        }
                    }
                }

                return value.ToString();
            }
            static void error(string message)
            {
                throw new AssertException(message);
            }
            static ILuaValue getmetatable(ILuaValue value)
            {
                if (value.ValueType != LuaValueType.Table)
                    return LuaNil.Nil;

                ILuaTable meta = ((ILuaTable)value).MetaTable;
                if (meta != null)
                {
                    ILuaValue method = meta.GetItemRaw(_metamethod);
                    if (method != null && method != LuaNil.Nil)
                        return method;
                }

                return meta;
            }
            [MultipleReturn]
            static object[] ipairs(ILuaTable table)
            {
                ILuaTable meta = table.MetaTable;
                if (meta != null)
                {
                    ILuaValue method = meta.GetItemRaw(_ipairs);
                    if (method != null && method != LuaNil.Nil)
                    {
                        var ret = method.Invoke(table, true, -1, LuaMultiValue.Empty);
                        // The runtime will correctly expand the results (because the multi-value
                        // is at the end).
                        return new object[] { ret.AdjustResults(3) };
                    }
                }

                return new object[] { (Func<ILuaTable, double, object[]>)_ipairs_itr, table, 0 };
            }
            [MultipleReturn]
            static object[] next(ILuaTable table, ILuaValue index)
            {
                bool return_next = index == LuaNil.Nil;
                foreach (var item in table)
                {
                    if (return_next)
                        return new object[] { item.Key, item.Value };
                    else if (item.Key == index)
                        return_next = true;
                }

                // return nil, nil;
                return new object[0];
            }
            [MultipleReturn]
            static object[] pairs(ILuaTable table)
            {
                ILuaTable meta = table.MetaTable;
                if (meta != null)
                {
                    ILuaValue p = meta.GetItemRaw(_pairs);
                    if (p != null && p != LuaNil.Nil)
                    {
                        var ret = p.Invoke(table, true, -1, LuaMultiValue.Empty);
                        return new object[] { ret.AdjustResults(3) };
                    }
                }

                return new object[] { (Func<ILuaTable, ILuaValue, object[]>)next, table };
            }
            static bool rawequal(ILuaValue v1, ILuaValue v2)
            {
                return object.Equals(v1, v2);
            }
            static ILuaValue rawget(ILuaTable table, ILuaValue index)
            {
                return table.GetItemRaw(index);
            }
            static ILuaValue rawlen(ILuaTable table)
            {
                return table.RawLength();
            }
            static ILuaValue rawset(ILuaTable table, ILuaValue index, ILuaValue value)
            {
                table.SetItemRaw(index, value);
                return table;
            }
            [MultipleReturn]
            static IEnumerable<object> select(ILuaValue index, params object[] args)
            {
                if (index.Equals("#"))
                {
                    return new object[] { args.Length };
                }
                else if (index.ValueType == LuaValueType.Number)
                {
                    double ind = index.AsDouble() ?? 0;
                    if (ind < 0)
                        ind = args.Length + ind + 1;

                    return args.Skip((int)(ind - 1));
                }
                else
                {
                    throw new ArgumentException(
                        "First argument to function 'select' must be a number or the string '#'.");
                }
            }
            static ILuaValue setmetatable(ILuaTable table, ILuaValue metatable)
            {
                if (metatable == LuaNil.Nil)
                    table.MetaTable = null;
                else if (metatable.ValueType == LuaValueType.Table)
                    table.MetaTable = (ILuaTable)metatable;
                else
                {
                    throw new ArgumentException(
                        "Second argument to 'setmetatable' must be a table.");
                }

                return table;
            }
            static double? tonumber(ILuaValue value)
            {
                return value.AsDouble();
            }
            static string type(ILuaValue value)
            {
                return value.ValueType.ToString().ToLower();
            }

            sealed class overload : LuaFrameworkFunction
            {
                public overload(ILuaEnvironment E) : base(E, "overload") { }

                protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args)
                {
                    if (args.Count < 2)
                        throw new ArgumentException("Expecting at least two arguments to function 'overload'.");

                    ILuaValue meth = args[0];
                    ILuaValue obj = args[1];

                    if (meth.ValueType != LuaValueType.Function)
                        throw new ArgumentException("First argument to function 'overload' must be a method.");
                    if (obj.ValueType != LuaValueType.Number || ((double)obj.GetValue() % 1 != 0))
                        throw new ArgumentException("Second argument to function 'overload' must be an integer.");

                    int i = Convert.ToInt32((double)obj.GetValue());

                    return meth.Invoke(null, false, i, Environment.Runtime.CreateMultiValue(args.Skip(2).ToArray()));
                }
            }
            sealed class pcall : LuaFrameworkFunction
            {
                public pcall(ILuaEnvironment E) : base(E, "pcall") { }

                protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args)
                {
                    if (args.Count < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'pcall'.");

                    ILuaValue func = args[0];
                    if (func.ValueType == LuaValueType.Function)
                    {
                        try
                        {
                            var ret = func.Invoke(LuaNil.Nil, false, -1, Environment.Runtime.CreateMultiValue(args.Skip(1).ToArray()));
                            return Environment.Runtime.CreateMultiValue(new ILuaValue[] { LuaBoolean.True }.Union(ret).ToArray());
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
                            return Environment.Runtime.CreateMultiValueFromObj(false, e.Message, e);
                        }
                    }
                    else
                        throw new ArgumentException("First argument to 'pcall' must be a function.");
                }
            }
            sealed class print : LuaFrameworkFunction
            {
                public print(ILuaEnvironment E) : base(E, "print") { }

                protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args)
                {
                    StringBuilder str = new StringBuilder();
                    if (args != null)
                    {
                        for (int i = 0; i < args.Count; i++)
                        {
                            ILuaValue temp = args[i];
                            str.Append(temp.ToString());
                            str.Append('\t');
                        }
                        str.Append("\n");
                    }

                    Stream s = Environment.Settings.Stdout;
                    byte[] txt = (Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(str.ToString());
                    s.Write(txt, 0, txt.Length);

                    return LuaMultiValue.Empty;
                }
            }
            
            [MultipleReturn]
            static object[] _ipairs_itr(ILuaTable table, double index)
            {
                if (index < 0 || index % 1 != 0)
                    throw new ArgumentException("Second argument to function 'ipairs iterator' must be a positive integer.");
                index++;

                ILuaValue ret = table.GetItemRaw(new LuaNumber(index));
                if (ret == null || ret == LuaNil.Nil)
                    return new object[0];
                else
                    return new object[] { index, ret };
            }
        }
    }
}