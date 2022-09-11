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
  /// Defines the type of a unary operation.
  /// </summary>
  public enum UnaryOperationType {
    /// <summary>
    /// The type of the operation is unknown.
    /// </summary>
    Unknown,
    /// <summary>
    /// A unary negation, (-A).
    /// </summary>
    Minus,
    /// <summary>
    /// A logical negation, (not A).
    /// </summary>
    Not,
    /// <summary>
    /// The length of a variable (#A).
    /// </summary>
    Length,
  }

  /// <summary>
  /// Defines a parse item that represents a unary operation expression.
  /// </summary>
  public sealed class UnOpItem : IParseExp {
    /// <summary>
    /// Creates a new UnOpItem with the given state.
    /// </summary>
    /// <param name="target">The target expression.</param>
    /// <param name="type">The type of operation.</param>
    public UnOpItem(IParseExp target, UnaryOperationType type) {
      Target = target;
      OperationType = type;
    }

    /// <summary>
    /// Gets or sets the target expression.
    /// </summary>
    public IParseExp Target { get; set; }
    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    public UnaryOperationType OperationType { get; set; }

    public DebugInfo Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      return visitor.Visit(this);
    }
  }
}
