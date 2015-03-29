using ModMaker.Lua.Parser.Items;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines the behaviour of Lua code.  Defines methods such as resolving
    /// binary operators and conversion between types.
    /// </summary>
    public interface ILuaRuntime
    {
        /// <summary>
        /// Gets the value of an indexer.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="target">The target object.</param>
        /// <param name="index">The indexing object.</param>
        /// <returns>The value of the indexer.</returns>
        /// <exception cref="System.ArgumentNullException">If E or index
        /// is null.</exception>
        /// <exception cref="System.InvalidOperationException">Attenpting to
        /// index an invalid type.</exception>
        /// <exception cref="System.MemberAccessException">If Lua does not have
        /// access to the given index.</exception>
        object GetIndex(ILuaEnvironment E, object target, object index);
        /// <summary>
        /// Sets the value of an indexer.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="target">The target object.</param>
        /// <param name="index">The indexing object.</param>
        /// <param name="value">The value to set to.</param>
        /// <exception cref="System.ArgumentNullException">If E or index
        /// is null.</exception>
        /// <exception cref="System.InvalidOperationException">Attenpting to
        /// index an invalid type -or- if value is not a valid value.</exception>
        /// <exception cref="System.MemberAccessException">If Lua does not have
        /// access to the given index.</exception>
        void SetIndex(ILuaEnvironment E, object target, object index, object value);
        /// <summary>
        /// Collapses any MultipleReturns into a single array of objects.  Also
        /// converts any numbers into a double.  If the initial array is null,
        /// simply return an empty array.  Ensures that the array has at least
        /// the given number of elements.
        /// </summary>
        /// <param name="count">The minimum number of elements, ignored if negative.</param>
        /// <param name="args">The initial array of objects.</param>
        /// <returns>A new array of objects.</returns>
        /// <remarks>
        /// If the number of actual arguments is less than the given count,
        /// append null elements.  If the number of actual elements is more
        /// than count, ignore it.
        /// </remarks>
        object[] FixArgs(object[] args, int count);
        /// <summary>
        /// Attempts to invoke a given object.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="value">The object to invoke.</param>
        /// <param name="args">The arguments passed to the method.</param>
        /// <param name="byRef">Contains the indicies that are passed by-reference.</param>
        /// <param name="overload">The zero-based index of the overload to invoke,
        /// a negative number to ignore.</param>
        /// <param name="memberCall">Whether the function was invoked using member call (:).</param>
        /// <param name="self">The object being called on.</param>
        /// <returns>The return value of the method.</returns>
        /// <exception cref="System.InvalidOperationException">If attempting
        /// to invoke an invalid value.</exception>
        /// <exception cref="System.ArgumentNullException">If E is null.</exception>
        MultipleReturn Invoke(ILuaEnvironment E, object self, object value, int overload, bool memberCall, object[] args, int[] byRef);
        /// <summary>
        /// Determines whether a given object is true according
        /// to Lua.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>False if the object is null or false, otherwise true.</returns>
        bool IsTrue(object value);
        /// <summary>
        /// Tries to convert a given value to a number.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value as a double or null on error.</returns>
        double? ToNumber(object value);
        /// <summary>
        /// Creates a new function from the given method and target objects.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the method, used for errors.</param>
        /// <param name="method">The method to call.</param>
        /// <param name="target">The target object.</param>
        /// <returns>The new function object.</returns>
        /// <exception cref="System.ArgumentNullException">If method, E, or target is null.</exception>
        object CreateFunction(ILuaEnvironment E, string name, MethodInfo method, object target);
        /// <summary>
        /// Creates a new LuaTable object.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <returns>A new LuaTable object.</returns>
        /// <exception cref="System.ArgumentNullException">If E is null.</exception>
        object CreateTable(ILuaEnvironment E);
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
        IEnumerable<MultipleReturn> GenericLoop(ILuaEnvironment E, object[] args);

        /// <summary>
        /// Converts an object to a given type using TypesCompatible.
        /// </summary>
        /// <param name="target">The object to convert.</param>
        /// <param name="type">The type to convert to.</param>
        /// <returns>An object that can be passed in MethodInfo.Invoke.</returns>
        /// <exception cref="System.InvalidCastException">If the type cannot
        /// be converted to the type.</exception>
        /// <exception cref="System.ArgumentNullException">If type is null.</exception>
        object ConvertType(object target, Type type);
        /// <summary>
        /// This is called whenever a binary operation occurs to determine which function to call.
        /// </summary>
        /// <param name="lhs">The left-hand operand.</param>
        /// <param name="type">The type of operation.</param>
        /// <param name="rhs">The right-hand operand.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="System.InvalidOperationException">If the operator is
        /// inaccessible to Lua -or- if the objects are of an invalid type.</exception>
        object ResolveBinaryOperation(object lhs, BinaryOperationType type, object rhs);
        /// <summary>
        /// This is called whenever a unary operation occurs to determine which
        /// function to call.
        /// </summary>
        /// <param name="type">The type of operation.</param>
        /// <param name="target">The target of the operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="System.InvalidOperationException">If the operator is
        /// inaccessible to Lua -or- if the objects are of an invalid type.</exception>
        object ResolveUnaryOperation(UnaryOperationType type, object target);
        /// <summary>
        /// Called when the code encounters the 'class' keyword.  Defines a 
        /// LuaClass object with the given name.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="types">The types that the class will derive.</param>
        /// <param name="name">The name of the class.</param>
        /// <exception cref="System.InvalidOperationException">If there is
        /// already a type with the given name -or- if the types are not valid
        /// to derive from (e.g. sealed).</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        void DefineClass(ILuaEnvironment E, string[] types, string name);
    }
}
