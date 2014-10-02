
namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines a type that modifies how Lua invokes it.  This is similar to
    /// C# DynamicObject.  This is needed to be able to invoke any type.  If
    /// a type does not implement this interface, it cannot be invoked.
    /// </summary>
    public interface IMethod
    {
        /// <summary>
        /// Gets or sets the current environment.
        /// </summary>
        ILuaEnvironment Environment { get; set; }

        /// <summary>
        /// Invokes the current object with the given arguments.
        /// </summary>
        /// <param name="args">The current arguments, can be null or empty.</param>
        /// <returns>The arguments to return to Lua.</returns>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <exception cref="System.ArgumentNullException">If E is null.</exception>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        MultipleReturn Invoke(int[] byRef, object[] args);
        /// <summary>
        /// Invokes the current object with the given arguments.
        /// </summary>
        /// <param name="args">The current arguments, can be null or empty.</param>
        /// <param name="overload">The zero-based index of the overload to invoke;
        /// if negative, use normal overload resolution.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <returns>The arguments to return to Lua.</returns>
        /// <exception cref="System.ArgumentNullException">If E is null.</exception>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        /// <exception cref="System.IndexOutOfRangeException">If overload is
        /// larger than the number of overloads.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support overloads.</exception>
        /// <remarks>
        /// If this object does not support overloads, you still need to write
        /// this method to work with negative indicies, however you should throw
        /// an exception if zero or positive.  This method is always the one
        /// invoked by the default runtime.
        /// 
        /// It is sugested that the other method simply call this one with -1
        /// as the overload index.
        /// </remarks>
        MultipleReturn Invoke(int overload, int[] byRef, object[] args);
    }
}
