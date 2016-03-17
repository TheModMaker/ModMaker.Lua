using ModMaker.Lua.Parser.Items;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModMaker.Lua.Runtime.LuaValues
{
    public sealed class LuaUserData<T> : LuaValueBase<T>
    {
        // TODO: Implement LuaUserData.

        /// <summary>
        /// Creates a new LuaUserData that wraps the given value.
        /// </summary>
        /// <param name="data">The data it wraps.</param>
        public LuaUserData(T data)
            : base(data) { }

        /// <summary>
        /// Gets the value type of the value.
        /// </summary>
        public override LuaValueType ValueType { get { return LuaValueType.UserData; } }
        /// <summary>
        /// Gets whether the value is Lua true value.
        /// </summary>
        public override bool IsTrue { get { return Value != null && Value as bool? != false; } }

        /// <summary>
        /// Indexes the value and returns the value.
        /// </summary>
        /// <param name="index">The index to use.</param>
        /// <returns>The value at the given index.</returns>
        public override ILuaValue GetIndex(ILuaValue index)
        {
            if (Value == null)
                throw new InvalidOperationException(Errors.CannotIndex(LuaValueType.Nil));

            return Helpers.GetSetMember(Value.GetType(), Value, index);
        }
        /// <summary>
        /// Indexes the value and assigns it a value.
        /// </summary>
        /// <param name="index">The index to use.</param>
        /// <param name="value">The value to assign to.</param>
        public override void SetIndex(ILuaValue index, ILuaValue value)
        {
            if (Value == null)
                throw new InvalidOperationException(Errors.CannotIndex(LuaValueType.Nil));

            Helpers.GetSetMember(Value.GetType(), Value, index, value);
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
        public override ILuaValue Arithmetic<T2>(BinaryOperationType type, LuaUserData<T2> self)
        {
            Contract.Requires<ArgumentNullException>(self != null, "self");
            Contract.Ensures(Contract.Result<ILuaValue>() != null);

            return self.ArithmeticFrom(type, this);
        }

        // this + self
        public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaBoolean self)
        {
            throw new NotImplementedException();
        }
        public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaClass self)
        {
            throw new NotImplementedException();
        }
        public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaFunction self)
        {
            throw new NotImplementedException();
        }
        public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaNil self)
        {
            throw new NotImplementedException();
        }
        public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaNumber self)
        {
            throw new NotImplementedException();
        }
        public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaString self)
        {
            throw new NotImplementedException();
        }
        public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaTable self)
        {
            throw new NotImplementedException();
        }
        public ILuaValue ArithmeticFrom(BinaryOperationType type, LuaThread self)
        {
            throw new NotImplementedException();
        }
        public ILuaValue ArithmeticFrom<T2>(BinaryOperationType type, LuaUserData<T2> self)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the .NET name of a given operation.
        /// </summary>
        /// <param name="type">The type of operation.</param>
        /// <returns>The name of the operation (e.g. op_Addition).</returns>
        static string GetOperationName(BinaryOperationType type)
        {
            switch (type)
            {
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