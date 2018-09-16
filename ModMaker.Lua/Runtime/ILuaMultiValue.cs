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

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines an ordered collection of LuaValues.  This is used
    /// as an argument to methods and as the result.  This is also
    /// a LuaValue and under normal operations, should act as the
    /// first item.
    /// </summary>
    public interface ILuaMultiValue : ILuaValue, IEnumerable<ILuaValue>
    {
        /// <summary>
        /// Gets the value at the given index.  If the index is less
        /// than zero or larger than Count, it should return LuaNil.
        /// This should never return null.  Setting the value outside
        /// the current range has no effect.
        /// </summary>
        /// <param name="index">The index to get.</param>
        /// <returns>The value at the given index, or LuaNil.</returns>
        ILuaValue this[int index] { get; set; }
        /// <summary>
        /// Gets the number of values in this object.
        /// </summary>
        int Count { get; }
        
        /// <summary>
        /// Returns a new object with the same values as this object except
        /// where any extra values are removed and any missing values are
        /// set to null.
        /// </summary>
        /// <param name="number">The number of values to have.</param>
        /// <returns>A new ILuaMultiValue object with the values in this object.</returns>
        ILuaMultiValue AdjustResults(int number);
    }
}
