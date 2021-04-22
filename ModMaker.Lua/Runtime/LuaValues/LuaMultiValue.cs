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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines multiple LuaValues.  This is used to pass arguments and get results from functions.
  /// </summary>
  public sealed class LuaMultiValue : LuaValueBase, ILuaMultiValue {
    static readonly ILuaValue[] _nil = new[] { LuaNil.Nil };
    /// <summary>
    /// Contains an empty multi-value object.
    /// </summary>
    public static ILuaMultiValue Empty = new LuaMultiValue(new ILuaValue[0]);

    /// <summary>
    /// Creates a new multi-value from the given objects.  Each object is first converted
    /// using LuaValueBase::CreateValue.
    /// </summary>
    /// <param name="values">The values to store.</param>
    /// <returns>A new multi-value object.</returns>
    public static ILuaMultiValue CreateMultiValueFromObj(params object[] args) {
      var temp = args.Select(o => CreateValue(o)).ToArray();
      return new LuaMultiValue(temp);
    }

    readonly ILuaValue[] _values;

    /// <summary>
    /// Creates a new LuaMultiValue containing the given objects.
    /// </summary>
    /// <param name="args">The arguments of the function.</param>
    public LuaMultiValue(params ILuaValue[] args) {
      var end = args.Length > 0 && args[^1] is ILuaMultiValue ? (IEnumerable<ILuaValue>)args[^1]
                                                              : args.Skip(args.Length - 1);
      _values = args.Take(args.Length - 1).Select(v => v.Single()).Concat(end).ToArray();

      Count = _values.Length;
      if (_values.Length == 0) {
        _values = _nil;
      }
    }
    /// <summary>
    /// Creates a new LuaMultiValue containing the given objects.
    /// </summary>
    /// <param name="args">The arguments of the function.</param>
    LuaMultiValue(ILuaValue[] args, int _) {
      _values = args;
      Count = _values.Length;
      if (_values.Length == 0) {
        _values = _nil;
      }
    }

    public override bool IsTrue { get { return _values[0].IsTrue; } }
    public override LuaValueType ValueType { get { return LuaValueType.UserData; } }

    public ILuaValue this[int index] {
      get { return index < 0 || index >= _values.Length ? LuaNil.Nil : _values[index]; }
      set {
        if (index >= 0 && index < _values.Length) {
          _values[index] = value;
        }
      }
    }
    public int Count { get; }

    public override bool Equals(ILuaValue other) {
      return Equals((object)other);
    }
    public override bool Equals(object obj) {
      return _values[0].Equals(obj);
    }
    public override int GetHashCode() {
      return _values[0].GetHashCode();
    }
    public override int CompareTo(ILuaValue other) {
      return _values[0].CompareTo(other);
    }

    public override object GetValue() {
      return _values[0].GetValue();
    }
    public override double? AsDouble() {
      return _values[0].AsDouble();
    }

    public IEnumerator<ILuaValue> GetEnumerator() {
      if (Count == 0) {
        return Enumerable.Empty<ILuaValue>().GetEnumerator();
      }
      return ((IEnumerable<ILuaValue>)_values).GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    public override ILuaValue Minus() {
      return _values[0].Minus();
    }
    public override ILuaValue Length() {
      return _values[0].Length();
    }
    public override ILuaValue RawLength() {
      return _values[0].RawLength();
    }
    public override ILuaValue Single() {
      return _values[0];
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
      return _values[0].Arithmetic(type, other);
    }
    public override ILuaMultiValue Invoke(ILuaValue self, bool memberCall, ILuaMultiValue args) {
      return _values[0].Invoke(self, memberCall, args);
    }
    public override ILuaValue GetIndex(ILuaValue index) {
      return _values[0].GetIndex(index);
    }
    public override void SetIndex(ILuaValue index, ILuaValue value) {
      _values[0].SetIndex(index, value);
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, LuaBoolean self) {
      return self.Arithmetic(type, _values[0]);
    }
    public override ILuaValue Arithmetic(BinaryOperationType type, LuaClass self) {
      return self.Arithmetic(type, _values[0]);
    }
    public override ILuaValue Arithmetic(BinaryOperationType type, LuaFunction self) {
      return ((ILuaValue)self).Arithmetic(type, _values[0]);
    }
    public override ILuaValue Arithmetic(BinaryOperationType type, LuaNil self) {
      return self.Arithmetic(type, _values[0]);
    }
    public override ILuaValue Arithmetic(BinaryOperationType type, LuaNumber self) {
      return self.Arithmetic(type, _values[0]);
    }
    public override ILuaValue Arithmetic(BinaryOperationType type, LuaString self) {
      return self.Arithmetic(type, _values[0]);
    }
    public override ILuaValue Arithmetic(BinaryOperationType type, LuaTable self) {
      return self.Arithmetic(type, _values[0]);
    }
    public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self) {
      return self.Arithmetic(type, _values[0]);
    }

    public ILuaMultiValue AdjustResults(int number) {
      if (number < 0) {
        number = 0;
      }

      ILuaValue[] temp = new ILuaValue[number];
      int max = Math.Min(number, _values.Length);
      for (int i = 0; i < max; i++) {
        temp[i] = _values[i];
      }

      return new LuaMultiValue(temp, 0);
    }
  }
}
