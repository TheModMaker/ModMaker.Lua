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
using System.Reflection;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// A method that is defined in Lua.  This simply passes the arguments to the Lua function.
  /// </summary>
  public class LuaDefinedFunction : LuaFunction {
    // TODO: Make dynamic version for proper tail calls.
    /// <summary>
    /// A delegate for Lua defined functions.
    /// </summary>
    /// <param name="env">The current environment.</param>
    /// <param name="args">The arguments to pass.</param>
    /// <returns>The values returned from Lua.</returns>
    protected delegate LuaMultiValue LuaFunc(LuaMultiValue args);
    /// <summary>
    /// The backing Lua defined method.
    /// </summary>
    protected LuaFunc _method;

    /// <summary>
    /// Creates a new LuaDefinedMethod from the given method.
    /// </summary>
    /// <param name="env">The current environment.</param>
    /// <param name="name">The name of the method, used for errors.</param>
    /// <param name="method">The method to invoke.</param>
    /// <param name="target">The target object.</param>
    /// <exception cref="System.ArgumentNullException">If method, E, or target is null.</exception>
    /// <exception cref="System.ArgumentException">If method does not have
    /// the correct method signature:
    /// ILuaMultiValue Method(ILuaEnvironment, ILuaMultiValue)</exception>
    public LuaDefinedFunction(ILuaEnvironment env, string name, MethodInfo method, object target)
        : base(env, name) {
      LuaFunc func = (LuaFunc)Delegate.CreateDelegate(typeof(LuaFunc), target, method);

      _method = func;
    }

    protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
      return _method(args);
    }
  }
}
