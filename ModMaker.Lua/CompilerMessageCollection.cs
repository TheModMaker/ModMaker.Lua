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

using System.Collections.Generic;
using System.Linq;

namespace ModMaker.Lua {
  /// <summary>
  /// Contains a collection of CompilerMessage objects that have happened.  This also handles
  /// creating exception objects and tracking if we need to throw.
  /// </summary>
  public sealed class CompilerMessageCollection : List<CompilerMessage> {
    public CompilerMessageCollection(MessageLevel errorLevel) {
      ErrorLevel = errorLevel < MessageLevel.Fatal ? MessageLevel.Fatal : errorLevel;
    }

    /// <summary>
    /// Contains the error level to throw at.  Any message at this level or higher will result in an
    /// exception being thrown during parsing.
    /// </summary>
    public MessageLevel ErrorLevel { get; }

    /// <summary>
    /// Gets whether there are messages above the error level and we should throw an exception.
    /// </summary>
    public bool ShouldThrow() {
      foreach (CompilerMessage msg in this) {
        if (msg.Level <= ErrorLevel)
          return true;
      }
      return false;
    }

    /// <summary>
    /// Makes an exception object based on the current collection.  The returned exception will only
    /// contain the messages that are above the error limit.
    /// </summary>
    /// <returns>The new exception object.</returns>
    public CompilerException MakeException() {
      var messages = this.Where(m => m.Level <= ErrorLevel).ToArray();
      if (messages.Length == 0)
        throw new System.InvalidOperationException("Must have some errors to throw");
      return new CompilerException(messages);
    }
  }
}
