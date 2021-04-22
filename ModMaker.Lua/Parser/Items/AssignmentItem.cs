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
  /// Defines a variable assignment statement.
  /// e.g. i, x = foo();
  /// </summary>
  public sealed class AssignmentItem : IParseStatement {
    /// <summary>
    /// Creates a new instance of AssignmentItem.
    /// </summary>
    /// <param name="local">True if this is a local definition, otherwise false.</param>
    public AssignmentItem(IParseVariable[] names, IParseExp[] exps) {
      Names = names;
      Expressions = exps;
    }

    /// <summary>
    /// Gets the name expression of this item.
    /// </summary>
    public IParseVariable[] Names { get; set; }
    /// <summary>
    /// Gets the value expression of this item.
    /// </summary>
    public IParseExp[] Expressions { get; set; }
    /// <summary>
    /// Gets or sets whether this is a local definition.
    /// </summary>
    public bool Local { get; set; } = false;
    /// <summary>
    /// Gets or sets whether the last expression is single.  Namely whether the last expression
    /// is wrapped in parentheses, e.g. i = (foo()).
    /// </summary>
    public bool IsLastExpressionSingle { get; set; } = false;

    public Token Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      if (visitor == null) {
        throw new ArgumentNullException(nameof(visitor));
      }

      return visitor.Visit(this);
    }
  }
}
