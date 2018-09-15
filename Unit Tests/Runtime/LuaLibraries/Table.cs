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

using ModMaker.Lua;
using ModMaker.Lua.Runtime;
using NUnit.Framework;
using System;

namespace UnitTests.Runtime.LuaLibraries
{
    [TestFixture]
    public class Table : LibraryTestBase
    {
        #region concat
        [Test]
        public void concat()
        {
            Lua.DoText(@"
assertEquals('1c3',   table.concat({1,'c',3}),              'concat: normal')
assertEquals('1c',    table.concat({1,'c',nil,3}),          'concat: with nil')
assertEquals('1,c,3', table.concat({1,'c',3}, ','),         'concat: with sep')
assertEquals('c,3,4', table.concat({1,'c',3,4}, ',', 2),    'concat: with start')
assertEquals('c,3',   table.concat({1,'c',3,4}, ',', 2, 3), 'concat: with start & end')
");
        }

        [Test]
        public void concat_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.Table, "table.concat({0})");
            RunInvalidTypeTests(LuaValueType.String, "table.concat({{1,2,3}}, {0})",
                                allowNil: true);
            RunInvalidTypeTests(LuaValueType.Number, "table.concat({{1,2,3}}, '1', {0})",
                                allowNil: true);
            RunInvalidTypeTests(LuaValueType.Number, "table.concat({{1,2,3}}, 'cat', 2, {0})",
                                allowNil: true);
        }

        [Test]
        public void concat_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"table.concat()");
            });
        }
        #endregion

        #region insert
        [Test]
        public void insert()
        {
            Lua.DoText(@"
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
");
        }

        [Test]
        public void insert_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.Table, "table.insert({0}, 'c')");
            RunInvalidTypeTests(LuaValueType.Number, "table.insert({{1,2,3}}, {0}, 'c')");
        }

        [Test]
        public void insert_InvalidArg()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"table.insert({1,2}, -3, 'cat')");
            });
        }

        [Test]
        public void insert_InvalidArg2()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"table.insert({1,2}, 10, 'cat')");
            });
        }

        [Test]
        public void insert_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"table.insert()");
            });
        }

        [Test]
        public void insert_NotEnoughArgs2()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"table.insert({1,2,3})");
            });
        }
        #endregion

        #region pack
        [Test]
        public void pack()
        {
            Lua.DoText(@"
local t = table.pack(1, nil, 'cat')
assertEquals(3,     t.n,  'pack: normal length')
assertEquals(1,     t[1], 'pack: normal values(1)')
assertEquals(nil,   t[2], 'pack: normal values(2)')
assertEquals('cat', t[3], 'pack: normal values(3)')

t = table.pack()
assertEquals(0, #t, 'pack: empty length')
assertEquals(0, t.n, 'pack: empty n')
");
        }
        #endregion

        #region remove
        [Test]
        public void remove()
        {
            Lua.DoText(@"
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
");
        }

        [Test]
        public void remove_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.Table, "table.remove({0}, 0)");
            RunInvalidTypeTests(LuaValueType.Number, "table.remove({{0, 1}}, {0})",
                                allowNil: true);
        }

        [Test]
        public void remove_InvalidArg()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"table.remove({1,2}, -3)");
            });
        }

        [Test]
        public void remove_InvalidArg2()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"table.remove({1,2}, 10)");
            });
        }

        [Test]
        public void remove_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"table.remove()");
            });
        }
        #endregion

        #region sort
        [Test]
        public void sort()
        {
            Lua.DoText(@"
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
");
        }

        [Test]
        public void sort_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.Table, "table.sort({0}, 0)");
            RunInvalidTypeTests(LuaValueType.Function, "table.sort({{0, 1}}, {0})",
                                allowNil: true);
        }

        [Test]
        public void sort_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"table.sort()");
            });
        }
        #endregion

        #region unpack
        [Test]
        public void unpack()
        {
            Lua.DoText(@"
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
");
        }

        [Test]
        public void unpack_InvalidTypes()
        {
            RunInvalidTypeTests(LuaValueType.Table, "table.unpack({0})");
            RunInvalidTypeTests(
                    LuaValueType.Number, "table.unpack({{1,2,3}}, {0})", allowNil: true);
            RunInvalidTypeTests(
                    LuaValueType.Number, "table.unpack({{1,2,3}}, 2, {0})", allowNil: true);
        }

        [Test]
        public void unpack_NotEnoughArgs()
        {
            Assert.Throws<ArgumentException>(delegate {
                Lua.DoText(@"table.unpack()");
            });
        }
        #endregion
    }
}