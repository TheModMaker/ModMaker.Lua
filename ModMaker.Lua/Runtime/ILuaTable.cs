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

using System.Collections.Generic;

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// An interface for a table in Lua.  This acts like a dictionary of objects to objects.  This is
  /// both indexable and callable.  This supports a metatable which can have methods that will
  /// change the behavior of the table.
  /// </summary>
  public interface ILuaTable : ILuaValue, IEnumerable<KeyValuePair<ILuaValue, ILuaValue>> {
    /// <summary>
    /// Gets or sets the metatable for the table.
    /// </summary>
    ILuaTable? MetaTable { get; set; }

    /// <summary>
    /// Gets the item at the specified key without invoking any metamethods.
    /// </summary>
    /// <param name="key">The key to get.</param>
    /// <returns>The value at the specified key.</returns>
    ILuaValue GetItemRaw(ILuaValue key);
    /// <summary>
    /// Sets the item at the specified key without invoking any metamethods.
    /// </summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to set the key to.</param>
    void SetItemRaw(ILuaValue key, ILuaValue value);
  }
}
