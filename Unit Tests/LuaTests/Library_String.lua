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
  testCase('byte', function()
    -- These use UTF-16 code points.
    assertEquals(0x57,   string.byte('CatWow', 4), 'byte: normal')
    assertEquals(6,      select('#', string.byte('Lorem Ipsum', 5, 10)),
                 'byte: returns multiple values')
    assertEquals(0x20ac, string.byte('€'), 'byte: handles UTF-16')

    local a, b = string.byte('𐐷', 1, 2)
    assertEquals(0xd801, a, 'byte: handles surrogate pairs(1)')
    assertEquals(0xdc37, b, 'byte: handles surrogate pairs(2)')

    assertEquals(0x44, string.byte('ABCDEF', -3),                 'byte: negative start')
    assertEquals(3,    select('#', string.byte('ABCDEF', 3, -2)), 'byte: negative end')
    assertEquals(0,    select('#', string.byte('ABCDEF', 10)),    'byte: start past end')
    assertEquals(3,    select('#', string.byte('ABCDEF', 4, 10)), 'byte: end past end')
  end)

  testCase('byte_InvalidTypes', function()
    invalidTypeTest('byte 1', function(a) string.byte(a) end,           'string')
    invalidTypeTest('byte 2', function(a) string.byte('cat', a) end,    'number')
    invalidTypeTest('byte 3', function(a) string.byte('cat', 2, a) end, 'number', 'nil')
  end)
end

do
  testCase('char', function()
    assertEquals('ABC', string.char(0x41, 0x42, 0x43), 'char: normal')
    assertEquals('',    string.char(),                 'char: zero args')
    assertEquals('€',   string.char(0x20ac),           'char: handles UTF-16')
    assertEquals('𐐷',   string.char(0xd801, 0xdc37),   'char: handles surrogate pairs split')
    assertEquals('𐐷',   string.char(0x10437),          'char: handles high code points')
  end)

  testCase('char_InvalidTypes', function()
    invalidTypeTest('char 1', function(a) string.char(a) end,          'number')
    invalidTypeTest('char 2', function(a) string.char(2, 3, a, 1) end, 'number')
  end)

  testCase('char_BadArgument', function()
    assertThrowsArg('char', function() string.char(2, -1) end)
  end)
end

do
  testCase('find', function()
    assertEquals(3,     string.find('ABCABCABC', 'CA'),             'find: normal')
    assertEquals(6,     string.find('ABCABCABC', 'CA', 4),          'find: with start')
    assertEquals(0,     select('#', string.find('ABCABCABC', 'x')), 'find: not found')
    assertEquals(0,     select('#', string.find('aXabcd', 'X', 3)), 'find: not found, with start')

    local start, e, c1, c2 = string.find('xABcccDx', 'A(B)(.+)D')
    assertEquals(2,     start, 'find: returns start')
    assertEquals(7,     e,     'find: returns end')
    assertEquals('B',   c1,    'find: returns capture(1)')
    assertEquals('ccc', c2,    'find: returns capture(2)')

    assertEquals(4,     string.find('aXaXX+a', 'XX+', 1, true), 'find: plain')
  end)

  testCase('find_InvalidTypes', function()
    invalidTypeTest('find 1', function(a) string.find(a, 'cat') end, 'string')
    invalidTypeTest('find 2', function(a) string.find('cat', a) end, 'string')
    invalidTypeTest('find 3', function(a) string.find('cat', 'cat', a) end, 'number')
  end)

  testCase('find_NotEnoughArgs', function()
    assertThrowsArg('find', function() string.find() end)
  end)

  testCase('find_NotEnoughArgs2', function()
    assertThrowsArg('find', function() string.find('cat') end)
  end)
end

do
  testCase('format', function()
    assertEquals('AB3CD', string.format('AB{0}CD', 3), 'format: normal')
    assertEquals('ABCD',  string.format('ABCD'),       'format: no format')
  end)

  testCase('format_InvalidTypes', function()
    invalidTypeTest('format', function(a) string.format(a) end, 'string')
  end)

  testCase('format_NoArgs', function()
    assertThrowsArg('format', function() string.format() end)
  end)
end

do
  testCase('gmatch', function()
    local func = string.gmatch('hello world', '\\w+')
    local x, y = func()
    assertEquals('hello', x,                   'gmatch: no captures (1)')
    assertEquals(nil,     y,                   'gmatch: no captures (1e)')
    assertEquals('world', func(),              'gmatch: no captures (2)')
    assertEquals(0,       select('#', func()), 'gmatch: no captures (3)')

    func = string.gmatch('key=value, foo=bar', '(\\w+)=(\\w+)')
    x, y = func()
    assertEquals('key',   x,                   'gmatch: captures (1a)')
    assertEquals('value', y,                   'gmatch: captures (1b)')
    x, y = func()
    assertEquals('foo',   x,                   'gmatch: captures (2a)')
    assertEquals('bar',   y,                   'gmatch: captures (2b)')
    assertEquals(0,       select('#', func()), 'gmatch: captures (3)')
  end)

  testCase('gmatch_InvalidTypes', function()
    invalidTypeTest('gmatch 1', function(a) string.gmatch(a, 'cat') end, 'string')
    invalidTypeTest('gmatch 2', function(a) string.gmatch('cat', a) end, 'string')
  end)

  testCase('gmatch_NotEnoughArgs', function()
    assertThrowsArg('gmatch', function() string.gmatch() end)
  end)

  testCase('gmatch_NotEnoughArgs2', function()
    assertThrowsArg('gmatch', function() string.gmatch('cat') end)
  end)
end

do
  testCase('gsub', function()
    local function repl(s)
      assertEquals('123', s, 'gsub: function arguments')
      return 'abc'
    end

    local t = {abc = 'code', xyz = 'lua'}
    assertEquals('abab xyxy',      string.gsub('ab xy', '\\w+', '%0%0'),         'gsub: normal')
    assertEquals('abab xyxy 12',   string.gsub('ab xy 12', '\\w+', '%0%0', 2),   'gsub: limit')
    assertEquals('abc abc',        string.gsub('123 abc', '(\\w+)', repl, 1),    'gsub: function')
    assertEquals('makes lua code', string.gsub('makes #xyz #abc', '#(\\w+)', t), 'gsub: table')
  end)

  testCase('gsub_InvalidTypes', function()
    invalidTypeTest('gsub 1', function(a) string.gsub(a, 'cat', '') end, 'string')
    invalidTypeTest('gsub 2', function(a) string.gsub('cat', a, '') end, 'string')
  end)

  testCase('gsub_NotEnoughArgs', function()
    assertThrowsArg('gsub', function() string.gsub() end)
  end)

  testCase('gsub_NotEnoughArgs2', function()
    assertThrowsArg('gsub', function() string.gsub('cat') end)
  end)

  testCase('gsub_NotEnoughArgs3', function()
    assertThrowsArg('gsub', function() string.gsub('cat', 'cat') end)
  end)
end

do
  testCase('len', function()
    assertEquals(5, string.len('ab xy'),       'len: normal')
    assertEquals(0, string.len(''),            'len: empty string')
    assertEquals(5, string.len('a\000b\000c'), 'len: embedded nulls')
    assertEquals(5, string.len('𐐷xa€'),        'len: Unicode')
  end)

  testCase('len_InvalidTypes', function()
    invalidTypeTest('len', function(a) string.len(a) end, 'string')
  end)

  testCase('len_NotEnoughArgs', function()
    assertThrowsArg('len', function() string.len() end)
  end)
end

do
  testCase('lower', function()
    assertEquals('abcde',  string.lower('aBCdE'),  'lower: normal')
    assertEquals('',       string.lower(''),       'lower: empty string')
    assertEquals('τυφχψω', string.lower('ΤΥΦΧΨΩ'), 'lower: Unicode')
  end)

  testCase('lower_InvalidTypes', function()
    invalidTypeTest('lower', function(a) string.lower(a) end, 'string')
  end)

  testCase('lower_NotEnoughArgs', function()
    assertThrowsArg('lower', function() string.lower() end)
  end)
end

do
  testCase('match', function()
    local x, y = string.match('hello world', '\\w+')
    assertEquals('hello', x, 'match: no captures (1)')
    assertEquals(nil,     y, 'match: no captures (2)')

    x, y = string.match('hello world', '(\\w+) (\\w+)')
    assertEquals('hello', x, 'match: captures (1)')
    assertEquals('world', y, 'match: captures (2)')

    x, y = string.match('hello world', '(\\w+)', 7)
    assertEquals('world', x,  'match: with start (1)')
    assertEquals(nil,     y,  'match: with start (2)')
  end)

  testCase('match_InvalidTypes', function()
    invalidTypeTest('match 1', function(a) string.match(a, 'cat') end, 'string')
    invalidTypeTest('match 2', function(a) string.match('cat', a) end, 'string')
    invalidTypeTest('match 3', function(a) string.match('cat', 'c', a) end, 'number', 'nil')
  end)

  testCase('match_NotEnoughArgs', function()
    assertThrowsArg('match', function() string.match() end)
  end)

  testCase('match_NotEnoughArgs2', function()
    assertThrowsArg('match', function() string.match('cat') end)
  end)
end

do
  testCase('rep', function()
    assertEquals('XaXaXaXa', string.rep('Xa', 4),      'rep: normal')
    assertEquals('',         string.rep('Xa', 0),      'rep: zero rep')
    assertEquals('',         string.rep('Xa', -3),     'rep: negative rep')
    assertEquals('Xa,Xa,Xa', string.rep('Xa', 3, ','), 'rep: with sep')
  end)

  testCase('rep_InvalidTypes', function()
    invalidTypeTest('rep 1', function(a) string.rep(a, 8) end, 'string')
    invalidTypeTest('rep 2', function(a) string.rep('a', a) end, 'number')
    invalidTypeTest('rep 3', function(a) string.rep('a', 1, a) end, 'string', 'nil')
  end)

  testCase('rep_NotEnoughArgs', function()
    assertThrowsArg('rep', function() string.rep() end)
  end)

  testCase('rep_NotEnoughArgs2', function()
    assertThrowsArg('rep', function() string.rep('a') end)
  end)
end

do
  testCase('reverse', function()
    assertEquals('DCBA', string.reverse('ABCD'), 'reverse: normal')
    assertEquals('',     string.reverse(''),     'reverse: empty string')
    assertEquals('䘣a♸', string.reverse('♸a䘣'), 'reverse: supports Unicode')
    assertEquals('𐐷',    string.reverse('𐐷'),    'reverse: supports UTF-16')
  end)

  testCase('reverse_InvalidTypes', function()
    invalidTypeTest('reverse', function(a) string.reverse(a) end, 'string')
  end)

  testCase('reverse_NotEnoughArgs', function()
    assertThrowsArg('reverse', function() string.reverse() end)
  end)
end

do
  testCase('sub', function()
    assertEquals('CDE', string.sub('ABCDE', 3),     'sub: normal')
    assertEquals('DE',  string.sub('ABCDE', -2),    'sub: negative start')
    assertEquals('BCD', string.sub('ABCDE', 2, 4),  'sub: with end')
    assertEquals('B',   string.sub('ABCDE', 2, 2),  'sub: start == end')
    assertEquals('BCD', string.sub('ABCDE', 2, -2), 'sub: with negative end')
    assertEquals('',    string.sub('ABCDE', 4, 1),  'sub: start > end')
  end)

  testCase('sub_InvalidTypes', function()
    invalidTypeTest('sub 1', function(a) string.sub(a, 3) end, 'string')
    invalidTypeTest('sub 2', function(a) string.sub('a', a) end, 'number')
    invalidTypeTest('sub 3', function(a) string.sub('a', 2, a) end, 'number', 'nil')
  end)

  testCase('sub_NotEnoughArgs', function()
    assertThrowsArg('sub', function() string.sub() end)
  end)

  testCase('sub_NotEnoughArgs2', function()
    assertThrowsArg('sub', function() string.sub('a') end)
  end)
end

do
  testCase('upper', function()
    assertEquals('ABCDE', string.upper('aBCdE'), 'upper: normal')
    assertEquals('',      string.upper(''),      'upper: empty string')
    assertEquals('ΤΥΦΧΨΩ',  string.upper('τυφχψω'),  'upper: Unicode')
  end)

  testCase('upper_InvalidTypes', function()
    invalidTypeTest('upper', function(a) string.upper(a) end, 'string')
  end)

  testCase('upper_NotEnoughArgs', function()
    assertThrowsArg('upper', function() string.upper() end)
  end)
end