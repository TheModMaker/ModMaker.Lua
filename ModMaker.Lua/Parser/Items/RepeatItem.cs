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

namespace ModMaker.Lua.Parser.Items {
  /// <summary>
  /// Defines a parse item that represents a repeat statement.
  /// </summary>
  public sealed class RepeatItem : IParseStatement {
    /// <summary>
    /// Creates a new instance of RepeatItem.
    /// </summary>
    public RepeatItem(IParseExp exp, BlockItem block) {
      Block = block;
      Expression = exp;
    }

    /// <summary>
    /// Gets or sets the block of the loop.
    /// </summary>
    public BlockItem Block { get; set; }
    /// <summary>
    /// Gets or sets the expression that defines the end of the loop.
    /// </summary>
    public IParseExp Expression { get; set; }
    /// <summary>
    /// Gets or sets a label that represents a break from the loop.
    /// </summary>
    public LabelItem Break { get; set; } = new LabelItem("<break>");

    /// <summary>
    /// Contains the DebugInfo for the whole block.
    /// </summary>
    public DebugInfo Debug { get; set; }
    /// <summary>
    /// Contains the DebugInfo for the 'repeat' token.
    /// </summary>
    public DebugInfo RepeatDebug { get; set; }
    /// <summary>
    /// Contains the DebugInfo for the 'until' and the expression.
    /// </summary>
    public DebugInfo UntilDebug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      return visitor.Visit(this);
    }
  }
}
