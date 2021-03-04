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
using System.Linq;
using System.Reflection.Emit;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ModMaker.Lua.Runtime.LuaValues
{
    class LuaType : LuaValueBase
    {
        public LuaType(Type type)
        {
            this.Type = type;
        }

        public override LuaValueType ValueType { get { return LuaValueType.UserData; } }

        public Type Type { get; private set; }

        public override string ToString()
        {
            return Type.ToString();
        }

        public override void SetIndex(ILuaValue index, ILuaValue value)
        {
            Helpers.GetSetMember(Type, null, index, value);
        }
        public override ILuaValue GetIndex(ILuaValue index)
        {
            return Helpers.GetSetMember(Type, null, index);
        }

        public override ILuaMultiValue Invoke(ILuaValue self, bool memberCall, int overload, ILuaMultiValue args)
        {
            if (overload >= 0)
                throw new NotSupportedException(string.Format(Resources.CannotUseOverload, "LuaType"));
            if (args == null)
                args = new LuaMultiValue();

            Type t = Type;
            ConstructorInfo method;
            object ignored;
            if (Helpers.GetCompatibleMethod(
                t.GetConstructors()
                    .Where(c => c.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0)
                    .Select(c => Tuple.Create(c, (object)null)),
                args, out method, out ignored))
            {
                object value = method.Invoke(Helpers.ConvertForArgs(args, method));
                return LuaMultiValue.CreateMultiValueFromObj(value);
            }

            throw new InvalidOperationException(string.Format(Resources.CannotCall, "LuaType"));
        }

        public override bool Equals(ILuaValue other)
        {
            var temp = other as LuaType;
            return temp != null && temp.Type == Type;
        }

        public override ILuaValue Arithmetic(Parser.Items.BinaryOperationType type, ILuaValue other)
        {
            throw new ArgumentException(Resources.BadBinOp);
        }

        public override ILuaValue Arithmetic<T>(Parser.Items.BinaryOperationType type, LuaValues.LuaUserData<T> self)
        {
            throw new ArgumentException(Resources.BadBinOp);
        }
    }

    /// <summary>
    /// A class that was defined in Lua.  This is created in
    /// ILuaRuntime.DefineClass.  When this object is indexed in Lua,
    /// it will change the class that is defined.  When this is
    /// invoked, it creates a new instance.
    /// </summary>
    public sealed class LuaClass : LuaValueBase
    {
        Type _created;
        ILuaValue _ctor;
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
        /// Gets the value type of the value.
        /// </summary>
        public override LuaValueType ValueType { get { return LuaValueType.UserData; } }

        /// <summary>
        /// Gets the current environment.
        /// </summary>
        public ILuaEnvironment Environment { get { return _data.Env; } }
        /// <summary>
        /// Gets the simple name of the class.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Gets the base type of the class.
        /// </summary>
        public Type BaseType { get; private set; }
        /// <summary>
        /// Gets the interfaces that the class implements.
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

            InvokeConstructor(_data.CtorGen, _data.EnvField);

            _created = _data.TB.CreateType();
        }
        /// <summary>
        /// Creates an instance of the given type with the given arguments.  Calls CreateType if it has
        /// not been called before.
        /// </summary>
        /// <param name="args">Any arguments to pass to the constructor.</param>
        /// <returns>An instance of the type.</returns>
        public object CreateInstance(params ILuaValue[] args)
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
        /// <typeparam name="T">The base-type or interface to cast the object to.  The class must implement it.</typeparam>
        /// <returns>An instance of the type.</returns>
        public T CreateInstance<T>(params ILuaValue[] args)
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
        /// ILuaMultiValue ret = this.methodField.Invoke(this, false, -1, arguments);
        /// return E.Runtime.ConvertType(ret[0], returnType);
        /// </code>
        /// </summary>
        /// <param name="gen">The generator to inject code into.</param>
        /// <param name="returnType">The return type of the method.</param>
        /// <param name="methodField">The field (of type LuaValueType.Function) that contains the method to call.</param>
        /// <param name="arguments">The local variable (of type ILuaMultiValue) that contains the arguments to pass to the method.</param>
        /// <param name="E">The field that holds the environment.</param>
        static void CallFieldAndReturn(ILGenerator gen, Type returnType, FieldBuilder methodField, LocalBuilder arguments, FieldBuilder E)
        {
            //$PUSH this.{methodField}.Invoke(E.Runtime.CreateValue(this), true, -1, arguments);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, methodField);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, E);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateValue)));
            gen.Emit(OpCodes.Ldc_I4_1);
            gen.Emit(OpCodes.Ldc_I4_M1);
            gen.Emit(OpCodes.Ldloc, arguments);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod("Invoke"));

            // Convert and push result if the return type is not null.
            if (returnType != null && returnType != typeof(void))
            {
                // return $POP.As<{returnType}>();
                gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.As)).MakeGenericMethod(returnType));
            }
            else
            {
                gen.Emit(OpCodes.Pop);
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
        static void InvokeConstructor(ILGenerator ctorgen, FieldBuilder _E)
        {
            // call the Lua defined constructor method.

            // if(ctor == null) goto end;
            Label end = ctorgen.DefineLabel();
            ctorgen.Emit(OpCodes.Ldarg, 5);
            ctorgen.Emit(OpCodes.Brfalse, end);

            // ILuaValue target = E.Runtime.CreateValue(this);
            LocalBuilder target = ctorgen.DeclareLocal(typeof(ILuaValue));
            ctorgen.Emit(OpCodes.Ldarg_0);
            ctorgen.Emit(OpCodes.Ldfld, _E);
            ctorgen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
            ctorgen.Emit(OpCodes.Ldarg_0);
            ctorgen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(CreateValue)));
            ctorgen.Emit(OpCodes.Stloc, target);

            // ILuaMultiValue args = this.E.Runtime.CreateMultiValue(ctorArgs);
            LocalBuilder args = ctorgen.DeclareLocal(typeof(ILuaMultiValue));
            ctorgen.Emit(OpCodes.Ldarg_0);
            ctorgen.Emit(OpCodes.Ldfld, _E);
            ctorgen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
            ctorgen.Emit(OpCodes.Ldarg, 4);
            ctorgen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateMultiValue)));
            ctorgen.Emit(OpCodes.Stloc, args);

            // ctor.Invoke(target, true, -1, args);
            ctorgen.Emit(OpCodes.Ldarg, 5);
            ctorgen.Emit(OpCodes.Ldloc, target);
            ctorgen.Emit(OpCodes.Ldc_I4_1);
            ctorgen.Emit(OpCodes.Ldc_I4_M1);
            ctorgen.Emit(OpCodes.Ldloc, args);
            ctorgen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod("Invoke"));
            ctorgen.Emit(OpCodes.Pop);

            ctorgen.MarkLabel(end);

            ctorgen.Emit(OpCodes.Ret);
        }

        #region ILuaIndexer implementation

        class LuaClassItem : LuaValueBase
        {
            LuaClass parent;
            Type type;

            public LuaClassItem(LuaClass parent, Type type)
            {
                this.parent = parent;
                this.type = type;
            }

            public override LuaValueType ValueType
            {
                get { return LuaValueType.UserData; }
            }

            public override void SetIndex(ILuaValue index, ILuaValue value)
            {
                string name = index.GetValue() as string;
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

                // set the backing parent object.
                name = type.FullName + "." + name;
                if (members[0].MemberType == MemberTypes.Method)
                {
                    if (value.ValueType != LuaValueType.Function)
                        throw new InvalidOperationException(string.Format(Resources.MustBeFunction, name));

                    Item item = parent.CreateItem(name, new[] { members[overload == -1 ? 0 : overload] });
                    item.Assign(value);
                    parent._items.Add(item);
                }
                else if (members[0].MemberType == MemberTypes.Property)
                {
                    Item item = parent.CreateItem(name, members);
                    item.Assign(value);
                    parent._items.Add(item);
                }
            }

            public override bool Equals(ILuaValue other)
            {
                var temp = other as LuaClassItem;
                return temp != null && temp.parent == parent && temp.type == type;
            }

            public override ILuaValue Arithmetic(Parser.Items.BinaryOperationType type, ILuaValue other)
            {
                throw new ArgumentException(Resources.BadBinOp);
            }

            public override ILuaValue Arithmetic<T>(Parser.Items.BinaryOperationType type, LuaValues.LuaUserData<T> self)
            {
                throw new ArgumentException(Resources.BadBinOp);
            }
        }

        public override ILuaValue GetIndex(ILuaValue index)
        {
            string n = index.GetValue() as string;
            if (n == null)
                throw new InvalidOperationException(string.Format(Resources.BadIndexType, "class definition", "string"));

            foreach (var item in Interfaces)
            {
                if (item.Name == n)
                    return new LuaClassItem(this, item);
            }
            throw new InvalidOperationException(string.Format(Resources.DoesNotImplement, Name, n));
        }

        public override void SetIndex(ILuaValue index, ILuaValue value)
        {
            var name = index.GetValue() as string;
            if (_created != null || name == null)
                return;

            // If this is the constructor, assign it.
            if (name == "__ctor")
            {
                if (value.ValueType == LuaValueType.Function)
                    _ctor = value;
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
                        i = CreateItem(name, mems);
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

        public override ILuaMultiValue Invoke(ILuaValue self, bool memberCall, int overload, ILuaMultiValue args)
        {
            return new LuaMultiValue(new LuaValues.LuaUserData<object>(CreateInstance(args.ToArray())));
        }

        #region Item Classes

        /// <summary>
        /// Contains data used to generate an item.
        /// </summary>
        sealed class ItemData
        {
            public ItemData(ILuaEnvironment E, string name, Type baseType, Type[] _interfaces)
            {
                this.MethodArgs = new List<ILuaValue>();
                this.Methods = new List<MethodInfo>();
                this.Constants = new List<ILuaValue>();
                this.Names = new HashSet<string>();
                this.Env = E;
                this.FID = 0;

                // Create the type builder.
                var ab = AssemblyBuilder.DefineDynamicAssembly(
                    new AssemblyName("DynamicAssembly2"),
                    AssemblyBuilderAccess.Run);
                this.AB = ab;
                ModuleBuilder mb = ab.DefineDynamicModule("DynamicAssembly2.dll");
                this.TB = mb.DefineType(name, TypeAttributes.Class | TypeAttributes.BeforeFieldInit | TypeAttributes.Public, baseType, _interfaces);
                this.EnvField = TB.DefineField("$Env", typeof(ILuaEnvironment), FieldAttributes.Private);

                // public ctor(ILuaValue[] methods, ILuaValue[] initialValues, LuaEnvironment E, ILuaValue[] ctorArgs, ILuaValue ctor);
                {
                    var temp = new[] { typeof(ILuaValue[]), typeof(ILuaValue[]), typeof(ILuaEnvironment), typeof(ILuaValue[]), typeof(ILuaValue) };
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

            public List<ILuaValue> MethodArgs;
            public List<MethodInfo> Methods;
            public List<ILuaValue> Constants;
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
            /// <param name="value">The value to assign to.</param>
            public abstract void Assign(ILuaValue value);

            /// <summary>
            /// Adds a field to the type that contains the given method.  Adds
            /// code in the constructor to load the method.
            /// </summary>
            /// <param name="method">The method to store in the field.</param>
            /// <returns>The field that contains the method.</returns>
            protected FieldBuilder AddMethodArg(ItemData data, ILuaValue method)
            {
                // define a envField to hold a pointer to the method
                FieldBuilder field = data.TB.DefineField("<>_field_" + (data.FID++), typeof(ILuaValue), FieldAttributes.Private);

                // store the method in the input list and create code in the
                //   constructor to get the method from the argument and store
                //   it in the envField.
                data.MethodArgs.Add(method);
                // {envField} = arg_1[{input.Count - 1}];
                data.CtorGen.Emit(OpCodes.Ldarg_0);
                data.CtorGen.Emit(OpCodes.Ldarg_1);
                data.CtorGen.Emit(OpCodes.Ldc_I4, (data.MethodArgs.Count - 1));
                data.CtorGen.Emit(OpCodes.Ldelem, typeof(ILuaValue));
                data.CtorGen.Emit(OpCodes.Stfld, field);

                return field;
            }
        }
        /// <summary>
        /// A class item that is a method.
        /// </summary>
        sealed class MethodItem : Item
        {
            public ILuaValue Method;
            public MethodInfo BoundTo;

            public MethodItem(string name, MemberInfo member)
                : base(name)
            {
                this.Method = null;
                this.BoundTo = (MethodInfo)member;
            }

            public override void Assign(ILuaValue value)
            {
                if (value.ValueType != LuaValueType.Function)
                    throw new InvalidOperationException(string.Format(Resources.CannotHideMember, Name, BoundTo.DeclaringType));

                Method = value;
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

                // ILuaValue[] loc = new ILuaValue[{param.length}];
                LocalBuilder loc = gen.CreateArray(typeof(ILuaValue), param.Length);

                for (int ind = 0; ind < param.Length; ind++)
                {
                    // loc[{ind}] = E.Runtime.CreateValue(arg_{ind});
                    gen.Emit(OpCodes.Ldloc, loc);
                    gen.Emit(OpCodes.Ldc_I4, ind);
                    gen.Emit(OpCodes.Ldarg_0);
                    gen.Emit(OpCodes.Ldfld, data.EnvField);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
                    gen.Emit(OpCodes.Ldarg, ind + 1);
                    if (!param[ind].ParameterType.IsClass)
                        gen.Emit(OpCodes.Box, param[ind].ParameterType);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateValue)));
                    gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
                }

                // ILuaMultiValue args = E.Runtime.CreateMultiValue(loc);
                var args = gen.DeclareLocal(typeof(ILuaMultiValue));
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, data.EnvField);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
                gen.Emit(OpCodes.Ldloc, loc);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateMultiValue)));
                gen.Emit(OpCodes.Stloc, args);

                CallFieldAndReturn(gen, BoundTo.ReturnType, field, args, data.EnvField);

                // link our new method to the method it's bound to.
                data.TB.DefineMethodOverride(meth, BoundTo);
                data.Methods.Add(BoundTo);
            }
        }
        /// <summary>
        /// A class item that is a property.
        /// </summary>
        sealed class PropertyItem : Item
        {
            public ILuaValue Default;
            public Type Type;
            public ILuaValue Method, MethodSet;
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

            public override void Assign(ILuaValue value)
            {
                if (value.ValueType == LuaValueType.Table)
                {
                    // set the 'get' method for the item
                    var item = value.GetIndex(new LuaString("get"));
                    if (item != null && item != LuaNil.Nil)
                    {
                        MethodInfo m = _prop.GetGetMethod(true);
                        if (m == null || (!m.Attributes.HasFlag(MethodAttributes.Abstract) && !m.Attributes.HasFlag(MethodAttributes.Virtual)))
                            throw new InvalidOperationException(string.Format(Resources.CannotOverrideProperty, Name));
                        BoundTo = m;

                        if (item.ValueType != LuaValueType.Function)
                            throw new InvalidOperationException(Resources.PropTableFuncs);
                        Method = item;
                    }
                    // set the 'set' method for the item
                    item = value.GetIndex(new LuaString("set"));
                    if (item != null && item != LuaNil.Nil)
                    {
                        MethodInfo m = _prop.GetSetMethod(true);
                        if (m == null || (!m.Attributes.HasFlag(MethodAttributes.Abstract) && !m.Attributes.HasFlag(MethodAttributes.Virtual)))
                            throw new InvalidOperationException(string.Format(Resources.CannotOverrideProperty, Name));
                        BoundSet = m;

                        if (item.ValueType != LuaValueType.Function)
                            throw new InvalidOperationException(Resources.PropTableFuncs);
                        MethodSet = item;
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
                            // store the constant passed in the constructor in the envField.
                            data.Constants.Add(Default);
                            // this.{field} = arg_2[{_input2.Count - 1}].As<Type>();
                            data.CtorGen.Emit(OpCodes.Ldarg_0);
                            data.CtorGen.Emit(OpCodes.Ldarg_2);
                            data.CtorGen.Emit(OpCodes.Ldc_I4, (data.Constants.Count - 1));
                            data.CtorGen.Emit(OpCodes.Ldelem, typeof(ILuaValue));
                            data.CtorGen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.As)).MakeGenericMethod(Type));
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

                        // ILuaMultiValue loc = E.Runtime.CreateMultiValue(new ILuaValue[0]);
                        LocalBuilder loc = gen.DeclareLocal(typeof(ILuaMultiValue));
                        gen.Emit(OpCodes.Ldarg_0);
                        gen.Emit(OpCodes.Ldfld, data.EnvField);
                        gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
                        gen.Emit(OpCodes.Ldc_I4, 0);
                        gen.Emit(OpCodes.Newarr, typeof(ILuaValue));
                        gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateMultiValue)));
                        gen.Emit(OpCodes.Stloc, loc);

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

                        // loc[0] = (object)arg_1
                        gen.Emit(OpCodes.Ldloc, loc);
                        gen.Emit(OpCodes.Ldc_I4_0);
                        gen.Emit(OpCodes.Ldarg_1);
                        if (Type.IsValueType)
                            gen.Emit(OpCodes.Box, Type);
                        gen.Emit(OpCodes.Stelem, typeof(object));

                        // ILuaMultiValue args = E.Runtime.CreateMultiValueFromObj(loc);
                        LocalBuilder args = gen.DeclareLocal(typeof(ILuaMultiValue));
                        gen.Emit(OpCodes.Ldarg_0);
                        gen.Emit(OpCodes.Ldfld, data.EnvField);
                        gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
                        gen.Emit(OpCodes.Ldloc, loc);
                        gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateMultiValue)));
                        gen.Emit(OpCodes.Stloc, args);

                        CallFieldAndReturn(gen, null, field, args, data.EnvField);
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
            public ILuaValue Default;

            public FieldItem(string name)
                : base(name)
            {
                this.Type = null;
                this.Default = null;
            }

            public override void Assign(ILuaValue value)
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

                    if (Type == null)
                    {
                        object temp = value == null ? value : value.GetValue();
                        Type = temp == null ? typeof(object) : temp.GetType();
                    }
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
                    // get the initial value from the constructor and set it.
                    data.Constants.Add(Default);
                    // {envField} = arg_2[{_input2.Count - 1}].As<Type>();
                    data.CtorGen.Emit(OpCodes.Ldarg_0);
                    data.CtorGen.Emit(OpCodes.Ldarg_2);
                    data.CtorGen.Emit(OpCodes.Ldc_I4, (data.Constants.Count - 1));
                    data.CtorGen.Emit(OpCodes.Ldelem, typeof(ILuaValue));
                    data.CtorGen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.As)).MakeGenericMethod(Type));
                    data.CtorGen.Emit(OpCodes.Stfld, field);
                }
            }
        }

        #endregion

        public override bool Equals(ILuaValue other)
        {
            return object.ReferenceEquals(this, other);
        }

        public override ILuaValue Arithmetic(Parser.Items.BinaryOperationType type, ILuaValue other)
        {
            throw new ArgumentException(Resources.BadBinOp);
        }

        public override ILuaValue Arithmetic<T>(Parser.Items.BinaryOperationType type, LuaValues.LuaUserData<T> self)
        {
            throw new ArgumentException(Resources.BadBinOp);
        }
    }
}
