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

using System.Collections.Generic;
using System.Reflection;

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Defines the behavior of Lua code.  These are helper methods that are called from within the
  /// generated code.  These methods are not required if using a different compiler.
  /// </summary>
  public interface ILuaRuntime {
    // TODO: Consider replacing GenericLoop.

    /// <summary>
    /// Gets the Lua thread object for the current thread.
    /// </summary>
    ILuaThread CurrentThread { get; }

    /// <summary>
    /// Starts a generic for loop and returns an enumerator object used to get the values.
    /// </summary>
    /// <param name="args">The input arguments.</param>
    /// <param name="e">The current environment.</param>
    /// <returns>An object used to enumerate over the loop, cannot be null.</returns>
    /// <exception cref="System.ArgumentNullException">If args or E is null.</exception>
    /// <exception cref="System.InvalidOperationException">If the object(s)
    /// cannot be enumerated over.</exception>
    IEnumerable<ILuaMultiValue> GenericLoop(ILuaEnvironment e, ILuaMultiValue args);

    /// <summary>
    /// Creates a new Lua thread that calls the given method.
    /// </summary>
    /// <param name="method">The method to call.</param>
    /// <returns>The new Lua thread object.</returns>
    ILuaThread CreateThread(ILuaValue method);
    /// <summary>
    /// Called when the code encounters the 'class' keyword.  Defines a LuaClass object with the
    /// given name.
    /// </summary>
    /// <param name="impl">The types that the class will derive.</param>
    /// <param name="name">The name of the class.</param>
    /// <exception cref="System.InvalidOperationException">If there is
    /// already a type with the given name -or- if the types are not valid
    /// to derive from (e.g. sealed).</exception>
    /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
    void CreateClassValue(string[] impl, string name);
  }
}
