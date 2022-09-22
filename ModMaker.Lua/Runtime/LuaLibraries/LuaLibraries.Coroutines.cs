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
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  static partial class LuaStaticLibraries {
    class Coroutine {
      public static void Initialize(ILuaEnvironment env) {
        ILuaTable coroutine = new LuaTable(env);
        Register(env, coroutine, (Func<LuaFunction, ILuaValue>)create);
        Register(env, coroutine, (Func<LuaCoroutine, ILuaValue[], LuaMultiValue>)resume);
        Register(env, coroutine, (Func<object[]>)running);
        Register(env, coroutine, (Func<LuaCoroutine, string>)status);
        Register(env, coroutine, (Func<LuaFunction, object>)wrap);
        Register(env, coroutine, (Func<ILuaValue[], LuaMultiValue>)yield);

        env.GlobalsTable.SetItemRaw(new LuaString("coroutine"), coroutine);
      }

      static ILuaValue create(LuaFunction method) {
        return LuaEnvironment.CurrentEnvironment.Runtime.CreateThread(method);
      }
      static LuaMultiValue resume(LuaCoroutine thread, params ILuaValue[] args) {
        if (thread.Status == LuaCoroutineStatus.Complete)
          return LuaMultiValue.CreateMultiValueFromObj(false, "cannot resume dead coroutine");
        return _pcallInternal(
            () => new LuaMultiValue(LuaBoolean.True, thread.Resume(new LuaMultiValue(args))),
            pcall: true);
      }
      [MultipleReturn]
      static object[] running() {
        LuaCoroutine thread = LuaCoroutine.Current(LuaEnvironment.CurrentEnvironment);
        return new object[] { thread, !thread.IsLua };
      }
      static string status(LuaCoroutine thread) {
        return thread.Status switch {
          LuaCoroutineStatus.Running => "running",
          LuaCoroutineStatus.Yielded => "suspended",
          LuaCoroutineStatus.Waiting => "normal",
          LuaCoroutineStatus.Complete => "dead",
          _ => throw new InvalidOperationException("Invalid thread status"),
        };
      }
      static object wrap(LuaFunction func) {
        var thread = LuaEnvironment.CurrentEnvironment.Runtime.CreateThread(func);
        return (Func<LuaMultiValue, LuaMultiValue>)thread.Resume;
      }
      static LuaMultiValue yield(params ILuaValue[] args) {
        return LuaCoroutine.Yield(new LuaMultiValue(args));
      }
    }
  }
}
