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
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines a LuaValue that is a number (double).
  /// </summary>
  public sealed class LuaNumber : LuaValueBase<double> {
    static readonly LuaNumber[] _numbers;

    static LuaNumber() {
      _numbers = new LuaNumber[50];
      for (int i = 0; i < _numbers.Length; i++) {
        _numbers[i] = new LuaNumber(i - 1);
      }
    }

    /// <summary>
    /// Creates a new LuaNumber that wraps the given number.
    /// </summary>
    /// <param name="num">The number that it wraps.</param>
    LuaNumber(double num) : base(num) { }

    public static LuaNumber Create(double num) {
      if (num % 1 == 0 && num >= -1 && num < _numbers.Length - 1)
        return _numbers[(int)num + 1];
      else
        return new LuaNumber(num);
    }

    public override LuaValueType ValueType { get { return LuaValueType.Number; } }

    public override double? AsDouble() {
      return Value;
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
      return _arithmeticBase(type, other) ?? ((ILuaValueVisitor)other).Arithmetic(type, this);
    }

    public override ILuaValue Minus() {
      return Create(-Value);
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, LuaNumber self) {
      // Cannot use DefaultArithmetic since self and this are swapped.
      switch (type) {
        case BinaryOperationType.Add:
          return Create(self.Value + Value);
        case BinaryOperationType.Subtract:
          return Create(self.Value - Value);
        case BinaryOperationType.Multiply:
          return Create(self.Value * Value);
        case BinaryOperationType.Divide:
          return Create(self.Value / Value);
        case BinaryOperationType.Power:
          return Create(Math.Pow(self.Value, Value));
        case BinaryOperationType.Modulo:
          return Create(self.Value - Math.Floor(self.Value / Value) * Value);
        case BinaryOperationType.Concat:
          return new LuaString(self.ToString() + ToString());
        case BinaryOperationType.Gt:
          return LuaBoolean.Create(self.CompareTo(this) > 0);
        case BinaryOperationType.Lt:
          return LuaBoolean.Create(self.CompareTo(this) < 0);
        case BinaryOperationType.Gte:
          return LuaBoolean.Create(self.CompareTo(this) >= 0);
        case BinaryOperationType.Lte:
          return LuaBoolean.Create(self.CompareTo(this) <= 0);
        case BinaryOperationType.Equals:
          return LuaBoolean.Create(self.Equals(this));
        case BinaryOperationType.NotEquals:
          return LuaBoolean.Create(!self.Equals(this));
        case BinaryOperationType.And:
          return !self.IsTrue ? self : this;
        case BinaryOperationType.Or:
          return self.IsTrue ? self : this;
        default:
          throw new ArgumentException(Resources.BadBinOp);
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
