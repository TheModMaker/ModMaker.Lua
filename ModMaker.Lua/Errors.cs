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

using ModMaker.Lua.Runtime;

namespace ModMaker.Lua
{
    /// <summary>
    /// Defines helper methods to create error strings from the resource
    /// file.
    /// </summary>
    static class Errors
    {
        // TODO: Add more errors and use this more.
        
        /// <summary>
        /// Creates a string when calling on an invalid value.
        /// </summary>
        /// <param name="type">The type of the value.</param>
        /// <returns>An error string.</returns>
        public static string CannotCall(LuaValueType type)
        {
            return string.Format(Resources.CannotCall, type.ToString().ToLower());
        }
        /// <summary>
        /// Creates a string when indexing on an invalid value.
        /// </summary>
        /// <param name="type">The type of the value.</param>
        /// <returns>An error string.</returns>
        public static string CannotIndex(LuaValueType type)
        {
            return string.Format(Resources.CannotIndex, type.ToString().ToLower());
        }
        /// <summary>
        /// Creates a string when performing arithmetic on an invalid value.
        /// </summary>
        /// <param name="type">The type of the value.</param>
        /// <returns>An error string.</returns>
        public static string CannotArithmetic(LuaValueType type)
        {
            return string.Format(Resources.CannotArithmetic, type.ToString().ToLower());
        }
        /// <summary>
        /// Creates a string when enumerating on an invalid value.
        /// </summary>
        /// <param name="type">The type of the value.</param>
        /// <returns>An error string.</returns>
        public static string CannotEnumerate(LuaValueType type)
        {
            return string.Format(Resources.CannotEnumerate, type.ToString().ToLower());
        }
    }
}