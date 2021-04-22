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
using System.IO;
using System.Linq;
using System.Reflection;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Defines how Lua looks for external modules.  When 'require' is called from Lua, it calls
  /// IModuleBinder.Load.
  /// </summary>
  public interface IModuleBinder {
    /// <summary>
    /// Searches and loads the module according to the binder.
    /// </summary>
    /// <param name="env">The environment to load to.</param>
    /// <param name="name">The name of the module to find.</param>
    /// <returns>The loaded module, or null if it could not be loaded.</returns>
    ILuaValue Load(ILuaEnvironment env, string name);
  }

  /// <summary>
  /// Defines the default binding behavior similar to that of the Lua Language Specification.
  /// </summary>
  public sealed class ModuleBinder : IModuleBinder {
    readonly Dictionary<string, ILuaValue> _loaded = new Dictionary<string, ILuaValue>();

    /// <summary>
    /// Creates a new ModuleBinder with default settings.
    /// </summary>
    public ModuleBinder() {
      Path = @"!\?.lua;!\?.dll;!\?\init.lua;.\?.lua;.\?.dll;.\?\init.lua";
      AllowAssemblies = false;
      AllowLua = true;
      WhitelistPublicKeys = null;
    }

    /// <summary>
    /// Gets or sets the search-path for modules.
    /// </summary>
    public string Path { get; set; }
    /// <summary>
    /// Gets or sets whether to allow .NET assemblies to run.  Any code that implements IMethod is
    /// valid.  Default: false.
    /// </summary>
    public bool AllowAssemblies { get; set; }
    /// <summary>
    /// Gets or sets whether to allow uncompiled(source) Lua code.
    /// Default: true.
    /// </summary>
    public bool AllowLua { get; set; }
    /// <summary>
    /// Gets or sets the list of public-keys that are allowed to load from.  These must be full
    /// public keys.  Set to null to allow any assembly.  Include a null entry to allow weakly-named
    /// assemblies.
    /// </summary>
    /// <remarks>
    /// This value overrides ModuleBinder.AllowCompiledLua.
    /// </remarks>
    public string[] WhitelistPublicKeys { get; set; }

    public ILuaValue Load(ILuaEnvironment env, string name) {
      if (env == null) {
        throw new ArgumentNullException(nameof(env));
      }

      if (name == null) {
        throw new ArgumentNullException(nameof(name));
      }

      if (_loaded.ContainsKey(name)) {
        return _loaded[name];
      }

      List<Exception> exceptions = new List<Exception>();
      foreach (string s in this.Path.Split(';')) {
        string path = s.Replace("?", name);
        if (File.Exists(path)) {
          ILuaValue o = _processFile(path, name, name, env, exceptions);
          if (o != null) {
            _loaded.Add(name, o);
            return o;
          }
        }

        int i = name.IndexOf('.');
        while (i != -1) {
          path = s.Replace("?", name.Substring(0, i));
          if (File.Exists(path)) {
            ILuaValue o = _processFile(path, name, name.Substring(i + 1), env, exceptions);
            if (o != null) {
              _loaded.Add(name, o);
              return o;
            }
          }
          i = name.IndexOf('.', i + 1);
        }
      }
      throw new AggregateException(
          "Unable to load module '" + name + "' because a valid module could not be located.  " +
          "See $Exception.InnerExceptions to see why.",
          exceptions);
    }

    ILuaValue _processFile(string path, string name, string partname, ILuaEnvironment env,
                           List<Exception> exceptions) {
      if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) {
        if (!this.AllowLua) {
          exceptions.Add(new InvalidOperationException(
              $"Cannot load file '{path}' because ModuleBinder.AllowLua is set to false."));
          return null;
        }

        var item = PlainParser.Parse(
            env.Parser, File.ReadAllText(path), System.IO.Path.GetFileNameWithoutExtension(path));
        var chunk = env.CodeCompiler.Compile(env, item, null);
        return chunk.Invoke(LuaNil.Nil, false, LuaMultiValue.Empty).Single();
      } else if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
        var aname = AssemblyName.GetAssemblyName(path);

        if (WhitelistPublicKeys != null) {
          bool valid = false;
          foreach (var key in WhitelistPublicKeys) {
            if (key == null) {
              if (aname.GetPublicKey() == null) {
                valid = true;
              }
            } else {
              if (aname.GetPublicKey().ToStringBase16().ToLowerInvariant() ==
                  key.ToLowerInvariant()) {
                valid = true;
              }
            }
          }
          if (!valid) {
            exceptions.Add(new InvalidOperationException(
                "Cannot load file '" + path +
                "' because the assembly's public key is not in the white-list."));
            return null;
          }
        }

        // Process the assembly
        if (AllowAssemblies) {
          var a = Assembly.LoadFrom(path);
          var types = a.GetTypes();
          if (types == null || types.Length == 0) {
            exceptions.Add(new InvalidOperationException(
                "Cannot load file '" + path + "' because it does not define any types."));
            return null;
          } else if (types.Length > 1) {
            HashSet<Type> validTypes = new HashSet<Type>();
            bool matchesName(Type t) => t.Name == name || t.Name == partname ||
                                        t.FullName == name || t.FullName == partname;

            foreach (var item in types.Where(t => matchesName(t) && _validType(t))) {
              validTypes.Add(item);
            }

            if (validTypes.Count > 1) {
              exceptions.Add(new InvalidOperationException(
                  $"Cannot load file '{path}' because more than one type matches the given name."));
              return null;
            } else if (validTypes.Count == 0) {
              exceptions.Add(new InvalidOperationException(
                  $"Cannot load file '{path}' because no types match the given name."));
              return null;
            } else {
              return _processType(validTypes.First(), env);
            }
          } else {
            return _processType(types[0], env);
          }
        }
      }
      return null;
    }
    static bool _validType(Type t) {
      return typeof(ILuaValue).IsAssignableFrom(t) &&
          (t.GetConstructor(new Type[0]) != null ||
           t.GetConstructor(new[] { typeof(ILuaEnvironment) }) != null);
    }
    static ILuaValue _processType(Type t, ILuaEnvironment env) {
      if (!typeof(ILuaValue).IsAssignableFrom(t)) {
        return null;
      }

      ConstructorInfo ci = t.GetConstructor(new Type[0]);
      if (ci == null) {
        ci = t.GetConstructor(new Type[] { typeof(ILuaEnvironment) });
        if (ci != null) {
          ILuaValue mod = (ILuaValue)ci.Invoke(new[] { env });
          return mod.Invoke(LuaNil.Nil, false, new LuaMultiValue()).Single();
        }
        return null;
      } else {
        ILuaValue mod = (ILuaValue)ci.Invoke(null);
        return mod.Invoke(LuaNil.Nil, false, new LuaMultiValue()).Single();
      }
    }
  }
}
