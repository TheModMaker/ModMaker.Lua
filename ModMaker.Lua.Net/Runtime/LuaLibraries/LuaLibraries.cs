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
        public static void Initialize(ILuaEnvironment/*!*/ E)
        {
            LuaLibraries libraries = E.Settings.Libraries;
            ILuaTable _globals = E.GlobalsTable;

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
                _globals.SetItemRaw("string", String.Initialize(E));
            }
            if ((libraries & LuaLibraries.Math) == LuaLibraries.Math)
            {
                _globals.SetItemRaw("math", Math.Initialize(E));
                _globals.SetItemRaw("bit32", Bit32.Initialize(E));
            }
            if ((libraries & LuaLibraries.Coroutine) == LuaLibraries.Coroutine)
            {
                _globals.SetItemRaw("coroutine", Coroutine.Initialize(E));
            }
            if ((libraries & LuaLibraries.OS) == LuaLibraries.OS)
            {
                _globals.SetItemRaw("os", OS.Initialize(E));
            }
            if ((libraries & LuaLibraries.Table) == LuaLibraries.Table)
            {
                _globals.SetItemRaw("table", Table.Initialize(E));
            }
            if ((libraries & LuaLibraries.Modules) == LuaLibraries.Modules)
            {
                _globals.SetItemRaw("require", new Module.require(E));
            }
        }
        static string _type(object value)
        {
            if (value == null)
                return "nil";
            else if (value is string)
                return "string";
            else if (value is double)
                return "number";
            else if (value is bool)
                return "boolean";
            else if (value is ILuaTable)
                return "table";
            else if (value is IMethod)
                return "function";
            else if (value is LuaThread)
                return "thread";
            else
                return "userdata";
        }
    }
}