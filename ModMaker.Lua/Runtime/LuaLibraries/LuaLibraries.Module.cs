using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        class Module
        {
            ILuaEnvironment E_;

            public Module(ILuaEnvironment E)
            {
                if (!(E is ILuaEnvironmentNet))
                {
                    throw new InvalidOperationException(
                        "'require' only works with the NET version of the environment.");
                }
                E_ = E;
            }

            public void Initialize()
            {
                E_.RegisterDelegate((Func<string, ILuaValue>)require, "require");
            }

            ILuaValue require(string name)
            {
                IModuleBinder bind = ((ILuaEnvironmentNet)E_).ModuleBinder;
                return bind.Load(E_, name);
            }
        }
    }
}
