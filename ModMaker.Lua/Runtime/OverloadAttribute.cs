using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Attach to a method to specify the index of the overload.  The overload
    /// must be positive and cannot conflict with another index.  Any method
    /// that does not have an index will fill in the blank indicies.
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
