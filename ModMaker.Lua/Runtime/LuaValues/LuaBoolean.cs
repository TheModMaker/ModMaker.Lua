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

using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines a LuaValue that is a boolean.
  /// </summary>
  public sealed class LuaBoolean : LuaValueBase<bool> {
    /// <summary>
    /// Contains the true value.
    /// </summary>
    public static readonly LuaBoolean True = new LuaBoolean(true);
    /// <summary>
    /// Contains the false value.
    /// </summary>
    public static readonly LuaBoolean False = new LuaBoolean(false);

    /// <summary>
    /// Creates a new LuaBoolean that wraps the given value.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    LuaBoolean(bool value) : base(value) { }

    /// <summary>
    /// Creates a LuaBoolean that wraps the given value.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A LuaBoolean that wraps the given value.</returns>
    public static LuaBoolean Create(bool value) {
      return value ? True : False;
    }

    public override LuaValueType ValueType { get { return LuaValueType.Bool; } }
    public override bool IsTrue { get { return Value; } }

    public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
      return _arithmeticBase(type, other) ?? ((ILuaValueVisitor)other).Arithmetic(type, this);
    }

    public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self) {
      return self.ArithmeticFrom(type, this);
    }
  }
}
