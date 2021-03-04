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
    /// Attach to a method to specify the index of the overload.  The overload
    /// must be positive and cannot conflict with another index.  Any method
    /// that does not have an index will fill in the blank indices.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OverloadAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the index of the overload.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Creates a new instance of OverloadAttribute with the given index.
        /// </summary>
        /// <param name="index">The zero-based index of the overload.</param>
        public OverloadAttribute(int index)
        {
            this.Index = index;
        }
    }
}
