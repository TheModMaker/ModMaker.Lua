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
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace ModMaker.Lua.Runtime.LuaValues
{
    /// <summary>
    /// Defines a LuaValue that is a number (double).
    /// </summary>
    public sealed class LuaNumber : LuaValueBase<double>
    {
        /// <summary>
        /// Creates a new LuaNumber that wraps the given number.
        /// </summary>
        /// <param name="num">The number that it wraps.</param>
        public LuaNumber(double num)
            : base(num) { }

        /// <summary>
        /// Gets the value type of the value.
        /// </summary>
        public override LuaValueType ValueType { get { return LuaValueType.Number; } }

        /// <summary>
        /// Converts the given value to a number, or returns null.
        /// </summary>
        /// <returns>The current value as a double, or null.</returns>
        public override double? AsDouble()
        {
            return Value;
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
        /// Gets the unary minus of the value.
        /// </summary>
        /// <returns>The unary minus of the value.</returns>
        public override ILuaValue Minus()
        {
            return new LuaNumber(-Value);
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
            // Cannot use DefaultArithmetic since self and this are swapped.
            switch (type)
            {
                case BinaryOperationType.Add:
                    return new LuaNumber(self.Value + this.Value);
                case BinaryOperationType.Subtract:
                    return new LuaNumber(self.Value - this.Value);
                case BinaryOperationType.Multiply:
                    return new LuaNumber(self.Value * this.Value);
                case BinaryOperationType.Divide:
                    return new LuaNumber(self.Value / this.Value);
                case BinaryOperationType.Power:
                    return new LuaNumber(Math.Pow(self.Value, this.Value));
                case BinaryOperationType.Modulo:
                    return new LuaNumber(self.Value - Math.Floor(self.Value / this.Value) * this.Value);
                case BinaryOperationType.Concat:
                    return new LuaString(self.ToString() + this.ToString());
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