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
using System.Threading;
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

    public override bool TryConvert(ConvertBinder binder, out object? result) {
      Type t1 = binder.Type;
      if (t1 == typeof(SByte) || t1 == typeof(Int16) || t1 == typeof(Int32) ||
          t1 == typeof(Int64) || t1 == typeof(Single) || t1 == typeof(Double) ||
          t1 == typeof(UInt16) || t1 == typeof(UInt32) || t1 == typeof(UInt64) ||
          t1 == typeof(Byte) || t1 == typeof(Decimal)) {
        result = Helpers.DynamicInvoke(
            typeof(Convert).GetMethod("To" + t1.Name, new[] { typeof(double) })!,
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
  public class LuaEnvironment : DynamicObject, ILuaEnvironment {
    static readonly ThreadLocal<ILuaEnvironment?> _environment =
        new ThreadLocal<ILuaEnvironment?>();

    /// <summary>
    /// Creates a new LuaEnvironment without initializing the state, for use with a derived type.
    /// </summary>
    protected LuaEnvironment() {
      Settings = new LuaSettings().AsReadOnly();
      CodeCompiler = new CodeCompiler(Settings);
      GlobalsTable = new LuaTable(this);
      Runtime = new LuaRuntime(this);
    }
    /// <summary>
    /// Creates a new environment with the given settings.
    /// </summary>
    /// <param name="settings">The settings to give the Environment.</param>
    public LuaEnvironment(LuaSettings settings) {
      Settings = settings.AsReadOnly();
      CodeCompiler = new CodeCompiler(Settings);
      GlobalsTable = new LuaTable(this);
      Runtime = new LuaRuntime(this);

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

    /// <summary>
    /// Gets the current Environment object for the current thread.  This is only valid while Lua
    /// code is being executed; this throws an exception if Lua code isn't running.
    /// </summary>
    public static ILuaEnvironment CurrentEnvironment {
      get {
        return _environment.Value ??
               throw new InvalidOperationException("No current environment set");
      }
    }

    /// <summary>
    /// Sets the current thread's environment to the given object.  This MUST be used in a "using"
    /// statement to restore the old environment at the end of the block.
    /// </summary>
    /// <param name="e">The environment to set to.</param>
    /// <returns>An object to use in a "using" statement to restore the environment.</returns>
    internal static IDisposable _setEnvironment(ILuaEnvironment? e) {
      if (e != null && _environment.Value != null && _environment.Value != e)
        throw new InvalidOperationException("Cannot invoke Lua code from a different environment");

      var prev = _environment.Value;
      _environment.Value = e;
      return Helpers.Disposable(() => {
        if (_environment.Value != e)
          throw new InvalidOperationException("Environment changed out of order");
        _environment.Value = prev;
      });
    }

    public virtual ILuaValue this[string name] {
      get { return GlobalsTable.GetIndex(new LuaString(name)); }
      set { GlobalsTable.SetIndex(new LuaString(name), value); }
    }

    public LuaSettings Settings { get; protected set; }
    public ILuaRuntime Runtime { get; set; }
    public ILuaTable GlobalsTable { get; set; }
    public ICodeCompiler CodeCompiler { get; set; }
    public IParser Parser { get; set; } = new PlainParser();
    public IModuleBinder ModuleBinder { get; set; } = new ModuleBinder();

    public virtual void RegisterDelegate(Delegate d, string name) {
      lock (this) {
        object o = GlobalsTable.GetItemRaw(new LuaString(name));
        if (o != LuaNil.Nil) {
          LuaOverloadFunction? meth = o as LuaOverloadFunction;
          if (meth == null) {
            throw new ArgumentException(string.Format(Resources.AlreadyRegistered, name));
          }

          meth.AddOverload(d);
        } else {
          GlobalsTable.SetItemRaw(
              new LuaString(name),
              new LuaOverloadFunction(this, name, new[] { d.Method }, new[] { d.Target }));
        }
      }
    }
    public virtual void RegisterType(Type t, string name) {
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
          yield return (string)item.Key.GetValue()!;
        }
      }
    }
    public override bool TryConvert(ConvertBinder binder, out object? result) {
      if (typeof(ILuaEnvironment) == binder.Type) {
        result = this;
        return true;
      }
      return base.TryConvert(binder, out result);
    }
    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result) {
      if (indexes.Length == 1) {
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
    public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object? value) {
      if (indexes.Length == 1) {
        lock (this) {
          GlobalsTable.SetItemRaw(
              LuaValueBase.CreateValue(indexes[0]), LuaValueBase.CreateValue(value));
        }
        return true;
      } else {
        return base.TrySetIndex(binder, indexes, value);
      }
    }
    public override bool TryGetMember(GetMemberBinder binder, out object? result) {
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
    public override bool TrySetMember(SetMemberBinder binder, object? value) {
      lock (this) {
        GlobalsTable.SetItemRaw(new LuaString(binder.Name), LuaValueBase.CreateValue(value));
      }

      return true;
    }
  }
}
