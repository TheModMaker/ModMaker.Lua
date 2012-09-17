<<<<<<< HEAD
﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ModMaker.Lua.Parser;

namespace ModMaker.Lua.Runtime
{
    enum LuaValueType
    {
        Nil,
        String, 
        Bool,
        Table,
        Function,
        Number,
        Thread,
        UserData,
    }

    /// <summary>
    /// Defines a lot of helper functions for use in the Runtime.
    /// </summary>
    static class RuntimeHelper
    {
        /// <summary>
        /// Creates a list of the specified type.
        /// </summary>
        /// <param name="t">The type of the list.</param>
        /// <returns>A new list.</returns>
        static IList CreateList(Type t)
        {
            return (IList)typeof(List<>).MakeGenericType(t).GetConstructor(new Type[0]).Invoke(null);
        }
        static object ToArray(this IList list, Type t)
        {
            return typeof(List<>).MakeGenericType(t).GetMethod("ToArray").Invoke(list, null);
        }
        /// <summary>
        /// Defines arithmetic between two doubles.
        /// </summary>
        /// <param name="v1">The first value.</param>
        /// <param name="v2">The second value.</param>
        /// <param name="type">The type of operation.</param>
        /// <returns>The result of the operation.</returns>
        static object NativeArithmetic(double v1, double v2, BinaryOperationType type)
        {
            switch (type)
            {
                case BinaryOperationType.Add:
                    return (v1 + v2);
                case BinaryOperationType.Subtract:
                    return (v1 - v2);
                case BinaryOperationType.Multiply:
                    return (v1 * v2);
                case BinaryOperationType.Divide:
                    return (v1 / v2);
                case BinaryOperationType.Power:
                    return (Math.Pow(v1, v2));
                case BinaryOperationType.Modulo:
                    return (v1 % v2);
                case BinaryOperationType.Concat:
                    return (v1.ToString(CultureInfo.CurrentCulture) + v2.ToString(CultureInfo.CurrentCulture));
                case BinaryOperationType.Gt:
                    return (v1 > v2);
                case BinaryOperationType.Lt:
                    return (v1 < v2);
                case BinaryOperationType.Gte:
                    return (v1 >= v2);
                case BinaryOperationType.Lte:
                    return (v1 <= v2);
                case BinaryOperationType.Equals:
                    return (v1 == v2);
                case BinaryOperationType.NotEquals:
                    return (v1 != v2);
                case BinaryOperationType.And:
                    return (v2);
                case BinaryOperationType.Or:
                    return (v1);
                default:
                    throw new NotImplementedException();
            }
        }
        /// <summary>
        /// Gets the type of a given object.
        /// </summary>
        /// <param name="o">The object to check.</param>
        /// <returns>The type of the given object.</returns>
        static LuaValueType GetType(object o)
        {
            if (o == null)
                return LuaValueType.Nil;

            else if (o is double)
                return LuaValueType.Number;
            else if (o is string)
                return LuaValueType.String;
            else if (o is bool)
                return LuaValueType.Bool;
            else if (o is LuaTable)
                return LuaValueType.Table;
            else if (o is LuaMethod)
                return LuaValueType.Function;
            else if (o is LuaThread)
                return LuaValueType.Thread;
            else
                return LuaValueType.UserData;
        }
        /// <summary>
        /// Gets the .NET name of a given operation.
        /// </summary>
        /// <param name="type">The type of operation.</param>
        /// <returns>The name of the operation (e.g. op_Addition).</returns>
        static string GetBinName(BinaryOperationType type)
        {
            switch (type)
            {
                case BinaryOperationType.Add:
                    return "op_Addition";
                case BinaryOperationType.Subtract:
                    return "op_Subtraction";
                case BinaryOperationType.Multiply:
                    return "op_Multiply";
                case BinaryOperationType.Divide:
                    return "op_Division";
                case BinaryOperationType.Power:
                    return "op_Power";
                case BinaryOperationType.Modulo:
                    return "op_Modulo";
                case BinaryOperationType.Gt:
                    return "op_GreaterThan";
                case BinaryOperationType.Lt:
                    return "op_LessThan";
                case BinaryOperationType.Gte:
                    return "op_GreaterThanOrEqual";
                case BinaryOperationType.Lte:
                    return "op_LessThanOrEqual";
                case BinaryOperationType.Equals:
                    return "op_Equality";
                case BinaryOperationType.NotEquals:
                    return "op_Inequality";
                default:
                    return "";
            }
        }
        /// <summary>
        /// Checks whether two types are compatible and gets a conversion
        /// method if it can be.
        /// </summary>
        /// <param name="t1">The resultant type (e.g. the parameter type).</param>
        /// <param name="t2">The original type (e.g. the argument type).</param>
        /// <param name="meth">The resulting conversion method, will be static.</param>
        /// <returns>True if the two types can be implicitly cast, false if not.  If meth is null, the
        /// two types cannot be converted.</returns>
        static bool TypesCompatible(Type t1, Type t2, out MethodInfo meth)
        {
            meth = null;

            if (t1 == t2)
                return true;

            // NOTE: This only checks for derived classes and interfaces,
            //  this will not work for implicit/explicit casts.
            if (t1.IsAssignableFrom(t2))
                return true;

            // all numeric types are explicitly compatible but do not
            //   define a cast in their type.
            if ((t1 == typeof(SByte) ||
                t1 == typeof(Int16) ||
                t1 == typeof(Int32) ||
                t1 == typeof(Int64) ||
                t1 == typeof(Single) ||
                t1 == typeof(Double) ||
                t1 == typeof(UInt16) ||
                t1 == typeof(UInt32) ||
                t1 == typeof(UInt64) ||
                t1 == typeof(Byte) ||
                t1 == typeof(Decimal) ||
                t1 == typeof(Char)) &&
                (t2 == typeof(SByte) ||
                t2 == typeof(Int16) ||
                t2 == typeof(Int32) ||
                t2 == typeof(Int64) ||
                t2 == typeof(Single) ||
                t2 == typeof(Double) ||
                t2 == typeof(UInt16) ||
                t2 == typeof(UInt32) ||
                t2 == typeof(UInt64) ||
                t2 == typeof(Byte) ||
                t2 == typeof(Decimal) ||
                t2 == typeof(Char)))
            {
                // although they are compatible, they need to be converted,
                //  return false and get the Convert.ToXX method.
                meth = typeof(Convert).GetMethod("To" + t1.Name, new Type[] { t2 });
                return false;
            }

            // get any methods from t1 that is not marked with LuaIgnoreAttribute
            //  and has the name 'op_Explicit' or 'op_Implicit' and has a return type
            //  of t1 and a sole argument that is implicitly compatible with t2.
            meth = t1.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length == 0)
                .Where(m => (m.Name == "op_Explicit" || m.Name == "op_Implicit") && m.ReturnType == t1 &&
                    m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(t2))
                .FirstOrDefault();

            // if meth still equals null, check for a cast in t2.
            if (meth == null)
            {
                meth = t2.GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length == 0)
                    .Where(m => (m.Name == "op_Explicit" || m.Name == "op_Implicit") && m.ReturnType == t1 &&
                        m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(t2))
                    .FirstOrDefault();
            }

            // still return false even if we found a cast
            //   because the types are not implicitly compatible.
            return false;
        }
        static object ConvertType(object o, Type t, out bool conv)
        {
            conv = false;
            // stop now if no type
            if (t == null || t == typeof(void))
                return null;

            // if the object is null, any type that is not
            //  a value type can be used.
            if (o == null)
            {
                if (t.IsValueType)
                    throw new InvalidCastException("Cannot convert 'null' to type '" + t + "'.");
                return o;
            }

            // if type is an array, convert according to underlying type
            if (t.IsArray)
            {
                // get the original array.
                object[] orig;
                if (o is LuaTable)
                {
                    var table = o as LuaTable;
                    double len = table.GetLength();
                    if (len > 0)
                    {
                        List<object> temp = new List<object>();
                        for (double d = 1; d <= len; d++)
                            temp.Add(table[d]);
                        orig = temp.ToArray();
                    }
                    else
                        orig = new[] { o };
                }
                else if (o is IEnumerable)
                    orig = (o as IEnumerable).Cast<object>().ToArray();
                else
                    orig = new[] { o };

                // get the underlying type
                t = t.GetElementType();

                // get the resulting array
                IList list = CreateList(t);
                for (int i = 0; i < orig.Length; i++)
                {
                    list.Add(ConvertType(orig[i], t));
                    if (orig[i] != null && orig[i].GetType() != t)
                        conv = true;
                }

                return list.ToArray(t);
            }
            else
            {
                // if o is a MultipleReturn, get the first object
                if (o is MultipleReturn)
                    o = (o as MultipleReturn)[0];

                // check with TypesCompatible to get whether
                //  it can be converted.
                MethodInfo m;
                if (!TypesCompatible(t, o.GetType(), out m))
                {
                    // if there is no conversion method, throw an exception.
                    if (m != null)
                        o = m.Invoke(null, new object[] { o });
                    else
                        throw new InvalidCastException("Cannot convert type '" + o.GetType() + "' to type '" + t + "'.");
                }
                return o;
            }
        }

        /// <summary>
        /// Gets the real value of a given LuaValue by resolving
        /// any LuaPointers.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The real value of the object.</returns>
        public static object GetValue(object value)
        {
            LuaPointer pointer = value as LuaPointer;
            while (pointer != null)
            {
                value = pointer.GetValue();
                pointer = value as LuaPointer;
            }
            return value;
        }
        /// <summary>
        /// Attempts to invoke a given object.
        /// </summary>
        /// <param name="e">The current environment.</param>
        /// <param name="value">The object to invoke.</param>
        /// <param name="args">The arguments passed to the method.</param>
        /// <returns>The return value of the method.</returns>
        public static MultipleReturn Invoke(LuaEnvironment e, object value, object[] args)
        {
            value = GetValue(value);
            if (value is MultipleReturn)
                value = RuntimeHelper.GetValue(((MultipleReturn)value)[0]);

            if (value is LuaMethod)
                return (value as LuaMethod).InvokeInternal(args, -1);
            else if (value is LuaTable)
            {
                LuaTable m = ((LuaTable)value).MetaTable;
                if (m != null)
                {
                    object v = m._get("__call");
                    if (GetType(v) == LuaValueType.Function)
                        return (GetValue(v) as LuaMethod).InvokeInternal(args, -1);
                }
            }
            else if (value is LuaType)
            {
                Type t = (value as LuaType).Type;
                var ret = GetCompatibleMethod(
                    t.GetConstructors()
                    .Where(c => c.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length == 0)
                    .Select(c => new Tuple<ConstructorInfo, object>(c, null))
                    .ToArray(), 
                    ref args);
                if (ret != null)
                {
                    return new MultipleReturn(ret.Item1.Invoke(args));
                }
            }

            throw new InvalidOperationException("Attempt to call a '" + GetType(value) + "' type.");
        }
        public static object InvokeClass(List<object> args, LuaMethod meth, object self, Type type)
        {
            var ret = meth.InvokeInternal(new[] { self }.Then(args).ToArray(), -1);
            object o = ret[0];

            if (type == typeof(void))
                return null;

            if (o == null)
            {
                if (type.IsValueType)
                    throw new InvalidCastException("Unable to convert 'nil' to a value type.");
                else
                    return null;
            }

            MethodInfo m;
            if (!TypesCompatible(type, o.GetType(), out m))
            {
                if (m == null)
                    throw new InvalidCastException("Cannot cast an object of type '" + o.GetType() + "' to type '" + type + "'.");
                o = m.Invoke(null, new[] { o });
            }
            return o;
        }
        /// <summary>
        /// Creates an index of an object (e.g. obj[12] or obj.some).
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="value">The value of the indexer.</param>
        /// <param name="index">The index to use.</param>
        /// <returns>A pointer that points to the result of the index.</returns>
        public static object Indexer(LuaEnvironment E, object value, object index)
        {
            if (value is MultipleReturn)
                value = GetValue(((MultipleReturn)value)[0]);

            if (value is LuaTable || GetType(value) == LuaValueType.UserData || value is LuaPointer)
                return new LuaPointer(value, index, E);
            else
                throw new InvalidOperationException("Attempt to index a '" + GetType(value) + "' type.");
        }
        public static void SetValue(ref object index, object value)
        {
            value = GetValue(value);
            if (value is MultipleReturn)
                value = GetValue(((MultipleReturn)value)[0]);

            if (index is LuaPointer)
                (index as LuaPointer).SetValue(value);
            else
                index = value;
        }
        /// <summary>
        /// Determines whether a given object is true according
        /// to Lua.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>False if the object is null or false, otherwise true.</returns>
        public static bool IsTrue(object value)
        {
            object o = GetValue(value);
            return !(o == null || o as bool? == false);
        }
        /// <summary>
        /// Tries to convert a given value to a number.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value as a double or null on error.</returns>
        public static double? ToNumber(object value)
        {
            object o = GetValue(value);
            if (o == null)
                return null;
            else if (o is double)
                return (double)o;
            else if (o is string)
            {
                long pos = 0;
                return RuntimeHelper.ReadNumber(new StringReader(o as string), null, 0, 0, ref pos);
            }
            else
            {
                MethodInfo meth;
                if (!RuntimeHelper.TypesCompatible(typeof(double), o.GetType(), out meth))
                {
                    if (meth != null)
                        o = meth.Invoke(null, new object[] { o });
                    else
                        throw new InvalidCastException("Cannot cast from type '" + o.GetType() + "' to type 'double'.");
                }
                return (double)o;
            }
        }
        /// <summary>
        /// Converts a number to a double or ignores any other type.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns></returns>
        public static object ToDouble(this object value)
        {
            if (value == null)
                return null;

            Type t1 = value.GetType();
            if (t1 == typeof(SByte) ||
                t1 == typeof(Int16) ||
                t1 == typeof(Int32) ||
                t1 == typeof(Int64) ||
                t1 == typeof(Single) ||
                t1 == typeof(UInt16) ||
                t1 == typeof(UInt32) ||
                t1 == typeof(UInt64) ||
                t1 == typeof(Byte) ||
                t1 == typeof(Decimal))
                return Convert.ToDouble(value);
            else
                return value;
        }

        /// <summary>
        /// Reads a number from a text reader.
        /// </summary>
        /// <param name="input">The input to read from.</param>
        /// <param name="name">The name to use with errors.</param>
        /// <param name="line">The current line number to use with errors.</param>
        /// <param name="col">The colOffset to use with errors.</param>
        /// <param name="pos">The current position in the reader, will be increased with reading.</param>
        /// <returns>The number read.</returns>
        public static double ReadNumber(TextReader input, string name, long line, long col, ref long pos)
        {
            bool hex = false;
            double val = 0, exp = 0, dec = 0;
            int decC = 0;
            bool negV = false, negE = false;
            if (input.Peek() == '-')
            {
                negV = true;
                pos++;
                input.Read();
            }
            if (input.Peek() == '0' && (input.Read() != -1 && pos <= ++pos && (input.Peek() == 'x' || input.Peek() == 'X')))
            {
                hex = true;
                input.Read();
                pos++;
            }

            bool b = true;
            int stat = 0; // 0-val, 1-dec, 2-exp
            int c;
            while (b && input.Peek() != -1)
            {
                switch (c = input.Peek())
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        pos++;
                        input.Read();
                        if (stat == 0)
                        {
                            val *= (hex ? 16 : 10);
                            val += int.Parse(((char)c).ToString(), CultureInfo.InvariantCulture);
                        }
                        else if (stat == 1)
                        {
                            dec *= (hex ? 16 : 10);
                            dec += int.Parse(((char)c).ToString(), CultureInfo.InvariantCulture);
                            decC++;
                        }
                        else
                        {
                            exp *= (hex ? 16 : 10);
                            exp += int.Parse(((char)c).ToString(), CultureInfo.InvariantCulture);
                        }
                        break;
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'f':
                        pos++;
                        input.Read();
                        if (!hex)
                        {
                            b = false; break;
                        }
                        if (stat == 0)
                        {
                            val *= 16;
                            val += int.Parse(((char)c).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        }
                        else if (stat == 1)
                        {
                            dec *= 16;
                            dec += int.Parse(((char)c).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                            decC++;
                        }
                        else
                        {
                            exp *= 16;
                            exp += int.Parse(((char)c).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        }
                        break;
                    case 'e':
                    case 'p':
                        pos++;
                        input.Read();
                        if ((hex && c == 'p') || (!hex && c == 'e'))
                        {
                            if (stat == 2)
                                throw new SyntaxException("Can only have exponent designator('e' or 'p') per number.", line, pos - col, name);
                            stat = 2;

                            if (input.Peek() != -1)
                                throw new SyntaxException("Must specify at least one number for the exponent.", line, pos - col, name);
                            if (input.Peek() == '+' || (input.Peek() == '-' && (negE = true == true)))
                            {
                                pos++;
                                input.Read();
                                if (input.Peek() == -1)
                                    throw new SyntaxException("Must specify at least one number for the exponent.", line, pos - col, name);
                            }

                            if ("0123456789".Contains((char)input.Peek()))
                            {
                                pos++;
                                exp = int.Parse(((char)input.Read()).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                break;
                            }
                            else if (hex && "abcdefABCDEF".Contains((char)input.Peek()))
                            {
                                pos++;
                                exp = int.Parse(((char)input.Read()).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                break;
                            }
                            throw new SyntaxException("Must specify at least one number for the exponent.", line, pos - col, name);
                        }
                        else if (hex && c == 'e')
                        {
                            if (stat == 0)
                            {
                                val *= 16;
                                val += 14;
                            }
                            else if (stat == 1)
                            {
                                dec *= 16;
                                dec += 14;
                                decC++;
                            }
                            else
                            {
                                exp *= 16;
                                exp += 14;
                            }
                        }
                        else
                            b = false;
                        break;
                    case '.':
                        pos++;
                        input.Read();
                        if (stat == 0)
                            stat = 1;
                        else
                            throw new SyntaxException("A number can only have one decimal point(.).", line, pos - col, name);
                        break;
                    default:
                        b = false;
                        break;
                }
            }
            while (decC-- > 0) dec *= 0.1;
            val += dec;
            if (negV) dec *= -1;
            val *= Math.Pow((hex ? 2 : 10), (negE ? -exp : exp));
            if (double.IsInfinity(val))
                throw new SyntaxException("Number outside range of double.", line, pos - col, name);
            return val;
        }
        /// <summary>
        /// Converts an object to a given type using TypesCompatible.
        /// </summary>
        /// <param name="o">The object to convert.</param>
        /// <param name="t">The type to convert to.</param>
        /// <returns>An object that can be passed in MethodInfo.Invoke.</returns>
        public static object ConvertType(object o, Type t)
        {
            bool v;
            return ConvertType(o, t, out v);
        }
        /// <summary>
        /// A runtime version of overload-resolution.  Searches each of the methods
        /// to find a method with the given arguments while converting the arguments
        /// to work.
        /// </summary>
        /// <param name="meths">The methods to search in.</param>
        /// <param name="inArgs">The arguments to use, must pass a reference to an array because the
        /// arguments in this array will be converted to work (i.e. don't use list.ToArray()).</param>
        /// <returns>The method that will work with the arguments or null if none work.</returns>
        public static Tuple<T, object> GetCompatibleMethod<T>(Tuple<T, object>[] meths, ref object[] inArgs) 
            where T : MethodBase
        {
            Tuple<T, object> ret = null;
            List<object> tempArgs = new List<object>();
            int max = int.MaxValue;

            /* loop first to find exact match for the types */
            foreach (var item in meths)
            {
                var param = item.Item1.GetParameters();
                bool cont = false;
                int cur = 0;
                tempArgs = new List<object>(inArgs);
                if (param.Length != tempArgs.Count)
                    continue;

                for (int i = 0; i < param.Length; i++)
                {
                    if (param[i].ParameterType == typeof(object))
                        continue;

                    if (tempArgs[i] == null)
                    {
                        if (param[i].ParameterType.IsValueType)
                        {
                            cont = true;
                            break;
                        }
                    }
                    else
                    {
                        if (!param[i].ParameterType.IsAssignableFrom(tempArgs[i].GetType()))
                        {
                            cont = true;
                            break;
                        }
                        if (param[i].ParameterType != tempArgs[i].GetType())
                            cur++;
                    }
                }
                if (cont) continue;

                if (ret == null || cur < max)
                {
                    inArgs = tempArgs.ToArray();
                    ret = item;
                    max = cur;
                }
                else
                {
                    throw new AmbiguousMatchException();
                }
            }

            if (ret != null)
                return ret;
            max = int.MaxValue;

            /* loop again for a less specific type, or if a user-defined cast exists */
            foreach (var item in meths)
            {
                var param = item.Item1.GetParameters();
                bool cont = false;
                int cur = 0;
                tempArgs = new List<object>(inArgs);
                if (param[param.Length - 1].GetCustomAttributes(typeof(ParamArrayAttribute), false).Count() > 0)
                {
                    // support the 'params' keyword (this is why there is a temp array).
                    Type t = param[param.Length - 1].ParameterType.GetElementType();
                    IList pa = (IList)typeof(List<>).MakeGenericType(t).GetConstructor(new Type[0]).Invoke(null);
                    bool b = true;
                    while (tempArgs.Count > param.Length - 1)
                    {
                        // pa.Add(ConvertType(tempArgs.Pop(), t));
                        try
                        {
                            pa.Add(ConvertType(tempArgs[param.Length - 1], t));
                            tempArgs.RemoveAt(param.Length - 1);
                        }
                        catch (Exception)
                        {
                            b = false;
                            break;
                        }
                    }

                    if (b)
                    {
                        cur++;
                        tempArgs.Add(typeof(List<>).MakeGenericType(t).GetMethod("ToArray").Invoke(pa, null));
                    }
                    else
                        tempArgs = new List<object>(inArgs);
                }
                else if (param.Select(p => p.IsOptional).Count() > 0)
                {
                    // support optional arguments (this is why there is a temp array).
                    bool b = false;
                    for (int i = tempArgs.Count; i < param.Length; i++)
                    {
                        try
                        {
                            if (!param[i].IsOptional)
                                throw new Exception();
                            tempArgs.Add(param[i].DefaultValue);
                            cur++;
                        }
                        catch (Exception)
                        {
                            b = true;
                            cur = 0;
                            break;
                        }
                    }

                    if (b)
                        tempArgs = new List<object>(inArgs);
                }
                if (tempArgs.Count != param.Length)
                    continue;

                for (int i=0; i<param.Length; i++)
                {
                    if (param[i].ParameterType == typeof(object))
                        continue;

                    if (tempArgs[i] == null)
                    {
                        if (param[i].ParameterType.IsValueType)
                        {
                            cont = true;
                            break;
                        }
                        continue;
                    }

                    try
                    {
                        bool b;
                        tempArgs[i] = ConvertType(tempArgs[i], param[i].ParameterType, out b);
                        if (b || tempArgs[i] == null || param[i].ParameterType != tempArgs[i].GetType())
                            cur++;
                    }
                    catch (Exception)
                    {
                        cont = true;
                        break;
                    }
                }

                if (cont) continue;

                if (ret == null || cur < max)
                {
                    inArgs = tempArgs.ToArray();
                    ret = item;
                    max = cur;
                }
                else
                    throw new AmbiguousMatchException();
            }

            return ret;
        }
        /// <summary>
        /// This is called whenever a binary operation occurs to determine which function to call.
        /// </summary>
        /// <param name="lhs">The left-hand operand.</param>
        /// <param name="type">The type of operation.</param>
        /// <param name="rhs">The right-hand operand.</param>
        /// <returns>The result of the operation.</returns>
        public static object ResolveBinaryOperation(object lhs, BinaryOperationType type, object rhs)
        {
            switch (type)
            {
                case BinaryOperationType.Add:
                case BinaryOperationType.Subtract:
                case BinaryOperationType.Multiply:
                case BinaryOperationType.Divide:
                case BinaryOperationType.Power:
                case BinaryOperationType.Modulo:
                case BinaryOperationType.Gt:
                case BinaryOperationType.Lt:
                case BinaryOperationType.Gte:
                case BinaryOperationType.Lte:
                    {
                        // get the operatand's true value.
                        object o1 = lhs is LuaPointer ? GetValue(lhs) : lhs;
                        object o2 = rhs is LuaPointer ? GetValue(rhs) : rhs;
                        LuaUserData u1 = o1 as LuaUserData;
                        LuaUserData u2 = o2 as LuaUserData;

                        // check that if one operand is UserData that the operator is visible.
                        if (u1 != null && u1.Members != null && !u1.Members.Contains(GetBinName(type)))
                            throw new InvalidOperationException(type + " operator is inaccessable to Lua code.");
                        if (u2 != null && u2.Members != null && !u2.Members.Contains(GetBinName(type)))
                            throw new InvalidOperationException(type + " operator is inaccessable to Lua code.");

                        // if operand is multiple return, use the first result.
                        if (o1 is MultipleReturn)
                            o1 = GetValue((o1 as MultipleReturn)[0]);
                        if (o2 is MultipleReturn)
                            o2 = GetValue((o2 as MultipleReturn)[0]);

                        if (o1 is LuaClass || o2 is LuaClass)
                            throw new InvalidOperationException("Attempted to perform arithmetic on a 'class definition' object.");

                        // get the type of the operands.
                        LuaValueType t1 = GetType(u1 == null ? o1 : u1), t2 = GetType(u2 != null ? u2 : o2);

                        if (t1 == LuaValueType.Number && t2 == LuaValueType.Number)
                            return NativeArithmetic((double)(u1 == null ? o1 : u1), (double)(u2 != null ? u2 : o2), type);
                        else if (t1 != LuaValueType.UserData && t2 != LuaValueType.UserData)
                        {
                            string err = t1 == LuaValueType.UserData || t1 == LuaValueType.Number ? t2.ToString() : t1.ToString();

                            throw new InvalidOperationException("Attempted to perform arithmetic on a '" + err.ToLower(CultureInfo.InvariantCulture) + "' value.");
                        }
                        else
                        {
                            // try the first type
                            if (t1 == LuaValueType.UserData)
                            {
                                Type user = u1 == null ? o1.GetType() : u1.Value.GetType();
                                object[] args = new[] { (u1 == null ? o1 : u1.Value), (u2 == null ? o2 : u2.Value) };
                                var ret = GetCompatibleMethod(
                                    user.GetMethods()
                                        .Where(m => m.Name == GetBinName(type) && m.Attributes.HasFlag(MethodAttributes.Static) && m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Count() == 0)
                                        .Select(m => new Tuple<MethodInfo, object>(m, null))
                                        .ToArray(),
                                    ref args
                                );
                                if (ret != null)
                                {
                                    return ret.Item1.Invoke(null, args);
                                }
                            }

                            // try the second type
                            if (t2 == LuaValueType.UserData)
                            {
                                var user = u2 == null ? o2.GetType() : u2.Value.GetType();
                                var args = new[] { (u1 == null ? o1 : u1.Value), (u2 == null ? o2 : u2.Value) };
                                var ret = GetCompatibleMethod(
                                    user.GetMethods()
                                        .Where(m => m.Name == GetBinName(type) && m.Attributes.HasFlag(MethodAttributes.Static) && m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Count() == 0)
                                        .Select(m => new Tuple<MethodInfo, object>(m, null))
                                        .ToArray(),
                                    ref args
                                );
                                if (ret != null)
                                {
                                    return ret.Item1.Invoke(null, args);
                                }
                            }

                            throw new InvalidOperationException("Unable to find an operator that matches the given operands.");
                        }
                    }
                case BinaryOperationType.Concat:
                    return (GetValue(lhs) ?? "").ToString() + (GetValue(rhs) ?? "").ToString();
                case BinaryOperationType.Equals:
                case BinaryOperationType.NotEquals:
                    {
                        object o1 = RuntimeHelper.GetValue(lhs);
                        object o2 = RuntimeHelper.GetValue(rhs);

                        bool b = object.Equals(o1, o2);
                        return type == BinaryOperationType.Equals ? b : !b;
                    }
                case BinaryOperationType.And:
                    {
                        object o = GetValue(lhs);
                        if (o != null && o as bool? != false)
                        {
                            o = GetValue(rhs);
                        }

                        return (o);
                    }
                case BinaryOperationType.Or:
                    {
                        object o = GetValue(lhs);
                        if (o == null || o as bool? == false)
                        {
                            o = GetValue(rhs);
                        }

                        return (o);
                    }
                default:
                    throw new InvalidOperationException("Unable to resolve BinaryOperation." + type);
            }
        }
        /// <summary>
        /// This is called whenever a unary operation occurs to determine which function to call.
        /// </summary>
        /// <param name="type">The type of operation.</param>
        /// <param name="target">The target of the operation.</param>
        /// <returns>The result of the operation.</returns>
        public static object ResolveUnaryOperation(UnaryOperationType type, object target)
        {
            switch (type)
            {
                case UnaryOperationType.Minus:
                    {
                        target = GetValue(target);
                        LuaValueType t1 = GetType(target);

                        if (t1 == LuaValueType.Number)
                        {
                            return -(double)target;
                        }
                        else if (t1 == LuaValueType.UserData)
                        {
                            Type t = target.GetType();
                            MethodInfo meth = t.GetMethod("op_UnaryMinus");
                            if (meth == null || meth.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Count() > 0)
                                throw new InvalidOperationException("User data type '" + t + "' does not define a unary-minus operator.");

                            return meth.Invoke(null, new[] { target });
                        }
                        else
                            throw new InvalidOperationException("Attempted to perform arithmetic on a '" + 
                                t1.ToString().ToLower(CultureInfo.InvariantCulture) + "' value.");
                    }
                case UnaryOperationType.Not:
                    return !IsTrue(target);
                case UnaryOperationType.Length:
                    {
                        target = GetValue(target);
                        LuaValueType t1 = GetType(target);

                        if (t1 == LuaValueType.String)
                        {
                            return (double)((string)target).Length;
                        }
                        else if (t1 == LuaValueType.Table)
                        {
                            double d = ((LuaTable)target).GetLength();
                            return d == -1 ? 0 : d;
                        }
                        else if (t1 == LuaValueType.UserData)
                        {
                            Type t = target.GetType();
                            bool flag = false;
                            var mems = t.GetMember("Length");
                        validate:
                            if (mems != null && mems.Length > 0)
                            {
                                foreach (var item in mems)
                                {
                                    if (item.MemberType == MemberTypes.Method)
                                    {
                                        MethodInfo meth = item as MethodInfo;
                                        if ((!meth.Attributes.HasFlag(MethodAttributes.Static) && meth.GetParameters().Length == 0) ||
                                            (meth.Attributes.HasFlag(MethodAttributes.Static) && meth.GetParameters().Length == 1) && meth.GetParameters()[0].ParameterType.IsAssignableFrom(t))
                                        {
                                            if (meth.Attributes.HasFlag(MethodAttributes.Static))
                                                return meth.Invoke(null, new[] { target });
                                            else
                                                return meth.Invoke(target, null);
                                        }
                                    }
                                    else if (item.MemberType == MemberTypes.Field)
                                    {
                                        FieldInfo field = item as FieldInfo;
                                        return field.GetValue(target);
                                    }
                                    else if (item.MemberType == MemberTypes.Property)
                                    {
                                        PropertyInfo field = item as PropertyInfo;
                                        return field.GetValue(target, null);
                                    }
                                }
                            }
                            if (!flag)
                            {
                                flag = true;
                                mems = t.GetMember("Count");
                                goto validate;
                            }
                            throw new InvalidOperationException("User data type '" + t + "' does not define a 'Length' or 'Count' member.");
                        }

                        throw new InvalidOperationException("Attempted to perform arithmetic on a '" + 
                            t1.ToString().ToLower(CultureInfo.InvariantCulture) + "' value.");
                    }
            }
            return null;
        }
        /// <summary>
        /// Called when the code encounters the 'class' keyword.  Defines a LuaClass
        /// object with the given name.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="types">The types that the class will derive.</param>
        /// <param name="name">The name of the class.</param>
        public static void DefineClass(LuaEnvironment E, List<string> types, string name)
        {
            if (E._globals.GetItemRaw(name) != null)
                throw new InvalidOperationException("The name '" + name + "' is already a global variable and cannot be a class name.");

            // resolve each of the types
            Type b = null;
            List<Type> inter = new List<Type>();
            foreach (var item in types)
            {
                // get the types that this Lua code can access according to the settings.
                Type[] access;
                if (E.Settings.ClassAccess == LuaClassAccess.All)
                    access = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).ToArray();
                else if (E.Settings.ClassAccess == LuaClassAccess.System)
                {
                    access = E._globals.Where(k => k.Value is LuaType).Select(k => (k.Value as LuaType).Type).ToArray();
                    access = access.Then(AppDomain.CurrentDomain.GetAssemblies().Where(a => Resources.Whitelist.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Contains(a.GetName().GetPublicKey().ToStringBase16())).SelectMany(a => a.GetTypes())).ToArray();
                }
                else
                    access = E._globals.Where(k => k.Value is LuaType).Select(k => (k.Value as LuaType).Type).ToArray();

                // get the types that match the given name.
                Type[] typesa = access.Where(t => t.Name == item || t.FullName == item).ToArray();
                if (typesa == null || typesa.Length == 0)
                    throw new InvalidOperationException("Unable to locate the type '" + item + "'");
                if (typesa.Length > 1)
                    throw new InvalidOperationException("More than one type found for name '" + name + "'");
                Type type = typesa.FirstOrDefault();

                if (!type.Attributes.HasFlag(TypeAttributes.Public))
                    throw new InvalidOperationException("Base class and interfaces must be public");

                if (type.IsClass)
                {
                    // if the type is a class, it will be the base class
                    if (b == null)
                    {
                        if (type.IsSealed)
                            throw new InvalidOperationException("Cannot derive from a sealed class.");
                        if (type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[0], null) == null)
                            throw new InvalidOperationException("Cannot derive from a type without an empty constructor.");

                        b = type;
                    }
                    else
                        throw new InvalidOperationException("Can only derive from a single base class.");
                }
                else if (type.IsInterface)
                    inter.Add(type);
                else
                    throw new InvalidOperationException("Cannot derive from a value-type.");
            }

            // create and register the LuaClass object.
            LuaClass c = new LuaClass(name, b, inter.ToArray(), E);
            E._globals.SetItemRaw(name, c);
        }
        public static object GetFunction(LuaEnvironment E, Type t, string name, object target)
        {
            LuaMethod r = new LuaMethod(t.GetMethod(name), target, name, E);
            return (r);
        }
        /// <summary>
        /// Creates a pointer to a global Lua value.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the value.</param>
        /// <returns>A pointer to the value.</returns>
        public static object GetGlobal(LuaEnvironment E, string name)
        {
            return new LuaPointer(E._globals, name, E);
        }
        /// <summary>
        /// Used in a VarInitItem to set the values.  It creates an array
        /// of a given length from the given values.  Expands any MultipleReturns
        /// and adds nulls as necessary.
        /// </summary>
        /// <param name="values">The input values.</param>
        /// <param name="names">The length of the array.</param>
        /// <returns>An array of the given length to set the values to.</returns>
        public static object[] SetValues(List<object> values, int names)
        {
            object[] val = new object[names];
            for (int i = 0; i < names; i++)
            {
                object o = i < values.Count ? GetValue(values[i]) : null;
                if (o is MultipleReturn)
                {
                    if (i < values.Count - 1)
                        o = GetValue(((MultipleReturn)o)[0]);
                    else
                    {
                        using (var itr = ((MultipleReturn)o).GetEnumerator())
                        {
                            while (i < names && itr.MoveNext())
                            {
                                val[i] = itr.Current;
                                i++;
                            }
                            break;
                        }
                    }
                }
                val[i] = o;
            }

            return val;
        }
        /// <summary>
        /// Used by a ForGenItem to start the loop.
        /// </summary>
        /// <param name="exp">The expressions for the intial values.</param>
        /// <param name="f">The function to call.</param>
        /// <param name="s">The value to pass to each call.</param>
        /// <param name="var">The initial value.</param>
        public static void ForGenStart(List<object> exp, ref object f, ref object s, ref object var)
        {
            int j = 0;
            for (int i = 0; i < exp.Count; i++)
            {
                object o = GetValue(exp[i]);
                if (o is MultipleReturn)
                {
                    if (i + 1 <= exp.Count)
                    {
                        foreach (var item in (o as MultipleReturn))
                        {
                            o = GetValue(item);
                            if (o is MultipleReturn)
                                o = (o as MultipleReturn)[0];

                            if (j == 0)
                                f = o;
                            else if (j == 1)
                                s = o;
                            else if (j == 2)
                                var = o;
                            else
                                return;
                            j++;
                        }
                    }
                    else
                        o = (o as MultipleReturn)[0];
                }

                if (j == 0)
                    f = o;
                else if (j == 1)
                    s = o;
                else if (j == 2)
                    var = o;
                else
                    return;
                j++;
            }
        }
    }
=======
﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime
{
    enum LuaValueType
    {
        Nil,
        String, 
        Bool,
        Table,
        Function,
        Number,
        Thread,
        UserData,
    }

    /// <summary>
    /// Defines a lot of helper functions for use in the Runtime.
    /// </summary>
    static class RuntimeHelper
    {
        /// <summary>
        /// Defines arithmetic between two doubles.
        /// </summary>
        /// <param name="v1">The first value.</param>
        /// <param name="v2">The second value.</param>
        /// <param name="type">The type of operation.</param>
        /// <returns>The result of the operation.</returns>
        static object NativeArithmetic(double v1, double v2, BinaryOperationType type)
        {
            switch (type)
            {
                case BinaryOperationType.Add:
                    return (v1 + v2);
                case BinaryOperationType.Subtract:
                    return (v1 - v2);
                case BinaryOperationType.Multiply:
                    return (v1 * v2);
                case BinaryOperationType.Divide:
                    return (v1 / v2);
                case BinaryOperationType.Power:
                    return (Math.Pow(v1, v2));
                case BinaryOperationType.Modulo:
                    return (v1 % v2);
                case BinaryOperationType.Concat:
                    return (v1.ToString(CultureInfo.CurrentCulture) + v2.ToString(CultureInfo.CurrentCulture));
                case BinaryOperationType.Gt:
                    return (v1 > v2);
                case BinaryOperationType.Lt:
                    return (v1 < v2);
                case BinaryOperationType.Gte:
                    return (v1 >= v2);
                case BinaryOperationType.Lte:
                    return (v1 <= v2);
                case BinaryOperationType.Equals:
                    return (v1 == v2);
                case BinaryOperationType.NotEquals:
                    return (v1 != v2);
                case BinaryOperationType.And:
                    return (v2);
                case BinaryOperationType.Or:
                    return (v1);
                default:
                    throw new NotImplementedException();
            }
        }
        /// <summary>
        /// Gets the type of a given object.
        /// </summary>
        /// <param name="o">The object to check.</param>
        /// <returns>The type of the given object.</returns>
        static LuaValueType GetType(object o)
        {
            if (o == null)
                return LuaValueType.Nil;

            else if (o is double)
                return LuaValueType.Number;
            else if (o is string)
                return LuaValueType.String;
            else if (o is bool)
                return LuaValueType.Bool;
            else if (o is LuaTable)
                return LuaValueType.Table;
            else if (o is LuaMethod)
                return LuaValueType.Function;
            else if (o is LuaThread)
                return LuaValueType.Thread;
            else
                return LuaValueType.UserData;
        }
        /// <summary>
        /// Gets the .NET name of a given operation.
        /// </summary>
        /// <param name="type">The type of operation.</param>
        /// <returns>The name of the operation (e.g. op_Addition).</returns>
        static string GetBinName(BinaryOperationType type)
        {
            switch (type)
            {
                case BinaryOperationType.Add:
                    return "op_Addition";
                case BinaryOperationType.Subtract:
                    return "op_Subtraction";
                case BinaryOperationType.Multiply:
                    return "op_Multiply";
                case BinaryOperationType.Divide:
                    return "op_Division";
                case BinaryOperationType.Power:
                    return "op_Power";
                case BinaryOperationType.Modulo:
                    return "op_Modulo";
                case BinaryOperationType.Gt:
                    return "op_GreaterThan";
                case BinaryOperationType.Lt:
                    return "op_LessThan";
                case BinaryOperationType.Gte:
                    return "op_GreaterThanOrEqual";
                case BinaryOperationType.Lte:
                    return "op_LessThanOrEqual";
                case BinaryOperationType.Equals:
                    return "op_Equality";
                case BinaryOperationType.NotEquals:
                    return "op_Inequality";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Gets the real value of a given LuaValue by resolving
        /// any LuaPointers.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The real value of the object.</returns>
        public static object GetValue(object value)
        {
            LuaPointer pointer = value as LuaPointer;
            while (pointer != null)
            {
                value = pointer.GetValue();
                pointer = value as LuaPointer;
            }
            return value;
        }
        /// <summary>
        /// Attempts to invoke a given object.
        /// </summary>
        /// <param name="e">The current environment.</param>
        /// <param name="value">The object to invoke.</param>
        /// <param name="args">The arguments passed to the method.</param>
        /// <returns>The return value of the method.</returns>
        public static MultipleReturn Invoke(LuaEnvironment e, object value, object[] args)
        {
            value = GetValue(value);
            if (value is MultipleReturn)
                value = RuntimeHelper.GetValue(((MultipleReturn)value)[0]);

            if (value is LuaMethod)
                return (value as LuaMethod).InvokeInternal(args, -1);
            else if (value is LuaTable)
            {
                LuaTable m = ((LuaTable)value).MetaTable;
                if (m != null)
                {
                    object v = m._get("__call");
                    if (GetType(v) == LuaValueType.Function)
                        return (GetValue(v) as LuaMethod).InvokeInternal(args, -1);
                }
            }
            else if (value is LuaType)
            {
                Type t = (value as LuaType).Type;
                var ret = GetCompatibleMethod(
                    t.GetConstructors()
                    .Where(c => c.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length == 0)
                    .Select(c => new Tuple<MethodBase, object>(c, null))
                    .ToArray(), 
                    args);
                if (ret != null)
                {
                    ConstructorInfo c = ret.Item1 as ConstructorInfo;
                    return new MultipleReturn(c.Invoke(args));
                }
            }

            throw new InvalidOperationException("Attempt to call a '" + GetType(value) + "' type.");
        }
        public static object InvokeClass(List<object> args, LuaMethod meth, object self, Type type)
        {
            var ret = meth.InvokeInternal(new[] { self }.Then(args).ToArray(), -1);
            object o = ret[0];

            if (type == typeof(void))
                return null;

            if (o == null)
            {
                if (type.IsValueType)
                    throw new InvalidCastException("Unable to convert 'nil' to a value type.");
                else
                    return null;
            }

            MethodInfo m;
            if (!TypesCompatible(type, o.GetType(), out m))
            {
                if (m == null)
                    throw new InvalidCastException("Cannot cast an object of type '" + o.GetType() + "' to type '" + type + "'.");
                o = m.Invoke(null, new[] { o });
            }
            return o;
        }
        /// <summary>
        /// Creates an index of an object (e.g. obj[12] or obj.some).
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="value">The value of the indexer.</param>
        /// <param name="index">The index to use.</param>
        /// <returns>A pointer that points to the result of the index.</returns>
        public static object Indexer(LuaEnvironment E, object value, object index)
        {
            if (value is MultipleReturn)
                value = GetValue(((MultipleReturn)value)[0]);

            if (value is LuaTable || GetType(value) == LuaValueType.UserData || value is LuaPointer)
                return new LuaPointer(value, index, E);
            else
                throw new InvalidOperationException("Attempt to index a '" + GetType(value) + "' type.");
        }
        public static void SetValue(ref object index, object value)
        {
            value = GetValue(value);
            if (value is MultipleReturn)
                value = GetValue(((MultipleReturn)value)[0]);

            if (index is LuaPointer)
                (index as LuaPointer).SetValue(value);
            else
                index = value;
        }
        /// <summary>
        /// Determines whether a given object is true according
        /// to Lua.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>False if the object is null or false, otherwise true.</returns>
        public static bool IsTrue(object value)
        {
            object o = GetValue(value);
            return !(o == null || o as bool? == false);
        }
        /// <summary>
        /// Tries to convert a given value to a number.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value as a double or null on error.</returns>
        public static double? ToNumber(object value)
        {
            object o = GetValue(value);
            if (o == null)
                return null;
            else if (o is double)
                return (double)o;
            else if (o is string)
            {
                long pos = 0;
                return RuntimeHelper.ReadNumber(new StringReader(o as string), null, 0, 0, ref pos);
            }
            else
            {
                MethodInfo meth;
                if (!RuntimeHelper.TypesCompatible(typeof(double), o.GetType(), out meth))
                {
                    if (meth != null)
                        o = meth.Invoke(null, new object[] { o });
                    else
                        throw new InvalidCastException("Cannot cast from type '" + o.GetType() + "' to type 'double'.");
                }
                return (double)o;
            }
        }

        /// <summary>
        /// Reads a number from a text reader.
        /// </summary>
        /// <param name="input">The input to read from.</param>
        /// <param name="name">The name to use with errors.</param>
        /// <param name="line">The current line number to use with errors.</param>
        /// <param name="col">The colOffset to use with errors.</param>
        /// <param name="pos">The current position in the reader, will be increased with reading.</param>
        /// <returns>The number read.</returns>
        public static double ReadNumber(TextReader input, string name, long line, long col, ref long pos)
        {
            bool hex = false;
            double val = 0, exp = 0, dec = 0;
            int decC = 0;
            bool negV = false, negE = false;
            if (input.Peek() == '-')
            {
                negV = true;
                pos++;
                input.Read();
            }
            if (input.Peek() == '0' && (input.Read() != -1 && pos <= ++pos && (input.Peek() == 'x' || input.Peek() == 'X')))
            {
                hex = true;
                input.Read();
                pos++;
            }

            bool b = true;
            int stat = 0; // 0-val, 1-dec, 2-exp
            int c;
            while (b && input.Peek() != -1)
            {
                switch (c = input.Peek())
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        pos++;
                        input.Read();
                        if (stat == 0)
                        {
                            val *= (hex ? 16 : 10);
                            val += int.Parse(((char)c).ToString(), CultureInfo.InvariantCulture);
                        }
                        else if (stat == 1)
                        {
                            dec *= (hex ? 16 : 10);
                            dec += int.Parse(((char)c).ToString(), CultureInfo.InvariantCulture);
                            decC++;
                        }
                        else
                        {
                            exp *= (hex ? 16 : 10);
                            exp += int.Parse(((char)c).ToString(), CultureInfo.InvariantCulture);
                        }
                        break;
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'f':
                        pos++;
                        input.Read();
                        if (!hex)
                        {
                            b = false; break;
                        }
                        if (stat == 0)
                        {
                            val *= 16;
                            val += int.Parse(((char)c).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        }
                        else if (stat == 1)
                        {
                            dec *= 16;
                            dec += int.Parse(((char)c).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                            decC++;
                        }
                        else
                        {
                            exp *= 16;
                            exp += int.Parse(((char)c).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        }
                        break;
                    case 'e':
                    case 'p':
                        pos++;
                        input.Read();
                        if ((hex && c == 'p') || (!hex && c == 'e'))
                        {
                            if (stat == 2)
                                throw new SyntaxException("Can only have exponent designator('e' or 'p') per number.", line, pos - col, name);
                            stat = 2;

                            if (input.Peek() != -1)
                                throw new SyntaxException("Must specify at least one number for the exponent.", line, pos - col, name);
                            if (input.Peek() == '+' || (input.Peek() == '-' && (negE = true == true)))
                            {
                                pos++;
                                input.Read();
                                if (input.Peek() == -1)
                                    throw new SyntaxException("Must specify at least one number for the exponent.", line, pos - col, name);
                            }

                            if ("0123456789".Contains((char)input.Peek()))
                            {
                                pos++;
                                exp = int.Parse(((char)input.Read()).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                break;
                            }
                            else if (hex && "abcdefABCDEF".Contains((char)input.Peek()))
                            {
                                pos++;
                                exp = int.Parse(((char)input.Read()).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                break;
                            }
                            throw new SyntaxException("Must specify at least one number for the exponent.", line, pos - col, name);
                        }
                        else if (hex && c == 'e')
                        {
                            if (stat == 0)
                            {
                                val *= 16;
                                val += 14;
                            }
                            else if (stat == 1)
                            {
                                dec *= 16;
                                dec += 14;
                                decC++;
                            }
                            else
                            {
                                exp *= 16;
                                exp += 14;
                            }
                        }
                        else
                            b = false;
                        break;
                    case '.':
                        pos++;
                        input.Read();
                        if (stat == 0)
                            stat = 1;
                        else
                            throw new SyntaxException("A number can only have one decimal point(.).", line, pos - col, name);
                        break;
                    default:
                        b = false;
                        break;
                }
            }
            while (decC-- > 0) dec *= 0.1;
            val += dec;
            if (negV) dec *= -1;
            val *= Math.Pow((hex ? 2 : 10), (negE ? -exp : exp));
            if (double.IsInfinity(val))
                throw new SyntaxException("Number outside range of double.", line, pos - col, name);
            return val;
        }
        /// <summary>
        /// Checks whether two types are compatible and gets a conversion
        /// method if it can be.
        /// </summary>
        /// <param name="t1">The resultant type (e.g. the parameter type).</param>
        /// <param name="t2">The original type (e.g. the argument type).</param>
        /// <param name="meth">The resulting conversion method, will be static.</param>
        /// <returns>True if the two types can be implicitly cast, false if not.  If meth is null, the
        /// two types cannot be converted.</returns>
        public static bool TypesCompatible(Type t1, Type t2, out MethodInfo meth)
        {
            meth = null;

            if (t1 == t2)
                return true;

            // NOTE: This only checks for derived classes and interfaces,
            //  this will not work for implicit/explicit casts.
            if (t1.IsAssignableFrom(t2))
                return true;

            // all numeric types are explicitly compatible but do not
            //   define a cast in their type.
            if ((t1 == typeof(SByte) ||
                t1 == typeof(Int16) ||
                t1 == typeof(Int32) ||
                t1 == typeof(Int64) ||
                t1 == typeof(Single) ||
                t1 == typeof(Double) ||
                t1 == typeof(UInt16) ||
                t1 == typeof(UInt32) ||
                t1 == typeof(UInt64) ||
                t1 == typeof(Byte) ||
                t1 == typeof(Decimal) ||
                t1 == typeof(Char)) &&
                (t2 == typeof(SByte) ||
                t2 == typeof(Int16) ||
                t2 == typeof(Int32) ||
                t2 == typeof(Int64) ||
                t2 == typeof(Single) ||
                t2 == typeof(Double) ||
                t2 == typeof(UInt16) ||
                t2 == typeof(UInt32) ||
                t2 == typeof(UInt64) ||
                t2 == typeof(Byte) ||
                t2 == typeof(Decimal) ||
                t2 == typeof(Char)))
            {
                // although they are compatible, they need to be converted,
                //  return false and get the Convert.ToXX method.
                meth = typeof(Convert).GetMethod("To" + t1.Name, new Type[] { t2 });
                return false;
            }

            // get any methods from t1 that is not marked with LuaIgnoreAttribute
            //  and has the name 'op_Explicit' or 'op_Implicit' and has a return type
            //  of t1 and a sole argument that is implicitly compatible with t2.
            meth = t1.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length == 0)
                .Where(m => (m.Name == "op_Explicit" || m.Name == "op_Implicit") && m.ReturnType == t1 &&
                    m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(t2))
                    .FirstOrDefault();

            // if meth still equals null, check for a cast in t2.
            if (meth == null)
            {
                meth = t2.GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length == 0)
                    .Where(m => (m.Name == "op_Explicit" || m.Name == "op_Implicit") && m.ReturnType == t1 &&
                        m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(t2))
                        .FirstOrDefault();
            }

            // still return false even if we found a cast
            //   because the types are not implicitly compatible.
            return false;
        }
        /// <summary>
        /// Converts an object to a given type using TypesCompatible.
        /// </summary>
        /// <param name="o">The object to convert.</param>
        /// <param name="t">The type to convert to.</param>
        /// <returns>An object that can be passed in MethodInfo.Invoke.</returns>
        public static object ConvertType(object o, Type t)
        {
            // stop now if no type
            if (t == null || t == typeof(void))
                return null;

            // if the object is null, any type that is not
            //  a value type can be used.
            if (o == null)
            {
                if (t.IsValueType)
                    throw new InvalidCastException("Cannot convert 'null' to type '" + t + "'.");
                return o;
            }

            // if type is an array, convert according to underlying type
            if (t.IsArray)
            {
                // get the original array.
                object[] orig;
                if (o is IEnumerable)
                    orig = (o as IEnumerable).Cast<object>().ToArray();
                else
                    orig = new[] { o };

                // get the underlying type
                t = t.GetElementType();

                // get the resulting array
                object[] result = new object[orig.Length];
                for (int i = 0; i < orig.Length; i++)
                {
                    result[i] = ConvertType(orig[i], t);
                }

                return result;
            }
            else
            {
                // if o is a MultipleReturn, get the first object
                if (o is MultipleReturn)
                    o = (o as MultipleReturn)[0];

                // check with TypesCompatible to get whether
                //  it can be converted.
                MethodInfo m;
                if (!TypesCompatible(t, o.GetType(), out m))
                {
                    // if there is no conversion method, throw an exception.
                    if (m != null)
                        o = m.Invoke(null, new object[] { o });
                    else
                        throw new InvalidCastException("Cannot convert type '" + o.GetType() + "' to type '" + t + "'.");
                }
                return o;
            }
        }
        /// <summary>
        /// A runtime version of overload-resolution.  Searches each of the methods
        /// to find a method with the given arguments while converting the arguments
        /// to work.
        /// </summary>
        /// <param name="meths">The methods to search in.</param>
        /// <param name="args">The arguments to use, must pass a reference to an array because the
        /// arguments in this array will be converted to work (i.e. don't use list.ToArray()).</param>
        /// <returns>The method that will work with the arguments or null if none work.</returns>
        /// <remarks>
        /// This currently does not support parrams arrays or optional arguments, this may be
        /// added in newer releases.
        /// </remarks>
        public static Tuple<MethodBase, object> GetCompatibleMethod(Tuple<MethodBase, object>[] meths, object[] args)
        {
            Tuple<MethodBase, object> ret = null;
            int max = int.MaxValue;

            /* loop first to find exact match for the types */
            foreach (var item in meths)
            {
                var param = item.Item1.GetParameters();
                bool cont = false;
                int cur = 0;
                if (param.Length != args.Length)
                    continue;

                for (int i = 0; i < param.Length; i++)
                {
                    if (param[i].ParameterType == typeof(object))
                        continue;

                    if (args[i] == null)
                    {
                        if (param[i].ParameterType.IsValueType)
                        {
                            cont = true;
                            break;
                        }
                    }
                    else
                    {
                        if (!param[i].ParameterType.IsAssignableFrom(args[i].GetType()))
                        {
                            cont = true;
                            break;
                        }
                        if (param[i].ParameterType != args[i].GetType())
                            cur++;
                    }
                }
                if (cont) continue;

                if (ret == null || cur < max)
                {
                    ret = item;
                    max = cur;
                }
                else
                {
                    throw new AmbiguousMatchException();
                }
            }

            if (ret != null)
                return ret;
            max = int.MaxValue;

            /* loop again for a less specific type, or if a user-defined cast exists */
            foreach (var item in meths)
            {
                var param = item.Item1.GetParameters();
                bool cont = false;
                int cur = 0;
                if (param.Length != args.Length)
                    continue;

                for (int i=0; i<param.Length; i++)
                {
                    if (param[i].ParameterType == typeof(object))
                        continue;

                    if (args[i] == null)
                    {
                        if (param[i].ParameterType.IsValueType)
                        {
                            cont = true;
                            break;
                        }
                        continue;
                    }

                    MethodInfo meth;
                    if (!TypesCompatible(param[i].ParameterType, args[i].GetType(), out meth))
                    {
                        if (meth != null)
                        {
                            args[i] = meth.Invoke(null, new object[] { args[i] });
                        }
                        else
                        {
                            cont = true;
                            break;
                        }
                    }
                    if (param[i].ParameterType != args[i].GetType())
                        cur++;
                }

                if (cont) continue;

                if (ret == null || cur < max)
                {
                    ret = item;
                    max = cur;
                }
                else
                    throw new AmbiguousMatchException();
            }

            return ret;
        }
        /// <summary>
        /// This is called whenever a binary operation occurs to determine which function to call.
        /// </summary>
        /// <param name="lhs">The left-hand operand.</param>
        /// <param name="type">The type of operation.</param>
        /// <param name="rhs">The right-hand operand.</param>
        /// <returns>The result of the operation.</returns>
        public static object ResolveBinaryOperation(object lhs, BinaryOperationType type, object rhs)
        {
            switch (type)
            {
                case BinaryOperationType.Add:
                case BinaryOperationType.Subtract:
                case BinaryOperationType.Multiply:
                case BinaryOperationType.Divide:
                case BinaryOperationType.Power:
                case BinaryOperationType.Modulo:
                case BinaryOperationType.Gt:
                case BinaryOperationType.Lt:
                case BinaryOperationType.Gte:
                case BinaryOperationType.Lte:
                    {
                        // get the operatand's true value.
                        object o1 = lhs is LuaPointer ? GetValue(lhs) : lhs;
                        object o2 = rhs is LuaPointer ? GetValue(rhs) : rhs;
                        LuaUserData u1 = o1 as LuaUserData;
                        LuaUserData u2 = o2 as LuaUserData;

                        // check that if one operand is UserData that the operator is visible.
                        if (u1 != null && u1.Members != null && !u1.Members.Contains(GetBinName(type)))
                            throw new InvalidOperationException(type + " operator is inaccessable to Lua code.");
                        if (u2 != null && u2.Members != null && !u2.Members.Contains(GetBinName(type)))
                            throw new InvalidOperationException(type + " operator is inaccessable to Lua code.");

                        // if operand is multiple return, use the first result.
                        if (o1 is MultipleReturn)
                            o1 = GetValue((o1 as MultipleReturn)[0]);
                        if (o2 is MultipleReturn)
                            o2 = GetValue((o2 as MultipleReturn)[0]);

                        if (o1 is LuaClass || o2 is LuaClass)
                            throw new InvalidOperationException("Attempted to perform arithmetic on a 'class definition' object.");

                        // get the type of the operands.
                        LuaValueType t1 = GetType(u1 == null ? o1 : u1), t2 = GetType(u2 != null ? u2 : o2);

                        if (t1 == LuaValueType.Number && t2 == LuaValueType.Number)
                            return NativeArithmetic((double)(u1 == null ? o1 : u1), (double)(u2 != null ? u2 : o2), type);
                        else if (t1 != LuaValueType.UserData && t2 != LuaValueType.UserData)
                        {
                            string err = t1 == LuaValueType.UserData || t1 == LuaValueType.Number ? t2.ToString() : t1.ToString();

                            throw new InvalidOperationException("Attempted to perform arithmetic on a '" + err.ToLower(CultureInfo.InvariantCulture) + "' value.");
                        }
                        else
                        {
                            // try the first type
                            if (t1 == LuaValueType.UserData)
                            {
                                Type user = u1 == null ? o1.GetType() : u1.Value.GetType();
                                object[] args = new[] { (u1 == null ? o1 : u1.Value), (u2 == null ? o2 : u2.Value) };
                                var ret = GetCompatibleMethod(
                                    user.GetMethods()
                                        .Where(m => m.Name == GetBinName(type) && m.Attributes.HasFlag(MethodAttributes.Static) && m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Count() == 0)
                                        .Select(m => new Tuple<MethodBase, object>(m, null))
                                        .ToArray(),
                                    args
                                );
                                if (ret != null)
                                {
                                    return ret.Item1.Invoke(null, args);
                                }
                            }

                            // try the second type
                            if (t2 == LuaValueType.UserData)
                            {
                                var user = u2 == null ? o2.GetType() : u2.Value.GetType();
                                var args = new[] { (u1 == null ? o1 : u1.Value), (u2 == null ? o2 : u2.Value) };
                                var ret = GetCompatibleMethod(
                                    user.GetMethods()
                                        .Where(m => m.Name == GetBinName(type) && m.Attributes.HasFlag(MethodAttributes.Static) && m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Count() == 0)
                                        .Select(m => new Tuple<MethodBase, object>(m, null))
                                        .ToArray(),
                                    args
                                );
                                if (ret != null)
                                {
                                    return ret.Item1.Invoke(null, args);
                                }
                            }

                            throw new InvalidOperationException("Unable to find an operator that matches the given operands.");
                        }
                    }
                case BinaryOperationType.Concat:
                    return (GetValue(lhs) ?? "").ToString() + (GetValue(rhs) ?? "").ToString();
                case BinaryOperationType.Equals:
                case BinaryOperationType.NotEquals:
                    {
                        object o1 = RuntimeHelper.GetValue(lhs);
                        object o2 = RuntimeHelper.GetValue(rhs);

                        bool b = object.Equals(o1, o2);
                        return type == BinaryOperationType.Equals ? b : !b;
                    }
                case BinaryOperationType.And:
                    {
                        object o = GetValue(lhs);
                        if (o != null && o as bool? != false)
                        {
                            o = GetValue(rhs);
                        }

                        return (o);
                    }
                case BinaryOperationType.Or:
                    {
                        object o = GetValue(lhs);
                        if (o == null || o as bool? == false)
                        {
                            o = GetValue(rhs);
                        }

                        return (o);
                    }
                default:
                    throw new InvalidOperationException("Unable to resolve BinaryOperation." + type);
            }
        }
        /// <summary>
        /// This is called whenever a unary operation occurs to determine which function to call.
        /// </summary>
        /// <param name="type">The type of operation.</param>
        /// <param name="target">The target of the operation.</param>
        /// <returns>The result of the operation.</returns>
        public static object ResolveUnaryOperation(UnaryOperationType type, object target)
        {
            switch (type)
            {
                case UnaryOperationType.Minus:
                    {
                        target = GetValue(target);
                        LuaValueType t1 = GetType(target);

                        if (t1 == LuaValueType.Number)
                        {
                            return -(double)target;
                        }
                        else if (t1 == LuaValueType.UserData)
                        {
                            Type t = target.GetType();
                            MethodInfo meth = t.GetMethod("op_UnaryMinus");
                            if (meth == null || meth.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Count() > 0)
                                throw new InvalidOperationException("User data type '" + t + "' does not define a unary-minus operator.");

                            return meth.Invoke(null, new[] { target });
                        }
                        else
                            throw new InvalidOperationException("Attempted to perform arithmetic on a '" + 
                                t1.ToString().ToLower(CultureInfo.InvariantCulture) + "' value.");
                    }
                case UnaryOperationType.Not:
                    return !IsTrue(target);
                case UnaryOperationType.Length:
                    {
                        target = GetValue(target);
                        LuaValueType t1 = GetType(target);

                        if (t1 == LuaValueType.String)
                        {
                            return (double)((string)target).Length;
                        }
                        else if (t1 == LuaValueType.Table)
                        {
                            double d = ((LuaTable)target).GetSequenceLen();
                            return d == -1 ? 0 : d;
                        }
                        else if (t1 == LuaValueType.UserData)
                        {
                            Type t = target.GetType();
                            bool flag = false;
                            var mems = t.GetMember("Length");
                        validate:
                            if (mems != null && mems.Length > 0)
                            {
                                foreach (var item in mems)
                                {
                                    if (item.MemberType == MemberTypes.Method)
                                    {
                                        MethodInfo meth = item as MethodInfo;
                                        if ((!meth.Attributes.HasFlag(MethodAttributes.Static) && meth.GetParameters().Length == 0) ||
                                            (meth.Attributes.HasFlag(MethodAttributes.Static) && meth.GetParameters().Length == 1) && meth.GetParameters()[0].ParameterType.IsAssignableFrom(t))
                                        {
                                            if (meth.Attributes.HasFlag(MethodAttributes.Static))
                                                return meth.Invoke(null, new[] { target });
                                            else
                                                return meth.Invoke(target, null);
                                        }
                                    }
                                    else if (item.MemberType == MemberTypes.Field)
                                    {
                                        FieldInfo field = item as FieldInfo;
                                        return field.GetValue(target);
                                    }
                                    else if (item.MemberType == MemberTypes.Property)
                                    {
                                        PropertyInfo field = item as PropertyInfo;
                                        return field.GetValue(target, null);
                                    }
                                }
                            }
                            if (!flag)
                            {
                                flag = true;
                                mems = t.GetMember("Count");
                                goto validate;
                            }
                            throw new InvalidOperationException("User data type '" + t + "' does not define a 'Length' or 'Count' member.");
                        }

                        throw new InvalidOperationException("Attempted to perform arithmetic on a '" + 
                            t1.ToString().ToLower(CultureInfo.InvariantCulture) + "' value.");
                    }
            }
            return null;
        }
        /// <summary>
        /// Called when the code encounters the 'class' keyword.  Defines a LuaClass
        /// object with the given name.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="types">The types that the class will derive.</param>
        /// <param name="name">The name of the class.</param>
        public static void DefineClass(LuaEnvironment E, List<string> types, string name)
        {
            if (E._globals.GetItemRaw(name) != null)
                throw new InvalidOperationException("The name '" + name + "' is already a global variable and cannot be a class name.");

            // resolve each of the types
            Type b = null;
            List<Type> inter = new List<Type>();
            foreach (var item in types)
            {
                // get the types that this Lua code can access according to the settings.
                Type[] access;
                if (E.Settings.ClassAccess == LuaClassAccess.All)
                    access = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).ToArray();
                else if (E.Settings.ClassAccess == LuaClassAccess.System)
                {
                    access = E._globals.Where(k => k.Value is LuaType).Select(k => (k.Value as LuaType).Type).ToArray();
                    access = access.Then(AppDomain.CurrentDomain.GetAssemblies().Where(a => Resources.Whitelist.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Contains(a.GetName().GetPublicKey().ToStringBase16())).SelectMany(a => a.GetTypes())).ToArray();
                }
                else
                    access = E._globals.Where(k => k.Value is LuaType).Select(k => (k.Value as LuaType).Type).ToArray();

                // get the types that match the given name.
                Type[] typesa = access.Where(t => t.Name == item || t.FullName == item).ToArray();
                if (typesa == null || typesa.Length == 0)
                    throw new InvalidOperationException("Unable to locate the type '" + item + "'");
                if (typesa.Length > 1)
                    throw new InvalidOperationException("More than one type found for name '" + name + "'");
                Type type = typesa.FirstOrDefault();

                if (!type.Attributes.HasFlag(TypeAttributes.Public))
                    throw new InvalidOperationException("Base class and interfaces must be public");

                if (type.IsClass)
                {
                    // if the type is a class, it will be the base class
                    if (b == null)
                    {
                        if (type.IsSealed)
                            throw new InvalidOperationException("Cannot derive from a sealed class.");
                        if (type.GetConstructor(new Type[0]) == null)
                            throw new InvalidOperationException("Cannot derive from a type without an empty constructor.");

                        b = type;
                    }
                    else
                        throw new InvalidOperationException("Can only derive from a single base class.");
                }
                else if (type.IsInterface)
                    inter.Add(type);
                else
                    throw new InvalidOperationException("Cannot derive from a value-type.");
            }

            // create and register the LuaClass object.
            LuaClass c = new LuaClass(name, b, inter.ToArray(), E);
            E._globals.SetItemRaw(name, c);
        }
        public static object GetFunction(LuaEnvironment E, Type t, string name, object target)
        {
            LuaMethod r = new LuaMethod(t.GetMethod(name), target, name, E);
            return (r);
        }
        /// <summary>
        /// Creates a pointer to a global Lua value.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the value.</param>
        /// <returns>A pointer to the value.</returns>
        public static object GetGlobal(LuaEnvironment E, string name)
        {
            return new LuaPointer(E._globals, name, E);
        }
        /// <summary>
        /// Used in a VarInitItem to set the values.  It creates an array
        /// of a given length from the given values.  Expands any MultipleReturns
        /// and adds nulls as necessary.
        /// </summary>
        /// <param name="values">The input values.</param>
        /// <param name="names">The length of the array.</param>
        /// <returns>An array of the given length to set the values to.</returns>
        public static object[] SetValues(List<object> values, int names)
        {
            object[] val = new object[names];
            for (int i = 0; i < names; i++)
            {
                object o = i < values.Count ? GetValue(values[i]) : null;
                if (o is MultipleReturn)
                {
                    if (i < values.Count - 1)
                        o = GetValue(((MultipleReturn)o)[0]);
                    else
                    {
                        using (var itr = ((MultipleReturn)o).GetEnumerator())
                        {
                            while (i < names && itr.MoveNext())
                            {
                                val[i] = itr.Current;
                                i++;
                            }
                            break;
                        }
                    }
                }
                val[i] = o;
            }

            return val;
        }
        /// <summary>
        /// Used by a ForGenItem to start the loop.
        /// </summary>
        /// <param name="exp">The expressions for the intial values.</param>
        /// <param name="f">The function to call.</param>
        /// <param name="s">The value to pass to each call.</param>
        /// <param name="var">The initial value.</param>
        public static void ForGenStart(List<object> exp, ref object f, ref object s, ref object var)
        {
            int j = 0;
            for (int i = 0; i < exp.Count; i++)
            {
                object o = GetValue(exp[i]);
                if (o is MultipleReturn)
                {
                    if (i + 1 <= exp.Count)
                    {
                        foreach (var item in (o as MultipleReturn))
                        {
                            o = GetValue(item);
                            if (o is MultipleReturn)
                                o = (o as MultipleReturn)[0];

                            if (j == 0)
                                f = o;
                            else if (j == 1)
                                s = o;
                            else if (j == 2)
                                var = o;
                            else
                                return;
                            j++;
                        }
                    }
                    else
                        o = (o as MultipleReturn)[0];
                }

                if (j == 0)
                    f = o;
                else if (j == 1)
                    s = o;
                else if (j == 2)
                    var = o;
                else
                    return;
                j++;
            }
        }
    }
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
}