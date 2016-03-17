using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua;
using System.IO;
using ModMaker.Lua.Runtime;
using System.Reflection.Emit;
using System.Linq.Expressions;

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
            // Create the Lua object.
            Lua lua = new Lua();
            dynamic E = lua.Environment;

            //E.TailCalls = Lua.UseDynamicTypes;
            E.DoThreads = true;

            // Expose these to Lua.
            lua.Register(typeof(ITest));
            lua.Register((Test)Foo);

            // Load and execute a Lua file.
            lua.DoFile("Tests.lua");
            
            // Keep the console window open.
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
