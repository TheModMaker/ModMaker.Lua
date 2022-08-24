// Copyright 2014 Jacob Trimble
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
using System.Dynamic;
using System.Reflection;
using ModMaker.Lua.Compiler;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// A dynamic object that is used to convert implicitly to numeric types.
  /// </summary>
  sealed class NumberProxy : DynamicObject {
    // TODO: Consider removing.
    public double Value;

    public NumberProxy(double value) {
      Value = value;
    }

    public override bool TryConvert(ConvertBinder binder, out object result) {
      Type t1 = binder.Type;
      if (t1 == typeof(SByte) || t1 == typeof(Int16) || t1 == typeof(Int32) ||
          t1 == typeof(Int64) || t1 == typeof(Single) || t1 == typeof(Double) ||
          t1 == typeof(UInt16) || t1 == typeof(UInt32) || t1 == typeof(UInt64) ||
          t1 == typeof(Byte) || t1 == typeof(Decimal)) {
        result = Helpers.DynamicInvoke(
            typeof(Convert).GetMethod("To" + t1.Name, new[] { typeof(double) }),
            null, new object[] { Value });
        return true;
      }
      return base.TryConvert(binder, out result);
    }

    public static object Create(object o) {
      if (o is double d) {
        o = new NumberProxy(d);
      }

      return o;
    }
  }

  /// <summary>
  /// Defines the environment that Lua operates in.
  /// </summary>
  [LuaIgnore]
  public class LuaEnvironmentNet : DynamicObject, ILuaEnvironmentNet {
    IModuleBinder _modules;
    ILuaTable _globals;
    ICodeCompiler _compiler;
    IParser _parser;
    ILuaRuntime _runtime;

    /// <summary>
    /// Creates a new LuaEnvironment without initializing the state, for use with a derived type.
    /// </summary>
    protected LuaEnvironmentNet() {
      Settings = new LuaSettings().AsReadOnly();
      _compiler = new CodeCompiler(Settings);
      _parser = new PlainParser();
      _runtime = new LuaRuntimeNet(this);
      _globals = new LuaValues.LuaTable();
      _modules = new ModuleBinder();
    }
    /// <summary>
    /// Creates a new environment with the given settings.
    /// </summary>
    /// <param name="settings">The settings to give the Environment.</param>
    /// <exception cref="System.ArgumentNullException">If settings is null.</exception>
    public LuaEnvironmentNet(LuaSettings settings) {
      if (settings == null) {
        throw new ArgumentNullException(nameof(settings));
      }

      Settings = settings.AsReadOnly();

      _globals = new LuaTable();
      _runtime = new LuaRuntimeNet(this);
      _compiler = new CodeCompiler(Settings);
      _parser = new PlainParser();
      _modules = new ModuleBinder();

      // initialize the global variables.
      LuaStaticLibraries.Initialize(this);
      _initializeTypes();
    }

    /// <summary>
    /// Registers all the primitive types to the globals table.
    /// </summary>
    protected void _initializeTypes() {
      RegisterType(typeof(bool), "bool");
      RegisterType(typeof(byte), "byte");
      RegisterType(typeof(sbyte), "sbyte");
      RegisterType(typeof(short), "short");
      RegisterType(typeof(int), "int");
      RegisterType(typeof(long), "long");
      RegisterType(typeof(ushort), "ushort");
      RegisterType(typeof(uint), "uint");
      RegisterType(typeof(ulong), "ulong");
      RegisterType(typeof(float), "float");
      RegisterType(typeof(double), "double");
      RegisterType(typeof(decimal), "decimal");
      RegisterType(typeof(Int16), "Int16");
      RegisterType(typeof(Int32), "Int32");
      RegisterType(typeof(Int64), "Int64");
      RegisterType(typeof(UInt16), "UInt16");
      RegisterType(typeof(UInt32), "UInt32");
      RegisterType(typeof(UInt64), "UInt64");
      RegisterType(typeof(Single), "Single");
      RegisterType(typeof(Double), "Double");
      RegisterType(typeof(Decimal), "Decimal");
      RegisterType(typeof(String), "String");
      RegisterType(typeof(Byte), "Byte");
      RegisterType(typeof(SByte), "SByte");
      RegisterType(typeof(Boolean), "Boolean");
    }

    public virtual ILuaValue this[string name] {
      get {
        return _globals.GetIndex(new LuaString(name));
      }
      set {
        _globals.SetIndex(new LuaString(name), value);
      }
    }

    public LuaSettings Settings { get; protected set; }
    public ILuaRuntime Runtime {
      get { return _runtime; }
      set {
        if (value == null) {
          throw new ArgumentNullException(nameof(value));
        }

        lock (this) {
          _runtime = value;
        }
      }
    }
    public ILuaTable GlobalsTable {
      get { return _globals; }
      protected set {
        if (value == null) {
          throw new ArgumentNullException(nameof(value));
        }

        lock (this) {
          _globals = value;
        }
      }
    }
    public ICodeCompiler CodeCompiler {
      get { return _compiler; }
      set {
        if (value == null) {
          throw new ArgumentNullException(nameof(value));
        }

        lock (this) {
          _compiler = value;
        }
      }
    }
    public IParser Parser {
      get { return _parser; }
      set {
        if (value == null) {
          throw new ArgumentNullException(nameof(value));
        }

        lock (this) {
          _parser = value;
        }
      }
    }
    public IModuleBinder ModuleBinder {
      get { return _modules; }
      set {
        if (value == null) {
          throw new ArgumentNullException(nameof(value));
        }

        _modules = value;
      }
    }

    public virtual void RegisterDelegate(Delegate d, string name) {
      if (d == null) {
        throw new ArgumentNullException(nameof(d));
      }

      if (name == null) {
        throw new ArgumentNullException(nameof(name));
      }

      lock (this) {
        object o = GlobalsTable.GetItemRaw(new LuaString(name));
        if (o != LuaNil.Nil) {
          LuaOverloadFunction meth = o as LuaOverloadFunction;
          if (meth == null) {
            throw new ArgumentException(string.Format(Resources.AlreadyRegistered, name));
          }

          meth.AddOverload(d);
        } else {
          GlobalsTable.SetItemRaw(
              new LuaString(name),
              new LuaOverloadFunction(name, new[] { d.Method }, new[] { d.Target }));
        }
      }
    }
    public virtual void RegisterType(Type t, string name) {
      if (t == null) {
        throw new ArgumentNullException(nameof(t));
      }

      if (name == null) {
        throw new ArgumentNullException(nameof(name));
      }

      lock (this) {
        var n = new LuaString(name);
        ILuaValue o = GlobalsTable.GetItemRaw(n);
        if (o != LuaNil.Nil) {
          throw new ArgumentException(string.Format(Resources.AlreadyRegistered, name));
        } else {
          GlobalsTable.SetItemRaw(n, new LuaType(t));
        }
      }
    }

    public override IEnumerable<string> GetDynamicMemberNames() {
      foreach (var item in GlobalsTable) {
        if (item.Key.ValueType == LuaValueType.String) {
          yield return (string)item.Key.GetValue();
        }
      }
    }
    public override bool TryConvert(ConvertBinder binder, out object result) {
      if (typeof(ILuaEnvironment) == binder.Type) {
        result = this;
        return true;
      }
      return base.TryConvert(binder, out result);
    }
    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result) {
      if (indexes != null && indexes.Length == 1) {
        ILuaValue o;
        lock (this) {
          o = GlobalsTable.GetItemRaw(LuaValueBase.CreateValue(indexes[0]));
        }

        MethodInfo asMethod = ReflectionMembers.ILuaValue.As.MakeGenericMethod(binder.ReturnType);
        result = Helpers.DynamicInvoke(asMethod, o, null);
        if (result is double d) {
          result = new NumberProxy(d);
        }

        return true;
      } else {
        return base.TryGetIndex(binder, indexes, out result);
      }
    }
    public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value) {
      if (indexes != null && indexes.Length == 1) {
        lock (this) {
          GlobalsTable.SetItemRaw(
              LuaValueBase.CreateValue(indexes[0]), LuaValueBase.CreateValue(value));
        }
        return true;
      } else {
        return base.TrySetIndex(binder, indexes, value);
      }
    }
    public override bool TryGetMember(GetMemberBinder binder, out object result) {
      ILuaValue o;
      lock (this) {
        o = GlobalsTable.GetItemRaw(new LuaString(binder.Name));
      }

      MethodInfo asMethod = ReflectionMembers.ILuaValue.As.MakeGenericMethod(binder.ReturnType);
      result = Helpers.DynamicInvoke(asMethod, o, null);
      if (result is double d) {
        result = new NumberProxy(d);
      }

      return true;
    }
    public override bool TrySetMember(SetMemberBinder binder, object value) {
      lock (this) {
        GlobalsTable.SetItemRaw(new LuaString(binder.Name), LuaValueBase.CreateValue(value));
      }

      return true;
    }
  }
}
