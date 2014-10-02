using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines a return for a function that has more than one return value.
    /// </summary>
    /// <remarks>
    /// If you want to return a single objec that also implements IEnumerable,
    /// (e.g. string) you should cast it to object so it uses the params object[]
    /// constructor.
    /// 
    /// If your function return this, it will simply pass this to Lua, so make
    /// sure that you do not intend for creating LuaUserData objects because it
    /// will ignore any LuaIgnreAttributes attached to the return value, even 
    /// if the return type is not MultipleReturn.
    /// </remarks>
    public sealed class MultipleReturn : IEnumerable<object>
    {
        /// <summary>
        /// Creates a new MultipleReturn object from the given enumerable object.
        /// </summary>
        /// <param name="args">The arguments to pass.</param>
        public MultipleReturn(IEnumerable args)
        {
            if (args != null)
                this.Values = Helpers.FixArgs(args.Cast<object>().ToArray(), -1);
            else
                this.Values = new object[0];
        }
        /// <summary>
        /// Creates a new MultipleReturn object from the given objects.
        /// </summary>
        /// <param name="args"></param>
        public MultipleReturn(params object[] args)
            : this((IEnumerable)args) { }

        /// <summary>
        /// Gets the array of values that is stored in this object.
        /// </summary>
        public object[] Values { get; private set; }
        /// <summary>
        /// Gets the number of values in this object.
        /// </summary>
        public int Count { get { return Values.Length; } }

        /// <summary>
        /// Gets the object at the specified index.  Returns null if the index
        /// is not in a valid range.
        /// </summary>
        /// <param name="index">The zero-based index of the argument.</param>
        /// <returns>The object at the given index or null.</returns>
        public object this[int index] { get { return index < 0 || index >= Values.Length ? null : Values[index]; } }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An System.Collections.IEnumerator object that can be used 
        /// to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }
        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>A System.Collections.Generic.IEnumerator&lt;T&gt; that can
        /// be used to iterate through the collection.</returns>
        public IEnumerator<object> GetEnumerator()
        {
            return new List<object>(Values).GetEnumerator();
        }
        /// <summary>
        /// Returns a new object with the same values as this object except
        /// where any extra values are removed and any missing values are
        /// set to null.
        /// </summary>
        /// <param name="number">The number of values to have.</param>
        /// <returns>A new MultipleReturn object with the values in this object.</returns>
        public MultipleReturn AdjustResults(int number)
        {
            if (number < 0)
                throw new ArgumentException("The number of elements cannot be negative.");

            object[] temp = new object[number];
            int max = Math.Min(number, Values.Length);
            for (int i = 0; i < max; i++)
                temp[i] = Values[i];

            return new MultipleReturn(temp);
        }
    }
}