using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines a method that contains several user-defined methods and choses
    /// the method to invoke based on runtime types of the arguments and does
    /// any needed conversion so it will work.
    /// </summary>
    public class LuaOverloadMethod : LuaMethod
    {
        /// <summary>
        /// A special list for specifying overloads with OverloadAttribute.
        /// There are two kinds of objects, fixed and free.  Fixed indicies
        /// cannot move and there can be only one of them.  These are the
        /// overloads marked with OverloadAttribute.  The free ones move
        /// to make room for adding fixed variables.
        /// </summary>
        /// <typeparam name="T">The generic type of the list.</typeparam>
        sealed class OverloadList<T> : IEnumerable<T>
        {
            /// <summary>
            /// The backing list of objects.  If the bool is true, it is fixed
            /// and cannot move; otherwise it is free and can move.
            /// </summary>
            List<Tuple<bool, T>> backing = new List<Tuple<bool, T>>();

            /// <summary>
            /// Creates a new empty OverloadList&lt;T&gt;.
            /// </summary>
            public OverloadList() { }
            /// <summary>
            /// Creates a new OverloadList&lt;T&gt; with the given items.
            /// </summary>
            /// <param name="items">The intial items.</param>
            public OverloadList(IEnumerable<T> items)
            {
                AddRange(items);
            }

            /// <summary>
            /// Adds a new 'free' overload.  This variable can move around and
            /// may have a dynamic index if new fixed variables are added.
            /// </summary>
            /// <param name="item">The item to add, can be null for nullable types.</param>
            public void Add(T item)
            {
                // try to find a null slot
                for (int i = 0; i < backing.Count; i++)
                {
                    if (backing[i] == null)
                    {
                        backing[i] = new Tuple<bool, T>(false, item);
                        return;
                    }
                }

                // otherwise append to the end
                backing.Add(new Tuple<bool, T>(false, item));
            }
            /// <summary>
            /// Adds a new 'fixed' overload.  This variable cannot move and will
            /// have the given fixed index.  An exception is thrown if the index
            /// is already occupied.
            /// </summary>
            /// <param name="item">The item to add, can be null for nullable types.</param>
            /// <param name="index">The index to add to.</param>
            /// <exception cref="System.InvalidOperationException">If the index
            /// is occupied by another fixed object.</exception>
            public void Add(T item, int index)
            {
                if (index < 0)
                    throw new InvalidOperationException("An overload index cannot be negative.");
                if (backing.Count > index && backing[index] != null && backing[index].Item1)
                    throw new InvalidOperationException(
                        string.Format("There is already an overload specified with the index '{0}'.", index));

                Tuple<bool, T> temp = new Tuple<bool, T>(true, item);
                for (int i = index; i < backing.Count; i++)
                {
                    if (backing[i] == null || !backing[i].Item1)
                    {
                        var temp2 = backing[i];
                        backing[i] = temp;
                        temp = temp2;

                        // if this is null, then we found a spot for the temp.
                        if (temp == null)
                            return;
                    }
                }

                // if temp is fixed, then index was larger than the list so add nulls until the index
                if (temp.Item1)
                {
                    while (backing.Count < index)
                    {
                        backing.Add(null);
                    }
                }
                backing.Add(temp);
            }

            /// <summary>
            /// Adds all the given items to the collection.
            /// </summary>
            /// <param name="items">The collection of items to add.</param>
            /// <exception cref="System.ArgumentNullException">If items is null.</exception>
            public void AddRange(IEnumerable<T> items)
            {
                if (items == null)
                    throw new ArgumentNullException("items");

                int i = 0;
                foreach (var item in items)
                {
                    while (i < backing.Count && backing[i] != null)
                    {
                        i++;
                    }

                    if (i >= backing.Count)
                        backing.Add(new Tuple<bool, T>(false, item));
                    else
                        backing[i] = new Tuple<bool, T>(false, item);
                    i++;
                }
            }
            /// <summary>
            /// Adds all the given items to the collection.
            /// </summary>
            /// <param name="items">The collection of items to add.</param>
            /// <param name="indicies">A function that gets the indicies for each item.</param>
            /// <exception cref="System.ArgumentNullException">If items or indicies is null.</exception>
            public void AddRange(IEnumerable<T> items, Func<T, int> indicies)
            {
                if (items == null)
                    throw new ArgumentNullException("items");
                if (indicies == null)
                    throw new ArgumentNullException("indicies");

                foreach (var item in items)
                {
                    Add(item, indicies(item));
                }
            }

            /// <summary>
            /// Attempts to get the item at the given index and returns whether
            /// it was successful.
            /// </summary>
            /// <param name="index">The zero-based index of the item.</param>
            /// <param name="item">Where the item is placed if found.</param>
            /// <returns>True if successful, otherwise false.</returns>
            public bool TryGetIndex(int index, out T item)
            {
                if (index < 0 || index >= backing.Count || backing[index] == null)
                {
                    item = default(T);
                    return false;
                }
                else
                {
                    item = backing[index].Item2;
                    return true;
                }
            }

            /// <summary>
            /// Returns an enumerator that iterates through the collection.
            /// </summary>
            /// <returns>A System.Collections.Generic.IEnumerator&lt;T&gt; that can 
            /// be used to iterate through the collection.</returns>
            public IEnumerator<T> GetEnumerator()
            {
                foreach (var item in backing)
                {
                    if (item != null)
                        yield return item.Item2;
                }
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        static Type createdType = null;
        OverloadList<Tuple<MethodInfo, object>> methods;

        /// <summary>
        /// Creates a new LuaOverloadMethod with the given choices.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the method, used for errors.</param>
        /// <param name="methods">The method choices, cannot be null.</param>
        /// <param name="targets">The targets for the methods, cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">If methods or targets is null.</exception>
        /// <exception cref="System.ArgumentException">If the length of methods
        /// is not equal to that of targets.</exception>
        protected LuaOverloadMethod(ILuaEnvironment E, string name, List<MethodInfo> methods, List<object> targets)
            : base(E, name)
        {
            if (methods == null)
                throw new ArgumentNullException("methods");
            if (targets == null)
                throw new ArgumentNullException("targets");
            if (methods.Count != targets.Count)
                throw new ArgumentException("The length of methods must equal that of targets.");

            this.methods = new OverloadList<Tuple<MethodInfo, object>>();
            for (int i = 0; i < methods.Count; i++)
            {
                if (methods[i] == null)
                    throw new ArgumentException("Cannot contain a null method.");

                var temp = methods[i].GetCustomAttributes(typeof(OverloadAttribute), false);
                if (temp != null && temp.Length > 0)
                {
                    OverloadAttribute attr = temp[0] as OverloadAttribute;
                    this.methods.Add(new Tuple<MethodInfo, object>(methods[i], targets[i]), attr.Index);
                }
                else
                {
                    this.methods.Add(new Tuple<MethodInfo, object>(methods[i], targets[i]));
                }
            }
        }

        /// <summary>
        /// Creates a new LuaOverloadMethod object with the given name and
        /// choices.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the method, used for errors.</param>
        /// <param name="delegates">The possible method choices.</param>
        /// <exception cref="System.ArgumentException">If the delegates array
        /// contains a null value.</exception>
        /// <exception cref="System.ArgumentNullException">If E, methods, or 
        /// targets is null.</exception>
        public static LuaOverloadMethod Create(ILuaEnvironment E, string name, params Delegate[] delegates)
        {
            if (delegates == null)
                delegates = new Delegate[0];

            return Create(E, name, delegates.Select(d => d.Method), delegates.Select(d => d.Target));
        }
        /// <summary>
        /// Creates a new LuaOverloadMethod object with the given name and
        /// choices.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="name">The name of the method, used for errors.</param>
        /// <param name="methods">The possible method choices.</param>
        /// <param name="targets">The targets for the given methods.  The length
        /// must equal that of methods or one.</param>
        /// <exception cref="System.ArgumentException">If methods contains a
        /// null value -or- if the length of targets is not valid.</exception>
        /// <exception cref="System.ArgumentNullException">If E, methods, or 
        /// targets is null.</exception>
        public static LuaOverloadMethod Create(ILuaEnvironment E, string name, IEnumerable<MethodInfo> methods, IEnumerable<object> targets)
        {
            if (methods == null)
                throw new ArgumentNullException("methods");
            if (targets == null)
                throw new ArgumentNullException("targets");
            if (E == null)
                throw new ArgumentNullException("E");

            var tempMethods = methods.ToList();
            var tempTargets = targets.ToList();

            if (tempTargets.Count == 1)
                tempTargets = Enumerable.Range(1, tempMethods.Count).Select(i => tempTargets[0]).ToList();
            if (tempMethods.Contains(null))
                throw new ArgumentException("Methods cannot contains a null value.");
            if (tempMethods.Count != tempTargets.Count)
                throw new ArgumentException("Length of targets must be equal to that of methods or 1.");

            if (createdType == null)
                CreateType();

            if (Lua.UseDynamicTypes)
                return (LuaOverloadMethod)Activator.CreateInstance(createdType, E, name, tempMethods, tempTargets);
            else
                return new LuaOverloadMethod(E, name, tempMethods, tempTargets);
        }
        /// <summary>
        /// Creates the dynamic type.
        /// </summary>
        static void CreateType()
        {
            if (createdType != null)
                return;

            var tb = NetHelpers.GetModuleBuilder().DefineType("LuaOverloadMethodImpl",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoClass,
                typeof(LuaOverloadMethod), new Type[0]);

            //// .ctor(ILuaEnvironment E, string name, List<MethodInfo>/*!*/ methods, List<object/*!*/ targets);
            var ctor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                new[] { typeof(ILuaEnvironment), typeof(string), typeof(List<MethodInfo>), typeof(List<object>) });
            var gen = ctor.GetILGenerator();

            // base(E, name, methods, targets);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Ldarg, 4);
            gen.Emit(OpCodes.Call, typeof(LuaOverloadMethod).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(ILuaEnvironment), typeof(string), typeof(List<MethodInfo>), typeof(List<object>) }, null));
            gen.Emit(OpCodes.Ret);

            LuaMethod.AddInvokableImpl(tb);
            createdType = tb.CreateType();
        }

        /// <summary>
        /// Performs that actual invokation of the method.
        /// </summary>
        /// <param name="self">The object that this was called on.</param>
        /// <param name="memberCall">Whether the call used member call syntax (:).</param>
        /// <param name="args">The current arguments, not null but maybe empty.</param>
        /// <param name="overload">The overload to chose or negative to do 
        /// overload resoltion.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <returns>The values to return to Lua.</returns>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        /// <exception cref="System.IndexOutOfRangeException">If overload is
        /// larger than the number of overloads.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support overloads.</exception>
        protected override MultipleReturn InvokeInternal(object self, bool memberCall, int overload, int[] byRef, object[] args)
        {
            // convert the arguments to a form that can be passed to the method
            object[] r_args = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                object temp = args[i];
                LuaUserData userData = temp as LuaUserData;
                if (userData != null)
                {
                    if (!userData.CanPass)
                        throw new ArgumentException("One of the arguments to function '" + Name + "' cannot be passed as an argument.");
                }

                if (temp is LuaType)
                    temp = ((LuaType)temp).Type;
                r_args[i] = temp;
            }

            // try to find an overload
            MethodInfo method;
            object target;
            if (overload < 0)
            {
                if (!NetHelpers.GetCompatibleMethod(methods.Select(t => t.Item1).ToArray(), methods.Select(t => t.Item2).ToArray(),
                    ref r_args, byRef, out method, out target))
                {
                    throw new ArgumentException("No overload of method '" + Name + "' could be found with specified parameters.");
                }
            }
            else
            {
                Tuple<MethodInfo, object> temp;
                if (!methods.TryGetIndex(overload, out temp))
                    throw new InvalidOperationException("There is not an overload for '" + Name + "' with the index of '" + overload + "'.");

                if (!NetHelpers.GetCompatibleMethod(new[] { temp.Item1 }, new[] { temp.Item2 },
                    ref r_args, byRef, out method, out target))
                {
                    throw new ArgumentException("The given overload for '" + Name + "' is not compatible with the specified parameters.");
                }
            }

            // invoke the selected method
            object retObj;
            retObj = method.Invoke(target, r_args);

            // restore by-reference variables
            int max = Math.Min(args.Length, r_args.Length);
            var param = method.GetParameters();
            for (int i = 0; i < max; i++)
            {
                if (byRef.Contains(i))
                {
                    // allow a parameter to be marked with LuaIgnore.
                    var attr = param[i].GetCustomAttributes(typeof(LuaIgnoreAttribute), true);
                    if (attr.Length > 0)
                    {
                        args[i] = LuaUserData.CreateFrom(r_args[i], attr[0] as LuaIgnoreAttribute);
                    }
                    else
                        args[i] = r_args[i];
                }
            }

            if (retObj is MultipleReturn)
                return (MultipleReturn)retObj;

            // convert the return type and return
            Type returnType = method.ReturnType;
            if (method.GetCustomAttributes(typeof(MultipleReturnAttribute), true).Length > 0)
            {
                if (typeof(IEnumerable).IsAssignableFrom(returnType))
                {
                    IEnumerable tempE = (IEnumerable)retObj;
                    var temp = returnType.GetInterfaces().Where(t => t.IsGenericType &&
                        t.GetGenericTypeDefinition() == typeof(IEnumerable<>)).ToArray();

                    // if validating the return type, use either 'object' or if there is only one IEnumerable<>, use that type
                    if (Environment.Settings.EnsureReturnType)
                    {
                        Type underlying = (temp.Length == 1 ? NetHelpers.GetLuaSafeType(temp[0].GetGenericArguments()[0]) : typeof(object));
                        return new MultipleReturn(
                            tempE
                                .Cast<object>()
                                .Select(o => typeof(LuaUserData).IsAssignableFrom(underlying) ? o : new LuaUserData(o, underlying)));
                    }
                    else
                        return new MultipleReturn(tempE);
                }
                else
                    throw new InvalidOperationException(
                        "Methods marked with MultipleReturnAttribute must return a type compatible with IEnumerable.");
            }
            else
            {
                if (typeof(LuaUserData).IsAssignableFrom(returnType))
                {
                    return new MultipleReturn(retObj);
                }

                if (returnType != typeof(void))
                {
                    // check if the return value is marked with LuaIgnoreAttribute
                    var attr = method.ReturnTypeCustomAttributes.GetCustomAttributes(typeof(LuaIgnoreAttribute), true);
                    if (attr.Length > 0)
                    {
                        return new MultipleReturn(LuaUserData.CreateFrom(retObj, attr[0] as LuaIgnoreAttribute));
                    }
                    else
                    {
                        return new MultipleReturn(!Environment.Settings.EnsureReturnType ? retObj : new LuaUserData(retObj, returnType));
                    }
                }
                else
                {
                    return new MultipleReturn();
                }
            }
        }
        /// <summary>
        /// Adds an overload to the current method object.  This is used by the
        /// environment to register multiple delegates.  The default behaviour
        /// is to throw a NotSupportedException.
        /// </summary>
        /// <param name="d">The delegate to register.</param>
        /// <exception cref="System.ArgumentNullException">If d is null.</exception>
        /// <exception cref="System.ArgumentException">If the delegate is not
        /// compatible with the current object.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support adding overloads.</exception>
        protected internal override void AddOverload(Delegate d)
        {
            if (d == null)
                throw new ArgumentNullException("d");

            var temp = d.Method.GetCustomAttributes(typeof(OverloadAttribute), false);
            if (temp != null && temp.Length > 0)
            {
                OverloadAttribute attr = temp[0] as OverloadAttribute;
                this.methods.Add(new Tuple<MethodInfo, object>(d.Method, d.Target), attr.Index);
            }
            else
            {
                this.methods.Add(new Tuple<MethodInfo, object>(d.Method, d.Target));
            }
        }
    }
}