using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua
{
    /// <summary>
    /// Defines that a method will ignore any extra arguments passed to it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    class IgnoreExtraArgumentsAttribute : Attribute
    {
    }
}
