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
  /// Defines a parse item that represents an indexer expression.
  /// </summary>
  public sealed class IndexerItem : IParseVariable {
    /// <summary>
    /// Creates a new IndexerItem.
    /// </summary>
    /// <param name="prefix">The prefix expression.</param>
    /// <param name="exp">The expression of the accessed member.</param>
    public IndexerItem(IParseExp prefix, IParseExp exp) {
      Prefix = prefix;
      Expression = exp;
    }

    /// <summary>
    /// Gets or sets the prefix expression.
    /// </summary>
    public IParseExp Prefix { get; set; }
    /// <summary>
    /// Gets or sets the indexing expression.
    /// </summary>
    public IParseExp Expression { get; set; }

    public DebugInfo Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      return visitor.Visit(this);
    }
  }
}
