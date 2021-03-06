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
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines a base class for the standard LuaValue's.  This uses the visitor pattern to pick the
  /// correct function to call.  This is the base type for all LuaValue's, for user-defined types,
  /// see LuaUserData&lt;T&gt;.
  /// </summary>
  public abstract class LuaValueBase : ILuaValue, ILuaValueVisitor {
    // TODO: Handle accessibility.

    /// <summary>
    /// Gets whether the value is Lua true value.
    /// </summary>
    public virtual bool IsTrue { get { return true; } }
    /// <summary>
    /// Gets the value type of the value.
    /// </summary>
    public abstract LuaValueType ValueType { get; }

    /// <summary>
    /// Creates a new LuaValue object wrapping the given value.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A new LuaValue object.</returns>
    public static ILuaValue CreateValue(object value) {
      if (value == null) {
        return LuaNil.Nil;
      }

      Type type = value.GetType();
      if (value is ILuaValue luaValue) {
        return luaValue;
      }

      Delegate delegate_ = value as Delegate;
      if (delegate_ != null) {
        return new LuaOverloadFunction(
            delegate_.Method.Name, new[] { delegate_.Method }, new[] { delegate_.Target });
      }

      bool? boolValue = value as bool?;
      if (boolValue == true) {
        return LuaBoolean.True;
      } else if (boolValue == false) {
        return LuaBoolean.False;
      }

      string stringValue = value as string;
      if (stringValue != null) {
        return new LuaString(stringValue);
      } else if (type.IsPrimitive) {
        return new LuaNumber(Convert.ToDouble(value));
      } else {
        return (ILuaValue)typeof(LuaValues.LuaUserData<>)
            .MakeGenericType(value.GetType())
            .GetConstructor(new[] { value.GetType() })
            .Invoke(new[] { value });
      }
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    /// true if the current object is equal to the other parameter; otherwise, false.
    /// </returns>
    public abstract bool Equals(ILuaValue other);
    /// <summary>
    ///  Determines whether the specified System.Object is equal to the current System.Object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>
    /// true if the specified object is equal to the current object; otherwise, false.
    /// </returns>
    public override bool Equals(object obj) {
      return obj is ILuaValue value && Equals(value);
    }
    /// <summary>
    /// Compares the current object with another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    /// A value that indicates the relative order of the objects being compared. The return value
    /// has the following meanings: Value Meaning Less than zero This object is less than the other
    /// parameter.Zero This object is equal to other. Greater than zero This object is greater than
    /// other.
    /// </returns>
    public virtual int CompareTo(ILuaValue other) {
      if (Equals(other)) {
        return 0;
      } else {
        throw new InvalidOperationException(Errors.CannotArithmetic(ValueType));
      }
    }
    /// <summary>
    /// Serves as a hash function for a particular type.
    /// </summary>
    /// <returns>A hash code for the current System.Object.</returns>
    public override int GetHashCode() {
      return base.GetHashCode();
    }

    /// <summary>
    /// Gets the value for this object.  For values that don't  wrap something, it simply returns
    /// this.
    /// </summary>
    /// <returns>The value for this object.</returns>
    public virtual object GetValue() {
      return this;
    }
    /// <summary>
    /// Converts the given value to a number, or returns null.
    /// </summary>
    /// <returns>The current value as a double, or null.</returns>
    public virtual double? AsDouble() {
      return null;
    }

    /// <summary>
    /// Determines if the current object can be cast to the given
    /// value.
    /// </summary>
    /// <typeparam name="T">The type to cast to.</typeparam>
    /// <returns>Whether this object can be cast to the given type.</returns>
    public virtual bool TypesCompatible<T>() {
      // Sometimes this will return this; assume this is intended behavior.
      var value = GetValue();
      if (value == null) {
        return !typeof(T).IsValueType;
      }

      return Helpers.TypesCompatible(value.GetType(), typeof(T), out _, out _) !=
             LuaCastType.NoCast;
    }
    /// <summary>
    /// Gets information about the cast between the current type and the given type.  This value is
    /// used in overload resolution. If this is not implemented; the default values will be used.
    /// </summary>
    /// <typeparam name="T">The type to cast to.</typeparam>
    /// <param name="type">The type of cast used.</param>
    /// <param name="distance">The type distance for the given cast.</param>
    /// <exception cref="System.NotSupportedException">If custom
    /// type distance is not implemented.</exception>
    /// <remarks>
    /// The distance must be a non-negative number.  The same value means an equivalent cast.  A
    /// larger number means that it is further away.  When determining overload resolution, a
    /// smaller value is attempted.  They are only used for comparison; their value is never used
    /// directly.
    /// </remarks>
    public virtual void GetCastInfo<T>(out LuaCastType type, out int distance) {
      if (typeof(T).IsAssignableFrom(GetType())) {
        distance = 0;
        type = LuaCastType.BaseClass;
        return;
      }

      // Sometimes this will return |this|; assume this is intended behavior.
      var value = GetValue();
      if (value == null) {
        type = typeof(T).IsValueType ? LuaCastType.NoCast : LuaCastType.SameType;
        distance = 0;
        return;
      }

      type = Helpers.TypesCompatible(value.GetType(), typeof(T), out _, out distance);
    }
    /// <summary>
    /// Gets the value of the object cast to the given type. Throws an exception if the cast is
    /// invalid.
    /// </summary>
    /// <typeparam name="T">The type to cast to.</typeparam>
    /// <returns>The value of the object as the given type.</returns>
    /// <exception cref="System.InvalidCastException">If the type cannot
    /// be converted to the type.</exception>
    public virtual T As<T>() {
      if (typeof(T).IsAssignableFrom(GetType())) {
        return (T)(object)this;
      }

      object value = GetValue();
      if (value == null) {
        bool isNullable = typeof(T).IsGenericType &&
                          typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>);
        if (typeof(T).IsValueType && !isNullable) {
          throw new InvalidCastException(string.Format(Resources.BadCast, "null", typeof(T)));
        } else {
          return (T)value;
        }
      }

      if (Helpers.TypesCompatible(value.GetType(), typeof(T), out MethodInfo m, out _) !=
          LuaCastType.NoCast) {
        // Cast the object if needed.
        if (m != null) {
          value = m.Invoke(null, new[] { value });
        }

        return (T)value;
      } else {
        throw new InvalidCastException(string.Format(Resources.BadCast, value.GetType(),
                                                     typeof(T)));
      }
    }

    /// <summary>
    /// Indexes the value and returns the value.
    /// </summary>
    /// <param name="index">The index to use.</param>
    /// <returns>The value at the given index.</returns>
    public virtual ILuaValue GetIndex(ILuaValue index) {
      throw new InvalidOperationException(Errors.CannotIndex(this.ValueType));
    }
    /// <summary>
    /// Indexes the value and assigns it a value.
    /// </summary>
    /// <param name="index">The index to use.</param>
    /// <param name="value">The value to assign to.</param>
    public virtual void SetIndex(ILuaValue index, ILuaValue value) {
      throw new InvalidOperationException(Errors.CannotIndex(this.ValueType));
    }
    /// <summary>
    /// Invokes the object with the given arguments.
    /// </summary>
    /// <param name="self">The object being called on.</param>
    /// <param name="memberCall">Whether the call was using member call notation (:).</param>
    /// <param name="args">The arguments for the call.</param>
    /// <param name="overload">
    /// Specifies the overload to call; -1 to use overload-resolution.
    /// </param>
    /// <returns>The return values from the invocation.</returns>
    public virtual ILuaMultiValue Invoke(ILuaValue self, bool memberCall, int overload,
                                         ILuaMultiValue args) {
      throw new InvalidOperationException(Errors.CannotCall(ValueType));
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
    /// <remarks>
    /// This would be implemented as always throwing, but then it would not support user-data as the
    /// second argument.
    /// </remarks>
    public abstract ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other);
    /// <summary>
    /// Defines basic arithmetic for derived classes.  This returns a non-null value when default,
    /// or null if it is a visitor.  This throws if not a visitor.
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
    protected ILuaValue _arithmeticBase(BinaryOperationType type, ILuaValue other) {
      // Attempt to use a meta-method.
      var ret = _attemptMetamethod(type, this, other);
      if (ret != null) {
        return ret;
      }

      // Do some default operations.
      ret = _defaultArithmetic(type, other);
      if (ret != null) {
        return ret;
      }

      // If the other is not a visitor, throw.
      if (!(other is ILuaValueVisitor)) {
        throw new InvalidOperationException(Errors.CannotArithmetic(ValueType));
      } else {
        return null;
      }
    }
    /// <summary>
    /// Performs some default arithmetic like comparisons and returns the result. Returns null if
    /// there is no default.
    /// </summary>
    /// <param name="type">The type of operation to perform.</param>
    /// <param name="other">The other value to use.</param>
    /// <returns>The result of the operation.</returns>
    private ILuaValue _defaultArithmetic(BinaryOperationType type, ILuaValue other) {
      switch (type) {
        case BinaryOperationType.Concat:
          return new LuaString(this.ToString() + other.ToString());
        case BinaryOperationType.Gt:
          return LuaBoolean.Create(CompareTo(other) > 0);
        case BinaryOperationType.Lt:
          return LuaBoolean.Create(CompareTo(other) < 0);
        case BinaryOperationType.Gte:
          return LuaBoolean.Create(CompareTo(other) >= 0);
        case BinaryOperationType.Lte:
          return LuaBoolean.Create(CompareTo(other) <= 0);
        case BinaryOperationType.Equals:
          return LuaBoolean.Create(Equals(other));
        case BinaryOperationType.NotEquals:
          return LuaBoolean.Create(!Equals(other));
        case BinaryOperationType.And:
          return !IsTrue ? this : other;
        case BinaryOperationType.Or:
          return IsTrue ? this : other;
        default:
          return null;
      }
    }

    /// <summary>
    /// Attempts to invoke a meta-method and returns the result.
    /// </summary>
    /// <param name="type">The type of operation.</param>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the meta-method, or null if not found.</returns>
    internal static ILuaValue _attemptMetamethod(BinaryOperationType type, ILuaValue self,
                                                 ILuaValue other) {
      if (type == BinaryOperationType.And || type == BinaryOperationType.Or) {
        return null;
      }

      // Search the first object.
      ILuaValue ret = null;
      var self2 = self as ILuaTable;
      if (self2 != null && self2.MetaTable != null) {
        var t = self2.MetaTable.GetItemRaw(LuaString._metamethods[type]);
        if (t != null) {
          ret = t.Invoke(LuaNil.Nil, false, -1, new LuaMultiValue(self, other));
        } else if (type == BinaryOperationType.Lte || type == BinaryOperationType.Gt) {
          t = self2.MetaTable.GetItemRaw(LuaString._metamethods[BinaryOperationType.Lt]);
          if (t != null) {
            ret = t.Invoke(LuaNil.Nil, false, -1, new LuaMultiValue(other, self)).Not();
          }
        }
      }

      // Search the second object.
      var other2 = other as ILuaTable;
      if (ret == null && other2 != null && other2.MetaTable != null) {
        var t = other2.MetaTable.GetItemRaw(LuaString._metamethods[type]);
        if (t != null) {
          ret = t.Invoke(LuaNil.Nil, true, -1, new LuaMultiValue(self, other));
        } else if (type == BinaryOperationType.Lte || type == BinaryOperationType.Gt) {
          t = other2.MetaTable.GetItemRaw(LuaString._metamethods[BinaryOperationType.Lt]);
          if (t != null) {
            ret = t.Invoke(LuaNil.Nil, true, -1, new LuaMultiValue(other, self)).Not();
          }
        }
      }

      // Fix the arguments for comparisons.
      if (ret != null) {
        ret = ret.Single();
        switch (type) {
          case BinaryOperationType.Gt:
          case BinaryOperationType.Gte:
          case BinaryOperationType.NotEquals:
            ret = LuaBoolean.Create(!ret.IsTrue);
            break;
          case BinaryOperationType.Equals:
          case BinaryOperationType.Lt:
          case BinaryOperationType.Lte:
            ret = LuaBoolean.Create(ret.IsTrue);
            break;
        }
      }

      return ret;
    }

    /// <summary>
    /// Gets the boolean negation of the value.
    /// </summary>
    /// <returns>The boolean negation of the value.</returns>
    public ILuaValue Not() {
      return LuaBoolean.Create(!IsTrue);
    }
    /// <summary>
    /// Gets the unary minus of the value.
    /// </summary>
    /// <returns>The unary minus of the value.</returns>
    public virtual ILuaValue Minus() {
      throw new InvalidOperationException(Errors.CannotArithmetic(ValueType));
    }
    /// <summary>
    /// Gets the length of the value.
    /// </summary>
    /// <returns>The length of the value.</returns>
    public virtual ILuaValue Length() {
      throw new InvalidOperationException(Errors.CannotArithmetic(ValueType));
    }
    /// <summary>
    /// Gets the raw-length of the value.
    /// </summary>
    /// <returns>The length of the value.</returns>
    public virtual ILuaValue RawLength() {
      return Length();
    }
    /// <summary>
    /// Removes and multiple arguments and returns as a single item.
    /// </summary>
    /// <returns>Either this, or the first in a multi-value.</returns>
    public virtual ILuaValue Single() {
      return this;
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
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaBoolean self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Bool));
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
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaClass self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.UserData));
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
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaFunction self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
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
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaNil self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Nil));
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
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaNumber self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(this.ValueType));
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
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaString self) {
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
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaTable self) {
      var ret = _attemptMetamethod(type, self, this);
      if (ret != null) {
        return ret;
      }

      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Table));
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
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaThread self) {
      var ret = _attemptMetamethod(type, self, this);
      if (ret != null) {
        return ret;
      }

      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Thread));
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
    public abstract ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self);
  }

  /// <summary>
  /// A type-safe wrapper for a LuaValue.
  /// </summary>
  /// <typeparam name="T">The type of data stored.</typeparam>
  [DebuggerDisplay("LuaValue {Value}")]
  public abstract class LuaValueBase<T> : LuaValueBase, ILuaValue<T> {
    /// <summary>
    /// Creates a new LuaValueBase that wraps the given value.
    /// </summary>
    /// <param name="value">The value this will wrap.</param>
    public LuaValueBase(T value) {
      Value = value;
    }

    /// <summary>
    /// Gets the value defined in this object.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Gets the value for this object.  For values that don't wrap something, it simply returns
    /// this.
    /// </summary>
    /// <returns>The value for this object.</returns>
    public override object GetValue() {
      return Value;
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    /// true if the current object is equal to the other parameter; otherwise, false.
    /// </returns>
    public override bool Equals(ILuaValue other) {
      return other != null && other.GetType() == GetType() &&
          Equals(Value, ((LuaValueBase<T>)other).Value);
    }
    /// <summary>
    ///  Determines whether the specified System.Object is equal to the current System.Object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>
    /// true if the specified object is equal to the current object; otherwise, false.
    /// </returns>
    public override bool Equals(object obj) {
      if (obj is ILuaValue value) {
        return Equals(value);
      } else {
        return Equals(Value, obj);
      }
    }
    /// <summary>
    /// Compares the current object with another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>
    /// A value that indicates the relative order of the objects being compared. The return value
    /// has the following meanings: Value Meaning Less than zero This object is less than the other
    /// parameter.Zero This object is equal to other. Greater than zero This object is greater than
    /// other.
    /// </returns>
    public override int CompareTo(ILuaValue other) {
      if (other is LuaValueBase<T> temp && other.GetType() == GetType()) {
        var comp = Comparer<T>.Default;
        return comp.Compare(Value, temp.Value);
      }

      return base.CompareTo(other);
    }
    /// <summary>
    /// Serves as a hash function for a particular type.
    /// </summary>
    /// <returns>A hash code for the current System.Object.</returns>
    public override int GetHashCode() {
      return Value == null ? 0 : Value.GetHashCode();
    }
    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() {
      if (Value == null) {
        return "nil";
      } else {
        return Value.ToString();
      }
    }
  }
}
