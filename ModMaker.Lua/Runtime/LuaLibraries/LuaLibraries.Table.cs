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
using System.Linq;
using System.Text;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  static partial class LuaStaticLibraries {
    class Table {
      public Table(ILuaEnvironment env) {
        _env = env;
      }

      public void Initialize() {
        ILuaValue table = new LuaTable();
        Register(_env, table, (Func<ILuaTable, string, int, int, string>)concat);
        Register(_env, table, (Action<ILuaTable, ILuaValue, ILuaValue>)insert);
        Register(_env, table, (Func<ILuaValue[], ILuaValue>)pack);
        Register(_env, table, (Func<ILuaTable, int?, ILuaValue>)remove);
        Register(_env, table, (Action<ILuaTable, ILuaValue>)sort);
        Register(_env, table, (Func<ILuaTable, int, int?, IEnumerable<ILuaValue>>)unpack);

        _env.GlobalsTable.SetItemRaw(new LuaString("table"), table);
      }

      readonly ILuaEnvironment _env;

      string concat(ILuaTable table, string sep = null, int i = 1, int j = -1) {
        CheckNotNull("table.concat", table);
        int len = (int)(table.Length().AsDouble() ?? 0);
        if (i >= len) {
          return "";
        }

        i = normalizeIndex_(len, i);
        j = normalizeIndex_(len, j);

        StringBuilder str = new StringBuilder();
        for (; i <= j; i++) {
          ILuaValue temp = table.GetItemRaw(LuaNumber.Create(i));
          if (temp.ValueType != LuaValueType.String && temp.ValueType != LuaValueType.Number) {
            throw new ArgumentException(
                $"Invalid '{temp.ValueType}' value for function 'table.concat'.");
          }

          if (str.Length > 0) {
            str.Append(sep);
          }

          str.Append(temp);
        }

        return str.ToString();
      }
      void insert(ILuaTable table, ILuaValue pos, ILuaValue value = null) {
        CheckNotNull("table.insert", table);
        CheckNotNull("table.insert", pos);
        double i;
        double len = table.Length().AsDouble() ?? 0;
        if (value == null) {
          value = pos;
          i = len + 1;
        } else {
          i = pos.AsDouble() ?? 0;
        }

        if (i > len + 1 || i < 1 || i % 1 != 0) {
          throw new ArgumentException(
              "Position given to function 'table.insert' is outside valid range.");
        }

        for (double d = len; d >= i; d--) {
          var temp = table.GetItemRaw(LuaNumber.Create(d));
          table.SetItemRaw(LuaNumber.Create(d + 1), temp);
        }
        table.SetItemRaw(pos, value);
      }
      ILuaValue pack(params ILuaValue[] args) {
        ILuaTable ret = new LuaTable();
        for (int i = 0; i < args.Length; i++) {
          ret.SetItemRaw(LuaNumber.Create(i + 1), args[i]);
        }
        ret.SetItemRaw(new LuaString("n"), LuaNumber.Create(args.Length));
        return ret;
      }
      ILuaValue remove(ILuaTable table, int? pos = null) {
        CheckNotNull("table.remove", table);

        double len = table.Length().AsDouble() ?? 0;
        pos = pos ?? (int)len;
        if (pos > len + 1 || pos < 1) {
          throw new ArgumentException(
              "Position given to function 'table.remove' is outside valid range.");
        }

        ILuaValue prev = LuaNil.Nil;
        for (double d = len; d >= pos; d--) {
          ILuaValue ind = LuaNumber.Create(d);
          ILuaValue temp = table.GetItemRaw(ind);
          table.SetItemRaw(ind, prev);
          prev = temp;
        }
        return prev;
      }
      void sort(ILuaTable table, ILuaValue comp = null) {
        CheckNotNull("table.sort", table);
        var comparer = new SortComparer(_env, comp);

        ILuaValue[] elems = unpack(table).OrderBy(k => k, comparer).ToArray();
        for (int i = 0; i < elems.Length; i++) {
          ILuaValue ind = LuaNumber.Create(i + 1);
          table.SetItemRaw(ind, elems[i]);
        }
      }
      [MultipleReturn]
      IEnumerable<ILuaValue> unpack(ILuaTable table, int i = 1, int? jOrNull = null) {
        CheckNotNull("table.unpack", table);
        int len = (int)(table.Length().AsDouble() ?? 0);
        int j = jOrNull ?? len;
        for (; i <= j; i++) {
          yield return table.GetItemRaw(LuaNumber.Create(i));
        }
      }

      class SortComparer : IComparer<ILuaValue> {
        public SortComparer(ILuaEnvironment E, ILuaValue method) {
          if (method != null && method.ValueType != LuaValueType.Function) {
            throw new ArgumentException(
                $"Invalid '{method.ValueType}' value for function 'table.sort'.");
          }
          _method = method;
          _env = E;
        }

        public int Compare(ILuaValue x, ILuaValue y) {
          if (_method != null) {
            LuaMultiValue ret = _method.Invoke(
                LuaNil.Nil, false, LuaMultiValue.CreateMultiValueFromObj(x, y));
            return ret.IsTrue ? -1 : 1;
          }

          return x.CompareTo(y);
        }

        readonly ILuaEnvironment _env;
        readonly ILuaValue _method;
      }
    }
  }
}
