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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime.LuaValues
{
    /// <summary>
    /// One of the methods that are defined by this framework.  The methods
    /// will subclass this type.
    /// </summary>
    public abstract class LuaFrameworkFunction : LuaFunction
    {
        /// <summary>
        /// Creates a new instance of LuaFrameworkMethod.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the method.</param>
        protected LuaFrameworkFunction(ILuaEnvironment E, string name)
            : base(name)
        {
            Environment = E;
        }

        /// <summary>
        /// Gets the current environment.
        /// </summary>
        protected ILuaEnvironment Environment { get; private set; }

        /// <summary>
        /// Does the actual work of invoking the method.
        /// </summary>
        /// <param name="args">The arguments that were passed to the method,
        /// never null.</param>
        /// <returns>The values returned by this method.</returns>
        protected abstract ILuaMultiValue InvokeInternal(ILuaMultiValue args);

        /// <summary>
        /// Performs that actual invokation of the method.
        /// </summary>
        /// <param name="target">The object that this was called on.</param>
        /// <param name="memberCall">Whether the call used member call syntax (:).</param>
        /// <param name="args">The current arguments, not null but maybe empty.</param>
        /// <param name="overload">The overload to chose or negative to do
        /// overload resoltion.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <returns>The values to return to Lua.</returns>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        /// <exception cref="System.IndexOutOfRangeException">If overload is
        /// larger than the number of overloads.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support overloads.</exception>
        protected override ILuaMultiValue InvokeInternal(ILuaValue target, bool methodCall, int overload, ILuaMultiValue args)
        {
            if (methodCall) args = new LuaMultiValue(new[] { target }.Concat(args).ToArray());
            return InvokeInternal(args);
        }
    }
}
