using ModMaker.Lua.Parser.Items;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModMaker.Lua.Runtime.LuaValues
{
    /// <summary>
    /// Defines a table in Lua.  This acts as a dictionary of key-value pairs.
    /// </summary>
    /// <remarks>
    /// As this implements two IEnumerable&lt;T&gt;, IEnumerable returns the
    /// KeyValuePair values.
    /// </remarks>
    public sealed class LuaTable : LuaValueBase, ILuaTable
    {
        static LuaString _index = new LuaString("__index");
        static LuaString _newindex = new LuaString("__newindex");
        static LuaString _len = new LuaString("__len");
        Dictionary<ILuaValue, ILuaValue> _values = new Dictionary<ILuaValue, ILuaValue>();

        /// <summary>
        /// Gets the value type of the value.
        /// </summary>
        public override LuaValueType ValueType { get { return LuaValueType.Table; } }

        /// <summary>
        /// Gets or sets the metatable for the table.
        /// </summary>
        public ILuaTable MetaTable { get; set; }
        /// <summary>
        /// Gets or sets the value at the given index.  This uses 
        /// meta-methods if needed.
        /// </summary>
        /// <param name="index">The index to get/set.</param>
        /// <returns>The value at the given index.</returns>
        public ILuaValue this[ILuaValue index] 
        {
            get { return Get(index); }
            set { Set(index, value); }
        }

        /// <summary>
        /// Helper function, gets the value at the given index.  Uses
        /// meta-methods if needed.
        /// </summary>
        /// <param name="index">The index to use.</param>
        /// <returns>The value at the given index.</returns>
        ILuaValue Get(ILuaValue index)
        {
            index = index ?? LuaNil.Nil;

            ILuaValue ret;
            if (!_values.TryGetValue(index, out ret) && MetaTable != null)
            {
                var method = MetaTable.GetItemRaw(_index);
                if (method != null)
                {
                    if (method.ValueType == LuaValueType.Function)
                        return method.Invoke(this, true, -1, new LuaMultiValue(index)).Single();
                    else
                        return method.GetIndex(index);
                }
            }

            return ret ?? LuaNil.Nil;
        }
        /// <summary>
        /// Helper function, sets the value at the given index.  Uses
        /// meta-methods if needed.
        /// </summary>
        /// <param name="index">The index to use.</param>
        /// <param name="value">The value to set to.</param>
        void Set(ILuaValue index, ILuaValue value)
        {
            index = index ?? LuaNil.Nil;
            value = value ?? LuaNil.Nil;

            ILuaValue ret;
            if (!_values.TryGetValue(index, out ret) && MetaTable != null)
            {
                var method = MetaTable.GetItemRaw(_newindex);
                if (method != null && method != LuaNil.Nil)
                {
                    if (method.ValueType == LuaValueType.Function)
                        method.Invoke(this, true, -1, new LuaMultiValue(index, value));
                    else
                        method.SetIndex(index, value);

                    return;
                }
            }

            SetItemRaw(index, value);
        }

        /// <summary>
        /// Gets the length of the value.
        /// </summary>
        /// <returns>The length of the value.</returns>
        public override ILuaValue Length()
        {
            if (MetaTable != null)
            {
                ILuaValue meth = MetaTable.GetItemRaw(_len);
                if (meth != null)
                {
                    var ret = meth.Invoke(this, true, -1, LuaMultiValue.Empty);
                    return ret[0];
                }
            }

            return RawLength();
        }
        /// <summary>
        /// Gets the raw-length of the value.
        /// </summary>
        /// <returns>The length of the value.</returns>
        public override ILuaValue RawLength()
        {
            double i = 1;
            while (GetItemRaw(new LuaNumber(i)).ValueType != LuaValueType.Nil)
            {
                i++;
            }

            return new LuaNumber(i-1);
        }
        
        /// <summary>
        /// Indexes the value and returns the value.
        /// </summary>
        /// <param name="index">The index to use.</param>
        /// <returns>The value at the given index.</returns>
        public override ILuaValue GetIndex(ILuaValue index)
        {
            return Get(index);
        }
        /// <summary>
        /// Indexes the value and assigns it a value.
        /// </summary>
        /// <param name="index">The index to use.</param>
        /// <param name="value">The value to assign to.</param>
        public override void SetIndex(ILuaValue index, ILuaValue value)
        {
            Set(index, value);
        }

        /// <summary>
        /// Gets the value at the given index without using meta-methods.
        /// </summary>
        /// <param name="index">The index to get at.</param>
        /// <returns>The value at the given index.</returns>
        public ILuaValue GetItemRaw(ILuaValue index)
        {
            index = index ?? LuaNil.Nil;

            ILuaValue ret;
            return _values.TryGetValue(index, out ret) && ret != null ? ret : LuaNil.Nil;
        }
        /// <summary>
        /// Sets the value at the given index without using meta-methods.
        /// </summary>
        /// <param name="index">The index to get at.</param>
        /// <param name="value">The value to assign to.</param>
        public void SetItemRaw(ILuaValue index, ILuaValue value)
        {
            index = index ?? LuaNil.Nil;
            value = value ?? LuaNil.Nil;

            if (value == LuaNil.Nil)
            {
                if (_values.ContainsKey(index))
                    _values.Remove(index);
            }
            else
                _values[index] = value;
        }

        /// <summary>
        /// Gets a IEnumerable&lt;KeyValuePair&lt;object, object&gt;&gt; to enumerate over
        /// each of the keys of the Table.
        /// </summary>
        /// <returns>An enumerator to enumerate of the keys.</returns>
        public IEnumerator<KeyValuePair<ILuaValue, ILuaValue>> GetEnumerator()
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
        /// Performs a binary arithmetic operation and returns the result.
        /// </summary>
        /// <param name="type">The type of operation to perform.</param>
        /// <param name="other">The other value to use.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// If the operation cannot be performed with the given values.
        /// </exception>
        /// <exception cref="System.InvalidArgumentException">
        /// If the argument is an invalid value.
        /// </exception>
        public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other)
        {
            return base.ArithmeticBase(type, other) ?? ((ILuaValueVisitor)other).Arithmetic(type, this);
        }
        /// <summary>
        /// Performs a binary arithmetic operation and returns the result.
        /// </summary>
        /// <param name="type">The type of operation to perform.</param>
        /// <param name="self">The first value to use.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// If the operation cannot be performed with the given values.
        /// </exception>
        /// <exception cref="System.InvalidArgumentException">
        /// If the argument is an invalid value.
        /// </exception>
        public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self)
        {
            return self.ArithmeticFrom(type, this);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the other parameter; otherwise, false.</returns>
        public override bool Equals(ILuaValue other)
        {
            return object.ReferenceEquals(this, other);
        }
        /// <summary>
        ///  Determines whether the specified System.Object is equal to the current System.Object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return object.ReferenceEquals(this, obj);
        }
        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>A hash code for the current System.Object.</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}