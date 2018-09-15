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
using System;
using System.Collections.Generic;

namespace ModMaker.Lua.Runtime.LuaValues
{
    /// <summary>
    /// Defines a LuaValue that is a string.
    /// </summary>
    public sealed class LuaString : LuaValueBase<string>
    {
        /// <summary>
        /// Contains a dictionary from operation types to strings.
        /// </summary>
        internal static Dictionary<BinaryOperationType, LuaString> _metamethods = new Dictionary<BinaryOperationType, LuaString>()
        {
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
        public LuaString(string value)
            : base(value) {}

        /// <summary>
        /// Gets the value type of the value.
        /// </summary>
        public override LuaValueType ValueType { get { return LuaValueType.String; } }

        /// <summary>
        /// Gets the length of the value.
        /// </summary>
        /// <returns>The length of the value.</returns>
        public override ILuaValue Length()
        {
            return new LuaNumber(Value.Length);
        }

        /// <summary>
        /// Converts the current string to a number, or returns null.
        /// </summary>
        /// <returns>The value as a number, or null.</returns>
        public LuaNumber ToNumber()
        {
            double d;
            if (double.TryParse(Value, out d))
                return new LuaNumber(d);
            else
                return null;
        }

        /// <summary>
        /// Performs a binary arithmetic operation and returns the result.
        /// </summary>
        /// <param name="type">The type of operation to perform.</param>
        /// <param name="other">The other value to use.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// If the operation cannot be performed with the given values.
        /// </exception>
        /// <exception cref="System.InvalidArgumentException">
        /// If the argument is an invalid value.
        /// </exception>
        public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other)
        {
            return base.ArithmeticBase(type, other) ?? ((ILuaValueVisitor)other).Arithmetic(type, this);
        }

        /// <summary>
        /// Performs a binary arithmetic operation and returns the result.
        /// </summary>
        /// <param name="type">The type of operation to perform.</param>
        /// <param name="self">The first value to use.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// If the operation cannot be performed with the given values.
        /// </exception>
        /// <exception cref="System.InvalidArgumentException">
        /// If the argument is an invalid value.
        /// </exception>
        public override ILuaValue Arithmetic(BinaryOperationType type, LuaNumber self)
        {
            var t = this.ToNumber();
            if (t != null)
                return t.Arithmetic(type, self);
            else
                throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.String));
        }
        /// <summary>
        /// Performs a binary arithmetic operation and returns the result.
        /// </summary>
        /// <param name="type">The type of operation to perform.</param>
        /// <param name="self">The first value to use.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// If the operation cannot be performed with the given values.
        /// </exception>
        /// <exception cref="System.InvalidArgumentException">
        /// If the argument is an invalid value.
        /// </exception>
        public override ILuaValue Arithmetic(BinaryOperationType type, LuaString self)
        {
            var t = self.ToNumber();
            if (t != null)
                return Arithmetic(type, t);
            else
                throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.String));
        }
        /// <summary>
        /// Performs a binary arithmetic operation and returns the result.
        /// </summary>
        /// <param name="type">The type of operation to perform.</param>
        /// <param name="self">The first value to use.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// If the operation cannot be performed with the given values.
        /// </exception>
        /// <exception cref="System.InvalidArgumentException">
        /// If the argument is an invalid value.
        /// </exception>
        public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self)
        {
            return self.ArithmeticFrom(type, this);
        }
    }
}