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
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Names match Lua versions")]
  static partial class LuaStaticLibraries {
    public static void Initialize(ILuaEnvironment env) {
      LuaLibraries libraries = env.Settings.Libraries;

      if ((libraries & LuaLibraries.Standard) == LuaLibraries.Standard) {
        Standard.Initialize(env);
      }
      if ((libraries & LuaLibraries.IO) == LuaLibraries.IO) {
        IO.Initialize(env);
      }
      if ((libraries & LuaLibraries.String) == LuaLibraries.String) {
        String.Initialize(env);
      }
      if ((libraries & LuaLibraries.Math) == LuaLibraries.Math) {
        Math.Initialize(env);
        Bit32.Initialize(env);
      }
      if ((libraries & LuaLibraries.Table) == LuaLibraries.Table) {
        Table.Initialize(env);
      }
      if ((libraries & LuaLibraries.OS) == LuaLibraries.OS) {
        OS.Initialize(env);
      }
      if ((libraries & LuaLibraries.Modules) == LuaLibraries.Modules) {
        Module.Initialize(env);
      }
      if ((libraries & LuaLibraries.Coroutine) == LuaLibraries.Coroutine) {
        Coroutine.Initialize(env);
      }
    }

    static void Register(ILuaEnvironment env, ILuaValue table, Delegate func, string? name = null) {
      var funcValue = new LuaOverloadFunction(env, name ?? func.Method.Name, new[] { func.Method },
                                              new[] { func.Target });
      var nameValue = new LuaString(name ?? func.Method.Name);
      table.SetIndex(nameValue, funcValue);
    }

    static LuaMultiValue _pcallInternal(Action cb, bool pcall = false) {
      return _pcallInternal(() => { cb(); return LuaMultiValue.Empty; }, pcall);
    }
    static LuaMultiValue _pcallInternal(Func<LuaMultiValue> cb, bool pcall = false) {
      try {
        return cb();
      } catch (ThreadAbortException) {
        throw;
      } catch (ThreadInterruptedException) {
        throw;
      } catch (Exception e) {
        if (pcall)
          return LuaMultiValue.CreateMultiValueFromObj(false, e.Message, e);
        else
          return LuaMultiValue.CreateMultiValueFromObj(null, e.Message, e.HResult, e);
      }
    }

    static int normalizeIndex_(int max, int i) {
      if (i == 0) {
        return 1;
      } else if (i < 0) {
        return max + i + 1;
      } else if (i > max) {
        return max;
      } else {
        return i;
      }
    }
  }
}
