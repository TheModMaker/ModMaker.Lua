// Copyright 2021 Jacob Trimble
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
using System.Reflection;
using ModMaker.Lua.Runtime.LuaValues;
using NUnit.Framework;

namespace UnitTests {
  [TestFixture]
  class MiscLuaTest : TestBase {
    [Test]
    public void ReflectionIsOpaque() {
      Func<Type> getType = () => typeof(string);
      Func<MethodInfo?> getMethod = () => typeof(string).GetMethod(nameof(string.Clone));
      Func<object> getObj = () => new object();
      _lua.Register(getType, "getType");
      _lua.Register(getMethod, "getMethod");
      _lua.Register(getObj, "getObj");

      _lua.DoText(@"
        assertEquals('', getType().Empty, 'using Type gets static members')

        assertEquals(nil, getType().Name, ""members on Type aren't visible"")
        assertEquals(nil, getMethod().Name, ""members on MethodInfo aren't visible"")
        assertEquals(nil, getMethod().GetParameters, ""members on MethodInfo aren't visible 2"")
        assertEquals(nil, getMethod().GetType, ""GetType isn't visible"")
        assertEquals(nil, getObj().GetType, ""GetType isn't visible 2"")
      ");

      Assert.Throws<InvalidOperationException>(() => _lua.DoText("getType().Name = ''"));
    }

    [Test]
    public void CanUseFunctions() {
      Action<Action> foo = (act) => act();
      _lua.Register(foo, "foo");

      _lua.DoText(@"
        local x = 0
        foo(function() x = 1 end)
        assertEquals(x, 1, 'functions')
      ");
    }

    [Test]
    public void CanAcceptMultiValue() {
      Action<LuaMultiValue> foo = (vals) => { };
      _lua.Register(foo, "foo");

      _lua.DoText(@"
        foo()
        foo(1, 2, 3)
      ");
    }
  }
}
