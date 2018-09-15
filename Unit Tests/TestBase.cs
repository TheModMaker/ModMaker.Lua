// Copyright 2017 Jacob Trimble
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
using ModMaker.Lua.Runtime.LuaValues;
using NUnit.Framework;
using System;

namespace UnitTests
{
    /// <summary>
    /// Defines a base class for tests.  This defines a Lua object that includes global methods for
    /// assertions that will fail the test.
    /// </summary>
    public class TestBase
    {
        protected TestBase()
        {
            Lua = new Lua();
            Lua.Register((Action<string>)Assert.Fail, "fail");
            Lua.Register((Action<object, object, string>)assertEquals);
            Lua.Register((Action<bool, string>)assertTrue);
            Lua.Register((Action<bool, string>)assertFalse);
            Lua.Register((Action<object, string>)assertNotNull);
            Lua.Register((Action<double, double, string>)assertEqualsDelta);
            Lua.Register((Action<string, ILuaValue>)assertThrows);
        }

        /// <summary>
        /// Gets the current Lua instance.
        /// </summary>
        protected Lua Lua { get; private set; }

        static void assertNotNull(object actual, string message)
        {
            Assert.IsNotNull(actual, message);
        }

        static void assertEquals(object expected, object actual, string message)
        {
            Assert.AreEqual(expected, actual, message);
        }

        static void assertTrue(bool actual, string message)
        {
            Assert.IsTrue(actual, message);
        }

        static void assertFalse(bool actual, string message)
        {
            Assert.IsFalse(actual, message);
        }

        static void assertEqualsDelta(double expected, double actual, string message)
        {
            Assert.AreEqual(expected, actual, 0.0000001, message);
        }

        void assertThrows(string message, ILuaValue value)
        {
            bool throws = false;
            try
            {
                value.Invoke(LuaNil.Nil, false, -1, Lua.Environment.Runtime.CreateMultiValue());
            }
            catch (Exception)
            {
                throws = true;
            }
            Assert.IsTrue(throws, "Should throw exception:" + message);
        }
    }
}