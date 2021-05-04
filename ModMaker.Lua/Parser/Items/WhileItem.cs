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
  /// Defines a parse item that represents a while loop.
  /// </summary>
  public sealed class WhileItem : IParseStatement {
    /// <summary>
    /// Creates a new instance of WhileItem.
    /// </summary>
    public WhileItem(IParseExp exp, BlockItem block) {
      Expression = exp;
      Block = block;
    }

    /// <summary>
    /// Gets or sets the expression that determines the bounds of the loop.
    /// </summary>
    public IParseExp Expression { get; set; }
    /// <summary>
    /// Gets or sets the block of this loop.
    /// </summary>
    public BlockItem Block { get; set; }
    /// <summary>
    /// Gets a label that represents a break from the loop.
    /// </summary>
    public LabelItem Break { get; } = new LabelItem("<break>");

    /// <summary>
    /// Contains the DebugInfo for the whole block.
    /// </summary>
    public DebugInfo Debug { get; set; }
    /// <summary>
    /// Contains the DebugInfo for the 'while', 'end', and expression parts.
    /// </summary>
    public DebugInfo WhileDebug { get; set; }
    /// <summary>
    /// Contains the DebugInfo for the 'end' token.
    /// </summary>
    public DebugInfo EndDebug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      if (visitor == null) {
        throw new ArgumentNullException(nameof(visitor));
      }

      return visitor.Visit(this);
    }
  }
}
