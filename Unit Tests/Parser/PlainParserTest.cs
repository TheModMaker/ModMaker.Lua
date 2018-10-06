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

using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace UnitTests.Parser
{
    /// <summary>
    /// This is a test class for PlainParserTest and is intended
    /// to contain all PlainParserTest Unit Tests
    ///</summary>
    [TestFixture]
    public class PlainParserTest
    {
        class SyntaxErrorCollection : List<Tuple<string, int>>
        {
            public SyntaxErrorCollection() { }
            public void Add(string body, int column)
            {
                Add(new Tuple<string, int>(body, column));
            }
        }

        void ValidateDebug(Token debug, string prefix, string value, long startLine, long startPos)
        {
            Assert.AreEqual(value, debug.Value, prefix + ".Debug.Value");
            Assert.AreEqual(startLine, debug.StartLine, prefix + ".Debug.StartLine");
            Assert.AreEqual(startPos, debug.StartPos, prefix + ".Debug.StartPos");
        }

        IParseItem ParseBlock(string input)
        {
            PlainParser target = new PlainParser();
            return target.Parse(TokenizerTest.CreateTokenizer(input), null, null);
        }

        /// <summary>
        /// Parses the given statement code.
        /// </summary>
        /// <param name="input">The input code.</param>
        /// <returns>The parsed statement.</returns>
        T ParseStatement<T>(string input) where T : class
        {
            var block = ParseBlock(input) as BlockItem;
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
        T ParseExpression<T>(string input) where T : class
        {
            var assign = ParseStatement<AssignmentItem>("local x = " + input);
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
        void AssertIsVariable(IParseItem item, string name)
        {
            var nameItem = item as NameItem;
            Assert.IsNotNull(nameItem);
            Assert.AreEqual(name, nameItem.Name);
        }

        /// <summary>
        /// Asserts the given parsed item is a literal with the given value.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <param name="value">The expected value of the literal.</param>
        void AssertIsLiteral(IParseItem item, object value)
        {
            var literal = item as LiteralItem;
            Assert.IsNotNull(literal);
            Assert.AreEqual(value, literal.Value);
        }


        /// <summary>
        /// A test for PlainParser.Parse with valid input
        /// testing for valid DebugInfo also.
        ///</summary>
        [Test]
        public void GenralParse()
        {
            string input1 =
@"local a = 12
t = { [34]= function() print(i) end }
function Some(a, ...)
    a, b, c = ...
    for i= 12, 23 do
        print(i)
    end
end";
            IParseItem actual = ParseBlock(input1);

            // check the main block
            BlockItem block = actual as BlockItem;
            Assert.IsInstanceOf<BlockItem>(actual);
            Assert.IsNotNull(block.Children);
            Assert.AreEqual(3, block.Children.Count, "Block.Children.Count");
            ValidateDebug(block.Debug, "Block", "local", 1, 1);

            // check the return statement of the main block
            {
                ReturnItem ret = block.Return;
                Assert.IsInstanceOf<ReturnItem>(block.Return);
                ValidateDebug(ret.Debug, "Block.Return", null, 0, 0);
                Assert.IsNotNull(ret.Expressions);
                Assert.AreEqual(0, ret.Expressions.Count);
            }

            // local a = 12
            {
                AssignmentItem init = block.Children[0] as AssignmentItem;
                Assert.IsNotNull(init, "Block.Children[0]");
                Assert.AreEqual(true, init.Local);
                ValidateDebug(init.Debug, "Block.Children[0]", "local", 1, 1);

                // check the names
                {
                    Assert.IsNotNull(init.Names, "Block.Children[0].Names");
                    Assert.AreEqual(1, init.Names.Count, "Block.Children[0].Names.Count");

                    NameItem name = init.Names[0] as NameItem;
                    Assert.IsNotNull(name, "Block.Children[0].Names[0]");
                    Assert.AreEqual("a", name.Name, "Block.Children[0].Names[0].Name");
                    ValidateDebug(name.Debug, "Block.Children[0].Names[0]", "a", 1, 7);
                }

                // check the expressions
                {
                    Assert.IsNotNull(init.Expressions, "Block.Children[0].Expressions");
                    Assert.AreEqual(1, init.Expressions.Count, "Block.Children[0].Expressions.Count");

                    LiteralItem literal = init.Expressions[0] as LiteralItem;
                    Assert.IsNotNull(literal, "Block.Children[0].Expressions[0]");
                    Assert.AreEqual(12.0, literal.Value, "Block.Children[0].Expressions[0].Value");
                    ValidateDebug(literal.Debug, "Block.Children[0].Expressions[0]", "12", 1, 11);
                }
            }

            // t = { [34]= function() print(i) end }
            {
                AssignmentItem init = block.Children[1] as AssignmentItem;
                Assert.IsNotNull(init, "Block.Children[1]");
                Assert.AreEqual(false, init.Local);
                ValidateDebug(init.Debug, "Block.Children[1]", "t", 2, 1);

                // check the names
                {
                    Assert.IsNotNull(init.Names, "Block.Children[1].Names");
                    Assert.AreEqual(1, init.Names.Count, "Block.Children[1].Names.Count");

                    NameItem name = init.Names[0] as NameItem;
                    Assert.IsNotNull(name, "Block.Children[1].Names[0]");
                    Assert.AreEqual("t", name.Name, "Block.Children[1].Names[0].Name");
                    ValidateDebug(name.Debug, "Block.Children[1].Names[0]", "t", 2, 1);
                }

                // check the expressions
                {
                    Assert.IsNotNull(init.Expressions, "Block.Children[1].Expressions");
                    Assert.AreEqual(1, init.Expressions.Count, "Block.Children[1].Expressions.Count");

                    TableItem table = init.Expressions[0] as TableItem;
                    Assert.IsNotNull(table, "Block.Children[1].Expressions[0]");
                    ValidateDebug(table.Debug, "Block.Children[1].Expressions[0]", "{", 2, 5);

                    Assert.IsNotNull(table.Fields, "Block.Children[1].Expressions[0].Fields");
                    Assert.AreEqual(1, table.Fields.Count, "Block.Children[1].Expressions[0].Fields.Count");

                    var field = table.Fields[0];
                    {
                        LiteralItem literal = field.Key as LiteralItem;
                        Assert.IsNotNull(literal, "Block.Children[1].Expressions[0].Fields[0].Item1");
                        Assert.AreEqual(34.0, literal.Value, "Block.Children[1].Expressions[0].Fields[0].Item1.Value");
                        ValidateDebug(literal.Debug, "Block.Children[1].Expressions[0].Fields[0].Item1", "34", 2, 8);
                    }
                    {
                        FuncDefItem func = field.Value as FuncDefItem;
                        Assert.IsNotNull(func, "Block.Children[1].Expressions[0].Fields[0].Item2");
                        Assert.IsNull(func.InstanceName, "Block.Children[1].Expressions[0].Fields[0].Item2.InstanceName");
                        Assert.IsNull(func.Prefix, "Block.Children[1].Expressions[0].Fields[0].Item2.Prefix");
                        Assert.AreEqual(false, func.Local, "Block.Children[1].Expressions[0].Fields[0].Item2.Local");
                        Assert.IsNull(func.FunctionInformation, "Block.Children[1].Expressions[0].Fields[0].Item2.FunctionInformation");
                        ValidateDebug(func.Debug, "Block.Children[1].Expressions[0].Fields[0].Item2", "function", 2, 13);

                        // validate the block
                        {
                            BlockItem funcBlock = func.Block;
                            Assert.IsNotNull(funcBlock, "Block.Children[1].Expressions[0].Fields[0].Item2.Block");
                            ValidateDebug(funcBlock.Debug, "Block.Children[1].Expressions[0].Fields[0].Item2.Block", "print", 2, 24);


                            // validate the return
                            {
                                ReturnItem ret = funcBlock.Return;
                                Assert.IsNotNull(ret, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Return");
                                ValidateDebug(ret.Debug, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Return", null, 0, 0);
                                Assert.IsNotNull(ret.Expressions, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Return.Expressions");
                                Assert.AreEqual(0, ret.Expressions.Count, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Return.Expressions.Count");
                            }

                            // validate the statement
                            {
                                Assert.IsNotNull(funcBlock.Children, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children");
                                Assert.AreEqual(1, funcBlock.Children.Count, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children.Count");

                                // print ( i )
                                {
                                    FuncCallItem call = funcBlock.Children[0] as FuncCallItem;
                                    Assert.IsNotNull(call, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0]");
                                    Assert.AreEqual(true, call.Statement, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Statement");
                                    Assert.IsNull(call.InstanceName, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].InstanceName");
                                    ValidateDebug(call.Debug, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0]", "print", 2, 24);

                                    // validate the prefix
                                    {
                                        NameItem name = call.Prefix as NameItem;
                                        Assert.IsNotNull(call.Prefix, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Prefix");
                                        Assert.AreEqual("print", name.Name, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Prefix.Name");
                                        ValidateDebug(name.Debug, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Prefix.Name", "print", 2, 24);
                                    }

                                    // validate the arguments
                                    {
                                        Assert.IsNotNull(call.Arguments, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Arguments");
                                        Assert.AreEqual(1, call.Arguments.Count, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Arguments.Count");

                                        NameItem name = call.Arguments[0].Expression as NameItem;
                                        Assert.IsNotNull(name, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Arguments[0]");
                                        Assert.AreEqual("i", name.Name, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Arguments[0].Name");
                                        ValidateDebug(name.Debug, "Block.Children[1].Expressions[0].Fields[0].Item2.Block.Children[0].Arguments[0]", "i", 2, 30);
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
                ValidateDebug(func.Debug, "Block.Children[2]", "function", 3, 1);

                // validate the block
                {
                    BlockItem someBlock = func.Block;
                    ValidateDebug(someBlock.Debug, "Block.Children[2].Block", "a", 4, 5);

                    // validate the return
                    {
                        ReturnItem ret = someBlock.Return;
                        Assert.IsNotNull(ret, "Block.Children[2].Block.Return");
                        ValidateDebug(ret.Debug, "Block.Children[2].Block.Return", null, 0, 0);
                        Assert.IsNotNull(ret.Expressions, "Block.Children[2].Block.Return.Expressions");
                        Assert.AreEqual(0, ret.Expressions.Count, "Block.Children[2].Block.Return.Expressions.Count");
                    }

                    // check the children
                    {
                        Assert.IsNotNull(someBlock.Children, "Block.Children[2].Block.Children");
                        Assert.AreEqual(2, someBlock.Children.Count, "Block.Children[2].Block.Children.Count");

                        // a , b , c = ...
                        {
                            AssignmentItem varInit = someBlock.Children[0] as AssignmentItem;
                            Assert.IsNotNull(varInit, "Block.Children[2].Block.Children[0]");
                            Assert.AreEqual(false, varInit.Local, "Block.Children[2].Block.Children[0].Local");
                            ValidateDebug(varInit.Debug, "Block.Children[2].Block.Children[0]", "a", 4, 5);

                            // validate the names
                            {
                                Assert.IsNotNull(varInit.Names, "Block.Children[2].Block.Children[0].Names");
                                Assert.AreEqual(3, varInit.Names.Count, "Block.Children[2].Block.Children[0].Names.Count");

                                NameItem name = varInit.Names[0] as NameItem;
                                Assert.IsNotNull(name, "Block.Children[2].Block.Children[0].Names[0]");
                                Assert.AreEqual(name.Name, "a", "Block.Children[2].Block.Children[0].Names[0].Name");
                                ValidateDebug(name.Debug, "Block.Children[2].Block.Children[0].Names[0]", "a", 4, 5);

                                name = varInit.Names[1] as NameItem;
                                Assert.IsNotNull(name, "Block.Children[2].Block.Children[0].Names[1]");
                                Assert.AreEqual(name.Name, "b", "Block.Children[2].Block.Children[0].Names[1].Name");
                                ValidateDebug(name.Debug, "Block.Children[2].Block.Children[0].Names[1]", "b", 4, 8);

                                name = varInit.Names[2] as NameItem;
                                Assert.IsNotNull(name, "Block.Children[2].Block.Children[0].Names[2]");
                                Assert.AreEqual(name.Name, "c", "Block.Children[2].Block.Children[0].Names[2].Name");
                                ValidateDebug(name.Debug, "Block.Children[2].Block.Children[0].Names[2]", "c", 4, 11);
                            }
                            // validate the expressions
                            {
                                Assert.IsNotNull(varInit.Expressions, "Block.Children[2].Block.Children[0].Expressions");
                                Assert.AreEqual(1, varInit.Expressions.Count, "Block.Children[2].Block.Children[0].Expressions.Count");

                                NameItem name = varInit.Expressions[0] as NameItem;
                                Assert.IsNotNull(name, "Block.Children[2].Block.Children[0].Expressions[0]");
                                Assert.AreEqual(name.Name, "...", "Block.Children[2].Block.Children[0].Expressions[0].Name");
                                ValidateDebug(name.Debug, "Block.Children[2].Block.Children[0].Expressions[0]", "...", 4, 15);
                            }
                        }
                        // for i= 12, 23 do print ( i ) end
                        {
                            ForNumItem forLoop = someBlock.Children[1] as ForNumItem;
                            Assert.IsNotNull(forLoop, "Block.Children[2].Block.Children[1]");
                            ValidateDebug(forLoop.Debug, "Block.Children[2].Block.Children[1]", "for", 5, 5);

                            // validate the name
                            {
                                NameItem name = forLoop.Name;
                                Assert.IsNotNull(name, "Block.Children[2].Block.Children[1].Name");
                                ValidateDebug(name.Debug, "Block.Children[2].Block.Children[1].Name", "i", 5, 9);
                                Assert.AreEqual(name.Name, "i", "Block.Children[2].Block.Children[1].Name.Name");
                            }

                            // validate the start
                            {
                                LiteralItem lit = forLoop.Start as LiteralItem;
                                Assert.IsNotNull(lit, "Block.Children[2].Block.Children[1].Start");
                                Assert.AreEqual(12.0, lit.Value, "Block.Children[2].Block.Children[1].Start.Value");
                                ValidateDebug(lit.Debug, "Block.Children[2].Block.Children[1].Start", "12", 5, 12);
                            }

                            // validate the limit
                            {
                                LiteralItem lit = forLoop.Limit as LiteralItem;
                                Assert.IsNotNull(lit, "Block.Children[2].Block.Children[1].Limit");
                                Assert.AreEqual(23.0, lit.Value, "Block.Children[2].Block.Children[1].Limit.Value");
                                ValidateDebug(lit.Debug, "Block.Children[2].Block.Children[1].Limit", "23", 5, 16);
                            }

                            // validate the step
                            {
                                Assert.IsNull(forLoop.Step, "Block.Children[2].Block.Children[1].Step");
                            }

                            // validate the block
                            {
                                BlockItem forBlock = forLoop.Block;
                                ValidateDebug(forBlock.Debug, "Block.Children[2].Block.Children[1].Block", "print", 6, 9);
                                Assert.IsNull(forBlock.Return, "Block.Children[2].Block.Children[1].Block.Return");

                                // validate the statement
                                {
                                    Assert.IsNotNull(forBlock.Children, "Block.Children[2].Block.Children[1].Block.Children");
                                    Assert.AreEqual(1, forBlock.Children.Count, "Block.Children[2].Block.Children[1].Block.Children.Count");

                                    // print ( i )
                                    {
                                        FuncCallItem call = forBlock.Children[0] as FuncCallItem;
                                        Assert.IsNotNull(call, "Block.Children[2].Block.Children[1].Block.Children[0]");
                                        Assert.AreEqual(true, call.Statement, "Block.Children[2].Block.Children[1].Block.Children[0].Statement");
                                        Assert.IsNull(call.InstanceName, "Block.Children[2].Block.Children[1].Block.Children[0].InstanceName");
                                        ValidateDebug(call.Debug, "Block.Children[2].Block.Children[1].Block.Children[0]", "print", 6, 9);

                                        // validate the prefix
                                        {
                                            NameItem name = call.Prefix as NameItem;
                                            Assert.IsNotNull(call.Prefix, "Block.Children[2].Block.Children[1].Block.Children[0].Prefix");
                                            Assert.AreEqual("print", name.Name, "Block.Children[2].Block.Children[1].Block.Children[0].Prefix.Name");
                                            ValidateDebug(name.Debug, "Block.Children[2].Block.Children[1].Block.Children[0].Prefix.Name", "print", 6, 9);
                                        }

                                        // validate the arguments
                                        {
                                            Assert.IsNotNull(call.Arguments, "Block.Children[2].Block.Children[1].Block.Children[0].Arguments");
                                            Assert.AreEqual(1, call.Arguments.Count, "Block.Children[2].Block.Children[1].Block.Children[0].Arguments.Count");

                                            NameItem name = call.Arguments[0].Expression as NameItem;
                                            Assert.IsNotNull(name, "Block.Children[2].Block.Children[1].Block.Children[0].Arguments[0]");
                                            Assert.AreEqual("i", name.Name, "Block.Children[2].Block.Children[1].Block.Children[0].Arguments[0].Name");
                                            ValidateDebug(name.Debug, "Block.Children[2].Block.Children[1].Block.Children[0].Arguments[0]", "i", 6, 15);
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
        public void ReadExp_NestedUnaryOps()
        {
            var item = ParseExpression<IParseExp>("-#not foo");
            var types = new[]
            {
                UnaryOperationType.Minus,
                UnaryOperationType.Length,
                UnaryOperationType.Not,
            };
            foreach (var type in types)
            {
                var cur = item as UnOpItem;
                Assert.IsNotNull(cur);
                Assert.AreEqual(type, cur.OperationType);
                item = cur.Target;
            }
        }

        [Test]
        public void ReadExp_UnaryWithAdd()
        {
            // This should be parsed as (-foo)+bar
            var item = ParseExpression<BinOpItem>("-foo+bar");
            var first = item.Lhs as UnOpItem;
            Assert.IsNotNull(first);
            Assert.AreEqual(UnaryOperationType.Minus, first.OperationType);

            // This should be parsed as foo + (not bar)
            item = ParseExpression<BinOpItem>("foo + not bar");
            var second = item.Rhs as UnOpItem;
            Assert.IsNotNull(second);
            Assert.AreEqual(UnaryOperationType.Not, second.OperationType);
        }

        [Test]
        public void ReadExp_UnaryWithPower()
        {
            // This should be parsed as -(foo^bar)
            var item = ParseExpression<UnOpItem>("-foo^bar");
            Assert.AreEqual(UnaryOperationType.Minus, item.OperationType);
            var target = item.Target as BinOpItem;
            Assert.IsNotNull(target);
            Assert.AreEqual(BinaryOperationType.Power, target.OperationType);
        }

        [Test]
        public void ReadExp_UnaryKeepsPrecedence()
        {
            // This should be parsed as (foo * (-bar)) + baz
            var item = ParseExpression<BinOpItem>("foo*-bar+baz");
            Assert.AreEqual(BinaryOperationType.Add, item.OperationType);
            var left = item.Lhs as BinOpItem;
            Assert.IsNotNull(left);
            Assert.AreEqual(BinaryOperationType.Multiply, left.OperationType);
            var neg = left.Rhs as UnOpItem;
            Assert.IsNotNull(neg);
            Assert.AreEqual(UnaryOperationType.Minus, neg.OperationType);
        }

        [Test]
        public void ReadExp_BinaryHandlesPrecedence()
        {
            // This should be parsed as foo+(bar*baz)
            var item = ParseExpression<BinOpItem>("foo+bar*baz");
            Assert.AreEqual(BinaryOperationType.Add, item.OperationType);
            var right = item.Rhs as BinOpItem;
            Assert.IsNotNull(right);
            Assert.AreEqual(BinaryOperationType.Multiply, right.OperationType);
        }

        [Test]
        public void ReadExp_BinaryHandlesRightAssociative()
        {
            // This should be parsed as foo^(bar^baz)
            var item = ParseExpression<BinOpItem>("foo^bar^baz");
            Assert.AreEqual(BinaryOperationType.Power, item.OperationType);
            AssertIsVariable(item.Lhs, "foo");
            var right = item.Rhs as BinOpItem;
            Assert.IsNotNull(right);
            Assert.AreEqual(BinaryOperationType.Power, right.OperationType);
            AssertIsVariable(right.Lhs, "bar");
            AssertIsVariable(right.Rhs, "baz");
        }

        [Test]
        public void ReadExp_Literals()
        {
            var str = "foo(nil, false, true, 123, 'foo')";
            var item = ParseExpression<FuncCallItem>(str);
            Assert.AreEqual(5, item.Arguments.Count);
            AssertIsLiteral(item.Arguments[0].Expression, null);
            AssertIsLiteral(item.Arguments[1].Expression, false);
            AssertIsLiteral(item.Arguments[2].Expression, true);
            AssertIsLiteral(item.Arguments[3].Expression, 123.0);
            AssertIsLiteral(item.Arguments[4].Expression, "foo");
        }

        [Test]
        public void ReadExp_Indexer()
        {
            var item = ParseExpression<IndexerItem>("foo.bar[baz + 1]");
            var left = item.Prefix as IndexerItem;
            Assert.IsNotNull(left);
            AssertIsVariable(left.Prefix, "foo");
            AssertIsLiteral(left.Expression, "bar");
            var right = item.Expression as BinOpItem;
            Assert.IsNotNull(right);
        }

        [Test]
        public void ReadExp_Call()
        {
            string str = "foo(foo, 1 + 2, ref i, ref(j[2]), @j.bar)";
            var item = ParseExpression<FuncCallItem>(str);
            AssertIsVariable(item.Prefix, "foo");
            Assert.IsFalse(item.IsLastArgSingle);
            Assert.IsNull(item.InstanceName);
            Assert.AreEqual(-1, item.Overload);
            Assert.IsFalse(item.Statement);

            Assert.AreEqual(5, item.Arguments.Count);
            AssertIsVariable(item.Arguments[0].Expression, "foo");
            Assert.IsFalse(item.Arguments[0].IsByRef);
            Assert.IsInstanceOf<BinOpItem>(item.Arguments[1].Expression);
            Assert.IsFalse(item.Arguments[1].IsByRef);
            AssertIsVariable(item.Arguments[2].Expression, "i");
            Assert.IsTrue(item.Arguments[2].IsByRef);
            Assert.IsInstanceOf<IndexerItem>(item.Arguments[3].Expression);
            Assert.IsTrue(item.Arguments[3].IsByRef);
            Assert.IsInstanceOf<IndexerItem>(item.Arguments[4].Expression);
            Assert.IsTrue(item.Arguments[4].IsByRef);
        }

        [Test]
        public void ReadExp_CallLastSingle()
        {
            var item = ParseExpression<FuncCallItem>("foo((x))");
            Assert.IsTrue(item.IsLastArgSingle);
            Assert.AreEqual(1, item.Arguments.Count);
            AssertIsVariable(item.Arguments[0].Expression, "x");
        }

        [Test]
        public void ReadExp_CallInstanceMethod()
        {
            var item = ParseExpression<FuncCallItem>("bar:foo()");
            AssertIsVariable(item.Prefix, "bar");
            Assert.AreEqual("foo", item.InstanceName);
        }

        [Test]
        public void ReadExp_CallOverload()
        {
            var item = ParseExpression<FuncCallItem>("foo`123()");
            Assert.AreEqual(123, item.Overload);
            item = ParseExpression<FuncCallItem>("bar:foo`456()");
            Assert.AreEqual(456, item.Overload);
        }

        [Test]
        public void ReadExp_CallWithString()
        {
            var item = ParseExpression<FuncCallItem>("foo 'bar'");
            AssertIsVariable(item.Prefix, "foo");
            Assert.AreEqual(1, item.Arguments.Count);
            AssertIsLiteral(item.Arguments[0].Expression, "bar");
            Assert.IsFalse(item.Arguments[0].IsByRef);
        }

        [Test]
        public void ReadExp_CallWithTable()
        {
            var item = ParseExpression<FuncCallItem>("foo {}");
            AssertIsVariable(item.Prefix, "foo");
            Assert.AreEqual(1, item.Arguments.Count);
            Assert.IsInstanceOf<TableItem>(item.Arguments[0].Expression);
            Assert.IsFalse(item.Arguments[0].IsByRef);
        }

        [Test]
        public void ReadTable_Empty()
        {
            var item = ParseExpression<TableItem>("{}");
            Assert.AreEqual(0, item.Fields.Count);
        }

        [Test]
        public void ReadTable_AcceptsNamedKeys()
        {
            var item = ParseExpression<TableItem>("{x=1,y=2}");
            Assert.AreEqual(2, item.Fields.Count);
            AssertIsLiteral(item.Fields[0].Key, "x");
            AssertIsLiteral(item.Fields[0].Value, 1);
            AssertIsLiteral(item.Fields[1].Key, "y");
            AssertIsLiteral(item.Fields[1].Value, 2);
        }

        [Test]
        public void ReadTable_AcceptsExpressionKeys()
        {
            var item = ParseExpression<TableItem>("{[x+1]=1}");
            Assert.AreEqual(1, item.Fields.Count);
            Assert.IsInstanceOf<BinOpItem>(item.Fields[0].Key);
            AssertIsLiteral(item.Fields[0].Value, 1);
        }

        [Test]
        public void ReadTable_AcceptsJustValues()
        {
            var item = ParseExpression<TableItem>("{10; 20; 30}");
            Assert.AreEqual(3, item.Fields.Count);
            AssertIsLiteral(item.Fields[0].Key, 1);
            AssertIsLiteral(item.Fields[0].Value, 10);
            AssertIsLiteral(item.Fields[1].Key, 2);
            AssertIsLiteral(item.Fields[1].Value, 20);
            AssertIsLiteral(item.Fields[2].Key, 3);
            AssertIsLiteral(item.Fields[2].Value, 30);
        }

        [Test]
        public void ReadTable_AcceptsMixedKeysAndValues()
        {
            var str = "{ [f(1)] = g; 'x', 'y'; x = 1, f(x), [30] = 23; 45 }";
            var item = ParseExpression<TableItem>(str);
            Assert.AreEqual(7, item.Fields.Count);
            Assert.IsInstanceOf<FuncCallItem>(item.Fields[0].Key);
            AssertIsVariable(item.Fields[0].Value, "g");
            AssertIsLiteral(item.Fields[1].Key, 1);
            AssertIsLiteral(item.Fields[1].Value, "x");
            AssertIsLiteral(item.Fields[2].Key, 2);
            AssertIsLiteral(item.Fields[2].Value, "y");
            AssertIsLiteral(item.Fields[3].Key, "x");
            AssertIsLiteral(item.Fields[3].Value, 1);
            AssertIsLiteral(item.Fields[4].Key, 3);
            Assert.IsInstanceOf<FuncCallItem>(item.Fields[4].Value);
            AssertIsLiteral(item.Fields[5].Key, 30);
            AssertIsLiteral(item.Fields[5].Value, 23);
            AssertIsLiteral(item.Fields[6].Key, 4);
            AssertIsLiteral(item.Fields[6].Value, 45);
        }


        [Test]
        public void ReadStatement_ClassOldStyle()
        {
            var str = "class \"Foo\" (Bar, Baz)";
            var item = ParseStatement<ClassDefItem>(str);
            Assert.AreEqual("Foo", item.Name);
            Assert.AreEqual(2, item.Implements.Count);
            Assert.AreEqual("Bar", item.Implements[0]);
            Assert.AreEqual("Baz", item.Implements[1]);
        }

        [Test]
        public void ReadStatement_ClassNewStyle()
        {
            var str = "class Foo : Bar, Baz";
            var item = ParseStatement<ClassDefItem>(str);
            Assert.AreEqual("Foo", item.Name);
            Assert.AreEqual(2, item.Implements.Count);
            Assert.AreEqual("Bar", item.Implements[0]);
            Assert.AreEqual("Baz", item.Implements[1]);
        }

        [Test]
        public void ReadStatement_ForGeneric()
        {
            var str = "for x, y in foo do end";
            var item = ParseStatement<ForGenItem>(str);
            Assert.AreEqual(2, item.Names.Count);
            Assert.AreEqual("x", item.Names[0].Name);
            Assert.AreEqual("y", item.Names[1].Name);
            Assert.AreEqual(1, item.Expressions.Count);
            AssertIsVariable(item.Expressions[0], "foo");
            Assert.AreEqual(0, item.Block.Children.Count);
        }

        [Test]
        public void ReadStatement_ForNumber()
        {
            var str = "for x = i, j, k do end";
            var item = ParseStatement<ForNumItem>(str);
            Assert.AreEqual("x", item.Name.Name);
            AssertIsVariable(item.Start, "i");
            AssertIsVariable(item.Limit, "j");
            AssertIsVariable(item.Step, "k");
            Assert.AreEqual(0, item.Block.Children.Count);
        }

        [Test]
        public void ReadStatement_If()
        {
            var str = "if i then end";
            var item = ParseStatement<IfItem>(str);
            AssertIsVariable(item.Exp, "i");
            Assert.AreEqual(0, item.Block.Children.Count);
            Assert.AreEqual(0, item.Elses.Count);
            Assert.IsNull(item.ElseBlock);
        }

        [Test]
        public void ReadStatement_IfElse()
        {
            var str = "if i then else end";
            var item = ParseStatement<IfItem>(str);
            AssertIsVariable(item.Exp, "i");
            Assert.AreEqual(0, item.Block.Children.Count);
            Assert.AreEqual(0, item.Elses.Count);
            Assert.AreEqual(0, item.ElseBlock.Children.Count);
        }

        [Test]
        public void ReadStatement_IfElseIf()
        {
            var str = "if i then elseif y then x = 1 end";
            var item = ParseStatement<IfItem>(str);
            AssertIsVariable(item.Exp, "i");
            Assert.AreEqual(0, item.Block.Children.Count);
            Assert.IsNull(item.ElseBlock);
            Assert.AreEqual(1, item.Elses.Count);
            AssertIsVariable(item.Elses[0].Expression, "y");
            Assert.AreEqual(1, item.Elses[0].Block.Children.Count);
            Assert.IsInstanceOf<AssignmentItem>(item.Elses[0].Block.Children[0]);
        }

        [Test]
        public void ReadStatement_Repeat()
        {
            var str = "repeat x = 1 until i";
            var item = ParseStatement<RepeatItem>(str);
            AssertIsVariable(item.Expression, "i");
            Assert.AreEqual(1, item.Block.Children.Count);
            Assert.IsInstanceOf<AssignmentItem>(item.Block.Children[0]);
        }

        [Test]
        public void ReadStatement_Label()
        {
            var item = ParseStatement<LabelItem>("::foo::");
            Assert.AreEqual("foo", item.Name);
        }

        [Test]
        public void ReadStatement_Goto()
        {
            var item = ParseStatement<GotoItem>("goto x");
            Assert.AreEqual("x", item.Name);
        }

        [Test]
        public void ReadStatement_Do()
        {
            var item = ParseStatement<BlockItem>("do x = 1 end");
            Assert.IsNull(item.Return);
            Assert.AreEqual(1, item.Children.Count);
            Assert.IsInstanceOf<AssignmentItem>(item.Children[0]);
        }

        [Test]
        public void ReadStatement_While()
        {
            var str = "while i do x = 1 y = 2 end";
            var item = ParseStatement<WhileItem>(str);
            AssertIsVariable(item.Exp, "i");
            Assert.IsNull(item.Block.Return);
            Assert.AreEqual(2, item.Block.Children.Count);
            Assert.IsInstanceOf<AssignmentItem>(item.Block.Children[0]);
            Assert.IsInstanceOf<AssignmentItem>(item.Block.Children[1]);
        }


        [Test]
        public void SyntaxError()
        {
            var tests = new SyntaxErrorCollection()
            {
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
            foreach (var test in tests)
            {
                try
                {
                    ParseBlock(test.Item1);
                    Assert.Fail("Expected to throw error: {0}", test.Item1);
                }
                catch (SyntaxException e)
                {
                    Assert.AreEqual(test.Item2, e.SourceToken.StartPos,
                                    "Error in wrong position, Code: {0}, Message: {1}",
                                    test.Item1, e.Message);
                }
            }
        }
    }
}
