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

    /// <summary>
    /// Performs that actual invocation of the method.
    /// </summary>
    /// <param name="target">The object that this was called on.</param>
    /// <param name="methodCall">Whether the call used member call syntax (:).</param>
    /// <param name="args">The current arguments, not null but maybe empty.</param>
    /// <param name="overload">
    /// The overload to chose or negative to do overload resolution.
    /// </param>
    /// <returns>The values to return to Lua.</returns>
    /// <exception cref="System.ArgumentException">If the object cannot be
    /// invoked with the given arguments.</exception>
    /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
    /// valid overloads for the given arguments.</exception>
    /// <exception cref="System.IndexOutOfRangeException">If overload is
    /// larger than the number of overloads.</exception>
    /// <exception cref="System.NotSupportedException">If this object does
    /// not support overloads.</exception>
    protected override ILuaMultiValue _invokeInternal(ILuaValue target, bool methodCall,
                                                     int overload, ILuaMultiValue args) {
      if (methodCall) {
        args = new LuaMultiValue(new[] { target }.Concat(args).ToArray());
      }

      return _invokeInternal(args);
    }
  }
}
