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

testCase('overload', function()
  assertEquals(1, overloadGetNumber(0),              'overload: normal')
  assertEquals(1, overload(overloadGetNumber, 0)(0), 'overload: first')
  assertEquals(2, overload(overloadGetNumber, 1)(0), 'overload: second')

  local obj = OverloadType()
  assertEquals(1, obj.Call(0),              'overload: normal')
  assertEquals(1, overload(obj.Call, 0)(0), 'overload: member first')
  assertEquals(2, overload(obj.Call, 1)(0), 'overload: member second')
end)

testCase('overload_OutOfRange', function()
  assertThrowsArg('overload 1', function() overload(overloadGetNumber) end)
  assertThrowsArg('overload 2', function() overload(overloadGetNumber, -1) end)
  assertThrowsArg('overload 3', function() overload(overloadGetNumber, 3) end)
  assertThrowsArg('overload 4', function() overload(OverloadType().Call, -2) end)
  assertThrowsArg('overload 5', function() overload(OverloadType().Call, 3) end)
end)