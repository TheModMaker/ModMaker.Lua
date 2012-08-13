using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace ModMaker.Lua.Runtime
{
    class LuaParameters : IEnumerable
    {
        object[] args;

        public LuaParameters(object[] args, LuaEnvironment e)
        {
            List<object> l = new List<object>();
            for (int i = 0; i < args.Length; i++)
            {
                object o = RuntimeHelper.GetValue(args[i]);
                if (o is MultipleReturn)
                {
                    if (i + 1 < args.Length)
                        l.Add((o as MultipleReturn)[0]);
                    else
                    {
                        foreach (var item in (o as MultipleReturn))
                            l.Add(item);
                        break;
                    }
                }
                else
                    l.Add(o);
            }

            this.args = l.ToArray();
            this.Environment = e;
        }

        public object this[int i] { get { return GetArg(i); } }
        public int Count { get { return args.Length; } }
        public LuaEnvironment Environment { get; private set; }

        public object GetArg(int i)
        {
            return i < args.Length ? RuntimeHelper.GetValue(args[i]) : null;
        }
        public void SetArg(int i, object value)
        {
            if (i < args.Length)
                args[i] = value;
        }
        public IEnumerator GetEnumerator()
        {
            return args.GetEnumerator();
        }
    }
}