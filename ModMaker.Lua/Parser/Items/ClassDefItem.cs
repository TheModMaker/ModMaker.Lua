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
  /// Defines a parse item that represents a class definition.
  /// </summary>
  public sealed class ClassDefItem : IParseStatement {
    /// <summary>
    /// Creates a new instance of ClassDefItem with the given state.
    /// </summary>
    /// <param name="name">The name of the class.</param>
    /// <param name="implements">The types that it implements.</param>
    public ClassDefItem(NameItem name, IParseExp[] implements) {
      Name = name;
      Implements = implements;
    }

    /// <summary>
    /// Gets or sets the name of the class.
    /// </summary>
    public NameItem Name { get; set; }
    /// <summary>
    /// Gets the types that this class implements.
    /// </summary>
    public IParseExp[] Implements { get; set; }

    public DebugInfo Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      return visitor.Visit(this);
    }
  }
}
