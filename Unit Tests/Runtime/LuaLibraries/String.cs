// Copyright 2016 Jacob Trimble
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

namespace UnitTests.Runtime.LuaLibraries
{
    [TestFixture]
    public class String : LibraryTestBase
    {
        #region byte
        [Test]
        public void byte_()
        {
            Lua.DoText(@"
-- These use UTF-16 code points.
-- The escape sequences are done in C#, so they are passed as Unicode characters to Lua.
assertEquals(0x57,   string.byte('CatWow', 4),                       'byte: normal')
assertEquals(6,      select('#', string.byte('Lorem Ipsum', 5, 10)), 'byte: returns multiple values')
assertEquals(0x20ac, string.byte('" + "\u20ac" + @"'),               'byte: handles UTF-16')

local a, b = string.byte('" + "\ud801\udc37" + @"', 1, 2)
assertEquals(0xd801, a,                                              'byte: handles surrogate pairs(1)')
assertEquals(0xdc37, b,                                              'byte: handles surrogate pairs(2)')

assertEquals(0x44,   string.byte('ABCDEF', -3),                      'byte: negative start')
assertEquals(3,      select('#', string.byte('ABCDEF', 3, -2)),      'byte: negative end')
assertEquals(0,      select('#', string.byte('ABCDEF', 10)),         'byte: start past end')
assertEquals(3,      select('#', string.byte('ABCDEF', 4, 10)),      'byte: end past end')
");
        }

        [Test]
        public void byte_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.byte({0})");
            RunInvalidTypeTests(LuaValueType.Number, "string.byte('cat', {0})");
            RunInvalidTypeTests(LuaValueType.Number, "string.byte('cat', 3, {0})", allowNil: true);
        }
        #endregion

        #region char
        [Test]
        public void char_()
        {
            Lua.DoText(@"
-- These use UTF-16 code points.
-- The escape sequences are done in C#, so they are passed as Unicode characters to Lua.
assertEquals('ABC',                       string.char(0x41, 0x42, 0x43), 'char: normal')
assertEquals('',                          string.char(),               'char: zero args')
assertEquals('" + "\uac20" + @"',         string.char(0xac20),         'char: handles UTF-16')
assertEquals('" + "\ud801\udc37" + @"',   string.char(0xd801, 0xdc37), 'char: handles surrotgate pairs split')
assertEquals('" + "\ud801\udc37" + @"',   string.char(0x10437),        'char: handles high code points')
");
        }

        [Test]
        public void char_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.Number, "string.char({0})");
            RunInvalidTypeTests(LuaValueType.Number, "string.char(3, 4, {0}, 1)");
        }

        [Test]
        public void char_BadArgument()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.char(2, -1)");
            });
        }
        #endregion

        #region find
        [Test]
        public void find()
        {
            Lua.DoText(@"
assertEquals(3,     string.find('ABCABCABC', 'CA'),             'find: normal')
assertEquals(6,     string.find('ABCABCABC', 'CA', 4),          'find: with start')
assertEquals(0,     select('#', string.find('ABCABCABC', 'x')), 'find: not found')
assertEquals(0,     select('#', string.find('aXabcd', 'X', 3)), 'find: not found, with start')

local start, end, c1, c2 = string.find('xABcccDx', 'A(B)(.+)D')
assertEquals(2,     start,                                      'find: returns start')
assertEquals(7,     end,                                        'find: returns end')
assertEquals('B',   c1,                                         'find: returns capture(1)')
assertEquals('ccc', c2,                                         'find: returns capture(2)')

assertEquals(4,     string.find('aXaXX+a', 'XX+', 1, true),     'find: plain')
");
        }

        [Test]
        public void find_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.find({0}, 'cat')");
            RunInvalidTypeTests(LuaValueType.String, "string.find('cat', {0})");
            RunInvalidTypeTests(LuaValueType.Number, "string.find('cat', 'cat', {0})");
        }

        [Test]
        public void find_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.find()");
            });
        }

        [Test]
        public void find_NotEnoughArgs2()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.find('foobar')");
            });
        }
        #endregion

        #region format
        [Test]
        public void format()
        {
            Lua.DoText(@"
assertEquals('AB3CD', string.format('AB{0}CD', 3), 'format: normal')
assertEquals('ABCD',  string.format('ABCD'),       'format: no format')
");
        }

        [Test]
        public void format_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.format({0})");
        }

        [Test]
        public void format_NoArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
              Lua.DoText(@"string.format()");
            });
        }
        #endregion

        #region gmatch
        [Test]
        public void gmatch()
        {
            Lua.DoText(@"
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
");
        }

        [Test]
        public void gmatch_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.gmatch({0}, 'cat')");
            RunInvalidTypeTests(LuaValueType.String, "string.gmatch('cat', {0})");
        }

        [Test]
        public void gmatch_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.gmatch()");
            });
        }

        [Test]
        public void gmatch_NotEnoughArgs2()
        {
            Assert.Throws<ArgumentException>(delegate {
              Lua.DoText(@"string.gmatch('cat')");
            });
        }
        #endregion

        #region gsub
        [Test]
        public void gsub()
        {
            Lua.DoText(@"
local function repl(s)
  assertEquals('123', s, 'gsub: function arguments')
  return 'abc'
end

local t = {abc = 'code', xyz = 'lua'}
assertEquals('abab xyxy',      string.gsub('ab xy', '\\w+', '%0%0'),         'gsub: normal')
assertEquals('abab xyxy 12',   string.gsub('ab xy 12', '\\w+', '%0%0', 2),   'gsub: limit')
assertEquals('abc abc',        string.gsub('123 abc', '(\\w+)', repl, 1),    'gsub: function')
assertEquals('makes lua code', string.gsub('makes #xyz #abc', '#(\\w+)', t), 'gsub: table')
");
        }

        [Test]
        public void gsub_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.gsub({0}, 'cat', '')");
            RunInvalidTypeTests(LuaValueType.String, "string.gsub('cat', {0}, '')");
        }

        [Test]
        public void gsub_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.gsub()");
            });
        }

        [Test]
        public void gsub_NotEnoughArgs2()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.gsub('cat')");
            });
        }

        [Test]
        public void gsub_NotEnoughArgs3()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.gsub('cat', 'cat')");
            });
        }
        #endregion

        #region len
        [Test]
        public void len()
        {
            Lua.DoText(@"
assertEquals(5, string.len('ab xy'),                            'len: normal')
assertEquals(0, string.len(''),                                 'len: empty string')
assertEquals(5, string.len('a\000b\000c'),                      'len: embedded nulls')
assertEquals(6, string.len('" + "a\u94ac\ud852xa\udf62" + @"'), 'len: Unicode')
");
        }

        [Test]
        public void len_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.len({0})");
        }

        [Test]
        public void len_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.len()");
            });
        }
        #endregion

        #region lower
        [Test]
        public void lower()
        {
            Lua.DoText(@"
assertEquals('abcde', string.lower('aBCdE'), 'lower: normal')
assertEquals('',      string.lower(''),      'lower: empty string')
assertEquals('τυφχψω',  string.lower('ΤΥΦΧΨΩ'),  'lower: Unicode')
");
        }

        [Test]
        public void lower_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.lower({0})");
        }

        [Test]
        public void lower_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.lower()");
            });
        }
        #endregion

        #region match
        [Test]
        public void match()
        {
            Lua.DoText(@"
local x, y = string.match('hello world', '\\w+')
assertEquals('hello', x, 'match: no captures (1)')
assertEquals(nil,     y, 'match: no captures (2)')

x, y = string.match('hello world', '(\\w+) (\\w+)')
assertEquals('hello', x, 'match: captures (1)')
assertEquals('world', y, 'match: captures (2)')

x, y = string.match('hello world', '(\\w+)', 7)
assertEquals('world', x,  'match: with start (1)')
assertEquals(nil,     y,  'match: with start (2)')
");
        }

        [Test]
        public void match_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.match({0}, 'cat')");
            RunInvalidTypeTests(LuaValueType.String, "string.match('cat', {0})");
            RunInvalidTypeTests(LuaValueType.Number, "string.match('cat', 'c', {0})", allowNil:true);
        }

        [Test]
        public void match_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.match()");
            });
        }

        [Test]
        public void match_NotEnoughArgs2()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.match('cat')");
            });
        }
        #endregion

        #region rep
        [Test]
        public void rep()
        {
            Lua.DoText(@"
assertEquals('XaXaXaXa', string.rep('Xa', 4),      'rep: normal')
assertEquals('',         string.rep('Xa', 0),      'rep: zero rep')
assertEquals('',         string.rep('Xa', -3),     'rep: negative rep')
assertEquals('Xa,Xa,Xa', string.rep('Xa', 3, ','), 'rep: with sep')
");
        }

        [Test]
        public void rep_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.rep({0}, 8)");
            RunInvalidTypeTests(LuaValueType.Number, "string.rep('cat', {0})");
            RunInvalidTypeTests(LuaValueType.String, "string.rep('cat', 8, {0})", allowNil: true);
        }

        [Test]
        public void rep_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.rep()");
            });
        }

        [Test]
        public void rep_NotEnoughArgs2()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.rep('cat')");
            });
        }
        #endregion

        #region reverse
        [Test]
        public void reverse()
        {
            Lua.DoText(@"
assertEquals('DCBA', string.reverse('ABCD'), 'reverse: normal')
assertEquals('',     string.reverse(''),     'reverse: empty string')
assertEquals('" + "\u2678a\u4623" + @"', string.reverse('" + "\u4623a\u2678" + @"'), 'reverse: supports Unicode')
assertEquals('" + "\ud801\udc37a" + @"', string.reverse('" + "a\ud801\udc37" + @"'), 'reverse: supports UTF-16')
");
        }

        [Test]
        public void reverse_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.reverse({0})");
        }

        [Test]
        public void reverse_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.reverse()");
            });
        }
        #endregion

        #region sub
        [Test]
        public void sub()
        {
            Lua.DoText(@"
assertEquals('CDE', string.sub('ABCDE', 3),     'sub: normal')
assertEquals('DE',  string.sub('ABCDE', -2),    'sub: negative start')
assertEquals('BCD', string.sub('ABCDE', 2, 4),  'sub: with end')
assertEquals('B',   string.sub('ABCDE', 2, 2),  'sub: start == end')
assertEquals('BCD', string.sub('ABCDE', 2, -2), 'sub: with negative end')
assertEquals('',    string.sub('ABCDE', 4, 1),  'sub: start > end')
");
        }

        [Test]
        public void sub_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.sub({0}, 3)");
            RunInvalidTypeTests(LuaValueType.Number, "string.sub('cat', {0})");
            RunInvalidTypeTests(LuaValueType.Number, "string.sub('cat', 2, {0})", allowNil: true);
        }

        [Test]
        public void sub_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.sub()");
            });
        }

        [Test]
        public void sub_NotEnoughArgs2()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.sub('cat')");
            });
        }
        #endregion

        #region upper
        [Test]
        public void upper()
        {
            Lua.DoText(@"
assertEquals('ABCDE', string.upper('aBCdE'), 'upper: normal')
assertEquals('',      string.upper(''),      'upper: empty string')
assertEquals('ΤΥΦΧΨΩ',  string.upper('τυφχψω'),  'upper: Unicode')
");
        }

        [Test]
        public void upper_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.String, "string.upper({0})");
        }

        [Test]
        public void upper_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"string.upper()");
            });
        }
        #endregion
    }
}
