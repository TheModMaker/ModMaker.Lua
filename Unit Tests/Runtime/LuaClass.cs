// Copyright 2017 Jacob Trimble
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
using System.Reflection;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;
using NUnit.Framework;

namespace UnitTests.Runtime {
  [TestFixture]
  public class LuaClassTest : TestBase {
    public abstract class BaseClass {
      public BaseClass() { }

      public virtual int VirtualMethod() {
        return 15;
      }

      public virtual void MethodIntArg(int arg) { }

      public abstract int AbstractMethod();

      public int Field;
    }

    public interface IInterface {
      int VirtualMethod();
      int Ambiguous();
    }

    public interface IInterface2 {
      int OtherMethod();
      int Ambiguous();
    }

    public LuaClassTest() {
      _lua.Register(typeof(BaseClass));
      _lua.Register(typeof(IInterface));
      _lua.Register(typeof(IInterface2));
    }


    #region NoBaseClass
    [Test]
    public void NoBaseClass_InstanceInLua() {
      _lua.DoText(@"
class Foobar

function Foobar:method()
  return 'abc'
end

Foobar.field = 123


local inst = Foobar()
assertEquals(123, inst.field, 'field should be 123')
assertEquals('abc', inst.method(), 'member should exist')
");
    }

    [Test]
    public void NoBaseClass_InstanceInCs() {
      _lua.DoText(@"
class Foobar

function Foobar:method()
  return 'abc'
end

Foobar.field = 123
");

      LuaClass cls = _getLuaClass("Foobar");
      object inst = cls.CreateInstance();
      Assert.IsNotNull(inst);

      FieldInfo field = inst.GetType().GetField("field");
      Assert.IsNotNull(inst.GetType().GetField("field"));
      object value = field.GetValue(inst);
      Assert.AreEqual((double)123, value);
    }

    [Test]
    public void NoBaseClass_CanCallOtherMembers() {
      _lua.DoText(@"
class Foobar

function Foobar:other()
  return 123
end

function Foobar:method()
  assertNotNull(self, 'must have self')
  assertEquals(123, self.other(), 'other value')
end


local inst = Foobar()
inst:method()
");
    }

    [Test]
    public void NoBaseClass_CanDefineLuaConstructor() {
      _lua.DoText(@"
class Foobar

function Foobar:__ctor()
  self.field = 123
end
Foobar.field = int


local inst = Foobar()
assertEquals(123, inst.field, 'constructor set field')
");
    }
    #endregion

    #region BaseClass
    [Test]
    public void BaseClass_InstanceInLua() {
      _lua.DoText(@"
class Foobar : BaseClass

local inst = Foobar()

assertNotNull(inst, 'create instance')
assertEquals(15, inst.VirtualMethod(), 'virtual method')
");
    }

    [Test]
    public void BaseClass_InheritsFromType() {
      _lua.DoText(@"class Foobar : BaseClass");

      var cls = _getLuaClass("Foobar");
      object inst = cls.CreateInstance();
      Assert.IsNotNull(inst);
      Assert.IsInstanceOf<BaseClass>(inst);
    }

    [Test]
    public void BaseClass_OverridesMethod() {
      _lua.DoText(@"
class Foobar : BaseClass

function Foobar:VirtualMethod()
  return 1000
end
");

      var cls = _getLuaClass("Foobar");
      object inst = cls.CreateInstance();
      Assert.IsNotNull(inst);
      Assert.AreEqual((double)1000, ((BaseClass)inst).VirtualMethod());
    }

    [Test]
    public void BaseClass_CanAddMethods() {
      _lua.DoText(@"
class Foobar : BaseClass

function Foobar:VirtualMethod()
  return 1000
end
function Foobar:NewMethod()
  return 123
end

local inst = Foobar()
assertEquals(1000, inst:VirtualMethod(), 'VirtualMethod')
assertEquals(123, inst:NewMethod(), 'NewMethod')
");
    }

    [Test]
    public void BaseClass_OverridesMethodWithArguments() {
      _lua.DoText(@"
class Foobar : BaseClass

function Foobar:MethodIntArg(a)
  print(self, a)
  assertEquals(123, a, '')
end
");

      var cls = _getLuaClass("Foobar");
      object inst = cls.CreateInstance();
      Assert.IsNotNull(inst);
      ((BaseClass)inst).MethodIntArg(123);
    }

    [Test]
    public void BaseClass_CanCallOtherMembers() {
      _lua.DoText(@"
class Foobar : BaseClass

function Foobar:VirtualMethod()
  return 456
end
function Foobar:NewMethod()
  return 123
end

function Foobar:AbstractMethod()
  assertNotNull(self, 'AbstractMethod must have self')
  assertEquals(123, self:NewMethod(), 'NewMethod inside AbstractMethod')
  assertEquals(456, self:VirtualMethod(), 'VirtualMethod inside AbstractMethod')
  return 0
end
function Foobar:TestMethod()
  assertNotNull(self, 'TestMethod must have self')
  assertEquals(123, self:NewMethod(), 'NewMethod inside TestMethod')
  assertEquals(456, self:VirtualMethod(), 'VirtualMethod inside TestMethod')
end


local inst = Foobar()
inst:AbstractMethod()
inst:TestMethod()
");
    }

    [Test]
    public void BaseClass_CantReplaceField() {
      _lua.DoText(@"
class Foobar : BaseClass

assertThrows('replace fields with different type', function()
  Foobar.Field = 'abc'
end)

assertThrows('replace fields with same type', function()
  Foobar.Field = 123
end)
");

    }

    [Test]
    public void BaseClass_CantReplaceMethodWithField() {
      _lua.DoText(@"
class Foobar : BaseClass

assertThrows('can\'t replace method with field', function()
  Foobar.VirtualMethod = 'abc'
end)
");

    }

    [Test]
    public void BaseClass_ThrowsNotImplemented() {
      _lua.DoText(@"class Foobar : BaseClass");

      var cls = _getLuaClass("Foobar");
      object inst = cls.CreateInstance();
      Assert.IsNotNull(inst);

      try {
        ((BaseClass)inst).AbstractMethod();
        Assert.Fail("Should throw");
      } catch (NotImplementedException) { }
    }
    #endregion

    #region Interfaces

    [Test]
    public void Interface_InstanceInLua() {
      _lua.DoText(@"
class Foobar : IInterface

function Foobar:VirtualMethod()
  return 1
end

local inst = Foobar()

assertNotNull(inst, 'create instance')
assertEquals(1, inst.VirtualMethod(), 'virtual method')
");
    }

    [Test]
    public void Interface_InheritsFromType() {
      _lua.DoText(@"
class Foobar : IInterface

function Foobar:VirtualMethod()
  return 1
end
");

      var cls = _getLuaClass("Foobar");
      object inst = cls.CreateInstance();
      Assert.IsNotNull(inst);
      Assert.IsInstanceOf<IInterface>(inst);
    }

    [Test]
    public void Interface_ImplementsMethod() {
      _lua.DoText(@"
class Foobar : IInterface

function Foobar:VirtualMethod()
  return 1000
end
");

      var cls = _getLuaClass("Foobar");
      object inst = cls.CreateInstance();
      Assert.IsNotNull(inst);
      Assert.AreEqual((double)1000, ((IInterface)inst).VirtualMethod());
    }

    [Test]
    public void Interface_ThrowsNotImplemented() {
      _lua.DoText(@"class Foobar : IInterface");

      var cls = _getLuaClass("Foobar");
      object inst = cls.CreateInstance();
      Assert.IsNotNull(inst);

      try {
        ((IInterface)inst).VirtualMethod();
        Assert.Fail("Should throw");
      } catch (NotImplementedException) { }
    }

    [Test]
    public void Interface_MultipleInheritance() {
      _lua.DoText(@"
class Foobar : IInterface, IInterface2

function Foobar:VirtualMethod()
  return 123
end
function Foobar:OtherMethod()
  return 555
end");

      var cls = _getLuaClass("Foobar");
      object inst = cls.CreateInstance();
      Assert.IsNotNull(inst);
      Assert.AreEqual((double)123, ((IInterface)inst).VirtualMethod());
      Assert.AreEqual((double)555, ((IInterface2)inst).OtherMethod());
    }

    [Test]
    public void Interface_ExplicitImplementation() {
      _lua.DoText(@"
class Foobar : IInterface, IInterface2

function Foobar.IInterface:Ambiguous()
  return 123
end
function Foobar.IInterface2:Ambiguous()
  return 456
end

local inst = Foobar()
assertNotNull(inst, 'create instance')
assertEquals(nil, inst.Ambiguous, 'no member when explicit')");

      var cls = _getLuaClass("Foobar");
      object inst = cls.CreateInstance();
      Assert.IsNotNull(inst);
      Assert.AreEqual((double)123, ((IInterface)inst).Ambiguous());
      Assert.AreEqual((double)456, ((IInterface2)inst).Ambiguous());
    }

    #endregion

    private LuaClass _getLuaClass(string name) {
      ILuaValue luaName = _lua.Environment.Runtime.CreateValue(name);
      ILuaValue cls = _lua.Environment.GlobalsTable.GetItemRaw(luaName);

      Assert.IsInstanceOf<LuaClass>(cls);
      return (LuaClass)cls;
    }
  }
}
