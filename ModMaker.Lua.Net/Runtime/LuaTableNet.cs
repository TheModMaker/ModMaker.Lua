using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// A table for Lua code that also is a dynamic object.  This acts the same
    /// as the default LuaTable, but also is a dynamic object.
    /// </summary>
    public sealed class LuaTableNet : DynamicObject, ILuaTable
    {
        LuaTable _table;

        /// <summary>
        /// Creates a new LuaTable object.
        /// </summary>
        public LuaTableNet()
        {
            _table = new LuaTable();
        }

        /// <summary>
        /// Gets or sets the metatable for the table.
        /// </summary>
        public ILuaTable MetaTable { get { return _table.MetaTable; } set { _table.MetaTable = value; } }
        /// <summary>
        /// The current number of keys in the dictionary, not the same
        /// as GetLength().
        /// </summary>
        public int Length { get { return _table.Length; } }

        /// <summary>
        /// Gets or sets the value of the specified key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>The value of the given key.</returns>
        public dynamic this[object key]
        {
            get { return NumberProxy.Create(_table[key]); }
            set { _table[key] = (object)value; }
        }

        /// <summary>
        /// Gets a IEnumerable&lt;KeyValuePair&lt;object, object&gt;&gt; to enumerate over
        /// each of the keys of the Table.
        /// </summary>
        /// <returns>An enumerator to enumerate of the keys.</returns>
        public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
        {
            return _table.GetEnumerator();
        }
        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>An System.Collections.IEnumerator object that can be used 
        /// to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _table.GetEnumerator();
        }

        /// <summary>
        /// Gets the item at the specified key without invoking any
        /// metamethods.
        /// </summary>
        /// <param name="key">The key to get.</param>
        /// <returns>The value at the specified key.</returns>
        public object GetItemRaw(object key)
        {
            return _table.GetItemRaw(key);
        }
        /// <summary>
        /// Sets the item at the specified key without invoking any
        /// metamethods.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="value">The value to set the key to.</param>
        public void SetItemRaw(object key, object value)
        {
            _table.SetItemRaw(key, value);
        }
        /// <summary>
        /// Gets the length of the table acording to Lua.  This will use any
        /// metamethods that exists to get the length.
        /// </summary>
        /// <returns>The length of the table.</returns>
        public double GetLength()
        {
            return _table.GetLength();
        }

        /// <summary>
        /// Helper method for 'next' Lua method.  This returns the tuple
        /// defined by the element after the one with the value index.
        /// The order is undefined (as is in the Lua spec) and is
        /// defined only by the Dictionary iterator.
        /// </summary>
        /// <param name="index">The value to get the next item of.</param>
        /// <returns>The key and value for the element after 'index' or null entries.</returns>
        internal static Tuple<object, object> GetNext(ILuaTable table, object index)
        {
            int i = (index == null ? 0 : 1);
            foreach (var item in table)
            {
                if (item.Key == index)
                    i = -1;
                if (i == 0)
                    return new Tuple<object, object>(item.Key, item.Value);
                i++;
            }

            return new Tuple<object, object>(null, null);
        }

        /// <summary>
        /// Determines whether the specified System.Object is equal to the current System.Object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is LuaTableNet)
                obj = ((LuaTableNet)obj)._table;
            return _table.Equals(obj);
        }
        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>A hash code for the current System.Object.</returns>
        public override int GetHashCode()
        {
            return _table.GetHashCode();
        }
        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return _table.ToString();
        }

        #region DynamicObject overrides

        /// <summary>
        /// Returns the enumeration of all dynamic member names.
        /// </summary>
        /// <returns>A sequence that contains dynamic member names.</returns>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            foreach (var item in _table)
                if (item.Key is string)
                    yield return item.Key as string;
        }
        /// <summary>
        /// Provides implementation for type conversion operations. Classes derived from
        ///     the System.Dynamic.DynamicObject class can override this method to specify
        ///     dynamic behavior for operations that convert an object from one type to another.
        /// </summary>
        /// <param name="binder">Provides information about the conversion operation. The binder.Type property
        ///     provides the type to which the object must be converted. For example, for
        ///     the statement (String)sampleObject in C# (CType(sampleObject, Type) in Visual
        ///     Basic), where sampleObject is an instance of the class derived from the System.Dynamic.DynamicObject
        ///     class, binder.Type returns the System.String type. The binder.Explicit property
        ///     provides information about the kind of conversion that occurs. It returns
        ///     true for explicit conversion and false for implicit conversion.</param>
        /// <param name="result">The result of the type conversion operation.</param>
        /// <returns>true if the operation is successful; otherwise, false. If this method returns
        ///     false, the run-time binder of the language determines the behavior. (In most
        ///     cases, a language-specific run-time exception is thrown.)</returns>
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (typeof(ILuaTable).IsAssignableFrom(binder.Type))
            {
                result = this;
                return true;
            }
            return base.TryConvert(binder, out result);
        }
        /// <summary>
        /// Provides the implementation for operations that get member values. Classes
        ///     derived from the System.Dynamic.DynamicObject class can override this method
        ///     to specify dynamic behavior for operations such as getting a value for a
        ///     property.
        /// </summary>
        /// <param name="binder">Provides information about the object that called the dynamic operation.
        ///     The binder.Name property provides the name of the member on which the dynamic
        ///     operation is performed. For example, for the Console.WriteLine(sampleObject.SampleProperty)
        ///     statement, where sampleObject is an instance of the class derived from the
        ///     System.Dynamic.DynamicObject class, binder.Name returns "SampleProperty".
        ///     The binder.IgnoreCase property specifies whether the member name is case-sensitive.</param>
        /// <param name="result">The result of the get operation. For example, if the method is called for
        ///     a property, you can assign the property value to result.</param>
        /// <returns>true if the operation is successful; otherwise, false. If this method returns
        ///     false, the run-time binder of the language determines the behavior. (In most
        ///     cases, a run-time exception is thrown.)</returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            object o = _table[binder.Name];
            int a;
            result = NetHelpers.ConvertType(o, binder.ReturnType, out a);
            if (result is double)
                result = new NumberProxy((double)result);
            return true;
        }
        /// <summary>
        /// Provides the implementation for operations that set member values. Classes
        ///     derived from the System.Dynamic.DynamicObject class can override this method
        ///     to specify dynamic behavior for operations such as setting a value for a
        ///     property.
        /// </summary>
        /// <param name="binder">Provides information about the object that called the dynamic operation.
        ///     The binder.Name property provides the name of the member to which the value
        ///     is being assigned. For example, for the statement sampleObject.SampleProperty
        ///     = "Test", where sampleObject is an instance of the class derived from the
        ///     System.Dynamic.DynamicObject class, binder.Name returns "SampleProperty".
        ///     The binder.IgnoreCase property specifies whether the member name is case-sensitive.</param>
        /// <param name="value">The value to set to the member. For example, for sampleObject.SampleProperty
        ///     = "Test", where sampleObject is an instance of the class derived from the
        ///     System.Dynamic.DynamicObject class, the value is "Test".</param>
        /// <returns>true if the operation is successful; otherwise, false. If this method returns
        ///     false, the run-time binder of the language determines the behavior. (In most
        ///     cases, a language-specific run-time exception is thrown.)</returns>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            _table[binder.Name] = value;
            return true;
        }
        /// <summary>
        /// Provides the implementation for operations that get a value by index. Classes
        ///     derived from the System.Dynamic.DynamicObject class can override this method
        ///     to specify dynamic behavior for indexing operations.
        /// </summary>
        /// <param name="binder">Provides information about the operation.</param>
        /// <param name="indexes">The indexes that are used in the operation. For example, for the sampleObject[3]
        ///     operation in C# (sampleObject(3) in Visual Basic), where sampleObject is
        ///     derived from the DynamicObject class, indexes[0] is equal to 3.</param>
        /// <param name="result">The result of the index operation.</param>
        /// <returns>true if the operation is successful; otherwise, false. If this method returns
        ///     false, the run-time binder of the language determines the behavior. (In most
        ///     cases, a run-time exception is thrown.)</returns>
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (indexes != null && indexes.Length == 1)
            {
                object o = _table[indexes[0]];
                int a;
                result = NetHelpers.ConvertType(o, binder.ReturnType, out a);
                if (result is double)
                    result = new NumberProxy((double)result);
                return true;
            }
            else
                return base.TryGetIndex(binder, indexes, out result);
        }
        /// <summary>
        ///  Provides the implementation for operations that set a value by index. Classes
        ///     derived from the System.Dynamic.DynamicObject class can override this method
        ///     to specify dynamic behavior for operations that access objects by a specified
        ///     index.
        /// </summary>
        /// <param name="binder">Provides information about the operation.</param>
        /// <param name="indexes">The indexes that are used in the operation. For example, for the sampleObject[3]
        ///     = 10 operation in C# (sampleObject(3) = 10 in Visual Basic), where sampleObject
        ///     is derived from the System.Dynamic.DynamicObject class, indexes[0] is equal
        ///     to 3.</param>
        /// <param name="value">The value to set to the object that has the specified index. For example,
        ///     for the sampleObject[3] = 10 operation in C# (sampleObject(3) = 10 in Visual
        ///     Basic), where sampleObject is derived from the System.Dynamic.DynamicObject
        ///     class, value is equal to 10.</param>
        /// <returns>true if the operation is successful; otherwise, false. If this method returns
        ///     false, the run-time binder of the language determines the behavior. (In most
        ///     cases, a language-specific run-time exception is thrown.</returns>
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            if (indexes != null && indexes.Length == 1)
            {
                _table[indexes[0]] = value;
                return true;
            }
            else
                return base.TrySetIndex(binder, indexes, value);
        }

        #endregion

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
            _table[index] = value;
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
            return _table[index];
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
        /// <param name="target">The object that this was called on.</param>
        /// <param name="memberCall">Whether the call used member call syntax (:).</param>
        /// <param name="byRef">An array of the indicies that are passed by-reference.</param>
        /// <param name="args">The current arguments, can be null or empty.</param>
        /// <returns>The arguments to return to Lua.</returns>
        /// <exception cref="System.ArgumentNullException">If E is null.</exception>
        /// <exception cref="System.ArgumentException">If the object cannot be
        /// invoked with the given arguments.</exception>
        /// <exception cref="System.Reflection.AmbiguousMatchException">If there are two
        /// valid overloads for the given arguments.</exception>
        MultipleReturn IMethod.Invoke(object target, bool memberCall, int[] byRef, object[] args)
        {
            return ((IMethod)_table).Invoke(target, memberCall, -1, byRef, args);
        }
        /// <summary>
        /// Invokes the current object with the given arguments.
        /// </summary>
        /// <param name="target">The object that this was called on.</param>
        /// <param name="memberCall">Whether the call used member call syntax (:).</param>
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
        MultipleReturn IMethod.Invoke(object target, bool memberCall, int overload, int[] byRef, object[] args)
        {
            return ((IMethod)_table).Invoke(target, memberCall, overload, byRef, args);
        }

        #endregion
    }
}