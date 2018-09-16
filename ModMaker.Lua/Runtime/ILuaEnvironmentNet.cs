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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines the environment that Lua operates in.  This type is the same as
    /// ILuaEnvironment, but adds the ability to use coroutines and modules.
    /// This is the interface that should be used if using the NET version of
    /// the runtime.
    /// </summary>
    public interface ILuaEnvironmentNet : ILuaEnvironment
    {
        /// <summary>
        /// Gets or sets the module binder for the environment.  The code
        /// can assume that the value returned is never null; however some
        /// implementations may allow setting to null.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to a null value.</exception>
        IModuleBinder ModuleBinder { get; set; }
    }
}
