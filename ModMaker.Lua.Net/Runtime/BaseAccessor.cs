using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Globalization;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// A Lua runtime object used as a parameter to allow for 'base.foo' syntax 
    /// for instance methods defined in Lua code.
    /// </summary>
    public sealed class BaseAccessor : IIndexable
    {
        object target;
        ILuaEnvironment E;

        /// <summary>
        /// Creates a new instance of BaseAccessor.
        /// </summary>
        /// <param name="obj">The target object, 'self'.</param>
        /// <param name="E">The current environment.</param>
        /// <exception cref="System.ArgumentNullException">If obj or E is null.</exception>
        public BaseAccessor(ILuaEnvironment E, object obj)
        {
            if (E == null)
                throw new ArgumentNullException("E");
            if (obj == null)
                throw new ArgumentNullException("obj");

            this.target = obj;
            this.E = E;
        }

        /// <summary>
        /// Gets the value of the given index.
        /// </summary>
        /// <param name="index">The index to use, cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">If index is null.</exception>
        /// <exception cref="System.InvalidOperationException">If the current
        /// type does not support getting an index -or- if index is not a valid
        /// value or type.</exception>
        public object GetIndex(object index)
        {
            if (index == null)
                throw new ArgumentNullException("index");

            return NetHelpers.GetSetIndex(E, target, index, false, true, true);
        }
        /// <summary>
        /// Sets the value of the given index to the given value.
        /// </summary>
        /// <param name="index">The index to use, cannot be null.</param>
        /// <param name="value">The value to set to, can be null.</param>
        /// <exception cref="System.ArgumentNullException">If index is null.</exception>
        /// <exception cref="System.InvalidOperationException">If the current
        /// type does not support setting an index -or- if index is not a valid
        /// value or type -or- if value is not a valid value or type.</exception>
        /// <exception cref="System.MemberAccessException">If Lua does not have
        /// access to the given index.</exception>
        public void SetIndex(object index, object value)
        {
            if (index == null)
                throw new ArgumentNullException("index");

            NetHelpers.GetSetIndex(E, target, index, false, false, true, value);
        }
    }
}