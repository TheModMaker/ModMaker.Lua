// Copyright 2012 Jacob Trimble
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
  /// When there is an error in the syntax of Lua code.
  /// </summary>
  public sealed class SyntaxException : Exception {
    /// <summary>
    /// Creates a new SyntaxException with the given message.
    /// </summary>
    /// <param name="message">The cause of the exception.</param>
    /// <param name="source">The source DebugInfo that caused the exception.</param>
    public SyntaxException(string message, DebugInfo source) : this(message, source, null) { }
    /// <summary>
    /// Creates a new SyntaxException with the given message.
    /// </summary>
    /// <param name="message">The cause of the exception.</param>
    /// <param name="source">The source DebugInfo that caused the exception.</param>
    /// <param name="inner">The inner exception.</param>
    public SyntaxException(string message, DebugInfo source, Exception inner)
        : base("Error in the syntax of the file.\nMessage: " + message, inner) {
      Debug = source;
    }

    /// <summary>
    /// Gets the source DebugInfo that caused the error.
    /// </summary>
    public DebugInfo Debug { get; }
  }
}
