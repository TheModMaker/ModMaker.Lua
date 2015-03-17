using ModMaker.Lua.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace ModMaker.Lua
{
    /// <summary>
    /// A static class that contains several helper methods.
    /// </summary>
    static class NetHelpers
    {
        /// <summary>
        /// The global module builder object.
        /// </summary>
        static ModuleBuilder _mb = null;
        /// <summary>
        /// A type id number for no-conflict naming.
        /// </summary>
        static int _tid = 1;

        /// <summary>
        /// Returns an IEnumerable&lt;T&gt; object that will enumerate over the
        /// first enumerable and then the other one.
        /// </summary>
        /// <typeparam name="T">The type of the enumerable.</typeparam>
        /// <param name="item">The first enumerable to enumerate over.</param>
        /// <param name="other">The other enumerable to enumerate over.</param>
        /// <returns>An IEnumerable&lt;T&gt; object that will enumerate over
        /// the two enumerables.</returns>
        public static IEnumerable<T> Then<T>(this IEnumerable<T> item, IEnumerable<T> other)
        {
            if (item == null)
                throw new ArgumentNullException("item");
            if (other == null)
                throw new ArgumentNullException("other");

            foreach (var i in item)
                yield return i;
            foreach (var i in other)
                yield return i;
        }
        /// <summary>
        /// Converts the given enumerable over bytes to a base-16 string of
        /// the object, (e.g. "1463E5FF").
        /// </summary>
        /// <param name="item">The enumerable to get data from.</param>
        /// <returns>The enumerable as a string.</returns>
        public static string ToStringBase16(this IEnumerable<byte> item)
        {
            StringBuilder ret = new StringBuilder();
            foreach (var i in item)
                ret.Append(i.ToString("X2", CultureInfo.InvariantCulture));

            return ret.ToString();
        }
        /// <summary>
        /// Creates an array of the given type and stores it in a returned local.
        /// </summary>
        /// <param name="gen">The generator to inject the code into.</param>
        /// <param name="type">The type of the array.</param>
        /// <param name="size">The size of the array.</param>
        /// <returns>A local builder that now contains the array.</returns>
        public static LocalBuilder CreateArray(this ILGenerator gen, Type type, int size)
        {
            var ret = gen.DeclareLocal(type.MakeArrayType());
            gen.Emit(OpCodes.Ldc_I4, size);
            gen.Emit(OpCodes.Newarr, type);
            gen.Emit(OpCodes.Stloc, ret);
            return ret;
        }
        /// <summary>
        /// Reads a number from a text reader.
        /// </summary>
        /// <param name="input">The input to read from.</param>
        /// <returns>The number read.</returns>
        public static double ReadNumber(TextReader input)
        {
            StringBuilder build = new StringBuilder();

            int c = input.Peek();
            int l = c;
            bool hex = false;
            CultureInfo ci = CultureInfo.CurrentCulture;
            if (c == '0')
            {
                input.Read();
                c = input.Peek();
                if (c == 'x' || c == 'X')
                {
                    input.Read();
                    hex = true;
                }
            }

            while (c != -1 && (char.IsNumber((char)c) || (hex && ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))) ||
                (!hex && (c == ci.NumberFormat.NumberDecimalSeparator[0] || c == '-' || (l != '.' && c == 'e')))))
            {
                input.Read();
                build.Append((char)c);
                l = c;
                c = input.Peek();
            }

            return double.Parse(build.ToString(), ci);
        }
        /// <summary>
        /// Gets the global module builder object for types that are generated
        /// by the framework and never saved to disk.
        /// </summary>
        /// <returns>A ModuleBuilder object to generate code with.</returns>
        public static ModuleBuilder GetModuleBuilder()
        {
            if (_mb == null)
            {
                var ab = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("DynamicAssembly"), AssemblyBuilderAccess.Run);
                _mb = ab.DefineDynamicModule("DynamicAssembly.dll");
            }
            return _mb;
        }
        /// <summary>
        /// Defines a new type in the glboal module builder with the given prefix.
        /// </summary>
        /// <param name="prefix">The name prefix of the type.</param>
        /// <returns>The newly created type.</returns>
        public static TypeBuilder DefineGlobalType(string prefix)
        {
            return _mb.DefineType(prefix + "_" + (_tid++));
        }
        /// <summary>
        /// Converts the given type to a Lua-safe type.  This means that it
        /// will convert any numeric types to double.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>A Lua-safe type.</returns>
        public static Type GetLuaSafeType(Type type)
        {
            if (type.IsPrimitive && type != typeof(IntPtr) && type != typeof(UIntPtr) && type != typeof(bool))
                return typeof(double);
            else
                return type;
        }

        /// <summary>
        /// Creates a new Method definition in the given Type that has the same
        /// definition as the given method.
        /// </summary>
        /// <param name="name">The name of the new method.</param>
        /// <param name="tb">The type to define the method.</param>
        /// <param name="otherMethod">The other method definition.</param>
        /// <returns>A new method clone.</returns>
        public static MethodBuilder CloneMethod(TypeBuilder tb, string name, MethodInfo otherMethod)
        {
            var attr = otherMethod.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.NewSlot);
            var param = otherMethod.GetParameters();
            Type[] paramType = new Type[param.Length];
            Type[][] optional = new Type[param.Length][];
            Type[][] required = new Type[param.Length][];
            for (int i = 0; i < param.Length; i++)
            {
                paramType[i] = param[i].ParameterType;
                optional[i] = param[i].GetOptionalCustomModifiers();
                required[i] = param[i].GetRequiredCustomModifiers();
            }

            return tb.DefineMethod(name, attr, otherMethod.CallingConvention,
                otherMethod.ReturnType, otherMethod.ReturnParameter.GetRequiredCustomModifiers(),
                otherMethod.ReturnParameter.GetOptionalCustomModifiers(), paramType, required, optional);
        }


        /// <summary>
        /// Defines information about a method overload.
        /// </summary>
        /// <typeparam name="T">The type of the method.</typeparam>
        /// <remarks>
        /// This information is filled in by GetOverloadInfo based on the given
        /// arguments.  The arguments stored in this variable are the ones
        /// after any needed casting or adding/removing values.
        /// 
        /// When checking if an argument is compatible with the formal parameter,
        /// a number is given to represent how it needs to be converted so it
        /// can be passed.  This value is stored in the respective index of
        /// ConversionAmount.  The indicies refer to the formal parameters
        /// (i.e. the method definition) and do not include optional
        /// parameters or params arrays.
        /// 
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
        /// 2.  If the parameter is an interface, then the amount is always -1.
        /// If there is a user-defined implicit cast to the parameter type, 
        /// then the amount is 1.  If there is a user-defined explicit cast to
        /// the type, then amount is -2. If the  parameter and the argument are
        /// both numbers, then the amount is always 1 no matter the types.
        /// 
        /// When determining which overload to chose, each overload gets it's
        /// own OverloadInfo and then they are all compared with eachother
        /// using GetBetterOverload until one remains.  If the only remaing
        /// OverloadInfo always are equal (GetBetterOverload returns 0), then
        /// an AmbiguousMatchException is thrown.
        /// 
        /// This assumes that when comparing, both overloads are valid with the
        /// given arguments and they both had originaly the same arguments.
        /// 
        /// An overload that has fewer arguments added/removed through optional
        /// parameters or params arrays (stored in ParamsOrOptional) is 
        /// considered better than one with more.  This means that the first
        /// one has more explicit parameters:
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
        /// If one overload has optional parameters and one has a params array,
        /// then the one with a optional parameters is chosen.
        /// 
        /// Otherwise the ConversionAmount is iterated over.  If for both
        /// overloads the arguments are implicitly cast to the parameter type,
        /// then the difference of the two methods is added to a counter value.
        /// If this counter is positive, then the first overload is better
        /// because the arguemnt types more closely resember the parameter 
        /// types.  If the number is zero, then they are the same.
        /// 
        /// User-defined explicit casts operate the same way, except that there
        /// is a seperate counter of the number of times it ocurs in each 
        /// overload and if the two overloads are the same, the one with fewer
        /// explicit casts is chosen.
        /// 
        /// If one overload has an interface in it's definition, then it's 
        /// behaviour is different. If both define interfaces at different
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
            /// Creates a new OberloadInfo object with the given initial values.
            /// </summary>
            /// <param name="method">The method that this is representing.</param>
            /// <param name="target">The target object that this is representing.</param>
            /// <param name="count">The number of formal parameters.</param>
            public OverloadInfo(T method, object target, int count)
            {
                this.Method = method;
                this.Target = target;
                this.ConversionAmount = new int[count];
            }

            /// <summary>
            /// Contains the resulting arguments that should be passed to this
            /// method.
            /// </summary>
            public object[] Args;
            /// <summary>
            /// Contains the method info for this overload.
            /// </summary>
            public T Method;
            /// <summary>
            /// Contains the target of this overload.
            /// </summary>
            public object Target;

            /// <summary>
            /// Descibes how the argument is converted to work as an argument.
            /// Zero means that the type is exactly the given type.  Positive
            /// means that it was implicitly cast from a base class (see clas
            /// remarks).  -1 means the argument is an interface.  -2 means
            /// a user-defined explicit cast.
            /// </summary>
            public int[] ConversionAmount;
            /// <summary>
            /// Contains the number of arguments added due to a params array
            /// or optional parameters.  This is always positive and greater
            /// than 1 if it is a params array.
            /// </summary>
            public int ParamsOrOptional;
            /// <summary>
            /// Contains the number of times an argument that was passed
            /// by-reference was used an a non-by-reference parameter.
            /// </summary>
            public int ByRefConvert;
            /// <summary>
            /// Contains the number of parameters that are of type LuaUserData,
            /// an overload that has a larger number is more desireable.
            /// </summary>
            public int UserDataCount;
            /// <summary>
            /// If ParamsOrOptional is non-zero, then this represents whether
            /// the number represents a params array or optional arguments.
            /// True for params array; false for optional arguments.
            /// </summary>
            public bool IsParams;
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
        /// <returns>True if the two types are implicitly compatible, otherwise
        /// false.  If it returns false, then the types may be compatible.  If
        /// method is null, then the types cannot be coverted, if non-null, then
        /// that method will convert the types.</returns>
        public static bool TypesCompatible(Type/*!*/ sourceType, Type/*!*/ destType, out MethodInfo method, out int amount)
        {
            method = null;
            amount = 0;

            if (sourceType == destType)
                return true;

            // NOTE: This only checks for derived classes and interfaces,
            //  this will not work for implicit/explicit casts.
            if (destType.IsAssignableFrom(sourceType))
            {
                if (destType.IsInterface)
                    amount = -1;
                else
                {
                    while (destType != sourceType)
                    {
                        amount++;
                        sourceType = sourceType.BaseType;
                    }
                }

                return true;
            }

            // all numeric types are explicitly compatible but do not
            //   define a cast in their type.
            if ((destType != typeof(bool) && destType != typeof(IntPtr) && destType != typeof(UIntPtr) && destType.IsPrimitive) &&
                (sourceType != typeof(bool) && sourceType != typeof(IntPtr) && sourceType != typeof(UIntPtr) && sourceType.IsPrimitive))
            {
                // although they are compatible, they need to be converted,
                //  return false and get the Convert.ToXX method.
                amount = 1;
                method = typeof(Convert).GetMethod("To" + destType.Name, new Type[] { sourceType });
                return false;
            }

            // check for LuaUserData types
            if (typeof(LuaUserData).IsAssignableFrom(sourceType) || typeof(LuaUserData).IsAssignableFrom(destType))
            {
                Type back1, back2;
                if (sourceType.IsGenericType && sourceType.GetGenericTypeDefinition() == typeof(LuaUserData<>))
                    back1 = sourceType.GetGenericArguments()[0];
                else if (sourceType == typeof(LuaUserData))
                    back1 = typeof(object);
                else
                    back1 = sourceType;
                if (destType.IsGenericType && destType.GetGenericTypeDefinition() == typeof(LuaUserData<>))
                    back2 = destType.GetGenericArguments()[0];
                else if (destType == typeof(LuaUserData))
                    back2 = typeof(object);
                else
                    back2 = destType;

                return TypesCompatible(back1, back2, out method, out amount);
            }

            // get any methods from source type that is not marked with 
            //  LuaIgnoreAttribute and has the name 'op_Explicit' or 
            //  'op_Implicit' and has a return type of the destination type and
            //  a sole argument that is implicitly compatible with the source type.
            LuaIgnoreAttribute attr = sourceType.GetCustomAttributes(typeof(LuaIgnoreAttribute), true)
                .Select(o => (LuaIgnoreAttribute)o).FirstOrDefault();
            var possible = sourceType.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0 &&
                    (attr == null || attr.IsMemberVisible(sourceType, m.Name)) &&
                    (m.Name == "op_Explicit" || m.Name == "op_Implicit") && m.ReturnType == destType &&
                    m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(sourceType))
                .ToArray();

            // check for a cast in the destination type
            attr = destType.GetCustomAttributes(typeof(LuaIgnoreAttribute), true)
                .Select(o => (LuaIgnoreAttribute)o).FirstOrDefault();
            possible = possible
                .Union(destType.GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0 &&
                        (attr == null || attr.IsMemberVisible(destType, m.Name)) &&
                        (m.Name == "op_Explicit" || m.Name == "op_Implicit") && m.ReturnType == destType &&
                        m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(sourceType)))
                    .ToArray();

            // check the possible choices, it should only be at most two
            if (possible != null && possible.Length > 0)
            {
                for (int i = 0; i < possible.Length; i++)
                {
                    method = possible[i];
                    if (possible[i].Name == "op_explicit")
                        amount = -2;
                    else // op_implicit
                    {
                        amount = 1;
                        break;
                    }
                }
            }

            // still return false even if we found a cast
            //   because the types are not implicitly compatible.
            return false;
        }
        /// <summary>
        /// Converts the given object to the given type using any user-defined 
        /// casts.  Calls TypesCompatible.
        /// </summary>
        /// <param name="source">The object to convert.</param>
        /// <param name="type">The type to convert to.</param>
        /// <param name="amount">Where to place the conversion amount.</param>
        /// <returns>The type converted to the type.</returns>
        /// <exception cref="System.InvalidCastException">If the type cannot
        /// be converted to the type.</exception>
        public static object ConvertType(object source, Type type, out int amount)
        {
            amount = 0;

            // no need to convert the type
            if (type == null || type == typeof(void))
                return null;

            // if the object is null, any type that is not
            //  a value type can be used.
            if (source == null)
            {
                if (type.IsValueType)
                    throw new InvalidCastException("Cannot convert 'null' to type '" + type + "'.");
                return source;
            }

            // handle user data values
            LuaUserData user = source as LuaUserData;
            if (user != null)
                source = user.Backing;
            Type oType = source.GetType();

            // if type is an array, convert according to underlying type
            if (type.IsArray)
            {
                // get the original array.
                object[] orig;
                if (source is ILuaTable)
                {
                    var table = source as ILuaTable;
                    double len = table.GetLength();
                    if (len > 0)
                    {
                        orig = new object[(int)len];
                        for (double d = 1; d <= len; d++)
                            orig[(int)d] = table[d];
                    }
                    else
                        orig = new[] { source };
                }
                else
                    if (source is IEnumerable)
                        orig = (source as IEnumerable).Cast<object>().ToArray();
                    else
                        orig = new[] { source };

                // get the underlying type
                type = type.GetElementType();

                // get the resulting array
                Array list = Array.CreateInstance(type, orig.Length);
                for (int i = 0; i < orig.Length; i++)
                {
                    list.SetValue(ConvertType(orig[i], type, out amount), i);
                }

                return list;
            }
            else if (typeof(LuaUserData).IsAssignableFrom(type))
            {
                // the non-generic LuaUserData is always compatible with object
                //   however the generic type is only with inherited types.
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(LuaUserData<>))
                {
                    Type back = type.GetGenericArguments()[0];
                    if (!back.IsAssignableFrom(oType))
                        throw new InvalidCastException("Cannot convert type '" + oType + "' to type '" + back + "'.");

                    while (back != oType)
                    {
                        amount++;
                        oType = oType.BaseType;
                    }

                    if (user == null)
                    {
                        var ctor = type.GetConstructor(new[] { back, typeof(Type), typeof(bool), typeof(string[]), typeof(string[]) });
                        return ctor.Invoke(new[] { source, null, false, null, null });
                    }
                    else
                    {
                        var ctor = type.GetConstructor(new[] { typeof(LuaUserData) });
                        return ctor.Invoke(new[] { user });
                    }
                }
                else
                {
                    amount = 1;
                    return new LuaUserData(source, false);
                }
            }
            else
            {
                // if target is a MultipleReturn, get the first object
                if (source is MultipleReturn)
                    source = (source as MultipleReturn)[0];

                // check with TypesCompatible to get whether
                //  it can be converted.
                MethodInfo m;
                if (!TypesCompatible(oType, type, out m, out amount))
                {
                    // if there is no conversion method, throw an exception
                    if (m != null)
                        source = m.Invoke(null, new object[] { source });
                    else
                        throw new InvalidCastException("Cannot convert type '" + oType + "' to type '" + type + "'.");
                }
                return source;
            }
        }
        /// <summary>
        /// Compares two OverloadInfo's and returns a positive number if first
        /// is a better choice, a negative number if second is better, and
        /// zero if they are the same.
        /// </summary>
        /// <typeparam name="T">The type of the method base.</typeparam>
        /// <param name="first">The first overload.</param>
        /// <param name="second">The second overload.</param>
        /// <returns>A positive number if first is better, a negative number if
        /// second is better, or zero if they are the same.</returns>
        static int GetBetterOverload<T>(OverloadInfo<T> first, OverloadInfo<T> second)
            where T : MethodBase
        {
            if (first == null)
                return second == null ? 0 : -1;
            if (second == null)
                return 1;

            // check if one uses more by-reference conversions than the other
            if (first.ByRefConvert != second.ByRefConvert)
            {
                return second.ByRefConvert - first.ByRefConvert;
            }
            // check if one has more LuaUserData parameters than the other
            else if (first.UserDataCount != second.UserDataCount)
            {
                // unlike the other options, a larger UserDataCount is better
                //   because we want the overload with the most LuaUserData types.
                return first.UserDataCount - second.UserDataCount;
            }

            // check for argument conversions and favor ones with less conversion
            {
                // see OverloadInfo remarks.
                int explicits = 0;
                int dif = 0;
                int force = 0;

                int max = Math.Min(first.ConversionAmount.Length, second.ConversionAmount.Length);
                for (int i = 0; i < max; i++)
                {
                    if (first.ConversionAmount[i] == -1 &&
                        second.ConversionAmount[i] == -1)
                    {
                        // both interfaces, this has no effect
                    }
                    else if (first.ConversionAmount[i] == -1)
                    {
                        dif--;

                        // if it's an interface, we need to force it
                        if (force >= 0)
                            force = 1;
                        else
                            return 0; // both define interfaces, ambiguous
                    }
                    else if (second.ConversionAmount[i] == -1)
                    {
                        dif++;

                        if (force <= 0)
                            force = -1;
                        else
                            return 0; // both define interfaces, ambiguous
                    }
                    else if (first.ConversionAmount[i] == -2 &&
                        second.ConversionAmount[i] == -2)
                    {
                        // both define explicit casts, this has no effect
                    }
                    else if (first.ConversionAmount[i] == -2)
                    {
                        explicits++;
                        dif += second.ConversionAmount[i] - 1;
                    }
                    else if (second.ConversionAmount[i] == -2)
                    {
                        explicits--;
                        dif += 1 - first.ConversionAmount[i];
                    }
                    else
                    {
                        dif += (second.ConversionAmount[i] - first.ConversionAmount[i]);
                    }
                }

                if (force > 0)                  // first defines an interface so only the
                    return Math.Min(dif, 0);    //  second one can be chosen or it's ambiguous
                else if (force < 0)
                    return Math.Max(dif, 0);    // second defines an interface so only the
                else if (dif != 0)              //   first one can be chosen or it's ambiguous
                    return dif;
                else if (explicits != 0)
                    return -explicits;
            }

            // favor optional arguments over params arrays
            if (first.ParamsOrOptional != 0 && first.IsParams != second.IsParams)
            {
                if (!first.IsParams)
                    return 1;
                else
                    return -1;
            }
            // favor one with more explicit parameters
            else if (first.ParamsOrOptional != second.ParamsOrOptional)
            {
                return second.ParamsOrOptional - first.ParamsOrOptional;
            }

            // there is nothing different between them
            return 0;
        }
        /// <summary>
        /// Gets the overload information about the given method.  Returns null
        /// if the method cannot be called with the given arguments.
        /// </summary>
        /// <param name="method">The method to get the information for.</param>
        /// <param name="target">The target object.</param>
        /// <param name="args">The arguments to check against.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <returns>The OverloadInfo for the given method or null if the method
        /// cannot be called with the given aruments.</returns>
        /// <exception cref="System.ArgumentNullException">If method, args, or 
        /// byRef is null.</exception>
        static OverloadInfo<T> GetOverloadInfo<T>(T method, object target, object[] args, int[] byRef)
            where T : MethodBase
        {
            if (method == null)
                throw new ArgumentNullException("method");
            if (args == null)
                throw new ArgumentNullException("args");
            if (byRef == null)
                throw new ArgumentNullException("byRef");

            // ignore any methods marked with LuaIgnore
            if (method.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0)
                return null;
            // if the backing method is marked with LuaIgnore, ignore it
            if (method is MethodInfo && target != null)
            {
                MethodInfo methodBase = ((MethodInfo)(object)method).GetBaseDefinition();
                if (target.GetType().GetMethods()
                    .Where(m => m.GetBaseDefinition() == methodBase)
                    .Any(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0)
                )
                    return null;
            }

            var param = method.GetParameters();
            var tempArgs = new List<object>(args);
            var ret = new OverloadInfo<T>(method, target, param.Length);

            // provide support for a params array
            if (param.Length > 0 && param[param.Length - 1].GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0 &&
                tempArgs.Count >= param.Length - 1 && param[param.Length - 1].ParameterType.IsArray)
            {
                // try to add each of the arguments to the list from the last given
                try
                {
                    Type arrayType = param[param.Length - 1].ParameterType.GetElementType();
                    Type listType = typeof(List<>).MakeGenericType(arrayType);
                    IList paramArray = (IList)listType.GetConstructor(new Type[0]).Invoke(null);

                    while (tempArgs.Count > param.Length - 1)
                    {
                        if (byRef.Contains(tempArgs.Count - 1))
                            return null;

                        int i;
                        paramArray.Add(ConvertType(tempArgs[param.Length - 1], arrayType, out i));
                        tempArgs.RemoveAt(param.Length - 1);
                    }

                    // reverse the array because we added them in the opposite order.
                    listType.GetMethod("Reverse", new Type[0]).Invoke(paramArray, null);
                    tempArgs.Add(listType.GetMethod("ToArray").Invoke(paramArray, null));
                    ret.ParamsOrOptional += paramArray.Count + 1;
                    ret.IsParams = true;
                }
                catch (InvalidCastException)
                {
                    tempArgs = new List<object>(args);
                }
            }
            // provide support for optional arguments
            else if (tempArgs.Count < param.Length && param[tempArgs.Count].IsOptional)
            {
                // all missing arguments must be optional
                ret.IsParams = false;
                for (int i = tempArgs.Count; i < param.Length; i++)
                {
                    tempArgs.Add(param[i].DefaultValue);
                    ret.ParamsOrOptional++;
                }
            }

            // check for missing arguments
            if (tempArgs.Count != param.Length)
            {
                return null;
            }

            // check each parameter and try to convert the arguments
            for (int i = 0; i < param.Length; i++)
            {
                // if this method is by reference and not passed that way, not
                //   compatible.
                if (param[i].ParameterType.IsByRef && !byRef.Contains(i))
                    return null;

                // if the argument is passed by-reference and the parameter is
                //   not, increase the counter
                if (byRef.Contains(i) && !param[i].ParameterType.IsByRef)
                    ret.ByRefConvert++;

                // if the parameter is a LuaUserData types, that increase the counter
                if (typeof(LuaUserData).IsAssignableFrom(param[i].ParameterType))
                    ret.UserDataCount++;

                if (tempArgs[i] == null)
                {
                    // cannot pass null to value type
                    if (param[i].ParameterType.IsValueType)
                        return null;
                }
                else
                {
                    try
                    {
                        Type destType = param[i].ParameterType;
                        if (param[i].ParameterType.IsByRef)
                            destType = destType.GetElementType();

                        tempArgs[i] = ConvertType(tempArgs[i], destType, out ret.ConversionAmount[i]);
                    }
                    catch (InvalidCastException)
                    {
                        // cannot convert the argument, so this overload cannot 
                        //   be used.
                        return null;
                    }
                }
            }

            ret.Args = tempArgs.ToArray();
            return ret;
        }
        /// <summary>
        /// Searches the given methods for an overload that will work with the 
        /// given arguments, converting the argument as necessary.
        /// </summary>
        /// <typeparam name="T">The type of the method base (e.g. MethodInfo 
        /// or ConstructorInfo).</typeparam>
        /// <param name="methods">The possible method choices.</param>
        /// <param name="targets">The target objects for the method choices.</param>
        /// <param name="inArgs">A reference to the arguments to check.  The
        /// array may be changed by this code.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <param name="resultMethod">Where the resulting method will be placed.</param>
        /// <param name="resultTarget">Where the respective target will be placed.</param>
        /// <returns>True if a compatible method was found, otherwise false.</returns>
        /// <exception cref="System.ArgumentNullException">If methods, byRef, or targets is null.</exception>
        /// <exception cref="System.ArgumentException">If the length of targets
        /// does not match the length of methods and is not one.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there
        /// is two methods that match the given arguments.</exception>
        public static bool GetCompatibleMethod<T>(T[] methods, object[] targets, ref object[] inArgs, int[] byRef,
            out T resultMethod, out object resultTarget) where T : MethodBase
        {
            resultMethod = null;
            resultTarget = null;
            inArgs = inArgs ?? new object[0];

            if (methods == null)
                throw new ArgumentNullException("methods");
            if (targets == null)
                throw new ArgumentNullException("targets");
            if (byRef == null)
                throw new ArgumentNullException("byRef");
            if (targets.Length != 1 && targets.Length != methods.Length)
                throw new ArgumentException("The length of targets must equal 1 or the length of methods.");

            OverloadInfo<T> min = null;
            bool ambiguous = false;

            for (int k = 0; k < methods.Length; k++)
            {
                var cur = GetOverloadInfo(methods[k], targets[targets.Length == 1 ? 0 : k], inArgs, byRef);
                int result = GetBetterOverload(cur, min);
                if (min == null || result > 0)
                {
                    ambiguous = false;
                    min = cur;
                }
                else if (result == 0)
                    ambiguous = true;
            }

            if (ambiguous)
                throw new AmbiguousMatchException();
            else if (min != null)
            {
                resultMethod = min.Method;
                resultTarget = min.Target;
                inArgs = min.Args;
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Invokes a given method without a virtual call.  Calling MethodInfo.Invoke
        /// on a type will use a virtual call wich will call the derrived type.  So
        /// this generates a DynamicMethod that calls the method non-virtually.
        /// </summary>
        /// <param name="method">The method to call non-virtually.</param>
        /// <param name="target">The target of the invokation.</param>
        /// <param name="args">The arguments to pass, cannot be null.</param>
        /// <returns>The value returned from the method.</returns>
        /// <exception cref="System.ArgumentNullException">If any args is null.</exception>
        static object NonVirtualCall(MethodInfo method, object target, params object[] args)
        {
            // thanks to desco in StackOverflow in this question:
            //  http://stackoverflow.com/questions/3378010/how-to-invoke-non-virtually-the-original-implementation-of-a-virtual-method
            //  this invokes a method info without using a virtual call.

            if (method == null)
                throw new ArgumentNullException("method");
            if (target == null)
                throw new ArgumentNullException("target");
            if (args == null)
                throw new ArgumentNullException("args");

            Type t = target.GetType();
            var dm = new DynamicMethod("proxy", method.ReturnType, new[] { t, typeof(object[]) }, t);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            for (int i = 0; i < args.Length; i++)
            {
                // push each argument to the stack
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldelem, typeof(object));
                il.Emit(OpCodes.Unbox_Any, args[i].GetType());
            }
            // we want to call this non-virtually so we call the base method.
            il.Emit(OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
            var action = dm.CreateDelegate(typeof(Func<,,>).MakeGenericType(t, typeof(object[]), typeof(object)));
            return action.DynamicInvoke(target, args);
        }
        /// <summary>
        /// Creates several methods for invoking the given methods non-virtually.
        /// This is the same as Hack, using dynamic methods to call the base methods
        /// without using virtual calls.  These methods are then used in a LuaMethod
        /// object and the dynamic types of the arguments are used to determine the
        /// method to call.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the method.</param>
        /// <param name="methods">The methods to choose from.</param>
        /// <param name="target">The target of the invokation.</param>
        /// <returns>An array of created methods, and an instance of the created type.</returns>
        /// <exception cref="System.ArgumentNullException">If E, methods, or target is null.</exception>
        static IMethod NonVirtualCallCreate(ILuaEnvironment E, string name, IEnumerable<MethodInfo> methods, object target)
        {
            // implimentation of NonVritualCall to work with a LuaMethod and to allow
            //   for run-time overload resolution.

            if (E == null)
                throw new ArgumentNullException("E");
            if (methods == null)
                throw new ArgumentNullException("methods");
            if (target == null)
                throw new ArgumentNullException("target");

            TypeBuilder tb = NetHelpers.DefineGlobalType("$NonVirtualCall");
            FieldBuilder field = tb.DefineField("Target", typeof(object), FieldAttributes.Private);

            //// public $NonVirtualCall();
            ConstructorBuilder cb = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(object) });
            ILGenerator gen = cb.GetILGenerator();

            // base();
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, typeof(object).GetConstructor(new Type[0]));
            // this.Target = arg_1
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, field);
            gen.Emit(OpCodes.Ret);

            // define the method
            foreach (var meth in methods)
            {
                var param = meth.GetParameters();
                MethodBuilder mb = NetHelpers.CloneMethod(tb, "Do", meth);
                gen = mb.GetILGenerator();

                // return this.Target.{meth}(...);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
                for (int i = 0; i < param.Length; i++)
                {
                    gen.Emit(OpCodes.Ldarg, (i + 1));
                }
                gen.Emit(OpCodes.Call, meth);
                gen.Emit(OpCodes.Ret);
            }

            // create the type and return
            Type ret = tb.CreateType();
            object retO = Activator.CreateInstance(ret, target);
            return LuaOverloadMethod.Create(E, name, ret.GetMethods().Where(m => m.Name == "Do"), new object[] { retO });
        }
        /// <summary>
        /// Gets or sets the given object to the given value.  It's behaviour is
        /// dependent on several forms, however they are mostly the same.
        /// </summary>
        /// <remarks>
        /// Also handles LuaUserData correctly as well as LuaIgnoreAttribute.s
        /// </remarks>
        /// <param name="E">The current environment.</param>
        /// <param name="target">The target object.</param>
        /// <param name="index">The indexing object.</param>
        /// <param name="isStatic">True if indexing the static type, otherwise
        /// indexing the actual object.</param>
        /// <param name="isGet">True if getting the value, otherwise is setting.</param>
        /// <param name="isNonVirtual">True if the method returned should be
        /// invoked non-virtually (e.g. base.Do); otherwise false.</param>
        /// <param name="value">The value to set to.</param>
        /// <returns>The value for get or null if setting.</returns>
        /// <exception cref="System.ArgumentNullException">If E or target or
        /// index is null.</exception>
        /// <exception cref="System.ArgumentException">If isStatic is true and
        /// target is not of type Type.</exception>
        /// <exception cref="System.InvalidOperationException">If the target type
        /// does not define an accessible index or member -or- if index is
        /// not of a valid type or value -or- if attemt to set a method.</exception>
        public static object GetSetIndex(ILuaEnvironment E, object target, object index,
            bool isStatic, bool isGet, bool isNonVirtual, object value = null)
        {
            if (E == null)
                throw new ArgumentNullException("E");
            if (target == null)
                throw new ArgumentNullException("target");
            if (index == null)
                throw new ArgumentNullException("index");

            LuaUserData userData = target as LuaUserData;
            target = userData == null ? target : userData.Backing;
            Type type = isStatic ? target as Type : ((userData != null ? userData.BehavesAs : null) ?? target.GetType());
            if (isNonVirtual)
                type = type.BaseType;
            LuaIgnoreAttribute ignAttr = (LuaIgnoreAttribute)type.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).FirstOrDefault();

            // check for access to reflection
            if (!E.Settings.AllowReflection &&
                ((typeof(Type).IsAssignableFrom(type) && !isStatic) || typeof(MemberInfo).IsAssignableFrom(type)))
                throw new InvalidOperationException("Lua does not have access to reflection.  See LuaSettings.AllowReflection.");

            if (index is double || index is ILuaTable)
            {
                if (isStatic)
                    throw new InvalidOperationException("Attempt to call indexer on a static type.");

                // find the arguments to the indexer
                List<object> param = new List<object>();
                if (index is double)
                {
                    param.Add(index);
                }
                else
                {
                    ILuaTable table = index as ILuaTable;
                    double len = table.GetLength();
                    for (double d = 1; d <= len; d++)
                    {
                        object oo = table[d];
                        if (oo is ILuaTable)
                            throw new InvalidOperationException("Arguments to indexer cannot be a table.");
                        param.Add(oo);
                    }
                }

                // arrays do not actually define an 'Item' method so we need
                //    to access the indexer directly
                if (target is Array)
                {
                    // convert the arguments to long
                    long[] lArgs = new long[param.Count];
                    for (int i = 0; i < param.Count; i++)
                    {
                        if (!(param[i] is double))
                            throw new InvalidOperationException("Arguments to indexer for an array can only be numbers.");
                        else
                            lArgs[i] = Convert.ToInt64(param[i], CultureInfo.InvariantCulture);
                    }

                    if (!isGet)
                    {
                        (target as Array).SetValue(value, lArgs);
                        return null;
                    }
                    else
                        return (target as Array).GetValue(lArgs);
                }

                // setting also requires the last arg be the 'value'
                if (!isGet)
                    param.Add(value);

                // find the valid method
                string name = type == typeof(string) ? "Chars" : "Item";
                object[] args = param.ToArray();
                var byref = new int[0];
                MethodInfo method;
                object methTarget;
                NetHelpers.GetCompatibleMethod(type.GetMethods()
                        .Where(m => m.Name == (isGet ? "get_" + name : "set_" + name))
                        .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0)
                        .ToArray(), new[] { target }, ref args, byref, out method, out methTarget);

                if (method == null ||
                    (userData != null && (!userData.IsMemberVisible(name) || !userData.IsMemberVisible(isGet ? "get_" + name : "set_" + name))) ||
                    (ignAttr != null && (!ignAttr.IsMemberVisible(type, name) || !ignAttr.IsMemberVisible(type, isGet ? "get_" + name : "set_" + name))))
                    throw new InvalidOperationException("Unable to find a visible indexer that " +
                        "matches the provided arguments for type '" +
                        target.GetType() + "'.");

                if (!isNonVirtual)
                    return method.Invoke(target, args);
                else
                    return NonVirtualCall(method, target, args);
            }
            else if (index is string)
            {
                // allow for specifying an overload to methods
                string name = index as string;
                int over = -1;
                if (name.Contains('`'))
                {
                    if (!int.TryParse(name.Substring(name.IndexOf('`') + 1), out over))
                        throw new InvalidOperationException("Only numbers are allowed after the grave(`) when specifying an overload.");

                    name = name.Substring(0, name.IndexOf('`'));
                }

                // find all visible members with the given name
                MemberInfo[] Base = type.GetMember(name)
                    .Where(m => !(m is MethodInfo) || !((MethodInfo)m).Attributes.HasFlag(MethodAttributes.Abstract))
                    .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0)
                    .ToArray();
                if (Base == null || Base.Length == 0 ||
                    (userData != null && !userData.IsMemberVisible(name)) ||
                    (ignAttr != null && !ignAttr.IsMemberVisible(type, name)))
                    throw new InvalidOperationException("'" + name + "' is not a visible member of type '" + type + "'.");

                // perform the action on the given member.
                //  although this only checks the first member,
                //  the only member that can return more than
                //  one with the same name is a method and can 
                //  only be other methods
                switch (Base[0].MemberType)
                {
                    case MemberTypes.Field:
                        {
                            if (over != -1)
                                throw new InvalidOperationException("Cannot specify an overload when accessing a field.");

                            FieldInfo field = (FieldInfo)Base[0];
                            if (isGet)
                            {
                                return field.GetValue(isStatic ? null : target);
                            }
                            else
                            {
                                // must try to convert the given type to the requested type.
                                //  this will use both implicit and explicit casts for
                                //  user-defined types
                                // by default, SetValue only works if the backing type is
                                //  the same as or derrived from the FieldType.  It does
                                //  not even support implicit numerical conversion
                                int a;
                                value = ConvertType(value, field.FieldType, out a);
                                field.SetValue(isStatic ? null : target, value);
                                return null;
                            }
                        }
                    case MemberTypes.Property:
                        {
                            if (over != -1)
                                throw new InvalidOperationException("Cannot specify an overload when accessing a field.");

                            if (isGet)
                            {
                                // with reflection, we can get/set the property even if it is
                                //  not marked with public, so we search for the backing method
                                MethodInfo meth = type.GetMethod("get_" + name, new Type[0]);
                                if (meth == null)
                                    throw new InvalidOperationException("The property '" + name + "' is write-only.");
                                if (meth.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0 ||
                                    (userData != null && !userData.IsMemberVisible("get_" + name)) ||
                                    (ignAttr != null && !ignAttr.IsMemberVisible(type, "get_" + name)))
                                    throw new InvalidOperationException("The get method for property '" + name + "' is inaccessible to Lua.");

                                if (!isNonVirtual)
                                    return meth.Invoke(isStatic ? null : target, null);
                                else
                                    return NonVirtualCall(meth, target);
                            }
                            else
                            {
                                // with reflection, we can get/set the property even if it is
                                //  not marked with public, so we search for the backing method
                                MethodInfo meth = type.GetMethod("set_" + name, new Type[] { (Base[0] as PropertyInfo).PropertyType });
                                if (meth == null)
                                    throw new InvalidOperationException("The property '" + name + "' is read-only.");
                                if (meth.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0 ||
                                    (userData != null && !userData.IsMemberVisible("set_" + name)) ||
                                    (ignAttr != null && !ignAttr.IsMemberVisible(type, "set_" + name)))
                                    throw new InvalidOperationException("The set method for property '" + name + "' is inaccessible to Lua.");

                                // same as with fields, we must convert the type before we give it to the method

                                int a;
                                value = ConvertType(value, (Base[0] as PropertyInfo).PropertyType, out a);
                                if (!isNonVirtual)
                                    meth.Invoke(isStatic ? null : target, new object[] { value });
                                else
                                    NonVirtualCall(meth, target, value);
                                return null;
                            }
                        }
                    case MemberTypes.Method:
                        {
                            if (!isGet)
                                throw new InvalidOperationException("Cannot set the value of a method.");
                            if (Base.Where(m => ((MethodInfo)m).IsSpecialName).Any())
                                throw new InvalidOperationException("Cannot call special method '" + name + "'.");

                            if (!isNonVirtual)
                                return LuaOverloadMethod.Create(E, name, Base.Select(m => (MethodInfo)m), new[] { isStatic ? null : target });
                            else
                                return NonVirtualCallCreate(E, name, Base.Select(m => (MethodInfo)m), target);
                        }
                    default:
                        throw new InvalidOperationException("MemberTypes." + Base[0].MemberType + " is not supported.");
                }
            }
            else
                throw new InvalidOperationException("Indices of a User-Defined type must be a string, number, or table.");
        }
    }
}