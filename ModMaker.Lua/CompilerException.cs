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

namespace ModMaker.Lua {
  /// <summary>
  /// Defines an exception for when an error happens.  This contains one or more CompilerMessage
  /// objects that describe the errors that happened.
  /// </summary>
  public sealed class CompilerException : Exception {
    public CompilerException(CompilerMessage[] messages)
        : base(messages.Length == 1 ? messages[0].Message : "Multiple errors happened") {
      Errors = messages;
    }

    /// <summary>
    /// Gets the messages that describe the errors.
    /// </summary>
    public CompilerMessage[] Errors { get; }
  }
}