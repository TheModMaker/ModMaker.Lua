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

using System;
using ModMaker.Lua;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;
using NUnit.Framework;

namespace UnitTests {
  /// <summary>
  /// Defines a base class for tests.  This defines a Lua object that includes global methods for
  /// assertions that will fail the test.
  /// </summary>
  public class TestBase {
    [SetUp]
    public virtual void SetUp() {
      _lua = new Lua();
      _lua.Register((Action<string>)Assert.Fail, "fail");
      _lua.Register((Action<object, object, string>)_assertEquals, "assertEquals");
      _lua.Register((Action<bool, string>)_assertTrue, "assertTrue");
      _lua.Register((Action<bool, string>)_assertFalse, "assertFalse");
      _lua.Register((Action<object, string>)_assertNotNull, "assertNotNull");
      _lua.Register((Action<double, double, string>)_assertEqualsDelta, "assertEqualsDelta");
      _lua.Register((Action<string, ILuaValue>)_assertThrows, "assertThrows");
    }

    /// <summary>
    /// Gets the current Lua instance.
    /// </summary>
    protected Lua _lua { get; private set; }

    static void _assertNotNull(object actual, string message) {
      Assert.IsNotNull(actual, message);
    }

    static void _assertEquals(object expected, object actual, string message) {
      Assert.AreEqual(expected, actual, message);
    }

    static void _assertTrue(bool actual, string message) {
      Assert.IsTrue(actual, message);
    }

    static void _assertFalse(bool actual, string message) {
      Assert.IsFalse(actual, message);
    }

    static void _assertEqualsDelta(double expected, double actual, string message) {
      Assert.AreEqual(expected, actual, 0.0000001, message);
    }

    void _assertThrows(string message, ILuaValue value) {
      bool throws = false;
      try {
        value.Invoke(LuaNil.Nil, false, _lua.Environment.Runtime.CreateMultiValue());
      } catch (Exception) {
        throws = true;
      }
      Assert.IsTrue(throws, "Should throw exception:" + message);
    }
  }
}
