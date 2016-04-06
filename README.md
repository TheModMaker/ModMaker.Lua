ModMaker.Lua
============

ModMaker.Lua is a complete rebuild of Lua for use in .NET. The code has been
entirely written in C# with no unmanaged code. This library parses Lua code,
creates a dynamic IL assembly, and executes it in a custom runtime. This
library also supports object-oriented Lua by allowing the creation of new types
from within Lua code. It is designed to be simple and easy to use while
providing extensibility.

# Features
* Simple design
* Create types in Lua
  * Derive from .Net types
  * Implement .Net interfaces
  * Implicit interface implementation
  * Public fields/properties
* Auto registering functions and types
  * No need to manage Lua stack
  * Accepts any method signature
  * Return multiple values to Lua
  * Access static an instance members of .Net types
  * Supports params arrays and optional arguments
  * Supports by-reference arguments passed from Lua
* Dynamic binding

# Example

```c#
using ModMaker.Lua;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Extensions;

public static string Something(ILuaTable table, string arg)
{
    return table[arg];
}

public static void Main(string[] args)
{
    Lua lua = new Lua();

    // Register a method for use in Lua.
    lua.Register(Something);

    // Load and run the given Lua file.
    lua.DoFiles("printf.lua", "argument", 12);

    // Get a global variable with the name 'Var'.
    dynamic E = lua.Environment;
    int d = E.Var;
}
```
