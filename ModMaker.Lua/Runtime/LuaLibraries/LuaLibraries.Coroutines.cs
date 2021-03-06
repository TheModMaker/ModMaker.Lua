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
using System.Linq;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  static partial class LuaStaticLibraries {
    class Coroutine {
      readonly ILuaEnvironment _env;

      public Coroutine(ILuaEnvironment E) {
        _env = E;
      }

      public void Initialize() {
        ILuaTable coroutine = new LuaTable();
        Register(_env, coroutine, (Func<ILuaValue, ILuaValue>)create);
        Register(_env, coroutine, (Func<ILuaThread, ILuaValue[], IEnumerable<ILuaValue>>)resume);
        Register(_env, coroutine, (Func<object[]>)running);
        Register(_env, coroutine, (Func<ILuaThread, string>)status);
        Register(_env, coroutine, (Func<ILuaValue, object>)wrap);
        Register(_env, coroutine, (Func<ILuaValue[], LuaMultiValue>)yield);

        _env.GlobalsTable.SetItemRaw(new LuaString("coroutine"), coroutine);
      }

      ILuaValue create(ILuaValue method) {
        if (method.ValueType != LuaValueType.Function) {
          throw new ArgumentException(
              "First argument to function 'coroutine.create' must be a function.");
        }

        return _env.Runtime.CreateThread(method);
      }
      [MultipleReturn]
      IEnumerable<ILuaValue> resume(ILuaThread thread, params ILuaValue[] args) {
        if (thread == null) {
          throw new ArgumentNullException();
        }
        try {
          LuaMultiValue ret = thread.Resume(new LuaMultiValue(args));
          return new[] { LuaBoolean.True }.Concat(ret);
        } catch (Exception e) {
          if (e.Message == "Cannot resume a dead thread.") {
            return LuaMultiValue.CreateMultiValueFromObj(false, "cannot resume dead coroutine");
          } else {
            return LuaMultiValue.CreateMultiValueFromObj(false, e.Message, e);
          }
        }
      }
      [MultipleReturn]
      object[] running() {
        ILuaThread thread = _env.Runtime.CurrentThread;
        return new object[] { thread, !thread.IsLua };
      }
      [IgnoreExtraArguments]
      string status(ILuaThread thread) {
        if (thread.Status == LuaThreadStatus.Complete)
          return "dead";
        else
          return thread.Status.ToString().ToLowerInvariant();
      }
      object wrap(ILuaValue func) {
        if (func.ValueType != LuaValueType.Function) {
          throw new ArgumentException(
              "First argument to function 'coroutine.wrap' must be a function.");
        }

        var thread = _env.Runtime.CreateThread(func);
        return (Func<LuaMultiValue, LuaMultiValue>)thread.Resume;
      }
      LuaMultiValue yield(params ILuaValue[] args) {
        ILuaThread thread = _env.Runtime.CurrentThread;
        if (!thread.IsLua) {
          throw new InvalidOperationException("Cannot yield the main thread.");
        }

        return thread.Yield(new LuaMultiValue(args));
      }
    }
  }
}
