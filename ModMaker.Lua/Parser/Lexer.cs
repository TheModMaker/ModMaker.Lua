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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ModMaker.Lua.Parser
{
    /// <summary>
    /// Defines a lexer that accepts a TextElementEnumerator and produces a
    /// stream of token for use in parsing.  It automatically ignores
    /// whitespace and comments.  This type can be extended to alter it's behavior.
    /// </summary>
    public class Lexer
    {
        readonly IDictionary<string, TokenType> tokens_ =
            new Dictionary<string, TokenType> {
                { "(",  TokenType.BeginParen },
                { ")", TokenType.EndParen },
                { "[", TokenType.BeginBracket },
                { "]", TokenType.EndBracket },
                { "{", TokenType.BeginTable },
                { "}", TokenType.EndTable },
                { ",", TokenType.Comma },
                { ";", TokenType.Semicolon },
                { ":", TokenType.Colon },
                { "::", TokenType.Label },
                { ".", TokenType.Indexer },
                { "..", TokenType.Concat },
                { "...", TokenType.Elipsis },
                { "+", TokenType.Add },
                { "-", TokenType.Subtract },
                { "*", TokenType.Multiply },
                { "/", TokenType.Divide },
                { "^", TokenType.Power },
                { "%", TokenType.Modulo },
                { "#", TokenType.Length },
                { "=", TokenType.Assign },
                { "==", TokenType.Equals },
                { "~=", TokenType.NotEquals },
                { ">", TokenType.Greater },
                { ">=", TokenType.GreaterEquals },
                { "<", TokenType.Less },
                { "<=", TokenType.LessEquals },
                { "@", TokenType.RefSymbol },

                { "and", TokenType.And },
                { "or", TokenType.Or },
                { "not", TokenType.Not },
                { "nil", TokenType.Nil },
                { "false", TokenType.False },
                { "true", TokenType.True },
                { "if", TokenType.If },
                { "then", TokenType.Then },
                { "elseif", TokenType.ElseIf },
                { "else", TokenType.Else },
                { "for", TokenType.For },
                { "do", TokenType.Do },
                { "while", TokenType.While },
                { "repeat", TokenType.Repeat },
                { "until", TokenType.Until },
                { "break", TokenType.Break },
                { "goto", TokenType.Goto },
                { "local", TokenType.Local },
                { "function", TokenType.Function },
                { "return", TokenType.Return },
                { "end", TokenType.End },
                { "in", TokenType.In },
                { "class", TokenType.Class },
                { "ref", TokenType.Ref },
            };

        /// <summary>
        /// Contains the previous peeks to support push-back.
        /// </summary>
        readonly Stack<Token> peek_;
        /// <summary>
        /// Contains the input to the lexer.
        /// </summary>
        readonly BufferedStringReader input_;

        /// <summary>
        /// Gets the name of the current file, used for throwing exceptions.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Creates a new Lexer object that will read from the given input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="name">The name of the input, used for debugging.</param>
        /// <exception cref="System.ArgumentNullException">If input is null.</exception>
        public Lexer(Stream input, Encoding encoding, string name)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            input_ = new BufferedStringReader(input, encoding);
            peek_ = new Stack<Token>();
            Name = name;
        }

        /// <summary>
        /// Reads a single token from the input stream and progresses the input.
        /// </summary>
        /// <returns>The token that was read.</returns>
        public Token Read()
        {
            if (peek_.Count > 0)
                return peek_.Pop();
            else
                return InternalRead();
        }
        /// <summary>
        /// Reads a single token but does not progress the input.
        /// </summary>
        /// <returns>The token that was read.</returns>
        public Token Peek()
        {
            if (peek_.Count == 0)
                peek_.Push(InternalRead());

            return peek_.Peek();
        }
        /// <summary>
        /// Expects the given type to be next.  If not, this throws an
        /// exception.  Otherwise this returns the read token.
        /// </summary>
        /// <param name="type">The type to expect.</param>
        /// <returns>The token that was read.</returns>
        public Token Expect(TokenType type)
        {
            Token read = Peek();
            if (read.Type == TokenType.None)
            {
                throw SyntaxError($"Unexpected EOF waiting for '{type}'");
            }
            if (read.Type != type)
            {
                throw SyntaxError($"Found '{read.Value}', expecting '{type}'.", read);
            }
            return Read();
        }
        /// <summary>
        /// Returns whether the next token is of the given type.
        /// </summary>
        public bool PeekType(TokenType type)
        {
            return Peek().Type == type;
        }
        /// <summary>
        /// Reads the next token if it is the given type.
        /// </summary>
        /// <param name="type">The type of token.</param>
        /// <returns>Whether a token was read.</returns>
        public bool ReadIfType(TokenType type)
        {
            if (!PeekType(type))
                return false;

            Read();
            return true;
        }


        /// <summary>
        /// Returns a syntax error object at the current position.
        /// </summary>
        /// <param name="message">The message of the error.</param>
        /// <param name="token">An optional token object to replace the current token.</param>
        /// <returns>A new SyntaxException object.</returns>
        public SyntaxException SyntaxError(string message, Token? token = null)
        {
            return new SyntaxException(
                message, Name,
                token ?? new Token(
                    TokenType.None, input_.Peek(1), input_.Column, input_.Line));
        }


        /// <summary>
        /// Reads a single token from the input stream.
        /// </summary>
        /// <returns>The token that was read or a null string token.</returns>
        /// <remarks>
        /// If it is at the end of the enumeration, it will return a token with
        /// a null string, the values of the other members are unspecified.
        /// </remarks>
        protected virtual Token InternalRead()
        {
            ReadWhitespace();
            while (input_.Peek(2) == "--")
            {
                ReadComment();
                ReadWhitespace();
            }

            // Detect long-strings first, otherwise it will be detected as an
            // indexer.
            if (input_.Peek(1) == "[")
            {
                int depth = 0;
                while (input_.Peek(depth + 2).EndsWith("="))
                {
                    depth++;
                }
                if (input_.Peek(depth + 2).EndsWith("["))
                {
                    Token retStr = new Token(
                        TokenType.StringLiteral, "", input_.Column, input_.Line);
                    string end = "]" + new string('=', depth) + "]";
                    input_.Read(depth + 2);
                    retStr.Value = input_.ReadUntil(end);
                    if (!retStr.Value.EndsWith(end))
                    {
                        throw SyntaxError(string.Format(Resources.UnexpectedEOF, "long string"));
                    }
                    // TODO: Remove once token type is set.  This ensures the
                    // calling code knows it's a string.
                    retStr.Value = "\"" + retStr.Value.Substring(
                        0, retStr.Value.Length - end.Length).Replace("\r\n", "\n");
                    return retStr;
                }
            }

            string first = input_.Peek(1);
            if (first == "")
                return new Token();
            else if (first == "_" || char.IsLetter(first, 0))
                return ReadIdentifier();
            else if (tokens_.ContainsKey(input_.Peek(3)))
                return ReadToken(3);
            else if (tokens_.ContainsKey(input_.Peek(2)))
                return ReadToken(2);
            else if (tokens_.ContainsKey(first))
                return ReadToken(1);
            else if (IsDigit(first) || first == ".")
                return ReadNumber();
            else if (first == "\"" || first == "'")
                return ReadString();

            throw SyntaxError("Invalid token");
        }

        /// <summary>
        /// Helper function that reads any current whitespace.
        /// </summary>
        protected virtual void ReadWhitespace()
        {
            input_.ReadWhile((ch) => char.IsWhiteSpace(ch, 0));
        }

        /// <summary>
        /// Helper function that reads a comment from the input.
        /// </summary>
        protected virtual void ReadComment()
        {
            if (input_.Peek(2) != "--")
                return;

            Token debug = new Token(TokenType.None, "", input_.Column, input_.Line);
            debug.Value += input_.Read(2);
            string endStr = null;
            if (input_.Peek(1) == "[")
            {
                debug.Value += input_.Read(1);
                string temp = input_.ReadWhile((ch) => ch == "=");
                debug.Value += temp;
                if (input_.Peek(1) == "[")
                {
                    debug.Value += input_.Read(1);
                    endStr = "]" + temp + "]";
                }
            }

            string read = input_.ReadUntil(endStr ?? "\n");
            debug.Value += read;

            if (endStr != null && !read.EndsWith(endStr))
            {
                throw new SyntaxException(
                    string.Format(Resources.MissingEnd, "long comment"), debug);
            }
        }

        /// <summary>
        /// Helper function that reads a string from the input.
        /// </summary>
        /// <returns>
        /// The token that represents the string read.
        /// </returns>
        protected virtual Token ReadString()
        {
            string end = input_.Peek(1);
            if (end != "\"" && end != "'")
                throw new ArgumentException("Not currently at a string.");

            Token ret = new Token(
                TokenType.StringLiteral, "", input_.Column, input_.Line);
            input_.Read(1);
            ret.Value = input_.ReadUntil(end);
            while (ret.Value.EndsWith("\\" + end))
            {
                ret.Value += input_.ReadUntil(end);
            }

            if (ret.Value.Contains("\n"))
                throw SyntaxError("Cannot have newline in string.");
            if (!ret.Value.EndsWith(end))
                throw SyntaxError("Unexpected EOF in string.");

            ret.Value = Regex.Replace(ret.Value, @"\\(x(\d\d)|(\d\d?\d?)|(z\s+)|.)", (match) =>
            {
                string hex = match.Groups[2].Value;
                string oct = match.Groups[3].Value;
                if (hex != "")
                    return new string((char)Convert.ToInt32(hex, 16), 1);
                else if (oct != "")
                    return new string((char)Convert.ToInt32(oct, 8), 1);
                else if (match.Groups[4].Value != "")
                    return "";

                string val = match.Groups[1].Value;
                switch (val)
                {
                    case "'":
                    case "\"":
                    case "\\":
                    case "\n":
                        return val;
                    case "n":
                        return "\n";
                    case "a":
                        return "\a";
                    case "b":
                        return "\b";
                    case "f":
                        return "\f";
                    case "r":
                        return "\r";
                    case "t":
                        return "\t";
                    case "v":
                        return "\v";
                    default:
                        throw new SyntaxException(
                            string.Format(Resources.InvalidEscape, val),
                            Name, ret);
                }
            });
            ret.Value = ret.Value.Substring(0, ret.Value.Length - 1);
            return ret;
        }

        /// <summary>
        /// Helper function that reads a number from the input.
        /// </summary>
        /// <returns>The token that was read.</returns>
        protected virtual Token ReadNumber()
        {
            Token ret = new Token(
                TokenType.NumberLiteral, "", input_.Column, input_.Line);

            string expLetter = "eE";
            Predicate<string> isDigit = IsDigit;
            if (input_.Peek(2) == "0x" || input_.Peek(2) == "0X")
            {
                ret.Value = input_.Read(2);
                expLetter = "pP";
                isDigit = IsHexDigit;
            }

            ret.Value += input_.ReadWhile(isDigit);
            if (input_.Peek(1) == ".")
                ret.Value += input_.Read(1) + input_.ReadWhile(isDigit);
            if (expLetter.Contains(input_.Peek(1)))
            {
                ret.Value += input_.Read(1);
                if ("-+".Contains(input_.Peek(1)))
                    ret.Value += input_.Read(1);
                ret.Value +=
                    input_.ReadWhile(IsDigit);
            }
            return ret;
        }

        /// <summary>
        /// Helper function that reads an identifier from the input.
        /// </summary>
        /// <returns>The token that was read.</returns>
        protected virtual Token ReadIdentifier()
        {
            Predicate<string> isWord =
                (ch) => ch == "_" || char.IsLetterOrDigit(ch, 0);
            Token ret = new Token(TokenType.None, "", input_.Column, input_.Line);
            ret.Value = input_.ReadWhile(isWord);
            if (!tokens_.TryGetValue(ret.Value, out ret.Type))
                ret.Type = TokenType.Identifier;

            if (input_.Peek(1) == "`")
            {
                if (ret.Type != TokenType.Identifier)
                    throw SyntaxError("Cannot use overload with reserved keyword.");
                ret.Value += input_.Read(1);
                string temp = input_.ReadWhile(IsDigit);
                if (temp == "")
                    throw SyntaxError("Must have at least one number in overload.");
                ret.Value += temp;
            }
            return ret;
        }


        /// <summary>
        /// Reads a token of the given length and returns it.
        /// </summary>
        Token ReadToken(int length)
        {
            // To avoid confusion and implementation-defined behavior for the
            // order of evaluation of arguments, this does the read last so the
            // position is at the start.
            var ret = new Token(TokenType.Identifier, "", input_.Column, input_.Line);
            ret.Value = input_.Read(length);
            ret.Type = tokens_[ret.Value];
            return ret;
        }

        /// <summary>
        /// Returns whether the given text element is an ASCII digit.
        /// </summary>
        static bool IsDigit(string str)
        {
            return "0123456789".Contains(str[0]);
        }

        /// <summary>
        /// Returns whether the given text element is a hex digit.
        /// </summary>
        static bool IsHexDigit(string str)
        {
            return "0123456789abcdefABCDEF".Contains(str[0]);
        }
    }
}
