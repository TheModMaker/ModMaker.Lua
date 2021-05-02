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
using NUnit.Framework;

namespace UnitTests.Runtime.LuaLibraries {
  [TestFixture]
  public class Standard : LibraryTestBase {
    class Example {
      public int Call(double _) {
        return 1;
      }
      public int Call(long _) {
        return 2;
      }
    }

    int _foo(double x) { return 1; }
    int _foo(long x) { return 2; }

    public override void SetUp() {
      base.SetUp();
      _lua.Register(typeof(Example));
      _lua.Register((Func<double, int>)_foo, "foo");
      _lua.Register((Func<long, int>)_foo, "foo");
    }

    [Test]
    public void Overload() {
      _lua.DoText(@"
        assertEquals(1, foo(0),              'overload: normal')
        assertEquals(1, overload(foo, 0)(0), 'overload: first')
        assertEquals(2, overload(foo, 1)(0), 'overload: second')

        local obj = Example()
        assertEquals(1, obj.Call(0),              'overload: normal')
        assertEquals(1, overload(obj.Call, 0)(0), 'overload: member first')
        assertEquals(2, overload(obj.Call, 1)(0), 'overload: member second')
      ");
    }

    [Test]
    public void Overload_OutOfRange() {
      Assert.Throws<ArgumentException>(() => _lua.DoText(@"overload(foo)"));
      Assert.Throws<ArgumentException>(() => _lua.DoText(@"overload(foo, -1)"));
      Assert.Throws<ArgumentException>(() => _lua.DoText(@"overload(foo, 3)"));
      Assert.Throws<ArgumentException>(() => _lua.DoText(@"overload(Example().Call, -1)"));
      Assert.Throws<ArgumentException>(() => _lua.DoText(@"overload(Example().Call, 3)"));
    }
  }
}
