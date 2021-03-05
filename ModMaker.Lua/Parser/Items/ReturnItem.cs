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
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ModMaker.Lua.Parser.Items {
  /// <summary>
  /// Defines a parse item that represents a return statement.
  /// </summary>
  public sealed class ReturnItem : IParseItem {
    readonly IList<IParseExp> _exps = new List<IParseExp>();

    /// <summary>
    /// Creates a new instance of ReturnItem.
    /// </summary>
    public ReturnItem() { }

    /// <summary>
    /// Gets the expressions of the return statement.
    /// </summary>
    public ReadOnlyCollection<IParseExp> Expressions {
      get { return new ReadOnlyCollection<IParseExp>(_exps); }
    }
    /// <summary>
    /// Gets or sets the debug info for this item.
    /// </summary>
    public Token Debug { get; set; }
    /// <summary>
    /// Gets or sets the user data for this object. This value is never modified by the default
    /// framework, but may be modified by other visitors.
    /// </summary>
    public object UserData { get; set; }
    /// <summary>
    /// Gets or sets whether the last expression should be single.  Namely whether the last
    /// expression is wrapped in parentheses, e.g. return 1, (foo()).
    /// </summary>
    public bool IsLastExpressionSingle { get; set; }

    /// <summary>
    /// Dispatches to the specific visit method for this item type.
    /// </summary>
    /// <param name="visitor">The visitor object.</param>
    /// <returns>The object returned from the specific IParseItemVisitor method.</returns>
    /// <exception cref="System.ArgumentNullException">If visitor is null.</exception>
    public IParseItem Accept(IParseItemVisitor visitor) {
      if (visitor == null) {
        throw new ArgumentNullException(nameof(visitor));
      }

      return visitor.Visit(this);
    }
    /// <summary>
    /// Adds an expression to the statement.
    /// </summary>
    /// <param name="expression">The expression to add.</param>
    /// <exception cref="System.ArgumentNullException">If expression is null.</exception>
    public void AddExpression(IParseExp expression) {
      if (expression == null) {
        throw new ArgumentNullException(nameof(expression));
      }

      _exps.Add(expression);
    }
  }
}
