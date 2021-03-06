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

namespace ModMaker.Lua.Parser.Items {
  /// <summary>
  /// Defines a parse item that represents a goto statement.
  /// </summary>
  public sealed class GotoItem : IParseStatement {
    /// <summary>
    /// Creates a new GotoItem with the given name.
    /// </summary>
    /// <param name="label">The name of the target label.</param>
    /// <exception cref="System.ArgumentNullException">If label is null.</exception>
    public GotoItem(string label) {
      if (label == null) {
        throw new ArgumentNullException(nameof(label));
      }

      Name = label;
    }

    /// <summary>
    /// Gets or sets the target name of the goto.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Gets or sets the destination of the goto.
    /// </summary>
    public LabelItem Target { get; set; }
    public Token Debug { get; set; }
    public object UserData { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      if (visitor == null) {
        throw new ArgumentNullException(nameof(visitor));
      }

      return visitor.Visit(this);
    }
  }
}
