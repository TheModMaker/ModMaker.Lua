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

testCase('ParenthesesCollapseMultiValues', function()
  function values()
    return 1, 2
  end
  function checkArgs(a, b)
    assertEquals(1, a, 'collapse arguments1')
    assertEquals(nil, b, 'collapse arguments2')
  end
  function checkReturn()
    return (values())
  end

  local a, b = values()
  assertEquals(1, a, 'collapse sanity check1')
  assertEquals(2, b, 'collapse sanity check2')

  local c, d = (values())
  assertEquals(1, c, 'collapse assignment1')
  assertEquals(nil, d, 'collapse assignment2')

  checkArgs((values()))

  local e, f = checkReturn()
  assertEquals(1, e, 'collapse return1')
  assertEquals(nil, f, 'collapse return2')
end)