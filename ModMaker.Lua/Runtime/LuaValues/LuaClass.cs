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
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ModMaker.Lua.Compiler;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime.LuaValues {
  class LuaType : LuaValueBase {
    public LuaType(Type type) {
      Type = type;
    }

    public override LuaValueType ValueType { get { return LuaValueType.UserData; } }

    public Type Type { get; private set; }

    public override object? GetValue() {
      return Type;
    }
    public override string ToString() {
      return Type.ToString();
    }

    public override void SetIndex(ILuaValue index, ILuaValue value) {
      Helpers.GetSetMember(Type, null, index, value);
    }
    public override ILuaValue GetIndex(ILuaValue index) {
      return Helpers.GetSetMember(Type, null, index);
    }

    public override LuaMultiValue Invoke(LuaMultiValue args) {
      ConstructorInfo[] ctors = Type.GetConstructors()
          .Where(c => c.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0)
          .ToArray();
      var choices = ctors.Select(c => new OverloadSelector.Choice(c)).ToArray();
      int index = OverloadSelector.FindOverload(choices, args);
      if (index < 0) {
        throw new InvalidOperationException(string.Format(Resources.CannotCall, "LuaType"));
      }
      object value = ctors[index].Invoke(OverloadSelector.ConvertArguments(args, choices[index]));
      return LuaMultiValue.CreateMultiValueFromObj(value);
    }

    public override bool Equals(ILuaValue? other) {
      return other is LuaType temp && temp.Type == Type;
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
      throw new ArgumentException(Resources.BadBinOp);
    }

    public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self) {
      throw new ArgumentException(Resources.BadBinOp);
    }
  }

  /// <summary>
  /// A class that was defined in Lua.  When this object is indexed in Lua, it will change the class
  /// that is defined.  When this is invoked, it creates a new instance.
  /// </summary>
  public sealed class LuaClass : LuaValueBase {
    /// <summary>
    /// The name of the Lua function that will be invoked for the constructor.
    /// </summary>
    public const string ConstructorName = "__ctor";

    Type? _created;
    ILuaValue? _ctor;
    readonly ItemData _data;
    readonly List<Item> _items;

    /// <summary>
    /// Creates a new instance of LuaClass.
    /// </summary>
    /// <param name="name">The simple name of the class.</param>
    /// <param name="base">The base class of the class; or null.</param>
    /// <param name="interfaces">The interfaces that are inherited; or null.</param>
    LuaClass(string name, Type? @base, Type[] interfaces) {
      Name = name;
      BaseType = @base;
      _items = new List<Item>();

      var inter = interfaces.SelectMany(t => t.GetInterfaces()).Union(interfaces).ToArray();
      Interfaces = new ReadOnlyCollection<Type>(inter);

      _data = new ItemData(name, @base, inter);
    }

    /// <summary>
    /// Creates a new LuaClass instance from the given Lua values.
    /// </summary>
    /// <param name="name">The name of the resulting type.</param>
    /// <param name="types">The type instances to derive from or implement.</param>
    /// <returns>A new LuaClass instance.</returns>
    public static LuaClass Create(string name, ILuaValue[] values) {
      var interfaces = new List<Type>();
      Type? @base = null;

      foreach (var item in values) {
        var type = item.GetValue() as Type;
        if (type == null) {
          throw new InvalidOperationException("A type can only derive from a Type");
        }
        if (type.IsInterface) {
          interfaces.Add(type);
        } else {
          // Allow non-public in the flags since we want protected too, but fail if the constructor
          // is private or internal ("Assembly").
          const BindingFlags flags =
              BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
          var ctor = type.GetConstructor(flags, null, Array.Empty<Type>(), null);
          if (ctor == null || ctor.GetCustomAttribute<LuaIgnoreAttribute>() != null ||
              ctor.IsPrivate || ctor.IsAssembly) {
            throw new InvalidOperationException(
                "Base classes must have a visible public empty constructor");
          }

          if (@base == null)
            @base = type;
          else
            throw new InvalidOperationException("A type can only have one concrete base class");
        }
      }
      return new LuaClass(name, @base, interfaces.ToArray());
    }

    public override LuaValueType ValueType { get { return LuaValueType.UserData; } }

    /// <summary>
    /// Gets the simple name of the class.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Gets the base type of the class.
    /// </summary>
    public Type? BaseType { get; }
    /// <summary>
    /// Gets the interfaces that the class implements.
    /// </summary>
    public ReadOnlyCollection<Type> Interfaces { get; }

    /// <summary>
    /// Completes the type making it ready to create instances.  If Lua attempts to change
    /// this object after this has been called, an exception will be thrown.  This method
    /// is called when you call CreateInstance.
    /// </summary>
    public void CreateType() {
      if (_created != null) {
        return;
      }

      // Generate for the items.
      _items.ForEach(i => i.Generate(_data));

      // Generate for the interfaces.
      foreach (var item in Interfaces) {
        foreach (var method in item.GetMethods()) {
          //  e.g. <System.IDisposable>_Dispose
          string name = "<" + item.FullName + ">_" + method.Name;
          _stubImplement(_data, method, name);
        }
      }

      // Generate for the base class
      if (BaseType != null) {
        static bool isAbstract(MethodInfo m) =>
            (m.Attributes & MethodAttributes.Abstract) == MethodAttributes.Abstract;
        foreach (var method in BaseType.GetMethods().Where(isAbstract)) {
          //  e.g. <>_Dispose
          string name = "<>_" + method.Name;
          _stubImplement(_data, method, name);
        }
      }

      _invokeConstructor(_data.CtorGen);

      _created = _data.TB.CreateType();
    }
    /// <summary>
    /// Creates an instance of the given type with the given arguments.  Calls CreateType if it has
    /// not been called before.
    /// </summary>
    /// <param name="args">Any arguments to pass to the constructor.</param>
    /// <returns>An instance of the type.</returns>
    public object CreateInstance(params ILuaValue[] args) {
      CreateType();
      var instArgs =
          new object?[] {_data.MethodArgs.ToArray(), _data.Constants.ToArray(), args, _ctor};
      return Activator.CreateInstance(_created!, instArgs, Array.Empty<object>())!;
    }
    /// <summary>
    /// Creates an instance of the given type with the given arguments.  Calls CreateType if it has
    /// not been called before.
    /// </summary>
    /// <param name="args">Any arguments to pass to the constructor.</param>
    /// <typeparam name="T">
    /// The base-type or interface to cast the object to.  The class must implement it.
    /// </typeparam>
    /// <returns>An instance of the type.</returns>
    public T CreateInstance<T>(params ILuaValue[] args) {
      CreateType();
      if (!typeof(T).IsAssignableFrom(_created)) {
        throw new ArgumentException(string.Format(Resources.CurrentDoesNotDerive, typeof(T)));
      }

      return (T)CreateInstance(args);
    }

    /// <summary>
    /// Injects the code necessary to return the first value from a call to a method that
    /// is stored in a field of a type.  Creates one local variable.
    /// Injects:
    ///
    /// <code>
    /// ILuaMultiValue ret = this.methodField.Invoke(this, false, arguments);
    /// return E.Runtime.ConvertType(ret[0], returnType);
    /// </code>
    /// </summary>
    /// <param name="gen">The generator to inject code into.</param>
    /// <param name="returnType">The return type of the method.</param>
    /// <param name="methodField">
    /// The field (of type LuaValueType.Function) that contains the method to call.
    /// </param>
    /// <param name="arguments">
    /// The local variable (of type ILuaMultiValue) that contains the arguments to pass to the
    /// method.
    /// </param>
    static void _callFieldAndReturn(ILGenerator gen, Type returnType, FieldBuilder methodField,
                                    LocalBuilder arguments) {
      //$PUSH this.{methodField}.Invoke(arguments);
      gen.Emit(OpCodes.Ldarg_0);
      gen.Emit(OpCodes.Ldfld, methodField);
      gen.Emit(OpCodes.Ldloc, arguments);
      gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.Invoke);

      // Convert and push result if the return type is not null.
      if (returnType != typeof(void)) {
        // return $POP.As<{returnType}>();
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.As.MakeGenericMethod(returnType));
      } else {
        gen.Emit(OpCodes.Pop);
      }
      gen.Emit(OpCodes.Ret);
    }
    /// <summary>
    /// Creates a new method that implements the given method that throws a NotImplementedException.
    /// </summary>
    /// <param name="data">The current data.</param>
    /// <param name="method">The method to implement.</param>
    static void _stubImplement(ItemData data, MethodInfo method, string name) {
      // We already created this method
      if (data.Methods.Contains(method)) {
        return;
      }

      // define a no-conflict name for this method
      if (data.Names.Contains(name)) {
        name = name + "_" + (data.FID++);
      }

      data.Names.Add(name);

      var meth = Helpers.CloneMethod(data.TB, name, method);
      ILGenerator gen = meth.GetILGenerator();
      gen.ThrowException(typeof(NotImplementedException));

      data.TB.DefineMethodOverride(meth, method);
    }
    /// <summary>
    /// Adds the code to invoke the constructor.
    /// </summary>
    /// <param name="ctorgen">The generator to add the code to.</param>
    static void _invokeConstructor(ILGenerator ctorgen) {
      // call the Lua defined constructor method.

      // if(ctor == null) goto end;
      Label end = ctorgen.DefineLabel();
      ctorgen.Emit(OpCodes.Ldarg, 4);
      ctorgen.Emit(OpCodes.Brfalse, end);

      // int argsSize = ctorArgs.Length;
      LocalBuilder argsSize = ctorgen.DeclareLocal(typeof(int));
      ctorgen.Emit(OpCodes.Ldarg, 3);
      ctorgen.Emit(OpCodes.Ldlen);
      ctorgen.Emit(OpCodes.Stloc, argsSize);

      // ILuaValue[] argsArray = new ILuaValue[argsSize + 1];
      LocalBuilder argsArray = ctorgen.DeclareLocal(typeof(ILuaValue[]));
      ctorgen.Emit(OpCodes.Ldloc, argsSize);
      ctorgen.Emit(OpCodes.Ldc_I4_1);
      ctorgen.Emit(OpCodes.Add);
      ctorgen.Emit(OpCodes.Newarr, typeof(ILuaValue));
      ctorgen.Emit(OpCodes.Stloc, argsArray);

      // argsArray[0] = LuaValueBase.CreateValue(this);
      ctorgen.Emit(OpCodes.Ldloc, argsArray);
      ctorgen.Emit(OpCodes.Ldc_I4_0);
      ctorgen.Emit(OpCodes.Ldarg_0);
      ctorgen.Emit(OpCodes.Call, ReflectionMembers.LuaValueBase.CreateValue);
      ctorgen.Emit(OpCodes.Stelem, typeof(ILuaValue));

      // for (int i = 0; i < argsSize; i++)
      Label startLoop = ctorgen.DefineLabel();
      Label endLoop = ctorgen.DefineLabel();
      LocalBuilder i = ctorgen.DeclareLocal(typeof(int));
      ctorgen.Emit(OpCodes.Ldc_I4_0);
      ctorgen.Emit(OpCodes.Stloc, i);
      ctorgen.MarkLabel(startLoop);
      ctorgen.Emit(OpCodes.Ldloc, i);
      ctorgen.Emit(OpCodes.Ldloc, argsSize);
      ctorgen.Emit(OpCodes.Clt);
      ctorgen.Emit(OpCodes.Brfalse, endLoop);
      //   argsArray[i + 1] = ctorArgs[i];
      ctorgen.Emit(OpCodes.Ldloc, argsArray);
      ctorgen.Emit(OpCodes.Ldloc, i);
      ctorgen.Emit(OpCodes.Ldc_I4_1);
      ctorgen.Emit(OpCodes.Add);
      ctorgen.Emit(OpCodes.Ldarg, 3);
      ctorgen.Emit(OpCodes.Ldloc, i);
      ctorgen.Emit(OpCodes.Ldelem, typeof(ILuaValue));
      ctorgen.Emit(OpCodes.Stelem, typeof(ILuaValue));
      //   i++
      ctorgen.Emit(OpCodes.Ldloc, i);
      ctorgen.Emit(OpCodes.Ldc_I4_1);
      ctorgen.Emit(OpCodes.Add);
      ctorgen.Emit(OpCodes.Stloc, i);
      ctorgen.Emit(OpCodes.Br, startLoop);
      ctorgen.MarkLabel(endLoop);

      // LuaMultiValue args = new LuaMultiValue(argsArray);
      LocalBuilder args = ctorgen.DeclareLocal(typeof(LuaMultiValue));
      ctorgen.Emit(OpCodes.Ldloc, argsArray);
      ctorgen.Emit(OpCodes.Newobj, ReflectionMembers.LuaMultiValue.Constructor);
      ctorgen.Emit(OpCodes.Stloc, args);

      // ctor.Invoke(args);
      ctorgen.Emit(OpCodes.Ldarg, 4);
      ctorgen.Emit(OpCodes.Ldloc, args);
      ctorgen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.Invoke);
      ctorgen.Emit(OpCodes.Pop);

      ctorgen.MarkLabel(end);

      ctorgen.Emit(OpCodes.Ret);
    }

    #region ILuaIndexer implementation

    class LuaClassItem : LuaValueBase {
      readonly LuaClass _parent;
      readonly Type _type;

      public LuaClassItem(LuaClass parent, Type type) {
        _parent = parent;
        _type = type;
      }

      public override LuaValueType ValueType {
        get { return LuaValueType.UserData; }
      }

      public override void SetIndex(ILuaValue index, ILuaValue value) {
        string? name = index.GetValue() as string;
        if (name == null) {
          throw new InvalidOperationException(
              string.Format(Resources.BadIndexType, "class definition", "string"));
        }

        // find the members with the given name.
        var members = _type.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Public |
                                       BindingFlags.Instance)
                          .Where(m => m.Name == name)
                          .ToArray();
        if (members.Length == 0) {
          throw new InvalidOperationException(string.Format(Resources.NoMemberFound, _type, name));
        }

        if (members.Length > 1) {
          throw new AmbiguousMatchException(string.Format(Resources.NoMemberFound, _type, name));
        }

        // set the backing parent object.
        name = _type.FullName + "." + name;
        if (members[0].MemberType == MemberTypes.Method) {
          if (value.ValueType != LuaValueType.Function) {
            throw new InvalidOperationException(string.Format(Resources.MustBeFunction, name));
          }

          Item item = _createItem(name, new[] { members[0] });
          item.Assign(value);
          _parent._items.Add(item);
        } else if (members[0].MemberType == MemberTypes.Property) {
          Item item = _createItem(name, members);
          item.Assign(value);
          _parent._items.Add(item);
        }
      }

      public override bool Equals(ILuaValue? other) {
        var temp = other as LuaClassItem;
        return temp != null && temp._parent == _parent && temp._type == _type;
      }

      public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
        throw new ArgumentException(Resources.BadBinOp);
      }

      public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self) {
        throw new ArgumentException(Resources.BadBinOp);
      }
    }

    public override ILuaValue GetIndex(ILuaValue index) {
      string? n = index.GetValue() as string;
      if (n == null) {
        throw new InvalidOperationException(
            string.Format(Resources.BadIndexType, "class definition", "string"));
      }

      foreach (var item in Interfaces) {
        if (item.Name == n) {
          return new LuaClassItem(this, item);
        }
      }
      throw new InvalidOperationException(string.Format(Resources.DoesNotImplement, Name, n));
    }

    public override void SetIndex(ILuaValue index, ILuaValue value) {
      var name = index.GetValue() as string;
      if (_created != null || name == null) {
        return;
      }

      // If this is the constructor, assign it.
      if (name == ConstructorName) {
        if (value.ValueType == LuaValueType.Function) {
          _ctor = value;
        } else {
          throw new InvalidOperationException(Resources.CtorMustBeFunc);
        }

        return;
      }

      // Try to find an existing element.
      Item? i = _items.Where(ii => ii.Name == name).FirstOrDefault();
      if (i == null) {
        // Check for the member in the base class.
        if (BaseType != null) {
          var mems = BaseType.GetMember(name);
          if (mems != null && mems.Length > 0) {
            i = _createItem(name, mems);
          }
        }

        // Check for the member in interfaces.
        if (i == null) {
          Type? inter = null;
          MemberInfo[]? mems = null;
          foreach (var item in Interfaces) {
            var temp = item.GetMember(name);
            if (temp != null && temp.Length > 0) {
              if (mems != null) {
                mems = null;
                break;
              }
              inter = item;
              mems = temp;
            }
          }

          if (mems != null) {
            i = _createItem(name, mems);
          }
        }

        // If still not found, create a new field.
        i ??= new FieldItem(name);

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
    static Item _createItem(string name, MemberInfo[] members) {
      if (members[0].MemberType == MemberTypes.Property) {
        return new PropertyItem(name, members[0]);
      } else if (members[0].MemberType == MemberTypes.Method) {
        if (members.Length > 1) {
          throw new AmbiguousMatchException(
              string.Format(Resources.ManyMembersFound, members[0].DeclaringType, name));
        }

        return new MethodItem(name, members[0]);
      } else {
        throw new InvalidOperationException(
            string.Format(Resources.CannotHideMember, name, members[0].DeclaringType));
      }
    }

    #endregion

    public override LuaMultiValue Invoke(LuaMultiValue args) {
      return new LuaMultiValue(new LuaUserData<object>(CreateInstance(args.ToArray())));
    }

    #region Item Classes

    /// <summary>
    /// Contains data used to generate an item.
    /// </summary>
    sealed class ItemData {
      public ItemData(string name, Type? baseType, Type[] interfaces) {
        MethodArgs = new List<ILuaValue>();
        Methods = new List<MethodInfo>();
        Constants = new List<ILuaValue>();
        Names = new HashSet<string>();
        FID = 0;

        // Create the type builder.
        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("DynamicAssembly2"), AssemblyBuilderAccess.Run);
        AB = ab;
        ModuleBuilder mb = ab.DefineDynamicModule("DynamicAssembly2.dll");
        TB = mb.DefineType(
            name, TypeAttributes.Class | TypeAttributes.BeforeFieldInit | TypeAttributes.Public,
            baseType, interfaces);

        // public ctor(ILuaValue[] methods, ILuaValue[] initialValues, ILuaValue[] ctorArgs,
        //             ILuaValue ctor);
        {
          var temp = new[] { typeof(ILuaValue[]), typeof(ILuaValue[]),  typeof(ILuaValue[]),
                             typeof(ILuaValue) };
          ConstructorBuilder ctor = TB.DefineConstructor(
              MethodAttributes.Public | MethodAttributes.HideBySig, CallingConventions.Standard,
              temp);
          CtorGen = ctor.GetILGenerator();

          // base();
          CtorGen.Emit(OpCodes.Ldarg_0);
          var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
          CtorGen.Emit(
              OpCodes.Call,
              (baseType ?? typeof(object)).GetConstructor(flags, null, Array.Empty<Type>(), null)!);
        }
      }

      public List<ILuaValue> MethodArgs;
      public List<MethodInfo> Methods;
      public List<ILuaValue> Constants;
      public HashSet<string> Names;
      public ILGenerator CtorGen;
      public TypeBuilder TB;
      public int FID;
      public AssemblyBuilder AB;
    }
    /// <summary>
    /// A base class for an item used for creating a class.
    /// </summary>
    abstract class Item {
      public string Name;

      protected Item(string name) {
        Name = name;
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
      /// Adds a field to the type that contains the given method.  Adds code in the constructor to
      /// load the method.
      /// </summary>
      /// <param name="method">The method to store in the field.</param>
      /// <returns>The field that contains the method.</returns>
      protected static FieldBuilder _addMethodArg(ItemData data, ILuaValue method) {
        FieldBuilder field = data.TB.DefineField("<>_field_" + data.FID++, typeof(ILuaValue),
                                                 FieldAttributes.Private);

        // Store the method in the input list and create code in the constructor to get the method
        // from the argument and store it in the envField.
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
    sealed class MethodItem : Item {
      public ILuaValue? Method;
      public MethodInfo BoundTo;

      public MethodItem(string name, MemberInfo member) : base(name) {
        Method = null;
        BoundTo = (MethodInfo)member;
      }

      public override void Assign(ILuaValue value) {
        if (value.ValueType != LuaValueType.Function) {
          throw new InvalidOperationException(
              string.Format(Resources.CannotHideMember, Name, BoundTo.DeclaringType));
        }

        Method = value;
      }
      public override void Generate(ItemData data) {
        if (Method == null)
          throw new InvalidOperationException($"Class member {Name} not assigned a value");

        var field = _addMethodArg(data, Method);

        // Define a non-conflict named method that will back the given method.
        string name = Name;
        if (data.Names.Contains(name)) {
          name = name + "_" + (data.FID++);
        }

        data.Names.Add(name);

        // Define the new method.
        var param = BoundTo.GetParameters();
        var meth = Helpers.CloneMethod(data.TB, name, BoundTo);
        ILGenerator gen = meth.GetILGenerator();

        // ILuaValue[] loc = new ILuaValue[{param.length} + 1];
        LocalBuilder loc = gen.CreateArray(typeof(ILuaValue), param.Length + 1);

        // loc[0] = LuaValueBase.CreateValue(this);
        gen.Emit(OpCodes.Ldloc, loc);
        gen.Emit(OpCodes.Ldc_I4_0);
        gen.Emit(OpCodes.Ldarg_0);
        gen.Emit(OpCodes.Call, ReflectionMembers.LuaValueBase.CreateValue);
        gen.Emit(OpCodes.Stelem, typeof(ILuaValue));

        for (int ind = 0; ind < param.Length; ind++) {
          // loc[{ind} + 1] = LuaValueBase.CreateValue(arg_{ind});
          gen.Emit(OpCodes.Ldloc, loc);
          gen.Emit(OpCodes.Ldc_I4, ind + 1);
          gen.Emit(OpCodes.Ldarg, ind + 1);
          if (!param[ind].ParameterType.IsClass) {
            gen.Emit(OpCodes.Box, param[ind].ParameterType);
          }

          gen.Emit(OpCodes.Call, ReflectionMembers.LuaValueBase.CreateValue);
          gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
        }

        // LuaMultiValue args = new LuaMultiValue(loc);
        var args = gen.DeclareLocal(typeof(LuaMultiValue));
        gen.Emit(OpCodes.Ldloc, loc);
        gen.Emit(OpCodes.Newobj, ReflectionMembers.LuaMultiValue.Constructor);
        gen.Emit(OpCodes.Stloc, args);

        _callFieldAndReturn(gen, BoundTo.ReturnType, field, args);

        data.TB.DefineMethodOverride(meth, BoundTo);
        data.Methods.Add(BoundTo);
      }
    }
    /// <summary>
    /// A class item that is a property.
    /// </summary>
    sealed class PropertyItem : Item {
      public ILuaValue? Default;
      public Type Type;
      public ILuaValue? Method, MethodSet;
      public MethodInfo? BoundTo, BoundSet;
      readonly PropertyInfo _prop;

      public PropertyItem(string name, MemberInfo member) : base(name) {
        _prop = (PropertyInfo)member;
        Default = null;
        Type = _prop.PropertyType;
        Method = null;
        MethodSet = null;
        BoundTo = null;
        BoundSet = null;
      }

      public override void Assign(ILuaValue value) {
        if (value.ValueType == LuaValueType.Table) {
          // Set the 'get' method for the item
          var item = value.GetIndex(new LuaString("get"));
          if (item != null && item != LuaNil.Nil) {
            MethodInfo? m = _prop.GetGetMethod(true);
            if (m == null || (!m.Attributes.HasFlag(MethodAttributes.Abstract) &&
                              !m.Attributes.HasFlag(MethodAttributes.Virtual))) {
              throw new InvalidOperationException(
                  string.Format(Resources.CannotOverrideProperty, Name));
            }

            BoundTo = m;

            if (item.ValueType != LuaValueType.Function) {
              throw new InvalidOperationException(Resources.PropTableFuncs);
            }

            Method = item;
          }
          // Set the 'set' method for the item
          item = value.GetIndex(new LuaString("set"));
          if (item != null && item != LuaNil.Nil) {
            MethodInfo? m = _prop.GetSetMethod(true);
            if (m == null || (!m.Attributes.HasFlag(MethodAttributes.Abstract) &&
                              !m.Attributes.HasFlag(MethodAttributes.Virtual))) {
              throw new InvalidOperationException(
                  string.Format(Resources.CannotOverrideProperty, Name));
            }

            BoundSet = m;

            if (item.ValueType != LuaValueType.Function) {
              throw new InvalidOperationException(Resources.PropTableFuncs);
            }

            MethodSet = item;
          }
        } else {
          MethodInfo? m = _prop.GetGetMethod(true);
          if (m == null || (!m.Attributes.HasFlag(MethodAttributes.Abstract) &&
                            !m.Attributes.HasFlag(MethodAttributes.Virtual))) {
            throw new InvalidOperationException(
                string.Format(Resources.CannotOverrideProperty, Name));
          }

          BoundTo = m;

          // Check if the set method is abstract, if it is, we need to implement that also.
          m = _prop.GetSetMethod(true);
          if (m != null &&
             (m.Attributes & MethodAttributes.Abstract) == MethodAttributes.Abstract) {
            BoundSet = m;
          }

          Default = value;
        }
      }
      public override void Generate(ItemData data) {
        const MethodAttributes flags = MethodAttributes.Public | MethodAttributes.Virtual |
                                        MethodAttributes.Final | MethodAttributes.HideBySig;
        if (BoundTo != null) {
          // Define a get method for the property.
          MethodBuilder m = data.TB.DefineMethod("get_" + Name, flags, Type, null);
          ILGenerator gen = m.GetILGenerator();
          if (Method == null) {
            // Define a default getter that returns a constant value.
            string name = "<" + Name + ">_backing";
            FieldBuilder field = data.TB.DefineField(name, Type, FieldAttributes.Private);
            if (Default != null) {
              data.Constants.Add(Default);
              // this.{field} = arg_2[{_input2.Count - 1}].As<Type>();
              data.CtorGen.Emit(OpCodes.Ldarg_0);
              data.CtorGen.Emit(OpCodes.Ldarg_2);
              data.CtorGen.Emit(OpCodes.Ldc_I4, (data.Constants.Count - 1));
              data.CtorGen.Emit(OpCodes.Ldelem, typeof(ILuaValue));
              data.CtorGen.Emit(OpCodes.Callvirt,
                                ReflectionMembers.ILuaValue.As.MakeGenericMethod(Type));
              data.CtorGen.Emit(OpCodes.Stfld, field);
            }

            // return this.{envField};
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, field);
            gen.Emit(OpCodes.Ret);
          } else {
            // Define a getter method that returns a value from a method.
            FieldBuilder field = _addMethodArg(data, Method);

            // LuaMultiValue loc = new LuaMultiValue(new ILuaValue[0]);
            LocalBuilder loc = gen.DeclareLocal(typeof(LuaMultiValue));
            gen.Emit(OpCodes.Ldc_I4, 0);
            gen.Emit(OpCodes.Newarr, typeof(ILuaValue));
            gen.Emit(OpCodes.Newobj, ReflectionMembers.LuaMultiValue.Constructor);
            gen.Emit(OpCodes.Stloc, loc);

            _callFieldAndReturn(gen, BoundTo.ReturnType, field, loc);
          }

          data.TB.DefineMethodOverride(m, BoundTo);
          data.Methods.Add(BoundTo);
        }

        if (BoundSet != null) {
          // Define a setter method.
          MethodBuilder m = data.TB.DefineMethod("set_" + Name, flags, null, new[] { Type });
          ILGenerator gen = m.GetILGenerator();

          if (MethodSet == null) {
            // throw new NotSupportedException("Cannot write to a constant property.");
            gen.Emit(OpCodes.Ldstr, Resources.ConstantProperty);
            gen.Emit(OpCodes.Newobj, ReflectionMembers.NotSupportedException.StringConstructor);
            gen.Emit(OpCodes.Throw);
          } else {
            FieldBuilder field = _addMethodArg(data, MethodSet);

            // object[] loc = new object[1];
            LocalBuilder loc = gen.CreateArray(typeof(object), 1);

            // loc[0] = (object)arg_1
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Ldarg_1);
            if (Type.IsValueType) {
              gen.Emit(OpCodes.Box, Type);
            }

            gen.Emit(OpCodes.Stelem, typeof(object));

            // LuaMultiValue args = LuaMultiValue.CreateMultiValueFromObj(loc);
            LocalBuilder args = gen.DeclareLocal(typeof(LuaMultiValue));
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Call, ReflectionMembers.LuaMultiValue.CreateMultiValueFromObj);
            gen.Emit(OpCodes.Stloc, args);

            _callFieldAndReturn(gen, typeof(void), field, args);
          }

          data.TB.DefineMethodOverride(m, BoundSet);
          data.Methods.Add(BoundSet);
        }
      }
    }
    /// <summary>
    /// A class item that is a field.
    /// </summary>
    sealed class FieldItem : Item {
      public Type? Type;
      public ILuaValue? Default;

      public FieldItem(string name) : base(name) {
        Type = null;
        Default = null;
      }

      public override void Assign(ILuaValue value) {
        if (value is LuaType type) {
          if (Type == null) {
            Type = type.Type;
          } else {
            throw new InvalidOperationException(string.Format(Resources.MemberHasType, Name));
          }
        } else {
          if (Default != null) {
            throw new InvalidOperationException(string.Format(Resources.MemberHasDefault, Name));
          }

          if (Type == null) {
            Type = value?.GetValue()?.GetType() ?? typeof(object);
          }
          Default = value;
        }
      }
      public override void Generate(ItemData data) {
        if (Type == null)
          throw new InvalidOperationException($"Class member {Name} not assigned a value");

        string name = Name;
        if (data.Names.Contains(name)) {
          name = name + "_" + (data.FID++);
        }

        FieldBuilder field = data.TB.DefineField(name, Type, FieldAttributes.Public);

        if (Default != null) {
          data.Constants.Add(Default);
          // {envField} = arg_2[{_input2.Count - 1}].As<Type>();
          data.CtorGen.Emit(OpCodes.Ldarg_0);
          data.CtorGen.Emit(OpCodes.Ldarg_2);
          data.CtorGen.Emit(OpCodes.Ldc_I4, (data.Constants.Count - 1));
          data.CtorGen.Emit(OpCodes.Ldelem, typeof(ILuaValue));
          data.CtorGen.Emit(OpCodes.Callvirt,
                            ReflectionMembers.ILuaValue.As.MakeGenericMethod(Type));
          data.CtorGen.Emit(OpCodes.Stfld, field);
        }
      }
    }

    #endregion

    public override bool Equals(ILuaValue? other) {
      return ReferenceEquals(this, other);
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
      throw new ArgumentException(Resources.BadBinOp);
    }

    public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self) {
      throw new ArgumentException(Resources.BadBinOp);
    }
  }
}
