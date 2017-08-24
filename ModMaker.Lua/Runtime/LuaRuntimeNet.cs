using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ModMaker.Lua.Parser.Items;
using ModMaker.Lua.Runtime.LuaValues;
using System.Threading;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines the default Lua runtime.  This class is incharge of resolving
    /// operators and converting types.  This can be inherited to modify it's 
    /// behaviour.
    /// </summary>
    public class LuaRuntimeNet : ILuaRuntime
    {
        ILuaEnvironment E;
        ThreadPool threadPool_;

        /// <summary>
        /// Creates a new instance of the default LuaRuntime.
        /// </summary>
        protected LuaRuntimeNet(ILuaEnvironment E) 
        {
            this.E = E;
            threadPool_ = new ThreadPool(E);
        }

        /// <summary>
        /// Contains the LuaRuntimeImpl type for generating LuaRuntime objects.
        /// </summary>
        private static Type runtimeType = null;
        
        /// <summary>
        /// Creates a new instance of LuaRuntime.
        /// </summary>
        /// <returns>A new LuaRuntime object.</returns>
        /// <remarks>
        /// This is needed because the Invoke method needs to have 
        /// OpCodes.Tailcall in order to have proper tail calls support.
        /// Because C# does not add these, the Invoke method must be generated
        /// at runtime.
        /// </remarks>
        public static LuaRuntimeNet Create(ILuaEnvironment E)
        {
            if (runtimeType == null)
                CreateType();

            if (Lua.UseDynamicTypes)
                return (LuaRuntimeNet)Activator.CreateInstance(runtimeType, E);
            else
                return new LuaRuntimeNet(E);
        }
        /// <summary>
        /// Create the dynamic type.
        /// </summary>
        static void CreateType()
        {
            // TODO: Port dynamic runtime for proper tail-calls.
            /*if (runtimeType != null)
                return;

            var tb = NetHelpers.GetModuleBuilder().DefineType("LuaRuntimeImpl",
                TypeAttributes.Public, typeof(LuaRuntimeNet), new Type[0]);

            //// .ctor();
            var ctor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(ILuaEnvironment) });
            var gen = ctor.GetILGenerator();

            // base(E);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Call, typeof(LuaRuntimeNet).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(ILuaEnvironment) }, null));
            gen.Emit(OpCodes.Ret);

            //// override MultipleReturn Invoke(ILuaEnvironment E, object self, object value, int overload, bool memberCall, object[] args, int[] byRef);
            var mb = tb.DefineMethod("Invoke",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(MultipleReturn), new[] { typeof(ILuaEnvironment), typeof(object), typeof(object), typeof(int), typeof(bool), typeof(object[]), typeof(int[]) });
            gen = mb.GetILGenerator();
            var next = gen.DefineLabel();
            var call = gen.DefineLabel();

            // if (E == null) throw new ArgumentNullException("E");
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Brtrue, next);
            gen.Emit(OpCodes.Ldstr, "E");
            gen.Emit(OpCodes.Newobj, typeof(ArgumentNullException).GetConstructor(new[] { typeof(string) }));
            gen.Emit(OpCodes.Throw);

            // if (value is MultipleReturn) value = ((MultipleReturn)value)[0];
            gen.MarkLabel(next);
            next = gen.DefineLabel();
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Isinst, typeof(MultipleReturn));
            gen.Emit(OpCodes.Brfalse, next);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Castclass, typeof(MultipleReturn));
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Callvirt, typeof(MultipleReturn).GetMethod("get_Item"));
            gen.Emit(OpCodes.Starg, 3);

            // if (value is LuaUserData) value = ((LuaUserData)value).Backing;
            gen.MarkLabel(next);
            next = gen.DefineLabel();
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Isinst, typeof(LuaUserData));
            gen.Emit(OpCodes.Brfalse, next);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Castclass, typeof(LuaUserData));
            gen.Emit(OpCodes.Callvirt, typeof(LuaUserData).GetMethod("get_Backing"));
            gen.Emit(OpCodes.Starg, 3);

            // if (!(value is IMethod)) goto next;
            gen.MarkLabel(next);
            next = gen.DefineLabel();
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Isinst, typeof(IMethod));
            gen.Emit(OpCodes.Brfalse, next);

            // return value.Invoke(self, memberCall, overload, byRef, args);
            gen.MarkLabel(call);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Castclass, typeof(IMethod));
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Ldarg, 5);
            gen.Emit(OpCodes.Ldarg, 4);
            gen.Emit(OpCodes.Ldarg, 7);
            gen.Emit(OpCodes.Ldarg, 6);
            gen.Emit(OpCodes.Tailcall);
            gen.Emit(OpCodes.Callvirt, typeof(IMethod).GetMethod("Invoke",
                new[] { typeof(object), typeof(bool), typeof(int), typeof(int[]), typeof(object[]) }));
            gen.Emit(OpCodes.Ret);

            // throw new InvalidOperationException("Attempt to call a '" + GetValueType(value) + "' type.");
            gen.MarkLabel(next);
            gen.Emit(OpCodes.Ldstr, "Attempt to call a '");
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Callvirt, typeof(LuaRuntimeNet).GetMethod("GetValueType", BindingFlags.NonPublic | BindingFlags.Instance));
            gen.Emit(OpCodes.Box, typeof(LuaValueType));
            gen.Emit(OpCodes.Ldstr, "' type.");
            gen.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new[] { typeof(object), typeof(object), typeof(object) }));
            gen.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) }));
            gen.Emit(OpCodes.Throw);
            gen.Emit(OpCodes.Ret);

            runtimeType = tb.CreateType();*/
        }

        /// <summary>
        /// Gets or sets whether to use a thread pool for Lua threads.
        /// </summary>
        public bool UseThreadPool { get; set; }
        /// <summary>
        /// Gets the Lua thread object for the current thread.  This will be null for the main
        /// thread.
        /// </summary>
        public ILuaThread CurrentThread 
        {
            get { return threadPool_.Search(Thread.CurrentThread.ManagedThreadId); }
        }
        
        /// <summary>
        /// Starts a generic for loop and returns an enumerator object used to
        /// get the values.
        /// </summary>
        /// <param name="args">The input arguments.</param>
        /// <param name="E">The current environment.</param>
        /// <returns>An object used to enumerate over the loop, cannot be null.</returns>
        /// <exception cref="System.ArgumentNullException">If args or E is null.</exception>
        /// <exception cref="System.InvalidOperationException">If the object(s)
        /// cannot be enumerated over.</exception>
        public virtual IEnumerable<ILuaMultiValue> GenericLoop(ILuaEnvironment E, ILuaMultiValue args)
        {
            // TODO: Replace this.
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            if (E == null)
                throw new ArgumentNullException(nameof(E));

            ILuaValue target = args[0];
            object temp = target.GetValue();
            if (temp is IEnumerable<ILuaMultiValue>)
            {
                foreach (var item in (IEnumerable<ILuaMultiValue>)temp)
                    yield return item;
            }
            else if (temp is IEnumerable)
            {
                foreach (var item in (IEnumerable)temp)
                {
                    yield return new LuaMultiValue(CreateValue(item));
                }
            }
            else if (target.ValueType == LuaValueType.Function)
            {
                ILuaValue s = args[1];
                ILuaValue var = args[2];

                while (true)
                {
                    var ret = target.Invoke(LuaNil.Nil, false, -1, CreateMultiValue(s, var));
                    if (ret == null || ret[0] == null || ret[0] == LuaNil.Nil)
                        yield break;
                    var = ret[0];

                    yield return ret;
                }
            }
            else
                throw new InvalidOperationException("Cannot enumerate over an object of type '" + args[0] + "'.");
        }

        /// <summary>
        /// Creates a new ILuaValue object that wraps the given C# value.
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        /// <returns>A new ILuaValue object.</returns>
        public ILuaValue CreateValue(object value)
        {
            return LuaValueBase.CreateValue(value);
        }
        /// <summary>
        /// Creates a new ILuaMultiValue object that contains the given values.
        /// </summary>
        /// <param name="values">The values it contains.</param>
        /// <returns>A new ILuaMultiValue object.</returns>
        public ILuaMultiValue CreateMultiValue(params ILuaValue[] values)
        {
            return new LuaMultiValue(values);
        }
        /// <summary>
        /// Creates a new ILuaMultiValue object that contains the given values.
        /// </summary>
        /// <param name="values">The values it contains.</param>
        /// <returns>A new ILuaMultiValue object.</returns>
        public ILuaMultiValue CreateMultiValueFromObj(params object[] values)
        {
            return LuaMultiValue.CreateMultiValueFromObj(values);
        }
        /// <summary>
        /// Creates a new Lua thread that calls the given method.
        /// </summary>
        /// <param name="method">The method to call.</param>
        /// <returns>The new Lua thread object.</returns>
        public ILuaThread CreateThread(ILuaValue method)
        {
            return threadPool_.Create(method);
        }
        /// <summary>
        /// Creates a new LuaTable object.
        /// </summary>
        /// <returns>A new LuaTable object.</returns>
        public ILuaTable CreateTable()
        {
            return new LuaValues.LuaTable();
        }
        /// <summary>
        /// Creates a new ILuaValue object that will call the given method.
        /// The calling convention of this method and the type of the object
        /// are implementation-defined; however none of the arguments are null.
        /// </summary>
        /// <param name="name">The name of the function.</param>
        /// <param name="method">The MethodInfo that defines the function.</param>
        /// <param name="target">The 'this' object for the function.</param>
        /// <returns>A new ILuaValue object.</returns>
        public ILuaValue CreateImplementationFunction(string name, MethodInfo method, object target)
        {
            return new LuaValues.LuaDefinedFunction(E, name, method, target);
        }
        /// <summary>
        /// Called when the code encounters the 'class' keyword.  Defines a 
        /// LuaClass object with the given name.
        /// </summary>
        /// <param name="impl">The types that the class will derive.</param>
        /// <param name="name">The name of the class.</param>
        /// <exception cref="System.InvalidOperationException">If there is
        /// already a type with the given name -or- if the types are not valid
        /// to derive from (e.g. sealed).</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        public void CreateClassValue(string[] impl, string name)
        {
            Type b = null;
            List<Type> inter = new List<Type>();
            foreach (var item in impl)
            {
                // get the types that this Lua code can access according to the settings.
                Type[] access;
                if (E.Settings.ClassAccess == LuaClassAccess.All)
                    access = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).ToArray();
                else if (E.Settings.ClassAccess == LuaClassAccess.System)
                {
                    access = E.GlobalsTable.Where(k => k.Value is LuaType).Select(k => (k.Value as LuaType).Type).ToArray();
                    access = access.Union(
                        AppDomain.CurrentDomain.GetAssemblies()
                            .Where(a => Resources.Whitelist.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Contains(a.GetName().GetPublicKey().ToStringBase16()))
                            .SelectMany(a => a.GetTypes())
                        ).ToArray();
                }
                else
                    access = E.GlobalsTable.Where(k => k.Value is LuaType).Select(k => (k.Value as LuaType).Type).ToArray();

                // Get the types that match the given name.
                Type[] typesa = access.Where(t => t.Name == item || t.FullName == item).ToArray();
                if (typesa == null || typesa.Length == 0)
                    throw new InvalidOperationException("Unable to locate the type '" + item + "'");
                if (typesa.Length > 1)
                    throw new InvalidOperationException("More than one type found for name '" + name + "'");
                Type type = typesa[0];

                if ((type.Attributes & TypeAttributes.Public) != TypeAttributes.Public &&
                    (type.Attributes & TypeAttributes.NestedPublic) != TypeAttributes.NestedPublic)
                    throw new InvalidOperationException("Base class and interfaces must be public");

                if (type.IsClass)
                {
                    // if the type is a class, it will be the base class
                    if (b == null)
                    {
                        if (type.IsSealed)
                            throw new InvalidOperationException("Cannot derive from a sealed class.");
                        if (type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null) == null)
                            throw new InvalidOperationException("Cannot derive from a type without an empty constructor.");

                        b = type;
                    }
                    else
                        throw new InvalidOperationException("Can only derive from a single base class.");
                }
                else if (type.IsInterface)
                    inter.Add(type);
                else
                    throw new InvalidOperationException("Cannot derive from a value-type.");
            }

            // create and register the LuaClass object.
            LuaClass c = new LuaClass(name, b, inter.ToArray(), E);
            E.GlobalsTable.SetItemRaw(new LuaString(name), c);
        }
    }
}