using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [TestClass]
    public class ConformanceTest : TestBase
    {
        [TestMethod]
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