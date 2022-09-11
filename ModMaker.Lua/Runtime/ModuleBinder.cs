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

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Defines how Lua looks for external modules.  When 'require' is called from Lua, it calls
  /// IModuleBinder.Load.
  /// </summary>
  public interface IModuleBinder {
    /// <summary>
    /// Gets the search-path for modules.  This is a ';' separated list of paths to search.  Each
    /// entry can have a '?' to use the name of the module, or '!' for the executable directory.
    /// </summary>
    public string Path { get; }

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
      Path = @"./?.lua;./?/init.lua;!/?.lua;!/?/init.lua";
    }

    public string Path { get; set; }

    public ILuaValue Load(ILuaEnvironment env, string name) {
      if (_loaded.TryGetValue(name, out ILuaValue? ret)) {
        return ret;
      }

      var exceptions = new List<Exception>();
      foreach (string s in Path.Split(';')) {
        string path = s.Replace("?", name).Replace("!", AppDomain.CurrentDomain.BaseDirectory);
        if (File.Exists(path)) {
          ILuaValue? o = _processFile(path, env, exceptions);
          if (o != null) {
            _loaded.Add(name, o);
            return o;
          }
        }

        int i = name.IndexOf('.');
        while (i != -1) {
          path = s.Replace("?", name.Substring(0, i));
          if (File.Exists(path)) {
            ILuaValue? o = _processFile(path, env, exceptions);
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

    ILuaValue? _processFile(string path, ILuaEnvironment env, List<Exception> exceptions) {
      try {
        using FileStream fs = File.Open(path, FileMode.Open);
        var item = env.Parser.Parse(fs, env.Settings.Encoding, path);
        return env.CodeCompiler.Compile(env, item, "").Single();
      } catch (IOException ex) {
        exceptions.Add(ex);
        return null;
      }
    }
  }
}
