// Copyright 2014 Jacob Trimble
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

using ModMaker.Lua.Parser;
using System;
using System.Globalization;
using NUnit.Framework;

namespace UnitTests.Parser
{
    /// <summary>
    ///This is a test class for TokenizerTest and is intended
    ///to contain all TokenizerTest Unit Tests
    ///</summary>
    [TestFixture]
    public class TokenizerTest
    {
        /// <summary>
        /// A general test with valid input for a
        /// sequence of Tokenizer.Read().
        ///</summary>
        [Test]
        public void GeneralReadTest()
        {
            var reader = StringInfo.GetTextElementEnumerator(
@"function test()
    local v = 12 -- this is a comment
    t ={ [12] = v, cat=12.345456 }
    str = ""this is a test string with \""escapes\""\n""
    ::potato::
end"
            );
            Tokenizer tokenizer = new Tokenizer(reader, "Test");

            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 1, Value = "function" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 10, Value = "test" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 14, Value = "(" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 15, Value = ")" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 2, StartPos = 5, Value = "local" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 2, StartPos = 11, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 2, StartPos = 13, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 2, StartPos = 15, Value = "12" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 5, Value = "t" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 7, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 8, Value = "{" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 10, Value = "[" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 11, Value = "12" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 13, Value = "]" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 15, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 17, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 18, Value = "," }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 20, Value = "cat" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 23, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 24, Value = "12.345456" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, StartPos = 34, Value = "}" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 4, StartPos = 5, Value = "str" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 4, StartPos = 9, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 4, StartPos = 11, Value = "\"this is a test string with \"escapes\"\n" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 5, StartPos = 5, Value = "::" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 5, StartPos = 7, Value = "potato" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 5, StartPos = 13, Value = "::" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 6, StartPos = 1, Value = "end" }, tokenizer.Read());

            Assert.AreEqual(new Token() { StartLine = 0, StartPos = 0, Value = null }, tokenizer.Read());
        }

        /// <summary>
        /// A test of Read, Peek, and PushBack for
        /// Tokenizer.
        ///</summary>
        [Test]
        public void ReadPeekPushBackTest()
        {
            var reader = StringInfo.GetTextElementEnumerator(@"function test()");
            Tokenizer tokenizer = new Tokenizer(reader, "Test");

            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 1, Value = "function" }, tokenizer.Read());

            // check for multiple calls to Peek
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 10, Value = "test" }, tokenizer.Peek());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 10, Value = "test" }, tokenizer.Peek());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 10, Value = "test" }, tokenizer.Peek());

            // check that peek changes
            Token next = tokenizer.Read();
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 10, Value = "test" }, next);
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 14, Value = "(" }, tokenizer.Peek());

            Token next2 = tokenizer.Read();
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 14, Value = "(" }, next2);
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 15, Value = ")" }, tokenizer.Peek());

            // check that PushBack works putting things back
            tokenizer.PushBack(next2);
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 14, Value = "(" }, tokenizer.Peek());
            tokenizer.PushBack(next);
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 10, Value = "test" }, tokenizer.Peek());

            // check that PushBack works after several calls and in the correct order.
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 10,  Value = "test" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 14, Value = "(" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 15, Value = ")" }, tokenizer.Read());

            Assert.AreEqual(new Token() { StartLine = 0, StartPos = 0, Value = null }, tokenizer.Read());
        }

        /// <summary>
        /// A test of long string for the tokenizer.
        ///</summary>
        [Test]
        public void LongStringTest()
        {
            var reader = StringInfo.GetTextElementEnumerator(
@"v = [==[
this is a test of a
long string ]]
this is still a string
not an escape \n]==]
end"
            );
            Tokenizer tokenizer = new Tokenizer(reader, "Test");

            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 1, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 3, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 5, Value = "\"this is a test of a\nlong string ]]\nthis is still a string\nnot an escape \\n" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 6, StartPos = 1, Value = "end" }, tokenizer.Read());

            Assert.AreEqual(new Token() { StartLine = 0, StartPos = 0, Value = null }, tokenizer.Read());
        }

        /// <summary>
        /// A test of an invalid comment.
        ///</summary>
        [Test]
        public void StringErrorTest()
        {
            // newline in string literal.
            var reader = StringInfo.GetTextElementEnumerator(
@"v = 'error string

end"
            );
            Tokenizer tokenizer = new Tokenizer(reader, "Test");

            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 1, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 3, Value = "=" }, tokenizer.Read());

            try
            {
                tokenizer.Read();
                Assert.Fail("Supposed to cause error.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOf<SyntaxException>(e);
            }

            // only one grave (`) per literal.
            reader = StringInfo.GetTextElementEnumerator(@"v = foo``");
            tokenizer = new Tokenizer(reader, "Test");

            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 1, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 3, Value = "=" }, tokenizer.Read());

            try
            {
                tokenizer.Read();
                Assert.Fail("Supposed to cause error.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOf<SyntaxException>(e);
            }

            // only number after a grave(`).
            reader = StringInfo.GetTextElementEnumerator(@"v = foo`23e");
            tokenizer = new Tokenizer(reader, "Test");

            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 1, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, StartPos = 3, Value = "=" }, tokenizer.Read());

            try
            {
                tokenizer.Read();
                Assert.Fail("Supposed to cause error.");
            }
            catch (Exception e)
            {
                Assert.IsInstanceOf<SyntaxException>(e);
            }
        }
    }
}
