using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// A method that is defined in Lua.  This simply passes the arguments to
    /// the Lua function.
    /// </summary>
    public class LuaDefinedMethod : LuaMethod
    {
        /// <summary>
        /// The dynamic LuaDefinedMethod type.
        /// </summary>
        static Type createdType = null;
        /// <summary>
        /// A delegate for Lua defined functions.
        /// </summary>
        /// <param name="target">The object that this was called on.</param>
        /// <param name="memberCall">Whether the call used member call syntax (:).</param>
        /// <param name="E">The current environment.</param>
        /// <param name="args">The arguments to pass.</param>
        /// <returns>The values returned from Lua.</returns>
        protected delegate MultipleReturn LuaFunc(ILuaEnvironment E, object[] args, object target, bool memberCall);
        /// <summary>
        /// The backing Lua defined method.
        /// </summary>
        protected LuaFunc _Method;

        /// <summary>
        /// Creates a new LuaDefinedMethod from the given method.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the method, used in errors.</param>
        /// <param name="method">The method to invoke.</param>
        /// <exception cref="System.ArgumentException">If method is not of
        /// the correct method signature.  It must have the following form:
        /// MultipleReturn Func(ILuaEnvironment, object[]).</exception>
        protected LuaDefinedMethod(ILuaEnvironment E, LuaFunc/*!*/ method, string name)
            : base(E, name)
        {
            this._Method = method;
        }

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
        protected override MultipleReturn InvokeInternal(object target, bool memberCall, int overload, int[] byRef, object[] args)
        {
            return _Method(Environment, args, target, memberCall);
        }

        /// <summary>
        /// Creates a new instance of LuaDefinedMethod with the given arguments.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the method, used for errors.</param>
        /// <param name="method">The method to invoke.</param>
        /// <param name="target">The target object.</param>
        /// <returns>A new instance of LuaDefinedMethod.</returns>
        /// <exception cref="System.ArgumentNullException">If method, E, or target is null.</exception>
        /// <exception cref="System.ArgumentException">If method does not have
        /// the correct method signature: 
        /// MultipleReturn Method(ILuaEnvironment, object[])</exception>
        public static LuaDefinedMethod Create(ILuaEnvironment E, string name, MethodInfo method, object target)
        {
            if (E == null)
                throw new ArgumentNullException("E");
            if (method == null)
                throw new ArgumentNullException("method");
            if (target == null)
                throw new ArgumentNullException("target");
            LuaFunc func = (LuaFunc)Delegate.CreateDelegate(typeof(LuaFunc), target, method, false);
            if (func == null)
                throw new ArgumentException("The given method does not have the correct method signature.");

            if (Lua.UseDynamicTypes && createdType == null)
                CreateType();

            if (Lua.UseDynamicTypes)
                return (LuaDefinedMethod)Activator.CreateInstance(createdType, E, name, func);
            else
                return new LuaDefinedMethod(E, func, name);
        }
        /// <summary>
        /// Creates the dynamic type.
        /// </summary>
        static void CreateType()
        {
            if (createdType != null)
                return;

            var tb = NetHelpers.GetModuleBuilder().DefineType("LuaDefinedMethodImpl",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoClass,
                typeof(LuaDefinedMethod), new Type[0]);
            var field = typeof(LuaDefinedMethod).GetField("_Method", BindingFlags.Instance | BindingFlags.NonPublic);

            //// .ctor(ILuaEnvironment E, string name, LuaFunc method);
            var ctor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                new[] { typeof(ILuaEnvironment), typeof(string), typeof(LuaFunc) });
            var gen = ctor.GetILGenerator();

            // base(E, method, name);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Call, typeof(LuaDefinedMethod).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(ILuaEnvironment), typeof(LuaFunc), typeof(string) }, null));
            gen.Emit(OpCodes.Ret);

            //// MultipleReturn InvokeInternal(object target, bool memberCall, int overload, int[] byRef, object[]/*!*/ args);
            var mb = tb.DefineMethod("InvokeInternal",
                MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(MultipleReturn), new[] { typeof(object), typeof(bool), typeof(int), typeof(int[]), typeof(object[]) });
            gen = mb.GetILGenerator();

            // return _Method(Environment, args, target, memberCall);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, field);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Callvirt, typeof(LuaMethod).GetMethod("get_Environment"));
            gen.Emit(OpCodes.Ldarg, 5);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Tailcall);
            gen.Emit(OpCodes.Callvirt, typeof(LuaFunc).GetMethod("Invoke"));
            gen.Emit(OpCodes.Ret);

            // add tailcall compatible IInvokable implementations.
            LuaMethod.AddInvokableImpl(tb);

            createdType = tb.CreateType();
        }
    }
}
