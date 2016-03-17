using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines the environment that Lua operates in.  This type is the same as
    /// ILuaEnvironment, but adds the ability to use coroutines and modules.
    /// This is the interface that should be used if using the NET version of
    /// the runtime.
    /// </summary>
    public interface ILuaEnvironmentNet : ILuaEnvironment
    {
        /// <summary>
        /// Gets or sets the module binder for the environment.  The code
        /// can assume that the value returned is never null; however some
        /// implementations may allow setting to null.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to a null value.</exception>
        IModuleBinder ModuleBinder { get; set; }
    }
}