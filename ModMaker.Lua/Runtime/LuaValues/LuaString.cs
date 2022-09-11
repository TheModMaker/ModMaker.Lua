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
using System.Collections.Generic;
using ModMaker.Lua.Parser.Items;

#nullable enable

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines a LuaValue that is a string.
  /// </summary>
  public sealed class LuaString : LuaValueBase<string> {
    /// <summary>
    /// Contains a dictionary from operation types to strings.
    /// </summary>
    internal static readonly Dictionary<BinaryOperationType, LuaString> _metamethods =
        new Dictionary<BinaryOperationType, LuaString>() {
            { BinaryOperationType.Add, new LuaString("__add") },
            { BinaryOperationType.Subtract, new LuaString("__sub") },
            { BinaryOperationType.Multiply, new LuaString("__mul") },
            { BinaryOperationType.Divide, new LuaString("__div") },
            { BinaryOperationType.Power, new LuaString("__pow") },
            { BinaryOperationType.Modulo, new LuaString("__mod") },
            { BinaryOperationType.Concat, new LuaString("__concat") },
            { BinaryOperationType.Gt, new LuaString("__le") },
            { BinaryOperationType.Lt, new LuaString("__lt") },
            { BinaryOperationType.Gte, new LuaString("__lt") },
            { BinaryOperationType.Lte, new LuaString("__le") },
            { BinaryOperationType.Equals, new LuaString("__eq") },
            { BinaryOperationType.NotEquals, new LuaString("__eq") },
        };

    /// <summary>
    /// Creates a new LuaString that wraps the given value.
    /// </summary>
    /// <param name="value">The value it contains.</param>
    public LuaString(string value) : base(value) { }

    public override LuaValueType ValueType { get { return LuaValueType.String; } }

    public override ILuaValue Length() {
      return LuaNumber.Create(Value.Length);
    }

    /// <summary>
    /// Converts the current string to a number, or returns null.
    /// </summary>
    /// <returns>The value as a number, or null.</returns>
    public LuaNumber? ToNumber() {
      if (double.TryParse(Value, out double d)) {
        return LuaNumber.Create(d);
      } else {
        return null;
      }
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
      return _arithmeticBase(type, other) ?? ((ILuaValueVisitor)other).Arithmetic(type, this);
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, LuaNumber self) {
      var t = ToNumber();
      if (t != null) {
        return t.Arithmetic(type, self);
      } else {
        throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.String));
      }
    }
    public override ILuaValue Arithmetic(BinaryOperationType type, LuaString self) {
      var t = self.ToNumber();
      if (t != null) {
        return Arithmetic(type, t);
      } else {
        throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.String));
      }
    }
    public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self) {
      return self.ArithmeticFrom(type, this);
    }
  }
}
