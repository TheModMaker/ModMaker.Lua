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

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Defines the different types of values in Lua.
  /// </summary>
  public enum LuaValueType {
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
}
