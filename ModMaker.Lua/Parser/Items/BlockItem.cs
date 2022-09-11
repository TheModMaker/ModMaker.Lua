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
  /// Defines a parse item that represents a block of code.
  /// </summary>
  public sealed class BlockItem : IParseStatement {
    /// <summary>
    /// Creates a new empty BlockItem.
    /// </summary>
    public BlockItem() : this(Array.Empty<IParseStatement>()) { }
    /// <summary>
    /// Creates a new BlockItem with the given return values.
    /// </summary>
    /// <param name="statements">The statements that make up the block.</param>
    public BlockItem(IParseStatement[] statements) {
      Children = statements;
    }

    /// <summary>
    /// Gets the children of the block item.
    /// </summary>
    public IParseStatement[] Children { get; set; }
    /// <summary>
    /// Gets or sets the return statement of the block, can be null.
    /// </summary>
    public ReturnItem? Return { get; set; } = null;

    public DebugInfo Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      return visitor.Visit(this);
    }
  }
}
