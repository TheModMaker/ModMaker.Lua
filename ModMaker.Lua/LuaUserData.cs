using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua
{
    /// <summary>
    /// When the return type of a method, will create a UserData with
    /// the specified options.
    /// </summary>
    public sealed class ReturnInfo
    {
        /// <summary>
        /// Creates a new ReturnInfo object.
        /// </summary>
        /// <param name="members">The string members that Lua will have access to.</param>
        /// <param name="values">The value(s) to pass to Lua.</param>
        public ReturnInfo(string[] members, params object[] values)
        {
            this.Values = values;
            this.Full = false;
            this.Members = members;
        }
        /// <summary>
        /// Creates a new ReturnInfo object.
        /// </summary>
        /// <param name="full">True to give full access to the members, otherwise false.</param>
        /// <param name="values">The value(s) to pass to Lua.</param>
        public ReturnInfo(bool full, params object[] values)
        {
            this.Values = values;
            this.Full = full;
            this.Members = null;
        }

        /// <summary>
        /// Gets or sets the values to return.
        /// </summary>
        public object[] Values { get; set; }
        /// <summary>
        /// Gets or sets whether Lua will have full access to the members of the returned values.
        /// </summary>
        public bool Full { get; set; }
        /// <summary>
        /// Gets or sets the members that Lua will have access to, can be null.
        /// </summary>
        public string[] Members { get; set; }

        internal MultipleReturn CreateReturn()
        {
            List<object> ret = new List<object>();
            foreach (var item in Values)
                ret.Add(new LuaUserData(item, Full ? null : (Members ?? new string[0]), true));

            return new MultipleReturn(ret);
        }
    }

    class LuaUserData
    {
        object _value;
        string[] members;

        public LuaUserData(object value, string[] members, bool pass)
        {
            this._value = value;
            this.members = members;
            this.Pass = pass;
        }

        public object Value { get { return _value; } }
        public string[] Members { get { return members; } }
        public bool Pass { get; private set; }
    }
}
