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
  /// Defines a parse item that represents a label.
  /// </summary>
  public sealed class LabelItem : IParseStatement {
    /// <summary>
    /// Creates a new instance of LabelItem with the given name.
    /// </summary>
    /// <param name="name">The name of the label.</param>
    public LabelItem(string name) {
      if (name == null) {
        throw new ArgumentNullException(nameof(name));
      }

      Name = name;
    }

    /// <summary>
    /// Gets or sets the name of the label.
    /// </summary>
    public string Name { get; set; }
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
