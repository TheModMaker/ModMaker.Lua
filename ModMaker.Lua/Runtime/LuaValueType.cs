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

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines the different types of values in Lua.
    /// </summary>
    public enum LuaValueType
    {
        /// <summary>
        /// Represents a 'nil' value.
        /// </summary>
        Nil,
        /// <summary>
        /// Represents a string of characters.
        /// </summary>
        String,
        /// <summary>
        /// Represents a truth value (true/false).
        /// </summary>
        Bool,
        /// <summary>
        /// Represents a table of values.
        /// </summary>
        Table,
        /// <summary>
        /// Represents a Lua function.
        /// </summary>
        Function,
        /// <summary>
        /// Represents a real number.
        /// </summary>
        Number,
        /// <summary>
        /// Represents a Lua thread.
        /// </summary>
        Thread,
        /// <summary>
        /// Represents a user defined type.
        /// </summary>
        UserData,
    }

    /// <summary>
    /// Defines different cast types.  When an object is cast to a
    /// different type, one of the following casts is performed.  This
    /// is used in overload resolution.
    /// </summary>
    public enum LuaCastType
    {
        /// <summary>
        /// There is no cast from the object to the destination type.
        /// </summary>
        NoCast,
        /// <summary>
        /// The object has the same type as the destination type.
        /// </summary>
        SameType,
        /// <summary>
        /// The destination type is an interface.
        /// </summary>
        Interface,
        /// <summary>
        /// The destination type is a base class of this object.
        /// </summary>
        BaseClass,
        /// <summary>
        /// There exists a user-defined implicit cast to the destination type.
        /// </summary>
        UserDefined,
        /// <summary>
        /// There exists a user-defined implicit cast to the destination type.
        /// </summary>
        ExplicitUserDefined,
    }
}
