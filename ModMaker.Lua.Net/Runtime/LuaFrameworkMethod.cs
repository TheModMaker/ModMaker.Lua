using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// One of the methods that are defined by this framework.  The methods
    /// will subclass this type.
    /// </summary>
    public abstract class LuaFrameworkMethod : LuaMethod
    {
        /// <summary>
        /// Creates a new instance of LuaFrameworkMethod.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the method.</param>
        public LuaFrameworkMethod(ILuaEnvironment E, string name)
            : base(E, name) { }

        /// <summary>
        /// Does the actual work of invoking the method.
        /// </summary>
        /// <param name="args">The arguments that were passed to the method,
        /// never null.</param>
        /// <returns>The values returned by this method.</returns>
        protected abstract MultipleReturn InvokeInternal(object[]/*!*/ args);

        /// <summary>
        /// Performs that actual invokation of the method.
        /// </summary>
        /// <param name="args">The current arguments, not null but maybe empty.</param>
        /// <param name="overload">The overload to chose or negative to do 
        /// overload resoltion.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <returns>The values to return to Lua.</returns>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        /// <exception cref="System.IndexOutOfRangeException">If overload is
        /// larger than the number of overloads.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support overloads.</exception>
        protected override MultipleReturn InvokeInternal(int overload, int[] byRef, object[] args)
        {
            return InvokeInternal(args);
        }
    }
}
