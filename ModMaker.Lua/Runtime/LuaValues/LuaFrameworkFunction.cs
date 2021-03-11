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

using System.Linq;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// One of the methods that are defined by this framework.  The methods will subclass this type.
  /// </summary>
  public abstract class LuaFrameworkFunction : LuaFunction {
    /// <summary>
    /// Creates a new instance of LuaFrameworkMethod.
    /// </summary>
    /// <param name="env">The current environment.</param>
    /// <param name="name">The name of the method.</param>
    protected LuaFrameworkFunction(ILuaEnvironment env, string name) : base(name) {
      _environment = env;
    }

    /// <summary>
    /// Gets the current environment.
    /// </summary>
    protected ILuaEnvironment _environment { get; }

    /// <summary>
    /// Does the actual work of invoking the method.
    /// </summary>
    /// <param name="args">The arguments that were passed to the method, never null.</param>
    /// <returns>The values returned by this method.</returns>
    protected abstract ILuaMultiValue _invokeInternal(ILuaMultiValue args);

    protected override ILuaMultiValue _invokeInternal(ILuaValue target, bool methodCall,
                                                     ILuaMultiValue args) {
      if (methodCall) {
        args = new LuaMultiValue(new[] { target }.Concat(args).ToArray());
      }

      return _invokeInternal(args);
    }
  }
}
