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

using System;
using System.IO;
using System.Text;
using ModMaker.Lua.Parser;
using NUnit.Framework;

namespace UnitTests.Parser {
  [TestFixture]
  public class LexerTest {
    static Lexer _createLexer(string str) {
      var encoding = Encoding.UTF8;
      var stream = new MemoryStream(encoding.GetBytes(str));
      return new Lexer(stream, encoding, "Test");
    }

    [Test]
    public void GeneralReadTest() {
      var lexer = _createLexer(
@"function test()
    local v = 12 -- this is a comment
    t ={ [12] = v, cat=12.345456 }
    str = ""this is a test string with \""escapes\""\n""
    ::potato::
end");
      Action<TokenType, int, int, string> check = (type, line, pos, str) => {
        Token read = lexer.Read();
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
      check(TokenType.StringLiteral, 4, 11, "this is a test string with \"escapes\"\n");
      check(TokenType.Label, 5, 5, "::");
      check(TokenType.Identifier, 5, 7, "potato");
      check(TokenType.Label, 5, 13, "::");
      check(TokenType.End, 6, 1, "end");

      check(TokenType.None, 6, 4, "");
    }

    [Test]
    public void LongStringTest() {
      var lexer = _createLexer(
@"v = [==[
this is a test of a
long string ]]
this is still a string
not an escape \n]==]
end"
      );

      Action<TokenType, int, int, string> check = (type, line, pos, str) => {
        Token read = lexer.Read();
        Assert.AreEqual(new Token(type, str, pos, line), read);
      };
      check(TokenType.Identifier, 1, 1, "v");
      check(TokenType.Assign, 1, 3, "=");
      check(TokenType.StringLiteral, 1, 5,
            "\"\nthis is a test of a\nlong string ]]\nthis is still a string\nnot an escape \\n");
      check(TokenType.End, 6, 1, "end");
      check(TokenType.None, 6, 4, "");
    }

    [Test]
    public void StringErrorTest() {
      Assert.Throws<SyntaxException>(() => _createLexer("'foo\nbar'").Read());
      Assert.Throws<SyntaxException>(() => _createLexer("\"foo\nbar\"").Read());
    }

    [Test]
    public void PeekTest() {
      var lexer = _createLexer("a b");

      var tok1 = lexer.Peek();
      Assert.AreEqual(tok1.Value, "a");
      var tok2 = lexer.Peek();
      Assert.AreEqual(tok1, tok2);

      Assert.AreEqual(lexer.Read().Value, "a");
      Assert.AreEqual(lexer.Read().Value, "b");

      Assert.AreEqual(lexer.Peek().Type, TokenType.None);  // EOF
      Assert.AreEqual(lexer.Peek().Type, TokenType.None);  // EOF
    }

    [Test]
    public void PeekTypeTest() {
      var lexer = _createLexer("a +");

      Assert.IsTrue(lexer.PeekType(TokenType.Identifier));
      Assert.IsTrue(lexer.PeekType(TokenType.Identifier));
      Assert.IsFalse(lexer.PeekType(TokenType.Assign));
      lexer.Read();
      Assert.IsTrue(lexer.PeekType(TokenType.Add));
      Assert.IsFalse(lexer.PeekType(TokenType.Identifier));
    }

    [Test]
    public void ExpectTest() {
      var lexer = _createLexer("a + b");

      var tok1 = lexer.Expect(TokenType.Identifier);
      Assert.AreEqual(tok1.Value, "a");
      var tok2 = lexer.Expect(TokenType.Add);
      Assert.AreEqual(tok2.Value, "+");

      Assert.Throws<SyntaxException>(() => lexer.Expect(TokenType.Add));
      Assert.Throws<SyntaxException>(() => lexer.Expect(TokenType.Add));
      Assert.Throws<SyntaxException>(() => lexer.Expect(TokenType.Assign));

      var tok3 = lexer.Expect(TokenType.Identifier);
      Assert.AreEqual(tok3.Value, "b");

      Assert.AreEqual(lexer.Read().Type, TokenType.None);  // EOF
      Assert.Throws<SyntaxException>(() => lexer.Expect(TokenType.Assign));
      Assert.Throws<SyntaxException>(() => lexer.Expect(TokenType.Equals));
    }

    [Test]
    public void ReadIfTypeTest() {
      var lexer = _createLexer("+ - * /");

      Assert.IsFalse(lexer.ReadIfType(TokenType.Assign));
      Assert.IsFalse(lexer.ReadIfType(TokenType.Assign));
      Assert.IsTrue(lexer.ReadIfType(TokenType.Add));
      Assert.IsFalse(lexer.ReadIfType(TokenType.Assign));
      Assert.IsTrue(lexer.ReadIfType(TokenType.Subtract));
      Assert.IsTrue(lexer.ReadIfType(TokenType.Multiply));
      Assert.IsFalse(lexer.ReadIfType(TokenType.Multiply));
      Assert.IsFalse(lexer.ReadIfType(TokenType.Assign));
      Assert.IsTrue(lexer.ReadIfType(TokenType.Divide));

      Assert.AreEqual(lexer.Read().Type, TokenType.None);  // EOF
      Assert.IsFalse(lexer.ReadIfType(TokenType.Add));
      Assert.IsFalse(lexer.ReadIfType(TokenType.Divide));
    }

    [Test]
    public void StringEscapes() {
      Token tok = _createLexer(@"'\'\""\\\n\a\b\f\r\t\v\x12\73\z    '").Read();
      Assert.AreEqual(tok.Value, "'\"\\\n\a\b\f\r\t\v\x12\x3b");

      Assert.Throws<SyntaxException>(() => _createLexer("'\\w'").Read());
      Assert.Throws<SyntaxException>(() => _createLexer("'\\q'").Read());
    }

    [Test]
    public void CommentErrorTest() {
      Assert.Throws<SyntaxException>(() => _createLexer("--[[ ").Read());
      Assert.Throws<SyntaxException>(() => _createLexer("--[[ ]").Read());
      Assert.Throws<SyntaxException>(() => _createLexer("--[[ ]=]").Read());
    }
  }
}
