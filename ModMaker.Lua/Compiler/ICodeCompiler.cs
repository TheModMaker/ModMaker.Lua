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

using ModMaker.Lua.Parser;
using ModMaker.Lua.Runtime;
using System;

namespace ModMaker.Lua.Compiler
{
    /// <summary>
    /// This object is in charge of compiling code into invokable objects.  It
    /// will compile an IParseItem tree into an IMethod.  It will also create
    /// a Delegate object for a given IMethod.  It also has the option of
    /// saving the compiled code to disk.
    /// </summary>
    public interface ICodeCompiler
    {
        /// <summary>
        /// Compiles an IParseItem tree indo an IModule object so that it can
        /// be executed.
        /// </summary>
        /// <param name="name">The name to given the module, can be null to
        /// auto-generate.</param>
        /// <param name="E">The current environment.</param>
        /// <param name="item">The item to compile.</param>
        /// <returns>A compiled version of the object.</returns>
        /// <exception cref="System.ArgumentNullException">If E or item is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is
        /// syntax errors in the item tree.</exception>
        ILuaValue Compile(ILuaEnvironment E, IParseItem item, string name);
        /// <summary>
        /// Creates a delegate that can be called to call the given ILuaValue.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="type">The type of the delegate.</param>
        /// <param name="method">The method to call.</param>
        /// <returns>A delegate that is used to call the method.</returns>
        /// <exception cref="System.ArgumentException">If type is not a delegate
        /// type.</exception>
        /// <exception cref="System.ArgumentNullException">If any argument is null.</exception>
        /// <exception cref="System.NotSupportedException">If this implementation
        /// does not support created delegates.</exception>
        Delegate CreateDelegate(ILuaEnvironment E, Type type, ILuaValue method);
    }
}
