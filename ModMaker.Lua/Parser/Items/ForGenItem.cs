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

#nullable enable

namespace ModMaker.Lua.Parser.Items {
  /// <summary>
  /// Defines a parse item that represents a for generic statement.
  /// e.g. for k, v in dict do ... end.
  /// </summary>
  public sealed class ForGenItem : IParseStatement {
    /// <summary>
    /// Creates a new instance of ForGenItem with the given names definition.
    /// </summary>
    /// <param name="names">The names that are defined by this statement.</param>
    public ForGenItem(NameItem[] names, IParseExp[] exps, BlockItem block) {
      Names = names;
      Expressions = exps;
      Block = block;
    }

    /// <summary>
    /// Gets the name definitions for the for generic statement.
    /// </summary>
    public NameItem[] Names { get; set; }
    /// <summary>
    /// Gets the expressions for the for generic statement.
    /// </summary>
    public IParseExp[] Expressions { get; set; }
    /// <summary>
    /// Gets or sets the block of the for generic statement.
    /// </summary>
    public BlockItem Block { get; set; }
    /// <summary>
    /// Gets the label that represents a break from the loop.
    /// </summary>
    public LabelItem Break { get; } = new LabelItem("<break>");

    /// <summary>
    /// Contains the DebugInfo for the whole block.
    /// </summary>
    public DebugInfo Debug { get; set; }
    /// <summary>
    ///  Contains the DebugInfo for the 'for' line.
    /// </summary>
    public DebugInfo ForDebug { get; set; }
    /// <summary>
    /// Contains the DebugInfo for the 'end' token.
    /// </summary>
    public DebugInfo EndDebug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      return visitor.Visit(this);
    }
  }
}
