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

namespace ModMaker.Lua.Parser {
  /// <summary>
  /// Defines possible types of tokens.
  /// </summary>
  public enum TokenType {
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
  public struct Token {
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
    /// The ending position of the token.
    /// </summary>
    public long EndPos;
    /// <summary>
    /// The ending line of the token.
    /// </summary>
    public long EndLine;

    /// <summary>
    /// Creates a new token with the given values.
    /// </summary>
    /// <param name="value">The string value of the token.</param>
    /// <param name="startPos">The starting position of the token.</param>
    /// <param name="startLine">The starting line of the token.</param>
    public Token(TokenType type, string value, long startPos, long startLine, long endPos = 0,
                 long endLine = 0) {
      Type = type;
      Value = value;
      StartPos = startPos;
      StartLine = startLine;
      EndPos = endPos;
      EndLine = endLine;
    }

    /// <summary>
    /// Checks whether two tokens are equal.
    /// </summary>
    /// <param name="lhs">The left-hand side.</param>
    /// <param name="rhs">The right-hand side.</param>
    /// <returns>True if the two token are equal, otherwise false.</returns>
    public static bool operator ==(Token lhs, Token rhs) {
      return lhs.Type == rhs.Type && lhs.Value == rhs.Value && lhs.StartPos == rhs.StartPos &&
          lhs.StartLine == rhs.StartLine && lhs.EndPos == rhs.EndPos && lhs.EndLine == rhs.EndLine;
    }

    /// <summary>
    /// Checks whether two tokens are not equal.
    /// </summary>
    /// <param name="lhs">The left-hand side.</param>
    /// <param name="rhs">The right-hand side.</param>
    /// <returns>True if the two token are not equal, otherwise false.</returns>
    public static bool operator !=(Token lhs, Token rhs) {
      return !(lhs == rhs);
    }

    /// <summary>
    /// Determines whether the specified System.Object is  equal to the current System.Object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>
    /// true if the specified System.Object is equal to the current System.Object; otherwise,
    /// false.
    /// </returns>
    public override bool Equals(object? obj) {
      Token? lhs = obj as Token?;
      return lhs.HasValue && lhs.Value == this;
    }

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
    public override int GetHashCode() {
#if NETFRAMEWORK
      return Value.GetHashCode() ^ Type.GetHashCode() ^ StartPos.GetHashCode() ^
             StartLine.GetHashCode() ^ EndPos.GetHashCode() ^ EndLine.GetHashCode();
#else
      return HashCode.Combine(Value, Type, StartPos, StartLine, EndPos, EndLine);
#endif
    }

    public override string ToString() {
      var need_value = Type switch {
        TokenType.Identifier => true,
        TokenType.NumberLiteral => true,
        TokenType.StringLiteral => true,
        _ => false,
      };
      var value = Value is string str && str.Length > 25 ? str.Substring(0, 25) + "..." : Value;
      var type = need_value ? $"{Type}({value})" : Type.ToString();
      return $"Token(Type={type}, Line={StartLine}, Pos={StartPos}, EndLine={EndLine}, " +
             $"EndPos={EndPos})";
    }
  }
}
