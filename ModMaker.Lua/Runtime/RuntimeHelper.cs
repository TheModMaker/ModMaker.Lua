using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua.Parser.Items;
using System.Reflection;
using System.Globalization;
using System.Reflection.Emit;
using System.IO;

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

    class RuntimeHelper
    {
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

        public static object GetValue(object value)
        {
            LuaPointerNew pointer = value as LuaPointerNew;
            while (pointer != null)
            {
                value = pointer.GetValue();
                pointer = value as LuaPointerNew;
            }
            return value;
        }
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
        public static object Indexer(LuaEnvironment E, object value, object index)
        {
            if (value is MultipleReturn)
                value = GetValue(((MultipleReturn)value)[0]);

            if (value is LuaTable || GetType(value) == LuaValueType.UserData || value is LuaPointerNew)
                return new LuaPointerNew(value, index, E);
            else
                throw new InvalidOperationException("Attempt to index a '" + GetType(value) + "' type.");
        }
        public static void SetValue(ref object index, object value)
        {
            value = GetValue(value);
            if (value is MultipleReturn)
                value = GetValue(((MultipleReturn)value)[0]);

            if (index is LuaPointerNew)
                (index as LuaPointerNew).SetValue(value);
            else
                index = value;
        }
        public static bool IsTrue(object value)
        {
            object o = GetValue(value);
            bool ret = !(o == null || o as bool? == false);
            return ret;
        }
        public static double? ToNumber(object value)
        {
            object o = GetValue(value);
            if (o == null)
                return null;
            else if (o is double)
                return (double)o;
            else if (o is string)
            {
                return RuntimeHelper.ReadNumber(o as string);
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

        public static double? ReadNumber(string input)
        {
            if (input == null)
                return null;

            int cur = 0;
            bool hex = false;
            double val = 0, exp = 0, dec = 0;
            int decC = 0;
            bool negV = false, negE = false;
            if (input[cur] == '-')
            {
                negV = true;
                cur++;
            }
            if (input[cur] == '0' && (input[cur + 1] == 'x' || input[cur + 1] == 'X'))
            {
                hex = true;
                cur += 2;
            }

            bool b = true;
            int stat = 0; // 0-val, 1-dec, 2-exp
            char c;
            while (b && cur < input.Length)
            {
                switch (c = input[cur])
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
                        cur++;
                        if (stat == 0)
                        {
                            val *= (hex ? 16 : 10);
                            val += int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                        }
                        else if (stat == 1)
                        {
                            dec *= (hex ? 16 : 10);
                            dec += int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                            decC++;
                        }
                        else
                        {
                            exp *= (hex ? 16 : 10);
                            exp += int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                        }
                        break;
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'f':
                        cur++;
                        if (!hex)
                        {
                            b = false; break;
                        }
                        if (stat == 0)
                        {
                            val *= 16;
                            val += int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        }
                        else if (stat == 1)
                        {
                            dec *= 16;
                            dec += int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                            decC++;
                        }
                        else
                        {
                            exp *= 16;
                            exp += int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        }
                        break;
                    case 'e':
                    case 'p':
                        cur++;
                        if ((hex && c == 'p') || (!hex && c == 'e'))
                        {
                            if (stat == 2)
                                return null;
                            stat = 2;

                            if (cur >= input.Length)
                                return null;
                            if (input[cur] == '+' || (input[cur] == '-' && (negE = true == true)))
                            {
                                cur++;
                                if (cur >= input.Length)
                                    return null;
                            }

                            if ("0123456789".Contains(input[cur]))
                            {
                                exp = int.Parse(input[cur++].ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                break;
                            }
                            else if (hex && "abcdefABCDEF".Contains(input[cur]))
                            {
                                exp = int.Parse(input[cur++].ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                break;
                            }
                            return null;
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
                        cur++;
                        if (stat == 0)
                            stat = 1;
                        else
                            return null;
                        break;
                    default:
                        b = false;
                        break;
                }
            }
            while (input[cur] == ' ' || input[cur] == '\t' || input[cur] == '\n') cur++;
            if (cur < input.Length)
                return null;
            while (decC-- > 0) dec *= 0.1;
            val += dec;
            if (negV) dec *= -1;
            val *= Math.Pow((hex ? 2 : 10), (negE ? -exp : exp));
            if (double.IsInfinity(val))
                return null;
            return val;
        }
        public static double? ReadNumber(StreamReader input)
        {
            if (input == null)
                return null;

            bool hex = false;
            double val = 0, exp = 0, dec = 0;
            int decC = 0;
            bool negV = false, negE = false;
            if (input.Peek() == '-')
            {
                negV = true;
                input.Read();
            }
            if (input.Peek() == '0' && (input.Read() != -1 && (input.Peek() == 'x' || input.Peek() == 'X')))
            {
                hex = true;
                input.Read();
            }

            bool b = true;
            int stat = 0; // 0-val, 1-dec, 2-exp
            char c;
            while (b && !input.EndOfStream)
            {
                switch (c = (char)input.Peek())
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
                        input.Read();
                        if (stat == 0)
                        {
                            val *= (hex ? 16 : 10);
                            val += int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                        }
                        else if (stat == 1)
                        {
                            dec *= (hex ? 16 : 10);
                            dec += int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                            decC++;
                        }
                        else
                        {
                            exp *= (hex ? 16 : 10);
                            exp += int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                        }
                        break;
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'f':
                        input.Read();
                        if (!hex)
                        {
                            b = false; break;
                        }
                        if (stat == 0)
                        {
                            val *= 16;
                            val += int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        }
                        else if (stat == 1)
                        {
                            dec *= 16;
                            dec += int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                            decC++;
                        }
                        else
                        {
                            exp *= 16;
                            exp += int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        }
                        break;
                    case 'e':
                    case 'p':
                        input.Read();
                        if ((hex && c == 'p') || (!hex && c == 'e'))
                        {
                            if (stat == 2)
                                return null;
                            stat = 2;

                            if (input.EndOfStream)
                                return null;
                            if (input.Peek() == '+' || (input.Peek() == '-' && (negE = true == true)))
                            {
                                input.Read();
                                if (input.EndOfStream)
                                    return null;
                            }

                            if ("0123456789".Contains((char)input.Peek()))
                            {
                                exp = int.Parse(((char)input.Read()).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                break;
                            }
                            else if (hex && "abcdefABCDEF".Contains((char)input.Peek()))
                            {
                                exp = int.Parse(((char)input.Read()).ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                break;
                            }
                            return null;
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
                        input.Read();
                        if (stat == 0)
                            stat = 1;
                        else
                            return null;
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
                return null;
            return val;
        }
        public static bool TypesCompatible(Type t1, Type t2, out MethodInfo meth)
        {
            meth = null;

            if (t1 == t2)
                return true;

            if (t1.IsAssignableFrom(t2))
                return true;

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
                meth = typeof(Convert).GetMethod("To" + t1.Name, new Type[] { t2 });
                return false;
            }

            meth = t1.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length == 0)
                .Where(m => (m.Name == "op_Explicit" || m.Name == "op_Implicit") && m.ReturnType == t1 &&
                    m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(t2))
                    .FirstOrDefault();

            if (meth == null)
            {
                meth = t2.GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length == 0)
                    .Where(m => (m.Name == "op_Explicit" || m.Name == "op_Implicit") && m.ReturnType == t1 &&
                        m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(t2))
                        .FirstOrDefault();
            }

            return false;
        }
        public static object ConvertType(object o, Type t)
        {
            if (o == null)
            {
                if (t.IsValueType)
                    throw new InvalidCastException("Cannot convert 'null' to type '" + t + "'.");
                return o;
            }

            MethodInfo m;
            if (!TypesCompatible(t, o.GetType(), out m))
            {
                if (m != null)
                    o = m.Invoke(null, new object[] { o });
                else
                    throw new InvalidCastException("Cannot convert type '" + o.GetType() + "' to type '" + t + "'.");
            }
            return o;
        }
        public static object ConvertReturnType(MultipleReturn ret, Type t)
        {
            return ret[0];
        }
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
                        object o1 = lhs is LuaPointerNew ? GetValue(lhs) : lhs;
                        object o2 = rhs is LuaPointerNew ? GetValue(rhs) : rhs;
                        LuaUserData u1 = o1 as LuaUserData;
                        LuaUserData u2 = o2 as LuaUserData;

                        if (u1 != null && u1.Members != null && !u1.Members.Contains(GetBinName(type)))
                            throw new InvalidOperationException(type + " operator is inaccessable to Lua code.");
                        if (u2 != null && u2.Members != null && !u2.Members.Contains(GetBinName(type)))
                            throw new InvalidOperationException(type + " operator is inaccessable to Lua code.");

                        if (o1 is MultipleReturn)
                            o1 = GetValue((o1 as MultipleReturn)[0]);
                        if (o2 is MultipleReturn)
                            o2 = GetValue((o2 as MultipleReturn)[0]);

                        if (o1 is LuaClass || o2 is LuaClass)
                            throw new InvalidOperationException("Attempted to perform arithmetic on a 'class definition' object.");

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
        public static void DefineClass(LuaEnvironment E, List<string> types, string name)
        {
            if (E._globals.GetItemRaw(name) != null)
                throw new InvalidOperationException("The name '" + name + "' is already a global variable and cannot be a class name.");

            Type b = null;
            List<Type> inter = new List<Type>();
            foreach (var item in types)
            {
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

                Type[] typesa = access.Where(t => t.Name == item || t.FullName == item).ToArray();
                if (typesa.Length > 1)
                    throw new InvalidOperationException("More than one type found for name '" + name + "'");
                Type type = typesa.FirstOrDefault();

                if (type == null)
                    throw new InvalidOperationException("Unable to locate the type '" + item + "'");

                if (!type.Attributes.HasFlag(TypeAttributes.Public))
                    throw new InvalidOperationException("Base class and interfaces must be public");

                if (type.IsClass)
                {
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

            LuaClass c = new LuaClass(name, b, inter.ToArray(), E);
            E._globals.SetItemRaw(name, c);
        }
        public static object GetFunction(LuaEnvironment E, Type t, string name, object target)
        {
            LuaMethod r = new LuaMethod(t.GetMethod(name), target, name, E);
            return (r);
        }
        public static object GetGlobal(LuaEnvironment E, string name)
        {
            return new LuaPointerNew(E._globals, name, E);
        }
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
}