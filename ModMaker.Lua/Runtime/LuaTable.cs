using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Dynamic;
using System.Reflection;

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
        public dynamic this[object key, int i]
        {
            get  { return _get(key); }
            set { _set(key, (object)value); }
        }

        internal object _get(object key)
        {
            object o = RuntimeHelper.GetValue(key);
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
            object o = RuntimeHelper.GetValue(key);
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
                _values[o] = value;
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
            object o = RuntimeHelper.GetValue(key);
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
            object o = RuntimeHelper.GetValue(key);
            if (value == null)
            {
                if (_values.ContainsKey(o))
                    _values.Remove(o);
            }
            else
                _values[o] = value;
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
        internal double GetSequenceLen()
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

            double i = 1;
            while (_values.ContainsKey(i))
                i++;

            foreach (var item in _values)
            {
                if (item.Key is double)
                {
                    double test = (double)item.Key;
                    if (Math.Floor(test) != Math.Ceiling(test) || test < 1 || test > i)
                        return -1;
                }
            }
            return i;
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            foreach (var item in _values)
                if (item.Key is string)
                    yield return item.Key as string;
        }
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.Type == typeof(LuaTable))
            {
                result = this;
                return true;
            }
            return base.TryConvert(binder, out result);
        }
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            object o = _get(binder.Name);
            result = RuntimeHelper.ConvertType(o, binder.ReturnType);
            return true;
        }
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            _set(binder.Name, value);
            return true;
        }
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (indexes != null && indexes.Length == 1)
            {
                object o = _get(indexes[0]);
                result = RuntimeHelper.ConvertType(o, binder.ReturnType);
                return true;
            }
            else
                return base.TryGetIndex(binder, indexes, out result);
        }
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