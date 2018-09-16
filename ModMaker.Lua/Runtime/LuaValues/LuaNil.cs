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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModMaker.Lua.Runtime.LuaValues
{
    /// <summary>
    /// Defines a LuaValue that is a nil.
    /// </summary>
    public sealed class LuaNil : LuaValueBase<object>
    {
        /// <summary>
        /// Contains the LuaNil value.
        /// </summary>
        public static readonly LuaNil Nil = new LuaNil();

        /// <summary>
        /// Creates a new LuaNil object.
        /// </summary>
        private LuaNil()
            : base(null)
        { }

        /// <summary>
        /// Gets the value type of the value.
        /// </summary>
        public override LuaValueType ValueType { get { return LuaValueType.Nil; } }
        /// <summary>
        /// Gets whether the value is Lua true value.
        /// </summary>
        public override bool IsTrue { get { return false; } }

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
        public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self)
        {
            return self.ArithmeticFrom(type, this);
        }
    }
}
