// Copyright 2017 Jacob Trimble
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

using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class ConformanceTest : TestBase
    {
        [Test]
        public void ParenthesesCollapseMultiValues()
        {
            Lua.DoText(@"
function values()
  return 1, 2
end

function checkArgs(a, b)
  assertEquals(a, 1, 'collapse arguments1')
  assertEquals(b, nil, 'collapse arguments2')
end

function checkReturn()
  return (values())
end


local a, b = values()
assertEquals(a, 1, 'collapse sanity check1')
assertEquals(b, 2, 'collapse sanity check2')

local c, d = (values())
assertEquals(c, 1, 'collapse assignment1')
assertEquals(d, nil, 'collapse assignment2')

checkArgs((values()))

local e, f = checkReturn()
assertEquals(e, 1, 'collapse return1')
assertEquals(f, nil, 'collapse return2')
");
        }
    }
}