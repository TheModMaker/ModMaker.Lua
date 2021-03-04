// Copyright 2016 Jacob Trimble
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ModMaker.Lua.Runtime.LuaValues
{
    /// <summary>
    /// Defines a method that contains several user-defined methods and choses
    /// the method to invoke based on runtime types of the arguments and does
    /// any needed conversion so it will work.
    /// </summary>
    public class LuaOverloadFunction : LuaFunction
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
            /// Adds a new 'free' overload.  This variable can move around and
            /// may have a dynamic index if new fixed variables are added.
            /// </summary>
            /// <param name="item">The item to add, can be null for nullable types.</param>
            public void Add(T item)
            {
                // Try to find a null slot.
                for (int i = 0; i < backing.Count; i++)
                {
                    if (backing[i] == null)
                    {
                        backing[i] = new Tuple<bool, T>(false, item);
                        return;
                    }
                }

                // Otherwise append to the end.
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
                if (backing.Count > index && backing[index] != null && backing[index].Item1)
                    throw new InvalidOperationException(string.Format(Resources.ExistingOverload, index));

                Tuple<bool, T> temp = new Tuple<bool, T>(true, item);
                for (int i = index; i < backing.Count; i++)
                {
                    if (backing[i] == null || !backing[i].Item1)
                    {
                        var temp2 = backing[i];
                        backing[i] = temp;
                        temp = temp2;

                        // If this is null, then we found a spot for the temp.
                        if (temp == null)
                            return;
                    }
                }

                // If temp is fixed, then index was larger than the list so add nulls until the index.
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
            /// <param name="indicies">A function that gets the indices for each item.</param>
            /// <exception cref="System.ArgumentNullException">If items or indices is null.</exception>
            public void AddRange(IEnumerable<T> items, Func<T, int?> indicies)
            {
                foreach (var item in items)
                {
                    int? ind = indicies(item);
                    if (ind != null)
                        Add(item, ind.Value);
                    else
                        Add(item);
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

        OverloadList<Tuple<MethodInfo, object>> methods;
        // TODO: Add a constructor that accepts Delegate[].

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
        public LuaOverloadFunction(string name, IEnumerable<MethodInfo> methods, IEnumerable<object> targets)
            : base(name)
        {
            this.methods = new OverloadList<Tuple<MethodInfo, object>>();
            this.methods.AddRange(methods.Zip(targets, (a, b) => Tuple.Create(a, b)), m =>
            {
                var temp = m.Item1.GetCustomAttribute<OverloadAttribute>(false);
                if (temp != null)
                    return temp.Index;
                else
                    return null;
            });
        }

        /// <summary>
        /// Adds an overload to the current method object.  This is used by the
        /// environment to register multiple delegates.  The default behavior
        /// is to throw a NotSupportedException.
        /// </summary>
        /// <param name="d">The delegate to register.</param>
        /// <exception cref="System.ArgumentNullException">If d is null.</exception>
        /// <exception cref="System.ArgumentException">If the delegate is not
        /// compatible with the current object.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support adding overloads.</exception>
        public override void AddOverload(Delegate d)
        {
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

        /// <summary>
        /// Performs that actual invocation of the method.
        /// </summary>
        /// <param name="self">The object that this was called on.</param>
        /// <param name="memberCall">Whether the call used member call syntax (:).</param>
        /// <param name="args">The current arguments, not null but maybe empty.</param>
        /// <param name="overload">The overload to chose or negative to do
        /// overload resolution.</param>
        /// <param name="byRef">An array of the indices that are passed by-reference.</param>
        /// <returns>The values to return to Lua.</returns>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        /// <exception cref="System.IndexOutOfRangeException">If overload is
        /// larger than the number of overloads.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support overloads.</exception>
        protected override ILuaMultiValue InvokeInternal(ILuaValue self, bool methodCall, int overload, ILuaMultiValue args)
        {
            MethodInfo method;
            object target;
            if (overload < 0)
            {
                if (!Helpers.GetCompatibleMethod(methods, args, out method, out target))
                    throw new ArgumentException("No overload of method '" + Name + "' could be found with specified parameters.");
            }
            else
            {
                Tuple<MethodInfo, object> temp;
                if (!methods.TryGetIndex(overload, out temp))
                    throw new InvalidOperationException("There is not an overload for '" + Name + "' with the index of '" + overload + "'.");

                if (!Helpers.GetCompatibleMethod(new[] { temp }, args, out method, out target))
                    throw new ArgumentException("No overload of method '" + Name + "' could be found with specified parameters.");
            }

            // Invoke the selected method
            object retObj;
            object[] r_args = Helpers.ConvertForArgs(args, method);
            retObj = Helpers.DynamicInvoke(method, target, r_args);

            // Restore by-reference variables.
            var min = Math.Min(method.GetParameters().Length, args.Count);
            for (int i = 0; i < min; i++)
            {
                args[i] = LuaValueBase.CreateValue(r_args[i]);
            }

            if (retObj is ILuaMultiValue)
                return (ILuaMultiValue)retObj;

            // Convert the return type and return
            Type returnType = method.ReturnType;
            if (method.GetCustomAttributes(typeof(MultipleReturnAttribute), true).Length > 0)
            {
                if (typeof(IEnumerable).IsAssignableFrom(returnType))
                {
                    // TODO: Support restricted variables.
                    IEnumerable tempE = (IEnumerable)retObj;
                    return LuaMultiValue.CreateMultiValueFromObj(tempE.Cast<object>().ToArray());
                }
                else
                    throw new InvalidOperationException(
                        "Methods marked with MultipleReturnAttribute must return a type compatible with IEnumerable.");
            }
            else
                return LuaMultiValue.CreateMultiValueFromObj(retObj);
        }
    }
}
