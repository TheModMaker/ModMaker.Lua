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

testCase('General', function()
  assertEquals(     251245,            math.abs(-251245),       'abs')
  assertEqualsDelta(0.361416951927645, math.asin(0.3536),       'asin')
  assertEqualsDelta(0.187568907875447, math.atan(0.1898),       'atan')
  assertEqualsDelta(0.161967351986035, math.atan2(25, 153),     'atan2')
  assertEquals(     2455,              math.ceil(2454.5147),    'ceil')
  assertEqualsDelta(0.658878051008508, math.cos(0.85147),       'cos')
  assertEqualsDelta(1.38493787774095,  math.cosh(0.85147),      'cosh')
  assertEqualsDelta(48.7856373820042,  math.deg(0.85147),       'deg')
  assertEqualsDelta(69.6254387609039,  math.exp(4.24313),       'exp')
  assertEquals(     2454,              math.floor(2454.5147),   'floor')
  assertEqualsDelta(-1.195,            math.fmod(24.54, 5.147), 'fmod')
  assertEqualsDelta(70866960384,       math.ldexp(528, 27),     'ldexp')
  assertEqualsDelta(3.20030443928277,  math.log(24.54),         'ln')
  assertEqualsDelta(1.53902111462939,  math.log(24.54, 8),      'log')
  assertEqualsDelta(40872.6120526573,  math.pow(3.678, 8.153),  'pow')
  assertEqualsDelta(4.802797035638,    math.rad(275.18),        'rad')
  assertEqualsDelta(0.608415615200534, math.sin(2.48753),       'sin')
  assertEqualsDelta(5.97420326157982,  math.sinh(2.48753),      'sinh')
  assertEqualsDelta(221.303904168002,  math.sqrt(48975.418),    'sqrt')
  assertEqualsDelta(-0.76663479914779, math.tan(2.48753),       'tan')
  assertEqualsDelta(0.986278580120099, math.tanh(2.48753),      'tanh')
end)

testCase('General_InvalidTypes', function()
  local oneArg = {
    'abs', 'asin', 'atan', 'ceil', 'cos', 'cosh', 'deg', 'exp',
    'frexp', 'floor', 'rad', 'sin', 'sinh', 'sqrt', 'tan', 'tanh',
  }
  local twoArg = {'atan2', 'fmod', 'ldexp', 'log', 'pow'}

  for i, func in pairs(oneArg) do
    invalidTypeTest(func, function(a) math[func](a) end, 'number')
  end
  for i, func in pairs(twoArg) do
    invalidTypeTest(func .. ' 1', function(a) math[func](a, 1) end, 'number')
    invalidTypeTest(func .. ' 2', function(a) math[func](3, a) end, 'number')
  end
end)

testCase('frexp', function()
  local a, b = math.frexp(245)
  assertEqualsDelta(0.95703125,      a, 'frexp: normal(1)')
  assertEquals(     8,               b, 'frexp: normal(2)')

  a, b = math.frexp(-24623)
  assertEqualsDelta(-0.751434326171, a, 'frexp: negative(1)')
  assertEquals(     15,              b, 'frexp: negative(2)')
end)

testCase('max', function()
  assertEquals(2672368, math.max(2, -566, 451, 2672368, 1), 'max: normal')
  assertEquals(63,      math.max(63, -566, -47, 0, -7),     'max: return is first argument')
  assertEquals(8,       math.max(8),                        'max: one argument')
end)

testCase('max_InvalidTypes', function()
  invalidTypeTest('max 1', function(a) math.max(2, a) end, 'number')
  invalidTypeTest('max 2', function(a) math.max(a, 2, 3) end, 'number')
end)

testCase('max_ZeroArgs', function()
  assertThrowsArg('max', function() math.max() end)
end)

testCase('min', function()
  assertEquals(-566, math.min(2, -566, 451, 2672368, 1), 'min: normal')
  assertEquals(-63,  math.min(-63, 566, 47, 0, -7),      'min: return is first argument')
  assertEquals(8,    math.min(8),                        'min: one argument')
end)

testCase('min_InvalidTypes', function()
  invalidTypeTest('min 1', function(a) math.min(2, a) end, 'number')
  invalidTypeTest('min 2', function(a) math.min(a, 2, 3) end, 'number')
end)

testCase('min_ZeroArgs', function()
  assertThrowsArg('min', function() math.min() end)
end)

testCase('modf', function()
  local a, b = math.modf(26825.2154672)
  assertEquals(    26825,       a, 'modf: normal(1)')
  assertEqualsDelta(0.2154672,  b, 'modf: normal(2)')

  a, b = math.modf(-48675.287548)
  assertEquals(     -48675,     a, 'modf: negative(1)')
  assertEqualsDelta(-0.287548,  b, 'modf: negative(2)')

  a, b = math.modf(8458)
  assertEquals(     8458,       a, 'modf: integer(1)')
  assertEqualsDelta(0,          b, 'modf: integer(2)')

  a, b = math.modf(0.4856256)
  assertEquals(     0,          a, 'modf: fraction(1)')
  assertEqualsDelta(0.4856256,  b, 'modf: fraction(2)')

  a, b = math.modf(0)
  assertEquals(     0,          a, 'modf: zero(1)')
  assertEqualsDelta(0,          b, 'modf: zero(2)')
end)

testCase('modf_InvalidTypes', function()
  invalidTypeTest('modf', function(a) math.modf(a) end, 'number')
end)

testCase('modf_ZeroArgs', function()
  assertThrowsArg('modf', function() math.modf() end)
end)

testCase('random', function()
  -- NOTE: Although the multiple runs will produce the same results, the actual
  -- value of the value is undefined; therefore we need to just check for
  -- 'randomness' and for consistency, not for specific values.
  math.randomseed(12345)
  local a = math.random()
  local b = math.random(45)
  local c = math.random(24, 68)

  assertTrue(0 <= a and a <= 1,         'random: no-arg range')
  assertTrue(1 <= b and b <= 45,        'random: one-arg range')
  assertEquals(0, math.fmod(b, 1),      'random: one-arg is an integer')
  assertTrue(24 <= c and c <= 68,       'random: two-arg range')
  assertEquals(0, math.fmod(c, 1),      'random: two-arg is an integer')

  -- This is technically possible, but extremely unlikely.
  math.randomseed(54321)
  local x = math.random()
  local y = math.random(45)
  assertTrue(x ~= a and y ~= b,         'random: different seeds make different values')

  math.randomseed(12345)

  assertEquals(a, math.random(),       'random: zero-arg makes same values')
  assertEquals(b, math.random(45),     'random: one-arg makes same values')
  assertEquals(c, math.random(24, 68), 'random: two-arg makes same values')
end)

testCase('randon_InvalidTypes', function()
  invalidTypeTest('randomseed', function(a) math.randomseed(a) end, 'number')
  invalidTypeTest('random 1', function(a) math.random(a) end, 'number', 'nil')
  invalidTypeTest('random 2', function(a) math.random(12, a) end, 'number', 'nil')
end)