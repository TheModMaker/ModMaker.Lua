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
  /// Defines a parse item that represents a literal.
  /// </summary>
  public sealed class LiteralItem : IParseExp {
    /// <summary>
    /// Creates a new LiteralItem with the given value.
    /// </summary>
    /// <param name="item">The value of this literal.</param>
    public LiteralItem(object? item) {
      if (!(item is bool || item is double || item is string || item is null)) {
        throw new InvalidOperationException(Resources.InvalidLiteralType);
      }

      Value = item;
    }

    /// <summary>
    /// Gets or sets the value of this literal.
    /// </summary>
    public object? Value { get; set; }

    public DebugInfo Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      return visitor.Visit(this);
    }
  }
}
