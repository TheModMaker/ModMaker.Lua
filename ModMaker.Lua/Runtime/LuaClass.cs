using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.Collections.ObjectModel;
using System.Reflection;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// A class that was defined in Lua.
    /// </summary>
    public sealed class LuaClass
    {
        Type _created;
        Type _base;
        Type[] _interfaces;
        List<Item> _items;
        List<LuaMethod> _input;
        List<object> _input2;
        LuaMethod _ctor;
        LuaEnvironment E;

        class Item
        {
            public Item(int type)
            {
                this.ItemType = type;
            }

            public int ItemType; // 1 - field, 2 - property, 3 - method.
            public string Name;

            // field or property
            public Type Type;
            public object Default;

            // method
            public MethodInfo BoundTo, BoundSet;
            public LuaMethod Method, MethodSet;
        }

        internal LuaClass(string name, Type _base, Type[] interfaces, LuaEnvironment E)
        {
            this._items = new List<Item>();
            this._input = new List<LuaMethod>();
            this._input2 = new List<object>();
            this._base = _base;
            this._interfaces = interfaces.SelectMany(t => t.GetInterfaces()).Union(interfaces).ToArray();
            this.Name = name;
            this._created = null;
            this.E = E;
        }

        /// <summary>
        /// Gets the name of the class.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Gets the base type of the class, or null if none.
        /// </summary>
        public Type BaseType { get { return _base; } }
        /// <summary>
        /// Gets the interfaces that the class impliments.
        /// </summary>
        public ReadOnlyCollection<Type> Interfaces { get { return new ReadOnlyCollection<Type>(_interfaces); } }

        /// <summary>
        /// Completes the type making it ready to create instances.  If Lua attempts to change
        /// this object after this has been called, an exception will be thrown.  This method
        /// is called when you call CreateInstance.
        /// </summary>
        public void CreateType()
        {
            if (_created != null)
                return;

            AssemblyBuilder _ab = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("DynamicAssembly") { KeyPair = new StrongNameKeyPair(Resources.Dynamic) },
                AssemblyBuilderAccess.RunAndSave);
            ModuleBuilder _mb = _ab.DefineDynamicModule("Name.dll");
            TypeBuilder tb = _mb.DefineType(Name, TypeAttributes.Class | TypeAttributes.BeforeFieldInit | TypeAttributes.Public, _base, _interfaces);
            List<MethodInfo> cache = new List<MethodInfo>();
            List<string> methods = new List<string>();
            int i = 1, fid = 1;

            #region Start Ctor
            var temp = _ctor == null ? new[] { typeof(LuaMethod[]), typeof(object[] ) } : 
                new[] { typeof(LuaMethod[]), typeof(object[] ), typeof(object[]), typeof(LuaEnvironment), typeof(LuaMethod) };
            ConstructorBuilder ctor = tb.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig,
                CallingConventions.Standard,
                temp);
            ILGenerator ctorgen = ctor.GetILGenerator();
            #endregion
            #region Handle Items
            foreach (var item in _items)
            {
                // if item is Method
                if (item.ItemType == 3)
                {
                    FieldBuilder field = tb.DefineField("<>_field_" + (fid++), typeof(LuaMethod), FieldAttributes.Private);

                    _input.Add(item.Method);
                    // {field} = arg_0[{_input.Count - 1}];
                    ctorgen.Emit(OpCodes.Ldarg_0);
                    ctorgen.Emit(OpCodes.Ldarg_1);
                    ctorgen.Emit(OpCodes.Ldc_I4, (_input.Count - 1));
                    ctorgen.Emit(OpCodes.Ldelem, typeof(LuaMethod));
                    ctorgen.Emit(OpCodes.Stfld, field);

                    string name = item.Name ?? "<>_method_" + (i++);
                    methods.Add(name);
                    var param = item.BoundTo.GetParameters().Select(p => p.ParameterType).ToArray();
                    var meth = tb.DefineMethod(
                        name, 
                        (item.Name == null ? MethodAttributes.Private : MethodAttributes.Public) | MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.Virtual, 
                        CallingConventions.Standard, 
                        item.BoundTo.ReturnType, 
                        param);
                    ILGenerator gen = meth.GetILGenerator();
                    LocalBuilder loc = gen.DeclareLocal(typeof(List<object>));
                    // loc = new List<object>();
                    gen.Emit(OpCodes.Newobj, typeof(List<object>).GetConstructor(new Type[0]));
                    gen.Emit(OpCodes.Stloc, loc);

                    for (int ind = 0; ind < param.Length; ind++)
                    {
                        // loc.Add(arg_{ind});
                        gen.Emit(OpCodes.Ldloc, loc);
                        gen.Emit(OpCodes.Ldarg, ind);
                        gen.Emit(OpCodes.Callvirt, typeof(List<object>).GetMethod("Add"));
                    }

                    // RuntimeHelper.InvokeClass(loc, this.{field}, this, {item.BoundTo.ReturnType});
                    gen.Emit(OpCodes.Ldloc, loc);
                    gen.Emit(OpCodes.Ldarg_0);
                    gen.Emit(OpCodes.Ldfld, field);
                    gen.Emit(OpCodes.Ldarg_0);
                    gen.Emit(OpCodes.Ldtoken, item.BoundTo.ReturnType);
                    gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("InvokeClass"));
                    if (item.BoundTo.ReturnType == null || item.BoundTo.ReturnType == typeof(void))
                        gen.Emit(OpCodes.Pop);
                    else
                        gen.Emit(OpCodes.Unbox_Any, item.BoundTo.ReturnType);
                    gen.Emit(OpCodes.Ret);

                    tb.DefineMethodOverride(meth, item.BoundTo);
                    cache.Add(item.BoundTo);
                }
                // else if item is Property
                else if (item.ItemType == 2)
                {
                    if (item.BoundTo != null)
                    {
                        MethodBuilder m = tb.DefineMethod("get_" + item.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig, item.Type, null);
                        ILGenerator gen = m.GetILGenerator();
                        if (item.Method == null)
                        {
                            string name = "<" + item.Name + ">_backing";
                            FieldBuilder field = tb.DefineField(name, item.Type, FieldAttributes.Private);
                            if (item.Default != null)
                            {
                                MethodInfo meth;
                                if (!RuntimeHelper.TypesCompatible(item.Type, item.Default.GetType(), out meth))
                                {
                                    if (meth != null)
                                        item.Default = meth.Invoke(null, new[] { item.Default });
                                    else
                                        throw new InvalidCastException("Cannot cast an object of type '" + item.Default.GetType() + "' to type '" + item.Type + "'.");
                                }

                                _input2.Add(item.Default);
                                // {field} = arg_2[{_input2.Count - 1}];
                                ctorgen.Emit(OpCodes.Ldarg_0);
                                ctorgen.Emit(OpCodes.Ldarg_2);
                                ctorgen.Emit(OpCodes.Ldc_I4, (_input2.Count - 1));
                                ctorgen.Emit(OpCodes.Ldelem, typeof(object));
                                ctorgen.Emit(OpCodes.Unbox_Any, item.Type);
                                ctorgen.Emit(OpCodes.Stfld, field);
                            }

                            // return this.{field};
                            gen.Emit(OpCodes.Ldarg_0);
                            gen.Emit(OpCodes.Ldfld, field);
                            gen.Emit(OpCodes.Ret);
                        }
                        else
                        {
                            FieldBuilder field = tb.DefineField("<>_field_" + (fid++), typeof(LuaMethod), FieldAttributes.Private);

                            _input.Add(item.Method);
                            // {field} = arg_1[{_input.Count - 1}];
                            ctorgen.Emit(OpCodes.Ldarg_0);
                            ctorgen.Emit(OpCodes.Ldarg_1);
                            ctorgen.Emit(OpCodes.Ldc_I4, (_input.Count - 1));
                            ctorgen.Emit(OpCodes.Ldelem, typeof(LuaMethod));
                            ctorgen.Emit(OpCodes.Stfld, field);

                            LocalBuilder loc = gen.DeclareLocal(typeof(List<object>));
                            // loc = new List<object>();
                            gen.Emit(OpCodes.Newobj, typeof(List<object>).GetConstructor(new Type[0]));
                            gen.Emit(OpCodes.Stloc, loc);

                            // return RuntimeHelper.InvokeClass(loc, this.{field}, this, {item.BoundTo.ReturnType});
                            gen.Emit(OpCodes.Ldloc, loc);
                            gen.Emit(OpCodes.Ldarg_0);
                            gen.Emit(OpCodes.Ldfld, field);
                            gen.Emit(OpCodes.Ldarg_0);
                            gen.Emit(OpCodes.Ldtoken, item.BoundTo.ReturnType);
                            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("InvokeClass"));
                            gen.Emit(OpCodes.Unbox_Any, item.BoundTo.ReturnType);
                            gen.Emit(OpCodes.Ret);
                        }
                        tb.DefineMethodOverride(m, item.BoundTo);
                        cache.Add(item.BoundTo);
                    }

                    if (item.BoundSet != null)
                    {
                        MethodBuilder m = tb.DefineMethod("set_" + item.Name, 
                            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig, 
                            null, new[] { item.Type });
                        ILGenerator gen = m.GetILGenerator();

                        if (item.MethodSet == null)
                        {
                            // throw new NotSupportedException("Cannot write to a constant property.");
                            gen.Emit(OpCodes.Ldstr, "Cannot write to a constant property.");
                            gen.Emit(OpCodes.Newobj, typeof(NotSupportedException).GetConstructor(new[] { typeof(string) }));
                            gen.Emit(OpCodes.Throw);
                        }
                        else
                        {
                            FieldBuilder field = tb.DefineField("<>_field_" + (fid++), typeof(LuaMethod), FieldAttributes.Private);

                            _input.Add(item.MethodSet);
                            // {field} = arg_1[{_input.Count - 1}];
                            ctorgen.Emit(OpCodes.Ldarg_0);
                            ctorgen.Emit(OpCodes.Ldarg_1);
                            ctorgen.Emit(OpCodes.Ldc_I4, (_input.Count - 1));
                            ctorgen.Emit(OpCodes.Ldelem, typeof(LuaMethod));
                            ctorgen.Emit(OpCodes.Stfld, field);

                            LocalBuilder loc = gen.DeclareLocal(typeof(List<object>));
                            // loc = new List<object>();
                            gen.Emit(OpCodes.Newobj, typeof(List<object>).GetConstructor(new Type[0]));
                            gen.Emit(OpCodes.Stloc, loc);

                            // loc.Add((object)arg_1);
                            gen.Emit(OpCodes.Ldloc, loc);
                            gen.Emit(OpCodes.Ldarg_1);
                            if (item.Type.IsValueType)
                                gen.Emit(OpCodes.Box, item.Type);
                            gen.Emit(OpCodes.Callvirt, typeof(List<object>).GetMethod("Add"));

                            // RuntimeHelper.InvokeClass(loc, this.{field}, this, {item.BoundSet.ReturnType});
                            gen.Emit(OpCodes.Ldloc, loc);
                            gen.Emit(OpCodes.Ldarg_0);
                            gen.Emit(OpCodes.Ldfld, field);
                            gen.Emit(OpCodes.Ldarg_0);
                            gen.Emit(OpCodes.Ldtoken, item.BoundSet.ReturnType);
                            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("InvokeClass"));
                            gen.Emit(OpCodes.Pop);
                            gen.Emit(OpCodes.Ret);
                        }

                        tb.DefineMethodOverride(m, item.BoundSet);
                        cache.Add(item.BoundSet);
                    }
                }
                // else if item is Field
                else if (item.Type != null)
                {
                    string name = item.Name ?? "<>_field_" + (fid++);
                    FieldBuilder field = tb.DefineField(name, item.Type, FieldAttributes.Public);
                    if (item.Default != null)
                    {
                        MethodInfo meth;
                        if (!RuntimeHelper.TypesCompatible(item.Type, item.Default.GetType(), out meth))
                        {
                            if (meth != null)
                                item.Default = meth.Invoke(null, new[] { item.Default });
                            else
                                throw new InvalidCastException("Cannot cast an object of type '" + item.Default.GetType() + "' to type '" + item.Type + "'.");
                        }

                        _input2.Add(item.Default);
                        // {field} = ({item.Type})arg_2[{_input2.Count - 1}];
                        ctorgen.Emit(OpCodes.Ldarg_0);
                        ctorgen.Emit(OpCodes.Ldarg_2);
                        ctorgen.Emit(OpCodes.Ldc_I4, (_input2.Count - 1));
                        ctorgen.Emit(OpCodes.Ldelem, typeof(object));
                        ctorgen.Emit(OpCodes.Unbox_Any, item.Type);
                        ctorgen.Emit(OpCodes.Stfld, field);
                    }
                }
            }
            #endregion
            #region Handle Interfaces
            foreach (var item in _interfaces)
            {
                foreach (var m in item.GetMethods())
                {
                    if (cache.Contains(m))
                        continue;

                    string name = "<" + item.FullName + ">_" + m.Name;
                    if (methods.Contains(name))
                    {
                        int j = 1;
                        while (methods.Contains(name + "`" + j))
                        {
                            j++;
                        }
                        name += "`" + j;
                    }
                    methods.Add(name);
                    var meth = tb.DefineMethod(
                        name,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.Virtual, 
                        CallingConventions.Standard, 
                        m.ReturnType, 
                        m.GetParameters().Select(p => p.ParameterType).ToArray());
                    ILGenerator gen = meth.GetILGenerator();
                    gen.ThrowException(typeof(NotImplementedException));

                    tb.DefineMethodOverride(meth, m);
                }
            }
            #endregion
            #region Handle Base Class
            if (_base != null)
            {
                // call base ctor
                ctorgen.Emit(OpCodes.Ldarg_0);
                ctorgen.Emit(OpCodes.Call, _base.GetConstructor(new Type[0]));

                // impliment all abstract members
                foreach (var item in _base.GetMethods().Where(m => m.Attributes.HasFlag(MethodAttributes.Abstract)))
                {
                    if (cache.Contains(item))
                        continue;

                    string name = "<>_method_" + (i++);
                    methods.Add(name);
                    var meth = tb.DefineMethod(
                        name,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.Virtual,
                        CallingConventions.Standard,
                        item.ReturnType,
                        item.GetParameters().Select(p => p.ParameterType).ToArray());
                    ILGenerator gen = meth.GetILGenerator();
                    gen.ThrowException(typeof(NotImplementedException));

                    tb.DefineMethodOverride(meth, item);
                }
            }
            #endregion
            #region End Constructor
            {
                if (_ctor != null)
                {
                    // RuntimeHelper.Invoke(arg_4, arg_5, arg_3);
                    ctorgen.Emit(OpCodes.Ldarg, 4);
                    ctorgen.Emit(OpCodes.Ldarg, 5);
                    ctorgen.Emit(OpCodes.Ldarg_3);
                    ctorgen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("Invoke"));
                    ctorgen.Emit(OpCodes.Pop);
                }
                ctorgen.Emit(OpCodes.Ret);
            }
            #endregion

            _created = tb.CreateType();
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

            if (_ctor == null)
                return Activator.CreateInstance(_created, new object[] { _input.ToArray(), _input2.ToArray() }, null);
            else
                return Activator.CreateInstance(_created, new object[] { _input.ToArray(), _input2.ToArray(), args, E, _ctor }, null);
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

            bool found = false;
            foreach (var item in _interfaces)
            {
                if (item.IsAssignableFrom(typeof(T)))
                    found = true;
            }
            if (!found && _base != null && _base.IsAssignableFrom(typeof(T)))
                found = true;

            if (!found)
                throw new ArgumentException("The current class does not derrive from type '" + typeof(T) + "'.");

            if (_ctor == null)
                return (T)Activator.CreateInstance(_created, new object[] { _input.ToArray(), _input2.ToArray() }, null);
            else
                return (T)Activator.CreateInstance(_created, new object[] { _input.ToArray(), _input2.ToArray(), args, E, _ctor }, null);
        }

        internal LuaClassItem GetItem(object name)
        {
            name = RuntimeHelper.GetValue(name);
            string n = name as string;
            if (n == null)
                throw new InvalidOperationException("Indexer for a 'class definition' must be a string.");

            foreach (var item in _interfaces)
            {
                if (item.Name == n)
                    return new LuaClassItem(this, item);
            }
            throw new InvalidOperationException("Class '" + Name + "' does not implement an interface with the name '" + n + "'.");
        }
        internal void SetItem(object n, object value)
        {
            if (_created != null)
                return;

            if (n is string)
            {
                string name = n as string;
                Item i = _items.Where(ii => ii.Name == name).FirstOrDefault();

                if (name == "__init")
                {
                    if (value is LuaMethod)
                        _ctor = value as LuaMethod;
                    else
                        throw new InvalidOperationException("The constructor can only be a function.");

                    return;
                }

                if (i == null)
                {
                    if (_base != null)
                    {
                        var v = _base.GetMember(name);
                        if (v != null && v.Length > 0)
                        {
                            SetItemHelper(value, name, v);
                            return;
                        }
                    }
                    MemberInfo[] mem = null;
                    foreach (var item in _interfaces)
                    {
                        var temp = item.GetMember(name);
                        if (temp != null && temp.Length > 0)
                        {
                            if (mem != null)
                            {
                                mem = null;
                                break;
                            }
                            mem = temp;
                        }
                    }
                    if (mem != null && mem.Length > 0)
                    {
                        SetItemHelper(value, name, mem);
                        return;
                    }

                    i = new Item(1);
                    i.Name = name;
                    _items.Add(i);
                }

                if (value is LuaType)
                {
                    if (i.ItemType == 3)
                        throw new InvalidOperationException("'" + name + "' is already defined as a Method.");

                    if (i.Type == null)
                        i.Type = (value as LuaType).Type;
                    else
                        throw new InvalidOperationException("The member '" + name + "' has already a been assigned a type.");
                }
                else if (value is LuaMethod)
                {
                    if (i == null)
                    {
                        i = new Item(3);
                        i.Name = name;
                        _items.Add(i);
                    }

                }
                else
                {
                    if (i.ItemType == 3)
                        throw new InvalidOperationException("'" + name + "' is already defined as a Method.");

                    if (i.Default != null)
                        throw new InvalidOperationException("The member '" + name + "' has already been assigned a default value.");

                    i.Type = i.Type ?? (value == null ? typeof(object) : value.GetType());
                    i.Default = value;
                }
            }
        }

        internal void SetItemHelper(object value, string name, MemberInfo[] v)
        {
            Item i = null;
            if (value is LuaType)
                throw new InvalidOperationException("Cannot hide member '" + name + "' defined in base class '" + _base + "'.");

            if (v[0].MemberType == MemberTypes.Property)
            {
                if (value is LuaTable)
                {
                    i = new Item(2);
                    PropertyInfo p = v[0] as PropertyInfo;
                    LuaTable table = value as LuaTable;
                    i.Name = name;
                    i.Type = p.PropertyType;
                    foreach (var item in table)
                    {
                        if (item.Key as string == "get")
                        {
                            MethodInfo m = p.GetGetMethod(true);
                            if (m == null || (!m.Attributes.HasFlag(MethodAttributes.Abstract) && !m.Attributes.HasFlag(MethodAttributes.Virtual)))
                                throw new InvalidOperationException("Cannot override get property '" + name + "' because it is not virtual.");
                            i.BoundTo = m;

                            if (!(item.Value is LuaMethod))
                                throw new InvalidOperationException("The values in a property table must be functions.");
                            i.Method = item.Value as LuaMethod;
                        }
                        else if (item.Key as string == "set")
                        {
                            MethodInfo m = p.GetSetMethod(true);
                            if (m == null || (!m.Attributes.HasFlag(MethodAttributes.Abstract) && !m.Attributes.HasFlag(MethodAttributes.Virtual)))
                                throw new InvalidOperationException("Cannot override set property '" + name + "' because it is not virtual.");
                            i.BoundSet = m;

                            if (!(item.Value is LuaMethod))
                                throw new InvalidOperationException("The values in a property table must be functions.");
                            i.MethodSet = item.Value as LuaMethod;
                        }
                        else
                            throw new InvalidOperationException("A property table can only contain a 'get' and/or a 'set' item.");
                    }

                    _items.Add(i);
                    return;
                }
                else
                {
                    i = new Item(2);
                    PropertyInfo p = v[0] as PropertyInfo;
                    MethodInfo m = p.GetGetMethod(true);
                    if (m == null || (!m.Attributes.HasFlag(MethodAttributes.Abstract) && !m.Attributes.HasFlag(MethodAttributes.Virtual)))
                        throw new InvalidOperationException("Cannot override property '" + name + "' because it is not virtual.");
                    i.BoundTo = m;

                    m = p.GetSetMethod(true);
                    if (m != null && m.Attributes.HasFlag(MethodAttributes.Abstract))
                        i.BoundSet = m;
                    i.Name = name;
                    i.Type = (v[0] as PropertyInfo).PropertyType;
                    i.Default = value;
                    _items.Add(i);
                    return;
                }
            }
            else if (v[0].MemberType == MemberTypes.Method)
            {
                if (v.Length > 1)
                    throw new AmbiguousMatchException("The type '" + _base + "' defines more than one method with the name '" + name + "'.");
                if (!(value is LuaMethod))
                    throw new InvalidOperationException("Cannot hide member '" + name + "' defined in base class '" + _base + "'.");

                i = new Item(3);
                i.Name = name;
                i.BoundTo = v[0] as MethodInfo;
                i.Method = value as LuaMethod;
                _items.Add(i);
                return;
            }
            else
                throw new InvalidOperationException("Cannot hide member '" + name + "' defined in base class '" + _base + "'.");
        }
        internal void SetItem(MethodInfo meth, LuaMethod value)
        {
            if (_created != null)
                return;

            var s = _items.Where(i => i.BoundTo == meth).FirstOrDefault();
            if (s != null)
            {
                s.Method = value;
            }
            else
            {
                _items.Add(new Item(3) { BoundTo = meth, Method = value });
            }
        }
    }

    class LuaClassItem
    {
        LuaClass parrent;
        Type type;

        public LuaClassItem(LuaClass parrent, Type type)
        {
            this.parrent = parrent;
            this.type = type;
        }

        public void SetItem(object name, object value)
        {
            name = RuntimeHelper.GetValue(name);
            value = RuntimeHelper.GetValue(value);
            string n = name as string;
            int over = -1;
            if (n == null)
                throw new InvalidOperationException("Index of a 'class definition' must be a string.");
            if (n.Contains('`'))
            {
                if (!int.TryParse(n.Substring(n.IndexOf('`') + 1), out over))
                    throw new InvalidOperationException("Only numbers are allowed after the grave(`) when specifying an overload.");

                n = n.Substring(0, n.IndexOf('`'));
            }

            var temp = type.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == n).ToArray();
            if (temp == null || temp.Length == 0)
                throw new InvalidOperationException("The type '" + type + "' does not contain a definition of member '" + n + "'.");

            if (temp.Length > 1 && over == -1)
                throw new AmbiguousMatchException("The type '" + type + "' defines more than one member named '" + n + "'.");
            if (over != -1 && over >= temp.Length)
                throw new InvalidOperationException("The specified overload is greater than the number defined.");

            if (temp[0].MemberType == MemberTypes.Method)
            {
                if (!(value is LuaMethod))
                    throw new InvalidOperationException("The member '" + n + "' is defined as a method and must be set to a Lua function.");

                parrent.SetItem(temp[over == -1 ? 0 : over] as MethodInfo, value as LuaMethod);
            }
            else if (temp[0].MemberType == MemberTypes.Property)
            {
                parrent.SetItemHelper(value, n, temp);
            }
        }
    }
}