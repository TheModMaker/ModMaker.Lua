
namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines a type that modifies how Lua indexes it.  This is similar to
    /// C# DynamicObject.  The methods in this interface are called rather
    /// than looking for members on the type.  This overrides visible members
    /// defined in LuaIgnore but not those for LuaUserData.
    /// </summary>
    public interface IIndexable
    {
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
        void SetIndex(object index, object value);
        /// <summary>
        /// Gets the value of the given index.
        /// </summary>
        /// <param name="index">The index to use, cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">If index is null.</exception>
        /// <exception cref="System.InvalidOperationException">If the current
        /// type does not support getting an index -or- if index is not a valid
        /// value or type.</exception>
        object GetIndex(object index);
    }
}