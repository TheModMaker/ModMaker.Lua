-- Copyright 2022 Jacob Trimble
--
-- Licensed under the Apache License, Version 2.0 (the "License");
-- you may not use this file except in compliance with the License.
-- You may obtain a copy of the License at
--
--     http://www.apache.org/licenses/LICENSE-2.0
--
-- Unless required by applicable law or agreed to in writing, software
-- distributed under the License is distributed on an "AS IS" BASIS,
-- WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
-- See the License for the specific language governing permissions and
-- limitations under the License.

testCase('Create', function()
  local function f() end
  local a = coroutine.create(f)
  local b = coroutine.create(f)
  assertEquals('thread', type(a), 'create: 1')
  assertEquals('thread', type(b), 'create: 2')
  assertFalse(a == b,  'create: 3')
end)

testCase('Create_IsOpaque', function()
  local function f() end
  local co = coroutine.create(f)
  assertThrows('opaque 1', function() local x = co.foo end)
  assertThrows('opaque 2', function() local x = co.Resume end)
  assertThrows('opaque 3', function() co.ToString() end)
  assertThrows('opaque 4', function() local x = co['Resume'] end)
end)

testCase('Create_InvalidTypes', function()
  invalidTypeTest('create', function(a) coroutine.create(a) end, 'function')
end)

testCase('Resume_BasicFlow', function()
  local function f(x)
    assertEquals(5, x, 'f: args')
    return 10
  end
  local co = coroutine.create(f)
  local a, b = coroutine.resume(co, 5)
  assertEquals(true, a, 'main: ret 1')
  assertEquals(10, b,   'main: ret 2')

  a, b = coroutine.resume(co, 15)
  assertEquals(false, a, 'main: ret 3')
  assertEquals('cannot resume dead coroutine', b, 'main: ret 4')
end)

testCase('Resume_Yield', function()
  local function f()
    local x = coroutine.yield(1, 2)
    assertEquals(7, x, 'f: args 1')
    local y = coroutine.yield(3, 4)
    assertEquals(8, y, 'f: args 2')
  end
  local co = coroutine.create(f)
  local a, b, c = coroutine.resume(co, 5)
  assertEquals(true, a, 'main: ret 1')
  assertEquals(1,    b, 'main: ret 2')
  assertEquals(2,    c, 'main: ret 3')

  a, b, c = coroutine.resume(co, 7)
  assertEquals(true, a, 'main: ret 4')
  assertEquals(3,    b, 'main: ret 5')
  assertEquals(4,    c, 'main: ret 6')

  assertEquals(true, coroutine.resume(co, 8), 'main: ret 7')
end)

testCase('Resume_Errors', function()
  local function f(x)
    error('foo')
  end
  local co = coroutine.create(f)
  local a, b = coroutine.resume(co, 5)
  assertEquals(false, a, 'main: ret 1')
  assertEquals('foo', b, 'main: ret 2')
end)

testCase('Resume_InvalidTypes', function()
  invalidTypeTest('resume', function(a) coroutine.resume(a) end, 'thread')
end)

testCase('Yield_Main', function()
  assertThrows('yield', function() coroutine.yield(1) end)
end)

testCase('Yield_AbortPcall', function()
  local function f(x)
    pcall(function()
      coroutine.yield()
    end)
    fail('Should not return from yield here')
  end
  local co = coroutine.create(f)
  coroutine.resume(co)
end)

testCase('NoArgs', function()
  local function f(a)
    -- Despite no args, it will fill with 'nil'
    assertEquals(nil, a, 'f: args 1')
    local x = coroutine.yield()
    assertEquals(nil, x, 'f: args 2')
  end
  local co = coroutine.create(f)
  local x, y = coroutine.resume(co)
  assertEquals(true, x, 'main: args 1')
  assertEquals(nil,  y, 'main: args 1')

  coroutine.resume(co)
end)

testCase('Running', function()
  local co
  local function f(a)
    local a, b = coroutine.running()
    assertEquals('thread', type(a), 'f: running 1')
    assertEquals(co,       a,       'f: running 2')
    assertEquals(false,    b,       'f: running 3')
  end
  co = coroutine.create(f)
  assertEquals(true, coroutine.resume(co), 'main: running 1')

  local x, y = coroutine.running()
  assertEquals('thread', type(x), 'main: running 2')
  assertEquals(true,     y,       'main: running 3')
end)

testCase('Status', function()
  local main = coroutine.running()
  local co
  local function f(a)
    assertEquals('running', coroutine.status(co),   'f: status 1')
    assertEquals('normal',  coroutine.status(main), 'f: status 2')

    co2 = coroutine.create(function()
      assertEquals('normal',  coroutine.status(co),  'g: status 1')
      assertEquals('running', coroutine.status(co2), 'g: status 2')
      error('foo')
    end)
    assertEquals(false,  coroutine.resume(co2), 'f: status 3')
    assertEquals('dead', coroutine.status(co2), 'f: status 4')

    coroutine.yield()
  end
  co = coroutine.create(f)
  assertEquals('running',  coroutine.status(main), 'main: status 1')
  assertEquals('suspended',coroutine.status(co),   'main: status 2')

  local x, y = coroutine.resume(co)
  assertEquals(nil,         y,                    'main: status 3')
  assertEquals('suspended', coroutine.status(co), 'main: status 4')

  assertEquals(true,   coroutine.resume(co), 'main: status 5')
  assertEquals('dead', coroutine.status(co), 'main: status 6')
end)

testCase('Status_InvalidArgs', function()
  invalidTypeTest('status', function(a) coroutine.status(a) end, 'thread')
end)

testCase('Wrap', function()
  local function f(a)
    assertEquals(1, a, 'f: wrap 1')
    local x = coroutine.yield(10)
    assertEquals(2, x, 'f: wrap 2')
    error('foo')
  end

  local func = coroutine.wrap(f)
  local x = func(1)
  assertEquals(10, x, 'main: wrap 1')
  assertThrows('main: wrap 2', function() func(2) end)
end)

testCase('Wrap_InvalidArgs', function()
  invalidTypeTest('wrap', function(a) coroutine.wrap(a) end, 'function')
end)