using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// An interface for a table in Lua.  This acts like a dictionary of
    /// objects to objects.  This is both indexable and callable.  This supports
    /// a metatable which can have methods that will change the gehaviour of the
    /// table.
    /// </summary>
    public interface ILuaTable : IEnumerable<KeyValuePair<object, object>>, IIndexable, IMethod
    {
        /// <summary>
        /// Gets or sets the metatable for the table.
        /// </summary>
        ILuaTable MetaTable { get; set; }
        /// <summary>
        /// The current number of keys in the dictionary, not the same
        /// as GetLength().
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Gets or sets the value of the specified key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>The value of the given key.</returns>
        object this[object key] { get; set; }

        /// <summary>
        /// Gets the item at the specified key without invoking any
        /// metamethods.
        /// </summary>
        /// <param name="key">The key to get.</param>
        /// <returns>The value at the specified key.</returns>
        object GetItemRaw(object key);
        /// <summary>
        /// Sets the item at the specified key without invoking any
        /// metamethods.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="value">The value to set the key to.</param>
        void SetItemRaw(object key, object value);

        /// <summary>
        /// Gets the length of the table acording to Lua.  This will use any
        /// metamethods that exists to get the length.
        /// </summary>
        /// <returns>The length of the table.</returns>
        double GetLength();
    }

    /// <summary>
    /// The default implementation of a LuaTable.  This is a wraper arround a 
    /// dictionary. Keys and Values are objects.
    /// </summary>
    public class LuaTable : ILuaTable
    {
        Dictionary<object, object> _values;
        
        /// <summary>
        /// Creates a new LuaTable object.
        /// </summary>
        public LuaTable()
        {
            _values = new Dictionary<object, object>();
        }

        /// <summary>
        /// Gets or sets the metatable for the table.
        /// </summary>
        public virtual ILuaTable MetaTable { get; set; }
        /// <summary>
        /// The current number of keys in the dictionary, not the same
        /// as GetLength().
        /// </summary>
        public virtual int Length { get { return _values.Count; } }

        /// <summary>
        /// Gets or sets the value of the specified key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>The value of the given key.</returns>
        public virtual object this[object key]
        {
            get { return _get(key); }
            set { _set(key, (object)value); }
        }

        /// <summary>
        /// Helper method to get the item with the given key.  Uses the metatable
        /// if it is included.
        /// </summary>
        /// <param name="key">The key to index the table.</param>
        /// <returns>The value at the given index.</returns>
        protected object _get(object key)
        {
            key = Helpers.ToDouble(key);
            object ret;
            if (!_values.TryGetValue(key, out ret))
            {
                if (MetaTable != null && !_values.ContainsKey(key))
                {
                    object method = MetaTable.GetItemRaw("__index");
                    if (method is IMethod)
                    {
                        try
                        {
                            var temp = ((IMethod)method).Invoke(null, new[] { this, key });
                            return temp[0];
                        }
                        catch (Exception) { }
                    }
                    if (method is IIndexable)
                    {
                        try
                        {
                            return ((IIndexable)method).GetIndex(key);
                        }
                        catch (Exception) { }
                    }

                    return null;
                }
            }

            return ret;
        }
        /// <summary>
        /// Helper method to set the item with the given key.  Uses the metatable
        /// if it is included.
        /// </summary>
        /// <param name="key">The key to index the table.</param>
        /// <param name="value">The value to set to.</param>
        protected void _set(object key, object value)
        {
            key = Helpers.ToDouble(key);
            value = Helpers.ToDouble(value);

            if (!_values.ContainsKey(key) && MetaTable != null)
            {
                IMethod m = MetaTable.GetItemRaw("__newindex") as IMethod;
                if (m != null)
                {
                    m.Invoke(null, new[] { this, key, value });
                    return;
                }
            }

            if (value == null)
            {
                if (_values.ContainsKey(key))
                    _values.Remove(key);
            }
            else
                _values[key] = value;
        }

        /// <summary>
        /// Gets a IEnumerable&lt;KeyValuePair&lt;object, object&gt;&gt; to enumerate over
        /// each of the keys of the Table.
        /// </summary>
        /// <returns>An enumerator to enumerate of the keys.</returns>
        public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
        {
            return _values.GetEnumerator();
        }
        /// <summary>
        ///  Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An System.Collections.IEnumerator object that can be used 
        /// to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        /// <summary>
        /// Gets the item at the specified key without invoking any
        /// metamethods.
        /// </summary>
        /// <param name="key">The key to get.</param>
        /// <returns>The value at the specified key.</returns>
        public virtual object GetItemRaw(object key)
        {
            object o = Helpers.ToDouble(key);
            return _values.ContainsKey(o) ? _values[o] : null;
        }
        /// <summary>
        /// Sets the item at the specified key without invoking any
        /// metamethods.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="value">The value to set the key to.</param>
        public virtual void SetItemRaw(object key, object value)
        {
            key = Helpers.ToDouble(key);
            value = Helpers.ToDouble(value);
            if (value == null)
            {
                if (_values.ContainsKey(key))
                    _values.Remove(key);
            }
            else
                _values[key] = Helpers.ToDouble(value);
        }

        /// <summary>
        /// Gets the length of the table acording to Lua.  This will use any
        /// metamethods that exists to get the length.
        /// </summary>
        /// <returns>The length of the table.</returns>
        public virtual double GetLength()
        {
            if (MetaTable != null)
            {
                IMethod meth = MetaTable.GetItemRaw("__len") as IMethod;
                if (meth != null)
                {
                    var ret = meth.Invoke(null, new[] { this });
                    object o = ret[0];
                    if (o is double)
                        return (double)o;
                }
            }

            double i = 0;

            foreach (var item in _values)
            {
                if (item.Key is double)
                {
                    double test = (double)item.Key;
                    if (test > i)
                        i = test;
                }
            }
            return Math.Floor(i);
        }

        /// <summary>
        /// Determines whether the specified System.Object is equal to the current System.Object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            LuaTable t = obj as LuaTable;
            return t != null && t.GetType() == this.GetType() && t.ToString() == this.ToString();
        }
        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>A hash code for the current System.Object.</returns>
        public override int GetHashCode()
        {
            // BUG: This may cause problems when equality is determined using this.
            //      This will return the same value the lifetime of this object, so
            //      it will always be unequal to other objects, even if this.Equals(other)
            //      would return true.  However, it is worse to have a changing hash-code
            //      because this object could be lost in a hash-table.
            return base.GetHashCode();
        }
        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            // ex: "{ Foo = 12, x = String }";
            StringBuilder str = new StringBuilder();
            bool first = true;
            str.Append("{ ");
            foreach (var item in _values)
            {
                if (!first)
                    str.Append(", ");
                first = false;

                str.Append(item.Key);
                str.Append(" = ");
                str.Append(item.Value);
            }

            str.Append(" }");
            return str.ToString();
        }

        #region ILuaIndexer implementation

        /// <summary>
        /// Sets the value of the given index to the given value.
        /// </summary>
        /// <param name="index">The index to use, cannot be null.</param>
        /// <param name="value">The value to set to, can be null.</param>
        /// <exception cref="System.ArgumentNullException">If index is null.</exception>
        /// <exception cref="System.InvalidOperationException">If the current
        /// type does not support setting an index -or- if index is not a valid
        /// value or type -or- if value is not a valid value or type.</exception>
        /// <exception cref="System.MemberAccessException">If Lua does not have
        /// access to the given index.</exception>
        void IIndexable.SetIndex(object index, object value)
        {
            _set(index, value);
        }
        /// <summary>
        /// Gets the value of the given index.
        /// </summary>
        /// <param name="index">The index to use, cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">If index is null.</exception>
        /// <exception cref="System.InvalidOperationException">If the current
        /// type does not support getting an index -or- if index is not a valid
        /// value or type.</exception>
        object IIndexable.GetIndex(object index)
        {
            return _get(index);
        }

        #endregion

        #region IMethod implementation

        ILuaEnvironment IMethod.Environment
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Invokes the current object with the given arguments.
        /// </summary>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <param name="args">The current arguments, can be null or empty.</param>
        /// <returns>The arguments to return to Lua.</returns>
        /// <exception cref="System.ArgumentNullException">If E is null.</exception>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        MultipleReturn IMethod.Invoke(int[] byRef, object[] args)
        {
            return ((IMethod)this).Invoke(-1, byRef, args);
        }
        /// <summary>
        /// Invokes the current object with the given arguments.
        /// </summary>
        /// <param name="args">The current arguments, can be null or empty.</param>
        /// <param name="overload">The zero-based index of the overload to invoke;
        /// if negative, use normal overload resolution.</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <returns>The arguments to return to Lua.</returns>
        /// <exception cref="System.ArgumentNullException">If E is null.</exception>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        /// <exception cref="System.IndexOutOfRangeException">If overload is
        /// larger than the number of overloads.</exception>
        /// <exception cref="System.NotSupportedException">If this object does
        /// not support overloads.</exception>
        /// <remarks>
        /// If this object does not support overloads, you still need to write
        /// this method to work with negative indicies, however you should throw
        /// an exception if zero or positive.  This method is always the one
        /// invoked by the default runtime.
        /// 
        /// It is sugested that the other method simply call this one with -1
        /// as the overload index.
        /// </remarks>
        MultipleReturn IMethod.Invoke(int overload, int[] byRef, object[] args)
        {
            if (overload >= 0)
                throw new NotSupportedException(string.Format(Resources.CannotUseOverload, "LuaTable"));
            if (args == null)
                args = new object[0];

            ILuaTable m = MetaTable;
            if (m != null)
            {
                object v = m.GetItemRaw("__call");
                if (v is IMethod)
                    return (v as IMethod).Invoke(byRef, args);
            }

            throw new InvalidOperationException(string.Format(Resources.CannotCall, "table"));
        }

        #endregion
    }
}