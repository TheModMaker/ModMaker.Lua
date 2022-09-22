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
    public LuaUserData(T data) : base(data) {
      if (data == null)
        throw new ArgumentNullException(nameof(data));
    }

    public override LuaValueType ValueType { get { return LuaValueType.UserData; } }
    public override bool IsTrue { get { return true; } }

    public override ILuaValue GetIndex(ILuaValue index) {
      return Helpers.GetSetMember(Value!.GetType(), Value, index);
    }
    public override void SetIndex(ILuaValue index, ILuaValue value) {
      Helpers.GetSetMember(Value!.GetType(), Value, index, value);
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
    public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaCoroutine self) {
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
      return type switch {
        BinaryOperationType.Add => "op_Addition",
        BinaryOperationType.Subtract => "op_Subtraction",
        BinaryOperationType.Multiply => "op_Multiply",
        BinaryOperationType.Divide => "op_Division",
        BinaryOperationType.Power => "op_Power",
        BinaryOperationType.Modulo => "op_Modulo",
        BinaryOperationType.Gt => "op_GreaterThan",
        BinaryOperationType.Lt => "op_LessThan",
        BinaryOperationType.Gte => "op_GreaterThanOrEqual",
        BinaryOperationType.Lte => "op_LessThanOrEqual",
        BinaryOperationType.Equals => "op_Equality",
        BinaryOperationType.NotEquals => "op_Inequality",
        _ => "",
      };
    }
  }
}
