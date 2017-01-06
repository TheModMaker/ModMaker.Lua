using ModMaker.Lua.Parser.Items;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ModMaker.Lua.Runtime.LuaValues
{
    /// <summary>
    /// Defines a method in Lua.  This can be a function that is defined in Lua,
    /// a framework method such as 'math.modf', or a user-defined method with
    /// dynamic overload resolution.
    /// </summary>
    /// <remarks>
    /// This type does little work in invoking a method, this is simply general
    /// code and a default implementation of LuaValues.Function.  See
    /// LuaOverloadFunction and LuaDefinedFunction for more info.
    /// </remarks>
    public abstract class LuaFunction : DynamicObject, ILuaValue, ILuaValueVisitor, IDisposable
    {
        bool _disposed = false;

        /// <summary>
        /// Creates a new LuaFunction.
        /// </summary>
        /// <param name="name">The name of the method</param>
        protected LuaFunction(string name)
        {
            this.Name = name;
        }
        /// <summary>
        /// Finalizer.
        /// </summary>
        ~LuaFunction()
        {
            if (!_disposed)
                Dispose(false);
        }

        /// <summary>
        /// Gets the name of the function.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Performs that actual invokation of the method.
        /// </summary>
        /// <param name="target">The object that this was called on.</param>
        /// <param name="memberCall">Whether the call used member call syntax (:).</param>
        /// <param name="args">The current arguments, not null but maybe empty.</param>
        /// <param name="overload">The overload to chose or negative to do 
        /// overload resoltion.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <returns>The values to return to Lua.</returns>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        /// <exception cref="System.IndexOutOfRangeException">If overload is
        /// larger than the number of overloads.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support overloads.</exception>
        protected abstract ILuaMultiValue InvokeInternal(ILuaValue target, bool methodCall, int overload, ILuaMultiValue args);
        /// <summary>
        /// Invokes the object with the given arguments.
        /// </summary>
        /// <param name="self">The object being called on.</param>
        /// <param name="memberCall">Whether the call was using member call notation (:).</param>
        /// <param name="args">The arguments for the call.</param>
        /// <param name="overload">Specifies the overload to call; -1 to use overload-resolution.</param>
        /// <returns>The return values from the invokation.</returns>
        public virtual ILuaMultiValue Invoke(ILuaValue self, bool memberCall, int overload, ILuaMultiValue args)
        {
            return InvokeInternal(self, memberCall, overload, args);
        }
        /// <summary>
        /// Adds an overload to the current method object.  This is used by the
        /// environment to register multiple delegates.  The default behavior
        /// is to throw a NotSupportedException.
        /// </summary>
        /// <param name="d">The delegate to register.</param>
        /// <exception cref="System.ArgumentNullException">If d is null.</exception>
        /// <exception cref="System.ArgumentException">If the delegate is not
        /// compatible with the current object.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support adding overloads.</exception>
        public virtual void AddOverload(Delegate d)
        {
            throw new NotSupportedException("Cannot add overloads to the current method.");
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the other parameter; otherwise, false.</returns>
        public bool Equals(ILuaValue other)
        {
            return object.ReferenceEquals(this, other);
        }
        /// <summary>
        ///  Determines whether the specified System.Object is equal to the current System.Object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return object.ReferenceEquals(this, obj);
        }
        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>A hash code for the current System.Object.</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
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
        public int CompareTo(ILuaValue other)
        {
            if (Equals(other))
                return 0;
            else
                throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
        }

        /// <summary>
        /// Converts the current function to a delegate type.
        /// </summary>
        /// <typeparam name="T">The delegate to convert to.</typeparam>
        /// <returns>A delegate that will call this function.</returns>
        public T As<T>(ILuaEnvironment E)
        {
            return (T)(object)E.CodeCompiler.CreateDelegate(E, typeof(T), this);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or
        /// resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                GC.SuppressFinalize(this);
                Dispose(true);
            }
        }
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or
        /// resetting unmanaged resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {

        }

        #region LuaValue and LuaValueVisitor Implementation

        /// <summary>
        /// Gets the value type of the value.
        /// </summary>
        LuaValueType ILuaValue.ValueType { get { return LuaValueType.Function; } }
        /// <summary>
        /// Gets whether the value is Lua true value.
        /// </summary>
        bool ILuaValue.IsTrue { get { return true; } }

        /// <summary>
        /// Gets the value for this object.  For values that don't
        /// wrap something, it simply returns this.
        /// </summary>
        /// <returns>The value for this object.</returns>
        object ILuaValue.GetValue()
        {
            return this;
        }
        /// <summary>
        /// Converts the given value to a number, or returns null.
        /// </summary>
        /// <returns>The current value as a double, or null.</returns>
        double? ILuaValue.AsDouble()
        {
            return null;
        }

        /// <summary>
        /// Gets the value of the object cast to the given type.
        /// Throws an exception if the cast is invalid.
        /// </summary>
        /// <typeparam name="T">The type to cast to.</typeparam>
        /// <returns>The value of the object as the given type.</returns>
        /// <exception cref="System.InvalidCastException">If the type cannot
        /// be converted to the type.</exception>
        T ILuaValue.As<T>()
        {
            if (typeof(T).IsAssignableFrom(GetType()))
                return (T)(object)this;

            if (typeof(Delegate).IsAssignableFrom(typeof(T)))
            {
                throw new NotSupportedException(
                    "Must use As(ILuaEnvironment) to cast to Delegate type.");
            }

            throw new InvalidCastException(string.Format(Resources.BadCast, GetType(), typeof(T)));
        }
        /// <summary>
        /// Determines if the current object can be cast to the given
        /// value.
        /// </summary>
        /// <typeparam name="T">The type to cast to.</typeparam>
        /// <returns>Whether this object can be cast to the given type.</returns>
        bool ILuaValue.TypesCompatible<T>()
        {
            if (typeof(T).IsAssignableFrom(GetType()))
                return true;
            else
                return false;
        }
        /// <summary>
        /// Gets information about the cast between the current type
        /// and the given type.  This value is used in overload 
        /// resolution. If this is not implemented; the default 
        /// values will be used.
        /// </summary>
        /// <typeparam name="T">The type to cast to.</typeparam>
        /// <param name="type">The type of cast used.</param>
        /// <param name="distance">The type distance for the given cast.</param>
        /// <exception cref="System.NotSupportedException">If custom
        /// type distance is not implemented.</exception>
        /// <remarks>
        /// The distance must be a non-negative number.  The same value
        /// means an equivilent cast.  A larger number means that it is
        /// further away.  When determining overload resolution, a
        /// smaller value is attempted.  They are only used for
        /// comparison; their value is never used directly.
        /// </remarks>
        void ILuaValue.GetCastInfo<T>(out LuaCastType type, out int distance)
        {
            distance = 0;
            if (typeof(T).IsAssignableFrom(GetType()))
                type = LuaCastType.SameType;
            else
                type = LuaCastType.NoCast;
        }

        /// <summary>
        /// Gets the unary minus of the value.
        /// </summary>
        /// <returns>The unary minus of the value.</returns>
        ILuaValue ILuaValue.Minus()
        {
            throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
        }
        /// <summary>
        /// Gets the boolean negation of the value.
        /// </summary>
        /// <returns>The boolean negation of the value.</returns>
        ILuaValue ILuaValue.Not()
        {
            return LuaBoolean.False;
        }
        /// <summary>
        /// Gets the length of the value.
        /// </summary>
        /// <returns>The length of the value.</returns>
        ILuaValue ILuaValue.Length()
        {
            throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
        }
        /// <summary>
        /// Gets the raw-length of the value.
        /// </summary>
        /// <returns>The length of the value.</returns>
        ILuaValue ILuaValue.RawLength()
        {
            throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
        }
        /// <summary>
        /// Removes and multiple arguments and returns as a single item.
        /// </summary>
        /// <returns>Either this, or the first in a multi-value.</returns>
        ILuaValue ILuaValue.Single()
        {
            return this;
        }

        /// <summary>
        /// Indexes the value and returns the value.
        /// </summary>
        /// <param name="index">The index to use.</param>
        /// <returns>The value at the given index.</returns>
        ILuaValue ILuaValue.GetIndex(ILuaValue index)
        {
            throw new InvalidOperationException(Errors.CannotIndex(LuaValueType.Function));
        }
        /// <summary>
        /// Indexes the value and assigns it a value.
        /// </summary>
        /// <param name="index">The index to use.</param>
        /// <param name="value">The value to assign to.</param>
        void ILuaValue.SetIndex(ILuaValue index, ILuaValue value)
        {
            throw new InvalidOperationException(Errors.CannotIndex(LuaValueType.Function));
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
        ILuaValue ILuaValue.Arithmetic(BinaryOperationType type, ILuaValue other)
        {
            // Attempt to use a meta-method.
            var ret = LuaValueBase.AttempMetamethod(type, this, other);
            if (ret != null)
                return ret;

            // Do some default operations.
            ret = DefaultArithmetic(type, other);
            if (ret != null)
                return ret;

            // If the other is not a visitor, throw.
            if (!(other is ILuaValueVisitor))
                throw new InvalidOperationException(Errors.CannotArithmetic(LuaValueType.Function));
            else 
                return ((ILuaValueVisitor)other).Arithmetic(type, this);
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
        ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaBoolean self)
        {
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
        ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaClass self)
        {
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
        ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaFunction self)
        {
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
        ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaNil self)
        {
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
        ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaNumber self)
        {
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
        ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaString self)
        {
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
        ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaValues.LuaTable self)
        {
            var ret = LuaValueBase.AttempMetamethod(type, self, this);
            if (ret != null)
                return ret;

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
        ILuaValue ILuaValueVisitor.Arithmetic(BinaryOperationType type, LuaValues.LuaThread self)
        {
            var ret = LuaValueBase.AttempMetamethod(type, self, this);
            if (ret != null)
                return ret;

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
        ILuaValue ILuaValueVisitor.Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self)
        {
            return self.ArithmeticFrom(type, this);
        }

        /// <summary>
        /// Performs some default arithmetic like comparisons and returns the result.
        /// Returns null if there is no default.
        /// </summary>
        /// <param name="type">The type of operation to perform.</param>
        /// <param name="other">The other value to use.</param>
        /// <returns>The result of the operation.</returns>
        private ILuaValue DefaultArithmetic(BinaryOperationType type, ILuaValue other)
        {
            switch (type)
            {
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
                    return other;
                case BinaryOperationType.Or:
                    return this;
                default:
                    return null;
            }
        }

        #endregion

        #region DynamicObject overrides

        /// <summary>
        /// Provides the implementation for operations that invoke an object. Classes
        ///     derived from the System.Dynamic.DynamicObject class can override this method
        ///     to specify dynamic behavior for operations such as invoking an object or
        ///     a delegate.
        /// </summary>
        /// <param name="binder">Provides information about the invoke operation.</param>
        /// <param name="args">The arguments that are passed to the object during the invoke operation.
        ///     For example, for the sampleObject(100) operation, where sampleObject is derived
        ///     from the System.Dynamic.DynamicObject class, args[0] is equal to 100.</param>
        /// <param name="result">The result of the object invocation.</param>
        /// <returns>true if the operation is successful; otherwise, false. If this method returns
        ///     false, the run-time binder of the language determines the behavior. (In most
        ///     cases, a language-specific run-time exception is thrown.</returns>
        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            var ret = InvokeInternal(LuaNil.Nil, false, -1, 
                                     LuaMultiValue.CreateMultiValueFromObj(args));
            result = ret.GetValue();
            return true;
        }
        /// <summary>
        /// Provides implementation for type conversion operations. Classes derived from
        ///     the System.Dynamic.DynamicObject class can override this method to specify
        ///     dynamic behavior for operations that convert an object from one type to another.
        /// </summary>
        /// <param name="binder">Provides information about the conversion operation. The binder.Type property
        ///     provides the type to which the object must be converted. For example, for
        ///     the statement (String)sampleObject in C# (CType(sampleObject, Type) in Visual
        ///     Basic), where sampleObject is an instance of the class derived from the System.Dynamic.DynamicObject
        ///     class, binder.Type returns the System.String type. The binder.Explicit property
        ///     provides information about the kind of conversion that occurs. It returns
        ///     true for explicit conversion and false for implicit conversion.</param>
        /// <param name="result">The result of the type conversion operation.</param>
        /// <returns>true if the operation is successful; otherwise, false. If this method returns
        ///     false, the run-time binder of the language determines the behavior. (In most
        ///     cases, a language-specific run-time exception is thrown.)</returns>
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.Type.IsAssignableFrom(GetType()))
            {
                result = this;
                return true;
            }
            return base.TryConvert(binder, out result);
        }

        #endregion
    }
}