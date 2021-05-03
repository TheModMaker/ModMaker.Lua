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
  /// Defines a parse item that represents a function definition.
  /// </summary>
  public sealed class FuncDefItem : IParseStatement, IParseExp {
    /// <summary>
    /// Defines information about a function definition.  This is used by GetInfoVisitor and the
    /// compiler.  This manages captured variables and allows for smaller generated code.
    /// </summary>
    public sealed class FunctionInfo {
      /// <summary>
      /// Creates a new instance of FunctionInfo.
      /// </summary>
      public FunctionInfo() { }

      /// <summary>
      /// Gets or sets whether this function has nested functions.
      /// </summary>
      public bool HasNested { get; set; } = false;
      /// <summary>
      /// Gets or sets whether this function captures local variables
      /// from the parent function.
      /// </summary>
      public bool CapturesParent { get; set; } = false;
      /// <summary>
      /// Gets or sets an array of the local variables defined in this function that are captured by
      /// nested  functions.
      /// </summary>
      public NameItem[] CapturedLocals { get; set; } = new NameItem[0];
    }

    /// <summary>
    /// Creates a new FuncDefItem with the given name.
    /// </summary>
    /// <param name="name">The name of the method, must be a NameItem or IndexerItem.</param>
    public FuncDefItem(NameItem[] args, BlockItem block) {
      Arguments = args;
      Block = block;
    }

    /// <summary>
    /// Gets the name of the arguments defined for this function definition.
    /// </summary>
    public NameItem[] Arguments { get; set; }
    /// <summary>
    /// Gets or sets the block of the function.
    /// </summary>
    public BlockItem Block { get; set; }
    /// <summary>
    /// Gets or sets whether this is a local function definition.
    /// </summary>
    public bool Local { get; set; } = false;
    /// <summary>
    /// Gets or sets the prefix expression for this function definition, must be a NameItem or an
    /// IndexerItem.
    /// </summary>
    public IParseVariable Prefix { get; set; } = null;
    /// <summary>
    /// Gets or sets the name if the instance method or null if this isn't an instance method.
    /// </summary>
    public string InstanceName { get; set; } = null;
    /// <summary>
    /// Gets or sets information about the function. To get this information, use GetInfoVisitor.
    /// </summary>
    public FunctionInfo FunctionInformation { get; set; } = null;

    public DebugInfo Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      if (visitor == null) {
        throw new ArgumentNullException(nameof(visitor));
      }

      return visitor.Visit(this);
    }
  }
}
