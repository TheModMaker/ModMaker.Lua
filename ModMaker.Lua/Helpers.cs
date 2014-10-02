using ModMaker.Lua.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ModMaker.Lua
{
    /// <summary>
    /// A static class that contains several helper methods.
    /// </summary>
    static class Helpers
    {
        /// <summary>
        /// Helper class that calls a given method when Dispose is called.
        /// </summary>
        sealed class DisposableHelper : IDisposable
        {
            Action act;
            bool _disposed = false;

            public DisposableHelper(Action act)
            {
                if (act == null)
                    throw new ArgumentNullException("act");

                this.act = act;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                act();
            }
        }

        /// <summary>
        /// Creates an IDisposable object that calls the given funcrion when
        /// Dispose is called.
        /// </summary>
        /// <param name="act">The function to call on Dispose.</param>
        /// <returns>An IDisposable object.</returns>
        public static IDisposable Disposable(Action act)
        {
            return new DisposableHelper(act);
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
        public static object[] FixArgs(object[] args, int count)
        {
            if (args == null)
                return new object[count > 0 ? count : 0];
            else
            {
                List<object> temp = new List<object>(args.Length);
                for (int i = 0; i < args.Length; i++)
                {
                    object o = args[i];
                    MultipleReturn multi = o as MultipleReturn;
                    if (multi != null)
                    {
                        if (i + 1 == args.Length)
                        {
                            foreach (var item in multi)
                            {
                                o = item;
                                if (o is MultipleReturn)
                                    o = (o as MultipleReturn)[0];
                                if (o is Int16 || o is Int32 || o is Int64 || o is UInt16 || o is UInt32 || o is UInt64 ||
                                    o is Decimal || o is Single)
                                    o = Convert.ToDouble(o, CultureInfo.InvariantCulture);
                                temp.Add(o);
                            }
                            break;
                        }
                        else
                            o = multi[0];
                    }
                    if (o is Int16 || o is Int32 || o is Int64 || o is UInt16 || o is UInt32 || o is UInt64 ||
                            o is Decimal || o is Single)
                        o = Convert.ToDouble(o, CultureInfo.InvariantCulture);

                    temp.Add(o);
                }

                // ensure at least 'count' number of elements
                while (temp.Count < count)
                {
                    temp.Add(null);
                }
                return temp.ToArray();
            }
        }
        /// <summary>
        /// If the given object is a number, it will convert it to a double,
        /// otherwise it is unchanged.
        /// </summary>
        /// <param name="value">The source object.</param>
        /// <returns>The source object as a double or the source.</returns>
        public static object ToDouble(object value)
        {
            if (value == null)
                return null;

            Type t1 = value.GetType();
            if (t1 == typeof(SByte) ||
                t1 == typeof(Int16) ||
                t1 == typeof(Int32) ||
                t1 == typeof(Int64) ||
                t1 == typeof(Single) ||
                t1 == typeof(UInt16) ||
                t1 == typeof(UInt32) ||
                t1 == typeof(UInt64) ||
                t1 == typeof(Byte) ||
                t1 == typeof(Decimal))
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            else
                return value;
        }
    }
}