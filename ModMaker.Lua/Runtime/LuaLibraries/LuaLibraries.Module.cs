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

#nullable enable

namespace ModMaker.Lua.Runtime {
  static partial class LuaStaticLibraries {
    class Module {
      readonly ILuaEnvironment _env;

      public Module(ILuaEnvironment env) {
        if (!(env is ILuaEnvironmentNet)) {
          throw new InvalidOperationException(
              "'require' only works with the NET version of the environment.");
        }
        _env = env;
      }

      public void Initialize() {
        _env.RegisterDelegate((Func<string, ILuaValue>)require, "require");
      }

      ILuaValue require(string name) {
        IModuleBinder bind = ((ILuaEnvironmentNet)_env).ModuleBinder;
        return bind.Load(_env, name);
      }
    }
  }
}
