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

namespace ModMaker.Lua.Parser.Items {
  /// <summary>
  /// Defines a parse item that represents a table definition expression.
  /// </summary>
  public sealed class TableItem : IParseExp {
    /// <summary>
    /// Creates a new TableItem with no entries.
    /// </summary>
    public TableItem() : this(new KeyValuePair<IParseExp, IParseExp>[0]) { }

    /// <summary>
    /// Creates a new instance of TableItem.
    /// </summary>
    public TableItem(KeyValuePair<IParseExp, IParseExp>[] fields) {
      Fields = fields;
    }

    /// <summary>
    /// Gets the fields of the table.
    /// </summary>
    public KeyValuePair<IParseExp, IParseExp>[] Fields { get; set; }

    public Token Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      if (visitor == null) {
        throw new ArgumentNullException(nameof(visitor));
      }

      return visitor.Visit(this);
    }
  }
}
