// Copyright 2021 Jacob Trimble
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
using ModMaker.Lua.Parser;

namespace ModMaker.Lua {
  public enum MessageLevel : byte {
    /// <summary>
    /// A fatal error; this cannot be ignored.  For example, invalid Lua syntax.
    /// </summary>
    Error = 1,
    /// <summary>
    /// A warning message; the program may behave unexpectedly, but is valid.  For example, trying
    /// to add invalid literal types.
    /// </summary>
    Warning = 2,
    /// <summary>
    /// An info message; the program will behave normally, but may be inefficient or may behave
    /// oddly.  For example, replacing built-in library methods.
    /// </summary>
    Info = 3,
  }

  public enum MessageId : int {
    /// <summary>
    /// Syntax error: Unknown token.
    /// </summary>
    UnknownToken = 1000,
    /// <summary>
    /// Syntax error: Expecting one token, but got a different one.
    /// </summary>
    UnexpectedToken = 1001,
    /// <summary>
    /// Syntax error: Cannot have newline in string literal.
    /// </summary>
    NewlineInStringLiteral = 1002,
    /// <summary>
    /// Syntax error: Invalid escape in string literal.
    /// </summary>
    InvalidEscapeInString = 1003,
    /// <summary>
    /// Syntax error: There was an end of file while reading something.
    /// </summary>
    UnexpectedEof = 1004,
    /// <summary>
    /// Syntax error: Expecting end of file.
    /// </summary>
    ExpectingEof = 1005,
    /// <summary>
    /// Syntax error: The token was invalid for the start of a statement.
    /// </summary>
    ExpectedStatementStart = 1006,
    /// <summary>
    /// Syntax error: A local definition must use identifiers.
    /// </summary>
    LocalMustBeIdentifier = 1007,
    /// <summary>
    /// Syntax error: The left-hand-side of assignment must be a variable (e.g. a name).
    /// </summary>
    AssignmentMustBeVariable = 1008,
    /// <summary>
    /// Syntax error: Function cannot have a name when used as an expression.
    /// </summary>
    FunctionNameWhenExpression = 1009,
    /// <summary>
    /// Syntax error: Function name must be provided when used as a statement.
    /// </summary>
    FunctionNameWhenStatement = 1010,
    /// <summary>
    /// Syntax error: Table keys must be identifiers.
    /// </summary>
    TableKeyMustBeName = 1011,
    /// <summary>
    /// The label for a "goto" wasn't found.
    /// </summary>
    LabelNotFound = 1012,
    /// <summary>
    /// Local functions cannot have member names.
    /// </summary>
    LocalInstanceName = 1013,
    /// <summary>
    /// Local functions cannot use indexers.
    /// </summary>
    LocalMethodIndexer = 1014,
  }

  /// <summary>
  /// Describes errors, warnings, and info messages.
  /// </summary>
  public sealed class CompilerMessage {
    /// <summary>
    /// Creates a new CompilerMessage with the given info.
    /// </summary>
    /// <param name="level">The level the message is.</param>
    /// <param name="id">The ID of the message.</param>
    /// <param name="source">The source DebugInfo that caused the exception.</param>
    public CompilerMessage(MessageLevel level, MessageId id, DebugInfo source,
                           string? message = null) {
      Debug = source;
      Level = level;
      ID = id;
      Message = message ?? _defaultMessage(id);
    }

    static string _defaultMessage(MessageId id) {
      return id switch {
        MessageId.UnknownToken => "Unknown token",
        MessageId.UnexpectedToken => "Expecting one token, but got a different one",
        MessageId.NewlineInStringLiteral => "Cannot have newline in string literal",
        MessageId.InvalidEscapeInString => "Invalid escape in string literal",
        MessageId.UnexpectedEof => "Unexpected end-of-file",
        MessageId.ExpectingEof => "Expecting end-of-file",
        MessageId.ExpectedStatementStart => "Expecting start of a statement",
        MessageId.LocalMustBeIdentifier => "Local variable name must be an identifier",
        MessageId.AssignmentMustBeVariable => "Assignment must have variable on left-hand-side",
        MessageId.FunctionNameWhenExpression =>
            "Function cannot have a name when used as an expression",
        MessageId.FunctionNameWhenStatement =>
            "Function name must be provided when used as a statement",
        MessageId.TableKeyMustBeName => "Table keys must be identifiers",
        MessageId.LabelNotFound => "Label for goto not found or not visible",
        MessageId.LocalInstanceName => "Local functions cannot have member names",
        MessageId.LocalMethodIndexer => "Local functions cannot use indexers",

        _ => throw new ArgumentException("Invalid message ID"),
      };
    }

    /// <summary>
    /// Gets the source DebugInfo that caused the error.
    /// </summary>
    public DebugInfo Debug { get; }
    /// <summary>
    /// Gets the level of the message.
    /// </summary>
    public MessageLevel Level { get; }
    /// <summary>
    /// Gets the ID of the message.
    /// </summary>
    public MessageId ID { get; }
    /// <summary>
    /// Gets the text of the message.
    /// </summary>
    public string Message { get; }
  }
}