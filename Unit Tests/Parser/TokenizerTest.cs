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
using System.Text;
using System.IO;

namespace UnitTests.Parser
{
    /// <summary>
    ///This is a test class for TokenizerTest and is intended
    ///to contain all TokenizerTest Unit Tests
    ///</summary>
    [TestFixture]
    public class TokenizerTest
    {
        public static Tokenizer CreateTokenizer(string str)
        {
            var encoding = Encoding.UTF8;
            var stream = new MemoryStream(encoding.GetBytes(str));
            return new Tokenizer(stream, encoding, "Test");
        }

        /// <summary>
        /// A general test with valid input for a
        /// sequence of Tokenizer.Read().
        ///</summary>
        [Test]
        public void GeneralReadTest()
        {
            var tokenizer = CreateTokenizer(
@"function test()
    local v = 12 -- this is a comment
    t ={ [12] = v, cat=12.345456 }
    str = ""this is a test string with \""escapes\""\n""
    ::potato::
end");
            Action<TokenType, int, int, string> check =
                (type, line, pos, str) =>
            {
                Token read = tokenizer.Read();
                Assert.AreEqual(new Token(type, str, pos, line), read);
            };
            check(TokenType.Function, 1, 1, "function");
            check(TokenType.Identifier, 1, 10, "test");
            check(TokenType.BeginParen, 1, 14, "(");
            check(TokenType.EndParen, 1, 15, ")");
            check(TokenType.Local, 2, 5, "local");
            check(TokenType.Identifier, 2, 11, "v");
            check(TokenType.Assign, 2, 13, "=");
            check(TokenType.NumberLiteral, 2, 15, "12");
            check(TokenType.Identifier, 3, 5, "t");
            check(TokenType.Assign, 3, 7, "=");
            check(TokenType.BeginTable, 3, 8, "{");
            check(TokenType.BeginBracket, 3, 10, "[");
            check(TokenType.NumberLiteral, 3, 11, "12");
            check(TokenType.EndBracket, 3, 13, "]");
            check(TokenType.Assign, 3, 15, "=");
            check(TokenType.Identifier, 3, 17, "v");
            check(TokenType.Comma, 3, 18, ",");
            check(TokenType.Identifier, 3, 20, "cat");
            check(TokenType.Assign, 3, 23, "=");
            check(TokenType.NumberLiteral, 3, 24, "12.345456");
            check(TokenType.EndTable, 3, 34, "}");
            check(TokenType.Identifier, 4, 5, "str");
            check(TokenType.Assign, 4, 9, "=");
            check(TokenType.StringLiteral, 4, 11,
                  "\"this is a test string with \"escapes\"\n");
            check(TokenType.Label, 5, 5, "::");
            check(TokenType.Identifier, 5, 7, "potato");
            check(TokenType.Label, 5, 13, "::");
            check(TokenType.End, 6, 1, "end");

            check(TokenType.None, 0, 0, null);
        }

        /// <summary>
        /// A test of long string for the tokenizer.
        ///</summary>
        [Test]
        public void LongStringTest()
        {
            var tokenizer = CreateTokenizer(
@"v = [==[
this is a test of a
long string ]]
this is still a string
not an escape \n]==]
end"
            );

            Action<TokenType, int, int, string> check =
                (type, line, pos, str) =>
                {
                    Token read = tokenizer.Read();
                    Assert.AreEqual(new Token(type, str, pos, line), read);
                };
            check(TokenType.Identifier, 1, 1, "v");
            check(TokenType.Assign, 1, 3, "=");
            check(TokenType.StringLiteral, 1, 5,
                  "\"\nthis is a test of a\nlong string ]]\nthis is still a string\nnot an escape \\n");
            check(TokenType.End, 6, 1, "end");
            check(TokenType.None, 0, 0, null);
        }

        /// <summary>
        /// A test of an invalid comment.
        ///</summary>
        [Test]
        public void StringErrorTest()
        {
            Assert.Throws<SyntaxException>(
                () => CreateTokenizer("'foo\nbar'").Read());
            Assert.Throws<SyntaxException>(
                () => CreateTokenizer("\"foo\nbar\"").Read());

            Assert.Throws<SyntaxException>(
                () => CreateTokenizer("foo`").Read());
            Assert.Throws<SyntaxException>(
                () => CreateTokenizer("foo`e").Read());
        }
    }
}
