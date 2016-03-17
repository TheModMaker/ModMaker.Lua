using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua.Parser;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Collections;
using System.Threading;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Reflection;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        public static void Initialize(ILuaEnvironment E)
        {
            LuaLibraries libraries = E.Settings.Libraries;

            if ((libraries & LuaLibraries.Standard) == LuaLibraries.Standard)
            {
                Standard.Initialize(E);
            }
            if ((libraries & LuaLibraries.IO) == LuaLibraries.IO)
            {
                IO.Initialize(E);
            }
            if ((libraries & LuaLibraries.String) == LuaLibraries.String)
            {
                String.Initialize(E);
            }
            if ((libraries & LuaLibraries.Math) == LuaLibraries.Math)
            {
                Math.Initialize(E);
                Bit32.Initialize(E);
            }
            if ((libraries & LuaLibraries.Table) == LuaLibraries.Table)
            {
                new Table(E).Initialize();
            }
            if ((libraries & LuaLibraries.OS) == LuaLibraries.OS)
            {
                new OS(E).Initialize();
            }
            if ((libraries & LuaLibraries.Modules) == LuaLibraries.Modules)
            {
                new Module(E).Initialize();
            }
            if ((libraries & LuaLibraries.Coroutine) == LuaLibraries.Coroutine)
            {
                new Coroutine(E).Initialize();
            }
        }

        static void Register(ILuaEnvironment E, ILuaValue table, Delegate func, string name = null)
        {
            var funcValue = E.Runtime.CreateValue(func);
            var nameValue = E.Runtime.CreateValue(name ?? func.Method.Name);
            table.SetIndex(nameValue, funcValue);
        }

        static int normalizeIndex_(int max, int i)
        {
            if (i == 0)
                return 1;
            else if (i < 0)
                return max + i + 1;
            else if (i > max)
                return max;
            else
                return i;
        }

    }
}