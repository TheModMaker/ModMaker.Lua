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
    /// Defines a single token read from the input stream.
    /// </summary>
    public struct Token
    {
        /// <summary>
        /// The string value of the token.
        /// </summary>
        public string Value;
        /// <summary>
        /// The starting position of the token.
        /// </summary>
        public long StartPos;
        /// <summary>
        /// The ending position of the token.
        /// </summary>
        public long EndPos;
        /// <summary>
        /// The starting line of the token.
        /// </summary>
        public long StartLine;
        /// <summary>
        /// The ending line of the token.
        /// </summary>
        public long EndLine;

        /// <summary>
        /// Creates a new token with the given values.
        /// </summary>
        /// <param name="value">The string value of the token.</param>
        /// <param name="startPos">The starting position of the token.</param>
        /// <param name="endPos">The ending position of the token.</param>
        /// <param name="startLine">The starting line of the token.</param>
        /// <param name="endLine">The ending line of the token.</param>
        public Token(string value, long startPos, long endPos, long startLine, long endLine)
        {
            this.Value = value;
            this.StartPos = startPos;
            this.EndPos = endPos;
            this.StartLine = startLine;
            this.EndLine = endLine;
        }

        /// <summary>
        /// Checks whether two tokens are equal.
        /// </summary>
        /// <param name="lhs">The left-hand side.</param>
        /// <param name="rhs">The right-hand side.</param>
        /// <returns>True if the two token are equal, otherwise false.</returns>
        public static bool operator ==(Token lhs, Token rhs)
        {
            return lhs.Value == rhs.Value && lhs.StartPos == rhs.StartPos &&
                lhs.EndPos == rhs.EndPos && lhs.StartLine == rhs.StartLine &&
                lhs.EndLine == rhs.EndLine;
        }
        /// <summary>
        /// Checks whether two tokens are not equal.
        /// </summary>
        /// <param name="lhs">The left-hand side.</param>
        /// <param name="rhs">The right-hand side.</param>
        /// <returns>True if the two token are not equal, otherwise false.</returns>
        public static bool operator !=(Token lhs, Token rhs)
        {
            return lhs.Value != rhs.Value || lhs.StartPos != rhs.StartPos ||
                lhs.EndPos != rhs.EndPos || lhs.StartLine != rhs.StartLine ||
                lhs.EndLine != rhs.EndLine;
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
            if (obj is Token)
            {
                Token rhs = (Token)obj;
                return Value == rhs.Value && StartPos == rhs.StartPos &&
                    EndPos == rhs.EndPos && StartLine == rhs.StartLine &&
                    EndLine == rhs.EndLine;
            }
            return false;
        }
        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer that is the hash code for this
        /// instance.</returns>
        public override int GetHashCode()
        {
            return Value.GetHashCode() ^
                (StartPos.GetHashCode() << 6 | StartPos.GetHashCode() >> 26) ^
                (EndPos.GetHashCode() << 12 | EndPos.GetHashCode() >> 20) ^
                (StartLine.GetHashCode() << 18 | StartLine.GetHashCode() >> 14) ^
                (EndLine.GetHashCode() << 24 | EndLine.GetHashCode() >> 8);
        }

        /// <summary>
        /// Appends the given token value to this token.
        /// </summary>
        /// <param name="other">The other token to add to.</param>
        public void Append(Token other)
        {
            this.EndLine = other.EndLine;
            this.EndPos = other.EndPos;
            this.Value += " " + other.Value;
            this.Value = this.Value.Trim();
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
        /// Pushes a token back onto the tokenizer.  This will allow to reverse
        /// a read.
        /// </summary>
        /// <param name="token">The token to push-back.</param>
        void PushBack(Token token);
    }
}
