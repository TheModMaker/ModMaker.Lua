using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines an ordered collection of LuaValues.  This is used
    /// as an argument to methods and as the result.  This is also
    /// a LuaValue and under normal operations, should act as the
    /// first item.
    /// </summary>
    [ContractClass(typeof(LuaMultiValueContract))]
    public interface ILuaMultiValue : ILuaValue, IEnumerable<ILuaValue>
    {
        /// <summary>
        /// Gets the value at the given index.  If the index is less
        /// than zero or larger than Count, it should return LuaNil.
        /// This should never return null.  Setting the value outside
        /// the current range has no effect.
        /// </summary>
        /// <param name="index">The index to get.</param>
        /// <returns>The value at the given index, or LuaNil.</returns>
        ILuaValue this[int index] { get; set; }
        /// <summary>
        /// Gets the number of values in this object.
        /// </summary>
        int Count { get; }
        
        /// <summary>
        /// Returns a new object with the same values as this object except
        /// where any extra values are removed and any missing values are
        /// set to null.
        /// </summary>
        /// <param name="number">The number of values to have.</param>
        /// <returns>A new ILuaMultiValue object with the values in this object.</returns>
        [Pure]
        ILuaMultiValue AdjustResults(int number);
    }
    
    /// <summary>
    /// A helper class for the contract of ILuaMultiValue.
    /// </summary>
    [ContractClassFor(typeof(ILuaMultiValue))]
    abstract class LuaMultiValueContract : ILuaMultiValue
    {
        public ILuaValue this[int index] 
        {
            get
            {
                Contract.Ensures(Contract.Result<ILuaValue>() != null);
                return null;
            }
            set
            {
                Contract.Requires(index >= 0 && index < Count);
                Contract.Requires(value != null);
                Contract.Ensures(this[index] == value);
            }
        }
        public int Count
        {
            get
            {
                Contract.Ensures(Contract.Result<int>() >= 0);
                return 0;
            }
        }
        public abstract bool IsTrue { get;  }
        public abstract LuaValueType ValueType { get; }

        public abstract object GetValue();
        public abstract double? AsDouble();
        public abstract T As<T>();
        public abstract bool TypesCompatible<T>();
        public abstract void GetCastInfo<T>(out LuaCastType type, out int distance);

        public ILuaMultiValue AdjustResults(int number)
        {
            Contract.Ensures(Contract.Result<ILuaMultiValue>() != null);
            Contract.Ensures(Contract.Result<ILuaMultiValue>().Count == number || number < 0);
            return null;
        }

        public abstract ILuaValue GetIndex(ILuaValue index);
        public abstract void SetIndex(ILuaValue index, ILuaValue value);
        public abstract ILuaMultiValue Invoke(ILuaValue self, bool memberCall, int overload, ILuaMultiValue args);
        public abstract ILuaValue Arithmetic(Parser.Items.BinaryOperationType type, ILuaValue other);
        public abstract ILuaValue Minus();
        public abstract ILuaValue Not();
        public abstract ILuaValue Length();
        public abstract ILuaValue RawLength();
        public abstract ILuaValue Single();
        public abstract bool Equals(ILuaValue other);
        public abstract int CompareTo(ILuaValue other);
        public abstract IEnumerator<ILuaValue> GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}