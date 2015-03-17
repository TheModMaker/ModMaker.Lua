using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ModMaker.Lua.Runtime
{
    class LuaType : IIndexable, IMethod
    {
        public LuaType(Type type)
        {
            this.Type = type;
        }

        public Type Type { get; private set; }

        public override string ToString()
        {
            return Type.ToString();
        }

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
            NetHelpers.GetSetIndex(Environment, Type, index, true, false, false, value);
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
            return NetHelpers.GetSetIndex(Environment, Type, index, true, true, false);
        }

        #endregion

        #region IMethod implementation

        public ILuaEnvironment Environment { get; set; }

        /// <summary>
        /// Invokes the current object with the given arguments.
        /// </summary>
        /// <param name="args">The current arguments, can be null or empty.</param>
        /// <returns>The arguments to return to Lua.</returns>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        public MultipleReturn Invoke(int[] byRef, params object[] args)
        {
            return Invoke(-1, byRef, args);
        }
        /// <summary>
        /// Invokes the current object with the given arguments.
        /// </summary>
        /// <param name="args">The current arguments, can be null or empty.</param>
        /// <param name="overload">The zero-based index of the overload to invoke;
        /// if negative, use normal overload resolution.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <returns>The arguments to return to Lua.</returns>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        /// <exception cref="System.IndexOutOfRangeException">If overload is
        /// larger than the number of overloads.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support overloads.</exception>
        /// <remarks>
        /// If this object does not support overloads, you still need to write
        /// this method to work with negative indicies, however you should throw
        /// an exception if zero or positive.  This method is always the one
        /// invoked by the default runtime.
        /// 
        /// It is sugested that the other method simply call this one with -1
        /// as the overload index.
        /// </remarks>
        public MultipleReturn Invoke(int overload, int[] byRef, params object[] args)
        {
            if (overload >= 0)
                throw new NotSupportedException(string.Format(Resources.CannotUseOverload, "LuaType"));
            if (args == null)
                args = new object[0];
            if (byRef == null)
                byRef = new int[0];

            Type t = Type;
            ConstructorInfo method;
            object target;
            if (NetHelpers.GetCompatibleMethod(
                t.GetConstructors()
                    .Where(c => c.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0)
                    .ToArray(),
                new object[] { null }, ref args, null, out method, out target))
            {
                return new MultipleReturn(method.Invoke(args));
            }

            throw new InvalidOperationException(string.Format(Resources.CannotCall, "LuaType"));
        }

        #endregion
    }

    /// <summary>
    /// A class that was defined in Lua.  This is created in 
    /// ILuaRuntime.DefineClass.  When this object is indexed in Lua,
    /// it will change the class that is defined.  When this is 
    /// invoked, it creates a new instance.
    /// </summary>
    public sealed class LuaClass : IIndexable, IMethod
    {
        Type _created;
        IMethod _ctor;
        ItemData _data;
        List<Item> _items;

        /// <summary>
        /// Creates a new instance of LuaClass.
        /// </summary>
        /// <param name="name">The simple name of the class.</param>
        /// <param name="_base">The base class of the class; or null.</param>
        /// <param name="interfaces">The interfaces that are inherited; or null.</param>
        /// <param name="E">The current environment.</param>
        internal LuaClass(string name, Type _base, Type[] interfaces, ILuaEnvironment E)
        {
            if (interfaces == null)
                interfaces = new Type[0];

            this.Name = name;
            this.BaseType = _base;
            this._items = new List<Item>();

            var inter = interfaces.SelectMany(t => t.GetInterfaces()).Union(interfaces).ToArray();
            this.Interfaces = new ReadOnlyCollection<Type>(inter);

            this._data = new ItemData(E, name, _base, inter);
        }

        /// <summary>
        /// Gets or sets the current environment.
        /// </summary>
        public ILuaEnvironment Environment { get { return _data.Env; } set { _data.Env = value; } }
        /// <summary>
        /// Gets the simple name of the class.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Gets the base type of the class.
        /// </summary>
        public Type BaseType { get; private set; }
        /// <summary>
        /// Gets the interfaces that the class impliments.
        /// </summary>
        public ReadOnlyCollection<Type> Interfaces { get; private set; }

        /// <summary>
        /// Completes the type making it ready to create instances.  If Lua attempts to change
        /// this object after this has been called, an exception will be thrown.  This method
        /// is called when you call CreateInstance.
        /// </summary>
        public void CreateType()
        {
            if (_created != null)
                return;

            // Generate for the items.
            _items.ForEach(i => i.Generate(_data));

            // Generate for the interfaces.
            foreach (var item in Interfaces)
            {
                foreach (var method in item.GetMethods())
                {
                    //  e.g. <System.IDisposable>_Dispose
                    string name = "<" + item.FullName + ">_" + method.Name;
                    StubImplement(_data, method, name);
                }
            }

            // Generate for the base class
            if (BaseType != null)
            {
                foreach (var method in BaseType.GetMethods().Where(m => (m.Attributes & MethodAttributes.Abstract) == MethodAttributes.Abstract))
                {
                    //  e.g. <>_Dispose
                    string name = "<>_" + method.Name;
                    StubImplement(_data, method, name);
                }
            }

            InvokeConstructor(_data.CtorGen);

            _created = _data.TB.CreateType();
            //_data.AB.Save("DynamicAssembly2.dll");
        }
        /// <summary>
        /// Creates an instance of the given type with the given arguments.  Calls CreateType if it has
        /// not been called before.
        /// </summary>
        /// <param name="args">Any arguments to pass to the constructor.</param>
        /// <returns>An instance of the type.</returns>
        public object CreateInstance(params object[] args)
        {
            if (_created == null)
                CreateType();

            return Activator.CreateInstance(_created, new object[] { _data.MethodArgs.ToArray(), _data.Constants.ToArray(), Environment, args, _ctor }, new object[0]);
        }
        /// <summary>
        /// Creates an instance of the given type with the given arguments.  Calls CreateType if it has
        /// not been called before.
        /// </summary>
        /// <param name="args">Any arguments to pass to the constructor.</param>
        /// <typeparam name="T">The base-type or interface to cast the object to.  The class must impliment it.</typeparam>
        /// <returns>An instance of the type.</returns>
        public T CreateInstance<T>(params object[] args)
        {
            if (_created == null)
                CreateType();

            if (!typeof(T).IsAssignableFrom(_created))
                throw new ArgumentException(string.Format(Resources.CurrentDoesNotDerive, typeof(T)));
            return (T)CreateInstance(args);
        }

        /// <summary>
        /// Injects the code necessary to return the first value from a call to a method that
        /// is stored in a field of a type.  Creates one local variable.
        /// Injects:
        /// 
        /// <code>
        /// MultipleReturn ret = E.Runtime.Invoke(E, this.envField, arguments);
        /// return RuntimeHelper.ConvertType(ret[0], returnType);
        /// </code>
        /// </summary>
        /// <param name="arguments">The local variable (of type object[]) that contains the arguments to pass to the method.</param>
        /// <param name="gen">The generator to inject code into.</param>
        /// <param name="methodField">The field (of type LuaMethod) that contains the method to call.</param>
        /// <param name="_E">The field that stores the environment.</param>
        /// <param name="returnType">The return type of the method.</param>
        static void CallFieldAndReturn(ILGenerator gen, Type returnType, FieldBuilder methodField, LocalBuilder arguments, FieldBuilder _E)
        {
            // MultipleReturn ret = E.Runtime.Invoke(E, this.{field}, -1, arguments, null);
            LocalBuilder ret = gen.DeclareLocal(typeof(MultipleReturn));
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, _E);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, _E);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, methodField);
            gen.Emit(OpCodes.Ldc_I4_4);
            gen.Emit(OpCodes.Ldloc, arguments);
            gen.Emit(OpCodes.Ldnull);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("Invoke"));
            gen.Emit(OpCodes.Stloc, ret);

            // convert and push result if the return type is not null.
            if (returnType != null && returnType != typeof(void))
            {
                // return E.Runtime.ConvertType(ret[0], {returnType});
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, _E);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                gen.Emit(OpCodes.Ldloc, ret);
                gen.Emit(OpCodes.Ldc_I4_0);
                gen.Emit(OpCodes.Callvirt, typeof(MultipleReturn).GetMethod("get_Item"));
                gen.Emit(OpCodes.Ldtoken, returnType);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("ConvertType"));
                gen.Emit(OpCodes.Unbox_Any, returnType);
            }
            gen.Emit(OpCodes.Ret);
        }
        /// <summary>
        /// Creates a new method that implements the given method that throws a
        /// NotImplementedException.
        /// </summary>
        /// <param name="data">The current data.</param>
        /// <param name="method">The method to implement.</param>
        static void StubImplement(ItemData data, MethodInfo method, string name)
        {
            // we already created this method
            if (data.Methods.Contains(method))
                return;

            // define a no-conflict name for this method
            if (data.Names.Contains(name))
                name = name + "_" + (data.FID++);
            data.Names.Add(name);

            // define the new method with the same signature as the interface.
            var meth = NetHelpers.CloneMethod(data.TB, name, method);
            ILGenerator gen = meth.GetILGenerator();
            gen.ThrowException(typeof(NotImplementedException));

            // link our new method to be the definition of the interface method.
            data.TB.DefineMethodOverride(meth, method);
        }
        /// <summary>
        /// Adds the code to invoke the constructor.
        /// </summary>
        /// <param name="ctorgen">The generator to add the code to.</param>
        static void InvokeConstructor(ILGenerator ctorgen)
        {
            // call the Lua defined constructor method.

            // if(ctor == null) goto end;
            Label end = ctorgen.DefineLabel();
            ctorgen.Emit(OpCodes.Ldarg, 5);
            ctorgen.Emit(OpCodes.Brfalse, end);

            // object[] temp = new object[1];
            LocalBuilder temp = ctorgen.CreateArray(typeof(object), 1);

            // temp[0] = this;
            ctorgen.Emit(OpCodes.Ldloc, temp);
            ctorgen.Emit(OpCodes.Ldc_I4_0);
            ctorgen.Emit(OpCodes.Ldarg_0);
            ctorgen.Emit(OpCodes.Stelem_Ref);

            // temp = temp.Union(ctorArgs).ToArray();
            ctorgen.Emit(OpCodes.Ldloc, temp);
            ctorgen.Emit(OpCodes.Ldarg, 4);
            ctorgen.Emit(OpCodes.Call, typeof(Enumerable).GetMethods().Where(m => m.Name == "Union" && m.GetParameters().Length == 2).First().MakeGenericMethod(typeof(object)));
            ctorgen.Emit(OpCodes.Call, typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(typeof(object)));
            ctorgen.Emit(OpCodes.Stloc, temp);

            // E.Runtime.Invoke(E, ctor, -1, temp, null);
            ctorgen.Emit(OpCodes.Ldarg_3);
            ctorgen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            ctorgen.Emit(OpCodes.Ldarg_3);
            ctorgen.Emit(OpCodes.Ldarg, 5);
            ctorgen.Emit(OpCodes.Ldc_I4_M1);
            ctorgen.Emit(OpCodes.Ldloc, temp);
            ctorgen.Emit(OpCodes.Ldnull);
            ctorgen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("Invoke"));
            ctorgen.Emit(OpCodes.Pop);

            ctorgen.MarkLabel(end);

            ctorgen.Emit(OpCodes.Ret);
        }

        #region ILuaIndexer implementation

        class LuaClassItem : IIndexable
        {
            LuaClass parent;
            Type type;

            public LuaClassItem(LuaClass parent, Type type)
            {
                this.parent = parent;
                this.type = type;
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
                throw new InvalidOperationException(string.Format(Resources.CannotIndex, "class definition item"));
            }
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
                string name = index as string;
                int overload = -1;
                if (name == null)
                    throw new InvalidOperationException(string.Format(Resources.BadIndexType, "class definition", "string"));

                // find if name is defining an overload.
                if (name.Contains('`'))
                {
                    if (!int.TryParse(name.Substring(name.IndexOf('`') + 1), out overload))
                        throw new InvalidOperationException(Resources.OnlyNumbersInOverload);

                    name = name.Substring(0, name.IndexOf('`'));
                }

                // find the members with the given name.
                var members = type.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == name).ToArray();
                if (members == null || members.Length == 0)
                    throw new InvalidOperationException(string.Format(Resources.NoMemberFound, type, name));

                if (members.Length > 1 && overload == -1)
                    throw new AmbiguousMatchException(string.Format(Resources.NoMemberFound, type, name));
                if (overload != -1 && overload >= members.Length)
                    throw new InvalidOperationException(Resources.OverloadOutOfRange);

                // set the backing parrent object.
                if (members[0].MemberType == MemberTypes.Method)
                {
                    if (!(value is IMethod))
                        throw new InvalidOperationException(string.Format(Resources.MustBeFunction, name));

                    var item = parent._items.Find(i => i.Name == name);
                    if (item == null)
                    {
                        item = parent.CreateItem(name, new[] { members[overload == -1 ? 0 : overload] });
                        parent._items.Add(item);
                    }
                    item.Assign(value);
                }
                else if (members[0].MemberType == MemberTypes.Property)
                {
                    var item = parent._items.Find(i => i.Name == name);
                    if (item == null)
                    {
                        item = parent.CreateItem(name, members);
                        parent._items.Add(item);
                    }
                    item.Assign(value);
                }
            }
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
            string n = index as string;
            if (n == null)
                throw new InvalidOperationException(string.Format(Resources.BadIndexType, "class definition", "string"));

            foreach (var item in Interfaces)
            {
                if (item.Name == n)
                    return new LuaClassItem(this, item);
            }
            throw new InvalidOperationException(string.Format(Resources.DoesNotImplement, Name, n));
        }
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
            var name = index as string;
            if (_created != null || name == null)
                return;

            // If this is the contructor, assign it.
            if (name == "__ctor")
            {
                if (value is IMethod)
                    _ctor = value as IMethod;
                else
                    throw new InvalidOperationException(Resources.CtorMustBeFunc);

                return;
            }

            // Try to find an existing element.
            Item i = _items.Where(ii => ii.Name == name).FirstOrDefault();
            if (i == null)
            {
                // Check for the member in the base class.
                if (BaseType != null)
                {
                    var mems = BaseType.GetMember(name);
                    if (mems != null && mems.Length > 0)
                        i = CreateItem(name, mems);
                }

                // Check for the member in interfaces.
                if (i == null)
                {
                    Type inter = null;
                    MemberInfo[] mems = null;
                    foreach (var item in Interfaces)
                    {
                        var temp = item.GetMember(name);
                        if (temp != null && temp.Length > 0)
                        {
                            if (mems != null)
                            {
                                mems = null;
                                break;
                            }
                            inter = item;
                            mems = temp;
                        }
                    }

                    if (mems != null)
                        i = CreateItem(inter.FullName + "." + name, mems);
                }

                // If still not found, create a new field.
                if (i == null)
                {
                    i = new FieldItem(name);
                }

                _items.Add(i);
            }

            i.Assign(value);
        }
        /// <summary>
        /// Creates a new Item when the given members are found.
        /// </summary>
        /// <param name="name">The name of the member.</param>
        /// <param name="members">The members that are found.</param>
        /// <returns>A new item for the member.</returns>
        Item CreateItem(string name, MemberInfo[] members)
        {
            if (members[0].MemberType == MemberTypes.Property)
            {
                return new PropertyItem(name, members[0]);
            }
            else if (members[0].MemberType == MemberTypes.Method)
            {
                if (members.Length > 1)
                    throw new AmbiguousMatchException(string.Format(Resources.ManyMembersFound, members[0].DeclaringType, name));

                return new MethodItem(name, members[0]);
            }
            else
                throw new InvalidOperationException(string.Format(Resources.CannotHideMember, name, members[0].DeclaringType));
        }

        #endregion

        #region IMethod implementation

        /// <summary>
        /// Invokes the current object with the given arguments.
        /// </summary>
        /// <param name="args">The current arguments, can be null or empty.</param>
        /// <returns>The arguments to return to Lua.</returns>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        MultipleReturn IMethod.Invoke(int[] byRef, object[] args)
        {
            return ((IMethod)this).Invoke(-1, byRef, args);
        }
        /// <summary>
        /// Invokes the current object with the given arguments.
        /// </summary>
        /// <param name="args">The current arguments, can be null or empty.</param>
        /// <param name="overload">The zero-based index of the overload to invoke;
        /// if negative, use normal overload resolution.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <returns>The arguments to return to Lua.</returns>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        /// <exception cref="System.IndexOutOfRangeException">If overload is
        /// larger than the number of overloads.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support overloads.</exception>
        /// <remarks>
        /// If this object does not support overloads, you still need to write
        /// this method to work with negative indicies, however you should throw
        /// an exception if zero or positive.  This method is always the one
        /// invoked by the default runtime.
        /// 
        /// It is sugested that the other method simply call this one with -1
        /// as the overload index.
        /// </remarks>
        MultipleReturn IMethod.Invoke(int overload, int[] byRef, object[] args)
        {
            if (args == null)
                args = new object[0];

            return new MultipleReturn(CreateInstance(args));
        }

        #endregion

        #region Item Classes

        /// <summary>
        /// Contains data used to generate an item.
        /// </summary>
        sealed class ItemData
        {
            public ItemData(ILuaEnvironment E, string name, Type baseType, Type[] _interfaces)
            {
                this.MethodArgs = new List<IMethod>();
                this.Methods = new List<MethodInfo>();
                this.Constants = new List<object>();
                this.Names = new HashSet<string>();
                this.Env = E;
                this.FID = 0;

                // Create the type builder.
                var ab = AppDomain.CurrentDomain.DefineDynamicAssembly(
                    new AssemblyName("DynamicAssembly2"),
                    AssemblyBuilderAccess.RunAndSave);
                this.AB = ab;
                ModuleBuilder mb = ab.DefineDynamicModule("DynamicAssembly2.dll");
                this.TB = mb.DefineType(name, TypeAttributes.Class | TypeAttributes.BeforeFieldInit | TypeAttributes.Public, baseType, _interfaces);
                this.EnvField = TB.DefineField("$Env", typeof(ILuaEnvironment), FieldAttributes.Private);

                // public ctor(IMethod[] methods, object[] initialValues, LuaEnvironment E, object[] ctorArgs, IMethod ctor);
                {
                    var temp = new[] { typeof(IMethod[]), typeof(object[]), typeof(ILuaEnvironment), typeof(object[]), typeof(IMethod) };
                    ConstructorBuilder ctor = TB.DefineConstructor(
                        MethodAttributes.Public | MethodAttributes.HideBySig,
                        CallingConventions.Standard,
                        temp);
                    this.CtorGen = ctor.GetILGenerator();

                    // base();
                    CtorGen.Emit(OpCodes.Ldarg_0);
                    CtorGen.Emit(OpCodes.Call, (baseType ?? typeof(object)).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[0], null));

                    // this.$Env = E
                    CtorGen.Emit(OpCodes.Ldarg_0);
                    CtorGen.Emit(OpCodes.Ldarg_3);
                    CtorGen.Emit(OpCodes.Stfld, EnvField);
                }
            }

            public List<IMethod> MethodArgs;
            public List<MethodInfo> Methods;
            public List<object> Constants;
            public HashSet<string> Names;
            public ILGenerator CtorGen;
            public TypeBuilder TB;
            public FieldBuilder EnvField;
            public ILuaEnvironment Env;
            public int FID;
            public AssemblyBuilder AB;
        }
        /// <summary>
        /// A base class for an item used for creating a class.
        /// </summary>
        abstract class Item
        {
            public string Name;

            protected Item(string name)
            {
                this.Name = name;
            }

            /// <summary>
            /// Generates the item using the given item data.
            /// </summary>
            /// <param name="data">The data used to generate.</param>
            public abstract void Generate(ItemData data);
            /// <summary>
            /// Assigns the given value to the item.
            /// </summary>
            /// <param name="value">The value to asign to.</param>
            public abstract void Assign(object value);

            /// <summary>
            /// Adds a field to the type that contains the given method.  Adds
            /// code in the constructor to load the method.
            /// </summary>
            /// <param name="method">The method to store in the field.</param>
            /// <returns>The field that contains the method.</returns>
            protected FieldBuilder AddMethodArg(ItemData data, IMethod method)
            {
                // define a envField to hold a pointer to the method
                FieldBuilder field = data.TB.DefineField("<>_field_" + (data.FID++), typeof(IMethod), FieldAttributes.Private);

                // store the method in the input list and create code in the 
                //   constructor to get the method from the argument and store
                //   it in the envField.
                data.MethodArgs.Add(method);
                // {envField} = arg_1[{input.Count - 1}];
                data.CtorGen.Emit(OpCodes.Ldarg_0);
                data.CtorGen.Emit(OpCodes.Ldarg_1);
                data.CtorGen.Emit(OpCodes.Ldc_I4, (data.MethodArgs.Count - 1));
                data.CtorGen.Emit(OpCodes.Ldelem, typeof(IMethod));
                data.CtorGen.Emit(OpCodes.Stfld, field);

                return field;
            }
        }
        /// <summary>
        /// A class item that is a method.
        /// </summary>
        sealed class MethodItem : Item
        {
            public IMethod Method;
            public MethodInfo BoundTo;

            public MethodItem(string name, MemberInfo member)
                : base(name)
            {
                this.Method = null;
                this.BoundTo = (MethodInfo)member;
            }

            public override void Assign(object value)
            {
                IMethod temp = value as IMethod;
                if (temp == null)
                    throw new InvalidOperationException(string.Format(Resources.CannotHideMember, Name, BoundTo.DeclaringType));

                Method = temp;
            }
            public override void Generate(ItemData data)
            {
                var field = AddMethodArg(data, Method);

                // define a non-conflict named method that will back the given method.
                string name = Name;
                if (data.Names.Contains(name))
                    name = name + "_" + (data.FID++);
                data.Names.Add(name);

                // Define the new method.
                var param = BoundTo.GetParameters();
                var meth = NetHelpers.CloneMethod(data.TB, name, BoundTo);
                ILGenerator gen = meth.GetILGenerator();

                // object[] loc = new object[{param.length + 1}];
                LocalBuilder loc = gen.CreateArray(typeof(object), param.Length + 1);

                // loc[0] = this;
                gen.Emit(OpCodes.Ldloc, loc);
                gen.Emit(OpCodes.Ldc_I4_0);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Stelem, typeof(object));

                for (int ind = 0; ind < param.Length; ind++)
                {
                    // loc[{ind + 1}] = arg_{ind};
                    gen.Emit(OpCodes.Ldloc, loc);
                    gen.Emit(OpCodes.Ldc_I4, ind + 1);
                    gen.Emit(OpCodes.Ldarg, ind);
                    gen.Emit(OpCodes.Stelem, typeof(object));
                }

                CallFieldAndReturn(gen, BoundTo.ReturnType, field, loc, data.EnvField);

                // link our new method to the method it's bound to.
                //data.TB.DefineMethodOverride(meth, BoundTo);
                data.Methods.Add(BoundTo);
            }
        }
        /// <summary>
        /// A class item that is a property.
        /// </summary>
        sealed class PropertyItem : Item
        {
            public object Default;
            public Type Type;
            public IMethod Method, MethodSet;
            public MethodInfo BoundTo, BoundSet;
            private PropertyInfo _prop;

            public PropertyItem(string name, MemberInfo member)
                : base(name)
            {
                _prop = (PropertyInfo)member;
                Default = null;
                Type = _prop.PropertyType;
                Method = null;
                MethodSet = null;
                BoundTo = null;
                BoundSet = null;
            }

            public override void Assign(object value)
            {
                LuaTable table = value as LuaTable;
                if (table != null)
                {
                    foreach (var item in table)
                    {
                        // set the 'get' method for the item
                        if (item.Key as string == "get")
                        {
                            MethodInfo m = _prop.GetGetMethod(true);
                            if (m == null || (!m.Attributes.HasFlag(MethodAttributes.Abstract) && !m.Attributes.HasFlag(MethodAttributes.Virtual)))
                                throw new InvalidOperationException(string.Format(Resources.CannotOverrideProperty, Name));
                            BoundTo = m;

                            if (!(item.Value is IMethod))
                                throw new InvalidOperationException(Resources.PropTableFuncs);
                            Method = (IMethod)item.Value;
                        }
                        // set the 'set' method for the item
                        else if (item.Key as string == "set")
                        {
                            MethodInfo m = _prop.GetSetMethod(true);
                            if (m == null || (!m.Attributes.HasFlag(MethodAttributes.Abstract) && !m.Attributes.HasFlag(MethodAttributes.Virtual)))
                                throw new InvalidOperationException(string.Format(Resources.CannotOverrideProperty, Name));
                            BoundSet = m;

                            if (!(item.Value is IMethod))
                                throw new InvalidOperationException(Resources.PropTableFuncs);
                            MethodSet = (IMethod)item.Value;
                        }
                        else
                            throw new InvalidOperationException(Resources.PropTableGetSet);
                    }
                }
                else
                {
                    MethodInfo m = _prop.GetGetMethod(true);
                    if (m == null || (!m.Attributes.HasFlag(MethodAttributes.Abstract) && !m.Attributes.HasFlag(MethodAttributes.Virtual)))
                        throw new InvalidOperationException(string.Format(Resources.CannotOverrideProperty, Name));
                    BoundTo = m;

                    // check if the set method is abstract, if it is, we need to implement that also.
                    m = _prop.GetSetMethod(true);
                    if (m != null && (m.Attributes & MethodAttributes.Abstract) == MethodAttributes.Abstract)
                        BoundSet = m;

                    Default = value;
                }
            }
            public override void Generate(ItemData data)
            {
                if (BoundTo != null)
                {
                    // define a get method for the property.
                    MethodBuilder m = data.TB.DefineMethod("get_" + Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig, Type, null);
                    ILGenerator gen = m.GetILGenerator();
                    if (Method == null)
                    {
                        // define a default getter that returns a constant value.
                        string name = "<" + Name + ">_backing";
                        FieldBuilder field = data.TB.DefineField(name, Type, FieldAttributes.Private);
                        if (Default != null)
                        {
                            // convert the default value to the requested type.
                            Default = data.Env.Runtime.ConvertType(Default, Type);

                            // store the constant passed in the constructor in the envField.
                            data.Constants.Add(Default);
                            // {envField} = arg_2[{_input2.Count - 1}];
                            data.CtorGen.Emit(OpCodes.Ldarg_0);
                            data.CtorGen.Emit(OpCodes.Ldarg_2);
                            data.CtorGen.Emit(OpCodes.Ldc_I4, (data.Constants.Count - 1));
                            data.CtorGen.Emit(OpCodes.Ldelem, typeof(object));
                            data.CtorGen.Emit(OpCodes.Unbox_Any, Type);
                            data.CtorGen.Emit(OpCodes.Stfld, field);
                        }

                        // return this.{envField};
                        gen.Emit(OpCodes.Ldarg_0);
                        gen.Emit(OpCodes.Ldfld, field);
                        gen.Emit(OpCodes.Ret);
                    }
                    else
                    {
                        // define a getter method that returns a value from a method.
                        FieldBuilder field = AddMethodArg(data, Method);

                        // object[] loc = new object[0];
                        LocalBuilder loc = gen.CreateArray(typeof(object), 0);
                        CallFieldAndReturn(gen, BoundTo.ReturnType, field, loc, data.EnvField);
                    }

                    // link our new method to the method this item is bound to.
                    data.TB.DefineMethodOverride(m, BoundTo);
                    data.Methods.Add(BoundTo);
                }

                if (BoundSet != null)
                {
                    // define a setter method.
                    MethodBuilder m = data.TB.DefineMethod("set_" + Name,
                        MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig,
                        null, new[] { Type });
                    ILGenerator gen = m.GetILGenerator();

                    if (MethodSet == null)
                    {
                        // throw new NotSupportedException("Cannot write to a constant property.");
                        gen.Emit(OpCodes.Ldstr, Resources.ConstantProperty);
                        gen.Emit(OpCodes.Newobj, typeof(NotSupportedException).GetConstructor(new[] { typeof(string) }));
                        gen.Emit(OpCodes.Throw);
                    }
                    else
                    {
                        FieldBuilder field = AddMethodArg(data, MethodSet);

                        // object[] loc = new object[1];
                        LocalBuilder loc = gen.CreateArray(typeof(object), 1);

                        // loc[0] = (object)arg_1;
                        gen.Emit(OpCodes.Ldloc, loc);
                        gen.Emit(OpCodes.Ldc_I4_0);
                        gen.Emit(OpCodes.Ldarg_1);
                        if (Type.IsValueType)
                            gen.Emit(OpCodes.Box, Type);
                        gen.Emit(OpCodes.Stelem, typeof(object));

                        CallFieldAndReturn(gen, null, field, loc, data.EnvField);
                    }

                    // link our new method to the method it is bound to.
                    data.TB.DefineMethodOverride(m, BoundSet);
                    data.Methods.Add(BoundSet);
                }
            }
        }
        /// <summary>
        /// A class item that is a field.
        /// </summary>
        sealed class FieldItem : Item
        {
            public Type Type;
            public object Default;

            public FieldItem(string name)
                : base(name)
            {
                this.Type = null;
                this.Default = null;
            }

            public override void Assign(object value)
            {
                LuaType type = value as LuaType;
                if (type != null)
                {
                    if (Type == null)
                        Type = type.Type;
                    else
                        throw new InvalidOperationException(string.Format(Resources.MemberHasType, Name));
                }
                else
                {
                    if (Default != null)
                        throw new InvalidOperationException(string.Format(Resources.MemberHasDefault, Name));

                    Type = Type ?? (value == null ? typeof(object) : value.GetType());
                    Default = value;
                }
            }
            public override void Generate(ItemData data)
            {
                // define the envField with the given name or a default one.
                string name = Name;
                if (data.Names.Contains(name))
                    name = name + "_" + (data.FID++);
                FieldBuilder field = data.TB.DefineField(name, Type, FieldAttributes.Public);

                if (Default != null)
                {
                    Default = data.Env.Runtime.ConvertType(Default, Type);

                    // get the initial value from the constructor and set it.
                    data.Constants.Add(Default);
                    // {envField} = ({item.Type})arg_2[{_input2.Count - 1}];
                    data.CtorGen.Emit(OpCodes.Ldarg_0);
                    data.CtorGen.Emit(OpCodes.Ldarg_2);
                    data.CtorGen.Emit(OpCodes.Ldc_I4, (data.Constants.Count - 1));
                    data.CtorGen.Emit(OpCodes.Ldelem, typeof(object));
                    data.CtorGen.Emit(OpCodes.Unbox_Any, Type);
                    data.CtorGen.Emit(OpCodes.Stfld, field);
                }
            }
        }

        #endregion
    }
}