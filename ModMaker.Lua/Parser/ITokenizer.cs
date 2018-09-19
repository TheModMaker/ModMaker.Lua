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

namespace ModMaker.Lua.Parser
{
    /// <summary>
    /// Defines possible types of tokens.
    /// </summary>
    public enum TokenType
    {
        None,

        /// <summary>
        /// 'foo'
        /// </summary>
        Identifier,
        /// <summary>
        /// '"123"'
        /// </summary>
        StringLiteral,
        /// <summary>
        /// '123'
        /// </summary>
        NumberLiteral,

        /// <summary>
        /// 'and'
        /// </summary>
        And,
        /// <summary>
        /// 'or'
        /// </summary>
        Or,
        /// <summary>
        /// 'not'
        /// </summary>
        Not,
        /// <summary>
        /// 'nil'
        /// </summary>
        Nil,
        /// <summary>
        /// 'false'
        /// </summary>
        False,
        /// <summary>
        /// 'true'
        /// </summary>
        True,

        /// <summary>
        /// 'if'
        /// </summary>
        If,
        /// <summary>
        /// 'then'
        /// </summary>
        Then,
        /// <summary>
        /// 'elseif'
        /// </summary>
        ElseIf,
        /// <summary>
        /// 'else'
        /// </summary>
        Else,
        /// <summary>
        /// 'for'
        /// </summary>
        For,
        /// <summary>
        /// 'do'
        /// </summary>
        Do,
        /// <summary>
        /// 'while'
        /// </summary>
        While,
        /// <summary>
        /// 'repeat'
        /// </summary>
        Repeat,
        /// <summary>
        /// 'until'
        /// </summary>
        Until,
        /// <summary>
        /// 'break'
        /// </summary>
        Break,
        /// <summary>
        /// 'goto'
        /// </summary>
        Goto,
        /// <summary>
        /// 'local'
        /// </summary>
        Local,
        /// <summary>
        /// 'function'
        /// </summary>
        Function,
        /// <summary>
        /// 'end'
        /// </summary>
        End,
        /// <summary>
        /// 'in'
        /// </summary>
        In,
        /// <summary>
        /// 'return'
        /// </summary>
        Return,

        /// <summary>
        /// 'class'
        /// </summary>
        Class,
        /// <summary>
        /// 'ref'
        /// </summary>
        Ref,
        /// <summary>
        /// '@'
        /// </summary>
        RefSymbol,

        /// <summary>
        /// '('
        /// </summary>
        BeginParen,
        /// <summary>
        /// ')'
        /// </summary>
        EndParen,
        /// <summary>
        /// '['
        /// </summary>
        BeginBracket,
        /// <summary>
        /// ']'
        /// </summary>
        EndBracket,
        /// <summary>
        /// '{'
        /// </summary>
        BeginTable,
        /// <summary>
        /// '}'
        /// </summary>
        EndTable,
        /// <summary>
        /// ','
        /// </summary>
        Comma,
        /// <summary>
        /// ';'
        /// </summary>
        Semicolon,
        /// <summary>
        /// ':'
        /// </summary>
        Colon,
        /// <summary>
        /// '::'
        /// </summary>
        Label,
        /// <summary>
        /// '.'
        /// </summary>
        Indexer,
        /// <summary>
        /// '..'
        /// </summary>
        Concat,
        /// <summary>
        /// '...'
        /// </summary>
        Elipsis,

        /// <summary>
        /// '+'
        /// </summary>
        Add,
        /// <summary>
        /// '-'
        /// </summary>
        Subtract,
        /// <summary>
        /// '*'
        /// </summary>
        Multiply,
        /// <summary>
        /// '/'
        /// </summary>
        Divide,
        /// <summary>
        /// '^'
        /// </summary>
        Power,
        /// <summary>
        /// '%'
        /// </summary>
        Modulo,
        /// <summary>
        /// '#'
        /// </summary>
        Length,

        /// <summary>
        /// '='
        /// </summary>
        Assign,
        /// <summary>
        /// '=='
        /// </summary>
        Equals,
        /// <summary>
        /// '~='
        /// </summary>
        NotEquals,
        /// <summary>
        /// '&gt;'
        /// </summary>
        Greater,
        /// <summary>
        /// '&gt;='
        /// </summary>
        GreaterEquals,
        /// <summary>
        /// '&lt;'
        /// </summary>
        Less,
        /// <summary>
        /// '&lt;='
        /// </summary>
        LessEquals,
    }

    /// <summary>
    /// Defines a single token read from the input stream.
    /// </summary>
    public struct Token
    {
        /// <summary>
        /// The type the token is.
        /// </summary>
        public TokenType Type;
        /// <summary>
        /// The string value of the token.
        /// </summary>
        public string Value;
        /// <summary>
        /// The starting position of the token.
        /// </summary>
        public long StartPos;
        /// <summary>
        /// The starting line of the token.
        /// </summary>
        public long StartLine;

        /// <summary>
        /// Creates a new token with the given values.
        /// </summary>
        /// <param name="value">The string value of the token.</param>
        /// <param name="startPos">The starting position of the token.</param>
        /// <param name="startLine">The starting line of the token.</param>
        public Token(TokenType type, string value, long startPos,
                     long startLine)
        {
            this.Type = type;
            this.Value = value;
            this.StartPos = startPos;
            this.StartLine = startLine;
        }

        /// <summary>
        /// Checks whether two tokens are equal.
        /// </summary>
        /// <param name="lhs">The left-hand side.</param>
        /// <param name="rhs">The right-hand side.</param>
        /// <returns>True if the two token are equal, otherwise false.</returns>
        public static bool operator ==(Token lhs, Token rhs)
        {
            return lhs.Type == rhs.Type && lhs.Value == rhs.Value &&
                lhs.StartPos == rhs.StartPos && lhs.StartLine == rhs.StartLine;
        }

        /// <summary>
        /// Checks whether two tokens are not equal.
        /// </summary>
        /// <param name="lhs">The left-hand side.</param>
        /// <param name="rhs">The right-hand side.</param>
        /// <returns>True if the two token are not equal, otherwise false.</returns>
        public static bool operator !=(Token lhs, Token rhs)
        {
            return lhs.Type != rhs.Type || lhs.Value != rhs.Value ||
                lhs.StartPos != rhs.StartPos || lhs.StartLine != rhs.StartLine;
        }


        /// <summary>
        /// Determines whether the specified System.Object is  equal to the
        /// current System.Object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified System.Object is equal to the current
        /// System.Object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            Token? lhs = obj as Token?;
            return lhs.HasValue && lhs.Value == this;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer that is the hash code for this
        /// instance.</returns>
        public override int GetHashCode()
        {
            return Value.GetHashCode() ^
                (Type.GetHashCode() << 6 | Type.GetHashCode() >> 26) ^
                (StartPos.GetHashCode() << 12 | StartPos.GetHashCode() >> 20) ^
                (StartLine.GetHashCode() << 18 | StartLine.GetHashCode() >> 14);
        }
    }

    /// <summary>
    /// Defines an object that produces a sequence of tokens for parsing.
    /// The default implementation is Tokenizer and reads from a string.
    /// </summary>
    public interface ITokenizer
    {
        /// <summary>
        /// Gets the name of the current file, used for throwing exceptions.
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Gets the current (one-based) position in the current line.
        /// </summary>
        long Position { get; }
        /// <summary>
        /// Gets the current (one-based) line number.
        /// </summary>
        long Line { get; }

        /// <summary>
        /// Throws a syntax error at the current position.
        /// </summary>
        /// <param name="message">The message of the error.</param>
        void SyntaxError(string message, Token? token = null);

        /// <summary>
        /// Reads a single token from the input stream and progresses the input.
        /// </summary>
        /// <returns>The token that was read.</returns>
        Token Read();
        /// <summary>
        /// Reads a single token but does not progress the input.
        /// </summary>
        /// <returns>The token that was read.</returns>
        Token Peek();
        /// <summary>
        /// Expects the given type to be next.  If not, this throws an
        /// exception.  Otherwise this returns the read token.
        /// </summary>
        /// <param name="type">The type to expect.</param>
        /// <returns>The token that was read.</returns>
        Token Expect(TokenType type);
        /// <summary>
        /// Returns whether the next token is of the given type.
        /// </summary>
        bool PeekType(TokenType type);
        /// <summary>
        /// Reads the next token if it is the given type.
        /// </summary>
        /// <param name="type">The type of token.</param>
        /// <returns>Whether a token was read.</returns>
        bool ReadIfType(TokenType type);
    }
}
