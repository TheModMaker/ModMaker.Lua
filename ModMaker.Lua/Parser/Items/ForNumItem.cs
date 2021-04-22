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
  /// Defines a parse item that represents a numerical for loop.
  /// e.g. for i = 2, 4 do ... end.
  /// </summary>
  public sealed class ForNumItem : IParseStatement {
    /// <summary>
    /// Creates a new ForNumItem with the given name.
    /// </summary>
    /// <param name="name">The name of the variable defined.</param>
    /// <param name="limit">The item that defines the limit of the loop.</param>
    /// <param name="start">The item that defines the start of the loop.</param>
    /// <param name="step">The item that defines the step of the loop.</param>
    /// <exception cref="System.ArgumentNullException">If name, start, or limit is null.</exception>
    public ForNumItem(NameItem name, IParseExp start, IParseExp limit, IParseExp step,
                      BlockItem block) {
      if (name == null) {
        throw new ArgumentNullException(nameof(name));
      }

      if (start == null) {
        throw new ArgumentNullException(nameof(start));
      }

      if (limit == null) {
        throw new ArgumentNullException(nameof(limit));
      }

      Start = start;
      Limit = limit;
      Step = step;
      Name = name;
      Block = block;
    }

    /// <summary>
    /// Gets or sets the name of the variable in the loop.
    /// </summary>
    public NameItem Name { get; set; }
    /// <summary>
    /// Gets or sets the expression that determines the start of the loop.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">If setting to null.</exception>
    public IParseExp Start { get; set; }
    /// <summary>
    /// Gets or sets the expression that determines the limit of the loop.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">If setting to null.</exception>
    public IParseExp Limit { get; set; }
    /// <summary>
    /// Gets or sets the expression that determines the step of the loop.
    /// </summary>
    public IParseExp Step { get; set; }
    /// <summary>
    /// Gets or sets the block of the for loop.
    /// </summary>
    public BlockItem Block { get; set; }
    /// <summary>
    /// Gets the label that represents a break from the loop.
    /// </summary>
    public LabelItem Break { get; } = new LabelItem("<break>");

    public Token Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      if (visitor == null) {
        throw new ArgumentNullException(nameof(visitor));
      }

      return visitor.Visit(this);
    }
  }
}
