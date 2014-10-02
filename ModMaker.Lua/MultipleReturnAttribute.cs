using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua
{
    /// <summary>
    /// Defines that a method returns more than one value.
    /// The method must return a compatible type to
    /// IEnumerable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class MultipleReturnAttribute : Attribute
    {
    }
}
