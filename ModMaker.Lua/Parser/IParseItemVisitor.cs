// Copyright 2014 Jacob Trimble
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

using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Parser {
  /// <summary>
  /// A visitor object used to select a specific method based on the runtime type of the IParseItem.
  /// This is used with the visitor pattern to traverse the parse item tree.
  /// </summary>
  public interface IParseItemVisitor {
    /// <summary>
    /// Called when the item is a binary expression item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Accept.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(BinOpItem target);
    /// <summary>
    /// Called when the item is a block item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Accept.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(BlockItem target);
    /// <summary>
    /// Called when the item is a class definition item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Accept.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(ClassDefItem target);
    /// <summary>
    /// Called when the item is a for generic item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Accept.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(ForGenItem target);
    /// <summary>
    /// Called when the item is a for numerical item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Accept.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(ForNumItem target);
    /// <summary>
    /// Called when the item is a function call item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(FuncCallItem target);
    /// <summary>
    /// Called when the item is a function definition item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(FuncDefItem target);
    /// <summary>
    /// Called when the item is the global code.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(GlobalItem target);
    /// <summary>
    /// Called when the item is a goto item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(GotoItem target);
    /// <summary>
    /// Called when the item is an if item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(IfItem target);
    /// <summary>
    /// Called when the item is an indexer item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(IndexerItem target);
    /// <summary>
    /// Called when the item is a label item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(LabelItem target);
    /// <summary>
    /// Called when the item is a literal item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(LiteralItem target);
    /// <summary>
    /// Called when the item is a name item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(NameItem target);
    /// <summary>
    /// Called when the item is a repeat item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(RepeatItem target);
    /// <summary>
    /// Called when the item is a return item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(ReturnItem target);
    /// <summary>
    /// Called when the item is a table item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(TableItem target);
    /// <summary>
    /// Called when the item is a unary operation item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(UnOpItem target);
    /// <summary>
    /// Called when the item is an assignment item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(AssignmentItem target);
    /// <summary>
    /// Called when the item is a while item.
    /// </summary>
    /// <param name="target">The object that was passed to IParseItem.Visit.</param>
    /// <returns>The passed target or a modification of it.</returns>
    IParseItem Visit(WhileItem target);
  }
}
