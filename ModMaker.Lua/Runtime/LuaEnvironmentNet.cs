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
        result = typeof(Convert).GetMethod("To" + t1.Name, new[] { typeof(double) })
                     .Invoke(null, new object[] { Value });
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
    /// Gets or sets the global value with the specified name.
    /// </summary>
    /// <param name="name">The name of the global variable.</param>
    /// <returns>The value of the variable.</returns>
    public virtual ILuaValue this[string name] {
      get {
        return _globals.GetIndex(_runtime.CreateValue(name));
      }
      set {
        _globals.SetIndex(_runtime.CreateValue(name), value);
      }
    }

    /// <summary>
    /// Creates a new LuaEnvironment without initializing the state, for use with a derived type.
    /// </summary>
    protected LuaEnvironmentNet() {
      _compiler = new CodeCompiler();
      _parser = new PlainParser();
      _runtime = LuaRuntimeNet.Create(this);
      Settings = new LuaSettings().AsReadOnly();
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

      _globals = new LuaTable();
      _runtime = LuaRuntimeNet.Create(this);
      _compiler = new CodeCompiler();
      _parser = new PlainParser();
      _modules = new ModuleBinder();

      Settings = settings.AsReadOnly();

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
    /// The settings of the environment.
    /// </summary>
    public LuaSettings Settings { get; protected set; }
    /// <summary>
    /// Gets or sets the runtime that Lua code will execute in.  This framework assumes that the
    /// value returned is never null.  Some implementations may support setting to null.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">If setting to a null value.</exception>
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
    /// <summary>
    /// Gets the globals table for the environment.  This can never return a null value.
    /// </summary>
    /// <remarks>
    /// If a derived type attempts to set to null, an ArugmentNullException will be thrown.
    /// </remarks>
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
    /// <summary>
    /// Gets or sets the code compiler for the environment.  Cannot be accessed from within Lua
    /// code.  This framework assumes that the value returned is never null.  Some implementations
    /// may support setting to null.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">If setting to a null value.</exception>
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
    /// <summary>
    /// Gets or sets the parser for the environment.  Cannot be accessed from within Lua code.This
    /// framework assumes that the value returned is never null.  Some implementations may support
    /// setting to null.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">If setting to a null value.</exception>
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
    /// <summary>
    /// Gets or sets the module binder for the environment.  The code can assume that the value
    /// returned is never null; however some implementations may allow setting to null.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">If setting to a null value.</exception>
    public IModuleBinder ModuleBinder {
      get { return _modules; }
      set {
        if (value == null) {
          throw new ArgumentNullException(nameof(value));
        }

        _modules = value;
      }
    }

    /// <summary>
    /// Registers a delegate to the globals table.
    /// </summary>
    /// <param name="d">The delegate to register.</param>
    /// <param name="name">The name of the delegate.</param>
    /// <exception cref="System.ArgumentException">If there is already an
    /// object registered with that name.</exception>
    /// <exception cref="System.ArgumentNullException">If d or name is null.</exception>
    public virtual void RegisterDelegate(Delegate d, string name) {
      if (d == null) {
        throw new ArgumentNullException(nameof(d));
      }

      if (name == null) {
        throw new ArgumentNullException(nameof(name));
      }

      lock (this) {
        object o = GlobalsTable.GetItemRaw(_runtime.CreateValue(name));
        if (o != LuaNil.Nil) {
          LuaOverloadFunction meth = o as LuaOverloadFunction;
          if (meth == null) {
            throw new ArgumentException(string.Format(Resources.AlreadyRegistered, name));
          }

          meth.AddOverload(d);
        } else {
          GlobalsTable.SetItemRaw(
              _runtime.CreateValue(name),
              new LuaOverloadFunction(name, new[] { d.Method }, new[] { d.Target }));
        }
      }
    }
    /// <summary>
    /// Registers a type with the globals table.
    /// </summary>
    /// <param name="t">The type to register.</param>
    /// <param name="name">The name of the type.</param>
    /// <exception cref="System.ArgumentException">If there is already an
    /// object registered with that name.</exception>
    /// <exception cref="System.ArgumentNullException">If t or name is null.</exception>
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

    /// <summary>
    /// Returns the enumeration of all dynamic member names.
    /// </summary>
    /// <returns>A sequence that contains dynamic member names.</returns>
    public override IEnumerable<string> GetDynamicMemberNames() {
      foreach (var item in GlobalsTable) {
        if (item.Key.ValueType == LuaValueType.String) {
          yield return (string)item.Key.GetValue();
        }
      }
    }
    /// <summary>
    /// Provides implementation for type conversion operations. Classes derived from
    /// the System.Dynamic.DynamicObject class can override this method to specify
    /// dynamic behavior for operations that convert an object from one type to another.
    /// </summary>
    /// <param name="binder">
    /// Provides information about the conversion operation. The binder.Type property provides the
    /// type to which the object must be converted. For example, for the statement
    /// (String)sampleObject in C# (CType(sampleObject, Type) in Visual Basic), where sampleObject
    /// is an instance of the class derived from the System.Dynamic.DynamicObject class,
    /// binder.Type returns the System.String type. The binder.Explicit property provides
    /// information about the kind of conversion that occurs. It returns true for explicit
    /// conversion and false for implicit conversion.
    /// </param>
    /// <param name="result">The result of the type conversion operation.</param>
    /// <returns>
    /// true if the operation is successful; otherwise, false. If this method returns false, the
    /// run-time binder of the language determines the behavior. (In most cases, a language-specific
    /// run-time exception is thrown.)
    /// </returns>
    public override bool TryConvert(ConvertBinder binder, out object result) {
      if (typeof(ILuaEnvironment) == binder.Type) {
        result = this;
        return true;
      }
      return base.TryConvert(binder, out result);
    }
    /// <summary>
    /// Provides the implementation for operations that get a value by index. Classes derived from
    /// the System.Dynamic.DynamicObject class can override this method to specify dynamic behavior
    /// for indexing operations.
    /// </summary>
    /// <param name="binder">Provides information about the operation.</param>
    /// <param name="indexes">
    /// The indexes that are used in the operation. For example, for the sampleObject[3] operation
    /// in C# (sampleObject(3) in Visual Basic), where sampleObject is derived from the
    /// DynamicObject class, indexes[0] is equal to 3.
    /// </param>
    /// <param name="result">The result of the index operation.</param>
    /// <returns>
    /// true if the operation is successful; otherwise, false. If this method returns false, the
    /// run-time binder of the language determines the behavior. (In most cases, a run-time
    /// exception is thrown.)
    /// </returns>
    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result) {
      if (indexes != null && indexes.Length == 1) {
        ILuaValue o;
        lock (this) {
          o = GlobalsTable.GetItemRaw(_runtime.CreateValue(indexes[0]));
        }

        MethodInfo asMethod =
            typeof(ILuaValue).GetMethod(nameof(ILuaValue.As)).MakeGenericMethod(binder.ReturnType);
        result = asMethod.Invoke(o, null);
        if (result is double d) {
          result = new NumberProxy(d);
        }

        return true;
      } else {
        return base.TryGetIndex(binder, indexes, out result);
      }
    }
    /// <summary>
    /// Provides the implementation for operations that set a value by index. Classes derived from
    /// the System.Dynamic.DynamicObject class can override this method to specify dynamic behavior
    /// for operations that access objects by a specified index.
    /// </summary>
    /// <param name="binder">Provides information about the operation.</param>
    /// <param name="indexes">
    /// The indexes that are used in the operation. For example, for the sampleObject[3] = 10
    /// operation in C# (sampleObject(3) = 10 in Visual Basic), where sampleObject is derived from
    /// the System.Dynamic.DynamicObject class, indexes[0] is equal to 3.
    /// </param>
    /// <param name="value">
    /// The value to set to the object that has the specified index. For example, for the
    /// sampleObject[3] = 10 operation in C# (sampleObject(3) = 10 in Visual Basic), where
    /// sampleObject is derived from the System.Dynamic.DynamicObject class, value is equal to 10.
    /// </param>
    /// <returns>
    /// true if the operation is successful; otherwise, false. If this method returns false, the
    /// run-time binder of the language determines the behavior. (In most cases, a language-specific
    /// run-time exception is thrown.
    /// </returns>
    public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value) {
      if (indexes != null && indexes.Length == 1) {
        lock (this) {
          GlobalsTable.SetItemRaw(
              _runtime.CreateValue(indexes[0]), _runtime.CreateValue(value));
        }
        return true;
      } else {
        return base.TrySetIndex(binder, indexes, value);
      }
    }
    /// <summary>
    /// Provides the implementation for operations that get member values. Classes derived from the
    /// System.Dynamic.DynamicObject class can override this method to specify dynamic behavior for
    /// operations such as getting a value for a property.
    /// </summary>
    /// <param name="binder">
    /// Provides information about the object that called the dynamic operation. The binder.Name
    /// property provides the name of the member on which the dynamic operation is performed. For
    /// example, for the Console.WriteLine(sampleObject.SampleProperty) statement, where
    /// sampleObject is an instance of the class derived from the System.Dynamic.DynamicObject
    /// class, binder.Name returns "SampleProperty". The binder.IgnoreCase property specifies
    /// whether the member name is case-sensitive.
    /// </param>
    /// <param name="result">
    /// The result of the get operation. For example, if the method is called for a property, you
    /// can assign the property value to result.
    /// </param>
    /// <returns>
    /// true if the operation is successful; otherwise, false. If this method returns false, the
    /// run-time binder of the language determines the behavior. (In most cases, a run-time
    /// exception is thrown.)
    /// </returns>
    public override bool TryGetMember(GetMemberBinder binder, out object result) {
      ILuaValue o;
      lock (this) {
        o = GlobalsTable.GetItemRaw(_runtime.CreateValue(binder.Name));
      }

      MethodInfo asMethod =
          typeof(ILuaValue).GetMethod(nameof(ILuaValue.As)).MakeGenericMethod(binder.ReturnType);
      result = asMethod.Invoke(o, null);
      if (result is double d) {
        result = new NumberProxy(d);
      }

      return true;
    }
    /// <summary>
    /// Provides the implementation for operations that set member values. Classes derived from the
    /// System.Dynamic.DynamicObject class can override this method to specify dynamic behavior for
    /// operations such as setting a value for a property.
    /// </summary>
    /// <param name="binder">
    /// Provides information about the object that called the dynamic operation. The binder.Name
    /// property provides the name of the member to which the value is being assigned. For example,
    /// for the statement sampleObject.SampleProperty = "Test", where sampleObject is an instance of
    /// the class derived from the System.Dynamic.DynamicObject class, binder.Name returns
    /// "SampleProperty". The binder.IgnoreCase property specifies whether the member name is
    /// case-sensitive.
    /// </param>
    /// <param name="value">
    /// The value to set to the member. For example, for sampleObject.SampleProperty = "Test", where
    /// sampleObject is an instance of the class derived from the System.Dynamic.DynamicObject
    /// class, the value is "Test".
    /// </param>
    /// <returns>
    /// true if the operation is successful; otherwise, false. If this method returns false, the
    /// run-time binder of the language determines the behavior. (In most cases, a language-specific
    /// run-time exception is thrown.)
    /// </returns>
    public override bool TrySetMember(SetMemberBinder binder, object value) {
      lock (this) {
        GlobalsTable.SetItemRaw(Runtime.CreateValue(binder.Name), Runtime.CreateValue(value));
      }

      return true;
    }
  }
}
