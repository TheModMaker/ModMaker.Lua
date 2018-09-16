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
using System.Globalization;
using ModMaker.Lua.Parser.Items;
using NUnit.Framework;

namespace UnitTests.Parser
{
    /// <summary>
    /// This is a test class for PlainParserTest and is intended
    /// to contain all PlainParserTest Unit Tests
    ///</summary>
    [TestFixture]
    public class PlainParserTest
    {
        void ValidateDebug(Token debug, string prefix, string value, long startLine, long startPos)
        {
            Assert.AreEqual(value, debug.Value, prefix + ".Debug.Value");
            Assert.AreEqual(startLine, debug.StartLine, prefix + ".Debug.StartLine");
            Assert.AreEqual(startPos, debug.StartPos, prefix + ".Debug.StartPos");
        }

        /// <summary>
        /// A test for PlainParser.Parse with valid input
        /// testing for valid DebugInfo also.
        ///</summary>
        [Test]
        public void GenralParse()
        {
            PlainParser target = new PlainParser();
            TextElementEnumerator input1 = StringInfo.GetTextElementEnumerator(
@"local a = 12
t = { [34]= function() print(i) end }
function Some(a, ...)
    a, b, c = ...
    for i= 12, 23 do
        print(i)
    end
end"
                );
            IParseItem actual;
            actual = target.Parse(new Tokenizer(input1, null), null, null);

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
    }
}
