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

#nullable enable

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines a base class for the standard LuaValue's.  This uses the visitor pattern to pick the
  /// correct function to call.  This is the base type for all LuaValue's, for user-defined types,
  /// see LuaUserData&lt;T&gt;.
  /// </summary>
  public abstract class LuaValueBase : ILuaValue, ILuaValueVisitor {
    // TODO: Handle accessibility.

    public virtual bool IsTrue { get { return true; } }
    public abstract LuaValueType ValueType { get; }

    /// <summary>
    /// Creates a new LuaValue object wrapping the given value.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A new LuaValue object.</returns>
    public static ILuaValue CreateValue(object? value) {
      if (value == null) {
        return LuaNil.Nil;
      }

      if (value is ILuaValue luaValue) {
        return luaValue;
      }
      if (value is Delegate delegate_) {
        return new LuaOverloadFunction(
            delegate_.Method.Name, new[] { delegate_.Method }, new[] { delegate_.Target });
      }

      bool? boolValue = value as bool?;
      if (boolValue == true) {
        return LuaBoolean.True;
      } else if (boolValue == false) {
        return LuaBoolean.False;
      }

      if (value is Type type) {
        return new LuaType(type);
      } else if (value is string stringValue) {
        return new LuaString(stringValue);
      } else if (value.GetType().IsPrimitive) {
        return LuaNumber.Create(Convert.ToDouble(value));
      } else {
        return (ILuaValue)Activator.CreateInstance(
            typeof(LuaUserData<>).MakeGenericType(value.GetType()), value)!;
      }
    }

    public abstract bool Equals(ILuaValue? other);
    public override bool Equals(object? obj) {
      return obj is ILuaValue value && Equals(value);
    }
    public virtual int CompareTo(ILuaValue? other) {
      if (Equals(other)) {
        return 0;
      } else {
        throw new InvalidOperationException(Errors.CannotArithmetic(ValueType));
      }
    }
    public override int GetHashCode() {
      return base.GetHashCode();
    }

    public virtual object? GetValue() {
      return this;
    }
    public virtual double? AsDouble() {
      return null;
    }

    public virtual T As<T>() {
      if (typeof(T) != typeof(object) && typeof(T).IsAssignableFrom(GetType())) {
        return (T)(object)this;
      }

      object? value = GetValue();
      if (value == null) {
        bool isNullable = typeof(T).IsGenericType &&
                          typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>);
        if (typeof(T).IsValueType && !isNullable) {
          throw new InvalidCastException(string.Format(Resources.BadCast, "null", typeof(T)));
        } else {
          return (T)value!;
        }
      }

      if (OverloadSelector.TypesCompatible(value.GetType(), typeof(T), out MethodInfo? m)) {
        // Cast the object if needed.
        if (m != null) {
          value = Helpers.DynamicInvoke(m, null, new[] { value });
        }

        return (T)value!;
      } else if (typeof(T) == typeof(object)) {
        return (T)(object)this;
      } else {
        throw new InvalidCastException(string.Format(Resources.BadCast, value.GetType(),
                                                     typeof(T)));
      }
    }

    public virtual ILuaValue GetIndex(ILuaValue index) {
      throw new InvalidOperationException(Errors.CannotIndex(this.ValueType));
    }
    public virtual void SetIndex(ILuaValue index, ILuaValue value) {
      throw new InvalidOperationException(Errors.CannotIndex(this.ValueType));
    }
    public virtual LuaMultiValue Invoke(ILuaValue self, bool memberCall, LuaMultiValue args) {
      throw new InvalidOperationException(Errors.CannotCall(ValueType));
    }

    public abstract ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other);
    /// <summary>
    /// Defines basic arithmetic for derived classes.  This returns a non-null value when default,
    /// or null if it is a visitor.  This throws if not a visitor.
    /// </summary>
    /// <param name="type">The type of operation to perform.</param>
    /// <param name="other">The other value to use.</param>
    /// <returns>The result of the operation.</returns>
    protected ILuaValue? _arithmeticBase(BinaryOperationType type, ILuaValue other) {
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
    private ILuaValue? _defaultArithmetic(BinaryOperationType type, ILuaValue other) {
      return type switch {
        BinaryOperationType.Concat => new LuaString(this.ToString() + other.ToString()),
        BinaryOperationType.Gt => LuaBoolean.Create(CompareTo(other) > 0),
        BinaryOperationType.Lt => LuaBoolean.Create(CompareTo(other) < 0),
        BinaryOperationType.Gte => LuaBoolean.Create(CompareTo(other) >= 0),
        BinaryOperationType.Lte => LuaBoolean.Create(CompareTo(other) <= 0),
        BinaryOperationType.Equals => LuaBoolean.Create(Equals(other)),
        BinaryOperationType.NotEquals => LuaBoolean.Create(!Equals(other)),
        BinaryOperationType.And => !IsTrue ? this : other,
        BinaryOperationType.Or => IsTrue ? this : other,
        _ => null,
      };
    }

    /// <summary>
    /// Attempts to invoke a meta-method and returns the result.
    /// </summary>
    /// <param name="type">The type of operation.</param>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the meta-method, or null if not found.</returns>
    internal static ILuaValue? _attemptMetamethod(BinaryOperationType type, ILuaValue self,
                                                  ILuaValue other) {
      if (type == BinaryOperationType.And || type == BinaryOperationType.Or) {
        return null;
      }

      // Search the first object.
      ILuaValue? ret = null;
      var self2 = self as ILuaTable;
      if (self2 != null && self2.MetaTable != null) {
        var t = self2.MetaTable.GetItemRaw(LuaString._metamethods[type]);
        if (t != LuaNil.Nil) {
          ret = t.Invoke(LuaNil.Nil, false, new LuaMultiValue(self, other));
        } else if (type == BinaryOperationType.Lte || type == BinaryOperationType.Gt) {
          t = self2.MetaTable.GetItemRaw(LuaString._metamethods[BinaryOperationType.Lt]);
          if (t != LuaNil.Nil) {
            ret = t.Invoke(LuaNil.Nil, false, new LuaMultiValue(other, self)).Not();
          }
        }
      }

      // Search the second object.
      var other2 = other as ILuaTable;
      if (ret == null && other2 != null && other2.MetaTable != null) {
        var t = other2.MetaTable.GetItemRaw(LuaString._metamethods[type]);
        if (t != LuaNil.Nil) {
          ret = t.Invoke(LuaNil.Nil, true, new LuaMultiValue(self, other));
        } else if (type == BinaryOperationType.Lte || type == BinaryOperationType.Gt) {
          t = other2.MetaTable.GetItemRaw(LuaString._metamethods[BinaryOperationType.Lt]);
          if (t != LuaNil.Nil) {
            ret = t.Invoke(LuaNil.Nil, true, new LuaMultiValue(other, self)).Not();
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

    public ILuaValue Not() {
      return LuaBoolean.Create(!IsTrue);
    }
    public virtual ILuaValue Minus() {
      throw new InvalidOperationException(Errors.CannotArithmetic(ValueType));
    }
    public virtual ILuaValue Length() {
      throw new InvalidOperationException(Errors.CannotArithmetic(ValueType));
    }
    public virtual ILuaValue RawLength() {
      return Length();
    }
    public virtual ILuaValue Single() {
      return this;
    }

    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaBoolean self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Bool));
    }
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaClass self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.UserData));
    }
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaFunction self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
    }
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaNil self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Nil));
    }
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaNumber self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(this.ValueType));
    }
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaString self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.String));
    }
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaTable self) {
      var ret = _attemptMetamethod(type, self, this);
      if (ret != null) {
        return ret;
      }

      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Table));
    }
    public virtual ILuaValue Arithmetic(BinaryOperationType type, LuaThread self) {
      var ret = _attemptMetamethod(type, self, this);
      if (ret != null) {
        return ret;
      }

      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Thread));
    }
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

    public T Value { get; }

    public override object? GetValue() {
      return Value;
    }

    public override bool Equals(ILuaValue? other) {
      return other != null && other.GetType() == GetType() &&
          Equals(Value, ((LuaValueBase<T>)other).Value);
    }
    public override bool Equals(object? obj) {
      if (obj is ILuaValue value) {
        return Equals(value);
      } else {
        return Equals(Value, obj);
      }
    }
    public override int CompareTo(ILuaValue? other) {
      if (other is LuaValueBase<T> temp && other.GetType() == GetType()) {
        var comp = Comparer<T>.Default;
        return comp.Compare(Value, temp.Value);
      }

      return base.CompareTo(other);
    }
    public override int GetHashCode() {
      return Value == null ? 0 : Value.GetHashCode();
    }
    public override string ToString() {
      if (Value == null) {
        return "nil";
      } else {
        return Value.ToString()!;
      }
    }
  }
}
