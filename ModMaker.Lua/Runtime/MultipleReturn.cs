using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Globalization;

namespace ModMaker.Lua.Runtime
{
    class MultipleReturn : IEnumerable<object>
    {
        public MultipleReturn(IEnumerable args)
        {
            List<object> l = new List<object>();
            var itr = args.GetEnumerator();
            bool b = false;
            while (b || itr.MoveNext())
            {
                b = false;
                object o = RuntimeHelper.GetValue(itr.Current);
                MultipleReturn m = o as MultipleReturn;
                if (o is Int16 || o is Int32 || o is Int64 || o is UInt16 || o is UInt32 || o is UInt64 ||
                    o is Decimal || o is Single)
                    o = Convert.ToDouble(o, CultureInfo.InvariantCulture);
                if (m != null)
                {
                    b = true;
                    if (itr.MoveNext())
                    {
                        o = m[0];
                    }
                    else
                    {
                        foreach (var item in m)
                        {
                            o = RuntimeHelper.GetValue(item);
                            if (o is Int16 || o is Int32 || o is Int64 || o is UInt16 || o is UInt32 || o is UInt64 ||
                                o is Decimal || o is Single)
                                o = Convert.ToDouble(o, CultureInfo.InvariantCulture);
                            if (o is MultipleReturn)
                                o = (o as MultipleReturn)[0];
                            l.Add(o);
                        }
                        break;
                    }
                }
                l.Add(o);
            }

            this.Values = l.ToArray();
        }
        public MultipleReturn(params object[] args)
            : this((IEnumerable)args) {  }

        public object[] Values { get; private set; }
        public int Count { get { return Values.Length; } }

        public object this[int i] { get { return i < 0 || i >= Values.Length ? null : Values[i]; } }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }
        public IEnumerator<object> GetEnumerator()
        {
            return new List<object>(Values).GetEnumerator();
        }
        public MultipleReturn AdjustResults(int num)
        {
            object[] temp = new object[num];
            for (int i = 0; i < num; i++)
                temp[i] = i >= Values.Length ? null : Values[i];
            Values = temp;

            return this;
        }
    }
}