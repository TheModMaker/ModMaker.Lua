using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// A table for use in Lua code.  This is a wraper arround a dictionary.
    /// Keys and Values are objects.
    /// </summary>
    public sealed class LuaTable : DynamicObject, IEnumerable<KeyValuePair<object, object>>
    {
        Dictionary<object, object> _values;

        /// <summary>
        /// Creates a new LuaTable object.
        /// </summary>
        public LuaTable()
        {
            _values = new Dictionary<object, object>();
        }
        internal LuaTable(Dictionary<object, object> values)
        {
            this._values = values;
        }

        internal LuaTable MetaTable { get; set; }
        internal int Length { get { return _values.Count; } }

        /// <summary>
        /// Gets or sets the value of the specified key.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>The value of the given key.</returns>
        public dynamic this[object key]
        {
            get  { return NumberProxy.Create(_get(key)); }
            set { _set(key, (object)value); }
        }

        internal object _get(object key)
        {
            object o = RuntimeHelper.GetValue(key).ToDouble();
            if (MetaTable != null && !_values.ContainsKey(o))
            {
                LuaMethod m = MetaTable.GetItemRaw("__index") as LuaMethod;
                if (m != null)
                {
                    var ret = m.InvokeInternal(new[] { this, o }, -1);
                    return ret[0];
                }
                return null;
            }

            object rets;
            return _values.TryGetValue(o, out rets) ? rets : null;
        }
        internal void _set(object key, object value)
        {
            object o = RuntimeHelper.GetValue(key).ToDouble();
            if (!_values.ContainsKey(o) && MetaTable != null)
            {
                LuaMethod m = MetaTable.GetItemRaw("__newindex") as LuaMethod;
                if (m != null)
                {
                    m.InvokeInternal(new[] { this, o, value }, -1);
                    return;
                }
            }

            if (value == null)
            {
                if (_values.ContainsKey(o))
                    _values.Remove(o);
            }
            else
                _values[o] = value.ToDouble();
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
        public object GetItemRaw(object key)
        {
            object o = RuntimeHelper.GetValue(key).ToDouble();
            return _values.ContainsKey(o) ? _values[o] : null;
        }
        /// <summary>
        /// Sets the item at the specified key without invoking any
        /// metamethods.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="value">The value to set the key to.</param>
        public void SetItemRaw(object key, object value)
        {
            object o = RuntimeHelper.GetValue(key).ToDouble();
            if (value == null)
            {
                if (_values.ContainsKey(o))
                    _values.Remove(o);
            }
            else
                _values[o] = value.ToDouble();
        }

        internal Tuple<object, object> GetNext(object index)
        {
            int i = (index == null ? 0 : 1);
            foreach (var item in _values)
            {
                if (item.Key == index)
                    i = -1;
                if (i == 0)
                    return new Tuple<object, object>(item.Key, item.Value);
                i++;
            }

            return new Tuple<object, object>(null, null);
        }
        internal double GetLength()
        {
            if (MetaTable != null)
            {
                LuaMethod meth = MetaTable.GetItemRaw("__len") as LuaMethod;
                if (meth != null)
                {
                    var ret = meth.InvokeInternal(new[] { this }, -1);
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
        /// Returns the enumeration of all dynamic member names.
        /// </summary>
        /// <returns>A sequence that contains dynamic member names.</returns>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            foreach (var item in _values)
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
            if (binder.Type == typeof(LuaTable))
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
            object o = _get(binder.Name);
            result = RuntimeHelper.ConvertType(o, binder.ReturnType);
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
            _set(binder.Name, value);
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
                object o = _get(indexes[0]);
                result = RuntimeHelper.ConvertType(o, binder.ReturnType);
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
                _set(indexes[0], value);
                return true;
            }
            else
                return base.TrySetIndex(binder, indexes, value);
        }
    }
}