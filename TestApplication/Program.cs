using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua;
using System.IO;
using ModMaker.Lua.Runtime;

namespace TestApplication
{
    /// <summary>
    /// An interface to derive from in Lua code.
    /// </summary>
    public abstract class ITest
    {
        // must define an explicit empty constructor.
        protected ITest() { }

        public abstract bool Do();
        public virtual int Some()
        {
            return 1;
        }
    }
    
    class Program
    {
        delegate void Test(ref int i);

        public static void Foo(ref int i)
        {
            i = 24;
        }

        static void Main(string[] args)
        {
            // create the Lua object
            Lua lua = new Lua();
            dynamic E = lua.Environment;

            E.TailCalls = Lua.UseDynamicTypes;
            E.DoThreads = true;

            // expose a type to Lua
            lua.Register(typeof(ITest));
            lua.Register((Test)Foo);

            // load and execute a Lua file
            var v = lua.Load("Tests.lua");

            v.Invoke(null, null);
            
            // keep the console window open
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
