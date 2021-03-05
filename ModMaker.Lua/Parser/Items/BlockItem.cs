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
  /// Defines a parse item that represents a block of code.
  /// </summary>
  public sealed class BlockItem : IParseStatement {
    readonly List<IParseStatement> _children = new List<IParseStatement>();

    /// <summary>
    /// Creates a new empty BlockItem.
    /// </summary>
    public BlockItem() {}

    /// <summary>
    /// Gets or sets the return statement of the block, can be null.
    /// </summary>
    public ReturnItem Return { get; set; }
    /// <summary>
    /// Gets the children of the block item.
    /// </summary>
    public ReadOnlyCollection<IParseStatement> Children {
      get { return new ReadOnlyCollection<IParseStatement>(_children); }
    }
    /// <summary>
    /// Gets or sets the user data for this object. This value is never modified by the default
    /// framework, but may be modified by other visitors.
    /// </summary>
    public object UserData { get; set; }
    /// <summary>
    /// Gets or sets the debug info for this item.
    /// </summary>
    public Token Debug { get; set; }

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
    /// Adds a child element to the block.
    /// </summary>
    /// <param name="child">The child statement to add.</param>
    /// <exception cref="System.ArgumentNullException">If child is null.</exception>
    public void AddItem(IParseStatement child) {
      if (child == null) {
        throw new ArgumentNullException(nameof(child));
      }

      _children.Add(child);
    }
    /// <summary>
    /// Adds a range of child elements to the block.
    /// </summary>
    /// <param name="children">The children to add.</param>
    /// <exception cref="System.ArgumentException">
    /// If children contains a null item -or- if one of the children is not a statement.
    /// </exception>
    /// <exception cref="System.ArgumentNullException">If children is null.</exception>
    public void AddRange(IEnumerable<IParseStatement> children) {
      if (children == null) {
        throw new ArgumentNullException(nameof(children));
      }

      if (children.Contains(null)) {
        throw new ArgumentException(string.Format(Resources.CannotContainNull, nameof(children)));
      }

      _children.AddRange(children);
    }
  }
}
