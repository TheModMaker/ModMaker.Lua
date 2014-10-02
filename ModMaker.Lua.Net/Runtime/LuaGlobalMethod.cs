using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security;
using ModMaker.Lua.Runtime;
using System.Threading;
using System.Dynamic;
using System.Reflection.Emit;
using System.Reflection;
using System.Linq.Expressions;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// A global Lua function for a chunk of code.
    /// </summary>
    public class LuaGlobalMethod : LuaMethod
    {
        /// <summary>
        /// Stores the dynamic type, contains a constuctor that accepts the type.
        /// </summary>
        static Type createdType = null;
        /// <summary>
        /// The dynamic Lua type.
        /// </summary>
        protected Type _Type;

        /// <summary>
        /// Creates a new instance of LuaChunk with the given backing type.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="type">The generated type, must implement IMethod.</param>
        protected LuaGlobalMethod(ILuaEnvironment E, Type/*!*/ type)
            : base(E, type.Name)
        {
            this._Type = type;
        }

        /// <summary>
        /// Performs that actual invokation of the method.
        /// </summary>
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
        protected override MultipleReturn InvokeInternal(int overload, int[]/*!*/ byRef, object[]/*!*/ args)
        {
            IMethod target = (IMethod)Activator.CreateInstance(_Type, new[] { Environment });
            return target.Invoke(byRef, args);
        }

        /// <summary>
        /// Creates a new instance of LuaGlobalMethod using the given type.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="type">The type to use, must implement IMethod.</param>
        /// <returns>A new LuaGlobalMethod object.</returns>
        /// <exception cref="System.ArgumentNullException">If E or type is null.</exception>
        /// <exception cref="System.ArgumentException">If type does not implement
        /// IMethod.</exception>
        public static LuaGlobalMethod Create(ILuaEnvironment E, Type type)
        {
            if (E == null)
                throw new ArgumentNullException("E");
            if (type == null)
                throw new ArgumentNullException("type");
            if (!typeof(IMethod).IsAssignableFrom(type))
                throw new ArgumentException("The type must implement IMethod.");

            if (createdType == null)
                CreateType();

            if (Lua.UseDynamicTypes)
                return (LuaGlobalMethod)Activator.CreateInstance(createdType, E, type);
            else
                return new LuaGlobalMethod(E, type);
        }
        /// <summary>
        /// Creates the dynamic type.
        /// </summary>
        static void CreateType()
        {
            if (createdType != null)
                return;

            var tb = NetHelpers.GetModuleBuilder().DefineType("LuaGlobalMethodImpl",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoClass,
                typeof(LuaGlobalMethod), new Type[0]);
            var field = typeof(LuaGlobalMethod).GetField("_Type", BindingFlags.NonPublic | BindingFlags.Instance);

            //// .ctor(ILuaEnvironment E, Type/*!*/ type);
            var ctor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                new[] { typeof(ILuaEnvironment), typeof(Type) });
            var gen = ctor.GetILGenerator();

            // base(E, type);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Call, typeof(LuaGlobalMethod).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(ILuaEnvironment), typeof(Type) }, null));
            gen.Emit(OpCodes.Ret);

            //// MultipleReturn InvokeInternal(int overload, int[]/*!*/ byRef, object[]/*!*/ args);
            var mb = tb.DefineMethod("InvokeInternal",
                MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(MultipleReturn), new[] { typeof(int), typeof(int[]), typeof(object[]) });
            gen = mb.GetILGenerator();

            // object[] args = new object[1];
            var args = gen.CreateArray(typeof(object), 1);

            // args[0] = this.Environment;
            gen.Emit(OpCodes.Ldloc, args);
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Callvirt, typeof(IMethod).GetMethod("get_Environment"));
            gen.Emit(OpCodes.Stelem, typeof(object));

            // IMethod target = (IMethod)Activator.CreateInstance($Type, args);
            var target = gen.DeclareLocal(typeof(IMethod));
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, field);
            gen.Emit(OpCodes.Ldloc, args);
            gen.Emit(OpCodes.Call, typeof(Activator).GetMethod("CreateInstance",
                new[] { typeof(Type), typeof(object[]) }));
            gen.Emit(OpCodes.Castclass, typeof(IMethod));
            gen.Emit(OpCodes.Stloc, target);

            // return target.Invoke(byRef, args);
            gen.Emit(OpCodes.Ldloc, target);
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Tailcall);
            gen.Emit(OpCodes.Callvirt, typeof(IMethod).GetMethod("Invoke",
                new[] { typeof(int[]), typeof(object[]) }));
            gen.Emit(OpCodes.Ret);

            // add tailcall compatible IInvokable implementations.
            LuaMethod.AddInvokableImpl(tb);
            createdType = tb.CreateType();
        }
    }
}