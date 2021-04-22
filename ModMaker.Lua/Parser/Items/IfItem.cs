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
  /// Defines a parse item that is a conditional statement.  It contains a main block, an optional
  /// else, and zero or more optional else if statements.
  /// </summary>
  public sealed class IfItem : IParseStatement {
    /// <summary>
    /// Contains information about an else if statement.
    /// </summary>
    public struct ElseInfo {
      /// <summary>
      /// Creates a new instance of ElseInfo.
      /// </summary>
      /// <param name="exp">The expression for the else if statement.</param>
      /// <param name="block">The block of the else if statement.</param>
      public ElseInfo(IParseExp exp, BlockItem block) {
        Expression = exp;
        Block = block;
      }

      /// <summary>
      /// Contains the expression for the else if statement
      /// </summary>
      public IParseExp Expression { get; }
      /// <summary>
      /// Contains the block of the else if statement.
      /// </summary>
      public BlockItem Block { get; }
    }

    public IfItem(IParseExp exp, BlockItem block) : this(exp, block, new ElseInfo[0], null) { }
    public IfItem(IParseExp exp, BlockItem block, ElseInfo[] elses, BlockItem elseBlock = null) {
      Expression = exp;
      Block = block;
      Elses = elses;
      ElseBlock = elseBlock;
    }

    /// <summary>
    /// The conditional expression for the first if block.
    /// </summary>
    public IParseExp Expression { get; set; }
    /// <summary>
    /// The block to execute if the 'Expression' is true.
    /// </summary>
    public BlockItem Block { get; set; }
    /// <summary>
    /// Gets the else-if statements, the first item is the conditional, the second is the block
    /// contents.
    /// </summary>
    public ElseInfo[] Elses { get; set; }
    /// <summary>
    /// The else block to execute if none are true.
    /// </summary>
    public BlockItem ElseBlock { get; set; }

    public Token Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      if (visitor == null) {
        throw new ArgumentNullException(nameof(visitor));
      }

      return visitor.Visit(this);
    }
  }
}
