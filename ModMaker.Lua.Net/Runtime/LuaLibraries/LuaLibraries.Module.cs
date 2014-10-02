using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        /// <summary>
        /// Contains the module Lua libraries.
        /// </summary>
        static class Module
        {
            public sealed class require : LuaFrameworkMethod
            {
                public require(ILuaEnvironment E) : base(E, "require") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args == null || args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'require'.");

                    string str = args[0] as string;
                    if (str == null)
                        throw new ArgumentException("First argument to function 'require' must be a string.");

                    if (!(Environment is ILuaEnvironmentNet))
                        throw new InvalidOperationException("'require' only works with the NET version of the environment.");

                    var bind = ((ILuaEnvironmentNet)Environment).ModuleBinder;
                    object ret = bind.Load(Environment, str);

                    if (ret is object[])
                        return new MultipleReturn((IEnumerable)ret);
                    else
                        return new MultipleReturn(ret);
                }
            }
        }
    }
}
