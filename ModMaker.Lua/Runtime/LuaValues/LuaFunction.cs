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
using System.Dynamic;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines a method in Lua.  This can be a function that is defined in Lua, a framework method
  /// such as 'math.modf', or a user-defined method with dynamic overload resolution.
  /// </summary>
  /// <remarks>
  /// This type does little work in invoking a method, this is simply general code and a default
  /// implementation of LuaValues.Function.  See LuaOverloadFunction and LuaDefinedFunction for more
  /// info.
  /// </remarks>
  public abstract class LuaFunction : DynamicObject, ILuaValue, ILuaValueVisitor {
    static DelegateBuilder _builder = new DelegateBuilder();

    /// <summary>
    /// Creates a new LuaFunction.
    /// </summary>
    /// <param name="name">The name of the method</param>
    protected LuaFunction(string name) {
      Name = name;
    }

    /// <summary>
    /// Gets the name of the function.
    /// </summary>
    public string Name { get; }

    public abstract ILuaMultiValue Invoke(ILuaValue self, bool memberCall, ILuaMultiValue args);

    public bool Equals(ILuaValue other) {
      return ReferenceEquals(this, other);
    }
    public override bool Equals(object obj) {
      return ReferenceEquals(this, obj);
    }
    public override int GetHashCode() {
      return base.GetHashCode();
    }
    public int CompareTo(ILuaValue other) {
      if (Equals(other)) {
        return 0;
      } else {
        throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
      }
    }

    #region LuaValue and LuaValueVisitor Implementation

    LuaValueType ILuaValue.ValueType { get { return LuaValueType.Function; } }
    bool ILuaValue.IsTrue { get { return true; } }

    object ILuaValue.GetValue() {
      return this;
    }
    double? ILuaValue.AsDouble() {
      return null;
    }

    T ILuaValue.As<T>() {
      if (typeof(T).IsAssignableFrom(GetType())) {
        return (T)(object)this;
      }
      if (typeof(Delegate).IsAssignableFrom(typeof(T))) {
        return (T)(object)_builder.CreateDelegate(typeof(T), this);
      }

      throw new InvalidCastException(string.Format(Resources.BadCast, GetType(), typeof(T)));
    }

    ILuaValue ILuaValue.Minus() {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
    }
    ILuaValue ILuaValue.Not() {
      return LuaBoolean.False;
    }
    ILuaValue ILuaValue.Length() {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
    }
    ILuaValue ILuaValue.RawLength() {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
    }
    ILuaValue ILuaValue.Single() {
      return this;
    }

    ILuaValue ILuaValue.GetIndex(ILuaValue index) {
      throw new InvalidOperationException(Errors.CannotIndex(LuaValueType.Function));
    }
    void ILuaValue.SetIndex(ILuaValue index, ILuaValue value) {
      throw new InvalidOperationException(Errors.CannotIndex(LuaValueType.Function));
    }

    ILuaValue ILuaValue.Arithmetic(BinaryOperationType type, ILuaValue other) {
      // Attempt to use a meta-method.
      var ret = LuaValueBase._attemptMetamethod(type, this, other);
      if (ret != null) {
        return ret;
      }

      // Do some default operations.
      ret = _defaultArithmetic(type, other);
      if (ret != null) {
        return ret;
      }

      if (other is ILuaValueVisitor visitor) {
        return visitor.Arithmetic(type, this);
      } else {
        throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
      }
    }
    ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaBoolean self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Bool));
    }
    ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaClass self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.UserData));
    }
    ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaFunction self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
    }
    ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaNil self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Nil));
    }
    ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaNumber self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
    }
    ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaString self) {
      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.String));
    }
    ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaTable self) {
      var ret = LuaValueBase._attemptMetamethod(type, self, this);
      if (ret != null) {
        return ret;
      }

      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Table));
    }
    ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaThread self) {
      var ret = LuaValueBase._attemptMetamethod(type, self, this);
      if (ret != null) {
        return ret;
      }

      throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Thread));
    }
    ILuaValue ILuaValueVisitor.Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self) {
      return self.ArithmeticFrom(type, this);
    }

    /// <summary>
    /// Performs some default arithmetic like comparisons and returns the result. Returns null if
    /// there is no default.
    /// </summary>
    /// <param name="type">The type of operation to perform.</param>
    /// <param name="other">The other value to use.</param>
    /// <returns>The result of the operation.</returns>
    ILuaValue _defaultArithmetic(BinaryOperationType type, ILuaValue other) {
      switch (type) {
        case BinaryOperationType.Concat:
          return new LuaString(ToString() + other.ToString());
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
          return other;
        case BinaryOperationType.Or:
          return this;
        default:
          return null;
      }
    }

    #endregion

    #region DynamicObject overrides

    public override bool TryInvoke(InvokeBinder binder, object[] args, out object result) {
      var ret = Invoke(LuaNil.Nil, false, LuaMultiValue.CreateMultiValueFromObj(args));
      result = ret.GetValue();
      return true;
    }

    #endregion
  }
}
