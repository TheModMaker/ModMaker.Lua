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
using System.Diagnostics.CodeAnalysis;
using ModMaker.Lua.Runtime;
using NUnit.Framework;

namespace UnitTests.Runtime.LuaLibraries {
  [TestFixture]
  [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Names match Lua versions")]
  public class Bit32 : LibraryTestBase {
    #region arshift
    [Test]
    public void arshift() {
      _lua.DoText(@"
-- Positive displacements.
assertEquals(0xf0,       bit32.arshift(0xf00, 4),        'arshift: normal')
assertEquals(0xf0,       bit32.arshift(0xf00, 4, 33),    'arshift: extra args')
assertEquals(0xf0,       bit32.arshift(0xff00000f00, 4), 'arshift: larger than 32-bit')
assertEquals(0xff015600, bit32.arshift(0x80ab0000, 7),   'arshift: high-bit fill')
assertEquals(0xffffff00, bit32.arshift(0x80000000, 23),  'arshift: high-bit fill small value')
assertEquals(0x0,        bit32.arshift(0x724624, 35),    'arshift: all shifted out')
assertEquals(0xffffffff, bit32.arshift(0x824624, 35),    'arshift: all shifted out (negative)')
assertEquals(0xfffffffc, bit32.arshift(-0xf, 2),         'arshift: negative source')

-- Negative displacements.
assertEquals(0xf0,       bit32.arshift(0xf, -4),          'arshift(left): normal')
assertEquals(0xf00000a0, bit32.arshift(0x0f00000a, -4),   'arshift(left): becomes negative')
assertEquals(0xf0,       bit32.arshift(0xff0000000f, -4), 'arshift(left): larger than 32-bit')
assertEquals(0xff000000, bit32.arshift(0x1155ff, -24),    'arshift(left): drop high bits')
assertEquals(0x0,        bit32.arshift(0x924624, -35),    'arshift(left): all shifted out')
assertEquals(0xffffff88, bit32.arshift(-0xf, -3),         'arshift(left): negative source')
");
    }

    [Test]
    public void arshift_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.arshift({0}, 2)");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.arshift(2, {0})");
    }

    [Test]
    public void arshift_NotEnoughArgs() {
      Assert.Throws<ArgumentException>(delegate {
        _lua.DoText(@"bit32.arshift(0xf00)");
      });
    }
    #endregion

    #region band
    [Test]
    public void band() {
      _lua.DoText(@"
assertEquals(0xea,       bit32.band(0xff, 0xea),                 'band: normal')
assertEquals(0xffffffff, bit32.band(),                           'band: zero arguments')
assertEquals(0xea,       bit32.band(0xea),                       'band: one argument')
assertEquals(0x22,       bit32.band(0xff, 0xea, 0x7f, 0xa3),     'band: more than two')
assertEquals(0x00,       bit32.band(0x42, 0xea, 0x7a, 0xa1),     'band: clears out')
assertEquals(0xaa000000, bit32.band(0xfa0000aa, 0xaf00aa00),     'band: large number')
assertEquals(0x0f,       bit32.band(0xff0000000f, 0xff0000000f), 'band: larger than 32-bits')
");
    }

    [Test]
    public void band_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.band({0})");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.band(23, 4, {0}, 5)");
    }
    #endregion

    #region bnot
    [Test]
    public void bnot() {
      _lua.DoText(@"
assertEquals(0xff005533, bit32.bnot(0x00ffaacc),        'bnot: normal')
assertEquals(0xff005533, bit32.bnot(0x00ffaacc, 'cat'), 'bnot: extra args')
assertEquals(0x00ffaa66, bit32.bnot(0xff005599),        'bnot: high-bit set')
assertEquals(11,         bit32.bnot(-12),               'bnot: negative')
assertEquals(0xffffffff, bit32.bnot(0),                 'bnot: zero')
assertEquals(0xffffefdb, bit32.bnot(0x4500001024),      'bnot: larger than 32-bits')
");
    }

    [Test]
    public void bnot_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.bnot({0})");
    }

    [Test]
    public void bnot_NotEnoughArgs() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.bnot()");
      });
    }
    #endregion

    #region bor
    [Test]
    public void bor() {
      _lua.DoText(@"
assertEquals(0xff,       bit32.bor(0xaa, 0x55),                 'bor: normal')
assertEquals(0x0,        bit32.bor(),                           'bor: zero arguments')
assertEquals(0xea,       bit32.bor(0xea),                       'bor: one argument')
assertEquals(0xab,       bit32.bor(0x01, 0x83, 0x21, 0x2a),     'bor: more than two')
assertEquals(0xff00aaaa, bit32.bor(0xfa0000aa, 0xaf00aa00),     'bor: large number')
assertEquals(0xff,       bit32.bor(0xff000000f0, 0xff0000000f), 'bor: larger than 32-bits')
");
    }

    [Test]
    public void bor_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.bor({0})");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.bor(23, 4, {0}, 5)");
    }
    #endregion

    #region btest
    [Test]
    public void btest() {
      _lua.DoText(@"
assertEquals(false, bit32.btest(0xaa, 0x55),                 'btest: normal')
assertEquals(true,  bit32.btest(),                           'btest: zero arguments')
assertEquals(true,  bit32.btest(0xea),                       'btest: one argument')
assertEquals(false, bit32.btest(0x01, 0x83, 0x21, 0x2a),     'btest: more than two')
assertEquals(true,  bit32.btest(0xfa0000aa, 0xaf00aa00),     'btest: large number')
assertEquals(false, bit32.btest(0xff000000f0, 0xff0000000f), 'btest: larger than 32-bits')
");
    }

    [Test]
    public void btest_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.btest({0})");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.btest(23, 4, {0}, 5)");
    }
    #endregion

    #region bxor
    [Test]
    public void bxor() {
      _lua.DoText(@"
assertEquals(0x82,       bit32.bxor(0x24, 0xa6),                 'bxor: normal')
assertEquals(0x0,        bit32.bxor(),                           'bxor: zero arguments')
assertEquals(0xea,       bit32.bxor(0xea),                       'bxor: one argument')
assertEquals(0x89,       bit32.bxor(0x01, 0x83, 0x21, 0x2a),     'bxor: more than two')
assertEquals(0x5500f848, bit32.bxor(0xfa005a1e, 0xaf00a256),     'bxor: large number')
assertEquals(0xff,       bit32.bxor(0xff000000f0, 0xff0000000f), 'bxor: larger than 32-bits')
");
    }

    [Test]
    public void bxor_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.bxor({0})");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.bxor(23, 4, {0}, 5)");
    }
    #endregion

    #region extract
    [Test]
    public void extract() {
      _lua.DoText(@"
assertEquals(0x3a28,  bit32.extract(0xa74e8a28, 6, 16),       'extract: normal')
assertEquals(0x0,     bit32.extract(0xa74e8a28, 6),           'extract: defaults to 1 width')
assertEquals(0x4fe3a, bit32.extract(0x913f8ea, 2, 21, 'cat'), 'extract: extra args')
assertEquals(0x1e,    bit32.extract(0xff0305d2f0, 3, 6),      'extract: larger than 32-bits')
");
    }

    [Test]
    public void extract_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.extract({0}, 0)");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.extract(4, {0})");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.extract(4, 3, {0})");
    }

    [Test]
    public void extract_TooLargeIndex() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.extract(0x3, 34)");
      });
    }

    [Test]
    public void extract_TooLargeSize() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.extract(0x3, 3, 42)");
      });
    }

    [Test]
    public void extract_TooLargeIndexPlusSize() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.extract(0x3, 21, 12)");
      });
    }

    [Test]
    public void extract_NotEnoughArgs() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.extract()");
      });
    }

    [Test]
    public void extract_NotEnoughArgs2() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.extract(0xf00)");
      });
    }
    #endregion

    #region replace
    [Test]
    public void replace() {
      _lua.DoText(@"
assertEquals(0xa74a4a28, bit32.replace(0xa74e8a28, 0x452, 13, 6),  'replace: normal')
assertEquals(0xa74e8a20, bit32.replace(0xa74e8a28, 6, 3),          'replace: defaults to 1 width')
assertEquals(0xffff4c47, bit32.replace(-45625, 34, 5, 4),          'replace: negative source')
assertEquals(0xa74eca,   bit32.replace(0xa74e8a, -42, 5, 4),       'replace: negative repl')
assertEquals(0x918aa,    bit32.replace(0x918ea, 0x2462a, 2, 10, 'cat'), 'replace: extra args')
assertEquals(0xd0f0,     bit32.replace(0xff0000d2f0, 0x23, 6, 4),  'replace: larger than 32-bits')
");
    }

    [Test]
    public void replace_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.replace({0}, 0)");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.replace(4, {0})");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.replace(4, 3, {0})");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.replace(4, 3, 4, {0})");
    }

    [Test]
    public void replace_TooLargeIndex() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.replace(0x3, 0x0, 34)");
      });
    }

    [Test]
    public void replace_TooLargeSize() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.replace(0x3, 0x0, 3, 42)");
      });
    }

    [Test]
    public void replace_TooLargeIndexPlusSize() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.replace(0x3, 0x0, 21, 12)");
      });
    }

    [Test]
    public void replace_NotEnoughArgs() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.replace()");
      });
    }

    [Test]
    public void replace_NotEnoughArgs2() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.replace(0xf00)");
      });
    }

    [Test]
    public void replace_NotEnoughArgs3() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.replace(0xf00, 0x00)");
      });
    }
    #endregion

    #region lrotate
    [Test]
    public void lrotate() {
      _lua.DoText(@"
assertEquals(0xd3a28a29, bit32.lrotate(0xa74e8a28, 6),         'lrotate: normal')
assertEquals(0x4e9d1451, bit32.lrotate(0xa74e8a28, 65),        'lrotate: > 32')
assertEquals(0x4e9d1451, bit32.lrotate(0xa74e8a28, 65, 'cat'), 'lrotate: > 32')
assertEquals(0xc25a789e, bit32.lrotate(0x5a789ec2, -8),        'lrotate: negative')
assertEquals(0x27b0969e, bit32.lrotate(0x5a789ec2, -82),       'lrotate: negative < -32')
assertEquals(0xffff216f, bit32.lrotate(-3562, 4),              'lrotate: negative source')
assertEquals(0xd2f00,    bit32.lrotate(0xff0000d2f0, 4),       'lrotate: larger than 32-bits')
");
    }

    [Test]
    public void lrotate_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.lrotate({0}, 0)");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.lrotate(4, {0})");
    }

    [Test]
    public void lrotate_NotEnoughArgs() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.lrotate()");
      });
    }

    [Test]
    public void lrotate_NotEnoughArgs2() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.lrotate(0xf00)");
      });
    }
    #endregion

    #region lshift
    [Test]
    public void lshift() {
      _lua.DoText(@"
assertEquals(0xd3a28a00, bit32.lshift(0xa74e8a28, 6),         'lshift: normal')
assertEquals(0,          bit32.lshift(0xa74e8a28, 65),        'lshift: > 32')
assertEquals(0,          bit32.lshift(0xa74e8a28, 65, 'cat'), 'lshift: extra args')
assertEquals(0x5a789e,   bit32.lshift(0x5a789ec2, -8),        'lshift: negative')
assertEquals(0,          bit32.lshift(0x5a789ec2, -82),       'lshift: negative < -32')
assertEquals(0xffff2160, bit32.lshift(-3562, 4),              'lshift: negative source')
assertEquals(0xd2f00,    bit32.lshift(0xff0000d2f0, 4),       'lshift: larger than 32-bits')
");
    }

    [Test]
    public void lshift_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.lshift({0}, 0)");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.lshift(4, {0})");
    }

    [Test]
    public void lshift_NotEnoughArgs() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.lshift()");
      });
    }

    [Test]
    public void lshift_NotEnoughArgs2() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.lshift(0xf00)");
      });
    }
    #endregion

    #region rrotate
    [Test]
    public void rrotate() {
      _lua.DoText(@"
assertEquals(0xa29d3a28, bit32.rrotate(0xa74e8a28, 6),         'rrotate: normal')
assertEquals(0x53a74514, bit32.rrotate(0xa74e8a28, 65),        'rrotate: > 32')
assertEquals(0x53a74514, bit32.rrotate(0xa74e8a28, 65, 'cat'), 'rrotate: extra args')
assertEquals(0x789ec25a, bit32.rrotate(0x5a789ec2, -8),        'rrotate: negative')
assertEquals(0x7b0969e2, bit32.rrotate(0x5a789ec2, -82),       'rrotate: negative < -32')
assertEquals(0x6fffff21, bit32.rrotate(-3562, 4),              'rrotate: negative source')
assertEquals(0xd2f,      bit32.rrotate(0xff0000d2f0, 4),       'rrotate: larger than 32-bits')
");
    }

    [Test]
    public void rrotate_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.rrotate({0}, 0)");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.rrotate(4, {0})");
    }

    [Test]
    public void rrotate_NotEnoughArgs() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.rrotate()");
      });
    }

    [Test]
    public void rrotate_NotEnoughArgs2() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.rrotate(0xf00)");
      });
    }
    #endregion

    #region rshift
    [Test]
    public void rshift() {
      _lua.DoText(@"
assertEquals(0x29d3a28,  bit32.rshift(0xa74e8a28, 6),         'rshift: normal')
assertEquals(0,          bit32.rshift(0xa74e8a28, 65),        'rshift: > 32')
assertEquals(0,          bit32.rshift(0xa74e8a28, 65, 'cat'), 'rshift: extra args')
assertEquals(0x789ec200, bit32.rshift(0x5a789ec2, -8),        'rshift: negative')
assertEquals(0,          bit32.rshift(0x5a789ec2, -82),       'rshift: negative < -32')
assertEquals(0xfffff21,  bit32.rshift(-3562, 4),              'rshift: negative source')
assertEquals(0xd2f,      bit32.rshift(0xff0000d2f0, 4),       'rshift: larger than 32-bits')
");
    }

    [Test]
    public void rshift_InvalidTypes() {
      _runInvalidTypeTests(LuaValueType.Number, "bit32.rshift({0}, 0)");
      _runInvalidTypeTests(LuaValueType.Number, "bit32.rshift(4, {0})");
    }

    [Test]
    public void rshift_NotEnoughArgs() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.rshift()");
      });
    }

    [Test]
    public void rshift_NotEnoughArgs2() {
      Assert.Throws<ArgumentException>(() => {
        _lua.DoText(@"bit32.rshift(0xf00)");
      });
    }
    #endregion

    [Ignore("Disabled until coalesce is supported")]
    [Test]
    public void coalesce() {
      _lua.DoText(@"
assertEquals(10,    bit32.arshift('43', 2),           'arshift: coalesce args')
assertEquals(0xaa,  bit32.band('0xfa', 0xaf),         'band: coalesce args')
assertEquals(0xf0,  bit32.bnot('0xffffff0f'),         'bnot: coalesce args')
assertEquals(0xff,  bit32.bor('0xfa', 0xaf),          'bor: coalesce args')
assertEquals(true,  bit32.btest('0xfa', 0x12),        'btest: coalesce args')
assertEquals(0x55,  bit32.bxor('0xfa', 0xaf),         'bxor: coalesce args')
assertEquals(0x3,   bit32.extract('0xfa', 4, 2),      'extract: coalesce args')
assertEquals(0xfe,  bit32.replace('0xfa', 0x3, 2, 2), 'replace: coalesce args')
assertEquals(0x3e8, bit32.lrotate('0xfa', 2),         'lrotate: coalesce args')

assertEquals(0x3e8, bit32.lshift('0xfa', 2),          'lshift: coalesce args')
assertEquals(0x3d,  bit32.rrotate('0xf4', 2),         'rrotate: coalesce args')
assertEquals(0x3e,  bit32.rshift('0xfa', 2),          'rshift: coalesce args')
");
    }
  }
}
