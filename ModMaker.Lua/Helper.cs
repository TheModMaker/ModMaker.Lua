using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;

namespace ModMaker.Lua
{
    static class Helper
    {
        class ThenEnumerable<T> : IEnumerable<T>
        {
            IEnumerable<T> first, second;

            public ThenEnumerable(IEnumerable<T> first, IEnumerable<T> second)
            {
                this.first = first;
                this.second = second;
            }

            class ThenEnumerator : IEnumerator<T>
            {
                IEnumerator<T> first, second;
                bool onFirst = true;

                public ThenEnumerator(IEnumerator<T> first, IEnumerator<T> second)
                {
                    this.first = first;
                    this.second = second;
                }

                public T Current { get { return onFirst ? first.Current : second.Current; } }
                object System.Collections.IEnumerator.Current { get { return onFirst ? first.Current : second.Current; } }

                public void Dispose()
                {
                    first.Dispose();
                    second.Dispose();
                    first = null;
                    second = null;
                }
                public bool MoveNext()
                {
                    if (onFirst)
                    {
                        bool b = first.MoveNext();
                        if (!b)
                        {
                            onFirst = false;
                            return second.MoveNext();
                        }
                        return b;
                    }
                    else
                        return second.MoveNext();
                }
                public void Reset()
                {
                    first.Reset();
                    second.Reset();
                    onFirst = true;
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                return new ThenEnumerator(first.GetEnumerator(), second.GetEnumerator());
            }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return new ThenEnumerator(first.GetEnumerator(), second.GetEnumerator());
            }
        }

        public static IEnumerable<T> Then<T>(this IEnumerable<T> item, IEnumerable<T> other)
        {
            return new ThenEnumerable<T>(item, other);
        }
        public static string ToStringBase16(this IEnumerable<byte> item)
        {
            StringBuilder ret = new StringBuilder();
            foreach (var i in item)
                ret.Append(i.ToString("X2", CultureInfo.InvariantCulture));

            return ret.ToString();
        }
        public static void Write(this Stream stream, byte[] buffer)
        {
            stream.Write(buffer, 0, buffer.Length);
        }
        public static void AddUnique<T>(this List<T> list, T item)
        {
            if (!list.Contains(item))
                list.Add(item);
        }
        public static void AddRangeUnique<T>(this List<T> list, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                if (!list.Contains(item))
                    list.Add(item);
            }
        }
    }
}