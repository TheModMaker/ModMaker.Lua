<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua;

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
        public virtual void Some()
        {
            Console.WriteLine("In base class.");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // create the Lua object
            Lua lua = new Lua();
            dynamic E = lua.Environment;

            // expose a type to Lua
            lua.Register(typeof(ITest));

            // load and execute a Lua file
            lua.DoFile("Life.lua");

            // execute a method defined in Lua
            E.LIFE(40, 20);

            // get a variable from Lua
            dynamic table = E.Some;
            int x = table.X;

            // create a type from Lua
            ITest obj = E.Test.CreateInstance();
            obj.Some();
            
            // keep the console window open
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua;

namespace TestApplication
{
    /// <summary>
    /// An interface to derive from in Lua code.
    /// </summary>
    public interface ITest
    {
        bool Do();
    }

    class Program
    {
        static void Main(string[] args)
        {
            // create the Lua object
            Lua lua = new Lua();
            dynamic E = lua.Environment;

            // expose a type to Lua
            lua.Register(typeof(ITest));

            // load and execute a Lua file
            lua.DoFile("Life.lua");

            // execute a method defined in Lua
            E.LIFE(40, 20);

            // get a variable from Lua
            dynamic table = E.Some;
            int x = table.X;

            // create a type from Lua
            ITest obj = E.Test.CreateInstance();
            obj.Do();
            
            // keep the console window open
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
