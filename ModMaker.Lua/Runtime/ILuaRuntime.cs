using ModMaker.Lua.Parser.Items;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines the behavior of Lua code.  These are helper methods that are called from within
    /// the generated code.  These methods are not required if using a different compiler.
    /// </summary>
    public interface ILuaRuntime
    {
        // TODO: Consider replacing GenericLoop.

        /// <summary>
        /// Gets the Lua thread object for the current thread.
        /// </summary>
        ILuaThread CurrentThread { get; }

        /// <summary>
        /// Starts a generic for loop and returns an enumerator object used to
        /// get the values.
        /// </summary>
        /// <param name="args">The input arguments.</param>
        /// <param name="E">The current environment.</param>
        /// <returns>An object used to enumerate over the loop, cannot be null.</returns>
        /// <exception cref="System.ArgumentNullException">If args or E is null.</exception>
        /// <exception cref="System.InvalidOperationException">If the object(s)
        /// cannot be enumerated over.</exception>
        IEnumerable<ILuaMultiValue> GenericLoop(ILuaEnvironment E, ILuaMultiValue args);
        
        /// <summary>
        /// Creates a new ILuaValue object that wraps the given C# value.
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        /// <returns>A new ILuaValue object.</returns>
        ILuaValue CreateValue(object value);
        /// <summary>
        /// Creates a new ILuaMultiValue object that contains the given values.
        /// </summary>
        /// <param name="values">The values it contains.</param>
        /// <returns>A new ILuaMultiValue object.</returns>
        ILuaMultiValue CreateMultiValue(params ILuaValue[] values);
        /// <summary>
        /// Creates a new ILuaMultiValue object that contains the given values.
        /// </summary>
        /// <param name="values">The values it contains.</param>
        /// <returns>A new ILuaMultiValue object.</returns>
        ILuaMultiValue CreateMultiValueFromObj(params object[] values);
        /// <summary>
        /// Creates a new Lua thread that calls the given method.
        /// </summary>
        /// <param name="method">The method to call.</param>
        /// <returns>The new Lua thread object.</returns>
        ILuaThread CreateThread(ILuaValue method);
        /// <summary>
        /// Creates a new LuaTable object.
        /// </summary>
        /// <returns>A new LuaTable object.</returns>
        ILuaTable CreateTable();
        /// <summary>
        /// Creates a new ILuaValue object that will call the given method.
        /// The calling convention of this method and the type of the object
        /// are implementation-defined; however none of the arguments are null.
        /// </summary>
        /// <param name="name">The name of the function.</param>
        /// <param name="method">The MethodInfo that defines the function.</param>
        /// <param name="target">The 'this' object for the function.</param>
        /// <returns>A new ILuaValue object.</returns>
        ILuaValue CreateImplementationFunction(string name, MethodInfo method, object target);
        /// <summary>
        /// Called when the code encounters the 'class' keyword.  Defines a 
        /// LuaClass object with the given name.
        /// </summary>
        /// <param name="impl">The types that the class will derive.</param>
        /// <param name="name">The name of the class.</param>
        /// <exception cref="System.InvalidOperationException">If there is
        /// already a type with the given name -or- if the types are not valid
        /// to derive from (e.g. sealed).</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        void CreateClassValue(string[] impl, string name);
    }
}