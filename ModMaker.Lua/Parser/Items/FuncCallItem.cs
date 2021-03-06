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
  /// Defines a parse item that represents a function call expression or statement.
  /// </summary>
  public sealed class FuncCallItem : IParseStatement, IParsePrefixExp {
    /// <summary>
    /// Contains information about an argument passed to the function.
    /// </summary>
    public struct ArgumentInfo {
      /// <summary>
      /// Creates a new instance of ArgumentInfo.
      /// </summary>
      /// <param name="exp">The expression for the argument.</param>
      /// <param name="byRef">Whether the argument is passed by-ref.</param>
      public ArgumentInfo(IParseExp exp, bool byRef) {
        Expression = exp;
        IsByRef = byRef;
      }

      /// <summary>
      /// Contains the expression for the argument.
      /// </summary>
      public readonly IParseExp Expression;
      /// <summary>
      /// Contains whether the argument is passed by-ref.
      /// </summary>
      public readonly bool IsByRef;
    }

    /// <inheritdoc cref="FuncCallItem(IParseExp, ArgumentInfo[])"/>
    public FuncCallItem(IParseExp prefix) : this(prefix, new ArgumentInfo[0]) { }

    /// <summary>
    /// Creates a new instance of FuncCallItem with the given state.
    /// </summary>
    /// <param name="prefix">The prefix expression that defines the call.</param>
    /// <param name="args">The arguments to call with.</param>
    /// <exception cref="System.ArgumentException">
    /// If prefix is not an expression or prefix-expression.
    /// </exception>
    /// <exception cref="System.ArgumentNullException">If prefix is null.</exception>
    public FuncCallItem(IParseExp prefix, ArgumentInfo[] args) {
      if (prefix == null) {
        throw new ArgumentNullException(nameof(prefix));
      }

      Arguments = args;
      Prefix = prefix;
    }

    /// <summary>
    /// Gets or sets the prefix expression that defines what object to call.
    /// </summary>
    public IParseExp Prefix { get; set; }
    /// <summary>
    /// Gets the arguments that are passed to the call.  The first item is the expression that
    /// defines the value, the second item is whether the argument is passed by-reference.
    /// </summary>
    public ArgumentInfo[] Arguments { get; set; }
    /// <summary>
    /// Gets or sets whether this is a tail call. This value is not checked for validity during
    /// compilation and may cause errors if changed.
    /// </summary>
    public bool IsTailCall { get; set; } = false;
    /// <summary>
    /// Gets or sets whether this represents a statement.  This value is not checked for validity
    /// during compilation and may cause errors if changed.
    /// </summary>
    public bool Statement { get; set; } = false;
    /// <summary>
    /// Gets or sets whether the last argument in this call should be single.  Namely that the
    /// last argument is wrapped in parentheses, e.g. foo(2, (call())).
    /// </summary>
    public bool IsLastArgSingle { get; set; } = false;
    /// <summary>
    /// Gets or sets the instance name of the call or null if not an instance call.
    /// </summary>
    public string InstanceName { get; set; } = null;
    /// <summary>
    /// Gets or sets the overload of the function, use a negative number to use overload resolution.
    /// </summary>
    public int Overload { get; set; } = -1;

    public Token Debug { get; set; }
    public object UserData { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      if (visitor == null) {
        throw new ArgumentNullException(nameof(visitor));
      }

      return visitor.Visit(this);
    }
  }
}
