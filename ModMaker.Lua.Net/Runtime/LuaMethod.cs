using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines a method in Lua.  This can be a function that is defined in Lua,
    /// a framework method such as 'math.modf', or a user-defined method with
    /// dynamic overload resolution.
    /// </summary>
    /// <remarks>
    /// This type does little work in invoking a method, this is simply general
    /// code and a default implementation of IMethod and DynamicObject.  See
    /// LuaOverloadMethod and LuaDefinedMethod for more info.
    /// </remarks>
    [LuaIgnore(DefinedOnly=true)]
    public abstract class LuaMethod : DynamicObject, IIndexable, IMethod
    {
        /// <summary>
        /// Creates a new instance of LuaMethod.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the method, can be null.</param>
        /// <exception cref="System.ArgumentNullException">If E is null.</exception>
        protected LuaMethod(ILuaEnvironment E, string name)
        {
            if (E == null)
                throw new ArgumentNullException("E");

            this.Environment = E;
            this.Name = name;
        }

        /// <summary>
        /// Gets or sets the current environment.
        /// </summary>
        public ILuaEnvironment Environment { get; set; }
        /// <summary>
        /// Gets the name of the method, used for error messages.  Can be null.
        /// </summary>
        public string Name { get; protected set; }

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
        protected abstract MultipleReturn InvokeInternal(object target, bool methodCall, int overload, int[] byRef, object[] args);
        /// <summary>
        /// Adds an overload to the current method object.  This is used by the
        /// environment to register multiple delegates.  The default behaviour
        /// is to throw a NotSupportedException.
        /// </summary>
        /// <param name="d">The delegate to register.</param>
        /// <exception cref="System.ArgumentNullException">If d is null.</exception>
        /// <exception cref="System.ArgumentException">If the delegate is not
        /// compatible with the current object.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support adding overloads.</exception>
        protected internal virtual void AddOverload(Delegate d)
        {
            throw new NotSupportedException("Cannot add overloads to the current method.");
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return "Lua Method: " + Name;
        }

        /// <summary>
        /// Adds the implementation of IInvokable to the given type builder
        /// so that it can support tail calls.
        /// </summary>
        /// <param name="tb">The type builder to add the methods to, must derive
        /// from LuaMethod.</param>
        protected static void AddInvokableImpl(TypeBuilder/*!*/ tb)
        {
            //// MultipleReturn Invoke(object target, bool methodCall, int[] byRef, object[] args);
            var mb = tb.DefineMethod("Invoke",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(MultipleReturn), new[] { typeof(object), typeof(bool), typeof(int[]), typeof(object[]) });
            var gen = mb.GetILGenerator();

            // return this.Invoke(target, methodCall, -1, byRef, args);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Ldc_I4_M1);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Ldarg, 4);
            gen.Emit(OpCodes.Tailcall);
            gen.Emit(OpCodes.Callvirt, typeof(LuaMethod).GetMethod("Invoke",
                new[] { typeof(object), typeof(bool), typeof(int), typeof(int[]), typeof(object[]) }));
            gen.Emit(OpCodes.Ret);

            //// MultipleReturn Invoke(object target, bool methodCall, int overload, int[] byRef, object[] args);
            mb = tb.DefineMethod("Invoke",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(MultipleReturn), new[] { typeof(object), typeof(bool), typeof(int), typeof(int[]), typeof(object[]) });
            gen = mb.GetILGenerator();
            var next = gen.DefineLabel();

            // if (args == null) args = new object[0];
            gen.Emit(OpCodes.Ldarg, 5);
            gen.Emit(OpCodes.Brtrue, next);
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Newarr, typeof(object));
            gen.Emit(OpCodes.Starg, 5);

            // if (byRef == null) byRef = new int[0];
            gen.MarkLabel(next);
            next = gen.DefineLabel();
            gen.Emit(OpCodes.Ldarg, 4);
            gen.Emit(OpCodes.Brtrue, next);
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Newarr, typeof(int));
            gen.Emit(OpCodes.Starg, 4);

            // return this.InvokeInternal(target, methodCall, overload, byRef, args);
            gen.MarkLabel(next);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Ldarg, 4);
            gen.Emit(OpCodes.Ldarg, 5);
            gen.Emit(OpCodes.Tailcall);
            gen.Emit(OpCodes.Callvirt, typeof(LuaMethod).GetMethod("InvokeInternal",
                BindingFlags.Instance | BindingFlags.NonPublic));
            gen.Emit(OpCodes.Ret);
        }

        #region IMethod implementation

        /// <summary>
        /// Invokes the current object with the given arguments.
        /// </summary>
        /// <param name="target">The object that this was called on.</param>
        /// <param name="memberCall">Whether the call used member call syntax (:).</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <param name="args">The current arguments, can be null or empty.</param>
        /// <returns>The arguments to return to Lua.</returns>
        /// <exception cref="System.ArgumentNullException">If E is null.</exception>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        public virtual MultipleReturn Invoke(object target, bool methodCall, int[] byRef, params object[] args)
        {
            if (args == null)
                args = new object[0];
            if (byRef == null)
                byRef = new int[0];

            return InvokeInternal(target, methodCall, -1, byRef, args);
        }
        /// <summary>
        /// Invokes the current object with the given arguments.
        /// </summary>
        /// <param name="target">The object that this was called on.</param>
        /// <param name="memberCall">Whether the call used member call syntax (:).</param>
        /// <param name="args">The current arguments, can be null or empty.</param>
        /// <param name="overload">The zero-based index of the overload to invoke;
        /// if negative, use normal overload resolution.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <returns>The arguments to return to Lua.</returns>
        /// <exception cref="System.ArgumentNullException">If E is null.</exception>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        /// <exception cref="System.IndexOutOfRangeException">If overload is
        /// larger than the number of overloads.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support overloads.</exception>
        public virtual MultipleReturn Invoke(object target, bool methodCall, int overload, int[] byRef, params object[] args)
        {
            if (args == null)
                args = new object[0];
            if (byRef == null)
                byRef = new int[0];

            return InvokeInternal(target, methodCall, overload, byRef, args);
        }

        #endregion

        #region IIndexable implementation

        /// <summary>
        /// Sets the value of the given index to the given value.
        /// </summary>
        /// <param name="index">The index to use, cannot be null.</param>
        /// <param name="value">The value to set to, can be null.</param>
        /// <exception cref="System.ArgumentNullException">If index is null.</exception>
        /// <exception cref="System.InvalidOperationException">If the current
        /// type does not support setting an index -or- if index is not a valid
        /// value or type -or- if value is not a valid value or type.</exception>
        /// <exception cref="System.MemberAccessException">If Lua does not have
        /// access to the given index.</exception>
        void IIndexable.SetIndex(object index, object value)
        {
            throw new InvalidOperationException(string.Format(Resources.CannotIndex, "method"));
        }
        /// <summary>
        /// Gets the value of the given index.
        /// </summary>
        /// <param name="index">The index to use, cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">If index is null.</exception>
        /// <exception cref="System.InvalidOperationException">If the current
        /// type does not support getting an index -or- if index is not a valid
        /// value or type.</exception>
        object IIndexable.GetIndex(object index)
        {
            throw new InvalidOperationException(string.Format(Resources.CannotIndex, "method"));
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
        [LuaIgnore]
        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            var ret = InvokeInternal(null, false, -1, new int[0], args);
            result = ret == null ? null : ret.ToArray();
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
        [LuaIgnore]
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (typeof(Delegate).IsAssignableFrom(binder.Type))
            {
                result = Environment.CodeCompiler.CreateDelegate(Environment, binder.Type, this);
                return true;
            }
            else if (binder.Type == typeof(LuaMethod))
            {
                result = this;
                return true;
            }
            return base.TryConvert(binder, out result);
        }

        #endregion
    }
}