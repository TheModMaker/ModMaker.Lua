// Copyright 2014 Jacob Trimble
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
