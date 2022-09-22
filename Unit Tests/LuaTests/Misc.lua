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

testCase('ReflectionIsOpaque', function()
  assertEquals('', getType().Empty, 'using Type gets static members')

  assertEquals(nil, getType().Name, "members on Type aren't visible")
  assertEquals(nil, getMethod().Name, "members on MethodInfo aren't visible")
  assertEquals(nil, getMethod().GetParameters, "members on MethodInfo aren't visible 2")
  assertEquals(nil, getMethod().GetType, "GetType isn't visible")
  assertEquals(nil, getObj().GetType, "GetType isn't visible 2")

  assertThrows("members on Type can't be set", function() getType().Name = '' end)
end)