// Copyright 2016 Jacob Trimble
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

using System.Collections;
using System.Collections.Generic;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines a table in Lua.  This acts as a dictionary of key-value pairs.
  /// </summary>
  /// <remarks>
  /// As this implements two IEnumerable&lt;T&gt;, IEnumerable returns the KeyValuePair values.
  /// </remarks>
  public sealed class LuaTable : LuaValueBase, ILuaTable {
    static readonly LuaString _index = new LuaString("__index");
    static readonly LuaString _newindex = new LuaString("__newindex");
    static readonly LuaString _len = new LuaString("__len");
    readonly Dictionary<ILuaValue, ILuaValue> _values = new Dictionary<ILuaValue, ILuaValue>();

    public override LuaValueType ValueType { get { return LuaValueType.Table; } }

    public ILuaTable? MetaTable { get; set; }
    public ILuaValue this[ILuaValue index] {
      get { return GetIndex(index); }
      set { SetIndex(index, value); }
    }

    public override ILuaValue Length() {
      if (MetaTable != null) {
        ILuaValue meth = MetaTable.GetItemRaw(_len);
        if (meth != LuaNil.Nil) {
          var ret = meth.Invoke(new LuaMultiValue(this));
          return ret[0];
        }
      }

      return RawLength();
    }
    public override ILuaValue RawLength() {
      double i = 1;
      while (GetItemRaw(LuaNumber.Create(i)).ValueType != LuaValueType.Nil) {
        i++;
      }

      return LuaNumber.Create(i - 1);
    }

    public override ILuaValue GetIndex(ILuaValue index) {
      ILuaValue? ret;
      if (!_values.TryGetValue(index, out ret) && MetaTable != null) {
        var method = MetaTable.GetItemRaw(_index);
        if (method != LuaNil.Nil) {
          if (method.ValueType == LuaValueType.Function) {
            return method.Invoke(new LuaMultiValue(this, index)).Single();
          } else {
            return method.GetIndex(index);
          }
        }
      }

      return ret ?? LuaNil.Nil;
    }
    public override void SetIndex(ILuaValue index, ILuaValue value) {
      if (!_values.TryGetValue(index, out _) && MetaTable != null) {
        var method = MetaTable.GetItemRaw(_newindex);
        if (method != LuaNil.Nil) {
          if (method.ValueType == LuaValueType.Function) {
            method.Invoke(new LuaMultiValue(this, index, value));
          } else {
            method.SetIndex(index, value);
          }

          return;
        }
      }

      SetItemRaw(index, value);
    }

    public ILuaValue GetItemRaw(ILuaValue index) {
      return _values.TryGetValue(index, out ILuaValue? ret) ? ret : LuaNil.Nil;
    }
    public void SetItemRaw(ILuaValue index, ILuaValue value) {
      if (value == LuaNil.Nil) {
        if (_values.ContainsKey(index)) {
          _values.Remove(index);
        }
      } else {
        _values[index] = value;
      }
    }

    public IEnumerator<KeyValuePair<ILuaValue, ILuaValue>> GetEnumerator() {
      return _values.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() {
      return _values.GetEnumerator();
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
      return _arithmeticBase(type, other) ?? ((ILuaValueVisitor)other).Arithmetic(type, this);
    }
    public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self) {
      return self.ArithmeticFrom(type, this);
    }

    public override bool Equals(ILuaValue? other) {
      return ReferenceEquals(this, other);
    }
    public override bool Equals(object? obj) {
      return ReferenceEquals(this, obj);
    }
    public override int GetHashCode() {
      return base.GetHashCode();
    }
  }
}
