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
using System.Text.RegularExpressions;

namespace ModMaker.Lua.Parser {
  /// <summary>
  /// Defines a lexer that accepts a TextElementEnumerator and produces a stream of token for use in
  /// parsing.  It automatically ignores whitespace and comments.  This type can be extended to
  /// alter it's behavior.
  /// </summary>
  public class Lexer {
    readonly IDictionary<string, TokenType> _tokens =
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
    readonly Stack<Token> _peek;
    /// <summary>
    /// Contains the input to the lexer.
    /// </summary>
    readonly BufferedStringReader _input;
    /// <summary>
    /// Contains the messages for the parser/lexer.
    /// </summary>
    readonly CompilerMessageCollection _messages;

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
    public Lexer(CompilerMessageCollection messages, BufferedStringReader input, string name) {
      if (input == null) {
        throw new ArgumentNullException(nameof(input));
      }

      _input = input;
      _peek = new Stack<Token>();
      _messages = messages;
      Name = name;
    }

    /// <summary>
    /// Reads a single token from the input stream and progresses the input.
    /// </summary>
    /// <returns>The token that was read.</returns>
    public Token Read() {
      if (_peek.Count > 0) {
        return _peek.Pop();
      } else {
        return _internalRead();
      }
    }
    /// <summary>
    /// Reads a single token but does not progress the input.
    /// </summary>
    /// <returns>The token that was read.</returns>
    public Token Peek() {
      if (_peek.Count == 0) {
        _peek.Push(_internalRead());
      }

      return _peek.Peek();
    }
    /// <summary>
    /// Expects the given type to be next.  If not, this throws an exception.  Otherwise this
    /// returns the read token.
    /// </summary>
    /// <param name="type">The type to expect.</param>
    /// <returns>The token that was read.</returns>
    public Token Expect(TokenType type) {
      Token read = Peek();
      if (read.Type == TokenType.None) {
        SyntaxError(MessageId.UnexpectedEof, null, $"Unexpected EOF waiting for '{type}'");
        throw MakeException();
      }
      if (read.Type != type) {
        SyntaxError(MessageId.UnexpectedToken, read, $"Found '{read.Value}', expecting '{type}'.");
        throw MakeException();
      }
      return Read();
    }
    /// <summary>
    /// Returns whether the next token is of the given type.
    /// </summary>
    public bool PeekType(TokenType type) {
      return Peek().Type == type;
    }
    /// <summary>
    /// Reads the next token if it is the given type.
    /// </summary>
    /// <param name="type">The type of token.</param>
    /// <returns>Whether a token was read.</returns>
    public bool ReadIfType(TokenType type) {
      if (!PeekType(type)) {
        return false;
      }

      Read();
      return true;
    }

    /// <summary>
    /// Adds a new syntax error to the message collection.
    /// </summary>
    /// <param name="id">The message ID to use.</param>
    /// <param name="token">An optional token object to replace the current token.</param>
    /// <param name="message">The message of the error.</param>
    public void SyntaxError(MessageId id, Token? token = null, string message = null) {
      if (token == null && _peek.Count > 0) {
        token = _peek.Peek();
      }
      token ??= new Token(TokenType.None, _input.Peek(1), _input.Column, _input.Line);
      DebugInfo debug = new DebugInfo(Name, token.Value.StartPos, token.Value.StartLine,
                                      token.Value.StartPos + token.Value.Value.Length,
                                      token.Value.StartLine);
      _messages.Add(new CompilerMessage(MessageLevel.Error, id, debug, message));
    }
    /// <summary>
    /// Creates a CompilerException based on the current error messages.
    /// </summary>
    public CompilerException MakeException() {
      return _messages.MakeException();
    }

    /// <summary>
    /// Reads a single token from the input stream.
    /// </summary>
    /// <returns>The token that was read or a null string token.</returns>
    /// <remarks>
    /// If it is at the end of the enumeration, it will return a token with a null string, the
    /// values of the other members are unspecified.
    /// </remarks>
    protected virtual Token _internalRead() {
      _readWhitespace();
      while (_input.Peek(2) == "--") {
        _readComment();
        _readWhitespace();
      }

      // Detect long-strings first, otherwise it will be detected as an indexer.
      if (_input.Peek(1) == "[") {
        int depth = 0;
        while (_input.Peek(depth + 2).EndsWith("=")) {
          depth++;
        }
        if (_input.Peek(depth + 2).EndsWith("[")) {
          Token retStr = new Token(TokenType.StringLiteral, "", _input.Column, _input.Line);
          string end = "]" + new string('=', depth) + "]";
          _input.Read(depth + 2);
          retStr.Value = _input.ReadUntil(end);
          if (!retStr.Value.EndsWith(end)) {
            SyntaxError(MessageId.UnexpectedEof);
            throw MakeException();
          }
          retStr.Value = retStr.Value.Substring(0, retStr.Value.Length - end.Length)
              .Replace("\r\n", "\n");
          retStr.EndPos = _input.Column;
          retStr.EndLine = _input.Line;
          return retStr;
        }
      }

      string first = _input.Peek(1);
      if (first == "") {
        return new Token(TokenType.None, "", _input.Column, _input.Line, _input.Column,
                         _input.Line);
      } else if (first == "_" || char.IsLetter(first, 0)) {
        return _readIdentifier();
      } else if (_tokens.ContainsKey(_input.Peek(3))) {
        return _readToken(3);
      } else if (_tokens.ContainsKey(_input.Peek(2))) {
        return _readToken(2);
      } else if (_tokens.ContainsKey(first)) {
        return _readToken(1);
      } else if (_isDigit(first) || first == ".") {
        return _readNumber();
      } else if (first == "\"" || first == "'") {
        return _readString();
      }

      SyntaxError(MessageId.UnknownToken);
      throw MakeException();
    }

    /// <summary>
    /// Helper function that reads any current whitespace.
    /// </summary>
    protected virtual void _readWhitespace() {
      _input.ReadWhile((ch) => char.IsWhiteSpace(ch, 0));
    }

    /// <summary>
    /// Helper function that reads a comment from the input.
    /// </summary>
    protected virtual void _readComment() {
      if (_input.Peek(2) != "--") {
        return;
      }

      Token debug = new Token(TokenType.None, "", _input.Column, _input.Line);
      debug.Value += _input.Read(2);
      string endStr = null;
      if (_input.Peek(1) == "[") {
        debug.Value += _input.Read(1);
        string temp = _input.ReadWhile((ch) => ch == "=");
        debug.Value += temp;
        if (_input.Peek(1) == "[") {
          debug.Value += _input.Read(1);
          endStr = "]" + temp + "]";
        }
      }

      string read = _input.ReadUntil(endStr ?? "\n");
      debug.Value += read;

      if (endStr != null && !read.EndsWith(endStr)) {
        SyntaxError(MessageId.UnexpectedEof, debug);
        throw MakeException();
      }
    }

    /// <summary>
    /// Helper function that reads a string from the input.
    /// </summary>
    /// <returns>The token that represents the string read.</returns>
    protected virtual Token _readString() {
      string end = _input.Peek(1);
      if (end != "\"" && end != "'") {
        throw new ArgumentException("Not currently at a string.");
      }

      Token ret = new Token(TokenType.StringLiteral, "", _input.Column, _input.Line);
      _input.Read(1);
      ret.Value = _input.ReadUntil(end);
      while (ret.Value.EndsWith("\\" + end)) {
        ret.Value += _input.ReadUntil(end);
      }

      if (ret.Value.Contains("\n")) {
        SyntaxError(MessageId.NewlineInStringLiteral);
      } else if (!ret.Value.EndsWith(end)) {
        SyntaxError(MessageId.UnexpectedEof);
      }

      ret.Value = Regex.Replace(ret.Value, @"\\(x(\d\d)|(\d\d?\d?)|(z\s+)|.)", (match) => {
        string hex = match.Groups[2].Value;
        string oct = match.Groups[3].Value;
        if (hex != "") {
          return new string((char)Convert.ToInt32(hex, 16), 1);
        } else if (oct != "") {
          return new string((char)Convert.ToInt32(oct, 8), 1);
        } else if (match.Groups[4].Value != "") {
          return "";
        }

        string val = match.Groups[1].Value;
        switch (val) {
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
            SyntaxError(MessageId.InvalidEscapeInString, ret);
            return "\\" + val;
        }
      });
      ret.Value = ret.Value.Substring(0, ret.Value.Length - 1);
      ret.EndPos = _input.Column;
      ret.EndLine = _input.Line;
      return ret;
    }

    /// <summary>
    /// Helper function that reads a number from the input.
    /// </summary>
    /// <returns>The token that was read.</returns>
    protected virtual Token _readNumber() {
      Token ret = new Token(TokenType.NumberLiteral, "", _input.Column, _input.Line);

      string expLetter = "eE";
      Predicate<string> isDigit = _isDigit;
      if (_input.Peek(2) == "0x" || _input.Peek(2) == "0X") {
        ret.Value = _input.Read(2);
        expLetter = "pP";
        isDigit = _isHexDigit;
      }

      ret.Value += _input.ReadWhile(isDigit);
      if (_input.Peek(1) == ".") {
        ret.Value += _input.Read(1) + _input.ReadWhile(isDigit);
      }
      if (expLetter.Contains(_input.Peek(1))) {
        ret.Value += _input.Read(1);
        if ("-+".Contains(_input.Peek(1))) {
          ret.Value += _input.Read(1);
        }
        ret.Value += _input.ReadWhile(_isDigit);
      }
      ret.EndPos = _input.Column;
      ret.EndLine = _input.Line;
      return ret;
    }

    /// <summary>
    /// Helper function that reads an identifier from the input.
    /// </summary>
    /// <returns>The token that was read.</returns>
    protected virtual Token _readIdentifier() {
      Predicate<string> isWord = (ch) => ch == "_" || char.IsLetterOrDigit(ch, 0);
      Token ret = new Token(TokenType.None, "", _input.Column, _input.Line);
      ret.Value = _input.ReadWhile(isWord);
      if (!_tokens.TryGetValue(ret.Value, out ret.Type)) {
        ret.Type = TokenType.Identifier;
      }

      ret.EndPos = _input.Column;
      ret.EndLine = _input.Line;
      return ret;
    }

    /// <summary>
    /// Reads a token of the given length and returns it.
    /// </summary>
    Token _readToken(int length) {
      // To avoid confusion and implementation-defined behavior for the order of evaluation of
      // arguments, this does the read last so the position is at the start.
      var ret = new Token(TokenType.Identifier, "", _input.Column, _input.Line);
      ret.Value = _input.Read(length);
      ret.Type = _tokens[ret.Value];
      ret.EndPos = _input.Column;
      ret.EndLine = _input.Line;
      return ret;
    }

    /// <summary>
    /// Returns whether the given text element is an ASCII digit.
    /// </summary>
    static bool _isDigit(string str) {
      return "0123456789".Contains(str);
    }

    /// <summary>
    /// Returns whether the given text element is a hex digit.
    /// </summary>
    static bool _isHexDigit(string str) {
      return "0123456789abcdefABCDEF".Contains(str);
    }
  }
}
