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
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;
using NUnit.Framework;

namespace UnitTests.Runtime {
  [TestFixture]
  class DelegateBuilderTest {
    class MockFunction : LuaFunction {
      public ILuaMultiValue LastCall = null;
      public ILuaMultiValue Return = new LuaMultiValue();

      public MockFunction() : base("") { }

      public override ILuaMultiValue Invoke(ILuaValue self, bool memberCall, ILuaMultiValue args) {
        Assert.IsNull(LastCall);
        Assert.IsNotNull(args);
        LastCall = args;
        return Return;
      }
    }

    delegate void ByRefIntType(ref int x);
    delegate void ByRefStringType(ref string x);
    delegate void ByOutIntType(out int x);
    delegate void ByOutStringType(out string x);

    [Test]
    public void BasicFlow() {
      var func = new MockFunction();
      var builder = new DelegateBuilder();

      var act = builder.CreateDelegate<Action>(func);
      Assert.IsNotNull(act);
      act();
      Assert.IsNotNull(func.LastCall);
      Assert.AreEqual(0, func.LastCall.Count);
    }

    [Test]
    public void Arguments() {
      var func = new MockFunction();
      var builder = new DelegateBuilder();

      var act = builder.CreateDelegate<Action<int, string>>(func);
      Assert.IsNotNull(act);
      act(3, "foo");
      Assert.IsNotNull(func.LastCall);
      Assert.AreEqual(2, func.LastCall.Count);
      Assert.AreEqual(LuaNumber.Create(3), func.LastCall[0]);
      Assert.AreEqual(new LuaString("foo"), func.LastCall[1]);
    }

    [Test]
    public void ReturnValue() {
      var func = new MockFunction();
      var builder = new DelegateBuilder();
      func.Return = new LuaMultiValue(new LuaString("foo"));

      var act = builder.CreateDelegate<Func<string>>(func);
      Assert.IsNotNull(act);
      string ret = act();
      Assert.AreEqual("foo", ret);
    }

    [Test]
    public void ReturnValue_Error() {
      var func = new MockFunction();
      var builder = new DelegateBuilder();
      func.Return = new LuaMultiValue(new LuaString("foo"));

      var act = builder.CreateDelegate<Func<int>>(func);
      Assert.IsNotNull(act);
      Assert.Throws<InvalidCastException>(() => act());
    }

    [Test]
    public void ByRefInt() {
      var func = new MockFunction();
      var builder = new DelegateBuilder();

      var act = builder.CreateDelegate<ByRefIntType>(func);
      Assert.IsNotNull(act);
      int x = 0;
      act(ref x);
    }

    [Test]
    public void ByRefString() {
      var func = new MockFunction();
      var builder = new DelegateBuilder();

      var act = builder.CreateDelegate<ByRefStringType>(func);
      Assert.IsNotNull(act);
      string x = "";
      act(ref x);
    }

    [Test]
    public void ByOutInt() {
      var func = new MockFunction();
      var builder = new DelegateBuilder();

      var act = builder.CreateDelegate<ByOutIntType>(func);
      Assert.IsNotNull(act);
      act(out int x);
      Assert.AreEqual(0, x);
    }

    [Test]
    public void ByOutString() {
      var func = new MockFunction();
      var builder = new DelegateBuilder();

      var act = builder.CreateDelegate<ByOutStringType>(func);
      Assert.IsNotNull(act);
      act(out string x);
      Assert.AreEqual(null, x);
    }
  }
}
