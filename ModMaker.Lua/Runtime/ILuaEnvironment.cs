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

using ModMaker.Lua.Compiler;
using ModMaker.Lua.Parser;
using System;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines the environment that Lua operates in.  It is suggested that the
    /// type that implements this interface should derrive from DynamicObject 
    /// to allow for dynamic member access.
    /// </summary>
    /// <remarks>
    /// The type that implements this interface should either be marked
    /// with LuaIgnore or some of it's members should be marked with that so
    /// members like CodeCompiler cannot be directly accessed in Lua code.  
    /// Unless a registered delegate returns the value, Lua code cannot get 
    /// access to the current environment directly.
    /// </remarks>
    [LuaIgnore]
    public interface ILuaEnvironment
    {
        /// <summary>
        /// Gets the settings of the environment.
        /// </summary>
        LuaSettings Settings { get; }
        /// <summary>
        /// Gets the globals table for the environment.  This can never return
        /// a null value.
        /// </summary>
        ILuaTable GlobalsTable { get; }
        /// <summary>
        /// Gets or sets the runtime that Lua code will execute in.  This
        /// framework assumes that the value returned is never null.  Some
        /// implementations may support setting to null.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to a null value.</exception>
        ILuaRuntime Runtime { get; set; }
        /// <summary>
        /// Gets or sets the code compiler for the environment.  This framework
        /// assumes that the value returned is never null.  Some implementations
        /// may support setting to null.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to a null value.</exception>
        ICodeCompiler CodeCompiler { get; set; }
        /// <summary>
        /// Gets or sets the parser for the environment.  This framework assumes
        /// that the value returned is never null.  Some implementations may 
        /// support setting to null.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to a null value.</exception>
        IParser Parser { get; set; }

        /// <summary>
        /// Gets or sets the global value with the specified name.
        /// </summary>
        /// <param name="name">The name of the global variable.</param>
        /// <returns>The value of the variable.</returns>
        ILuaValue this[string name] { get; set; }

        /// <summary>
        /// Registers a delegate to the globals table.
        /// </summary>
        /// <param name="method">The delegate to register.</param>
        /// <param name="name">The name of the delegate.</param>
        /// <exception cref="System.ArgumentException">If there is already an 
        /// object registered with that name.</exception>
        /// <exception cref="System.ArgumentNullException">If d or name is null.</exception>
        void RegisterDelegate(Delegate method, string name);
        /// <summary>
        /// Registers a type with the globals table.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="name">The name of the type.</param>
        /// <exception cref="System.ArgumentException">If there is already an 
        /// object registered with that name.</exception>
        /// <exception cref="System.ArgumentNullException">If t or name is null.</exception>
        void RegisterType(Type type, string name);
    }
}