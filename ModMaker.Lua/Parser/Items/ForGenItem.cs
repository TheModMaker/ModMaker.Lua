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
using System.Linq;

namespace ModMaker.Lua.Parser.Items {
  /// <summary>
  /// Defines a parse item that represents a for generic statement.
  /// e.g. for k, v in dict do ... end.
  /// </summary>
  public sealed class ForGenItem : IParseStatement {
    readonly NameItem[] _names;
    readonly IList<IParseExp> _exps = new List<IParseExp>();

    /// <summary>
    /// Creates a new instance of ForGenItem with the given names definition.
    /// </summary>
    /// <param name="names">The names that are defined by this statement.</param>
    /// <exception cref="System.ArgumentException">
    /// If names does not contain any names or contains a null item.
    /// </exception>
    /// <exception cref="System.ArgumentNullException">If names is null.</exception>
    public ForGenItem(IEnumerable<NameItem> names) {
      if (names == null) {
        throw new ArgumentNullException(nameof(names));
      }

      if (names.Contains(null)) {
        throw new ArgumentException(string.Format(Resources.CannotContainNull, nameof(names)));
      }

      _names = names.ToArray();

      if (_names.Length == 0) {
        throw new ArgumentException(Resources.DefineAtLeastOneName);
      }
    }

    /// <summary>
    /// Gets the expressions for the for generic statement.
    /// </summary>
    public ReadOnlyCollection<IParseExp> Expressions {
      get { return new ReadOnlyCollection<IParseExp>(_exps); }
    }
    /// <summary>
    /// Gets the name definitions for the for generic statement.
    /// </summary>
    public ReadOnlyCollection<NameItem> Names {
      get { return new ReadOnlyCollection<NameItem>(_names); }
    }
    /// <summary>
    /// Gets the label that represents a break from the loop.
    /// </summary>
    public LabelItem Break { get; } = new LabelItem("<break>");
    /// <summary>
    /// Gets or sets the block of the for generic statement.
    /// </summary>
    public BlockItem Block { get; set; }
    public Token Debug { get; set; }
    public object UserData { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      if (visitor == null) {
        throw new ArgumentNullException(nameof(visitor));
      }

      return visitor.Visit(this);
    }
    /// <summary>
    /// Adds the given expression to the object.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <exception cref="System.ArgumentException">If item is not an expression.</exception>
    /// <exception cref="System.ArgumentNullException">If item is null.</exception>
    public void AddExpression(IParseExp item) {
      if (item == null) {
        throw new ArgumentNullException(nameof(item));
      }

      _exps.Add(item);
    }
  }
}
