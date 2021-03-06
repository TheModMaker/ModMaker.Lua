// Copyright 2012 Jacob Trimble
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using ModMaker.Lua;

namespace ExampleProject {
  /// <summary>
  /// An interface to derive from in Lua code.
  /// </summary>
  public abstract class ITest {
    // must define an explicit empty constructor.
    protected ITest() { }

    public abstract bool Do();
    public virtual int Some() {
      return 1;
    }
  }

  class Program {
    delegate void Test(ref int i);

    public static void Foo(ref int i) {
      i = 24;
    }

    public static void Main(string[] _) {
      // Create the Lua object.
      Lua lua = new Lua();
      dynamic env = lua.Environment;

      //E.TailCalls = Lua.UseDynamicTypes;
      env.DoThreads = true;

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
