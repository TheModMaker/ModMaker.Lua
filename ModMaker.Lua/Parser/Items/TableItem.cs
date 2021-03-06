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
  /// Defines a parse item that represents a table definition expression.
  /// </summary>
  public sealed class TableItem : IParseExp {
    readonly IList<KeyValuePair<IParseExp, IParseExp>> _fields =
        new List<KeyValuePair<IParseExp, IParseExp>>();
    double _nextId = 1;

    /// <summary>
    /// Creates a new instance of TableItem.
    /// </summary>
    public TableItem() { }

    /// <summary>
    /// Gets the fields of the table.
    /// </summary>
    public ReadOnlyCollection<KeyValuePair<IParseExp, IParseExp>> Fields {
      get { return new ReadOnlyCollection<KeyValuePair<IParseExp, IParseExp>>(_fields); }
    }
    public Token Debug { get; set; }
    public object UserData { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      if (visitor == null) {
        throw new ArgumentNullException(nameof(visitor));
      }

      return visitor.Visit(this);
    }
    /// <summary>
    /// Adds a new item to the table definition.
    /// </summary>
    /// <param name="index">The index expression.</param>
    /// <param name="exp">The value expression.</param>
    /// <exception cref="System.ArgumentNullException">If exp is null.</exception>
    public void AddItem(IParseExp index, IParseExp exp) {
      if (exp == null) {
        throw new ArgumentNullException(nameof(exp));
      }

      if (index == null) {
        index = new LiteralItem(_nextId++);
      }

      _fields.Add(new KeyValuePair<IParseExp, IParseExp>(index, exp));
    }
  }
}
