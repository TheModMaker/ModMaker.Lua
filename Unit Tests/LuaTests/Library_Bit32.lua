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
  testCase('arshift', function()
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
  end)

  testCase('arshift_InvalidTypes', function()
    invalidTypeTest('arshift 1', function(a) bit32.arshift(a, 2) end, 'number')
    invalidTypeTest('arshift 2', function(a) bit32.arshift(2, a) end, 'number')
  end)

  testCase('arshift_NotEnoughArgs', function()
    assertThrowsArg('arshift', function() bit32.arshift(0xf00) end)
  end)
end

do
  testCase('band', function()
    assertEquals(0xea,       bit32.band(0xff, 0xea),                 'band: normal')
    assertEquals(0xffffffff, bit32.band(),                           'band: zero arguments')
    assertEquals(0xea,       bit32.band(0xea),                       'band: one argument')
    assertEquals(0x22,       bit32.band(0xff, 0xea, 0x7f, 0xa3),     'band: more than two')
    assertEquals(0x00,       bit32.band(0x42, 0xea, 0x7a, 0xa1),     'band: clears out')
    assertEquals(0xaa000000, bit32.band(0xfa0000aa, 0xaf00aa00),     'band: large number')
    assertEquals(0x0f,       bit32.band(0xff0000000f, 0xff0000000f), 'band: >32-bits')
  end)

  testCase('band_InvalidTypes', function()
    invalidTypeTest('band 1', function(a) bit32.band(a) end, 'number')
    invalidTypeTest('band 2', function(a) bit32.band(2, 3, a, 4) end, 'number')
  end)
end

do
  testCase('bnot', function()
    assertEquals(0xff005533, bit32.bnot(0x00ffaacc),        'bnot: normal')
    assertEquals(0xff005533, bit32.bnot(0x00ffaacc, 'cat'), 'bnot: extra args')
    assertEquals(0x00ffaa66, bit32.bnot(0xff005599),        'bnot: high-bit set')
    assertEquals(11,         bit32.bnot(-12),               'bnot: negative')
    assertEquals(0xffffffff, bit32.bnot(0),                 'bnot: zero')
    assertEquals(0xffffefdb, bit32.bnot(0x4500001024),      'bnot: >32-bits')
  end)

  testCase('bnot_InvalidTypes', function()
    invalidTypeTest('bnot', function(a) bit32.bnot(a) end, 'number')
  end)

  testCase('bnot_NotEnoughArgs', function()
    assertThrowsArg('bnot', function() bit32.bnot() end)
  end)
end

do
  testCase('extract', function()
    assertEquals(0x3a28,  bit32.extract(0xa74e8a28, 6, 16),       'extract: normal')
    assertEquals(0x0,     bit32.extract(0xa74e8a28, 6),           'extract: default width')
    assertEquals(0x4fe3a, bit32.extract(0x913f8ea, 2, 21, 'cat'), 'extract: extra args')
    assertEquals(0x1e,    bit32.extract(0xff0305d2f0, 3, 6),      'extract: >32-bits')
  end)

  testCase('extract_InvalidTypes', function()
    invalidTypeTest('extract 1', function(a) bit32.extract(a, 0, 2) end, 'number')
    invalidTypeTest('extract 2', function(a) bit32.extract(3, a, 2) end, 'number')
    invalidTypeTest('extract 3', function(a) bit32.extract(2, 0, a) end, 'number')
  end)

  testCase('extract_TooLargeIndex', function()
    assertThrowsArg('extract', function() bit32.extract(0x3, 34) end)
  end)

  testCase('extract_TooLargeSize', function()
    assertThrowsArg('extract', function() bit32.extract(0x3, 3, 42) end)
  end)

  testCase('extract_TooLargeIndexPlusSize', function()
    assertThrowsArg('extract', function() bit32.extract(0x3, 21, 12) end)
  end)

  testCase('extract_NotEnoughArgs', function()
    assertThrowsArg('extract', function() bit32.extract() end)
  end)

  testCase('extract_NotEnoughArgs2', function()
    assertThrowsArg('extract', function() bit32.extract(0xf00) end)
  end)
end

do
  testCase('replace', function()
    assertEquals(0xa74a4a28, bit32.replace(0xa74e8a28, 0x452, 13, 6),  'replace: normal')
    assertEquals(0xa74e8a20, bit32.replace(0xa74e8a28, 6, 3), 'replace: defaults to 1 width')
    assertEquals(0xffff4c47, bit32.replace(-45625, 34, 5, 4), 'replace: negative source')
    assertEquals(0xa74eca,   bit32.replace(0xa74e8a, -42, 5, 4), 'replace: negative repl')
    assertEquals(0x918aa,    bit32.replace(0x918ea, 0x2462a, 2, 10, 'cat'), 'replace: extra args')
    assertEquals(0xd0f0,     bit32.replace(0xff0000d2f0, 0x23, 6, 4),  'replace: >32-bits')
  end)

  testCase('replace_InvalidTypes', function()
    invalidTypeTest('replace 1', function(a) bit32.replace(a, 0) end, 'number')
    invalidTypeTest('replace 2', function(a) bit32.replace(0, a) end, 'number')
    invalidTypeTest('replace 3', function(a) bit32.replace(0, 2, a) end, 'number')
    invalidTypeTest('replace 4', function(a) bit32.replace(0, 2, 3, a) end, 'number')
  end)

  testCase('replace_TooLargeIndex', function()
    assertThrowsArg('replace', function() bit32.replace(0x3, 0x0, 34) end)
  end)

  testCase('replace_TooLargeSize', function()
    assertThrowsArg('replace', function() bit32.replace(0x3, 0x0, 3, 42) end)
  end)

  testCase('replace_TooLargeIndexPlusSize', function()
    assertThrowsArg('replace', function() bit32.replace(0x3, 0x0, 21, 12) end)
  end)

  testCase('replace_NotEnoughArgs', function()
    assertThrowsArg('replace', function() bit32.replace() end)
  end)

  testCase('replace_NotEnoughArgs2', function()
    assertThrowsArg('replace', function() bit32.replace(0xf00) end)
  end)

  testCase('replace_NotEnoughArgs3', function()
    assertThrowsArg('replace', function() bit32.replace(0xf00, 0x00) end)
  end)
end

do
  testCase('lrotate', function()
    assertEquals(0xd3a28a29, bit32.lrotate(0xa74e8a28, 6),         'lrotate: normal')
    assertEquals(0x4e9d1451, bit32.lrotate(0xa74e8a28, 65),        'lrotate: > 32')
    assertEquals(0x4e9d1451, bit32.lrotate(0xa74e8a28, 65, 'cat'), 'lrotate: > 32')
    assertEquals(0xc25a789e, bit32.lrotate(0x5a789ec2, -8),        'lrotate: negative')
    assertEquals(0x27b0969e, bit32.lrotate(0x5a789ec2, -82),       'lrotate: negative < -32')
    assertEquals(0xffff216f, bit32.lrotate(-3562, 4),              'lrotate: negative source')
    assertEquals(0xd2f00,    bit32.lrotate(0xff0000d2f0, 4),       'lrotate: >32-bits')
  end)

  testCase('lrotate_InvalidTypes', function()
    invalidTypeTest('lrotate 1', function(a) bit32.lrotate(a, 0) end, 'number')
    invalidTypeTest('lrotate 2', function(a) bit32.lrotate(3, a) end, 'number')
  end)

  testCase('lrotate_NotEnoughArgs', function()
    assertThrowsArg('lrotate', function() bit32.lrotate() end)
  end)

  testCase('lrotate_NotEnoughArgs2', function()
    assertThrowsArg('lrotate', function() bit32.lrotate(0xf00) end)
  end)
end

do
  testCase('lshift', function()
    assertEquals(0xd3a28a00, bit32.lshift(0xa74e8a28, 6),         'lshift: normal')
    assertEquals(0,          bit32.lshift(0xa74e8a28, 65),        'lshift: > 32')
    assertEquals(0,          bit32.lshift(0xa74e8a28, 65, 'cat'), 'lshift: extra args')
    assertEquals(0x5a789e,   bit32.lshift(0x5a789ec2, -8),        'lshift: negative')
    assertEquals(0,          bit32.lshift(0x5a789ec2, -82),       'lshift: negative < -32')
    assertEquals(0xffff2160, bit32.lshift(-3562, 4),              'lshift: negative source')
    assertEquals(0xd2f00,    bit32.lshift(0xff0000d2f0, 4),       'lshift: >32-bits')
  end)

  testCase('lshift_InvalidTypes', function()
    invalidTypeTest('lshift 1', function(a) bit32.lshift(a, 0) end, 'number')
    invalidTypeTest('lshift 2', function(a) bit32.lshift(4, a) end, 'number')
  end)

  testCase('lshift_NotEnoughArgs', function()
    assertThrowsArg('lshift', function() bit32.lshift() end)
  end)

  testCase('lshift_NotEnoughArgs2', function()
    assertThrowsArg('lshift', function() bit32.lshift(0xf00) end)
  end)
end

do
  testCase('rrotate', function()
    assertEquals(0xa29d3a28, bit32.rrotate(0xa74e8a28, 6),         'rrotate: normal')
    assertEquals(0x53a74514, bit32.rrotate(0xa74e8a28, 65),        'rrotate: > 32')
    assertEquals(0x53a74514, bit32.rrotate(0xa74e8a28, 65, 'cat'), 'rrotate: extra args')
    assertEquals(0x789ec25a, bit32.rrotate(0x5a789ec2, -8),        'rrotate: negative')
    assertEquals(0x7b0969e2, bit32.rrotate(0x5a789ec2, -82),       'rrotate: negative < -32')
    assertEquals(0x6fffff21, bit32.rrotate(-3562, 4),              'rrotate: negative source')
    assertEquals(0xd2f,      bit32.rrotate(0xff0000d2f0, 4),       'rrotate: >32-bits')
  end)

  testCase('rrotate_InvalidTypes', function()
    invalidTypeTest('rrotate 1', function(a) bit32.rrotate(a, 0) end, 'number')
    invalidTypeTest('rrotate 2', function(a) bit32.rrotate(4, a) end, 'number')
  end)

  testCase('rrotate_NotEnoughArgs', function()
    assertThrowsArg('rrotate', function() bit32.rrotate() end)
  end)

  testCase('rrotate_NotEnoughArgs2', function()
    assertThrowsArg('rrotate', function() bit32.rrotate(0xf00) end)
  end)
end

do
  testCase('rshift', function()
    assertEquals(0x29d3a28,  bit32.rshift(0xa74e8a28, 6),         'rshift: normal')
    assertEquals(0,          bit32.rshift(0xa74e8a28, 65),        'rshift: > 32')
    assertEquals(0,          bit32.rshift(0xa74e8a28, 65, 'cat'), 'rshift: extra args')
    assertEquals(0x789ec200, bit32.rshift(0x5a789ec2, -8),        'rshift: negative')
    assertEquals(0,          bit32.rshift(0x5a789ec2, -82),       'rshift: negative < -32')
    assertEquals(0xfffff21,  bit32.rshift(-3562, 4),              'rshift: negative source')
    assertEquals(0xd2f,      bit32.rshift(0xff0000d2f0, 4),       'rshift: >32-bits')
  end)

  testCase('rshift_InvalidTypes', function()
    invalidTypeTest('rshift 1', function(a) bit32.rshift(a, 0) end, 'number')
    invalidTypeTest('rshift 2', function(a) bit32.rshift(4, a) end, 'number')
  end)

  testCase('rshift_NotEnoughArgs', function()
    assertThrowsArg('rshift', function() bit32.rshift() end)
  end)

  testCase('rshift_NotEnoughArgs2', function()
    assertThrowsArg('rshift', function() bit32.rshift(0xf00) end)
  end)
end

do
  testCase('coalesce', function()
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
  end, 'Disabled until coalesce is supported')
end