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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;
using NUnit.Framework;

namespace UnitTests.Parser {
  [TestFixture]
  public class PlainParserTest {
    class SyntaxErrorCollection : List<Tuple<string, int>> {
      public SyntaxErrorCollection() { }
      public void Add(string body, int column) {
        Add(new Tuple<string, int>(body, column));
      }
    }

    void _validateDebug(Token debug, string prefix, string value, long startLine, long startPos) {
      Assert.AreEqual(value, debug.Value, prefix + ".Debug.Value");
      Assert.AreEqual(startLine, debug.StartLine, prefix + ".Debug.StartLine");
      Assert.AreEqual(startPos, debug.StartPos, prefix + ".Debug.StartPos");
    }

    IParseItem _parseBlock(string input) {
      PlainParser target = new PlainParser();
      var encoding = Encoding.UTF8;
      var stream = new MemoryStream(encoding.GetBytes(input));
      return target.Parse(stream, encoding, null);
    }

    /// <summary>
    /// Parses the given statement code.
    /// </summary>
    /// <param name="input">The input code.</param>
    /// <returns>The parsed statement.</returns>
    T _parseStatement<T>(string input) where T : class {
      var block = _parseBlock(input) as BlockItem;
      Assert.IsNotNull(block);
      Assert.AreEqual(1, block.Children.Count);
      var ret = block.Children[0] as T;
      Assert.IsNotNull(ret);
      return ret;
    }

    /// <summary>
    /// Parses the given expression code.
    /// </summary>
    /// <param name="input">The input code.</param>
    /// <returns>The parsed expression.</returns>
    T _parseExpression<T>(string input) where T : class {
      var assign = _parseStatement<AssignmentItem>("local x = " + input);
      Assert.AreEqual(assign.Expressions.Count, 1);
      var ret = assign.Expressions[0] as T;
      Assert.IsNotNull(ret);
      return ret;
    }

    /// <summary>
    /// Asserts the given parsed item is a variable with the given name.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <param name="name">The expected name of the variable.</param>
    void _assertIsVariable(IParseItem item, string name) {
      var nameItem = item as NameItem;
      Assert.IsNotNull(nameItem);
      Assert.AreEqual(name, nameItem.Name);
    }

    /// <summary>
    /// Asserts the given parsed item is a literal with the given value.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <param name="value">The expected value of the literal.</param>
    void _assertIsLiteral(IParseItem item, object value) {
      var literal = item as LiteralItem;
      Assert.IsNotNull(literal);
      Assert.AreEqual(value, literal.Value);
    }

    [Test]
    public void GenralParse() {
      string input1 =
@"local a = 12
t = { [34]= function() print(i) end }
function Some(a, ...)
    a, b, c = ...
    for i= 12, 23 do
        print(i)
    end
end";
      IParseItem actual = _parseBlock(input1);

      // Check the main block
      BlockItem block = actual as BlockItem;
      Assert.IsInstanceOf<BlockItem>(actual);
      Assert.IsNotNull(block.Children);
      Assert.AreEqual(3, block.Children.Count, "Block.Children.Count");
      _validateDebug(block.Debug, "Block", "local", 1, 1);

      // Check the return statement of the main block
      {
        ReturnItem ret = block.Return;
        Assert.IsInstanceOf<ReturnItem>(block.Return);
        _validateDebug(ret.Debug, "Block.Return", null, 0, 0);
        Assert.IsNotNull(ret.Expressions);
        Assert.AreEqual(0, ret.Expressions.Count);
      }

      // local a = 12
      {
        AssignmentItem init = block.Children[0] as AssignmentItem;
        Assert.IsNotNull(init, "Block.Children[0]");
        Assert.AreEqual(true, init.Local);
        _validateDebug(init.Debug, "Block.Children[0]", "local", 1, 1);

        // Check the names
        {
          Assert.IsNotNull(init.Names, "Block.Children[0].Names");
          Assert.AreEqual(1, init.Names.Count, "Block.Children[0].Names.Count");

          NameItem name = init.Names[0] as NameItem;
          Assert.IsNotNull(name, "Block.Children[0].Names[0]");
          Assert.AreEqual("a", name.Name, "Block.Children[0].Names[0].Name");
          _validateDebug(name.Debug, "Block.Children[0].Names[0]", "a", 1, 7);
        }

        // Check the expressions
        {
          Assert.IsNotNull(init.Expressions, "Block.Children[0].Expressions");
          Assert.AreEqual(1, init.Expressions.Count, "Block.Children[0].Expressions.Count");

          LiteralItem literal = init.Expressions[0] as LiteralItem;
          Assert.IsNotNull(literal, "Block.Children[0].Expressions[0]");
          Assert.AreEqual(12.0, literal.Value, "Block.Children[0].Expressions[0].Value");
          _validateDebug(literal.Debug, "Block.Children[0].Expressions[0]", "12", 1, 11);
        }
      }

      // t = { [34]= function() print(i) end }
      {
        AssignmentItem init = block.Children[1] as AssignmentItem;
        Assert.IsNotNull(init, "Block.Children[1]");
        Assert.AreEqual(false, init.Local);
        _validateDebug(init.Debug, "Block.Children[1]", "t", 2, 1);

        // Check the names
        {
          Assert.IsNotNull(init.Names, "Block.Children[1].Names");
          Assert.AreEqual(1, init.Names.Count, "Block.Children[1].Names.Count");

          NameItem name = init.Names[0] as NameItem;
          Assert.IsNotNull(name, "Block.Children[1].Names[0]");
          Assert.AreEqual("t", name.Name, "Block.Children[1].Names[0].Name");
          _validateDebug(name.Debug, "Block.Children[1].Names[0]", "t", 2, 1);
        }

        // Check the expressions
        {
          Assert.IsNotNull(init.Expressions, "Block.Children[1].Expressions");
          Assert.AreEqual(1, init.Expressions.Count, "Block.Children[1].Expressions.Count");

          TableItem table = init.Expressions[0] as TableItem;
          Assert.IsNotNull(table, "Block.Children[1].Expressions[0]");
          _validateDebug(table.Debug, "Block.Children[1].Expressions[0]", "{", 2, 5);

          Assert.IsNotNull(table.Fields, "Block.Children[1].Expressions[0].Fields");
          Assert.AreEqual(1, table.Fields.Count, "Block.Children[1].Expressions[0].Fields.Count");

          var field = table.Fields[0];
          {
            LiteralItem literal = field.Key as LiteralItem;
            Assert.IsNotNull(literal, "Block.Children[1].Expressions[0].Fields[0].Item1");
            Assert.AreEqual(34.0, literal.Value,
                            "Block.Children[1].Expressions[0].Fields[0].Item1.Value");
            _validateDebug(literal.Debug, "Block.Children[1].Expressions[0].Fields[0].Item1", "34",
                           2, 8);
          }
          {
            FuncDefItem func = field.Value as FuncDefItem;
            Assert.IsNotNull(func, "Block.Children[1].Expressions[0].Fields[0].Item2");
            Assert.IsNull(func.InstanceName,
                          "Block.Children[1].Expressions[0].Fields[0].Item2.InstanceName");
            Assert.IsNull(func.Prefix, "Block.Children[1].Expressions[0].Fields[0].Item2.Prefix");
            Assert.AreEqual(false, func.Local,
                            "Block.Children[1].Expressions[0].Fields[0].Item2.Local");
            Assert.IsNull(func.FunctionInformation,
                          "Block.Children[1].Expressions[0].Fields[0].Item2.FunctionInformation");
            _validateDebug(func.Debug, "Block.Children[1].Expressions[0].Fields[0].Item2",
                           "function", 2, 13);

            // Validate the block
            {
              BlockItem funcBlock = func.Block;
              Assert.IsNotNull(funcBlock, "Block.Children[1].Expressions[0].Fields[0].Item2.Block");
              _validateDebug(funcBlock.Debug,
                             "Block.Children[1].Expressions[0].Fields[0].Item2.Block", "print", 2,
                             24);

              // Validate the return
              {
                ReturnItem ret = funcBlock.Return;
                Assert.IsNotNull(ret,
                                 "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Return");
                _validateDebug(ret.Debug,
                               "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Return",
                               null, 0, 0);
                Assert.IsNotNull(
                    ret.Expressions,
                    "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Return.Expressions");
                Assert.AreEqual(
                    0, ret.Expressions.Count,
                    "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Return.Expressions.Count");
              }

              // Validate the statement
              {
                Assert.IsNotNull(funcBlock.Children,
                                 "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children");
                Assert.AreEqual(
                    1, funcBlock.Children.Count,
                    "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children.Count");

                // print ( i )
                {
                  FuncCallItem call = funcBlock.Children[0] as FuncCallItem;
                  Assert.IsNotNull(
                      call, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0]");
                  Assert.AreEqual(
                      true, call.Statement,
                      "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Statement");
                  Assert.IsNull(
                      call.InstanceName,
                      "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].InstanceName");
                  _validateDebug(
                      call.Debug,
                      "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0]", "print",
                      2, 24);

                  // Validate the prefix
                  {
                    NameItem name = call.Prefix as NameItem;
                    Assert.IsNotNull(
                        call.Prefix,
                        "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Prefix");
                    Assert.AreEqual(
                        "print", name.Name,
                        "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Prefix.Name");
                    _validateDebug(
                      name.Debug,
                      "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Prefix.Name",
                      "print", 2, 24);
                  }

                  // Validate the arguments
                  {
                    Assert.IsNotNull(
                        call.Arguments,
                        "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Arguments");
                    Assert.AreEqual(
                        1, call.Arguments.Count,
                        "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Arguments.Count");

                    NameItem name = call.Arguments[0].Expression as NameItem;
                    Assert.IsNotNull(
                        name,
                        "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Arguments[0]");
                    Assert.AreEqual(
                        "i", name.Name,
                        "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Arguments[0].Name");
                    _validateDebug(
                        name.Debug,
                        "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Arguments[0]",
                        "i", 2, 30);
                  }
                }
              }
            }
          }
        }
      }

      // function Some(a, ...)
      {
        FuncDefItem func = block.Children[2] as FuncDefItem;
        Assert.IsNotNull(func, "Block.Children[2]");
        Assert.AreEqual(false, func.Local, "Block.Children[2].Local");
        Assert.IsNull(func.InstanceName, "Block.Children[2].InstanceName");
        _validateDebug(func.Debug, "Block.Children[2]", "function", 3, 1);

        // Validate the block
        {
          BlockItem someBlock = func.Block;
          _validateDebug(someBlock.Debug, "Block.Children[2].Block", "a", 4, 5);

          // Validate the return
          {
            ReturnItem ret = someBlock.Return;
            Assert.IsNotNull(ret, "Block.Children[2].Block.Return");
            _validateDebug(ret.Debug, "Block.Children[2].Block.Return", null, 0, 0);
            Assert.IsNotNull(ret.Expressions, "Block.Children[2].Block.Return.Expressions");
            Assert.AreEqual(0, ret.Expressions.Count,
                            "Block.Children[2].Block.Return.Expressions.Count");
          }

          // Check the children
          {
            Assert.IsNotNull(someBlock.Children, "Block.Children[2].Block.Children");
            Assert.AreEqual(2, someBlock.Children.Count, "Block.Children[2].Block.Children.Count");

            // a , b , c = ...
            {
              AssignmentItem varInit = someBlock.Children[0] as AssignmentItem;
              Assert.IsNotNull(varInit, "Block.Children[2].Block.Children[0]");
              Assert.AreEqual(false, varInit.Local, "Block.Children[2].Block.Children[0].Local");
              _validateDebug(varInit.Debug, "Block.Children[2].Block.Children[0]", "a", 4, 5);

              // Validate the names
              {
                Assert.IsNotNull(varInit.Names, "Block.Children[2].Block.Children[0].Names");
                Assert.AreEqual(3, varInit.Names.Count,
                                "Block.Children[2].Block.Children[0].Names.Count");

                NameItem name = varInit.Names[0] as NameItem;
                Assert.IsNotNull(name, "Block.Children[2].Block.Children[0].Names[0]");
                Assert.AreEqual(name.Name, "a",
                                "Block.Children[2].Block.Children[0].Names[0].Name");
                _validateDebug(name.Debug, "Block.Children[2].Block.Children[0].Names[0]", "a", 4,
                               5);

                name = varInit.Names[1] as NameItem;
                Assert.IsNotNull(name, "Block.Children[2].Block.Children[0].Names[1]");
                Assert.AreEqual(name.Name, "b",
                                "Block.Children[2].Block.Children[0].Names[1].Name");
                _validateDebug(name.Debug, "Block.Children[2].Block.Children[0].Names[1]", "b", 4,
                               8);

                name = varInit.Names[2] as NameItem;
                Assert.IsNotNull(name, "Block.Children[2].Block.Children[0].Names[2]");
                Assert.AreEqual(name.Name, "c",
                                "Block.Children[2].Block.Children[0].Names[2].Name");
                _validateDebug(name.Debug, "Block.Children[2].Block.Children[0].Names[2]", "c", 4,
                               11);
              }
              // Validate the expressions
              {
                Assert.IsNotNull(varInit.Expressions,
                                 "Block.Children[2].Block.Children[0].Expressions");
                Assert.AreEqual(1, varInit.Expressions.Count,
                                "Block.Children[2].Block.Children[0].Expressions.Count");

                NameItem name = varInit.Expressions[0] as NameItem;
                Assert.IsNotNull(name, "Block.Children[2].Block.Children[0].Expressions[0]");
                Assert.AreEqual(name.Name, "...",
                                "Block.Children[2].Block.Children[0].Expressions[0].Name");
                _validateDebug(name.Debug, "Block.Children[2].Block.Children[0].Expressions[0]",
                               "...", 4, 15);
              }
            }
            // for i= 12, 23 do print ( i ) end
            {
              ForNumItem forLoop = someBlock.Children[1] as ForNumItem;
              Assert.IsNotNull(forLoop, "Block.Children[2].Block.Children[1]");
              _validateDebug(forLoop.Debug, "Block.Children[2].Block.Children[1]", "for", 5, 5);

              // Validate the name
              {
                NameItem name = forLoop.Name;
                Assert.IsNotNull(name, "Block.Children[2].Block.Children[1].Name");
                _validateDebug(name.Debug, "Block.Children[2].Block.Children[1].Name", "i", 5, 9);
                Assert.AreEqual(name.Name, "i", "Block.Children[2].Block.Children[1].Name.Name");
              }

              // Validate the start
              {
                LiteralItem lit = forLoop.Start as LiteralItem;
                Assert.IsNotNull(lit, "Block.Children[2].Block.Children[1].Start");
                Assert.AreEqual(12.0, lit.Value, "Block.Children[2].Block.Children[1].Start.Value");
                _validateDebug(lit.Debug, "Block.Children[2].Block.Children[1].Start", "12", 5, 12);
              }

              // Validate the limit
              {
                LiteralItem lit = forLoop.Limit as LiteralItem;
                Assert.IsNotNull(lit, "Block.Children[2].Block.Children[1].Limit");
                Assert.AreEqual(23.0, lit.Value, "Block.Children[2].Block.Children[1].Limit.Value");
                _validateDebug(lit.Debug, "Block.Children[2].Block.Children[1].Limit", "23", 5, 16);
              }

              // Validate the step
              {
                Assert.IsNull(forLoop.Step, "Block.Children[2].Block.Children[1].Step");
              }

              // Validate the block
              {
                BlockItem forBlock = forLoop.Block;
                _validateDebug(forBlock.Debug, "Block.Children[2].Block.Children[1].Block", "print",
                               6, 9);
                Assert.IsNull(forBlock.Return, "Block.Children[2].Block.Children[1].Block.Return");

                // Validate the statement
                {
                  Assert.IsNotNull(forBlock.Children,
                                   "Block.Children[2].Block.Children[1].Block.Children");
                  Assert.AreEqual(1, forBlock.Children.Count,
                                  "Block.Children[2].Block.Children[1].Block.Children.Count");

                  // print ( i )
                  {
                    FuncCallItem call = forBlock.Children[0] as FuncCallItem;
                    Assert.IsNotNull(call, "Block.Children[2].Block.Children[1].Block.Children[0]");
                    Assert.AreEqual(
                        true, call.Statement,
                        "Block.Children[2].Block.Children[1].Block.Children[0].Statement");
                    Assert.IsNull(
                        call.InstanceName,
                        "Block.Children[2].Block.Children[1].Block.Children[0].InstanceName");
                    _validateDebug(
                        call.Debug, "Block.Children[2].Block.Children[1].Block.Children[0]",
                        "print", 6, 9);

                    // Validate the prefix
                    {
                      NameItem name = call.Prefix as NameItem;
                      Assert.IsNotNull(
                          call.Prefix,
                          "Block.Children[2].Block.Children[1].Block.Children[0].Prefix");
                      Assert.AreEqual(
                          "print", name.Name,
                          "Block.Children[2].Block.Children[1].Block.Children[0].Prefix.Name");
                      _validateDebug(
                          name.Debug,
                          "Block.Children[2].Block.Children[1].Block.Children[0].Prefix.Name",
                          "print", 6, 9);
                    }

                    // Validate the arguments
                    {
                      Assert.IsNotNull(
                          call.Arguments,
                          "Block.Children[2].Block.Children[1].Block.Children[0].Arguments");
                      Assert.AreEqual(
                          1, call.Arguments.Count,
                          "Block.Children[2].Block.Children[1].Block.Children[0].Arguments.Count");

                      NameItem name = call.Arguments[0].Expression as NameItem;
                      Assert.IsNotNull(
                          name,
                          "Block.Children[2].Block.Children[1].Block.Children[0].Arguments[0]");
                      Assert.AreEqual(
                          "i", name.Name,
                          "Block.Children[2].Block.Children[1].Block.Children[0].Arguments[0].Name");
                      _validateDebug(
                          name.Debug,
                          "Block.Children[2].Block.Children[1].Block.Children[0].Arguments[0]", "i",
                          6, 15);
                    }
                  }
                }
              }
            }
          }
        }
      }
    }

    [Test]
    public void ReadExp_NestedUnaryOps() {
      var item = _parseExpression<IParseExp>("-#not foo");
      var types = new[] {
          UnaryOperationType.Minus,
          UnaryOperationType.Length,
          UnaryOperationType.Not,
      };
      foreach (var type in types) {
        var cur = item as UnOpItem;
        Assert.IsNotNull(cur);
        Assert.AreEqual(type, cur.OperationType);
        item = cur.Target;
      }
    }

    [Test]
    public void ReadExp_UnaryWithAdd() {
      // This should be parsed as (-foo)+bar
      var item = _parseExpression<BinOpItem>("-foo+bar");
      var first = item.Lhs as UnOpItem;
      Assert.IsNotNull(first);
      Assert.AreEqual(UnaryOperationType.Minus, first.OperationType);

      // This should be parsed as foo + (not bar)
      item = _parseExpression<BinOpItem>("foo + not bar");
      var second = item.Rhs as UnOpItem;
      Assert.IsNotNull(second);
      Assert.AreEqual(UnaryOperationType.Not, second.OperationType);
    }

    [Test]
    public void ReadExp_UnaryWithPower() {
      // This should be parsed as -(foo^bar)
      var item = _parseExpression<UnOpItem>("-foo^bar");
      Assert.AreEqual(UnaryOperationType.Minus, item.OperationType);
      var target = item.Target as BinOpItem;
      Assert.IsNotNull(target);
      Assert.AreEqual(BinaryOperationType.Power, target.OperationType);
    }

    [Test]
    public void ReadExp_UnaryKeepsPrecedence() {
      // This should be parsed as (foo * (-bar)) + baz
      var item = _parseExpression<BinOpItem>("foo*-bar+baz");
      Assert.AreEqual(BinaryOperationType.Add, item.OperationType);
      var left = item.Lhs as BinOpItem;
      Assert.IsNotNull(left);
      Assert.AreEqual(BinaryOperationType.Multiply, left.OperationType);
      var neg = left.Rhs as UnOpItem;
      Assert.IsNotNull(neg);
      Assert.AreEqual(UnaryOperationType.Minus, neg.OperationType);
    }

    [Test]
    public void ReadExp_BinaryHandlesPrecedence() {
      // This should be parsed as foo+(bar*baz)
      var item = _parseExpression<BinOpItem>("foo+bar*baz");
      Assert.AreEqual(BinaryOperationType.Add, item.OperationType);
      var right = item.Rhs as BinOpItem;
      Assert.IsNotNull(right);
      Assert.AreEqual(BinaryOperationType.Multiply, right.OperationType);
    }

    [Test]
    public void ReadExp_BinaryHandlesRightAssociative() {
      // This should be parsed as foo^(bar^baz)
      var item = _parseExpression<BinOpItem>("foo^bar^baz");
      Assert.AreEqual(BinaryOperationType.Power, item.OperationType);
      _assertIsVariable(item.Lhs, "foo");
      var right = item.Rhs as BinOpItem;
      Assert.IsNotNull(right);
      Assert.AreEqual(BinaryOperationType.Power, right.OperationType);
      _assertIsVariable(right.Lhs, "bar");
      _assertIsVariable(right.Rhs, "baz");
    }

    [Test]
    public void ReadExp_Literals() {
      var str = "foo(nil, false, true, 123, 'foo')";
      var item = _parseExpression<FuncCallItem>(str);
      Assert.AreEqual(5, item.Arguments.Count);
      _assertIsLiteral(item.Arguments[0].Expression, null);
      _assertIsLiteral(item.Arguments[1].Expression, false);
      _assertIsLiteral(item.Arguments[2].Expression, true);
      _assertIsLiteral(item.Arguments[3].Expression, 123.0);
      _assertIsLiteral(item.Arguments[4].Expression, "foo");
    }

    [Test]
    public void ReadExp_Indexer() {
      var item = _parseExpression<IndexerItem>("foo.bar[baz + 1]");
      var left = item.Prefix as IndexerItem;
      Assert.IsNotNull(left);
      _assertIsVariable(left.Prefix, "foo");
      _assertIsLiteral(left.Expression, "bar");
      var right = item.Expression as BinOpItem;
      Assert.IsNotNull(right);
    }

    [Test]
    public void ReadExp_Call() {
      string str = "foo(foo, 1 + 2, ref i, ref(j[2]), @j.bar)";
      var item = _parseExpression<FuncCallItem>(str);
      _assertIsVariable(item.Prefix, "foo");
      Assert.IsFalse(item.IsLastArgSingle);
      Assert.IsNull(item.InstanceName);
      Assert.AreEqual(-1, item.Overload);
      Assert.IsFalse(item.Statement);

      Assert.AreEqual(5, item.Arguments.Count);
      _assertIsVariable(item.Arguments[0].Expression, "foo");
      Assert.IsFalse(item.Arguments[0].IsByRef);
      Assert.IsInstanceOf<BinOpItem>(item.Arguments[1].Expression);
      Assert.IsFalse(item.Arguments[1].IsByRef);
      _assertIsVariable(item.Arguments[2].Expression, "i");
      Assert.IsTrue(item.Arguments[2].IsByRef);
      Assert.IsInstanceOf<IndexerItem>(item.Arguments[3].Expression);
      Assert.IsTrue(item.Arguments[3].IsByRef);
      Assert.IsInstanceOf<IndexerItem>(item.Arguments[4].Expression);
      Assert.IsTrue(item.Arguments[4].IsByRef);
    }

    [Test]
    public void ReadExp_CallLastSingle() {
      var item = _parseExpression<FuncCallItem>("foo((x))");
      Assert.IsTrue(item.IsLastArgSingle);
      Assert.AreEqual(1, item.Arguments.Count);
      _assertIsVariable(item.Arguments[0].Expression, "x");
    }

    [Test]
    public void ReadExp_CallInstanceMethod() {
      var item = _parseExpression<FuncCallItem>("bar:foo()");
      _assertIsVariable(item.Prefix, "bar");
      Assert.AreEqual("foo", item.InstanceName);
    }

    [Test]
    public void ReadExp_CallOverload() {
      var item = _parseExpression<FuncCallItem>("foo`123()");
      Assert.AreEqual(123, item.Overload);
      item = _parseExpression<FuncCallItem>("bar:foo`456()");
      Assert.AreEqual(456, item.Overload);
    }

    [Test]
    public void ReadExp_CallWithString() {
      var item = _parseExpression<FuncCallItem>("foo 'bar'");
      _assertIsVariable(item.Prefix, "foo");
      Assert.AreEqual(1, item.Arguments.Count);
      _assertIsLiteral(item.Arguments[0].Expression, "bar");
      Assert.IsFalse(item.Arguments[0].IsByRef);
    }

    [Test]
    public void ReadExp_CallWithTable() {
      var item = _parseExpression<FuncCallItem>("foo {}");
      _assertIsVariable(item.Prefix, "foo");
      Assert.AreEqual(1, item.Arguments.Count);
      Assert.IsInstanceOf<TableItem>(item.Arguments[0].Expression);
      Assert.IsFalse(item.Arguments[0].IsByRef);
    }

    [Test]
    public void ReadTable_Empty() {
      var item = _parseExpression<TableItem>("{}");
      Assert.AreEqual(0, item.Fields.Count);
    }

    [Test]
    public void ReadTable_AcceptsNamedKeys() {
      var item = _parseExpression<TableItem>("{x=1,y=2}");
      Assert.AreEqual(2, item.Fields.Count);
      _assertIsLiteral(item.Fields[0].Key, "x");
      _assertIsLiteral(item.Fields[0].Value, 1);
      _assertIsLiteral(item.Fields[1].Key, "y");
      _assertIsLiteral(item.Fields[1].Value, 2);
    }

    [Test]
    public void ReadTable_AcceptsExpressionKeys() {
      var item = _parseExpression<TableItem>("{[x+1]=1}");
      Assert.AreEqual(1, item.Fields.Count);
      Assert.IsInstanceOf<BinOpItem>(item.Fields[0].Key);
      _assertIsLiteral(item.Fields[0].Value, 1);
    }

    [Test]
    public void ReadTable_AcceptsJustValues() {
      var item = _parseExpression<TableItem>("{10; 20; 30}");
      Assert.AreEqual(3, item.Fields.Count);
      _assertIsLiteral(item.Fields[0].Key, 1);
      _assertIsLiteral(item.Fields[0].Value, 10);
      _assertIsLiteral(item.Fields[1].Key, 2);
      _assertIsLiteral(item.Fields[1].Value, 20);
      _assertIsLiteral(item.Fields[2].Key, 3);
      _assertIsLiteral(item.Fields[2].Value, 30);
    }

    [Test]
    public void ReadTable_AcceptsMixedKeysAndValues() {
      var str = "{ [f(1)] = g; 'x', 'y'; x = 1, f(x), [30] = 23; 45 }";
      var item = _parseExpression<TableItem>(str);
      Assert.AreEqual(7, item.Fields.Count);
      Assert.IsInstanceOf<FuncCallItem>(item.Fields[0].Key);
      _assertIsVariable(item.Fields[0].Value, "g");
      _assertIsLiteral(item.Fields[1].Key, 1);
      _assertIsLiteral(item.Fields[1].Value, "x");
      _assertIsLiteral(item.Fields[2].Key, 2);
      _assertIsLiteral(item.Fields[2].Value, "y");
      _assertIsLiteral(item.Fields[3].Key, "x");
      _assertIsLiteral(item.Fields[3].Value, 1);
      _assertIsLiteral(item.Fields[4].Key, 3);
      Assert.IsInstanceOf<FuncCallItem>(item.Fields[4].Value);
      _assertIsLiteral(item.Fields[5].Key, 30);
      _assertIsLiteral(item.Fields[5].Value, 23);
      _assertIsLiteral(item.Fields[6].Key, 4);
      _assertIsLiteral(item.Fields[6].Value, 45);
    }

    [Test]
    public void ReadStatement_ClassOldStyle() {
      var str = "class \"Foo\" (Bar, Baz)";
      var item = _parseStatement<ClassDefItem>(str);
      Assert.AreEqual("Foo", item.Name);
      Assert.AreEqual(2, item.Implements.Count);
      Assert.AreEqual("Bar", item.Implements[0]);
      Assert.AreEqual("Baz", item.Implements[1]);
    }

    [Test]
    public void ReadStatement_ClassNewStyle() {
      var str = "class Foo : Bar, Baz";
      var item = _parseStatement<ClassDefItem>(str);
      Assert.AreEqual("Foo", item.Name);
      Assert.AreEqual(2, item.Implements.Count);
      Assert.AreEqual("Bar", item.Implements[0]);
      Assert.AreEqual("Baz", item.Implements[1]);
    }

    [Test]
    public void ReadStatement_ForGeneric() {
      var str = "for x, y in foo do end";
      var item = _parseStatement<ForGenItem>(str);
      Assert.AreEqual(2, item.Names.Count);
      Assert.AreEqual("x", item.Names[0].Name);
      Assert.AreEqual("y", item.Names[1].Name);
      Assert.AreEqual(1, item.Expressions.Count);
      _assertIsVariable(item.Expressions[0], "foo");
      Assert.AreEqual(0, item.Block.Children.Count);
    }

    [Test]
    public void ReadStatement_ForNumber() {
      var str = "for x = i, j, k do end";
      var item = _parseStatement<ForNumItem>(str);
      Assert.AreEqual("x", item.Name.Name);
      _assertIsVariable(item.Start, "i");
      _assertIsVariable(item.Limit, "j");
      _assertIsVariable(item.Step, "k");
      Assert.AreEqual(0, item.Block.Children.Count);
    }

    [Test]
    public void ReadStatement_If() {
      var str = "if i then end";
      var item = _parseStatement<IfItem>(str);
      _assertIsVariable(item.Expression, "i");
      Assert.AreEqual(0, item.Block.Children.Count);
      Assert.AreEqual(0, item.Elses.Count);
      Assert.IsNull(item.ElseBlock);
    }

    [Test]
    public void ReadStatement_IfElse() {
      var str = "if i then else end";
      var item = _parseStatement<IfItem>(str);
      _assertIsVariable(item.Expression, "i");
      Assert.AreEqual(0, item.Block.Children.Count);
      Assert.AreEqual(0, item.Elses.Count);
      Assert.AreEqual(0, item.ElseBlock.Children.Count);
    }

    [Test]
    public void ReadStatement_IfElseIf() {
      var str = "if i then elseif y then x = 1 end";
      var item = _parseStatement<IfItem>(str);
      _assertIsVariable(item.Expression, "i");
      Assert.AreEqual(0, item.Block.Children.Count);
      Assert.IsNull(item.ElseBlock);
      Assert.AreEqual(1, item.Elses.Count);
      _assertIsVariable(item.Elses[0].Expression, "y");
      Assert.AreEqual(1, item.Elses[0].Block.Children.Count);
      Assert.IsInstanceOf<AssignmentItem>(item.Elses[0].Block.Children[0]);
    }

    [Test]
    public void ReadStatement_Repeat() {
      var str = "repeat x = 1 until i";
      var item = _parseStatement<RepeatItem>(str);
      _assertIsVariable(item.Expression, "i");
      Assert.AreEqual(1, item.Block.Children.Count);
      Assert.IsInstanceOf<AssignmentItem>(item.Block.Children[0]);
    }

    [Test]
    public void ReadStatement_Label() {
      var item = _parseStatement<LabelItem>("::foo::");
      Assert.AreEqual("foo", item.Name);
    }

    [Test]
    public void ReadStatement_Goto() {
      var item = _parseStatement<GotoItem>("goto x");
      Assert.AreEqual("x", item.Name);
    }

    [Test]
    public void ReadStatement_Do() {
      var item = _parseStatement<BlockItem>("do x = 1 end");
      Assert.IsNull(item.Return);
      Assert.AreEqual(1, item.Children.Count);
      Assert.IsInstanceOf<AssignmentItem>(item.Children[0]);
    }

    [Test]
    public void ReadStatement_While() {
      var str = "while i do x = 1 y = 2 end";
      var item = _parseStatement<WhileItem>(str);
      _assertIsVariable(item.Expression, "i");
      Assert.IsNull(item.Block.Return);
      Assert.AreEqual(2, item.Block.Children.Count);
      Assert.IsInstanceOf<AssignmentItem>(item.Block.Children[0]);
      Assert.IsInstanceOf<AssignmentItem>(item.Block.Children[1]);
    }

    [Test]
    public void SyntaxError() {
      var tests = new SyntaxErrorCollection() {
          { "local", 6 },

          { "func(ref(x, foo)", 11 },
          { "func(x,,)", 8 },
          { "func(x 5)", 8 },
          { "x = foo[2)", 10 },
          { "x = foo(1]", 10 },
          { "x = foo.345", 9 },

          { "{123, [345 + 2 = 3}", 16 },
          { "{123, ", 7 },
          { "{123,,", 6 },
          { "{123;;", 6 },
          { "{[123] 34}", 8 },
          { "{123", 5 },
          { "{123 end", 6 },

          { "if x", 5 },
          { "if x else end", 6 },
          { "if x end", 6 },
          { "if x then elseif x end", 20 },
          { "for x = 1 do end", 11 },
          { "for x = 1,, do end", 11 },
          { "for x = 1, 2, do end", 15 },
          { "for x, 9 in x do end", 8 },
          { "while i end", 9 },
          { "::foo x = 1", 7 },
          { "::end::", 3 },
      };
      foreach (var test in tests) {
        try {
          _parseBlock(test.Item1);
          Assert.Fail("Expected to throw error: {0}", test.Item1);
        } catch (SyntaxException e) {
          Assert.AreEqual(test.Item2, e.SourceToken.StartPos,
                          "Error in wrong position, Code: {0}, Message: {1}",
                          test.Item1, e.Message);
        }
      }
    }
  }
}
