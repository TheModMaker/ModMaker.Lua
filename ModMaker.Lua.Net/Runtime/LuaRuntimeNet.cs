using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines the different types of values in Lua.
    /// </summary>
    public enum LuaValueType
    {
        /// <summary>
        /// Represents a 'nil' value.
        /// </summary>
        Nil,
        /// <summary>
        /// Represents a string of characters.
        /// </summary>
        String,
        /// <summary>
        /// Represents a truth value (true/false).
        /// </summary>
        Bool,
        /// <summary>
        /// Represents a table of values.
        /// </summary>
        Table,
        /// <summary>
        /// Represents a Lua function.
        /// </summary>
        Function,
        /// <summary>
        /// Represents a real number.
        /// </summary>
        Number,
        /// <summary>
        /// Represents a Lua thread.
        /// </summary>
        Thread,
        /// <summary>
        /// Represents a user defined type.
        /// </summary>
        UserData,
    }

    /// <summary>
    /// Defines the default Lua runtime.  This class is incharge of resolving
    /// operators and converting types.  This can be inherited to modify it's 
    /// behaviour.
    /// </summary>
    public class LuaRuntimeNet : ILuaRuntime
    {
        /// <summary>
        /// Creates a new instance of the default LuaRuntime.
        /// </summary>
        protected LuaRuntimeNet() { }

        /// <summary>
        /// Contains the LuaRuntimeImpl type for generating LuaRuntime objects.
        /// </summary>
        private static Type runtimeType = null;

        /// <summary>
        /// Creates a new instance of LuaRuntime.
        /// </summary>
        /// <returns>A new LuaRuntime object.</returns>
        /// <remarks>
        /// This is needed because the Invoke method needs to have 
        /// OpCodes.Tailcall in order to have proper tail calls support.
        /// Because C# does not add these, the Invoke method must be generated
        /// at runtime.
        /// </remarks>
        public static LuaRuntimeNet Create()
        {
            if (runtimeType == null)
                CreateType();

            if (Lua.UseDynamicTypes)
                return (LuaRuntimeNet)Activator.CreateInstance(runtimeType);
            else
                return new LuaRuntimeNet();
        }
        /// <summary>
        /// Create the dynamic type.
        /// </summary>
        static void CreateType()
        {
            if (runtimeType != null)
                return;

            var tb = NetHelpers.GetModuleBuilder().DefineType("LuaRuntimeImpl",
                TypeAttributes.Public, typeof(LuaRuntimeNet), new Type[0]);

            //// .ctor();
            var ctor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[0]);
            var gen = ctor.GetILGenerator();

            // base();
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, typeof(LuaRuntimeNet).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[0], null));
            gen.Emit(OpCodes.Ret);

            //// override MultipleReturn Invoke(ILuaEnvironment E, object self, object value, int overload, bool memberCall, object[] args, int[] byRef);
            var mb = tb.DefineMethod("Invoke",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(MultipleReturn), new[] { typeof(ILuaEnvironment), typeof(object), typeof(object), typeof(int), typeof(bool), typeof(object[]), typeof(int[]) });
            gen = mb.GetILGenerator();
            var next = gen.DefineLabel();
            var call = gen.DefineLabel();

            // if (E == null) throw new ArgumentNullException("E");
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Brtrue, next);
            gen.Emit(OpCodes.Ldstr, "E");
            gen.Emit(OpCodes.Newobj, typeof(ArgumentNullException).GetConstructor(new[] { typeof(string) }));
            gen.Emit(OpCodes.Throw);

            // if (value is MultipleReturn) value = ((MultipleReturn)value)[0];
            gen.MarkLabel(next);
            next = gen.DefineLabel();
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Isinst, typeof(MultipleReturn));
            gen.Emit(OpCodes.Brfalse, next);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Castclass, typeof(MultipleReturn));
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Callvirt, typeof(MultipleReturn).GetMethod("get_Item"));
            gen.Emit(OpCodes.Starg, 3);

            // if (value is LuaUserData) value = ((LuaUserData)value).Backing;
            gen.MarkLabel(next);
            next = gen.DefineLabel();
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Isinst, typeof(LuaUserData));
            gen.Emit(OpCodes.Brfalse, next);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Castclass, typeof(LuaUserData));
            gen.Emit(OpCodes.Callvirt, typeof(LuaUserData).GetMethod("get_Backing"));
            gen.Emit(OpCodes.Starg, 3);

            // if (!(value is IMethod)) goto next;
            gen.MarkLabel(next);
            next = gen.DefineLabel();
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Isinst, typeof(IMethod));
            gen.Emit(OpCodes.Brfalse, next);

            // return value.Invoke(self, memberCall, overload, byRef, args);
            gen.MarkLabel(call);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Castclass, typeof(IMethod));
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Ldarg, 5);
            gen.Emit(OpCodes.Ldarg, 4);
            gen.Emit(OpCodes.Ldarg, 7);
            gen.Emit(OpCodes.Ldarg, 6);
            gen.Emit(OpCodes.Tailcall);
            gen.Emit(OpCodes.Callvirt, typeof(IMethod).GetMethod("Invoke",
                new[] { typeof(object), typeof(bool), typeof(int), typeof(int[]), typeof(object[]) }));
            gen.Emit(OpCodes.Ret);

            // throw new InvalidOperationException("Attempt to call a '" + GetValueType(value) + "' type.");
            gen.MarkLabel(next);
            gen.Emit(OpCodes.Ldstr, "Attempt to call a '");
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Callvirt, typeof(LuaRuntimeNet).GetMethod("GetValueType", BindingFlags.NonPublic | BindingFlags.Instance));
            gen.Emit(OpCodes.Box, typeof(LuaValueType));
            gen.Emit(OpCodes.Ldstr, "' type.");
            gen.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new[] { typeof(object), typeof(object), typeof(object) }));
            gen.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) }));
            gen.Emit(OpCodes.Throw);
            gen.Emit(OpCodes.Ret);

            runtimeType = tb.CreateType();
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
        protected virtual bool TypesCompatible(Type/*!*/ sourceType, Type/*!*/ destType, out MethodInfo method, out int amount)
        {
            return NetHelpers.TypesCompatible(sourceType, destType, out method, out amount);
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
        protected virtual object ConvertType(object source, Type type, out int amount)
        {
            return NetHelpers.ConvertType(source, type, out amount);
        }
        
        /// <summary>
        /// Defines arithmetic between two doubles.
        /// </summary>
        /// <param name="value1">The first value.</param>
        /// <param name="value2">The second value.</param>
        /// <param name="type">The type of operation.</param>
        /// <returns>The result of the operation.</returns>
        protected virtual object NativeArithmetic(double value1, double value2, BinaryOperationType type)
        {
            switch (type)
            {
                case BinaryOperationType.Add:
                    return (value1 + value2);
                case BinaryOperationType.Subtract:
                    return (value1 - value2);
                case BinaryOperationType.Multiply:
                    return (value1 * value2);
                case BinaryOperationType.Divide:
                    return (value1 / value2);
                case BinaryOperationType.Power:
                    return (Math.Pow(value1, value2));
                case BinaryOperationType.Modulo:
                    return (value1 % value2);
                case BinaryOperationType.Concat:
                    return (value1.ToString(CultureInfo.CurrentCulture) + value2.ToString(CultureInfo.CurrentCulture));
                case BinaryOperationType.Gt:
                    return (value1 > value2);
                case BinaryOperationType.Lt:
                    return (value1 < value2);
                case BinaryOperationType.Gte:
                    return (value1 >= value2);
                case BinaryOperationType.Lte:
                    return (value1 <= value2);
                case BinaryOperationType.Equals:
                    return (value1 == value2);
                case BinaryOperationType.NotEquals:
                    return (value1 != value2);
                case BinaryOperationType.And:
                    return (value2);
                case BinaryOperationType.Or:
                    return (value1);
                default:
                    throw new NotImplementedException();
            }
        }
        /// <summary>
        /// Gets the type of a given object.
        /// </summary>
        /// <param name="source">The object to check.</param>
        /// <returns>The type of the given object.</returns>
        protected virtual LuaValueType GetValueType(object source)
        {
            if (source == null)
                return LuaValueType.Nil;

            else if (source is double)
                return LuaValueType.Number;
            else if (source is string)
                return LuaValueType.String;
            else if (source is bool)
                return LuaValueType.Bool;
            else if (source is ILuaTable)
                return LuaValueType.Table;
            else if (source is LuaThread)
                return LuaValueType.Thread;
            else if (source is LuaMethod)
                return LuaValueType.Function;
            else
                return LuaValueType.UserData;
        }
        /// <summary>
        /// Gets the .NET name of a given operation.
        /// </summary>
        /// <param name="type">The type of operation.</param>
        /// <returns>The name of the operation (e.g. op_Addition).</returns>
        protected virtual string GetBinName(BinaryOperationType type)
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
        protected internal virtual object GetSetIndex(ILuaEnvironment E, object target, object index,
            bool isStatic, bool isGet, bool isNonVirtual, object value = null)
        {
            return NetHelpers.GetSetIndex(E, target, index, isStatic, isGet, isNonVirtual, value);
        }

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
        public virtual object GetIndex(ILuaEnvironment E, object target, object index)
        {
            if (E == null)
                throw new ArgumentNullException("E");
            if (index == null)
                throw new ArgumentNullException("index");

            if (target is MultipleReturn)
                target = ((MultipleReturn)target)[0];

            if (target == null)
                throw new InvalidOperationException("Attempt to index a nil value.");
            if (target is double)
                throw new InvalidOperationException("Attempt to index a 'number' type.");
            else if (target is string)
                throw new InvalidOperationException("Attempt to index a 'string' type.");
            else if (target is bool)
                throw new InvalidOperationException("Attempt to index a 'boolean' type.");

            if (target is IIndexable)
            {
                return ((IIndexable)target).GetIndex(index);
            }
            else if (target is LuaUserData && ((LuaUserData)target).Backing is IIndexable)
            {
                return ((IIndexable)((LuaUserData)target).Backing).GetIndex(index);
            }
            else // if target is UserData
            {
                return GetSetIndex(E, target, index, false, true, false);
            }
        }
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
        public virtual void SetIndex(ILuaEnvironment E, object target, object index, object value)
        {
            if (E == null)
                throw new ArgumentNullException("E");
            if (index == null)
                throw new ArgumentNullException("index");

            if (target == null)
                throw new InvalidOperationException("Attempt to index a nil value.");
            if (target is double)
                throw new InvalidOperationException("Attempt to index a 'number' type.");
            else if (target is string)
                throw new InvalidOperationException("Attempt to index a 'string' type.");
            else if (target is bool)
                throw new InvalidOperationException("Attempt to index a 'boolean' type.");

            if (target is IIndexable)
            {
                ((IIndexable)target).SetIndex(index, value);
            }
            else if (target is LuaUserData && ((LuaUserData)target).Backing is IIndexable)
            {
                ((IIndexable)((LuaUserData)target).Backing).SetIndex(index, value);
            }
            else // if target is UserData
            {
                GetSetIndex(E, target, index, false, false, false, value);
            }
        }
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
        public virtual object[] FixArgs(object[] args, int count)
        {
            return Helpers.FixArgs(args, count);
        }
        /// <summary>
        /// Attempts to invoke a given object.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="value">The object to invoke.</param>
        /// <param name="args">The arguments passed to the method.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <param name="overload">The zero-based index of the overload to invoke,
        /// a negative number to ignore.</param>
        /// <param name="memberCall">Whether the function was invoked using member call (:).</param>
        /// <param name="self">The object being called on.</param>
        /// <returns>The return value of the method.</returns>
        /// <exception cref="System.InvalidOperationException">If attempting
        /// to invoke an invalid value.</exception>
        /// <exception cref="System.ArgumentNullException">If E  is null.</exception>
        public virtual MultipleReturn Invoke(ILuaEnvironment E, object self, object value, int overload, bool memberCall, object[] args, int[] byRef)
        {
            if (E == null)
                throw new ArgumentNullException("E");
            if (value is MultipleReturn)
                value = ((MultipleReturn)value)[0];
            if (value is LuaUserData)
                value = ((LuaUserData)value).Backing;

            if (value is IMethod)
            {
                return ((IMethod)value).Invoke(self, memberCall, overload, byRef, args);
            }

            throw new InvalidOperationException("Attempt to call a '" + GetValueType(value) + "' type.");
        }
        /// <summary>
        /// Determines whether a given object is true according
        /// to Lua.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>False if the object is null or false, otherwise true.</returns>
        public virtual bool IsTrue(object value)
        {
            if (value is MultipleReturn)
                value = ((MultipleReturn)value)[0];
            return !(value == null || value as bool? == false);
        }
        /// <summary>
        /// Tries to convert a given value to a number.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value as a double or null on error.</returns>
        public virtual double? ToNumber(object value)
        {
            if (value == null)
                return null;
            else if (value is double)
                return (double)value;
            else if (value is string)
            {
                return double.Parse(value as string, CultureInfo.CurrentCulture);
            }
            else
            {
                MethodInfo meth;
                int conv;
                if (!TypesCompatible(typeof(double), value.GetType(), out meth, out conv))
                {
                    if (meth != null)
                        value = meth.Invoke(null, new object[] { value });
                    else
                        return null;
                }
                return (double)value;
            }
        }
        /// <summary>
        /// Creates a new function from the given method and target objects.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the method, used for errors.</param>
        /// <param name="method">The method to call.</param>
        /// <param name="target">The target object.</param>
        /// <returns>The new function object.</returns>
        /// <exception cref="System.ArgumentNullException">If method, E, or target is null.</exception>
        public virtual object CreateFunction(ILuaEnvironment E, string name, MethodInfo method, object target)
        {
            if (method == null)
                throw new ArgumentNullException("method");
            if (target == null)
                throw new ArgumentNullException("target");

            return LuaDefinedMethod.Create(E, name, method, target);
        }
        /// <summary>
        /// Creates a new LuaTable object.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <returns>A new LuaTable object.</returns>
        /// <exception cref="System.ArgumentNullException">If E is null.</exception>
        public virtual object CreateTable(ILuaEnvironment E)
        {
            if (E == null)
                throw new ArgumentNullException("E"); // TODO: Remove Environment from CreateTable.

            return new LuaTableNet();
        }

        /// <summary>
        /// Converts an object to a given type using TypesCompatible.
        /// </summary>
        /// <param name="target">The object to convert.</param>
        /// <param name="type">The type to convert to.</param>
        /// <returns>An object that can be passed in MethodInfo.Invoke.</returns>
        /// <exception cref="System.InvalidCastException">If the type cannot
        /// be converted to the type.</exception>
        /// <exception cref="System.ArgumentNullException">If type is null.</exception>
        public virtual object ConvertType(object target, Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            int v;
            return ConvertType(target, type, out v);
        }
        /// <summary>
        /// This is called whenever a binary operation occurs to determine which
        /// function to call.
        /// </summary>
        /// <param name="lhs">The left-hand operand.</param>
        /// <param name="type">The type of operation.</param>
        /// <param name="rhs">The right-hand operand.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="System.InvalidOperationException">If the operator is
        /// inaccessible to Lua -or- if the objects are of an invalid type.</exception>
        public virtual object ResolveBinaryOperation(object lhs, BinaryOperationType type, object rhs)
        {
            // if operand is multiple return, use the first result.
            if (lhs is MultipleReturn)
                lhs = (lhs as MultipleReturn)[0];
            if (rhs is MultipleReturn)
                rhs = (rhs as MultipleReturn)[0];
            LuaUserData userDataLeft = lhs as LuaUserData;
            LuaUserData userDataRight = rhs as LuaUserData;

            // get the operatand's true value.
            if (userDataLeft != null)
                lhs = userDataLeft.Backing;
            if (userDataRight != null)
                rhs = userDataRight.Backing;

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
                        // check that if one operand is UserData that the operator is visible.
                        if (userDataLeft != null && !userDataLeft.IsMemberVisible(GetBinName(type)))
                            throw new InvalidOperationException(type + " operator is inaccessible to Lua code.");
                        if (userDataRight != null && !userDataRight.IsMemberVisible(GetBinName(type)))
                            throw new InvalidOperationException(type + " operator is inaccessible to Lua code.");

                        if (lhs is LuaClass || rhs is LuaClass)
                            throw new InvalidOperationException("Attempted to perform arithmetic on a 'class definition' object.");

                        // get the type of the operands.
                        LuaValueType t1 = GetValueType(lhs), t2 = GetValueType(rhs);

                        if (t1 == LuaValueType.Number && t2 == LuaValueType.Number)
                            return NativeArithmetic((double)lhs, (double)rhs, type);
                        else if (t1 == LuaValueType.String && t2 == LuaValueType.String)
                        {
                            if (type == BinaryOperationType.Gt)
                                return Comparer.Default.Compare(lhs, rhs) > 0;
                            else if (type == BinaryOperationType.Gte)
                                return Comparer.Default.Compare(lhs, rhs) >= 0;
                            else if (type == BinaryOperationType.Lt)
                                return Comparer.Default.Compare(lhs, rhs) < 0;
                            else if (type == BinaryOperationType.Lte)
                                return Comparer.Default.Compare(lhs, rhs) <= 0;
                            else
                                throw new InvalidOperationException("Attempted to perform arithmetic on a 'string' value.");
                        }
                        else if (t1 != LuaValueType.UserData && t2 != LuaValueType.UserData)
                        {
                            string err = t1 == LuaValueType.UserData ||
                                t1 == LuaValueType.Number ? t2.ToString() : t1.ToString();

                            throw new InvalidOperationException("Attempted to perform arithmetic on a '" +
                                err.ToLowerInvariant() + "' value.");
                        }
                        else
                        {
                            // try the first type
                            if (t1 == LuaValueType.UserData)
                            {
                                Type user = (userDataLeft == null ? null : userDataLeft.BehavesAs) ?? lhs.GetType();
                                object[] args = new[] { lhs, rhs };
                                var byref = new int[0];
                                MethodInfo method;
                                object target;
                                if (NetHelpers.GetCompatibleMethod(user.GetMethods()
                                        .Where(m => m.Name == GetBinName(type) &&
                                            (m.Attributes & MethodAttributes.Static) == MethodAttributes.Static &&
                                            m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0)
                                        .ToArray(), new object[] { null }, ref args, byref, out method, out target))
                                {
                                    return method.Invoke(null, args);
                                }
                            }

                            // try the second type
                            if (t2 == LuaValueType.UserData)
                            {
                                var user = (userDataRight == null ? null : userDataRight.BehavesAs) ?? rhs.GetType();
                                var args = new[] { lhs, rhs };
                                var byref = new int[0];
                                MethodInfo method;
                                object target;
                                if (NetHelpers.GetCompatibleMethod(user.GetMethods()
                                        .Where(m => m.Name == GetBinName(type) &&
                                            (m.Attributes & MethodAttributes.Static) == MethodAttributes.Static &&
                                            m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0)
                                        .ToArray(), new object[] { null }, ref args, byref, out method, out target))
                                {
                                    return method.Invoke(null, args);
                                }
                            }

                            throw new InvalidOperationException("Unable to find an operator that matches the given operands.");
                        }
                    }
                case BinaryOperationType.Concat:
                    return (lhs ?? "").ToString() + (rhs ?? "").ToString();
                case BinaryOperationType.Equals:
                case BinaryOperationType.NotEquals:
                    {
                        bool b = object.Equals(lhs, rhs);
                        return type == BinaryOperationType.Equals ? b : !b;
                    }
                case BinaryOperationType.And:
                    {
                        object ret = lhs;
                        if (ret != null && ret as bool? != false)
                        {
                            ret = rhs;
                        }

                        return (ret);
                    }
                case BinaryOperationType.Or:
                    {
                        object ret = lhs;
                        if (ret == null || ret as bool? == false)
                        {
                            ret = rhs;
                        }

                        return (ret);
                    }
                default:
                    throw new InvalidOperationException("Unable to resolve BinaryOperation." + type);
            }
        }
        /// <summary>
        /// This is called whenever a unary operation occurs to determine which
        /// function to call.
        /// </summary>
        /// <param name="type">The type of operation.</param>
        /// <param name="target">The target of the operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="System.InvalidOperationException">If the operator is
        /// inaccessible to Lua -or- if the objects are of an invalid type.</exception>
        public virtual object ResolveUnaryOperation(UnaryOperationType type, object target)
        {
            if (target is MultipleReturn)
                target = ((MultipleReturn)target)[0];

            LuaUserData userData = target as LuaUserData;
            if (userData != null)
                target = userData.Backing;

            switch (type)
            {
                case UnaryOperationType.Minus:
                    {
                        if (userData != null && !userData.IsMemberVisible("op_UnaryMinus"))
                            throw new InvalidOperationException("Minus operator not visible to Lua.");

                        LuaValueType t1 = GetValueType(target);

                        if (t1 == LuaValueType.Number)
                        {
                            return -(double)target;
                        }
                        else if (t1 == LuaValueType.UserData)
                        {
                            Type t = target.GetType();
                            LuaIgnoreAttribute attr = t.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Select(a => (LuaIgnoreAttribute)a).FirstOrDefault();
                            MethodInfo meth = t.GetMethod("op_UnaryMinus");
                            if (meth == null || meth.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0 ||
                                (attr != null && !attr.IsMemberVisible(t, "op_UnaryMinus")))
                                throw new InvalidOperationException("User data type '" + t + "' does not define a visible unary-minus operator.");

                            return meth.Invoke(null, new[] { target });
                        }
                        else
                            throw new InvalidOperationException("Attempted to perform arithmetic on a '" +
                                t1.ToString().ToLowerInvariant() + "' value.");
                    }
                case UnaryOperationType.Not:
                    return !IsTrue(target);
                case UnaryOperationType.Length:
                    {
                        LuaValueType t1 = GetValueType(target);

                        if (t1 == LuaValueType.String)
                        {
                            if (userData != null && !userData.IsMemberVisible("Length"))
                                throw new InvalidOperationException("Minus operator not visible to Lua.");

                            return (double)((string)target).Length;
                        }
                        else if (t1 == LuaValueType.Table)
                        {
                            if (userData != null && !userData.IsMemberVisible("Length"))
                                throw new InvalidOperationException("Minus operator not visible to Lua.");

                            double d = ((ILuaTable)target).GetLength();
                            return d == -1 ? 0 : d;
                        }
                        else if (t1 == LuaValueType.UserData)
                        {
                            Type t = target.GetType();
                            var attr = t.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Select(a => (LuaIgnoreAttribute)a).FirstOrDefault();
                            foreach (var item in t.GetMember("Length").Union(t.GetMember("Count")))
                            {
                                if (item.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0 ||
                                    (attr != null && !attr.IsMemberVisible(t, item.Name)) ||
                                    (userData != null && !userData.IsMemberVisible(item.Name)))
                                    continue;

                                if (item.MemberType == MemberTypes.Method)
                                {
                                    MethodInfo meth = item as MethodInfo;
                                    if (((meth.Attributes & MethodAttributes.Static) != MethodAttributes.Static && meth.GetParameters().Length == 0) ||
                                        ((meth.Attributes & MethodAttributes.Static) == MethodAttributes.Static && meth.GetParameters().Length == 1 &&
                                        meth.GetParameters()[0].ParameterType.IsAssignableFrom(t)))
                                    {
                                        if ((meth.Attributes & MethodAttributes.Static) == MethodAttributes.Static)
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

                            throw new InvalidOperationException("User data type '" + t + "' does not define a 'Length' or 'Count' member.");
                        }

                        throw new InvalidOperationException("Attempted to perform arithmetic on a '" +
                            t1.ToString().ToLowerInvariant() + "' value.");
                    }
            }
            return null;
        }
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
        public virtual void DefineClass(ILuaEnvironment E, string[] types, string name)
        {
            if (E == null)
                throw new ArgumentNullException("E");
            if (types == null)
                throw new ArgumentNullException("types");
            if (name == null)
                throw new ArgumentNullException("name");

            if (E.GlobalsTable.GetItemRaw(name) != null)
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
                    access = E.GlobalsTable.Where(k => k.Value is LuaType).Select(k => (k.Value as LuaType).Type).ToArray();
                    access = access.Union(
                        AppDomain.CurrentDomain.GetAssemblies()
                            .Where(a => Resources.Whitelist.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Contains(a.GetName().GetPublicKey().ToStringBase16()))
                            .SelectMany(a => a.GetTypes())
                        ).ToArray();
                }
                else
                    access = E.GlobalsTable.Where(k => k.Value is LuaType).Select(k => (k.Value as LuaType).Type).ToArray();

                // get the types that match the given name.
                Type[] typesa = access.Where(t => t.Name == item || t.FullName == item).ToArray();
                if (typesa == null || typesa.Length == 0)
                    throw new InvalidOperationException("Unable to locate the type '" + item + "'");
                if (typesa.Length > 1)
                    throw new InvalidOperationException("More than one type found for name '" + name + "'");
                Type type = typesa.FirstOrDefault();

                if ((type.Attributes & TypeAttributes.Public) != TypeAttributes.Public)
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
            E.GlobalsTable.SetItemRaw(name, c);
        }
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
        public virtual IEnumerable<MultipleReturn> GenericLoop(ILuaEnvironment E, object[] args)
        {
            if (args == null)
                throw new ArgumentNullException("args");
            if (E == null)
                throw new ArgumentNullException("E");

            args = Helpers.FixArgs(args, 3);
            if (args[0] is LuaUserData)
                args[0] = ((LuaUserData)args[0]).Backing;

            if (args[0] is IEnumerable<MultipleReturn>)
            {
                foreach (var item in (IEnumerable<MultipleReturn>)args[0])
                    yield return item;
            }
            else if (args[0] is IEnumerable)
            {
                foreach (var item in (IEnumerable)args[0])
                {
                    yield return new MultipleReturn(item);
                }
            }
            else if (args[0] is IMethod)
            {
                IMethod target = (IMethod)args[0];
                object s = args[1];
                object var = args[2];

                while (true)
                {
                    var temp = target.Invoke(null, false, null, new[] { s, var });
                    if (temp == null || temp[0] == null)
                        yield break;
                    var = temp[0];

                    yield return temp;
                }
            }
            else
                throw new InvalidOperationException("Cannot enumerate over an object of type '" + GetValueType(args[0]) + "'.");
        }
    }
}