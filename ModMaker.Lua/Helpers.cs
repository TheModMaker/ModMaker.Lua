// Copyright 2014 Jacob Trimble
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ModMaker.Lua
{
    /// <summary>
    /// A static class that contains several helper methods.
    /// </summary>
    static class Helpers
    {
        static Action<Exception> preserveStackTrace_;

        static Helpers()
        {
            MethodInfo preserveStackTrace = typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);
            preserveStackTrace_ = (Action<Exception>)Delegate.CreateDelegate(typeof(Action<Exception>), preserveStackTrace);            
        }

        /// <summary>
        /// Helper class that calls a given method when Dispose is called.
        /// </summary>
        sealed class DisposableHelper : IDisposable
        {
            Action act;
            bool _disposed = false;

            public DisposableHelper(Action act)
            {
                this.act = act;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                act();
            }
        }
        
        /// <summary>
        /// Defines information about a method overload.
        /// </summary>
        /// <typeparam name="T">Either MethodInfo or ConstructorInfo</typeparam>
        /// <remarks>
        /// When checking if an argument is compatible with the formal parameter,
        /// a number is given to represent how it needs to be converted so it
        /// can be passed.  This value is stored in the respective index of
        /// ConversionAmounts.  The indicies refer to the formal parameters
        /// and do not include optional parameters or params arrays.
        /// 
        /// There is a similar array called ConversionTypes that contains the
        /// types of conversions that happen for each formal parameter.
        /// 
        /// For the following comments, I will use 'parameter' to refer to the
        /// formal parameter (i.e. the type in the method definition) and
        /// 'argument' to refer to the object that is passed to the method. I
        /// will also use the following type hierarchy:
        /// <![CDATA[I <- A <- B <- C]]>
        /// Where I is an interface and A, B, and C are classes.
        /// 
        /// If the argument is of type C and the parameter is of type B, then 
        /// the amount is 1.  If the parameter is of type A, then the amount is
        /// 2.  All other casts have an amount of 0 and a different type in 
        /// ConversionTypes.
        /// 
        /// When determining which overload to chose, each overload gets it's
        /// own OverloadInfo and then they are all compared with each other
        /// using Compare until one remains.  If the only remaing
        /// OverloadInfo are always equal (Compare returns 0), then
        /// an AmbiguousMatchException is thrown.
        /// 
        /// This assumes that when comparing, both overloads are valid with the
        /// given arguments and they both had originaly the same arguments.
        /// 
        /// An overload that has fewer arguments added/removed through optional
        /// parameters or params arrays (stored in ParamsOrOptional) is 
        /// considered better than one with more.
        /// 
        /// Foo(int, int=12, int=12)
        /// Foo(int, int)
        /// 
        /// When called, Foo(12, 23) will chose the second one because it has
        /// fewer arguments added due to optional parameters.
        /// 
        /// Foo(int, int, int)
        /// Foo(params int[])
        /// 
        /// When called Foo(42, 42, 42) will chose the first one because it has
        /// more explicit parameters.
        /// 
        /// When choosing between optional parameters and a params array, this
        /// will choose the optional parameters one.
        /// 
        /// Otherwise the ConversionAmount is iterated over.  If for both
        /// overloads the arguments are implicitly cast to the parameter type,
        /// then the difference of the two methods is added to a counter value.
        /// If this counter is positive, then the first overload is better
        /// because the arguemnt types more closely resemble the parameter 
        /// types.  If the number is zero, then they are the same.
        /// 
        /// User-defined explicit casts operate the same way, except that there
        /// is a seperate counter of the number of times it ocurs in each 
        /// overload and if the two overloads are the same, the one with fewer
        /// explicit casts is chosen.
        /// 
        /// If one overload has an interface in it's definition, then it's 
        /// behavior is different. If both define interfaces at different
        /// position than the other, then the result is ambiguous and returns
        /// 0.  If only one defines an interface, then the other method must
        /// be chosen by the first counter, otherwise the result is ambiguous.
        /// The first counter is not affected by this parameter.
        /// 
        /// Consider the call to Foo(C, C), the following definitins are:
        /// 
        /// Foo(I, A) and Foo(A, A), the second one is chosen because A is more
        /// specific than I.
        /// 
        /// Foo(I, A) and Foo(A, B), the second one is chosen because B is more
        /// specific than A.
        /// 
        /// Foo(I, B) and Foo(A, A), ambiguous because the method with the 
        /// interface is more specific.
        /// 
        /// Foo(I, B) and Foo(A, I), ambiguous because they both define 
        /// interfaces.
        /// 
        /// Foo(I, A) and Foo(I, I), the first one is chosen because A is more
        /// specific than I.  Note that becuase they both define the interface
        /// then that parameter is ignored.
        /// </remarks>
        sealed class OverloadInfo<T> where T : MethodBase
        {
            /// <summary>
            /// Creates a new OverloadInfo object from the given method.
            /// </summary>
            /// <param name="method">The method that this is representing.</param>
            /// M<param name="target">The 'this' argument of the method.</param>
            /// <param name="args">The arguments being passed to the method.</param>
            OverloadInfo(T method, object target, ILuaMultiValue args, int[] amounts, 
                LuaCastType[] types, int paramsOrOptional, int luaValueCount, bool isParams)
            {
                this.Method = method;
                this.Target = target;
                this.Arguments = args;

                this.ConversionAmounts = amounts;
                this.ConversionTypes = types;
                this.ParamsOrOptional = paramsOrOptional;
                this.LuaValueCount = luaValueCount;
                this.IsParams = isParams;
            }

            /// <summary>
            /// Contains the arguments that are passed to the method.
            /// </summary>
            public readonly ILuaMultiValue Arguments;
            /// <summary>
            /// Contains the method info for this overload.
            /// </summary>
            public readonly T Method;
            /// <summary>
            /// Contains the target of this overload.
            /// </summary>
            public readonly object Target;

            /// <summary>
            /// Descibes how each argument is converted to work as an argument.
            /// See class remarks.
            /// </summary>
            public readonly int[] ConversionAmounts;
            /// <summary>
            /// Describes the cast type for each argument.  See class remarks.
            /// </summary>
            public readonly LuaCastType[] ConversionTypes;
            /// <summary>
            /// Contains the number of arguments added due to a params array
            /// or optional parameters.  This is always positive and greater
            /// than 1 if it is a params array.
            /// </summary>
            public readonly int ParamsOrOptional;
            /// <summary>
            /// Contains the number of parameters that are of type ILuaValue,
            /// an overload that has a larger number is more desireable.
            /// </summary>
            public readonly int LuaValueCount;
            /// <summary>
            /// If ParamsOrOptional is non-zero, then this represents whether
            /// the number represents a params array or optional arguments.
            /// True for params array; false for optional arguments.
            /// </summary>
            public readonly bool IsParams;

            /// <summary>
            /// Creates a new OverloadInfo object from the given method.
            /// </summary>
            /// <param name="method">The method that this is representing.</param>
            /// M<param name="target">The 'this' argument of the method.</param>
            /// <param name="args">The arguments being passed to the method.</param>
            /// <returns>A new OverloadInfo object; or null if the overload is
            /// not valid.</returns>
            public static OverloadInfo<T> Create(T method, object target, ILuaMultiValue args)
            {
                // Ignore any methods marked with LuaIgnore.
                if (method.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0)
                    return null;
                // If the backing method is marked with LuaIgnore, ignore it
                if (method is MethodInfo && target != null)
                {
                    MethodInfo methodBase = ((MethodInfo)(object)method).GetBaseDefinition();
                    if (target.GetType().GetMethods()
                        .Where(m => m.GetBaseDefinition() == methodBase)
                        .Any(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0))
                    {
                        return null;
                    }
                }

                var param = method.GetParameters();
                bool isParams = false;
                int paramsOrOptional = 0;

                int luaValueCount = 0;
                var max = Math.Min(param.Length, args.Count);
                var conversionAmounts = new int[max];
                var conversionTypes = new LuaCastType[max];

                if (param.Length == 1 && param[0].ParameterType == typeof(ILuaMultiValue))
                    return new OverloadInfo<T>(method, target, args, conversionAmounts,
                        conversionTypes, paramsOrOptional, luaValueCount, isParams);

                // Provide support for a params array.
                if (param.Length > 0 && param[param.Length - 1].GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0)
                {
                    isParams = true;
                    paramsOrOptional = args.Count - param.Length;

                    // If there are extra arguments, handle them here.
                    if (args.Count >= param.Length)
                    {
                        max--;
                        conversionAmounts[max] = 0;
                        conversionTypes[max] = LuaCastType.SameType;
                    }
                    else
                        paramsOrOptional = 0;

                    // Check that each extra argument can be cast to the array type.
                    Type arrayType = param[param.Length - 1].ParameterType.GetElementType();
                    MethodInfo getCastInfo = typeof(ILuaValue).GetMethod("GetCastInfo").MakeGenericMethod(arrayType);
                    bool check = args
                        .Skip(param.Length - 1)
                        .All(luaValue =>
                        {
                            LuaCastType cast;
                            int i;
                            GetCastInfo(getCastInfo, luaValue, arrayType, out cast, out i);
                            conversionAmounts[max] = Math.Max(conversionAmounts[max], i);
                            conversionTypes[max] = CombineType(conversionTypes[max], cast);
                            return cast != LuaCastType.NoCast;
                        });
                    if (!check || args.Count < param.Length - 1)
                        return null;
                }
                // Provide support for optional arguments.
                else
                {
                    isParams = false;
                    paramsOrOptional = param.Length - args.Count;

                    // Check that any missing parameters are all optional.
                    bool check = param.Skip(args.Count).All(p => p.IsOptional);
                    bool ignoreExtra = method.GetCustomAttribute<IgnoreExtraArgumentsAttribute>() != null;
                    if (!check || (paramsOrOptional < 0 && !ignoreExtra))
                        return null;
                    paramsOrOptional = Math.Max(0, paramsOrOptional);
                }

                // Check each parameter.
                for (int i = 0; i < max; i++)
                {
                    bool nullable = false;
                    Type destType = param[i].ParameterType;
                    if (param[i].ParameterType.IsByRef)
                        destType = destType.GetElementType();
                    if (destType.IsGenericType &&
                        destType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        nullable = true;
                        destType = destType.GetGenericArguments()[0];
                    }

                    var value = args[i].GetValue();
                    if (value == null)
                    {
                        // Cannot pass null to value type
                        if (!nullable && destType.IsValueType)
                            return null;
                        // If the argument is ILuaValue, make sure it is compatible with LuaNil.
                        if (typeof(ILuaValue).IsAssignableFrom(destType) && 
                                !destType.IsAssignableFrom(typeof(LuaNil)))
                            return null;

                        conversionAmounts[i] = 0;
                        conversionTypes[i] = LuaCastType.SameType;
                    }
                    else
                    {
                        Type argType = value.GetType();

                        // If the parameter is a ILuaValue type, use the container type.
                        if (typeof(ILuaValue).IsAssignableFrom(destType))
                        {
                            luaValueCount++;
                            argType = args[i].GetType();
                        }

                        MethodInfo m;
                        conversionTypes[i] = TypesCompatible(argType, destType, out m, out conversionAmounts[i]);
                        if (conversionTypes[i] == LuaCastType.NoCast)
                            return null;
                    }
                }

                return new OverloadInfo<T>(method, target, args, conversionAmounts, 
                    conversionTypes, paramsOrOptional, luaValueCount, isParams);
            }

            /// <summary>
            /// Compares the current overload and the given one.  This assumes
            /// the same arguments are given to both overloads.  This is the
            /// same semantics as IComparable.  The return alue is given by
            /// the following table:
            /// 
            /// 0     | They are equivilent.
            /// &gt;0 | This object is the better overload.
            /// &lt;0 | The other one is the better overload.
            /// </summary>
            /// <param name="other">The other overload to compare to.</param>
            /// <returns>A value that defines which overload is better.</returns>
            public int Compare(OverloadInfo<T> other)
            {
                int ret;
                if (CompareFormalParameters(other, out ret))
                    return ret;

                // Favor optional arguments over params arrays.
                if (ParamsOrOptional != 0 && IsParams != other.IsParams)
                {
                    if (!IsParams)
                        return 1;
                    else
                        return -1;
                }
                // Favor one with more explicit parameters.
                else if (ParamsOrOptional != other.ParamsOrOptional)
                {
                    return other.ParamsOrOptional - ParamsOrOptional;
                }

                return 0;
            }

            /// <summary>
            /// Compares the given overload info based on its formal parameters.
            /// </summary>
            /// <param name="other">The other object to compare.</param>
            /// <param name="result">How this and the given value compare.</param>
            /// <returns>True if this resolves the overload; otherwise Compare
            /// needs to do more comparison.</returns>
            bool CompareFormalParameters(OverloadInfo<T> other, out int result)
            {
                result = 0;
                int explicits = 0;
                int dif = 0;
                int force = 0;

                int max = Math.Min(ConversionAmounts.Length, other.ConversionAmounts.Length);
                for (int i = 0; i < max; i++)
                {
                    if (ConversionTypes[i] == LuaCastType.Interface &&
                        other.ConversionTypes[i] == LuaCastType.Interface)
                    {
                        // Both interfaces, this has no effect
                    }
                    else if (ConversionTypes[i] == LuaCastType.Interface)
                    {
                        dif--;

                        // If it's an interface, we need to force it.
                        if (force >= 0)
                            force = 1;
                        else
                            return true; // Both define interfaces, ambiguous.
                    }
                    else if (other.ConversionTypes[i] == LuaCastType.Interface)
                    {
                        dif++;

                        if (force <= 0)
                            force = -1;
                        else
                            return true; // Both define interfaces, ambiguous.
                    }
                    else if (ConversionTypes[i] == LuaCastType.ExplicitUserDefined &&
                        other.ConversionTypes[i] == LuaCastType.ExplicitUserDefined)
                    {
                        // Both define explicit casts, this has no effect.
                    }
                    else if (ConversionTypes[i] == LuaCastType.ExplicitUserDefined)
                    {
                        explicits++;
                        dif += other.ConversionAmounts[i] - 1;
                    }
                    else if (other.ConversionTypes[i] == LuaCastType.ExplicitUserDefined)
                    {
                        explicits--;
                        dif += 1 - ConversionAmounts[i];
                    }
                    else
                    {
                        dif += (other.ConversionAmounts[i] - ConversionAmounts[i]);
                    }
                }

                if (force > 0)
                {
                    // First defines an interface so only the second one can be chosen or it's ambiguous.
                    result = Math.Min(dif, 0);
                    return true;
                }
                else if (force < 0)
                {
                    // Second defines an interface so only the first one can be chosen or it's ambiguous.
                    result = Math.Max(dif, 0);
                    return true;
                }
                else if (dif != 0)  
                {
                    result = dif;
                    return true;
                }
                else if (explicits != 0)
                {
                    result = -explicits;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Combines two cast types.  This is used in params arrays to
            /// determine the total cast type.
            /// </summary>
            /// <param name="typeA">The first cast type.</param>
            /// <param name="typeB">The second cast type.</param>
            /// <returns>The combined cast type.</returns>
            static LuaCastType CombineType(LuaCastType typeA, LuaCastType typeB)
            {
                if (typeB == LuaCastType.NoCast)
                    return LuaCastType.NoCast;

                switch (typeA)
                {
                    case LuaCastType.NoCast:
                        return LuaCastType.NoCast;
                    case LuaCastType.SameType:
                        return typeB;
                    case LuaCastType.Interface:
                        return typeB;
                    case LuaCastType.BaseClass:
                        if (typeB == LuaCastType.UserDefined || typeB == LuaCastType.ExplicitUserDefined)
                            return typeB;
                        else
                            return typeA;
                    case LuaCastType.UserDefined:
                        return typeA;
                    case LuaCastType.ExplicitUserDefined:
                        if (typeB == LuaCastType.UserDefined)
                            return typeB;
                        else
                            return typeA;
                    default:
                        throw new NotImplementedException();
                }
            }
            /// <summary>
            /// Gets the cast info for the given object.  This will try to use
            /// the delegate; if that fails, it will use the Helpers method.
            /// </summary>
            /// <param name="getCastInfo">The delegate to get the cast info.</param>
            /// <param name="target">The object to get the cast info for.</param>
            /// <param name="destType">The destination type to cast to.</param>
            /// <param name="castType">Will contain the cast type used.</param>
            /// <param name="amount">Will contain the amount of the cast.</param>
            static void GetCastInfo(MethodInfo getCastInfo, ILuaValue target, Type destType, out LuaCastType castType, out int amount)
            {
                try
                {
                    // These are out args, so the initial value can be null 
                    // since the real value is undefined.
                    object[] args = new object[2];
                    getCastInfo.Invoke(target, args);

                    // Out arguments modify the args array.
                    castType = (LuaCastType)args[0];
                    amount = (int)args[1];
                }
                catch (NotSupportedException)
                {
                    // If not supported, simply forward to TypesCompatible.
                    object value = target.GetValue();
                    if (value == null)
                    {
                        amount = 0;
                        castType = destType.IsValueType ? LuaCastType.NoCast : LuaCastType.SameType;
                    }
                    else
                    {
                        MethodInfo m;
                        castType = TypesCompatible(value.GetType(), destType, out m, out amount);
                    }
                }
            }
        }
        
        /// <summary>
        /// Creates an IDisposable object that calls the given funcrion when
        /// Dispose is called.
        /// </summary>
        /// <param name="act">The function to call on Dispose.</param>
        /// <returns>An IDisposable object.</returns>
        public static IDisposable Disposable(Action act)
        {
            return new DisposableHelper(act);
        }

        /// <summary>
        /// Retrieves a custom attribute applied to a member of a type. Parameters specify
        /// the member, and the type of the custom attribute to search for.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element">An object derived from the System.Reflection.MemberInfo class that describes
        /// a constructor, event, field, method, or property member of a class.</param>
        /// <returns>A reference to the single custom attribute of type attributeType that is
        /// applied to element, or null if there is no such attribute.</returns>
        /// <exception cref="System.ArgumentNullException">element is null.</exception>
        /// <exception cref="System.NotSupportedException">element is not a constructor, method, property, event, type, or field.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">More than one of the requested attributes was found.</exception>
        /// <exception cref="System.TypeLoadException">A custom attribute type cannot be loaded.</exception>
        public static T GetCustomAttribute<T>(this MemberInfo element) where T : Attribute
        {
            return (T)Attribute.GetCustomAttribute(element, typeof(T));
        }
        /// <summary>
        /// Retrieves a custom attribute applied to a member of a type. Parameters specify
        /// the member, and the type of the custom attribute to search for.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element">An object derived from the System.Reflection.MemberInfo class that describes
        /// a constructor, event, field, method, or property member of a class.</param>
        /// <returns>A reference to the single custom attribute of type attributeType that is
        /// applied to element, or null if there is no such attribute.</returns>
        /// <exception cref="System.ArgumentNullException">element is null.</exception>
        /// <exception cref="System.NotSupportedException">element is not a constructor, method, property, event, type, or field.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">More than one of the requested attributes was found.</exception>
        /// <exception cref="System.TypeLoadException">A custom attribute type cannot be loaded.</exception>
        public static T GetCustomAttribute<T>(this MemberInfo element, bool inherit) where T : Attribute
        {
            return (T)Attribute.GetCustomAttribute(element, typeof(T), inherit);
        }

        /// <summary>
        /// Converts the values in the multi-value to the given type.
        /// </summary>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <param name="args">The arguments to convert.</param>
        /// <param name="start">The index to start at.</param>
        /// <returns>An array of the arguments.</returns>
        public static T[] As<T>(this ILuaMultiValue args, int start)
        {
            return args.Skip(start).Select(a => a.As<T>()).ToArray();
        }
        /// <summary>
        /// Converts the values in the multi-value to the given type.
        /// </summary>
        /// <param name="type">The type to convert to.</param>
        /// <param name="args">The arguments to convert.</param>
        /// <param name="start">The index to start at.</param>
        /// <returns>An array of the arguments.</returns>
        public static object As(this ILuaMultiValue args, int start, Type type)
        {
            MethodInfo as_ = typeof(Helpers)
                .GetMethod(nameof(Helpers.As), new[] { typeof(ILuaMultiValue), typeof(int) });
            MethodInfo asGeneric = as_.MakeGenericMethod(type);
            Func<ILuaMultiValue, int, object> convert = 
                (Func<ILuaMultiValue, int, object>)Delegate.CreateDelegate(
                    typeof(Func<ILuaMultiValue, int, object>), asGeneric);
            return convert(args, start);
        }

        /// <summary>
        /// Checks whether two types are compatible and gets a conversion
        /// method if it can be.
        /// </summary>
        /// <param name="sourceType">The type of the original object.</param>
        /// <param name="destType">The type trying to convert to.</param>
        /// <param name="method">Will contains the resulting conversion method.
        /// This method will be static</param>
        /// <param name="amount">Will contain the conversion amount, see OverloadInfo.</param>
        /// <returns>The cast type that is used.</returns>
        public static LuaCastType TypesCompatible(Type sourceType, Type destType, out MethodInfo method, out int amount)
        {
            method = null;
            amount = 0;

            if (sourceType == destType)
            {
                return LuaCastType.SameType;
            }

            if (destType.IsGenericType &&
                destType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // If the destination is nullable, simply convert as the underlying type.
                destType = destType.GetGenericArguments()[0];
            }

            // NOTE: This only checks for derived classes and interfaces, this will not work for
            // implicit/explicit casts.
            if (destType.IsAssignableFrom(sourceType))
            {
                if (destType.IsInterface)
                    return LuaCastType.Interface;
                else
                {
                    while (destType != sourceType)
                    {
                        amount++;
                        sourceType = sourceType.BaseType;
                    }
                    return LuaCastType.BaseClass;
                }
            }

            amount = 1;

            // All numeric types are explicitly compatible but do not define a cast in their type.
            if ((destType != typeof(bool) && destType != typeof(IntPtr) && destType != typeof(UIntPtr) && destType.IsPrimitive) &&
                (sourceType != typeof(bool) && sourceType != typeof(IntPtr) && sourceType != typeof(UIntPtr) && sourceType.IsPrimitive))
            {
                // Although they are compatible, they need to be converted, get the
                // Convert.ToXX method.
                method = typeof(Convert).GetMethod("To" + destType.Name, new Type[] { sourceType });
                return LuaCastType.BaseClass;
            }

            // Get any methods from source type that is not marked with LuaIgnoreAttribute and has
            // the name 'op_Explicit' or 'op_Implicit' and has a return type of the destination
            // type and a sole argument that is implicitly compatible with the source type.
            LuaIgnoreAttribute attr = sourceType.GetCustomAttributes(typeof(LuaIgnoreAttribute), true)
                .Select(o => (LuaIgnoreAttribute)o).FirstOrDefault();
            var possible = sourceType.GetMethods()
                .Where(m => 
                    (m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0) &&
                    (attr == null || attr.IsMemberVisible(sourceType, m.Name)) &&
                    (m.Name == "op_Explicit" || m.Name == "op_Implicit") && 
                    (m.ReturnType == destType) &&
                    (m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(sourceType)))
                .ToArray();

            // Check for a cast in the destination type
            attr = destType.GetCustomAttributes(typeof(LuaIgnoreAttribute), true)
                .Select(o => (LuaIgnoreAttribute)o).FirstOrDefault();
            possible = possible
                .Union(destType.GetMethods()
                    .Where(m => 
                        (m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0) &&
                        (attr == null || attr.IsMemberVisible(destType, m.Name)) &&
                        (m.Name == "op_Explicit" || m.Name == "op_Implicit") && 
                        (m.ReturnType == destType) &&
                        (m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(sourceType))))
                .ToArray();

            // check the possible choices
            if (possible.Length > 0)
            {
                for (int i = 0; i < possible.Length; i++)
                {
                    method = possible[i];
                    if (possible[i].Name == "op_implicit")
                        return LuaCastType.UserDefined;
                }
                return LuaCastType.ExplicitUserDefined;
            }

            return LuaCastType.NoCast;
        }
        /// <summary>
        /// Searches the given methods for an overload that will work with the 
        /// given arguments.
        /// </summary>
        /// <typeparam name="T">The type of the method base (e.g. MethodInfo 
        /// or ConstructorInfo).</typeparam>
        /// <param name="methods">The possible method choices.</param>
        /// <param name="args">The arguments to check.</param>
        /// <param name="resultMethod">Where the resulting method will be placed.</param>
        /// <param name="resultTarget">Where the respective target will be placed.</param>
        /// <returns>True if a compatible method was found, otherwise false.</returns>
        /// <exception cref="System.ArgumentNullException">If methods, or targets is null.</exception>
        /// <exception cref="System.ArgumentException">If the length of targets
        /// does not match the length of methods and is not one.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there
        /// is two methods that match the given arguments.</exception>
        public static bool GetCompatibleMethod<T>(IEnumerable<Tuple<T, object>> methods, ILuaMultiValue args,
            out T resultMethod, out object resultTarget) where T : MethodBase
        {
            resultMethod = null;
            resultTarget = null;

            OverloadInfo<T> min = null;
            bool ambiguous = false;

            foreach (var method in methods)
            {
                var cur = OverloadInfo<T>.Create(method.Item1, method.Item2, args);
                if (min == null)
                    min = cur;
                else if (cur != null)
                {
                    int diff = cur.Compare(min);
                    if (diff > 0)
                    {
                        ambiguous = false;
                        min = cur;
                    }
                    else if (diff == 0)
                        ambiguous = true;
                }
            }

            if (ambiguous)
                throw new AmbiguousMatchException();
            else if (min != null)
            {
                resultMethod = min.Method;
                resultTarget = min.Target;
                return true;
            }
            else
                return false;
        }
        /// <summary>
        /// Converts the given arguments so they can be passed to the given method.  It assumes the
        /// arguments are valid.
        /// </summary>
        /// <param name="args">The arguments to convert.</param>
        /// <param name="method">The method to call.</param>
        /// <returns>The arguments as they can be passed to the given method.</returns>
        public static object[] ConvertForArgs(ILuaMultiValue args, MethodBase method)
        {
            var param = method.GetParameters();

            var ret = new object[param.Length];
            var min = Math.Min(param.Length, args.Count);
            var rootMethod = typeof(ILuaValue).GetMethod(nameof(ILuaValue.As));

            bool hasParams = param.Length > 0 &&
                param[param.Length - 1].GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0;

            if (param.Length == 1 && param[0].ParameterType == typeof(ILuaMultiValue))
                return new object[] { args };

            // Convert formal parameters.
            for (int i = 0; i < min; i++)
            {
                // Skip params array since it's handled below.
                if (i == param.Length - 1 && hasParams)
                    continue;

                var paramType = param[i].ParameterType;
                if (paramType.IsByRef)
                    paramType = paramType.GetElementType();

                if (typeof(ILuaValue).IsAssignableFrom(paramType))
                {
                    ret[i] = args[i];
                }
                else
                {
                    var asMethod = rootMethod.MakeGenericMethod(paramType);
                    ret[i] = asMethod.Invoke(args[i], null);
                }
            }

            // Get optional parameters.
            for (int i = min; i < param.Length; i++)
            {
                ret[i] = param[i].DefaultValue;
            }

            // Get params array.
            if (hasParams)
            {
                Type arrayType = param[param.Length - 1].ParameterType.GetElementType();
                int start = param.Length - 1;
                ret[param.Length - 1] = As(args, start, arrayType);
            }

            return ret;
        }
        
        /// <summary>
        /// Invokes the given method while throwing the inner exception.  This ensures that the
        /// TargetInvocationException is not thrown an instead the inner exception is thrown.
        /// </summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="target">The target of the invocation.</param>
        /// <param name="args">The arguments to pass to the method.</param>
        /// <returns>Any value returned from the method.</returns>
        public static object DynamicInvoke(MethodBase method, object target, object[] args)
        {
            // See: http://stackoverflow.com/a/1663549
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException e)
            {
                Exception inner = e.InnerException;
                if (inner == null)
                    throw;

                preserveStackTrace_(inner);
                throw inner;
            }
        }

        /// <summary>
        /// Gets or sets the given object to the given value.  Also handles accessibility correctly.
        /// </remarks>
        /// <param name="targetType">The type of the target object.</param>
        /// <param name="target">The target object.</param>
        /// <param name="index">The indexing object.</param>
        /// <param name="value">The value to set to.</param>
        /// <returns>The value for get or null if setting.</returns>
        /// <exception cref="System.InvalidOperationException">If the target type
        /// does not define an accessible index or member -or- if index is
        /// not of a valid type or value -or- if attempt to set a method.</exception>
        public static ILuaValue GetSetMember(Type targetType, object target, ILuaValue index, 
                                             ILuaValue value = null)
        {
            // TODO: Consider how to get settings here.
            /*if (!E.Settings.AllowReflection &&
                typeof(MemberInfo).IsAssignableFrom(targetType))
            {
                // TODO: Move to resources.
                throw new InvalidOperationException(
                    "Lua does not have access to reflection.  See LuaSettings.AllowReflection.");
            }*/

            if (index.ValueType == LuaValueType.Number || index.ValueType == LuaValueType.Table)
            {
                if (target == null)
                {
                    throw new InvalidOperationException(
                        "Attempt to call indexer on a static type.");
                }

                ILuaMultiValue args;
                if (index.ValueType == LuaValueType.Number)
                {
                    args = new LuaMultiValue(new[] { value });
                }
                else
                {
                    int len = index.Length().As<int>();
                    object[] objArgs = new object[len];
                    for (int i = 1; i <= len; i++)
                    {
                        ILuaValue item = index.GetIndex(LuaValueBase.CreateValue(i));
                        if (item.ValueType == LuaValueType.Table)
                        {
                            throw new InvalidOperationException(
                                "Arguments to indexer cannot be a table.");
                        }
                        objArgs[i - 1] = item;
                    }

                    args = LuaMultiValue.CreateMultiValueFromObj(objArgs);
                }

                return GetSetIndex(targetType, target, args, value);
            }
            else if (index.ValueType == LuaValueType.String)
            {
                // Allow for specifying an overload to methods
                string name = index.As<string>();
                int over = -1;
                if (name.Contains("`"))
                {
                    if (!int.TryParse(name.Substring(name.IndexOf('`') + 1), out over))
                        throw new InvalidOperationException(
                            "Only numbers are allowed after the grave(`) when specifying an overload.");

                    name = name.Substring(0, name.IndexOf('`'));
                }

                // Find all visible members with the given name
                MemberInfo[] members = targetType.GetMember(name)
                    .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0)
                    .ToArray();
                // TODO: Implement accessibility.
                //if (Base == null || Base.Length == 0 ||
                //    (userData != null && !userData.IsMemberVisible(name)) ||
                //    (ignAttr != null && !ignAttr.IsMemberVisible(type, name)))
                //    throw new InvalidOperationException("'" + name + "' is not a visible member of type '" + type + "'.");

                return GetSetValue(members, over, target, value);
            }
            else
                throw new InvalidOperationException("Indices of a User-Defined type must be a string, number, or table.");
        }

        static ILuaValue GetSetValue(MemberInfo[] members, int over, object target, 
                                     ILuaValue value = null)
        {
            // Perform the action on the given member.  Although this only checks the first
            // member, the only type that can return more than one with the same name is
            // a method and can only be other methods.
            if (members.Length == 0)
                return LuaNil.Nil;

            FieldInfo field = members[0] as FieldInfo;
            PropertyInfo property = members[0] as PropertyInfo;
            MethodInfo method = members[0] as MethodInfo;
            if (field != null)
            {
                if (over != -1)
                    throw new InvalidOperationException(
                        "Cannot specify an overload when accessing a field.");

                if (value == null)
                {
                    return LuaValueBase.CreateValue(field.GetValue(target));
                }
                else
                {
                    // Must try to convert the given type to the requested type.  This will use
                    // both implicit and explicit casts for user-defined types by default, SetValue
                    // only works if the backing type is the same as or derrived from the
                    // FieldType.  It does not even support implicit numerical conversion
                    var convert =
                        typeof(ILuaValue).GetMethod(nameof(ILuaValue.As)).MakeGenericMethod(field.FieldType);
                    field.SetValue(target, convert.Invoke(value, null));
                    return null;
                }
            }
            else if (property != null)
            {
                if (over != -1)
                    throw new InvalidOperationException(
                        "Cannot specify an overload when accessing a field.");

                if (value == null)
                {
                    MethodInfo meth = property.GetGetMethod();
                    if (meth == null)
                    {
                        throw new InvalidOperationException(
                            "The property '" + property.Name + "' is write-only.");
                    }
                    // TODO: Implement accessibility.
                    /*if (meth.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0 ||
                        (userData != null && !userData.IsMemberVisible("get_" + name)) ||
                        (ignAttr != null && !ignAttr.IsMemberVisible(type, "get_" + name)))
                        throw new InvalidOperationException("The get method for property '" + name + "' is inaccessible to Lua.");*/

                    return LuaValueBase.CreateValue(method.Invoke(target, null));
                }
                else
                {
                    MethodInfo meth = property.GetSetMethod();
                    if (meth == null)
                    {
                        throw new InvalidOperationException(
                            "The property '" + property.Name + "' is read-only.");
                    }
                    // TODO: Implement accessibility.
                    /*if (meth.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0 ||
                        (userData != null && !userData.IsMemberVisible("set_" + name)) ||
                        (ignAttr != null && !ignAttr.IsMemberVisible(type, "set_" + name)))
                        throw new InvalidOperationException("The set method for property '" + name + "' is inaccessible to Lua.");*/

                    var convert =
                        typeof(ILuaValue).GetMethod("As").MakeGenericMethod(property.PropertyType);
                    property.SetValue(target, convert.Invoke(value, null), null);
                    return null;
                }
            }
            else if (method != null)
            {
                if (value != null)
                    throw new InvalidOperationException("Cannot set the value of a method.");
                if (method.IsSpecialName)
                    throw new InvalidOperationException(
                        "Cannot call special method '" + method.Name + "'.");

                return new LuaOverloadFunction(method.Name, new[] { method }, new[] { target });
            }
            else
                throw new InvalidOperationException("Unrecognized member type " + members[0]);
        }

        /// <summary>
        /// Gets or sets the given index to the given value.
        /// </remarks>
        /// <param name="targetType">The type of the target object.</param>
        /// <param name="target">The target object, or null for static access.</param>
        /// <param name="index">The indexing object.</param>
        /// <param name="value">The value to set to.</param>
        /// <returns>The value for get or value if setting.</returns>
        /// <exception cref="System.InvalidOperationException">If the target type
        /// does not define an accessible index or member -or- if index is
        /// not of a valid type or value -or- if attemt to set a method.</exception>
        static ILuaValue GetSetIndex(Type targetType, object target, ILuaMultiValue indicies, 
                                     ILuaValue value = null)
        {
            // Arrays do not actually define an 'Item' method so we need to access the indexer
            // directly.
            Array targetArray = target as Array;
            if (targetArray != null)
            {
                // Convert the arguments to long.
                int[] args = new int[indicies.Count];
                for (int i = 0; i < indicies.Count; i++)
                {
                    if (indicies[i].ValueType == LuaValueType.Number)
                    {
                        // TODO: Move to resources.
                        throw new InvalidOperationException(
                            "Arguments to indexer for an array can only be numbers.");
                    }
                    else
                        args[i] = indicies[i].As<int>();
                }

                if (value == null)
                {
                    return LuaValueBase.CreateValue(targetArray.GetValue(args));
                }
                else
                {
                    // Convert to the array type.
                    Type arrayType = targetArray.GetType().GetElementType();
                    object valueObj = typeof(ILuaValue).GetMethod(nameof(ILuaValue.As))
                        .MakeGenericMethod(arrayType).Invoke(value, null);

                    targetArray.SetValue(valueObj, args);
                    return value;
                }
            }

            // Setting also requires the last arg be the 'value'
            if (value != null)
            {
                indicies = indicies.AdjustResults(indicies.Count + 1);
                indicies[indicies.Count - 1] = value;
            }

            // find the valid method
            string name = targetType == typeof(string) ? "Chars" : "Item";
            MethodInfo method;
            object methodTarget;
            GetCompatibleMethod(
                targetType.GetMethods()
                    .Where(m => m.Name == (value == null ? "get_" + name : "set_" + name))
                    .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0)
                    .Select(m => Tuple.Create(m, (object)target)), 
                indicies, out method, out methodTarget);

            if (method == null)
            {
                // TODO: Move to resources.
                throw new InvalidOperationException("Unable to find a visible indexer that " +
                    "matches the provided arguments for type '" + target.GetType() + "'.");
            }

            if (value == null)
            {
                return LuaValueBase.CreateValue(method.Invoke(target,
                    indicies.Select(v => v.GetValue()).ToArray()));
            }
            else
            {
                method.Invoke(target, indicies.Select(v => v.GetValue()).ToArray());
                return value;
            }
        }
    }
}