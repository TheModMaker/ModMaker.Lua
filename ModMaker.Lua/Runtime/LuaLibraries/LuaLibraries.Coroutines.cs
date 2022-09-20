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
      public static void Initialize(ILuaEnvironment env) {
        ILuaTable coroutine = new LuaTable();
        Register(env, coroutine, (Func<LuaFunction, ILuaValue>)create);
        Register(env, coroutine, (Func<ILuaThread, ILuaValue[], IEnumerable<ILuaValue>>)resume);
        Register(env, coroutine, (Func<object[]>)running);
        Register(env, coroutine, (Func<ILuaThread, string>)status);
        Register(env, coroutine, (Func<LuaFunction, object>)wrap);
        Register(env, coroutine, (Func<ILuaValue[], LuaMultiValue>)yield);

        env.GlobalsTable.SetItemRaw(new LuaString("coroutine"), coroutine);
      }

      static ILuaValue create(LuaFunction method) {
        return LuaEnvironment.CurrentEnvironment.Runtime.CreateThread(method);
      }
      [MultipleReturn]
      static IEnumerable<ILuaValue> resume(ILuaThread thread, params ILuaValue[] args) {
        if (thread.Status == LuaThreadStatus.Complete)
          return LuaMultiValue.CreateMultiValueFromObj(false, "cannot resume dead coroutine");
        try {
          LuaMultiValue ret = thread.Resume(new LuaMultiValue(args));
          return new[] { LuaBoolean.True }.Concat(ret);
        } catch (Exception e) {
          return LuaMultiValue.CreateMultiValueFromObj(false, e.Message, e);
        }
      }
      [MultipleReturn]
      static object[] running() {
        ILuaThread thread = LuaEnvironment.CurrentEnvironment.Runtime.CurrentThread;
        return new object[] { thread, !thread.IsLua };
      }
      static string status(ILuaThread thread) {
        if (thread.Status == LuaThreadStatus.Complete)
          return "dead";
        else
          return thread.Status.ToString().ToLowerInvariant();
      }
      static object wrap(LuaFunction func) {
        var thread = LuaEnvironment.CurrentEnvironment.Runtime.CreateThread(func);
        return (Func<LuaMultiValue, LuaMultiValue>)thread.Resume;
      }
      static LuaMultiValue yield(params ILuaValue[] args) {
        ILuaThread thread = LuaEnvironment.CurrentEnvironment.Runtime.CurrentThread;
        if (!thread.IsLua) {
          throw new InvalidOperationException("Cannot yield the main thread.");
        }

        return thread.Yield(new LuaMultiValue(args));
      }
    }
  }
}
