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
  public sealed class LuaUserData<T> : LuaValueBase<T> {
    // TODO: Implement LuaUserData.

    /// <summary>
    /// Creates a new LuaUserData that wraps the given value.
    /// </summary>
    /// <param name="data">The data it wraps.</param>
    public LuaUserData(T data) : base(data) { }

    public override LuaValueType ValueType { get { return LuaValueType.UserData; } }
    public override bool IsTrue { get { return Value != null && Value as bool? != false; } }

    public override ILuaValue GetIndex(ILuaValue index) {
      if (Value == null) {
        throw new InvalidOperationException(Errors.CannotIndex(LuaValueType.Nil));
      }

      return Helpers.GetSetMember(Value.GetType(), Value, index);
    }
    public override void SetIndex(ILuaValue index, ILuaValue value) {
      if (Value == null) {
        throw new InvalidOperationException(Errors.CannotIndex(LuaValueType.Nil));
      }

      Helpers.GetSetMember(Value.GetType(), Value, index, value);
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
      return _arithmeticBase(type, other) ?? ((ILuaValueVisitor)other).Arithmetic(type, this);
    }
    public override ILuaValue Arithmetic<T2>(BinaryOperationType type, LuaUserData<T2> self) {
      return self.ArithmeticFrom(type, this);
    }

    // this + self
    public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaBoolean self) {
      throw new NotImplementedException();
    }
    public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaClass self) {
      throw new NotImplementedException();
    }
    public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaFunction self) {
      throw new NotImplementedException();
    }
    public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaNil self) {
      throw new NotImplementedException();
    }
    public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaNumber self) {
      throw new NotImplementedException();
    }
    public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaString self) {
      throw new NotImplementedException();
    }
    public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaTable self) {
      throw new NotImplementedException();
    }
    public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaThread self) {
      throw new NotImplementedException();
    }
    public ILuaValue ArithmeticFrom<T2>(BinaryOperationType type, LuaUserData<T2> self) {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the .NET name of a given operation.
    /// </summary>
    /// <param name="type">The type of operation.</param>
    /// <returns>The name of the operation (e.g. op_Addition).</returns>
    static string _getOperationName(BinaryOperationType type) {
      switch (type) {
        case BinaryOperationType.Add:
          return "op_Addition";
        case BinaryOperationType.Subtract:
          return "op_Subtraction";
        case BinaryOperationType.Multiply:
          return "op_Multiply";
        case BinaryOperationType.Divide:
          return "op_Division";
        case BinaryOperationType.Power:
          return "op_Power";
        case BinaryOperationType.Modulo:
          return "op_Modulo";
        case BinaryOperationType.Gt:
          return "op_GreaterThan";
        case BinaryOperationType.Lt:
          return "op_LessThan";
        case BinaryOperationType.Gte:
          return "op_GreaterThanOrEqual";
        case BinaryOperationType.Lte:
          return "op_LessThanOrEqual";
        case BinaryOperationType.Equals:
          return "op_Equality";
        case BinaryOperationType.NotEquals:
          return "op_Inequality";
        default:
          return "";
      }
    }
  }
}
