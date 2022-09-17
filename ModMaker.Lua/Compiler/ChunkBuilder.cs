// Copyright 2012 Jacob Trimble
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
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Compiler {
  /// <summary>
  /// Helps build a chunk by managing nested types and scopes.  Also generates some code to load
  /// locals and the current environment.
  /// </summary>
  sealed class ChunkBuilder {
    /// <summary>
    /// Defines a nested type for a nested function.  Each nested function that also has nested
    /// methods will have a nested type.  If a function does not have any nested functions it does
    /// not have a nested type.  The function is defined in the parent nest and any local variables
    /// defined in this function that is captured is defined as a field in the nested type.  Then
    /// any nested functions are defined in the nested type.  This is how C# handles captures
    /// with lambda expressions.
    /// </summary>
    sealed class NestInfo {
      /// <summary>
      /// A static ID to generate unique names across all types.
      /// </summary>
      static int _id = 1;

      /// <summary>
      /// Defines the captures for this nest.
      /// </summary>
      readonly HashSet<NameItem> _captures;
      /// <summary>
      /// Contains the members that are defined in this type.
      /// </summary>
      public HashSet<string> Members;

      /// <summary>
      /// Gets a dictionary of local variables that can be reused indexed by the type of the
      /// variable.
      /// </summary>
      public Dictionary<Type, Stack<LocalBuilder>> FreeLocals { get; private set; }
      /// <summary>
      /// Gets the ILGenerator for this nest.  This generator belongs to the type of the parent nest
      /// but is only used for generating code for this nest.
      /// </summary>
      public ILGenerator? Generator { get; private set; }
      /// <summary>
      /// Gets the type definition for this nested type.  It is created in the constructor.
      /// </summary>
      public TypeBuilder? TypeDef { get; private set; }
      /// <summary>
      /// Gets the parent nest object, is null for the root nest object.
      /// </summary>
      public NestInfo? Parent { get; private set; }
      /// <summary>
      /// Gets the field defined in this type that holds the parent instance.  This may not exist if
      /// the type does not capture any locals from the parent type.
      /// </summary>
      public FieldBuilder? ParentInst { get; private set; }
      /// <summary>
      /// Gets the local variable that holds an instance to this type.
      /// </summary>
      public LocalBuilder? ThisInst { get; private set; }
      /// <summary>
      /// Gets the local variable definitions for this nest.  These are the variables defined in the
      /// defining method.  The indices in the list are the scopes of the variables and the
      /// dictionary maps the name to the field in the type.
      /// </summary>
      public Stack<Dictionary<string, IVarDefinition>> Locals { get; private set; }

      /// <summary>
      /// Creates a new nest with the given parent.
      /// </summary>
      /// <param name="parent">The parent nest.</param>
      /// <param name="gen">The generator used to generate code for this function.</param>
      /// <param name="storeParent">
      /// True to create a field that stores the parent instance; otherwise false.
      /// </param>
      /// <param name="captures">
      /// The local variables that have been captured by nested functions.
      /// </param>
      /// <param name="createType">True to create a nested type, otherwise false.</param>
      public NestInfo(NestInfo parent, ILGenerator gen, NameItem[] captures, bool createType,
                      bool storeParent) {
        FreeLocals = new Dictionary<Type, Stack<LocalBuilder>>();
        Members = new HashSet<string>();
        _captures = new HashSet<NameItem>(captures);
        Parent = parent;
        Generator = gen;
        Locals = new Stack<Dictionary<string, IVarDefinition>>();
        Locals.Push(new Dictionary<string, IVarDefinition>());

        if (createType) {
          // create the type and constructor.
          TypeDef = parent.TypeDef!.DefineNestedType(
              "<>c__DisplayClass" + (_id++),
              TypeAttributes.NestedPublic | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);
          var ctor = TypeDef.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                                               Array.Empty<Type>());
          var cgen = ctor.GetILGenerator();

          // base();
          cgen.Emit(OpCodes.Ldarg_0);
          cgen.Emit(OpCodes.Call, typeof(object).GetConstructor(new Type[0])!);
          cgen.Emit(OpCodes.Ret);

          if (storeParent) {
            ParentInst = TypeDef.DefineField("CS$<>__locals", parent.TypeDef,
                                             FieldAttributes.Public);
          } else {
            ParentInst = null;
          }

          // create the local definition
          // ThisInst = new TypeDef();
          ThisInst = gen.DeclareLocal(TypeDef);
          gen.Emit(OpCodes.Newobj, ctor);
          gen.Emit(OpCodes.Stloc, ThisInst);

          if (storeParent) {
            // ThisInst.ParentInst = this;
            gen.Emit(OpCodes.Ldloc, ThisInst);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Stfld, ParentInst!);
          }
        } else {
          TypeDef = null;
          ThisInst = null;
          ParentInst = null;
        }
      }
      NestInfo(TypeBuilder tb) {
        FreeLocals = new Dictionary<Type, Stack<LocalBuilder>>();
        _captures = new HashSet<NameItem>();
        Members = new HashSet<string>();
        Locals = new Stack<Dictionary<string, IVarDefinition>>();
        Locals.Push(new Dictionary<string, IVarDefinition>());
        Parent = null;
        ParentInst = null;
        Generator = null;
        TypeDef = tb;
        ThisInst = null;
      }

      /// <summary>
      /// Creates the root nest node from the given TypeBuilder.
      /// </summary>
      /// <param name="tb">The type builder to create for.</param>
      /// <param name="gen">The ILGenerator for the global function.</param>
      /// <param name="captures">The captures for the global function.</param>
      /// <param name="createType">
      /// Whether to create a type for the global function.
      /// </param>
      /// <returns>The new root nest node.</returns>
      public static NestInfo Create(TypeBuilder tb, ILGenerator gen, NameItem[] captures,
                                    bool createType) {
        NestInfo temp = new NestInfo(tb);
        return new NestInfo(temp, gen, captures, createType, false);
      }

      /// <summary>
      /// Searches this type for a given local variable and returns an object to manipulate it's
      /// value.
      /// </summary>
      /// <param name="name">The Lua name of the variable.</param>
      /// <returns>A variable that will manipulate it's value or null if not found.</returns>
      public IVarDefinition? FindLocal(NameItem name) {
        // The iterator will return in the order they would be pop'd.
        foreach (var item in Locals) {
          if (item.TryGetValue(name.Name, out IVarDefinition? ret)) {
            return ret;
          }
        }
        return null;
      }
      /// <summary>
      /// Defines a new Local variable and returns the field that represents it.
      /// </summary>
      /// <param name="name">The Lua name of the variable.</param>
      /// <returns>The variable that represents the local.</returns>
      public IVarDefinition DefineLocal(NameItem name) {
        if (_captures.Contains(name)) {
          string mName = name.Name;
          if (Members.Contains(mName)) {
            int i = 0;
            while (Members.Contains(mName + "_" + i)) {
              i++;
            }

            mName += "_" + i;
          }

          Members.Add(mName);
          var field = TypeDef!.DefineField(mName, typeof(ILuaValue), FieldAttributes.Public);
          return Locals.Peek()[name.Name] = new CapturedVarDef(Generator!, ThisInst!, field);
        } else {
          var loc = Generator!.DeclareLocal(typeof(ILuaValue));
          return Locals.Peek()[name.Name] = new LocalVarDef(Generator, loc);
        }
      }
      /// <summary>
      /// Returns an object that starts a new local block when created and will end it when Dispose
      /// is called.  For use with 'using' keyword.
      /// </summary>
      /// <returns>A helper object that will end the scope when Dispose is called.</returns>
      public IDisposable LocalBlock() {
        Locals.Push(new Dictionary<string, IVarDefinition>());
        return Helpers.Disposable(() => { Locals.Pop(); });
      }
    }

    readonly LuaSettings _settings;
    readonly ModuleBuilder _mb;
    NestInfo _curNest;
#if NETFRAMEWORK
    Dictionary<string, ISymbolDocumentWriter> _documents =
        new Dictionary<string, ISymbolDocumentWriter>();
#endif
    int _mid = 1;

    /// <summary>
    /// Creates a new ChunkBuilder and initializes the state.
    /// </summary>
    /// <param name="tb">The root type of this chunk.</param>
    /// <param name="captures">An array of the global captures.</param>
    /// <param name="createType">
    /// True to create a nested type for the global function, this means that there are nested
    /// functions.
    /// </param>
    public ChunkBuilder(LuaSettings settings, ModuleBuilder mb, TypeBuilder tb, NameItem[] captures,
                        bool createType) {
      //// ILuaEnviormnent $Env;
      var field = tb.DefineField("$Env", typeof(ILuaEnvironment), FieldAttributes.Private);

      //// ILuaMultiValue Invoke(ILuaEnvironment E, ILuaMultiValue args);
      var method = tb.DefineMethod(
          nameof(ILuaValue.Invoke), MethodAttributes.Public | MethodAttributes.HideBySig,
          typeof(LuaMultiValue), new[] { typeof(ILuaEnvironment), typeof(LuaMultiValue) });
      _curNest = NestInfo.Create(tb, method.GetILGenerator(), captures, createType);
      _mb = mb;
      _settings = settings;

      _addInvoke(tb, method, field);
      _addConstructor(tb, field);
      _addAbstracts(tb);
    }

    /// <summary>
    /// Adds default implementation of abstract methods.
    /// </summary>
    /// <param name="tb">The type builder to add to.</param>
    static void _addAbstracts(TypeBuilder tb) {
      // bool Equals(ILuaValue other);
      var method = tb.DefineMethod(
          nameof(ILuaValue.Equals),
          MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
          typeof(bool), new[] { typeof(ILuaValue) });
      var gen = method.GetILGenerator();
      // return object.ReferenceEquals(this, other);
      gen.Emit(OpCodes.Ldarg_0);
      gen.Emit(OpCodes.Ldarg_1);
      gen.Emit(OpCodes.Call, ReflectionMembers.Object_.ReferenceEquals);
      gen.Emit(OpCodes.Ret);

      // ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other);
      method = tb.DefineMethod(
          nameof(ILuaValue.Arithmetic),
          MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
          typeof(ILuaValue), new[] { typeof(BinaryOperationType), typeof(ILuaValue) });
      gen = method.GetILGenerator();
      // throw new NotImplementedException();
      gen.ThrowException(typeof(NotImplementedException));
      gen.Emit(OpCodes.Ret);

      // LuaValueType get_ValueType();
      method = tb.DefineMethod(
          "get_" + nameof(ILuaValue.ValueType),
          MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
          typeof(LuaValueType), Type.EmptyTypes);
      gen = method.GetILGenerator();
      // return LuaValueType.Function;
      gen.Emit(OpCodes.Ldc_I4, (int)LuaValueType.Function);
      gen.Emit(OpCodes.Ret);

      // ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self);
      method = tb.DefineMethod(
          nameof(ILuaValue.Arithmetic),
          MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual);
      var types = method.DefineGenericParameters("T");
      method.SetParameters(typeof(BinaryOperationType),
                           typeof(LuaUserData<>).MakeGenericType(types));
      method.SetReturnType(typeof(ILuaValue));
      gen = method.GetILGenerator();
      // throw new NotImplementedException();
      gen.ThrowException(typeof(NotImplementedException));
      gen.Emit(OpCodes.Ret);
    }
    /// <summary>
    /// Adds a constructor that accepts a single ILuaEnvironment argument to the given type builder
    /// object.
    /// </summary>
    /// <param name="tb">The type builder to add to.</param>
    /// <param name="envField">The field that stores the environment.</param>
    static void _addConstructor(TypeBuilder tb, FieldBuilder envField) {
      //// .ctor(ILuaEnvironment/*!*/ E);
      var ctor = tb.DefineConstructor(
          MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(ILuaEnvironment) });
      var gen = ctor.GetILGenerator();

      // this.$Env = E;
      gen.Emit(OpCodes.Ldarg_0);
      gen.Emit(OpCodes.Ldarg_1);
      gen.Emit(OpCodes.Stfld, envField);
      gen.Emit(OpCodes.Ret);
    }
    /// <summary>
    /// Adds the two overloads of IMethod.Invoke that simply calls the given method.
    /// </summary>
    /// <param name="tb">The type builder to add to.</param>
    /// <param name="realMethod">The real method to call.</param>
    /// <param name="envField">The field that contains the environment.</param>
    static void _addInvoke(TypeBuilder tb, MethodBuilder realMethod, FieldBuilder envField) {
      //// ILuaMultiValue Invoke(ILuaMultiValue args);
      MethodBuilder mb = Helpers.CloneMethod(tb, nameof(ILuaValue.Invoke),
                                             ReflectionMembers.ILuaValue.Invoke);
      var gen = mb.GetILGenerator();

      // return this.Invoke(this.Environment, args);
      gen.Emit(OpCodes.Ldarg_0);
      gen.Emit(OpCodes.Ldarg_0);
      gen.Emit(OpCodes.Ldfld, envField);
      gen.Emit(OpCodes.Ldarg_1);
      gen.Emit(OpCodes.Callvirt, realMethod);
      gen.Emit(OpCodes.Ret);
    }

    /// <summary>
    /// Gets the ILGenerator for the current function.
    /// </summary>
    public ILGenerator CurrentGenerator { get { return _curNest.Generator!; } }

    public void MarkSequencePoint(DebugInfo info) {
#if NETFRAMEWORK
      if (!_settings.AddNativeDebugSymbols || info.Path == null)
        return;

      if (!_documents.ContainsKey(info.Path)) {
        _documents.Add(info.Path, _mb.DefineDocument(info.Path, SymDocumentType.Text,
                                                     SymLanguageType.ILAssembly, Guid.Empty));
      }
      CurrentGenerator.MarkSequencePoint(_documents[info.Path], (int)info.StartLine,
                                         (int)info.StartPos, (int)info.EndLine, (int)info.EndPos);
#endif
    }

    /// <summary>
    /// Compiles the current code into am IMethod.
    /// </summary>
    /// <param name="e">The current environment.</param>
    /// <returns>A new IMethod compiled from the current code.</returns>
    public ILuaValue CreateChunk(ILuaEnvironment e) {
      if (_curNest == null) {
        throw new InvalidOperationException();
      }

      if (_curNest.TypeDef != null) {
        _curNest.TypeDef.CreateType();
      }

      Type t = _curNest.Parent!.TypeDef!.CreateType()!;
      return new LuaGlobalFunction(e, t);
    }
    /// <summary>
    /// Starts a local-variable scope block and returns an object that will end the scope when
    /// Dispose is called. This is to be used with the 'using' keyword.
    /// </summary>
    /// <returns>
    /// An object that will end the local block when 'Dispose' is called.
    /// </returns>
    public IDisposable LocalBlock() {
      return _curNest.LocalBlock();
    }

    /// <summary>
    /// Implements a function definition based on a given function definition.
    /// </summary>
    /// <param name="funcName">The simple name of the function, can be null.</param>
    /// <param name="visitor">The current visitor object.</param>
    /// <param name="function">The function to generate for.</param>
    public void ImplementFunction(IParseItemVisitor visitor, FuncDefItem function,
                                  string? funcName) {
      NameItem[] args = function.Arguments.ToArray();
      if (function.InstanceName != null) {
        args = new[] { new NameItem("self") }.Concat(args).ToArray();
      }

      // ILuaMultiValue function(ILuaEnvironment E, ILuaMultiValue args);
      funcName ??= "<>__" + (_mid++);
      string name = _curNest.Members.Contains(funcName) ? funcName + "_" + (_mid++) : funcName;
      MethodBuilder mb = _curNest.TypeDef!.DefineMethod(
          name, MethodAttributes.Public, typeof(LuaMultiValue),
          new Type[] {typeof(ILuaEnvironment), typeof(LuaMultiValue)});
      var gen = mb.GetILGenerator();
      _curNest = new NestInfo(
          _curNest, gen, function.FunctionInformation!.CapturedLocals,
          function.FunctionInformation.HasNested, function.FunctionInformation.CapturesParent);

      // if this is an instance method, create a BaseAccessor object to help types.
      if (function.InstanceName != null) {
        // TODO: Add base accessor back.
        //var field = curNest.DefineLocal(new NameItem("base"));
      }

      for (int i = 0; i < args.Length; i++) {
        var field = _curNest.DefineLocal(args[i]);

        if (args[i].Name == "...") {
          if (i != args.Length - 1) {
            throw new InvalidOperationException(
                "Variable arguments (...) only valid at end of argument list.");
          }

          // {field} = new LuaMultiValue(args.Skip({args.Length - 1}).ToArray());
          field.StartSet();
          gen.Emit(OpCodes.Ldarg_2);
          gen.Emit(OpCodes.Ldc_I4, args.Length - 1);
          gen.Emit(OpCodes.Call,
                   ReflectionMembers.Enumerable.Skip.MakeGenericMethod(typeof(ILuaValue)));
          gen.Emit(OpCodes.Call,
                   ReflectionMembers.Enumerable.ToArray.MakeGenericMethod(typeof(ILuaValue)));
          gen.Emit(OpCodes.Newobj, ReflectionMembers.LuaMultiValue.Constructor);
          field.EndSet();
        } else {
          // {field} = args[i];
          field.StartSet();
          gen.Emit(OpCodes.Ldarg_2);
          gen.Emit(OpCodes.Ldc_I4, i);
          gen.Emit(OpCodes.Call, ReflectionMembers.LuaMultiValue.get_Item);
          field.EndSet();
        }
      }

      function.Block.Accept(visitor);

      if (_curNest.TypeDef != null) {
        _curNest.TypeDef.CreateType();
      }

      _curNest = _curNest.Parent!;
      // push a pointer to the new method onto the stack of the previous nest method
      //   the above line restores the nest to the previous state and this code will
      //   push the new method.
      //! push new LuaDefinedFunction({name}, {nest.TypeDef}.GetMethod({name}),
      //                              {nest.ThisInst != null ? nest.NestInst : this} );
      _curNest.Generator!.Emit(OpCodes.Ldarg_1);
      _curNest.Generator.Emit(OpCodes.Ldstr, name);
      _curNest.Generator.Emit(OpCodes.Ldtoken, _curNest.TypeDef!);
      _curNest.Generator.Emit(OpCodes.Call, ReflectionMembers.Type_.GetTypeFromHandle);
      _curNest.Generator.Emit(OpCodes.Ldstr, name);
      _curNest.Generator.Emit(OpCodes.Callvirt, ReflectionMembers.Type_.GetMethod);
      if (_curNest.ThisInst != null) {
        _curNest.Generator.Emit(OpCodes.Ldloc, _curNest.ThisInst);
      } else {
        _curNest.Generator.Emit(OpCodes.Ldarg_0);
      }

      _curNest.Generator.Emit(OpCodes.Newobj, ReflectionMembers.LuaDefinedFunction.Constructor);
    }
    /// <summary>
    /// Searches for a variable with the given name and returns an object used to get/set it's
    /// value.  There are three kinds of variables: Local, Captured, and Global.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <returns>An object used to generate code for this variable.</returns>
    public IVarDefinition FindVariable(NameItem name) {
      // Search in the current nest
      var varDef = _curNest.FindLocal(name);
      if (varDef != null) {
        return varDef;
      }

      // Search for parent captures
      var fields = new List<FieldBuilder>();
      var cur = _curNest.Parent;
      while (cur != null) {
        varDef = cur.FindLocal(name);
        if (varDef != null) {
          if (varDef is LocalVarDef) {
            throw new InvalidOperationException();
          }

          fields.Add(((CapturedVarDef)varDef).Field);
          return new CapturedParVarDef(CurrentGenerator, fields.ToArray());
        }

        fields.Add(cur.ParentInst!);
        cur = cur.Parent;
      }

      // Still not found, it is a global variable
      return new GlobalVarDef(CurrentGenerator, name.Name);
    }
    /// <summary>
    /// Defines a new local variable and returns an object used to get/set it's value.  There are
    /// two possible variable types: Local and Captured.  Which one is chosen depends on whether the
    /// variable is captured in the FunctionInfo used to create the current function.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <returns>An object used to get/set it's value.</returns>
    public IVarDefinition DefineLocal(NameItem name) {
      return _curNest.DefineLocal(name);
    }

    /// <summary>
    /// Creates a new temporary variable and returns the local used to use it.  This may also use a
    /// variable from the cache.  When the variable is no longer used, call
    /// RemoveTemporary(LocalBuilder).
    /// </summary>
    /// <param name="type">The type of the local variable.</param>
    /// <returns>The local builder that defines the variable.</returns>
    public LocalBuilder CreateTemporary(Type type) {
      if (_curNest.FreeLocals.ContainsKey(type)) {
        var temp = _curNest.FreeLocals[type];
        if (temp.Count > 0) {
          return temp.Pop();
        }
      }

      return CurrentGenerator.DeclareLocal(type);
    }
    /// <summary>
    /// Creates an array of the given type and stores it in a returned local.
    /// </summary>
    /// <param name="arrayType">The type of the array.</param>
    /// <param name="size">The size of the array.</param>
    /// <returns>A local builder that now contains the array.</returns>
    public LocalBuilder CreateArray(Type arrayType, int size) {
      var ret = CreateTemporary(arrayType.MakeArrayType());
      CurrentGenerator.Emit(OpCodes.Ldc_I4, size);
      CurrentGenerator.Emit(OpCodes.Newarr, arrayType);
      CurrentGenerator.Emit(OpCodes.Stloc, ret);

      return ret;
    }
    /// <summary>
    /// Marks the given local variable as not being used anymore.  This allows the variable to be
    /// used again in other code.
    /// </summary>
    /// <param name="local">The local variable that is no longer used.</param>
    public void RemoveTemporary(LocalBuilder local) {
      if (!_curNest.FreeLocals.ContainsKey(local.LocalType)) {
        _curNest.FreeLocals.Add(local.LocalType, new Stack<LocalBuilder>());
      }

      _curNest.FreeLocals[local.LocalType].Push(local);
    }

    #region public interface VariableDefinition

    /// <summary>
    /// A helper interface used to generate code for different variable definitions.  There are
    /// three scopes: Global, Local, and Captured.
    /// </summary>
    public interface IVarDefinition {
      /// <summary>
      /// Starts the setting of the variable.  For example, pushing 'this' onto the stack.
      /// </summary>
      void StartSet();
      /// <summary>
      /// Ends the setting of the variable.  For example, emitting OpCodes.Stloc.
      /// </summary>
      void EndSet();
      /// <summary>
      /// Pushes the value of the variable onto the stack.
      /// </summary>
      void Get();
    }

    /// <summary>
    /// Defines a global variable.
    /// </summary>
    sealed class GlobalVarDef : IVarDefinition {
      readonly ILGenerator _gen;
      readonly string _name;

      public GlobalVarDef(ILGenerator gen, string name) {
        _gen = gen;
        _name = name;
      }

      public void StartSet() {
        // part of:
        // E.GlobalsTable.SetIndex(LuaValueBase.CreateValue({name}), value);
        _gen.Emit(OpCodes.Ldarg_1);
        _gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaEnvironment.get_GlobalsTable);
        _gen.Emit(OpCodes.Ldstr, _name);
        _gen.Emit(OpCodes.Call, ReflectionMembers.LuaValueBase.CreateValue);
      }
      public void EndSet() {
        // end of:
        // E.GlobalsTable.SetIndex({name}, value);
        _gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.SetIndex);
      }
      public void Get() {
        //! push E.GlobalsTable.GetIndex(LuaValueBase.CreateValue({name}))
        _gen.Emit(OpCodes.Ldarg_1);
        _gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaEnvironment.get_GlobalsTable);
        _gen.Emit(OpCodes.Ldstr, _name);
        _gen.Emit(OpCodes.Call, ReflectionMembers.LuaValueBase.CreateValue);
        _gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.GetIndex);
      }
    }
    /// <summary>
    /// Defines a local variable definition, one that is only referenced by the current function.
    /// </summary>
    sealed class LocalVarDef : IVarDefinition {
      readonly ILGenerator _gen;
      readonly LocalBuilder _local;

      public LocalVarDef(ILGenerator gen, LocalBuilder loc) {
        _gen = gen;
        _local = loc;
      }

      public void StartSet() {
        // do nothing.
      }
      public void EndSet() {
        // local = {value};
        _gen.Emit(OpCodes.Stloc, _local);
      }
      public void Get() {
        _gen.Emit(OpCodes.Ldloc, _local);
      }
    }
    /// <summary>
    /// Defines a local variable that has been captured by nested functions and is stored in a field
    /// in a nested type.  This version is used for when called from the defining function so only
    /// an instance to the child type is needed to access the variable.
    /// </summary>
    sealed class CapturedVarDef : IVarDefinition {
      readonly ILGenerator _gen;
      readonly LocalBuilder _thisInst;
      public FieldBuilder Field;

      public CapturedVarDef(ILGenerator gen, LocalBuilder thisInst, FieldBuilder field) {
        _gen = gen;
        _thisInst = thisInst;
        Field = field;
      }

      public void StartSet() {
        // part of:
        // thisInst.field = {value}
        _gen.Emit(OpCodes.Ldloc, _thisInst);
      }
      public void EndSet() {
        // part of:
        // thisInst.field = {value}
        _gen.Emit(OpCodes.Stfld, Field);
      }
      public void Get() {
        _gen.Emit(OpCodes.Ldloc, _thisInst);
        _gen.Emit(OpCodes.Ldfld, Field);
      }
    }
    /// <summary>
    /// Defines a local variable that has been captured by nested functions and is stored in a
    /// parent nest.  This version loads the variable from parent nest types.
    /// </summary>
    sealed class CapturedParVarDef : IVarDefinition {
      readonly ILGenerator _gen;
      readonly FieldBuilder[] _fields;

      public CapturedParVarDef(ILGenerator gen, FieldBuilder[] fields) {
        _gen = gen;
        _fields = fields;
      }

      public void StartSet() {
        _gen.Emit(OpCodes.Ldarg_0);
        for (int i = 0; i < _fields.Length - 1; i++) {
          _gen.Emit(OpCodes.Ldfld, _fields[i]);
        }
      }
      public void EndSet() {
        _gen.Emit(OpCodes.Stfld, _fields[_fields.Length - 1]);
      }
      public void Get() {
        _gen.Emit(OpCodes.Ldarg_0);
        for (int i = 0; i < _fields.Length; i++) {
          _gen.Emit(OpCodes.Ldfld, _fields[i]);
        }
      }
    }

    #endregion
  }
}
