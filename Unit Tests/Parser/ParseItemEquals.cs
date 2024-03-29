// Copyright 2021 Jacob Trimble
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;
using NUnit.Framework;

namespace UnitTests.Parser {
  /// <summary>
  /// A special parse item that will be ignored when comparing children.  This can be used as a
  /// replacement for any parse item type.
  /// </summary>
  sealed class IgnoreItem : IParseVariable, IParseStatement {
    /// <summary>
    /// A special instance that is used to indicate a block should be ignored.  This is used in a
    /// by-reference comparison.
    /// </summary>
    public static readonly BlockItem IgnoreBlock = new BlockItem();

    public DebugInfo Debug { get; set; }

    public IParseItem Accept(IParseItemVisitor visitor) {
      throw new NotImplementedException();
    }
  }

  static class ParseItemEquals {
    public static void CheckEquals(object? expected, object? actual, bool checkDebug = false) {
      _checkEquals(expected, actual, checkDebug, "$");
    }

    static void _checkEquals(object? expected, object? actual, bool checkDebug, string path) {
      // Skip these properties since they should be different and don't affect the AST itself.
      ISet<string> ignoredProperties = new HashSet<string>() {
          "Break", "Target", "FunctionInformation",
      };
      // Just look at the properties of the given types in addition to IParseItem subclasses.
      ISet<string> specialTypes = new HashSet<string>() {
          "ArgumentInfo", "ElseInfo", "KeyValuePair`2",
      };

      if (expected == null) {
        Assert.IsNull(actual, path);
        return;
      }

      Assert.IsNotNull(actual, path);
      Type type = expected.GetType();
      if (type == typeof(IgnoreItem) || ReferenceEquals(expected, IgnoreItem.IgnoreBlock))
        return;
      Assert.IsAssignableFrom(type, actual, path);

      if (type.GetInterfaces().Contains(typeof(IParseItem)) || specialTypes.Contains(type.Name)) {
        // Iterate through all the properties of the type and assert they have equal values.
        foreach (var prop in type.GetProperties()) {
          if (!ignoredProperties.Contains(prop.Name)) {
            _checkEquals(prop.GetValue(expected), prop.GetValue(actual), checkDebug,
                         $"{path}.{prop.Name}");
          }
        }
      } else if (type == typeof(Token) || type == typeof(DebugInfo)) {
        if (checkDebug) {
          foreach (var field in type.GetFields()) {
            _checkEquals(field.GetValue(expected), field.GetValue(actual), checkDebug,
                         $"{path}.{field.Name}");
          }
        }
      } else if (type.GetInterfaces().Contains(typeof(IEnumerable)) && type != typeof(string)) {
        // If this is a collection, iterate over each list.
        var expectedIter = ((IEnumerable)expected).GetEnumerator();
        var actualIter = ((IEnumerable)actual!).GetEnumerator();
        int i = 0;
        while (true) {
          bool expectedMove = expectedIter.MoveNext();
          bool actualMove = actualIter.MoveNext();
          Assert.AreEqual(expectedMove, actualMove, $"{path}.Length");
          if (!expectedMove) {
            break;
          }

          _checkEquals(expectedIter.Current, actualIter.Current, checkDebug, $"{path}[{i}]");
          i++;
        }
      } else {
        Assert.AreEqual(expected, actual, path);
      }
    }
  }

  [TestFixture]
  class ParseItemEqualsTest {
    [Test]
    public void BasicValues_Success() {
      ParseItemEquals.CheckEquals(1, 1);
      ParseItemEquals.CheckEquals(2.3, 2.3);
      ParseItemEquals.CheckEquals("abc", "abc");
      ParseItemEquals.CheckEquals(true, true);
    }

    [Test]
    public void BasicValues_Error() {
      Assert.Throws<AssertionException>(() => ParseItemEquals.CheckEquals(1, 4));
      Assert.Throws<AssertionException>(() => ParseItemEquals.CheckEquals(1.2, 1.4));
      Assert.Throws<AssertionException>(() => ParseItemEquals.CheckEquals("abc", "cde"));
      Assert.Throws<AssertionException>(() => ParseItemEquals.CheckEquals(true, false));
      // Different types.
      Assert.Throws<AssertionException>(() => ParseItemEquals.CheckEquals(1, 1.0));
      Assert.Throws<AssertionException>(() => ParseItemEquals.CheckEquals(1, true));
      Assert.Throws<AssertionException>(() => ParseItemEquals.CheckEquals(1, "1"));
    }

    [Test]
    public void ParseItem_BasicSuccess() {
      ParseItemEquals.CheckEquals(new LiteralItem(3.0), new LiteralItem(3.0));
    }

    [Test]
    public void ParseItem_BasicFailure() {
      _checkError(".Value", () => {
        ParseItemEquals.CheckEquals(new LiteralItem(3.0), new LiteralItem(4.0));
      });
    }

    [Test]
    public void ParseItem_DebugFailure() {
      var info1 = new DebugInfo("abc", 4, 7, 4, 7);
      var info2 = new DebugInfo("abc", 10, 7, 4, 7);
      var info3 = new DebugInfo("abc", 4, 10, 4, 7);
      var info4 = new DebugInfo("abc", 4, 7, 10, 7);
      var info5 = new DebugInfo("abc", 4, 7, 4, 10);
      var info6 = new DebugInfo("def", 4, 7, 4, 10);
      _checkError(".Debug.StartPos", () => {
        ParseItemEquals.CheckEquals(new LiteralItem(3.0) { Debug = info1 },
                                    new LiteralItem(3.0) { Debug = info2 },
                                    checkDebug: true);
      });
      _checkError(".Debug.StartLine", () => {
        ParseItemEquals.CheckEquals(new LiteralItem(3.0) { Debug = info1 },
                                    new LiteralItem(3.0) { Debug = info3 },
                                    checkDebug: true);
      });
      _checkError(".Debug.EndPos", () => {
        ParseItemEquals.CheckEquals(new LiteralItem(3.0) { Debug = info1 },
                                    new LiteralItem(3.0) { Debug = info4 },
                                    checkDebug: true);
      });
      _checkError(".Debug.EndLine", () => {
        ParseItemEquals.CheckEquals(new LiteralItem(3.0) { Debug = info1 },
                                    new LiteralItem(3.0) { Debug = info5 },
                                    checkDebug: true);
      });
      _checkError(".Debug.Path", () => {
        ParseItemEquals.CheckEquals(new LiteralItem(3.0) { Debug = info1 },
                                    new LiteralItem(3.0) { Debug = info6 },
                                    checkDebug: true);
      });
    }

    [Test]
    public void ParseItem_FailureDifferentTypes() {
      _checkError(".Lhs", () => {
        ParseItemEquals.CheckEquals(
            new BinOpItem(
                new LiteralItem(1.0),
                BinaryOperationType.Add,
                new LiteralItem(2.0)),
            new BinOpItem(
                new NameItem("foo"),
                BinaryOperationType.Add,
                new LiteralItem(2.0)));
      });
    }

    [Test]
    public void ParseItem_SkipDebug() {
      var info1 = new DebugInfo("abc", 4, 7, 5, 2);
      var info2 = new DebugInfo("def", 8, 12, 9, 14);
      ParseItemEquals.CheckEquals(new LiteralItem(3.0) { Debug = info1 },
                                  new LiteralItem(3.0) { Debug = info2 });
    }

    [Test]
    public void Nested_Success() {
      ParseItemEquals.CheckEquals(
        new BinOpItem(
            new BinOpItem(
              new LiteralItem(1.0),
              BinaryOperationType.Subtract,
              new LiteralItem(2.0)),
            BinaryOperationType.Add,
            new BinOpItem(
              new NameItem("foo"),
              BinaryOperationType.Subtract,
              new UnOpItem(new LiteralItem(3.0), UnaryOperationType.Minus))),
        new BinOpItem(
            new BinOpItem(
              new LiteralItem(1.0),
              BinaryOperationType.Subtract,
              new LiteralItem(2.0)),
            BinaryOperationType.Add,
            new BinOpItem(
              new NameItem("foo"),
              BinaryOperationType.Subtract,
              new UnOpItem(new LiteralItem(3.0), UnaryOperationType.Minus))));
    }

    [Test]
    public void Nested_Failure() {
      _checkError(".Lhs.Rhs.Value", () => {
        ParseItemEquals.CheckEquals(
          new BinOpItem(
              new BinOpItem(
                new LiteralItem(1.0),
                BinaryOperationType.Subtract,
                new LiteralItem(3.0)),
              BinaryOperationType.Add,
              new BinOpItem(
                new NameItem("foo"),
                BinaryOperationType.Subtract,
                new UnOpItem(new LiteralItem(3.0), UnaryOperationType.Minus))),
          new BinOpItem(
              new BinOpItem(
                new LiteralItem(1.0),
                BinaryOperationType.Subtract,
                new LiteralItem(2.0)),
              BinaryOperationType.Add,
              new BinOpItem(
                new NameItem("foo"),
                BinaryOperationType.Subtract,
                new UnOpItem(new LiteralItem(3.0), UnaryOperationType.Minus))));
      });
    }

    [Test]
    public void List_Success() {
      var expected = new AssignmentItem(
          new[] { new NameItem("foo"), new NameItem("bar"), }, new[] { new LiteralItem("cat") });
      var actual = new AssignmentItem(
          new[] { new NameItem("foo"), new NameItem("bar"), }, new[] { new LiteralItem("cat") });
      ParseItemEquals.CheckEquals(expected, actual);
    }

    [Test]
    public void List_FailureCount() {
      var expected = new AssignmentItem(
          new[] { new NameItem("foo"), new NameItem("bar"), }, new[] { new LiteralItem("cat") });

      var actual = new AssignmentItem(new[] { new NameItem("foo") },
                                      new[] { new LiteralItem("cat") });

      _checkError(".Names.Length", () => ParseItemEquals.CheckEquals(expected, actual));
    }

    [Test]
    public void List_FailureValue() {
      var expected = new AssignmentItem(
          new[] { new NameItem("foo"), new NameItem("bar"), }, new[] { new LiteralItem("cat") });

      var actual = new AssignmentItem(
          new[] { new NameItem("foo"), new NameItem("baz") }, new[] { new LiteralItem("cat") });

      _checkError(".Names[1]", () => ParseItemEquals.CheckEquals(expected, actual));
    }

    [Test]
    public void Table_Success() {
      var expected = new TableItem(new[] {
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(1.0), new NameItem("foo")),
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(2.0), new NameItem("bar")),
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(3.0), new LiteralItem(10.0)),
      });

      var actual = new TableItem(new[] {
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(1.0), new NameItem("foo")),
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(2.0), new NameItem("bar")),
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(3.0), new LiteralItem(10.0)),
      });

      ParseItemEquals.CheckEquals(expected, actual);
    }

    [Test]
    public void Table_Failure() {
      var expected = new TableItem(new[] {
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(1.0), new NameItem("foo")),
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(3.0), new NameItem("bar")),
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(3.0), new LiteralItem(10.0)),
      });

      var actual = new TableItem(new[] {
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(1.0), new NameItem("foo")),
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(2.0), new NameItem("bar")),
          new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(3.0), new LiteralItem(10.0)),
      });

      _checkError(".Fields[1].Key", () => ParseItemEquals.CheckEquals(expected, actual));
    }

    static void _checkError(string path, TestDelegate act) {
      var e = Assert.Throws<AssertionException>(act);
      if (!e!.Message.Contains(path)) {
        throw e;
      }
    }
  }
}
