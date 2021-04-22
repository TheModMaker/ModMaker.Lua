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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Defines the default Lua runtime.  This class is in charge of resolving operators and
  /// converting types.  This can be inherited to modify its behavior.
  /// </summary>
  public class LuaRuntimeNet : ILuaRuntime {
    readonly ILuaEnvironment _env;
    readonly ThreadPool _threadPool;

    /// <summary>
    /// Creates a new instance of the default LuaRuntime.
    /// </summary>
    public LuaRuntimeNet(ILuaEnvironment env) {
      _env = env;
      _threadPool = new ThreadPool(env);
    }

    /// <summary>
    /// Gets or sets whether to use a thread pool for Lua threads.
    /// </summary>
    public bool UseThreadPool { get; set; }
    public ILuaThread CurrentThread {
      get { return _threadPool.Search(Thread.CurrentThread.ManagedThreadId); }
    }

    public virtual IEnumerable<ILuaMultiValue> GenericLoop(ILuaEnvironment env,
                                                           ILuaMultiValue args) {
      // TODO: Replace this.
      if (args == null) {
        throw new ArgumentNullException(nameof(args));
      }

      if (env == null) {
        throw new ArgumentNullException(nameof(env));
      }

      ILuaValue target = args[0];
      object temp = target.GetValue();
      if (temp is IEnumerable<ILuaMultiValue> enumT) {
        foreach (var item in enumT) {
          yield return item;
        }
      } else if (temp is IEnumerable en) {
        foreach (var item in en) {
          yield return new LuaMultiValue(LuaValueBase.CreateValue(item));
        }
      } else if (target.ValueType == LuaValueType.Function) {
        ILuaValue s = args[1];
        ILuaValue var = args[2];

        while (true) {
          var ret = target.Invoke(LuaNil.Nil, false, new LuaMultiValue(s, var));
          if (ret == null || ret[0] == null || ret[0] == LuaNil.Nil) {
            yield break;
          }

          var = ret[0];

          yield return ret;
        }
      } else {
        throw new InvalidOperationException(
            $"Cannot enumerate over an object of type '{args[0]}'.");
      }
    }

    public ILuaThread CreateThread(ILuaValue method) {
      return _threadPool.Create(method);
    }
    public void CreateClassValue(string[] impl, string name) {
      Type b = null;
      List<Type> inter = new List<Type>();
      foreach (var item in impl) {
        // Get the types that this Lua code can access according to the settings.
        IEnumerable<Type> access;
        if (_env.Settings.ClassAccess == LuaClassAccess.All) {
          access = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).ToArray();
        } else if (_env.Settings.ClassAccess == LuaClassAccess.System) {
          var allowed =
              Resources.Whitelist.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
          access = _env.GlobalsTable.Where(k => k.Value is LuaType)
              .Select(k => (k.Value as LuaType).Type);
          access = access.Concat(AppDomain.CurrentDomain.GetAssemblies()
              .Where(a => allowed.Contains(a.GetName().GetPublicKey().ToStringBase16()))
              .SelectMany(a => a.GetTypes()));
        } else {
          access = _env.GlobalsTable.Where(k => k.Value is LuaType)
              .Select(k => (k.Value as LuaType).Type);
        }

        // Get the types that match the given name.
        Type[] typesa = access.Where(t => t.Name == item || t.FullName == item).ToArray();
        if (typesa.Length == 0) {
          throw new InvalidOperationException($"Unable to locate the type '{item}'");
        }

        if (typesa.Length > 1) {
          throw new InvalidOperationException($"More than one type found for name '{name}'");
        }

        Type type = typesa[0];

        if ((type.Attributes & TypeAttributes.Public) != TypeAttributes.Public &&
            (type.Attributes & TypeAttributes.NestedPublic) != TypeAttributes.NestedPublic) {
          throw new InvalidOperationException("Base class and interfaces must be public");
        }

        if (type.IsClass) {
          // if the type is a class, it will be the base class
          if (b == null) {
            if (type.IsSealed) {
              throw new InvalidOperationException("Cannot derive from a sealed class.");
            }

            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            if (type.GetConstructor(flags, null, new Type[0], null) == null) {
              throw new InvalidOperationException(
                  "Cannot derive from a type without an empty constructor.");
            }

            b = type;
          } else {
            throw new InvalidOperationException("Can only derive from a single base class.");
          }
        } else if (type.IsInterface) {
          inter.Add(type);
        } else {
          throw new InvalidOperationException("Cannot derive from a value-type.");
        }
      }

      // create and register the LuaClass object.
      LuaClass c = new LuaClass(name, b, inter.ToArray(), _env);
      _env.GlobalsTable.SetItemRaw(new LuaString(name), c);
    }
  }
}
