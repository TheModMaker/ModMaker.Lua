// Copyright 2022 Jacob Trimble
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
  /// Represents the top of the AST, holding the global code.
  /// </summary>
  public sealed class GlobalItem : IParseItem {
    public GlobalItem(BlockItem block) {
      Block = block;
    }

    /// <summary>
    /// Gets or sets the main code block.
    /// </summary>
    public BlockItem Block { get; set; }
    /// <summary>
    /// Gets or sets the function info for the global code.  Since this is global, it can't capture
    /// anything, so CapturedParents will always be empty.
    /// </summary>
    public FuncDefItem.FunctionInfo? FunctionInformation { get; set; } = null;

    public DebugInfo Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      return visitor.Visit(this);
    }
  }
}
