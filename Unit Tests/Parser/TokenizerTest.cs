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

            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 1, EndPos = 9, Value = "function" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 10, EndPos = 14, Value = "test" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 14, EndPos = 15, Value = "(" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 15, EndPos = 16, Value = ")" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 2, EndLine = 2, StartPos = 5, EndPos = 10, Value = "local" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 2, EndLine = 2, StartPos = 11, EndPos = 12, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 2, EndLine = 2, StartPos = 13, EndPos = 14, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 2, EndLine = 2, StartPos = 15, EndPos = 17, Value = "12" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 5, EndPos = 6, Value = "t" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 7, EndPos = 8, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 8, EndPos = 9, Value = "{" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 10, EndPos = 11, Value = "[" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 11, EndPos = 13, Value = "12" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 13, EndPos = 14, Value = "]" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 15, EndPos = 16, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 17, EndPos = 18, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 18, EndPos = 19, Value = "," }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 20, EndPos = 23, Value = "cat" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 23, EndPos = 24, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 24, EndPos = 33, Value = "12.345456" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 3, EndLine = 3, StartPos = 34, EndPos = 35, Value = "}" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 4, EndLine = 4, StartPos = 5, EndPos = 8, Value = "str" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 4, EndLine = 4, StartPos = 9, EndPos = 10, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 4, EndLine = 4, StartPos = 11, EndPos = 53, Value = "\"this is a test string with \"escapes\"\n" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 5, EndLine = 5, StartPos = 5, EndPos = 7, Value = "::" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 5, EndLine = 5, StartPos = 7, EndPos = 13, Value = "potato" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 5, EndLine = 5, StartPos = 13, EndPos = 15, Value = "::" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 6, EndLine = 6, StartPos = 1, EndPos = 4, Value = "end" }, tokenizer.Read());

            Assert.AreEqual(new Token() { StartLine = 0, EndLine = 0, StartPos = 0, EndPos = 0, Value = null }, tokenizer.Read());
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

            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 1, EndPos = 9, Value = "function" }, tokenizer.Read());

            // check for multiple calls to Peek
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 10, EndPos = 14, Value = "test" }, tokenizer.Peek());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 10, EndPos = 14, Value = "test" }, tokenizer.Peek());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 10, EndPos = 14, Value = "test" }, tokenizer.Peek());

            // check that peek changes
            Token next = tokenizer.Read();
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 10, EndPos = 14, Value = "test" }, next);
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 14, EndPos = 15, Value = "(" }, tokenizer.Peek());

            Token next2 = tokenizer.Read();
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 14, EndPos = 15, Value = "(" }, next2);
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 15, EndPos = 16, Value = ")" }, tokenizer.Peek());

            // check that PushBack works putting things back
            tokenizer.PushBack(next2);
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 14, EndPos = 15, Value = "(" }, tokenizer.Peek());
            tokenizer.PushBack(next);
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 10, EndPos = 14, Value = "test" }, tokenizer.Peek());

            // check that PushBack works after several calls and in the correct order.
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 10, EndPos = 14, Value = "test" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 14, EndPos = 15, Value = "(" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 15, EndPos = 16, Value = ")" }, tokenizer.Read());

            Assert.AreEqual(new Token() { StartLine = 0, EndLine = 0, StartPos = 0, EndPos = 0, Value = null }, tokenizer.Read());
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

            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 1, EndPos = 2, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 3, EndPos = 4, Value = "=" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 5, StartPos = 5, EndPos = 21, Value = "\"this is a test of a\nlong string ]]\nthis is still a string\nnot an escape \\n" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 6, EndLine = 6, StartPos = 1, EndPos = 4, Value = "end" }, tokenizer.Read());

            Assert.AreEqual(new Token() { StartLine = 0, EndLine = 0, StartPos = 0, EndPos = 0, Value = null }, tokenizer.Read());
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

            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 1, EndPos = 2, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 3, EndPos = 4, Value = "=" }, tokenizer.Read());

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

            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 1, EndPos = 2, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 3, EndPos = 4, Value = "=" }, tokenizer.Read());

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

            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 1, EndPos = 2, Value = "v" }, tokenizer.Read());
            Assert.AreEqual(new Token() { StartLine = 1, EndLine = 1, StartPos = 3, EndPos = 4, Value = "=" }, tokenizer.Read());

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
