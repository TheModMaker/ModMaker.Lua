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
  /// Defines a parse item that represents a return statement.
  /// </summary>
  public sealed class ReturnItem : IParseItem {
    /// <summary>
    /// Creates a new instance of ReturnItem.
    /// </summary>
    public ReturnItem() : this(Array.Empty<IParseExp>()) { }
    /// <summary>
    /// Creates a new instance of ReturnItem.
    /// </summary>
    public ReturnItem(IParseExp[] exps) {
      Expressions = exps;
    }

    /// <summary>
    /// Gets the expressions of the return statement.
    /// </summary>
    public IParseExp[] Expressions { get; set; }
    /// <summary>
    /// Gets or sets whether the last expression should be single.  Namely whether the last
    /// expression is wrapped in parentheses, e.g. return 1, (foo()).
    /// </summary>
    public bool IsLastExpressionSingle { get; set; } = false;

    public DebugInfo Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      return visitor.Visit(this);
    }
  }
}
