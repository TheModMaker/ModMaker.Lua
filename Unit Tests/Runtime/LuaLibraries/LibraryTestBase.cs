// Copyright 2016 Jacob Trimble
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

using ModMaker.Lua;
using ModMaker.Lua.Runtime;
using NUnit.Framework;
using System;
using System.Linq;

namespace UnitTests.Runtime.LuaLibraries
{
    public class LibraryTestBase : TestBase
    {
        /// <summary>
        /// A custom type that is passed to Lua for testing.
        /// </summary>
        public class UserData
        {
            /// <summary>
            /// Required explicit constructor for Lua.
            /// </summary>
            public UserData() { }
        }

        protected LibraryTestBase()
        {
            Lua.Register(typeof(UserData));
        }

        /// <summary>
        /// Runs a test that tests invalid arguments passed to a method.  It will run the test
        /// with all types except the given one.
        /// </summary>
        /// <param name="validType">The valid argument type.</param>
        /// <param name="format">A format string for the code to test.</param>
        /// <param name="allowNil">True to allow nil to be passed.</param>
        protected void RunInvalidTypeTests(LuaValueType validType, string format, bool allowNil = false)
        {
            foreach (var type in Enum.GetValues(typeof(LuaValueType)).Cast<LuaValueType>())
            {
                if (type == validType || (type == LuaValueType.Nil && allowNil))
                    continue;

                try
                {
                    Lua.DoText(string.Format(format, GetValueForType(type)));
                    Assert.Fail("Expected ArgumentException to be thrown for type " + type);
                }
                catch (ArgumentException) { /* noop */ }
            }
        }

        /// <summary>
        /// Gets a Lua expression that is of the given type.
        /// </summary>
        /// <param name="type">The type of expression.</param>
        /// <returns>A valid Lua expression of the given type.</returns>
        static string GetValueForType(LuaValueType type)
        {
            switch (type)
            {
            case LuaValueType.Nil:
                return "nil";
            case LuaValueType.String:
                return "'foobar'";
            case LuaValueType.Bool:
                return "true";
            case LuaValueType.Table:
                return "{}";
            case LuaValueType.Function:
                return "function() end";
            case LuaValueType.Number:
                return "123";
            case LuaValueType.Thread:
                return "coroutine.create(function() end)";
            case LuaValueType.UserData:
                return "UserData()";
            default:
                throw new NotImplementedException();
            }
        }
    }
}