// Copyright 2016 Jacob Trimble
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

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// A global Lua function for a chunk of code.
  /// </summary>
  public sealed class LuaGlobalFunction : LuaFunction {
    /// <summary>
    /// The dynamic Lua type.
    /// </summary>
    readonly Type _type;
    /// <summary>
    /// The current environment.
    /// </summary>
    readonly ILuaEnvironment _env;

    /// <summary>
    /// Creates a new instance of LuaChunk with the given backing type.
    /// </summary>
    /// <param name="env">The current environment.</param>
    /// <param name="type">The generated type, must implement IMethod.</param>
    public LuaGlobalFunction(ILuaEnvironment env, Type type) : base(type.Name) {
      _type = type;
      _env = env;
    }

    public override LuaMultiValue Invoke(ILuaValue target, bool memberCall, LuaMultiValue args) {
      ILuaValue method = (ILuaValue)Activator.CreateInstance(_type, new[] { _env })!;
      return method.Invoke(LuaNil.Nil, false, args);
    }
  }
}
