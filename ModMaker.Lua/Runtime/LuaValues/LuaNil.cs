using ModMaker.Lua.Parser.Items;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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
            Contract.Requires(other != null);
            Contract.Ensures(Contract.Result<ILuaValue>() != null);

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
            Contract.Requires<ArgumentNullException>(self != null, "self");
            Contract.Ensures(Contract.Result<ILuaValue>() != null);

            return self.ArithmeticFrom(type, this);
        }
    }
}