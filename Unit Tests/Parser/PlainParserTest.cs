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

using System.Collections.Generic;
using System.IO;
using System.Text;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;
using NUnit.Framework;

namespace UnitTests.Parser {
  [TestFixture]
  public class PlainParserTest {
    IParseItem _parseBlock(string input) {
      PlainParser target = new PlainParser();
      var encoding = Encoding.UTF8;
      var stream = new MemoryStream(encoding.GetBytes(input));
      return target.Parse(stream, encoding, null);
    }

    IParseItem _parseStatement(string input) {
      var block = _parseBlock(input) as BlockItem;
      Assert.IsNotNull(block);
      Assert.AreEqual(1, block.Children.Length);
      return block.Children[0];
    }

    IParseItem _parseExpression(string input) {
      var stmt = _parseStatement("local x = " + input) as AssignmentItem;
      Assert.IsNotNull(stmt);
      Assert.AreEqual(1, stmt.Expressions.Length);
      return stmt.Expressions[0];
    }

    void _checkSyntaxError(string input, Token token) {
      var err = Assert.Throws<SyntaxException>(() => _parseBlock(input));
      Assert.AreEqual(token, err.SourceToken);
    }

    #region Expressions

    [Test]
    public void GenralParseWithDebug() {
      string input1 = @"
        local a = 12
        t = { [34]= function() print(i) end }
        function Some(a, ...)
            a, b, c = ...
            for i= 12, 23 do
                print(i)
            end
        end";

      Token d(TokenType t, string value, int line, int col) => new Token(t, value, col, line);
      IParseItem expected = new BlockItem(new IParseStatement[] {
          new AssignmentItem(new[] {
              new NameItem("a") { Debug = d(TokenType.Identifier, "a", 2, 15) }
          }, new[] {
              new LiteralItem(12.0) { Debug = d(TokenType.NumberLiteral, "12", 2, 19) },
          }) {
              Debug = d(TokenType.Local, "local", 2, 9),
              IsLastExpressionSingle = false,
              Local = true,
          },
          new AssignmentItem(new[] {
              new NameItem("t") { Debug = d(TokenType.Identifier, "t", 3, 9) },
          }, new[] {
              new TableItem(new[] {
                  new KeyValuePair<IParseExp, IParseExp>(
                      new LiteralItem(34.0) { Debug = d(TokenType.NumberLiteral, "34", 3, 16) },
                      new FuncDefItem(
                          new NameItem[0],
                          new BlockItem(new[] {
                              new FuncCallItem(
                                  new NameItem("print") {
                                      Debug = d(TokenType.Identifier, "print", 3, 32),
                                  },
                                  new[] {
                                      new FuncCallItem.ArgumentInfo(
                                          new NameItem("i") {
                                              Debug = d(TokenType.Identifier, "i", 3, 38),
                                          },
                                          false)
                                  }) {
                                      Debug = d(TokenType.Identifier, "print", 3, 32),
                                      Statement = true,
                                  },
                          }) {
                             Debug = d(TokenType.Identifier, "print", 3, 32),
                             Return = new ReturnItem(),
                      }) {
                        Debug = d(TokenType.Function, "function", 3, 21)
                      }),
              }) { Debug = d(TokenType.BeginTable, "{", 3, 13) },
          }) {
              Debug = d(TokenType.Identifier, "t", 3, 9),
              IsLastExpressionSingle = false,
              Local = false,
          },
          new FuncDefItem(new[] {
              new NameItem("a") { Debug = d(TokenType.Identifier, "a", 4, 23) },
              new NameItem("...") { Debug = d(TokenType.Elipsis, "...", 4, 26) },
          }, new BlockItem(new IParseStatement[] {
              new AssignmentItem(new[] {
                  new NameItem("a") { Debug = d(TokenType.Identifier, "a", 5, 13) },
                  new NameItem("b") { Debug = d(TokenType.Identifier, "b", 5, 16) },
                  new NameItem("c") { Debug = d(TokenType.Identifier, "c", 5, 19) },
              }, new[] {
                  new NameItem("...") { Debug = d(TokenType.Elipsis, "...", 5, 23) },
              }) { Debug = d(TokenType.Identifier, "a", 5, 13) },
              new ForNumItem(
                  new NameItem("i") { Debug = d(TokenType.Identifier, "i", 6, 17) },
                  new LiteralItem(12.0) { Debug = d(TokenType.NumberLiteral, "12", 6, 20) },
                  new LiteralItem(23.0) { Debug = d(TokenType.NumberLiteral, "23", 6, 24) },
                  null,
                  new BlockItem(new[] {
                      new FuncCallItem(
                          new NameItem("print") { Debug = d(TokenType.Identifier, "print", 7, 17) },
                          new[] {
                              new FuncCallItem.ArgumentInfo(
                                  new NameItem("i") {
                                      Debug = d(TokenType.Identifier, "i", 7, 23)
                                  }, false)
                          }) {
                          Debug = d(TokenType.Identifier, "print", 7, 17),
                          Statement = true,
                      }
                  }) {
                      Debug = d(TokenType.Identifier, "print", 7, 17)
                  }) { Debug = d(TokenType.For, "for", 6, 13) },
          }) {
              Debug = d(TokenType.Identifier, "a", 5, 13),
              Return = new ReturnItem(),
          }) {
              Debug = d(TokenType.Function, "function", 4, 9),
              Local = false,
              Prefix = new NameItem("Some") { Debug = d(TokenType.Identifier, "Some", 4, 18) },
          }
      }) {
        Return = new ReturnItem(),
        Debug = d(TokenType.Local, "local", 2, 9),
      };

      ParseItemEquals.CheckEquals(expected, _parseBlock(input1), checkDebug: true);
    }

    [Test]
    public void UnaryExpression_Nested() {
      ParseItemEquals.CheckEquals(
          new UnOpItem(
              new UnOpItem(
                  new UnOpItem(new NameItem("foo"), UnaryOperationType.Not),
                  UnaryOperationType.Length),
              UnaryOperationType.Minus),
          _parseExpression("-#not foo"));
    }

    [Test]
    public void UnaryExpression_WithAdd() {
      // This should be parsed as (-foo)+bar
      ParseItemEquals.CheckEquals(
          new BinOpItem(
              new UnOpItem(new NameItem("foo"), UnaryOperationType.Minus),
              BinaryOperationType.Add,
              new NameItem("bar")),
          _parseExpression("-foo+bar"));

      // This should be parsed as foo + (not bar)
      ParseItemEquals.CheckEquals(
          new BinOpItem(
              new NameItem("foo"),
              BinaryOperationType.Add,
              new UnOpItem(new NameItem("bar"), UnaryOperationType.Not)),
          _parseExpression("foo + not bar"));
    }

    [Test]
    public void UnaryExpression_WithPower() {
      // This should be parsed as -(foo^bar)
      ParseItemEquals.CheckEquals(
          new UnOpItem(
              new BinOpItem(new NameItem("foo"), BinaryOperationType.Power, new NameItem("bar")),
              UnaryOperationType.Minus),
          _parseExpression("-foo^bar"));
    }

    [Test]
    public void UnaryExpression_KeepsPrecedence() {
      // This should be parsed as (foo * (-bar)) + baz
      ParseItemEquals.CheckEquals(
          new BinOpItem(
              new BinOpItem(
                  new NameItem("foo"),
                  BinaryOperationType.Multiply,
                  new UnOpItem(new NameItem("bar"), UnaryOperationType.Minus)),
              BinaryOperationType.Add,
              new NameItem("baz")),
          _parseExpression("foo*-bar+baz"));
      // This should be parsed as foo * (-(bar ^ baz))
      ParseItemEquals.CheckEquals(
          new BinOpItem(
              new NameItem("foo"),
              BinaryOperationType.Multiply,
              new UnOpItem(
                  new BinOpItem(
                      new NameItem("bar"), BinaryOperationType.Power, new NameItem("baz")),
              UnaryOperationType.Minus)),
          _parseExpression("foo*-bar^baz"));
    }

    [Test]
    public void BinaryExpression_HandlesPrecedence() {
      // This should be parsed as foo+(bar*baz)
      ParseItemEquals.CheckEquals(
          new BinOpItem(
              new NameItem("foo"),
              BinaryOperationType.Add,
              new BinOpItem(
                  new NameItem("bar"), BinaryOperationType.Multiply, new NameItem("baz"))),
          _parseExpression("foo+bar*baz"));
      // This should be parsed as (foo*bar)+baz
      ParseItemEquals.CheckEquals(
          new BinOpItem(
              new BinOpItem(new NameItem("foo"), BinaryOperationType.Multiply, new NameItem("bar")),
              BinaryOperationType.Add,
              new NameItem("baz")),
          _parseExpression("foo*bar+baz"));
    }

    [Test]
    public void BinaryExpression_HandlesRightAssociative() {
      // This should be parsed as foo^(bar^baz)
      ParseItemEquals.CheckEquals(
          new BinOpItem(
              new NameItem("foo"),
              BinaryOperationType.Power,
              new BinOpItem(new NameItem("bar"), BinaryOperationType.Power, new NameItem("baz"))),
          _parseExpression("foo^bar^baz"));
    }

    [Test]
    public void BinaryExpression_HandlesRightAssociativeWithAdd() {
      // Concat is 8 and add is 9; since we use +1 to handle right-associative, this validates we
      // don't mishandle that.
      // This should be parsed as foo..(bar+baz)
      ParseItemEquals.CheckEquals(
          new BinOpItem(
              new NameItem("foo"),
              BinaryOperationType.Concat,
              new BinOpItem(new NameItem("bar"), BinaryOperationType.Add, new NameItem("baz"))),
          _parseExpression("foo..bar+baz"));
      // This should be parsed as (foo+bar)..baz
      ParseItemEquals.CheckEquals(
          new BinOpItem(
              new BinOpItem(new NameItem("foo"), BinaryOperationType.Add, new NameItem("bar")),
              BinaryOperationType.Concat,
              new NameItem("baz")),
          _parseExpression("foo+bar..baz"));
      // This should be parsed as (foo+bar)..(baz+cat)
      ParseItemEquals.CheckEquals(
          new BinOpItem(
              new BinOpItem(new NameItem("foo"), BinaryOperationType.Add, new NameItem("bar")),
              BinaryOperationType.Concat,
              new BinOpItem(new NameItem("baz"), BinaryOperationType.Add, new NameItem("cat"))),
          _parseExpression("foo+bar..baz+cat"));
      // This should be parsed as foo..((bar+baz)..cat)
      ParseItemEquals.CheckEquals(
          new BinOpItem(
              new NameItem("foo"),
              BinaryOperationType.Concat,
              new BinOpItem(
                  new BinOpItem(new NameItem("bar"), BinaryOperationType.Add, new NameItem("baz")),
                  BinaryOperationType.Concat,
                  new NameItem("cat"))),
          _parseExpression("foo..bar+baz..cat"));
    }

    [Test]
    public void Literals() {
      ParseItemEquals.CheckEquals(new LiteralItem(null), _parseExpression("nil"));
      ParseItemEquals.CheckEquals(new LiteralItem(false), _parseExpression("false"));
      ParseItemEquals.CheckEquals(new LiteralItem(true), _parseExpression("true"));
      ParseItemEquals.CheckEquals(new LiteralItem(123.0), _parseExpression("123"));
      ParseItemEquals.CheckEquals(new LiteralItem("foo"), _parseExpression("'foo'"));
    }

    [Test]
    public void Properties() {
      ParseItemEquals.CheckEquals(
          new IndexerItem(new NameItem("foo"), new LiteralItem("bar")),
          _parseExpression("foo.bar"));
    }

    [Test]
    public void Properties_Multiple() {
      ParseItemEquals.CheckEquals(
          new IndexerItem(
              new IndexerItem(
                  new IndexerItem(new NameItem("foo"), new LiteralItem("bar")),
                  new LiteralItem("baz")),
              new LiteralItem("cat")),
          _parseExpression("foo.bar.baz.cat"));
    }

    [Test]
    public void Properties_Errors() {
      _checkSyntaxError("local x = a. .a", new Token(TokenType.Indexer, ".", 14, 1));
      _checkSyntaxError("local x = a.2", new Token(TokenType.NumberLiteral, "2", 13, 1));
    }

    [Test]
    public void Indexer() {
      ParseItemEquals.CheckEquals(
          new IndexerItem(
              new NameItem("foo"),
              new BinOpItem(new NameItem("a"), BinaryOperationType.Add, new NameItem("b"))),
          _parseExpression("foo[a + b]"));
    }

    [Test]
    public void Indexer_Multiple() {
      ParseItemEquals.CheckEquals(
          new IndexerItem(
              new IndexerItem(
                  new NameItem("foo"),
                  new BinOpItem(new NameItem("a"), BinaryOperationType.Add, new NameItem("b"))),
              new NameItem("c")),
          _parseExpression("foo[a + b][c]"));
    }

    [Test]
    public void Indexer_Errors() {
      _checkSyntaxError("local x = a[", new Token(TokenType.None, "", 13, 1));
      _checkSyntaxError("local x = a[]", new Token(TokenType.EndBracket, "]", 13, 1));
      _checkSyntaxError("local x = a[a,b]", new Token(TokenType.Comma, ",", 14, 1));
    }

    [Test]
    public void Call() {
      ParseItemEquals.CheckEquals(
          new FuncCallItem(
              new NameItem("foo"),
              new[] { new FuncCallItem.ArgumentInfo(new LiteralItem(1.0), false) }),
          _parseExpression("foo(1)"));
    }

    [Test]
    public void Call_MultipleArgs() {
      ParseItemEquals.CheckEquals(
          new FuncCallItem(
              new NameItem("foo"),
              new[] {
                  new FuncCallItem.ArgumentInfo(new NameItem("a"), false),
                  new FuncCallItem.ArgumentInfo(new NameItem("b"), false),
                  new FuncCallItem.ArgumentInfo(new NameItem("c"), false),
                  new FuncCallItem.ArgumentInfo(new NameItem("d"), false),
              }),
          _parseExpression("foo(a, b, c, d)"));
    }

    [Test]
    public void Call_ByRefArgs() {
      ParseItemEquals.CheckEquals(
          new FuncCallItem(
              new NameItem("foo"),
              new[] {
                  new FuncCallItem.ArgumentInfo(new NameItem("a"), true),
                  new FuncCallItem.ArgumentInfo(new NameItem("b"), true),
                  new FuncCallItem.ArgumentInfo(new NameItem("c"), true),
                  new FuncCallItem.ArgumentInfo(
                      new IndexerItem(new NameItem("d"), new LiteralItem("bar")), true),
              }),
          _parseExpression("foo(ref a, ref(b), @c, @d.bar)"));
    }

    [Test]
    public void Call_LastArgSingle() {
      ParseItemEquals.CheckEquals(
          new FuncCallItem(
              new NameItem("foo"),
              new[] { new FuncCallItem.ArgumentInfo(new NameItem("a"), false) }) { IsLastArgSingle = true },
          _parseExpression("foo((a))"));
      ParseItemEquals.CheckEquals(
          new FuncCallItem(
              new NameItem("foo"),
              new[] {
                  new FuncCallItem.ArgumentInfo(new NameItem("a"), false),
                  new FuncCallItem.ArgumentInfo(new NameItem("b"), false),
                  new FuncCallItem.ArgumentInfo(new NameItem("c"), false),
              }) { IsLastArgSingle = true },
          _parseExpression("foo(a, b, (c))"));
    }

    [Test]
    public void Call_NotLastArgSingle() {
      ParseItemEquals.CheckEquals(
          new FuncCallItem(
              new NameItem("foo"),
              new[] {
                  new FuncCallItem.ArgumentInfo(
                      new BinOpItem(new NameItem("a"), BinaryOperationType.Add,
                                    new LiteralItem(1.0)),
                      false),
              }) { IsLastArgSingle = false },
          _parseExpression("foo((a)+1)"));
      ParseItemEquals.CheckEquals(
          new FuncCallItem(
              new NameItem("foo"),
              new[] {
                  new FuncCallItem.ArgumentInfo(new NameItem("a"), false),
                  new FuncCallItem.ArgumentInfo(new NameItem("b"), false),
                  new FuncCallItem.ArgumentInfo(new NameItem("c"), false),
              }) { IsLastArgSingle = false },
          _parseExpression("foo(a, (b), c)"));
    }

    [Test]
    public void Call_InstanceMethod() {
      ParseItemEquals.CheckEquals(
          new FuncCallItem(new NameItem("foo")) { InstanceName = "bar" },
          _parseExpression("foo:bar()"));
    }

    [Test]
    public void Call_Statement() {
      ParseItemEquals.CheckEquals(
          new FuncCallItem(new NameItem("foo")) { Statement = true },
          _parseStatement("foo()"));
    }

    [Test]
    public void Call_WithString() {
      ParseItemEquals.CheckEquals(
          new FuncCallItem(
              new NameItem("foo"),
              new[] { new FuncCallItem.ArgumentInfo(new LiteralItem("bar"), false) }),
          _parseExpression("foo 'bar'"));
    }

    [Test]
    public void Call_WithTable() {
      ParseItemEquals.CheckEquals(
          new FuncCallItem(
              new NameItem("foo"), new[] { new FuncCallItem.ArgumentInfo(new TableItem(), false) }),
          _parseExpression("foo {}"));
    }

    [Test]
    public void Call_Errors() {
      _checkSyntaxError("a(()", new Token(TokenType.EndParen, ")", 4, 1));
      _checkSyntaxError("a())", new Token(TokenType.EndParen, ")", 4, 1));
      _checkSyntaxError("a(,)", new Token(TokenType.Comma, ",", 3, 1));
      _checkSyntaxError("a(a,,b)", new Token(TokenType.Comma, ",", 5, 1));
      _checkSyntaxError("a(ref(a,b))", new Token(TokenType.Comma, ",", 8, 1));
      _checkSyntaxError("a(a b)", new Token(TokenType.Identifier, "b", 5, 1));
      _checkSyntaxError("a:1(a b)", new Token(TokenType.NumberLiteral, "1", 3, 1));
    }

    [Test]
    public void Table_Empty() {
      ParseItemEquals.CheckEquals(new TableItem(), _parseExpression("{}"));
    }

    [Test]
    public void Table_NamedKeys() {
      ParseItemEquals.CheckEquals(
          new TableItem(new[] {
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem("x"), new LiteralItem(1.0)),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem("y"), new LiteralItem(2.0)),
          }),
          _parseExpression("{x=1, y=2}"));
    }

    [Test]
    public void Table_ExpressionKeys() {
      ParseItemEquals.CheckEquals(
          new TableItem(new[] {
              new KeyValuePair<IParseExp, IParseExp>(
                  new BinOpItem(new NameItem("x"), BinaryOperationType.Add,
                                new LiteralItem(1.0)),
                  new LiteralItem(1.0)),
              new KeyValuePair<IParseExp, IParseExp>(new NameItem("y"), new LiteralItem(2.0)),
          }),
          _parseExpression("{[x+1]=1, [y]=2}"));
    }

    [Test]
    public void Table_PlainValues() {
      ParseItemEquals.CheckEquals(
          new TableItem(new[] {
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(1.0), new LiteralItem(10.0)),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(2.0), new LiteralItem(20.0)),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(3.0), new LiteralItem(30.0)),
          }),
          _parseExpression("{10, 20, 30}"));
    }

    [Test]
    public void Table_MixedSeparators() {
      ParseItemEquals.CheckEquals(
          new TableItem(new[] {
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem("a"), new LiteralItem(1.0)),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem("b"), new LiteralItem(2.0)),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem("c"), new LiteralItem(3.0)),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem("d"), new LiteralItem(4.0)),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem("e"), new LiteralItem(5.0)),
          }),
          _parseExpression("{a=1, b=2; c=3; d=4, e=5}"));
    }

    [Test]
    public void Table_MixedValuesAndKeys() {
      ParseItemEquals.CheckEquals(
          new TableItem(new[] {
              new KeyValuePair<IParseExp, IParseExp>(
                  new FuncCallItem(new NameItem("f")), new NameItem("g")),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(1.0), new LiteralItem("x")),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(2.0), new LiteralItem("y")),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem("x"), new LiteralItem(1.0)),
              new KeyValuePair<IParseExp, IParseExp>(
                  new LiteralItem(3.0), new FuncCallItem(new NameItem("g"))),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(30.0), new LiteralItem(23.0)),
              new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(4.0), new LiteralItem(45.0)),
          }),
          _parseExpression("{[f()]=g, 'x', 'y', x=1, g(), [30]=23, 45}"));
    }

    [Test]
    public void Table_Errors() {
      _checkSyntaxError("{x+1=1}", new Token(TokenType.BeginTable, "{", 1, 1));
      _checkSyntaxError("{[x=1}", new Token(TokenType.Assign, "=", 4, 1));
      _checkSyntaxError("{[x], 1}", new Token(TokenType.Comma, ",", 5, 1));
      _checkSyntaxError("{a,,b}", new Token(TokenType.Comma, ",", 4, 1));
      _checkSyntaxError("{a a}", new Token(TokenType.Identifier, "a", 4, 1));
      _checkSyntaxError("{a", new Token(TokenType.None, "", 3, 1));
    }

    #endregion

    #region Statements

    [Test]
    public void Assignment() {
      ParseItemEquals.CheckEquals(
          new AssignmentItem(new[] { new NameItem("x") }, new[] { new LiteralItem(1.0) }),
          _parseStatement("x = 1"));
    }

    [Test]
    public void Assignment_Multiples() {
      ParseItemEquals.CheckEquals(
          new AssignmentItem(new[] {
              new NameItem("x"),
              new NameItem("y"),
              new NameItem("z"),
          }, new[] {
              new LiteralItem(1.0),
              new LiteralItem(2.0),
              new LiteralItem(3.0),
          }),
          _parseStatement("x,y,z = 1,2,3"));
    }

    [Test]
    public void Assignment_Local() {
      ParseItemEquals.CheckEquals(
          new AssignmentItem(new[] { new NameItem("x") },
                             new[] { new LiteralItem(1.0) }) { Local = true },
          _parseStatement("local x = 1"));
    }

    [Test]
    public void Assignment_LastArgSingle() {
      ParseItemEquals.CheckEquals(
          new AssignmentItem(new[] { new NameItem("x") },
                             new[] { new LiteralItem(1.0) }) { IsLastExpressionSingle = true },
          _parseStatement("x = (1)"));
    }

    [Test]
    public void Assignment_NotLastArgSingle() {
      ParseItemEquals.CheckEquals(
          new AssignmentItem(new[] { new NameItem("x") },
                             new[] { new LiteralItem(1.0), new LiteralItem(2.0) }),
          _parseStatement("x = (1), 2"));
    }

    [Test]
    public void Assignment_Errors() {
      _checkSyntaxError("local x, 1 = 2", new Token(TokenType.NumberLiteral, "1", 10, 1));
      _checkSyntaxError("local x, y[1] = 2", new Token(TokenType.Identifier, "y", 10, 1));
      _checkSyntaxError("x, 8 = 2", new Token(TokenType.NumberLiteral, "8", 4, 1));
      _checkSyntaxError("x, = 2", new Token(TokenType.Assign, "=", 4, 1));
      _checkSyntaxError("x, y z = 2", new Token(TokenType.Identifier, "z", 6, 1));
    }

    [Test]
    public void Class_OldStyle() {
      ParseItemEquals.CheckEquals(
          new ClassDefItem("Foo", new[] { "Bar", "Baz" }),
          _parseStatement("class \"Foo\" (Bar, Baz)"));
    }

    [Test]
    public void Class_NewStyle() {
      ParseItemEquals.CheckEquals(
          new ClassDefItem("Foo", new[] { "Bar", "Baz" }),
          _parseStatement("class Foo : Bar, Baz"));
    }

    [Test]
    public void Class_NewStyleWithIndexer() {
      ParseItemEquals.CheckEquals(
          new ClassDefItem("Foo", new[] { "Bar.Baz.Cat" }),
          _parseStatement("class Foo : Bar.Baz.Cat"));
    }

    [Test]
    public void Class_Errors() {
      _checkSyntaxError("class", new Token(TokenType.None, "", 6, 1));
      _checkSyntaxError("class()", new Token(TokenType.BeginParen, "(", 6, 1));
      _checkSyntaxError("class Foo :", new Token(TokenType.None, "", 12, 1));
      _checkSyntaxError("class Foo : Foo.", new Token(TokenType.None, "", 17, 1));
      _checkSyntaxError("class Foo : Foo.123", new Token(TokenType.NumberLiteral, "123", 17, 1));
      _checkSyntaxError("class 123", new Token(TokenType.NumberLiteral, "123", 7, 1));
      _checkSyntaxError("class \"Foo\" (\"a\")", new Token(TokenType.StringLiteral, "a", 14, 1));
      _checkSyntaxError("class \"Foo\" (A,)", new Token(TokenType.EndParen, ")", 16, 1));
      _checkSyntaxError("class \"Foo\" (A, B C)", new Token(TokenType.Identifier, "C", 19, 1));
    }

    [Test]
    public void Return_Empty() {
      ParseItemEquals.CheckEquals(
          new BlockItem() { Return = new ReturnItem() },
          _parseBlock("return"));
    }

    [Test]
    public void Return_OneValue() {
      ParseItemEquals.CheckEquals(
          new BlockItem() {
            Return = new ReturnItem(new[] {
                  new BinOpItem(new NameItem("a"), BinaryOperationType.Add, new NameItem("b")),
              })
          },
          _parseBlock("return a + b"));
    }

    [Test]
    public void Return_MultipleValues() {
      ParseItemEquals.CheckEquals(
          new BlockItem() {
            Return = new ReturnItem(
                  new[] { new NameItem("a"), new NameItem("b"), new NameItem("c") })
          },
          _parseBlock("return a, b, c"));
    }

    [Test]
    public void Return_LastExpressionSingle() {
      ParseItemEquals.CheckEquals(
          new BlockItem() {
            Return = new ReturnItem(
                  new[] { new NameItem("a"), new NameItem("b"), new NameItem("c") }) {
              IsLastExpressionSingle = true
            }
          },
          _parseBlock("return a, b, (c)"));
    }

    [Test]
    public void Return_Errors() {
      _checkSyntaxError("return 1,", new Token(TokenType.None, "", 10, 1));
      // Cannot have code after a return
      _checkSyntaxError("return 1 return 2", new Token(TokenType.Return, "return", 10, 1));
      _checkSyntaxError("return 1 x = 0", new Token(TokenType.Identifier, "x", 10, 1));
      _checkSyntaxError("if false then return 1 x = 0 end",
                        new Token(TokenType.Identifier, "x", 24, 1));
    }

    [Test]
    public void NumericFor() {
      ParseItemEquals.CheckEquals(
          new ForNumItem(new NameItem("x"), new LiteralItem(1.0), new LiteralItem(2.0),
                         new LiteralItem(3.0), new BlockItem()),
          _parseStatement("for x = 1, 2, 3 do end"));
    }

    [Test]
    public void NumericFor_NoStep() {
      ParseItemEquals.CheckEquals(
          new ForNumItem(new NameItem("x"), new LiteralItem(1.0), new LiteralItem(2.0), null,
                         new BlockItem()),
          _parseStatement("for x = 1, 2 do end"));
    }

    [Test]
    public void NumericFor_Errors() {
      _checkSyntaxError("for 1, 2 do end", new Token(TokenType.NumberLiteral, "1", 5, 1));
      _checkSyntaxError("for x 1, 2 do end", new Token(TokenType.NumberLiteral, "1", 7, 1));
      _checkSyntaxError("for x = 1 do end", new Token(TokenType.Do, "do", 11, 1));
      _checkSyntaxError("for x = 1,, 2 do end", new Token(TokenType.Comma, ",", 11, 1));
      _checkSyntaxError("for x = 1, 2 end", new Token(TokenType.End, "end", 14, 1));
      _checkSyntaxError("for x = 1, 2 do", new Token(TokenType.None, "", 16, 1));
      _checkSyntaxError("for x = 1, 2 do x = 0 elseif",
                        new Token(TokenType.ElseIf, "elseif", 23, 1));
    }

    [Test]
    public void GenericFor() {
      ParseItemEquals.CheckEquals(
          new ForGenItem(new[] { new NameItem("x") }, new[] { new NameItem("foo") },
                         new BlockItem()),
          _parseStatement("for x in foo do end"));
    }

    [Test]
    public void GenericFor_Multiples() {
      ParseItemEquals.CheckEquals(
          new ForGenItem(
              new[] { new NameItem("x"), new NameItem("y"), new NameItem("z") },
              new IParseExp[] { new NameItem("foo"), new FuncCallItem(new NameItem("run")) },
              new BlockItem()),
          _parseStatement("for x, y, z in foo, run() do end"));
    }

    [Test]
    public void GenericFor_Errors() {
      _checkSyntaxError("for x, in y do end", new Token(TokenType.In, "in", 8, 1));
      _checkSyntaxError("for x in y, do end", new Token(TokenType.Do, "do", 13, 1));
      _checkSyntaxError("for x, 1 in y do end", new Token(TokenType.NumberLiteral, "1", 8, 1));
      _checkSyntaxError("for x in y end", new Token(TokenType.End, "end", 12, 1));
    }

    [Test]
    public void If() {
      ParseItemEquals.CheckEquals(
          new IfItem(new NameItem("i"), new BlockItem()),
          _parseStatement("if i then end"));
    }

    [Test]
    public void If_Else() {
      ParseItemEquals.CheckEquals(
          new IfItem(new NameItem("i"),
          new BlockItem(new[] {
              new AssignmentItem(new[] { new NameItem("x") }, new[] { new LiteralItem(0.0) }),
          }),
          new IfItem.ElseInfo[0],
          new BlockItem(new[] {
              new AssignmentItem(new[] { new NameItem("y") }, new[] { new LiteralItem(1.0) }),
          })),
          _parseStatement("if i then x = 0 else y = 1 end"));
    }

    [Test]
    public void If_ElseIf() {
      ParseItemEquals.CheckEquals(
          new IfItem(new NameItem("i"),
          new BlockItem(new[] {
              new AssignmentItem(new[] { new NameItem("x") }, new[] { new LiteralItem(0.0) }),
          }),
          new[] {
              new IfItem.ElseInfo(new NameItem("y"), new BlockItem(new[] {
                  new AssignmentItem(new[] { new NameItem("z") }, new[] { new LiteralItem(1.0) }),
              }))
          }),
          _parseStatement("if i then x = 0 elseif y then z = 1 end"));
    }

    [Test]
    public void If_Errors() {
      _checkSyntaxError("if x end", new Token(TokenType.End, "end", 6, 1));
      _checkSyntaxError("if x else end", new Token(TokenType.Else, "else", 6, 1));
      _checkSyntaxError("if x then elseif end", new Token(TokenType.End, "end", 18, 1));
      _checkSyntaxError("if x then elseif y end", new Token(TokenType.End, "end", 20, 1));
      _checkSyntaxError("if x then", new Token(TokenType.None, "", 10, 1));
    }

    [Test]
    public void Repeat() {
      ParseItemEquals.CheckEquals(
          new RepeatItem(
              new NameItem("i"),
              new BlockItem(new[] {
                  new AssignmentItem(new[] { new NameItem("x") }, new[] { new LiteralItem(1.0) }),
              })),
          _parseStatement("repeat x = 1 until i"));
    }

    [Test]
    public void Repeat_Errors() {
      _checkSyntaxError("repeat x = 1 until", new Token(TokenType.None, "", 19, 1));
      _checkSyntaxError("repeat x = 1", new Token(TokenType.None, "", 13, 1));
      _checkSyntaxError("repeat x = 1 end", new Token(TokenType.End, "end", 14, 1));
    }

    [Test]
    public void Label() {
      ParseItemEquals.CheckEquals(
          new LabelItem("foo"),
          _parseStatement("::foo::"));
    }

    [Test]
    public void Label_Errors() {
      _checkSyntaxError("::foo", new Token(TokenType.None, "", 6, 1));
      _checkSyntaxError("::foo x = 1", new Token(TokenType.Identifier, "x", 7, 1));
      _checkSyntaxError("::foo: x = 1", new Token(TokenType.Colon, ":", 6, 1));
      _checkSyntaxError("::1::", new Token(TokenType.NumberLiteral, "1", 3, 1));
      _checkSyntaxError(": :foo::", new Token(TokenType.Colon, ":", 1, 1));
    }

    [Test]
    public void Break() {
      ParseItemEquals.CheckEquals(
          new RepeatItem(
              new NameItem("i"),
              new BlockItem(new[] { new GotoItem("<break>") })),
          _parseStatement("repeat break until i"));
    }

    [Test]
    public void Goto() {
      ParseItemEquals.CheckEquals(
          new GotoItem("foo"),
          _parseStatement("goto foo"));
    }

    [Test]
    public void Goto_Errors() {
      _checkSyntaxError("goto", new Token(TokenType.None, "", 5, 1));
      _checkSyntaxError("goto 1", new Token(TokenType.NumberLiteral, "1", 6, 1));
    }

    [Test]
    public void Do() {
      ParseItemEquals.CheckEquals(
          new BlockItem(new[] {
              new AssignmentItem(new[] { new NameItem("x") }, new[] { new LiteralItem(1.0) }),
          }),
          _parseStatement("do x = 1 end"));
    }

    [Test]
    public void While() {
      ParseItemEquals.CheckEquals(
          new WhileItem(
              new NameItem("i"),
              new BlockItem(new[] {
                  new AssignmentItem(new[] { new NameItem("x") }, new[] { new LiteralItem(1.0) }),
              })),
          _parseStatement("while i do x = 1 end"));
    }

    [Test]
    public void While_Errors() {
      _checkSyntaxError("while 1", new Token(TokenType.None, "", 8, 1));
      _checkSyntaxError("while do end", new Token(TokenType.Do, "do", 7, 1));
      _checkSyntaxError("while 1 x = 1 end", new Token(TokenType.Identifier, "x", 9, 1));
    }

    #endregion

    #region Function definitions

    [Test]
    public void FuncDef() {
      ParseItemEquals.CheckEquals(
          new FuncDefItem(new NameItem[0], new BlockItem() { Return = new ReturnItem() }) {
            Prefix = new NameItem("foo"),
          },
          _parseStatement("function foo() end"));
    }

    [Test]
    public void FuncDef_Args() {
      ParseItemEquals.CheckEquals(
          new FuncDefItem(new[] { new NameItem("a"), new NameItem("b"), new NameItem("...") },
                          new BlockItem() { Return = new ReturnItem() }) {
            Prefix = new NameItem("foo"),
          },
          _parseStatement("function foo(a, b, ...) end"));
    }

    [Test]
    public void FuncDef_InstanceName() {
      ParseItemEquals.CheckEquals(
          new FuncDefItem(new[] { new NameItem("a"), new NameItem("b"), new NameItem("...") },
                          new BlockItem() { Return = new ReturnItem() }) {
            Prefix = new NameItem("foo"),
            InstanceName = "bar",
          },
          _parseStatement("function foo:bar(a, b, ...) end"));
    }

    [Test]
    public void FuncDef_Local() {
      ParseItemEquals.CheckEquals(
          new FuncDefItem(new NameItem[0], new BlockItem() { Return = new ReturnItem() }) {
            Prefix = new NameItem("foo"),
            Local = true,
          },
          _parseStatement("local function foo() end"));
    }

    [Test]
    public void FuncDef_Indexer() {
      ParseItemEquals.CheckEquals(
          new FuncDefItem(new NameItem[0], new BlockItem() { Return = new ReturnItem() }) {
            Prefix = new IndexerItem(new IndexerItem(new NameItem("a"), new LiteralItem("b")),
                                     new LiteralItem("c")),
          },
          _parseStatement("function a.b.c() end"));
    }

    [Test]
    public void FuncDef_Errors() {
      _checkSyntaxError("function() end", new Token(TokenType.Function, "function", 1, 1));
      _checkSyntaxError("x = function a(a) end", new Token(TokenType.Function, "function", 5, 1));

      _checkSyntaxError("function f f() end", new Token(TokenType.Identifier, "f", 12, 1));
      _checkSyntaxError("function a:1() end", new Token(TokenType.NumberLiteral, "1", 12, 1));
      _checkSyntaxError("function a.1() end", new Token(TokenType.NumberLiteral, "1", 12, 1));
      _checkSyntaxError("function a.() end", new Token(TokenType.BeginParen, "(", 12, 1));
      _checkSyntaxError("function a(..., a) end", new Token(TokenType.Comma, ",", 15, 1));
      _checkSyntaxError("function a(a,) end", new Token(TokenType.EndParen, ")", 14, 1));
    }

    #endregion
  }
}
