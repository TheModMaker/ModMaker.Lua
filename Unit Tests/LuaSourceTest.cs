// Copyright 2022 Jacob Trimble
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
using System.Reflection;
using System.Text;
using ModMaker.Lua;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;
using NUnit.Framework;

namespace UnitTests {
  [TestFixture]
  class LuaSourceTest {
    static readonly Lua _lua52;
    static readonly List<Tuple<string, LuaFunction, string>> _tests =
        new List<Tuple<string, LuaFunction, string>>();
    static string _prefix = "";

    class OverloadType {
      public int Call(double _) {
        return 1;
      }
      public int Call(long _) {
        return 2;
      }
    }

    static LuaSourceTest() {
      _lua52 = new Lua();

      foreach (var lua in new[]  { _lua52 }) {
        lua.Register((Action<string, LuaFunction, string>)_testCase, "testCase");
        lua.Register((Action<string, LuaFunction, string[]>)_invalidTypeTest, "invalidTypeTest");

        lua.Register((Action<object?, object?, string>)_assertEquals, "assertEquals");
        lua.Register((Action<double, double, string>)_assertEqualsDelta, "assertEqualsDelta");
        lua.Register((Action<bool, string>)_assertTrue, "assertTrue");
        lua.Register((Action<bool, string>)_assertFalse, "assertFalse");
        lua.Register((Action<string, ILuaValue>)_assertThrows, "assertThrows");
        lua.Register((Action<string, ILuaValue>)_assertThrowsArg, "assertThrowsArg");

        Func<Type> getType = () => typeof(string);
        Func<MethodInfo?> getMethod = () => typeof(string).GetMethod(nameof(string.Clone));
        Func<object> getObj = () => new object();
        lua.Register(getType, "getType");
        lua.Register(getMethod, "getMethod");
        lua.Register(getObj, "getObj");
        lua.Register((Func<double, double>)((a) => 1), "overloadGetNumber");
        lua.Register((Func<long, double>)((a) => 2), "overloadGetNumber");
        lua.Register(typeof(OverloadType));
      }
    }

    [Test, TestCaseSource(nameof(_getTestData))]
    public void Lua52(LuaFunction func) {
      func.Invoke(LuaMultiValue.Empty);
    }

    static IEnumerable<TestCaseData> _getTestData() {
      var sources = new[] {
        Tuple.Create("Conformance", TestResources.Conformance),
        Tuple.Create("Library_Bit32", TestResources.Library_Bit32),
        Tuple.Create("Library_Coroutine", TestResources.Library_Coroutine),
        Tuple.Create("Library_Math", TestResources.Library_Math),
        Tuple.Create("Library_Standard", TestResources.Library_Standard),
        Tuple.Create("Library_String", TestResources.Library_String),
        Tuple.Create("Library_Table", TestResources.Library_Table),
        Tuple.Create("Misc", TestResources.Misc),
      };
      foreach (var info in sources) {
        _prefix = info.Item1;
        var code = _lua52.CompileText(Encoding.UTF8.GetString(info.Item2).TrimStart('\xfeff'),
                                      info.Item1 + ".lua");
        code.Invoke(LuaMultiValue.Empty);
      }
      return _tests.Select(t => {
        var ret = new TestCaseData(t.Item2).SetName(t.Item1);
        if (t.Item3 != "")
          ret = ret.Ignore(t.Item3);
        return ret;
      });
    }

    static void _testCase(string name, LuaFunction func, string ignoreMessage = "") {
      _tests.Add(Tuple.Create($"{_prefix}.{name}", func, ignoreMessage));
    }

    static void _invalidTypeTest(string context, LuaFunction func, params string[] types) {
      var typesSet = new HashSet<string>(types);
      var values = new ILuaValue[] {
        LuaNil.Nil,
        LuaNumber.Create(0),
        LuaBoolean.True,
        new LuaString("a"),
        new LuaTable(LuaEnvironment.CurrentEnvironment),
        func,
        LuaCoroutine.Current(LuaEnvironment.CurrentEnvironment),
        new LuaUserData<object>(new object()),
      };
      foreach (var value in values) {
        string type = value.ValueType.ToString().ToLower();
        if (typesSet.Contains(type))
          continue;
        Assert.That(() => func.Invoke(new LuaMultiValue(value)), Throws.ArgumentException,
                    $"{context} {type}");
      }
    }

    static void _assertEquals(object? expected, object? actual, string message) {
      Assert.That(actual, Is.EqualTo(expected), message);
    }

    static void _assertEqualsDelta(double expected, double actual, string message) {
      Assert.That(actual, Is.EqualTo(expected).Within(0.0000001), message);
    }

    static void _assertTrue(bool actual, string message) {
      Assert.That(actual, Is.True, message);
    }

    static void _assertFalse(bool actual, string message) {
      Assert.That(actual, Is.False, message);
    }

    static void _assertThrows(string message, ILuaValue value) {
      Assert.That(() => value.Invoke(LuaMultiValue.Empty), Throws.Exception);
    }

    static void _assertThrowsArg(string message, ILuaValue value) {
      Assert.That(() => value.Invoke(LuaMultiValue.Empty), Throws.ArgumentException);
    }
  }
}
