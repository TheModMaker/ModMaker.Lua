using ModMaker.Lua.Runtime;

namespace ModMaker.Lua
{
    /// <summary>
    /// Defines helper methods to create error strings from the resource
    /// file.
    /// </summary>
    static class Errors
    {
        // TODO: Add more errors and use this more.
        
        /// <summary>
        /// Creates a string when calling on an invalid value.
        /// </summary>
        /// <param name="type">The type of the value.</param>
        /// <returns>An error string.</returns>
        public static string CannotCall(LuaValueType type)
        {
            return string.Format(Resources.CannotCall, type.ToString().ToLower());
        }
        /// <summary>
        /// Creates a string when indexing on an invalid value.
        /// </summary>
        /// <param name="type">The type of the value.</param>
        /// <returns>An error string.</returns>
        public static string CannotIndex(LuaValueType type)
        {
            return string.Format(Resources.CannotIndex, type.ToString().ToLower());
        }
        /// <summary>
        /// Creates a string when performing arithmetic on an invalid value.
        /// </summary>
        /// <param name="type">The type of the value.</param>
        /// <returns>An error string.</returns>
        public static string CannotArithmetic(LuaValueType type)
        {
            return string.Format(Resources.CannotArithmetic, type.ToString().ToLower());
        }
        /// <summary>
        /// Creates a string when enumerating on an invalid value.
        /// </summary>
        /// <param name="type">The type of the value.</param>
        /// <returns>An error string.</returns>
        public static string CannotEnumerate(LuaValueType type)
        {
            return string.Format(Resources.CannotEnumerate, type.ToString().ToLower());
        }
    }
}