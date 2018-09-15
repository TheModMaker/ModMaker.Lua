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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime.LuaValues
{
    /// <summary>
    /// Defines multiple LuaValues.  This is used to pass arguments and 
    /// get results from functions.
    /// </summary>
    public sealed class LuaMultiValue : LuaValueBase, ILuaMultiValue
    {
        static ILuaValue[] _nil = new[] { LuaNil.Nil };
        /// <summary>
        /// Contains an empty multi-value object.
        /// </summary>
        public static ILuaMultiValue Empty = new LuaMultiValue(new ILuaValue[0]);

        /// <summary>
        /// Creates a new multi-value from the given objects.  Each object is first converted
        /// using LuaValueBase::CreateValue.
        /// </summary>
        /// <param name="values">The values to store.</param>
        /// <returns>A new multi-value object.</returns>
        public static ILuaMultiValue CreateMultiValueFromObj(params object[] args)
        {
            var temp = args.Select(o => CreateValue(o)).ToArray();
            return new LuaMultiValue(temp);
        }

        ILuaValue[] values_;

        /// <summary>
        /// Creates a new LuaMultiValue containing the given objects.
        /// </summary>
        /// <param name="args">The arguments of the function.</param>
        public LuaMultiValue(params ILuaValue[] args)
        {
            this.values_ = args.Take(args.Length - 1)
                .Select(v => v.Single())
                .Concat(args.Length > 0 && args[args.Length - 1] is ILuaMultiValue ? ((IEnumerable<ILuaValue>)args[args.Length - 1]) : args.Skip(args.Length - 1))
                .ToArray();

            this.Count = this.values_.Length;
            if (this.values_.Length == 0) 
                this.values_ = _nil;
        }
        /// <summary>
        /// Creates a new LuaMultiValue containing the given objects.
        /// </summary>
        /// <param name="args">The arguments of the function.</param>
        LuaMultiValue(ILuaValue[] args, int dummy)
        {
            this.values_ = args;
            this.Count = this.values_.Length;
            if (this.values_.Length == 0)
                this.values_ = _nil;
        }

        /// <summary>
        /// Gets whether the value is Lua true value.
        /// </summary>
        public override bool IsTrue { get { return values_[0].IsTrue; } }
        /// <summary>
        /// Gets the value type of the value.
        /// </summary>
        public override LuaValueType ValueType { get { return LuaValueType.UserData; } }

        /// <summary>
        /// Gets the value at the given index.  If the index is less
        /// than zero or larger than Count, it should return LuaNil.
        /// This should never return null.
        /// </summary>
        /// <param name="index">The index to get.</param>
        /// <returns>The value at the given index, or LuaNil.</returns>
        public ILuaValue this[int index] 
        {
            get { return index < 0 || index >= values_.Length ? LuaNil.Nil : values_[index]; }
            set
            {
                if (index >= 0 && index < values_.Length)
                    values_[index] = value;
            }
        }
        /// <summary>
        /// Gets the number of values in this object.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the other parameter; otherwise, false.</returns>
        public override bool Equals(ILuaValue other)
        {
            return Equals((object)other);
        }
        /// <summary>
        ///  Determines whether the specified System.Object is equal to the current System.Object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return values_[0].Equals(obj);
        }
        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>A hash code for the current System.Object.</returns>
        public override int GetHashCode()
        {
            return values_[0].GetHashCode();
        }
        /// <summary>
        /// Compares the current object with another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared.
        /// The return value has the following meanings: Value Meaning Less than zero
        /// This object is less than the other parameter.Zero This object is equal to
        /// other. Greater than zero This object is greater than other.
        /// </returns>
        public override int CompareTo(ILuaValue other)
        {
            return values_[0].CompareTo(other);
        }
        
        /// <summary>
        /// Gets the value for this object.  For values that don't
        /// wrap something, it simply returns this.
        /// </summary>
        /// <returns>The value for this object.</returns>
        public override object GetValue()
        {
            return values_.Length == 0 ? null : values_[0].GetValue();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>A System.Collections.Generic.IEnumerator&lt;T&gt; that can
        /// be used to iterate through the collection.</returns>
        public IEnumerator<ILuaValue> GetEnumerator()
        {
            if (Count == 0)
                return Enumerable.Empty<ILuaValue>().GetEnumerator();
            return ((IEnumerable<ILuaValue>)values_).GetEnumerator();
        }
        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An System.Collections.IEnumerator object that can be used 
        /// to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Gets the unary minus of the value.
        /// </summary>
        /// <returns>The unary minus of the value.</returns>
        public override ILuaValue Minus()
        {
            return values_[0].Minus();
        }
        /// <summary>
        /// Gets the length of the value.
        /// </summary>
        /// <returns>The length of the value.</returns>
        public override ILuaValue Length()
        {
            return values_[0].Length();
        }
        /// <summary>
        /// Removes and multiple arguments and returns as a single item.
        /// </summary>
        /// <returns>Either this, or the first in a multi-value.</returns>
        public override ILuaValue Single()
        {
            return values_[0];
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
            return values_[0].Arithmetic(type, other);
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
        public override ILuaValue Arithmetic(BinaryOperationType type, LuaBoolean self)
        {
            return self.Arithmetic(type, values_[0]);
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
        public override ILuaValue Arithmetic(BinaryOperationType type, LuaClass self)
        {
            return self.Arithmetic(type, values_[0]);
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
        public override ILuaValue Arithmetic(BinaryOperationType type, LuaFunction self)
        {
            return ((ILuaValue)self).Arithmetic(type, values_[0]);
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
        public override ILuaValue Arithmetic(BinaryOperationType type, LuaNil self)
        {
            return self.Arithmetic(type, values_[0]);
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
            return self.Arithmetic(type, values_[0]);
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
            return self.Arithmetic(type, values_[0]);
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
        public override ILuaValue Arithmetic(BinaryOperationType type, LuaValues.LuaTable self)
        {
            return self.Arithmetic(type, values_[0]);
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
            return self.Arithmetic(type, values_[0]);
        }

        /// <summary>
        /// Returns a new object with the same values as this object except
        /// where any extra values are removed and any missing values are
        /// set to null.
        /// </summary>
        /// <param name="number">The number of values to have.</param>
        /// <returns>A new ILuaMultiValue object with the values in this object.</returns>
        public ILuaMultiValue AdjustResults(int number)
        {
            if (number < 0) number = 0;

            ILuaValue[] temp = new ILuaValue[number];
            int max = Math.Min(number, values_.Length);
            for (int i = 0; i < max; i++)
                temp[i] = values_[i];

            return new LuaMultiValue(temp, 0);
        }
    }
}