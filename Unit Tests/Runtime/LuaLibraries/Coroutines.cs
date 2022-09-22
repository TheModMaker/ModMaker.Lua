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
using NUnit.Framework;

namespace UnitTests.Runtime.LuaLibraries {
  [TestFixture]
  public class Coroutines : LibraryTestBase {
    [Test]
    public void Create() {
      _lua.DoText(@"
        local function f() end
        local a = coroutine.create(f)
        local b = coroutine.create(f)
        assertEquals(type(a), 'thread', 'create: 1')
        assertEquals(type(a), 'thread', 'create: 2')
        assertFalse(a == b,  'create: 3')
      ");
    }

    [Test]
    public void IsOpaque() {
      _lua.DoText(@"
        local function f() end
        local co = coroutine.create(f)
        assertThrows('opaque 1', function() local x = co.foo end)
        assertThrows('opaque 2', function() local x = co.Resume end)
        assertThrows('opaque 3', function() co.ToString() end)
        assertThrows('opaque 4', function() local x = co['Resume'] end)
      ");
    }

    [Test]
    public void Create_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Function, "coroutine.create({0})");
    }

    [Test]
    public void Resume_BasicFlow() {
      _lua.DoText(@"
        local function f(x)
          assertEquals(x, 5, 'f: args')
          return 10
        end
        local co = coroutine.create(f)
        local a, b = coroutine.resume(co, 5)
        assertEquals(a, true, 'main: ret 1')
        assertEquals(b, 10,   'main: ret 2')

        a, b = coroutine.resume(co, 15)
        assertEquals(a, false, 'main: ret 3')
        assertEquals(b, 'cannot resume dead coroutine', 'main: ret 4')
      ");
    }

    [Test]
    public void Resume_Yield() {
      _lua.DoText(@"
        local function f()
          local x = coroutine.yield(1, 2)
          assertEquals(x, 7, 'f: args 1')
          local y = coroutine.yield(3, 4)
          assertEquals(y, 8, 'f: args 2')
        end
        local co = coroutine.create(f)
        local a, b, c = coroutine.resume(co, 5)
        assertEquals(a, true, 'main: ret 1')
        assertEquals(b, 1,    'main: ret 2')
        assertEquals(c, 2,    'main: ret 3')

        a, b, c = coroutine.resume(co, 7)
        assertEquals(a, true, 'main: ret 4')
        assertEquals(b, 3,    'main: ret 5')
        assertEquals(c, 4,    'main: ret 6')

        assertEquals(coroutine.resume(co, 8), true, 'main: ret 7')
      ");
    }

    [Test]
    public void Resume_Errors() {
      _lua.DoText(@"
        local function f(x)
          error('foo')
        end
        local co = coroutine.create(f)
        local a, b = coroutine.resume(co, 5)
        assertEquals(a, false, 'main: ret 1')
        assertEquals(b, 'foo', 'main: ret 2')
      ");
    }

    [Test]
    public void Resume_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Thread, "coroutine.resume({0})");
    }

    [Test]
    public void Yield_Main() {
      _lua.DoText(@"assertThrows('yield error', function() coroutine.yield(co) end)");
    }

    [Test]
    public void Yield_AbortPcall() {
      _lua.DoText(@"
        local function f(x)
          pcall(function()
            coroutine.yield()
          end)
          fail('Should not return from yield here')
        end
        local co = coroutine.create(f)
        coroutine.resume(co)
      ");
      GC.Collect();
    }

    [Test]
    public void Yield_InvalidTypes() {
      _runInvalidTypeTests(
          LuaValueType.Thread,
          "coroutine.wrap(coroutine.create(function() coroutine.yield({0}) end))()");
    }

    [Test]
    public void NoArgs() {
      _lua.DoText(@"
        local function f(a)
          -- Despite no args, it will fill with 'nil'
          assertEquals(a, nil, 'f: args 1')
          local x = coroutine.yield()
          assertEquals(x, nil, 'f: args 2')
        end
        local co = coroutine.create(f)
        local x, y = coroutine.resume(co)
        assertEquals(x, true, 'main: args 1')
        assertEquals(y, nil,  'main: args 1')

        coroutine.resume(co)
      ");
    }

    [Test]
    public void Running() {
      _lua.DoText(@"
        local co
        local function f(a)
          local a, b = coroutine.running()
          assertEquals(type(a), 'thread', 'f: running 1')
          assertEquals(co,      a,        'f: running 2')
          assertEquals(b,       false,    'f: running 3')
        end
        co = coroutine.create(f)
        assertEquals(coroutine.resume(co), true, 'main: running 1')

        local x, y = coroutine.running()
        assertEquals(type(x), 'thread', 'main: running 2')
        assertEquals(y,       true,     'main: running 3')
      ");
    }

    [Test]
    public void Status() {
      _lua.DoText(@"
        local main = coroutine.running()
        local co
        local function f(a)
          assertEquals(coroutine.status(co),   'running', 'f: status 1')
          assertEquals(coroutine.status(main), 'normal',  'f: status 2')

          co2 = coroutine.create(function()
            assertEquals(coroutine.status(co),  'normal',  'g: status 1')
            assertEquals(coroutine.status(co2), 'running', 'g: status 2')
            error('foo')
          end)
          assertEquals(coroutine.resume(co2), false,   'f: status 3')
          assertEquals(coroutine.status(co2), 'dead', 'f: status 4')

          coroutine.yield()
        end
        co = coroutine.create(f)
        assertEquals(coroutine.status(main), 'running',   'main: status 1')
        assertEquals(coroutine.status(co),   'suspended', 'main: status 2')

        local x, y = coroutine.resume(co)
        assertEquals(y,   nil,        'main: status 3')
        assertEquals(coroutine.status(co),   'suspended', 'main: status 4')

        assertEquals(coroutine.resume(co),   true,        'main: status 5')
        assertEquals(coroutine.status(co),   'dead',      'main: status 6')
      ");
    }

    [Test]
    public void Wrap() {
      _lua.DoText(@"
        local function f(a)
          assertEquals(a, 1, 'f: wrap 1')
          local x = coroutine.yield(10)
          assertEquals(x, 2, 'f: wrap 2')
          error('foo')
        end

        local func = coroutine.wrap(f)
        local x = func(1)
        assertEquals(x, 10, 'main: wrap 1')
        assertThrows('main: wrap 2', function() func(2) end)
      ");
    }
  }
}