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

do
  testCase('concat', function()
    assertEquals('1c3',   table.concat({1,'c',3}),              'concat: normal')
    assertEquals('1c',    table.concat({1,'c',nil,3}),          'concat: with nil')
    assertEquals('1,c,3', table.concat({1,'c',3}, ','),         'concat: with sep')
    assertEquals('c,3,4', table.concat({1,'c',3,4}, ',', 2),    'concat: with start')
    assertEquals('c,3',   table.concat({1,'c',3,4}, ',', 2, 3), 'concat: with start & end')
  end)

  testCase('concat_InvalidTypes', function()
    invalidTypeTest('concat 1', function(a) table.concat(a) end, 'table')
    invalidTypeTest('concat 2', function(a) table.concat({1}, a) end, 'string', 'nil')
    invalidTypeTest('concat 3', function(a) table.concat({1}, 'a', a) end, 'number', 'nil')
    invalidTypeTest('concat 4', function(a) table.concat({1}, 'a', 2, a) end, 'number', 'nil')
  end)

  testCase('concat_NotEnoughArgs', function()
    assertThrowsArg('concat', function() table.concat() end)
  end)
end

do
  testCase('insert', function()
    local t = {1,2,3}
    assertEquals(3,  #t,   'insert: start length')

    table.insert(t, 4)
    assertEquals(4,  #t,   'insert: add length')
    assertEquals(4,  t[4], 'insert: add element')

    table.insert(t, 2, -1)
    assertEquals(5,  #t,   'insert: insert length')
    assertEquals(1,  t[1], 'insert: insert element(1)')
    assertEquals(-1, t[2], 'insert: insert element(2)')
    assertEquals(2,  t[3], 'insert: insert element(3)')
  end)

  testCase('insert_InvalidTypes', function()
    invalidTypeTest('insert 1', function(a) table.insert(a, 'a') end, 'table')
    invalidTypeTest('insert 2', function(a) table.insert({1}, a, 'a') end, 'number')
  end)

  testCase('insert_NotEnoughArgs', function()
    assertThrowsArg('insert', function() table.insert() end)
  end)

  testCase('insert_NotEnoughArgs2', function()
    assertThrowsArg('insert', function() table.insert({1}) end)
  end)

  testCase('insert_InvalidArg', function()
    assertThrowsArg('insert', function() table.insert({1}, -2, 'cat') end)
  end)

  testCase('insert_InvalidArg2', function()
    assertThrowsArg('insert', function() table.insert({1}, 20, 'cat') end)
  end)
end

do
  testCase('pack', function()
    local t = table.pack(1, nil, 'cat')
    assertEquals(3,     t.n,  'pack: normal length')
    assertEquals(1,     t[1], 'pack: normal values(1)')
    assertEquals(nil,   t[2], 'pack: normal values(2)')
    assertEquals('cat', t[3], 'pack: normal values(3)')

    t = table.pack()
    assertEquals(0, #t,  'pack: empty length')
    assertEquals(0, t.n, 'pack: empty n')
  end)
end

do
  testCase('remove', function()
    local t = {1,2,3,4,5}
    local x = table.remove(t)
    assertEquals(4,   #t,   'remove: end length')
    assertEquals(nil, t[5], 'remove: end table')
    assertEquals(5,   x,    'remove: end return')

    x = table.remove(t, 2)
    assertEquals(3,   #t,   'remove: pos length')
    assertEquals(nil, t[4], 'remove: pos table')
    assertEquals(1, t[1],   'remove: pos table shifts(1)')
    assertEquals(3, t[2],   'remove: pos table shifts(2)')
    assertEquals(2,   x,    'remove: pos return')
  end)

  testCase('remove_InvalidTypes', function()
    invalidTypeTest('remove 1', function(a) table.remove(a) end, 'table')
    invalidTypeTest('remove 2', function(a) table.remove({1}, a) end, 'number', 'nil')
  end)

  testCase('remove_NotEnoughArgs', function()
    assertThrowsArg('remove', function() table.remove() end)
  end)

  testCase('remove_InvalidArg', function()
    assertThrowsArg('remove', function() table.remove({1}, -2) end)
  end)

  testCase('remove_InvalidArg2', function()
    assertThrowsArg('remove', function() table.remove({1}, 20) end)
  end)
end

do
  testCase('sort', function()
    local t = {5,2,8,1,6}
    table.sort(t)
    assertEquals(1, t[1], 'sort: normal(1)')
    assertEquals(2, t[2], 'sort: normal(2)')
    assertEquals(5, t[3], 'sort: normal(3)')
    assertEquals(6, t[4], 'sort: normal(4)')
    assertEquals(8, t[5], 'sort: normal(5)')

    local function comp(a, b)
      return b < a
    end
    table.sort(t, comp)
    assertEquals(8, t[1], 'sort: comp(5)')
    assertEquals(6, t[2], 'sort: comp(4)')
    assertEquals(5, t[3], 'sort: comp(3)')
    assertEquals(2, t[4], 'sort: comp(2)')
    assertEquals(1, t[5], 'sort: comp(1)')
  end)

  testCase('sort_InvalidTypes', function()
    invalidTypeTest('sort 1', function(a) table.sort(a) end, 'table')
    invalidTypeTest('sort 2', function(a) table.sort({1}, a) end, 'function', 'nil')
  end)

  testCase('sort_NotEnoughArgs', function()
    assertThrowsArg('sort', function() table.sort() end)
  end)
end

do
  testCase('unpack', function()
    local t = {1,2,3}
    local x, y, z = table.unpack(t)
    assertEquals(1, x, 'unpack: values(1)')
    assertEquals(2, y, 'unpack: values(2)')
    assertEquals(3, z, 'unpack: values(3)')

    x, y, z = table.unpack(t, -1, 1)
    assertEquals(nil, x, 'unpack: neg values(1)')
    assertEquals(nil, y, 'unpack: neg values(2)')
    assertEquals(1,   z, 'unpack: neg values(3)')

    assertEquals(3, select('#', table.unpack(t)),         'unpack: normal')
    assertEquals(2, select('#', table.unpack(t, 2)),      'unpack: start')
    assertEquals(1, select('#', table.unpack(t, 2, 2)),   'unpack: start & end')
    assertEquals(0, select('#', table.unpack(t, 2, 0)),   'unpack: start > end')
    assertEquals(3, select('#', table.unpack(t, -2, 0)),  'unpack: start < 0')
    assertEquals(4, select('#', table.unpack(t, -4, -1)), 'unpack: end < 0')
    assertEquals(6, select('#', table.unpack(t, 1, 6)),   'unpack: end > #t')
    assertEquals(2, select('#', table.unpack(t, 5, 6)),   'unpack: start > #t')
  end)

  testCase('unpack_InvalidTypes', function()
    invalidTypeTest('unpack 1', function(a) table.unpack(a) end, 'table')
    invalidTypeTest('unpack 2', function(a) table.unpack({1}, a) end, 'number', 'nil')
    invalidTypeTest('unpack 3', function(a) table.unpack({1}, 1, a) end, 'number', 'nil')
  end)

  testCase('unpack_NotEnoughArgs', function()
    assertThrowsArg('unpack', function() table.unpack() end)
  end)
end