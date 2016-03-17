using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// An interface for a table in Lua.  This acts like a dictionary of
    /// objects to objects.  This is both indexable and callable.  This supports
    /// a metatable which can have methods that will change the behavior of the
    /// table.
    /// </summary>
    public interface ILuaTable : ILuaValue, IEnumerable<KeyValuePair<ILuaValue, ILuaValue>>
    {
        // TODO: Add contract class.

        /// <summary>
        /// Gets or sets the metatable for the table.
        /// </summary>
        ILuaTable MetaTable { get; set; }

        /// <summary>
        /// Gets the item at the specified key without invoking any
        /// metamethods.
        /// </summary>
        /// <param name="key">The key to get.</param>
        /// <returns>The value at the specified key.</returns>
        ILuaValue GetItemRaw(ILuaValue key);
        /// <summary>
        /// Sets the item at the specified key without invoking any
        /// metamethods.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="value">The value to set the key to.</param>
        void SetItemRaw(ILuaValue key, ILuaValue value);
    }
}