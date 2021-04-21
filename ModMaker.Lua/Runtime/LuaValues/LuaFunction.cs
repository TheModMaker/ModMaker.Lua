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
  public abstract class LuaFunction : DynamicObject, ILuaValue, ILuaValueVisitor, IDisposable {
    bool _disposed = false;

    /// <summary>
    /// Creates a new LuaFunction.
    /// </summary>
    /// <param name="name">The name of the method</param>
    protected LuaFunction(string name) {
      Name = name;
    }
    ~LuaFunction() {
      if (!_disposed) {
        _dispose(false);
      }
    }

    /// <summary>
    /// Gets the name of the function.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Performs that actual invocation of the method.
    /// </summary>
    /// <param name="target">The object that this was called on.</param>
    /// <param name="memberCall">Whether the call used member call syntax (:).</param>
    /// <param name="args">The current arguments, not null but maybe empty.</param>
    /// <param name="overload">The overload to chose or negative to do overload resolution.</param>
    /// <param name="byRef">An array of the indices that are passed by-reference.</param>
    /// <returns>The values to return to Lua.</returns>
    /// <exception cref="System.ArgumentException">If the object cannot be
    /// invoked with the given arguments.</exception>
    /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
    /// valid overloads for the given arguments.</exception>
    /// <exception cref="System.IndexOutOfRangeException">If overload is
    /// larger than the number of overloads.</exception>
    /// <exception cref="System.NotSupportedException">If this object does
    /// not support overloads.</exception>
    protected abstract ILuaMultiValue _invokeInternal(ILuaValue target, bool methodCall,
                                                      int overload, ILuaMultiValue args);
    public virtual ILuaMultiValue Invoke(ILuaValue self, bool memberCall, int overload,
                                         ILuaMultiValue args) {
      return _invokeInternal(self, memberCall, overload, args);
    }
    /// <summary>
    /// Adds an overload to the current method object.  This is used by the environment to register
    /// multiple delegates.  The default behavior is to throw a NotSupportedException.
    /// </summary>
    /// <param name="d">The delegate to register.</param>
    /// <exception cref="System.ArgumentNullException">If d is null.</exception>
    /// <exception cref="System.ArgumentException">If the delegate is not
    /// compatible with the current object.</exception>
    /// <exception cref="System.NotSupportedException">If this object does
    /// not support adding overloads.</exception>
    public virtual void AddOverload(Delegate d) {
      throw new NotSupportedException("Cannot add overloads to the current method.");
    }

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

    /// <summary>
    /// Converts the current function to a delegate type.
    /// </summary>
    /// <typeparam name="T">The delegate to convert to.</typeparam>
    /// <returns>A delegate that will call this function.</returns>
    public T As<T>(ILuaEnvironment env) {
      return (T)(object)env.CodeCompiler.CreateDelegate(env, typeof(T), this);
    }

    public void Dispose() {
      if (!_disposed) {
        _disposed = true;
        GC.SuppressFinalize(this);
        _dispose(true);
      }
    }
    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting
    /// unmanaged resources.
    /// </summary>
    protected virtual void _dispose(bool disposing) { }

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
        throw new NotSupportedException(
            "Must use As(ILuaEnvironment) to cast to Delegate type.");
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
      var ret = _invokeInternal(LuaNil.Nil, false, -1,
                                LuaMultiValue.CreateMultiValueFromObj(args));
      result = ret.GetValue();
      return true;
    }
    public override bool TryConvert(ConvertBinder binder, out object result) {
      if (binder.Type.IsAssignableFrom(GetType())) {
        result = this;
        return true;
      }
      return base.TryConvert(binder, out result);
    }

    #endregion
  }
}
