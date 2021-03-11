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

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Defines a value that is stored in Lua.  This is a wrapper around a value.
  /// </summary>
  /// <remarks>
  /// Even though this implements IComparable, LuaValues may not be ordered. The compare method may
  /// throw an InvalidOperationException if a comparison is with an invalid object.
  ///
  /// Similarly, the Enumerator method may throw if on an invalid type.
  /// </remarks>
  public interface ILuaValue : IEquatable<ILuaValue>, IComparable<ILuaValue> {
    /// <summary>
    /// Gets whether the value is Lua true value.
    /// </summary>
    bool IsTrue { get; }
    /// <summary>
    /// Gets the value type of the value.
    /// </summary>
    LuaValueType ValueType { get; }

    /// <summary>
    /// Gets the value for this object.  For values that don't wrap something, it simply returns
    /// this.
    /// </summary>
    /// <returns>The value for this object.</returns>
    object GetValue();
    /// <summary>
    /// Converts the given value to a number, or returns null.
    /// </summary>
    /// <returns>The current value as a double, or null.</returns>
    double? AsDouble();

    /// <summary>
    /// Gets the value of the object cast to the given type. Throws an exception if the cast is
    /// invalid.
    /// </summary>
    /// <typeparam name="T">The type to cast to.</typeparam>
    /// <returns>The value of the object as the given type.</returns>
    /// <exception cref="System.InvalidCastException">If the type cannot
    /// be converted to the type.</exception>
    T As<T>();

    /// <summary>
    /// Indexes the value and returns the value.
    /// </summary>
    /// <param name="index">The index to use.</param>
    /// <returns>The value at the given index.</returns>
    ILuaValue GetIndex(ILuaValue index);
    /// <summary>
    /// Indexes the value and assigns it a value.
    /// </summary>
    /// <param name="index">The index to use.</param>
    /// <param name="value">The value to assign to.</param>
    void SetIndex(ILuaValue index, ILuaValue value);
    /// <summary>
    /// Invokes the object with the given arguments.
    /// </summary>
    /// <param name="self">The object being called on.</param>
    /// <param name="memberCall">Whether the call was using member call notation (:).</param>
    /// <param name="args">The arguments for the call.</param>
    /// <returns>The return values from the invocation.</returns>
    ILuaMultiValue Invoke(ILuaValue self, bool memberCall, ILuaMultiValue args);

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
    /// <remarks>
    /// This can be used for comparisons, but it should have the same behavior as IComparable and
    /// IEquatable.
    /// </remarks>
    ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other);

    /// <summary>
    /// Gets the unary minus of the value.
    /// </summary>
    /// <returns>The unary minus of the value.</returns>
    ILuaValue Minus();
    /// <summary>
    /// Gets the boolean negation of the value.
    /// </summary>
    /// <returns>The boolean negation of the value.</returns>
    ILuaValue Not();
    /// <summary>
    /// Gets the length of the value.
    /// </summary>
    /// <returns>The length of the value.</returns>
    ILuaValue Length();
    /// <summary>
    /// Gets the raw-length of the value.
    /// </summary>
    /// <returns>The length of the value.</returns>
    ILuaValue RawLength();
    /// <summary>
    /// Removes and multiple arguments and returns as a single item.
    /// </summary>
    /// <returns>Either this, or the first in a multi-value.</returns>
    ILuaValue Single();
  }

  /// <summary>
  /// Defines a type-safe version of ILuaValue.  This is used by the standard version.
  /// </summary>
  /// <typeparam name="T">The type of the backing value.</typeparam>
  public interface ILuaValue<T> : ILuaValue {
    /// <summary>
    /// Gets the value defined in this object.
    /// </summary>
    T Value { get; }
  }
}
