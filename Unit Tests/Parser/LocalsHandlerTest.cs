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

using ModMaker.Lua;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace UnitTests.Parser {
  [TestFixture]
  class LocalsResolverTest {
    static IResolveConstraint _throwsGotoException() {
      return Throws.TypeOf<CompilerException>()
          .With.Property("Errors").Exactly(1).Property("ID").EqualTo(MessageId.LabelNotFound);
    }

    [Test]
    public void ResolveName_Basic() {
      var handler = new LocalsResolver();
      var a = new NameItem("a");
      var b = new NameItem("b");
      using (handler.DefineFunction()) {
        handler.DefineLocals(new[] { a, b });
        Assert.That(handler.ResolveName("a"), Is.SameAs(a));
        Assert.That(handler.ResolveName("b"), Is.SameAs(b));
        Assert.That(handler.ResolveName("c"), Is.Null);
      }
    }

    [Test]
    public void ResolveName_NestedBlock() {
      var handler = new LocalsResolver();
      var a = new NameItem("a");
      var b = new NameItem("b");
      using (handler.DefineFunction()) {
        handler.DefineLocals(new[] { a });
        using (handler.DefineBlock()) {
          handler.DefineLocals(new[] { b });
          using (handler.DefineBlock()) {
            Assert.That(handler.ResolveName("a"), Is.SameAs(a));
            Assert.That(handler.ResolveName("b"), Is.SameAs(b));
            Assert.That(handler.ResolveName("c"), Is.Null);
          }
        }
      }
    }

    [Test]
    public void ResolveName_NestedFunction() {
      var handler = new LocalsResolver();
      var a = new NameItem("a");
      var b = new NameItem("b");
      using (handler.DefineFunction()) {
        handler.DefineLocals(new[] { a });
        using (handler.DefineFunction()) {
          handler.DefineLocals(new[] { b });
          using (handler.DefineFunction()) {
            Assert.That(handler.ResolveName("a"), Is.SameAs(a));
            Assert.That(handler.ResolveName("b"), Is.SameAs(b));
            Assert.That(handler.ResolveName("c"), Is.Null);
          }
        }
      }
    }

    [Test]
    public void ResolveName_ShadowsParentBlock() {
      var handler = new LocalsResolver();
      var a1 = new NameItem("a");
      var a2 = new NameItem("a");
      using (handler.DefineFunction()) {
        handler.DefineLocals(new[] { a1 });
        Assert.That(handler.ResolveName("a"), Is.SameAs(a1));
        using (handler.DefineBlock()) {
          handler.DefineLocals(new[] { a2 });
          Assert.That(handler.ResolveName("a"), Is.SameAs(a2));
        }
        Assert.That(handler.ResolveName("a"), Is.SameAs(a1));
      }
    }

    [Test]
    public void ResolveName_ShadowsParentFunction() {
      var handler = new LocalsResolver();
      var a1 = new NameItem("a");
      var a2 = new NameItem("a");
      using (handler.DefineFunction()) {
        handler.DefineLocals(new[] { a1 });
        Assert.That(handler.ResolveName("a"), Is.SameAs(a1));
        using (handler.DefineFunction()) {
          handler.DefineLocals(new[] { a2 });
          Assert.That(handler.ResolveName("a"), Is.SameAs(a2));
        }
        Assert.That(handler.ResolveName("a"), Is.SameAs(a1));
      }
    }

    [Test]
    public void GetFunctionInfo_HasNested_False() {
      var handler = new LocalsResolver();
      using (handler.DefineFunction()) {
        using (handler.DefineBlock())
          handler.DefineLocals(new[] { new NameItem("a") });
        Assert.That(handler.GetFunctionInfo().HasNested, Is.False);
      }
    }

    [Test]
    public void GetFunctionInfo_HasNested_True() {
      var handler = new LocalsResolver();
      using (handler.DefineFunction()) {
        using (handler.DefineFunction())
          handler.DefineLocals(new[] { new NameItem("a") });
        Assert.That(handler.GetFunctionInfo().HasNested, Is.True);
      }
    }

    [Test]
    public void GetFunctionInfo_Captures_Basic() {
      var handler = new LocalsResolver();
      var a = new NameItem("a");
      Assert.Multiple(() => {
        using (handler.DefineFunction()) {
          handler.DefineLocals(new[] { a });
          using (handler.DefineFunction()) {
            handler.ResolveName("a");
            var info = handler.GetFunctionInfo();
            Assert.That(info.CapturedParents, Has.Exactly(1).SameAs(a));
            Assert.That(info.CapturedLocals, Is.Empty);
          }
          var info2 = handler.GetFunctionInfo();
          Assert.That(info2.CapturedParents, Is.Empty);
          Assert.That(info2.CapturedLocals, Has.Exactly(1).SameAs(a));
        }
      });
    }

    [Test]
    public void GetFunctionInfo_Captures_Both() {
      var handler = new LocalsResolver();
      var a = new NameItem("a");
      var b = new NameItem("b");
      Assert.Multiple(() => {
        using (handler.DefineFunction()) {
          handler.DefineLocals(new[] { a });
          using (handler.DefineFunction()) {
            handler.DefineLocals(new[] { b });
            using (handler.DefineFunction()) {
              handler.ResolveName("a");
              handler.ResolveName("b");
            }
            var info = handler.GetFunctionInfo();
            Assert.That(info.CapturedParents, Has.Exactly(1).SameAs(a));
            Assert.That(info.CapturedLocals, Has.Exactly(1).SameAs(b));
          }
          var info2 = handler.GetFunctionInfo();
          Assert.That(info2.CapturedParents, Is.Empty);
          Assert.That(info2.CapturedLocals, Has.Exactly(1).SameAs(a));
        }
      });
    }

    [Test]
    public void GetFunctionInfo_Captures_NotLocal() {
      var handler = new LocalsResolver();
      var a = new NameItem("a");
      Assert.Multiple(() => {
        using (handler.DefineFunction()) {
          handler.DefineLocals(new[] { a });
          handler.ResolveName("a");

          var info = handler.GetFunctionInfo();
          Assert.That(info.CapturedParents, Is.Empty);
          Assert.That(info.CapturedLocals, Is.Empty);
        }
      });
    }

    [Test]
    public void Goto_BasicLabelFirst() {
      var handler = new LocalsResolver();
      var label = new LabelItem("a");
      var @goto = new GotoItem("a");
      Assert.That(@goto.Target, Is.Null);

      using (handler.DefineFunction()) {
        handler.DefineLabel(label);
        handler.DefineGoto(@goto);
      }

      Assert.That(@goto.Target, Is.SameAs(label));
    }

    [Test]
    public void Goto_BasicGotoFirst() {
      var handler = new LocalsResolver();
      var label = new LabelItem("a");
      var @goto = new GotoItem("a");
      Assert.That(@goto.Target, Is.Null);

      using (handler.DefineFunction()) {
        handler.DefineGoto(@goto);
        handler.DefineLabel(label);
      }

      Assert.That(@goto.Target, Is.SameAs(label));
    }

    [Test]
    public void Goto_NestedGoto() {
      var handler = new LocalsResolver();
      var label = new LabelItem("a");
      var @goto = new GotoItem("a");
      Assert.That(@goto.Target, Is.Null);

      using (handler.DefineFunction()) {
        handler.DefineLabel(label);
        using (handler.DefineBlock())
          handler.DefineGoto(@goto);
      }

      Assert.That(@goto.Target, Is.SameAs(label));
    }

    [Test]
    public void Goto_Local() {
      var handler = new LocalsResolver();
      var label = new LabelItem("a");
      var @goto = new GotoItem("a");
      Assert.That(@goto.Target, Is.Null);

      using (handler.DefineFunction()) {
        handler.DefineLabel(label);
        handler.DefineLocals(new[] { new NameItem("x") });
        handler.DefineGoto(@goto);
      }

      Assert.That(@goto.Target, Is.SameAs(label));
    }

    [Test]
    public void Goto_LocalNested() {
      var handler = new LocalsResolver();
      var label = new LabelItem("a");
      var @goto = new GotoItem("a");
      Assert.That(@goto.Target, Is.Null);

      using (handler.DefineFunction()) {
        handler.DefineLabel(label);
        using (handler.DefineBlock()) {
          handler.DefineLocals(new[] { new NameItem("x") });
          handler.DefineGoto(@goto);
        }
      }

      Assert.That(@goto.Target, Is.SameAs(label));
    }

    [Test]
    public void Goto_LocalNested2() {
      var handler = new LocalsResolver();
      var label = new LabelItem("a");
      var @goto = new GotoItem("a");
      Assert.That(@goto.Target, Is.Null);

      using (handler.DefineFunction()) {
        handler.DefineLabel(label);
        handler.DefineLocals(new[] { new NameItem("x") });
        using (handler.DefineBlock())
          handler.DefineGoto(@goto);
      }

      Assert.That(@goto.Target, Is.SameAs(label));
    }

    [Test]
    public void Goto_Error_Local() {
      var handler = new LocalsResolver();
      var label = new LabelItem("a");
      var @goto = new GotoItem("a");
      Assert.That(() => {
        using (handler.DefineFunction()) {
          handler.DefineGoto(@goto);
          handler.DefineLocals(new[] { new NameItem("x") });
          handler.DefineLabel(label);
        }
      }, _throwsGotoException());
    }

    [Test]
    public void Goto_Error_FunctionGoto() {
      var handler = new LocalsResolver();
      var label = new LabelItem("a");
      var @goto = new GotoItem("a");
      Assert.That(() => {
        using (handler.DefineFunction()) {
          handler.DefineLabel(label);
          using (handler.DefineFunction())
            handler.DefineGoto(@goto);
        }
      }, _throwsGotoException());
    }

    [Test]
    public void Goto_Error_FunctionLabel() {
      var handler = new LocalsResolver();
      var label = new LabelItem("a");
      var @goto = new GotoItem("a");
      Assert.That(() => {
        using (handler.DefineFunction()) {
          handler.DefineGoto(@goto);
          using (handler.DefineFunction())
            handler.DefineLabel(label);
        }
      }, _throwsGotoException());
    }

    [Test]
    public void Goto_Error_BlockLabel() {
      var handler = new LocalsResolver();
      var label = new LabelItem("a");
      var @goto = new GotoItem("a");
      Assert.That(() => {
        using (handler.DefineFunction()) {
          handler.DefineGoto(@goto);
          using (handler.DefineBlock())
            handler.DefineLabel(label);
        }
      }, _throwsGotoException());
    }
  }
}
