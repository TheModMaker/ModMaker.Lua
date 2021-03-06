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
    LuaGlobalFunction(ILuaEnvironment env, Type type) : base(type.Name) {
      _type = type;
      _env = env;
    }

    /// <summary>
    /// Creates a new instance of LuaGlobalFunction using the given type.
    /// </summary>
    /// <param name="env">The current environment.</param>
    /// <param name="type">The type to use, must implement ILuaValue.</param>
    /// <returns>A new LuaGlobalFunction object.</returns>
    /// <exception cref="System.ArgumentNullException">If E or type is null.</exception>
    /// <exception cref="System.ArgumentException">If type does not implement
    /// ILuaValue.</exception>
    public static LuaGlobalFunction Create(ILuaEnvironment env, Type type) {
      return new LuaGlobalFunction(env, type);
    }

    /// <summary>
    /// Performs that actual invocation of the method.
    /// </summary>
    /// <param name="target">The object that this was called on.</param>
    /// <param name="memberCall">Whether the call used member call syntax (:).</param>
    /// <param name="args">The current arguments, not null but maybe empty.</param>
    /// <param name="overload">
    /// The overload to chose or negative to do overload resolution.
    /// </param>
    /// <param name="byRef">An array of the indices that are passed by-reference.</param>
    /// <returns>The values to return to Lua.</returns>
    /// <exception cref="System.ArgumentException">If the object cannot be
    /// invoked with the given arguments.</exception>
    /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
    /// valid overloads for the given arguments.</exception>
    /// <exception cref="System.IndexOutOfRangeException">If overload is
    /// larger than the number of overloads.</exception>
    /// <exception cref="System.NotSupportedException">If this object does
    /// not support overloads.</exception>
    protected override ILuaMultiValue _invokeInternal(ILuaValue target, bool memberCall,
                                                      int overload, ILuaMultiValue args) {
      ILuaValue method = (ILuaValue)Activator.CreateInstance(_type, new[] { _env });
      return method.Invoke(LuaNil.Nil, false, -1, args);
    }
  }
}
