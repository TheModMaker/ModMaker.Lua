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
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Defines the default Lua runtime.  This class is in charge of resolving operators and
  /// converting types.  This can be inherited to modify its behavior.
  /// </summary>
  public class LuaRuntime : ILuaRuntime {
    readonly ThreadPool _threadPool;

    /// <summary>
    /// Creates a new instance of the default LuaRuntime.
    /// </summary>
    public LuaRuntime() {
      _threadPool = new ThreadPool();
    }

    /// <summary>
    /// Gets or sets whether to use a thread pool for Lua threads.
    /// </summary>
    public bool UseThreadPool { get; set; }
    public ILuaThread CurrentThread {
      get { return _threadPool.Search(Environment.CurrentManagedThreadId); }
    }

    public virtual IEnumerable<LuaMultiValue> GenericLoop(ILuaEnvironment env,
                                                           LuaMultiValue args) {
      ILuaValue target = args[0];
      object? temp = target.GetValue();
      if (temp is IEnumerable<LuaMultiValue> enumT) {
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
          if (ret[0] == LuaNil.Nil) {
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
  }
}
