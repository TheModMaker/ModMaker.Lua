using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua
{
    /// <summary>
    /// Attach to an type definition or member to make it not visible to Lua code.  This overrides the members specified
    /// in <see cref="ModMaker.Lua.ReturnInfo"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Enum | AttributeTargets.Event |
        AttributeTargets.Field | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Struct,
        AllowMultiple = false, Inherited = false)]
    public sealed class LuaIgnoreAttribute : Attribute
    {

    }
}
