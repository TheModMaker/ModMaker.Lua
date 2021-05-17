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
using ModMaker.Lua.Parser;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Compiler {
  /// <summary>
  /// This object is in charge of compiling code into invokable objects.  It will compile an
  /// IParseItem tree into an IMethod.  It will also create a Delegate object for a given IMethod.
  /// </summary>
  public interface ICodeCompiler {
    /// <summary>
    /// Compiles an IParseItem tree into an IModule object so that it can
    /// be executed.
    /// </summary>
    /// <param name="name">The name to given the module, can be null to auto-generate.</param>
    /// <param name="env">The current environment.</param>
    /// <param name="item">The item to compile.</param>
    /// <returns>A compiled version of the object.</returns>
    ILuaValue Compile(ILuaEnvironment env, IParseItem item, string name);
  }
}
