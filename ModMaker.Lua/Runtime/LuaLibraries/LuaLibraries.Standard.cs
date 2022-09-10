// Copyright 2014 Jacob Trimble
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ModMaker.Lua.Runtime.LuaValues;

#nullable enable

namespace ModMaker.Lua.Runtime {
  static partial class LuaStaticLibraries {
    static class Standard {
      public static void Initialize(ILuaEnvironment env) {
        var table = env.GlobalsTable;
        Register(env, table, (Func<ILuaValue, ILuaValue, ILuaValue>)assert);
        Register(env, table, (Func<ILuaValue, string>)tostring);
        Register(env, table, (Func<string, int?, object[]>)collectgarbage);
        Register(env, table, (Action<string>)error);
        Register(env, table, (Func<ILuaValue, ILuaValue>)getmetatable);
        Register(env, table, (Func<ILuaTable, object[]>)ipairs);
        Register(env, table, (Func<ILuaTable, ILuaValue, object[]>)next);
        Register(env, table, (Func<ILuaTable, object[]>)pairs);
        Register(env, table, (Func<ILuaValue, ILuaValue, bool>)rawequal);
        Register(env, table, (Func<ILuaTable, ILuaValue, ILuaValue>)rawget);
        Register(env, table, (Func<ILuaTable, ILuaValue>)rawlen);
        Register(env, table, (Func<ILuaTable, ILuaValue, ILuaValue, ILuaValue>)rawset);
        Register(env, table, (Func<ILuaValue, ILuaValue[], IEnumerable<ILuaValue>>)select);
        Register(env, table, (Func<ILuaTable, ILuaTable, ILuaValue>)setmetatable);
        Register(env, table, (Func<ILuaTable, double?>)tonumber);
        Register(env, table, (Func<ILuaTable, string>)type);
        Register(env, table, (Func<LuaFunction, int, ILuaValue>)overload);

        table.SetItemRaw(new LuaString("pcall"), new pcall(env));
        table.SetItemRaw(new LuaString("print"), new print(env));

        table.SetItemRaw(new LuaString("_VERSION"), new LuaString(_version));
        table.SetItemRaw(new LuaString("_NET"), new LuaString(_net));
        table.SetItemRaw(new LuaString("_G"), table);
      }

      private const string _version = "Lua 5.2";
      private const string _net = ".NET 4.0";

      static readonly ILuaValue _tostring = new LuaString("__tostring");
      static readonly ILuaValue _metamethod = new LuaString("__metatable");
      static readonly ILuaValue _ipairs = new LuaString("__ipairs");
      static readonly ILuaValue _pairs = new LuaString("__pairs");

      static ILuaValue assert(ILuaValue value, ILuaValue? obj = null) {
        string message = $"Assertion failed: '{obj?.ToString() ?? ""}'.";
        if (value.IsTrue) {
          return obj ?? LuaNil.Nil;
        } else {
          throw new AssertException(message);
        }
      }
      [MultipleReturn]
      static object[] collectgarbage(string operation = "", int? gen = null) {
        switch (operation) {
          case null:
          case "":
          case "collect":
            if (gen < 0 || gen > 2) {
              throw new ArgumentException("Second argument to 'collectgarbage' with operation as " +
                                          "'collect' must be a 0, 1 or 2.");
            }

            if (!gen.HasValue) {
              GC.Collect();
            } else {
              GC.Collect(gen.Value);
            }

            return Array.Empty<object>();
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
                $"The option '{operation}' is not supported by this framework.");
          default:
            throw new ArgumentException(
                $"The option '{operation}' is not recognized to function 'collectgarbage'.");
        }
      }
      static string tostring(ILuaValue value) {
        if (value.ValueType == LuaValueType.Table) {
          var meta = ((ILuaTable)value).MetaTable;
          if (meta != null) {
            var m = meta.GetItemRaw(_tostring);
            if (m != null && m.ValueType == LuaValueType.Function) {
              var result = m.Invoke(value, true, LuaMultiValue.Empty);
              return result[0].ToString()!;
            }
          }
        }

        return value.ToString()!;
      }
      static void error(string message) {
        throw new AssertException(message);
      }
      static ILuaValue getmetatable(ILuaValue value) {
        if (value.ValueType != LuaValueType.Table) {
          return LuaNil.Nil;
        }

        ILuaTable meta = ((ILuaTable)value).MetaTable;
        if (meta != null) {
          ILuaValue method = meta.GetItemRaw(_metamethod);
          if (method != null && method != LuaNil.Nil) {
            return method;
          }
        }

        return (ILuaValue?)meta ?? LuaNil.Nil;
      }
      [MultipleReturn]
      static object[] ipairs(ILuaTable table) {
        ILuaTable meta = table.MetaTable;
        if (meta != null) {
          ILuaValue method = meta.GetItemRaw(_ipairs);
          if (method != null && method != LuaNil.Nil) {
            var ret = method.Invoke(table, true, LuaMultiValue.Empty);
            // The runtime will correctly expand the results (because the multi-value
            // is at the end).
            return new object[] { ret.AdjustResults(3) };
          }
        }

        return new object[] { (Func<ILuaTable, double, object[]>)_ipairs_itr, table, 0 };
      }
      [MultipleReturn]
      static object[] next(ILuaTable table, ILuaValue index) {
        bool return_next = index == LuaNil.Nil;
        foreach (var item in table) {
          if (return_next) {
            return new object[] { item.Key, item.Value };
          } else if (item.Key == index) {
            return_next = true;
          }
        }

        // return nil, nil;
        return Array.Empty<object>();
      }
      [MultipleReturn]
      static object[] pairs(ILuaTable table) {
        ILuaTable meta = table.MetaTable;
        if (meta != null) {
          ILuaValue p = meta.GetItemRaw(_pairs);
          if (p != null && p != LuaNil.Nil) {
            var ret = p.Invoke(table, true, LuaMultiValue.Empty);
            return new object[] { ret.AdjustResults(3) };
          }
        }

        return new object[] { (Func<ILuaTable, ILuaValue, object[]>)next, table };
      }
      static bool rawequal(ILuaValue v1, ILuaValue v2) {
        return Equals(v1, v2);
      }
      static ILuaValue rawget(ILuaTable table, ILuaValue index) {
        return table.GetItemRaw(index);
      }
      static ILuaValue rawlen(ILuaTable table) {
        return table.RawLength();
      }
      static ILuaValue rawset(ILuaTable table, ILuaValue index, ILuaValue value) {
        table.SetItemRaw(index, value);
        return table;
      }
      [MultipleReturn]
      static IEnumerable<ILuaValue> select(ILuaValue index, params ILuaValue[] args) {
        if (index.Equals("#")) {
          return new ILuaValue[] { LuaNumber.Create(args.Length) };
        } else if (index.ValueType == LuaValueType.Number) {
          double ind = index.AsDouble() ?? 1;
          if (ind == 0)
            throw new ArgumentException("select index out of range");
          if (ind < 0) {
            ind = args.Length + ind + 1;
          }

          return args.Skip((int)(ind - 1));
        } else {
          throw new ArgumentException(
              "First argument to function 'select' must be a number or the string '#'.");
        }
      }
      static ILuaValue setmetatable(ILuaTable table, ILuaValue metatable) {
        if (metatable == LuaNil.Nil) {
          table.MetaTable = null;
        } else if (metatable.ValueType == LuaValueType.Table) {
          table.MetaTable = (ILuaTable)metatable;
        } else {
          throw new ArgumentException("Second argument to 'setmetatable' must be a table.");
        }

        return table;
      }
      static double? tonumber(ILuaValue value) {
        return value.AsDouble();
      }
      static string type(ILuaValue value) {
        return value.ValueType.ToString().ToLower();
      }
      static ILuaValue overload(LuaFunction func, int index) {
        if (func is LuaOverloadFunction overload) {
          return overload.GetOverload(index);
        } else {
          // Allow calling with other function types, but just return the original function.
          return func;
        }
      }

      sealed class pcall : LuaFrameworkFunction {
        public pcall(ILuaEnvironment env) : base(env, "pcall") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          if (args.Count < 1) {
            throw new ArgumentException("Expecting at least one argument to function 'pcall'.");
          }

          ILuaValue func = args[0];
          if (func.ValueType == LuaValueType.Function) {
            try {
              var ret = func.Invoke(LuaNil.Nil, false, new LuaMultiValue(args.Skip(1).ToArray()));
              return new LuaMultiValue(new ILuaValue[] { LuaBoolean.True }.Concat(ret).ToArray());
            } catch (ThreadAbortException) {
              throw;
            } catch (ThreadInterruptedException) {
              throw;
            } catch (Exception e) {
              return LuaMultiValue.CreateMultiValueFromObj(false, e.Message, e);
            }
          } else {
            throw new ArgumentException("First argument to 'pcall' must be a function.");
          }
        }
      }
      sealed class print : LuaFrameworkFunction {
        public print(ILuaEnvironment env) : base(env, "print") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          StringBuilder str = new StringBuilder();
          if (args != null) {
            for (int i = 0; i < args.Count; i++) {
              ILuaValue temp = args[i];
              str.Append(temp.ToString());
              str.Append('\t');
            }
            str.Append('\n');
          }

          Stream? s = _environment.Settings.Stdout;
          if (s == null)
            throw new Exception("No standard out given");
          byte[] txt = (_environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(str.ToString());
          s.Write(txt, 0, txt.Length);

          return LuaMultiValue.Empty;
        }
      }

      [MultipleReturn]
      static object[] _ipairs_itr(ILuaTable table, double index) {
        if (index < 0 || index % 1 != 0) {
          throw new ArgumentException(
              "Second argument to function 'ipairs iterator' must be a positive integer.");
        }

        index++;

        ILuaValue ret = table.GetItemRaw(LuaNumber.Create(index));
        if (ret == null || ret == LuaNil.Nil) {
          return Array.Empty<object>();
        } else {
          return new object[] { index, ret };
        }
      }
    }
  }
}
