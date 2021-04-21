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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using ModMaker.Lua;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;
using NUnit.Framework;

using Choice = ModMaker.Lua.OverloadSelector.Choice;
using CompareResult = ModMaker.Lua.OverloadSelector.CompareResult;

#nullable enable

namespace UnitTests {
  [TestFixture]
  class OverloadSelectorTest {
    class Base { }
    class Derived : Base { }
    class Other { }

    Tuple<Type, Type?>?[] _mapValues(Type?[] values) {
      return values.Select(t => t == null ? null : new Tuple<Type, Type?>(t, null)).ToArray();
    }

    void _runTest(Choice a, Choice b, Type?[] values, bool bValid = true) {
      _runTest(a, b, _mapValues(values), bValid);
    }

    void _runTest(Choice a, Choice b, Tuple<Type, Type?>?[] values, bool bValid = true) {
      // Verify both are valid when compared with something that won't match.
      var other = new Choice(new[] { typeof(int), typeof(int), typeof(int) });
      Assert.AreEqual(CompareResult.A, OverloadSelector.Compare(a, other, values));
      Assert.AreEqual(bValid ? CompareResult.A : CompareResult.Neither,
                      OverloadSelector.Compare(b, other, values));

      Assert.AreEqual(CompareResult.A, OverloadSelector.Compare(a, b, values));
      Assert.AreEqual(CompareResult.B, OverloadSelector.Compare(b, a, values));
    }

    void _runNeither(Choice a, Choice b, Type?[] values) {
      _runNeither(a, b, _mapValues(values));
    }

    void _runNeither(Choice a, Choice b, Tuple<Type, Type?>?[] values) {
      Assert.AreEqual(CompareResult.Neither, OverloadSelector.Compare(a, b, values));
      Assert.AreEqual(CompareResult.Neither, OverloadSelector.Compare(b, a, values));
    }

    delegate void TestDelegate(ref int x);
    static void _withNotNull([NotNull] object _) { }
    static void _withParams(params string[] _) { }
    static void _withParamsPrimitive(params int[] _) { }
    static void _withParamsNullablePrimitive(params int?[] _) { }
    static void _withOptional(string a, int b = 1, long c = 2) { }

    [Test]
    public void Choice_MethodInfo_Empty() {
      Action func = () => { };
      var choice = new Choice(func.GetMethodInfo()!);
      Assert.AreEqual(choice.FormalArguments, new Type[0]);
      Assert.AreEqual(choice.Nullable, new bool[0]);
      Assert.AreEqual(choice.OptionalValues, new object[0]);
      Assert.IsFalse(choice.HasParams);
      Assert.IsFalse(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_Args() {
      Func<int, bool, Base, int> func = (int a, bool b, Base c) => 0;
      var choice = new Choice(func.GetMethodInfo()!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(int), typeof(bool), typeof(Base) });
      Assert.AreEqual(choice.Nullable, new[] { false, false, true });
      Assert.AreEqual(choice.OptionalValues, new object[0]);
      Assert.IsFalse(choice.HasParams);
      Assert.IsFalse(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_NonNullAttribute() {
      var flags = BindingFlags.Static | BindingFlags.NonPublic;
      var choice = new Choice(typeof(OverloadSelectorTest).GetMethod(nameof(_withNotNull), flags)!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(object) });
      Assert.AreEqual(choice.Nullable, new[] { false });
      Assert.AreEqual(choice.OptionalValues, new object[0]);
      Assert.IsFalse(choice.HasParams);
      Assert.IsFalse(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_Params() {
      var flags = BindingFlags.Static | BindingFlags.NonPublic;
      var choice = new Choice(typeof(OverloadSelectorTest).GetMethod(nameof(_withParams), flags)!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(string[]) });
      Assert.AreEqual(choice.Nullable, new[] { true });
      Assert.AreEqual(choice.OptionalValues, new object[0]);
      Assert.IsTrue(choice.HasParams);
      Assert.IsTrue(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_ParamsPrimitive() {
      var flags = BindingFlags.Static | BindingFlags.NonPublic;
      var choice =
          new Choice(typeof(OverloadSelectorTest).GetMethod(nameof(_withParamsPrimitive), flags)!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(int[]) });
      Assert.AreEqual(choice.Nullable, new[] { true });
      Assert.AreEqual(choice.OptionalValues, new object[0]);
      Assert.IsTrue(choice.HasParams);
      Assert.IsFalse(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_ParamsNullablePrimitive() {
      var flags = BindingFlags.Static | BindingFlags.NonPublic;
      var choice = new Choice(typeof(OverloadSelectorTest).GetMethod(
          nameof(_withParamsNullablePrimitive), flags)!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(int?[]) });
      Assert.AreEqual(choice.Nullable, new[] { true });
      Assert.AreEqual(choice.OptionalValues, new object[0]);
      Assert.IsTrue(choice.HasParams);
      Assert.IsTrue(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_ByRef() {
      TestDelegate func = (ref int a) => { };
      var choice = new Choice(func.GetMethodInfo()!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(int) });
      Assert.AreEqual(choice.Nullable, new[] { false });
      Assert.AreEqual(choice.OptionalValues, new object[0]);
      Assert.IsFalse(choice.HasParams);
      Assert.IsFalse(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_Optional() {
      var flags = BindingFlags.Static | BindingFlags.NonPublic;
      var choice = new Choice(
          typeof(OverloadSelectorTest).GetMethod(nameof(_withOptional), flags)!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(string), typeof(int), typeof(long) });
      Assert.AreEqual(choice.Nullable, new[] { true, false, false});
      Assert.AreEqual(choice.OptionalValues, new object[] { (int)1, (long)2 });
      Assert.IsFalse(choice.HasParams);
      Assert.IsFalse(choice.ParamsNullable);
    }


    [Test]
    public void Compare_BasicFlow() {
      var a = new Choice(new[] { typeof(Derived) });
      var b = new Choice(new[] { typeof(Other) });
      var values = new[] { typeof(Derived) };
      Assert.AreEqual(CompareResult.A, OverloadSelector.Compare(a, b, _mapValues(values)));
    }

    [Test]
    public void Compare_BasicAmbiguous() {
      var a = new Choice(new[] { typeof(Derived) });
      var b = new Choice(new[] { typeof(Derived) });
      var values = new[] { typeof(Derived) };
      Assert.AreEqual(CompareResult.Both, OverloadSelector.Compare(a, b, _mapValues(values)));
    }

    [Test]
    public void Compare_Number() {
      var a = new Choice(new[] { typeof(double) });
      var b = new Choice(new[] { typeof(int) });
      _runTest(a, b, new[] { typeof(double) });
    }

    [Test]
    public void Compare_NumberAmbiguous() {
      var a = new Choice(new[] { typeof(int) });
      var b = new Choice(new[] { typeof(long) });
      var values = new[] { typeof(double) };
      Assert.AreEqual(CompareResult.Both, OverloadSelector.Compare(a, b, _mapValues(values)));
    }

    [Test]
    public void Compare_IgnoresExtraArgs() {
      var a = new Choice(new[] { typeof(Derived) });
      var b = new Choice(new[] { typeof(Other) });
      var values = new[] { typeof(Derived), typeof(Derived), typeof(Derived) };
      _runTest(a, b, values, bValid: false);
    }

    [Test]
    public void Compare_ChecksExtraArgs() {
      var a = new Choice(new[] { typeof(Base) });
      var b = new Choice(new[] { typeof(Derived), typeof(Other) });
      var values = new[] { typeof(Derived), typeof(Derived) };
      _runTest(a, b, values, bValid: false);
    }

    [Test]
    public void Compare_NotEnoughArgs() {
      var a = new Choice(new[] { typeof(Derived) });
      var b = new Choice(new[] { typeof(Derived), typeof(Derived) });
      var values = new[] { typeof(Derived) };
      _runTest(a, b, values, bValid: false);
    }

    [Test]
    public void Compare_NotEnoughArgsBoth() {
      var a = new Choice(new[] { typeof(Derived), typeof(Derived) });
      var b = new Choice(new[] { typeof(Derived), typeof(Other) });
      _runNeither(a, b, new[] { typeof(Derived) });
    }

    [Test]
    public void Compare_Invalid() {
      var a = new Choice(new[] { typeof(Derived) });
      var b = new Choice(new[] { typeof(Other) });
      var values = new[] { typeof(Derived) };
      _runTest(a, b, values, bValid: false);
    }

    [Test]
    public void Compare_InvalidBoth() {
      var a = new Choice(new[] { typeof(Derived) });
      var b = new Choice(new[] { typeof(Derived) });
      _runNeither(a, b, new[] { typeof(Other) });
    }

    [Test]
    public void Compare_Inherit_Better() {
      var a = new Choice(new[] { typeof(Derived) });
      var b = new Choice(new[] { typeof(Base) });
      _runTest(a, b, new[] { typeof(Derived) });

      a = new Choice(new[] { typeof(Derived), typeof(Derived) });
      b = new Choice(new[] { typeof(Base), typeof(Derived) });
      _runTest(a, b, new[] { typeof(Derived), typeof(Derived) });

      a = new Choice(new[] { typeof(Derived), typeof(Derived) });
      b = new Choice(new[] { typeof(Base), typeof(Base) });
      _runTest(a, b, new[] { typeof(Derived), typeof(Derived) });
    }

    [Test]
    public void Compare_Inherit_Ambiguous() {
      var a = new Choice(new[] { typeof(Base), typeof(Derived) });
      var b = new Choice(new[] { typeof(Derived), typeof(Base) });
      var values = new[] { typeof(Derived), typeof(Derived) };
      Assert.AreEqual(CompareResult.Both, OverloadSelector.Compare(a, b, _mapValues(values)));
      Assert.AreEqual(CompareResult.Both, OverloadSelector.Compare(b, a, _mapValues(values)));
    }

    [Test]
    public void Compare_Params_Success() {
      var a = new Choice(new[] { typeof(Derived[]) }, hasParams: true);
      var b = new Choice(new[] { typeof(Other) });
      _runTest(a, b, new[] { typeof(Derived), typeof(Derived), typeof(Derived) }, bValid: false);
    }

    [Test]
    public void Compare_Params_Ambiguous() {
      var a = new Choice(new[] { typeof(Derived[]) }, hasParams: true);
      var b = new Choice(new[] { typeof(Derived[]) }, hasParams: true);
      var values = new[] { typeof(Derived) };
      Assert.AreEqual(CompareResult.Both, OverloadSelector.Compare(a, b, _mapValues(values)));
    }

    [Test]
    public void Compare_Params_AcceptsZeroValues() {
      var a = new Choice(new[] { typeof(Derived[]) }, hasParams: true);
      var b = new Choice(new[] { typeof(Other) });
      _runTest(a, b, new Type[0], bValid: false);
    }

    [Test]
    public void Compare_Params_CantUseNormalForm() {
      var a = new Choice(new[] { typeof(Derived[]) }, hasParams: true);
      var b = new Choice(new[] { typeof(Other[]) }, hasParams: true);
      _runNeither(a, b, new[] { typeof(Derived[]) });
    }

    [Test]
    public void Compare_Params_ExpandedFormError() {
      var a = new Choice(new[] { typeof(Derived[]) }, hasParams: true);
      var b = new Choice(new[] { typeof(Other[]) }, hasParams: true);
      _runTest(a, b, new[] { typeof(Derived) }, bValid: false);
    }

    [Test]
    public void Compare_Params_Error() {
      var a = new Choice(new[] { typeof(Derived[]) }, hasParams: true);
      var b = new Choice(new[] { typeof(Derived) });
      _runNeither(a, b, new[] { typeof(Other) });
    }

    [Test]
    public void Compare_Params_MoreParamsWins() {
      var a = new Choice(new[] { typeof(Base), typeof(Base[]) }, hasParams: true);
      var b = new Choice(new[] { typeof(Base[]) }, hasParams: true);
      _runTest(a, b, new[] { typeof(Derived), typeof(Derived) });
      _runTest(a, b, new[] { typeof(Derived) });
    }

    [Test]
    public void Compare_Optional_Basic() {
      var a = new Choice(new[] { typeof(Base), typeof(Base) }, optionals: new object[] { 1 });
      var b = new Choice(new[] { typeof(Other) });
      _runTest(a, b, new[] { typeof(Derived) }, bValid: false);
    }

    [Test]
    public void Compare_Optional_NotEnough() {
      var a = new Choice(new[] { typeof(Base), typeof(Base) }, optionals: new object[] { 1 });
      var b = new Choice(new[] { typeof(Other) });
      _runNeither(a, b, new Type[0]);
    }

    [Test]
    public void Compare_Optional_MoreParamsWins() {
      var a = new Choice(new[] { typeof(Base), typeof(Base) }, optionals: new object[] { 1 });
      var b = new Choice(new[] { typeof(Base) });
      _runTest(a, b, new[] { typeof(Base) });
    }

    [Test]
    public void Compare_Nullable_Success() {
      var a = new Choice(new[] { typeof(Base) }, nullable: new[] { true });
      var b = new Choice(new[] { typeof(Base) });
      _runTest(a, b, new Type?[] { null }, bValid: false);
    }

    [Test]
    public void Compare_Nullable_Error() {
      var a = new Choice(new[] { typeof(Base) });
      var b = new Choice(new[] { typeof(Other) });
      _runNeither(a, b, new Type?[] { null });
    }

    [Test]
    public void Compare_Nullable_NonNullWins() {
      var a = new Choice(new[] { typeof(Derived) });
      var b = new Choice(new[] { typeof(Derived) }, nullable: new[] { true });
      _runTest(a, b, new[] { typeof(Derived) });
    }

    [Test]
    public void Compare_Nullable_ParamsSuccess() {
      var a = new Choice(new[] { typeof(Derived[]) }, hasParams: true, paramsNullable: true);
      var b = new Choice(new[] { typeof(Other) });
      _runTest(a, b, new Type?[] { null }, bValid: false);
    }

    [Test]
    public void Compare_Nullable_ParamsError() {
      var a = new Choice(new[] { typeof(Derived[]) }, hasParams: true);
      var b = new Choice(new[] { typeof(Other) });
      _runNeither(a, b, new Type?[] { null });
    }

    [Test]
    public void Compare_Nullable_ParamsNumber() {
      var a = new Choice(new[] { typeof(int?[]) }, hasParams: true, paramsNullable: true);
      var b = new Choice(new[] { typeof(Other) });
      _runTest(a, b, new[] { typeof(int), null, typeof(long) }, bValid: false);
    }

    [Test]
    public void Compare_DoubleType_Basic() {
      var a = new Choice(new[] { typeof(string) });
      var b = new Choice(new[] { typeof(object) });
      _runTest(a, b, new[] { new Tuple<Type, Type?>(typeof(LuaString), typeof(string)) });
    }

    [Test]
    public void Compare_DoubleType_NumberBase() {
      // Should favor double since that's the "real" type of the number.
      var a = new Choice(new[] { typeof(double) });
      var b = new Choice(new[] { typeof(int) });
      _runTest(a, b, new[] { new Tuple<Type, Type?>(typeof(LuaNumber), typeof(double)) });
    }

    [Test]
    public void Compare_DoubleType_NumberAmbiguous() {
      // Other than double, all numbers are "equivalent".
      var a = new Choice(new[] { typeof(long) });
      var b = new Choice(new[] { typeof(int) });
      var values = new[] { new Tuple<Type, Type?>(typeof(LuaNumber), typeof(double)) };
      Assert.AreEqual(CompareResult.Both, OverloadSelector.Compare(a, b, values));
    }

    [Test]
    public void FindOverload_SingleValue() {
      var choices = new[] {
        new Choice(new[] { typeof(Base) }),
      };
      var values = new[] { typeof(Base) };
      Assert.AreEqual(0, OverloadSelector.FindOverload(choices, _mapValues(values)));
    }

    [Test]
    public void FindOverload_SingleValueError() {
      var choices = new[] {
        new Choice(new[] { typeof(Other) }),
      };
      var values = new[] { typeof(Base) };
      Assert.AreEqual(-1, OverloadSelector.FindOverload(choices, _mapValues(values)));
    }

    [Test]
    public void FindOverload_Ambiguous() {
      var choices = new[] {
        new Choice(new[] { typeof(Base) }),
        new Choice(new[] { typeof(Base) }),
      };
      var values = new[] { typeof(Derived) };
      Assert.Throws<AmbiguousMatchException>(
          () => OverloadSelector.FindOverload(choices, _mapValues(values)));
    }

    [Test]
    public void FindOverload_ClearsAmbiguous() {
      var choices = new[] {
        new Choice(new[] { typeof(Base) }),
        new Choice(new[] { typeof(Base) }),
        new Choice(new[] { typeof(Derived) }),
      };
      var values = new[] { typeof(Derived) };
      Assert.AreEqual(2, OverloadSelector.FindOverload(choices, _mapValues(values)));
    }

    [Test]
    public void FindOverload_HandlesNeither() {
      var choices = new[] {
        new Choice(new[] { typeof(Base), typeof(Derived) }),
        new Choice(new[] { typeof(Derived), typeof(Base) }),
      };
      var values = new[] { typeof(Other), typeof(Other) };
      Assert.AreEqual(-1, OverloadSelector.FindOverload(choices, _mapValues(values)));
    }

    [Test]
    public void FindOverload_HandlesNeitherWithMore() {
      var choices = new[] {
        new Choice(new[] { typeof(Base), typeof(Derived) }),
        new Choice(new[] { typeof(Derived), typeof(Base) }),
        new Choice(new[] { typeof(Other), typeof(Other) }),
      };
      var values = new[] { typeof(Other), typeof(Other) };
      Assert.AreEqual(2, OverloadSelector.FindOverload(choices, _mapValues(values)));
    }


    class CastableFrom {
      public static explicit operator Base?(CastableFrom _) { return null; }
    }
    class CastableFromDerived : CastableFrom { }
    class CastableTo {
      public static implicit operator CastableTo?(Base _) { return null; }
    }
    class CastableToDerived : CastableTo { }
    class OtherCastable {
      public static implicit operator Other?(OtherCastable _) { return null; }
    }
    [LuaIgnore]
    class NonVisibleCastable {
      public static explicit operator Base?(NonVisibleCastable _) { return null; }
    }
    class NonVisibleCastable2 {
      [LuaIgnore]
      public static explicit operator Base?(NonVisibleCastable2 _) { return null; }
    }
    class NonVisibleCastableTo {
      [LuaIgnore]
      public static explicit operator NonVisibleCastableTo?(Base _) { return null; }
    }

    [Test]
    public void TypesCompatible_SameType() {
      MethodInfo? info;
      Assert.IsTrue(OverloadSelector.TypesCompatible(typeof(Derived), typeof(Derived), out info));
      Assert.IsNull(info);
    }

    [Test]
    public void TypesCompatible_BaseType() {
      MethodInfo? info;
      Assert.IsTrue(OverloadSelector.TypesCompatible(typeof(Derived), typeof(Base), out info));
      Assert.IsNull(info);
    }

    [Test]
    public void TypesCompatible_DerivedTypeError() {
      Assert.IsFalse(OverloadSelector.TypesCompatible(typeof(Base), typeof(Derived), out _));
    }

    [Test]
    public void TypesCompatible_NullableSource() {
      MethodInfo? info;
      Assert.IsTrue(OverloadSelector.TypesCompatible(typeof(int?), typeof(int), out info));
      Assert.IsNull(info);
    }

    [Test]
    public void TypesCompatible_NullableDest() {
      MethodInfo? info;
      Assert.IsTrue(OverloadSelector.TypesCompatible(typeof(int), typeof(int?), out info));
      Assert.IsNull(info);
    }

    [Test]
    public void TypesCompatible_CompatiblePrimitives() {
      Type[] types = new[] {
          // Note that bool and (U)IntPtr aren't considered the compatible.
          // Also, don't include char since there are cases where it won't work.
          typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int),
          typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
      };
      foreach (Type a in types) {
        foreach (Type b in types) {
          if (a == b)
            continue;
          MethodInfo? info;
          Assert.IsTrue(OverloadSelector.TypesCompatible(a, b, out info));
          Assert.IsNotNull(info);

          object? obj = info!.Invoke(null, new[] { Activator.CreateInstance(a) });
          Assert.IsInstanceOf(b, obj);
        }
      }
    }

    [Test]
    public void TypesCompatible_Char() {
      Type[] badTypes = new[] {
          typeof(float), typeof(double), typeof(decimal),
      };
      foreach (Type a in badTypes) {
        Assert.IsFalse(OverloadSelector.TypesCompatible(a, typeof(char), out _));
        Assert.IsFalse(OverloadSelector.TypesCompatible(typeof(char), a, out _));
      }

      Type[] goodTypes = new[] {
          typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int),
          typeof(uint), typeof(long), typeof(ulong),
      };
      foreach (Type a in goodTypes) {
        MethodInfo? info;
        Assert.IsTrue(OverloadSelector.TypesCompatible(a, typeof(char), out info));
        Assert.IsNotNull(info);
        object? obj = info!.Invoke(null, new[] { Activator.CreateInstance(a) });
        Assert.IsInstanceOf<char>(obj);

        Assert.IsTrue(OverloadSelector.TypesCompatible(typeof(char), a, out info));
        Assert.IsNotNull(info);
        obj = info!.Invoke(null, new[] { (object)'\0' });
        Assert.IsInstanceOf(a, obj);
      }
    }

    [Test]
    public void TypesCompatible_OtherPrimitives() {
      Type[] types = new[] { typeof(int), typeof(bool), typeof(IntPtr), typeof(UIntPtr) };
      foreach (Type a in types) {
        foreach (Type b in types) {
          if (a == b)
            continue;
          Assert.IsFalse(OverloadSelector.TypesCompatible(a, b, out _));
        }
      }
    }

    [Test]
    public void TypesCompatible_UserCastFrom() {
      MethodInfo? info;
      Assert.IsTrue(OverloadSelector.TypesCompatible(typeof(CastableFrom), typeof(Base), out info));
      Assert.IsNotNull(info);
    }

    [Test]
    public void TypesCompatible_UserCastFromInBaseClass() {
      MethodInfo? info;
      Assert.IsTrue(OverloadSelector.TypesCompatible(typeof(CastableFromDerived), typeof(Base),
                                                     out info));
      Assert.IsNotNull(info);
    }

    [Test]
    public void TypesCompatible_UserCastFromDerivedObject() {
      Assert.IsFalse(OverloadSelector.TypesCompatible(typeof(CastableFromDerived), typeof(Derived),
                                                     out _));
    }

    [Test]
    public void TypesCompatible_UserCastOther() {
      Assert.IsFalse(OverloadSelector.TypesCompatible(typeof(OtherCastable), typeof(Base), out _));
    }

    [Test]
    public void TypesCompatible_UserCastTo() {
      MethodInfo? info;
      Assert.IsTrue(OverloadSelector.TypesCompatible(typeof(Base), typeof(CastableTo), out info));
      Assert.IsNotNull(info);
    }

    [Test]
    public void TypesCompatible_UserCastToInBaseClass() {
      Assert.IsFalse(OverloadSelector.TypesCompatible(typeof(Base), typeof(CastableToDerived),
                                                      out _));
    }

    [Test]
    public void TypesCompatible_UserCastToDerivedObject() {
      MethodInfo? info;
      Assert.IsTrue(OverloadSelector.TypesCompatible(typeof(Derived), typeof(CastableTo),
                                                     out info));
      Assert.IsNotNull(info);
    }

    [Test]
    public void TypesCompatible_UserCastNotVisible() {
      Assert.IsFalse(OverloadSelector.TypesCompatible(typeof(NonVisibleCastable), typeof(Base),
                                                      out _));
      Assert.IsFalse(OverloadSelector.TypesCompatible(typeof(NonVisibleCastable2), typeof(Base),
                                                      out _));
      Assert.IsFalse(OverloadSelector.TypesCompatible(typeof(Base), typeof(NonVisibleCastableTo),
                                                      out _));
    }


    [Test]
    public void ConvertArguments_MultiValueArg() {
      var choice = new Choice(new[] { typeof(ILuaMultiValue) });
      var args = new LuaMultiValue();

      var converted = OverloadSelector.ConvertArguments(args, choice);
      Assert.AreEqual(new[] { args }, converted);
    }

    [Test]
    public void ConvertArguments_FormalArgs() {
      var choice = new Choice(new[] { typeof(int), typeof(string) });
      var args = new LuaMultiValue(LuaNumber.Create(0), new LuaString("foo"));

      var converted = OverloadSelector.ConvertArguments(args, choice);
      Assert.AreEqual(new object[] { 0, "foo" }, converted);
    }

    [Test]
    public void ConvertArguments_AddsOptionals() {
      var choice = new Choice(new[] { typeof(string), typeof(int), typeof(int) },
                              optionals: new object[] { 1, 2 });

      var args = new LuaMultiValue(new LuaString("a"));
      var converted = OverloadSelector.ConvertArguments(args, choice);
      Assert.AreEqual(new object[] { "a", 1, 2 }, converted);

      args = new LuaMultiValue(new LuaString("a"), LuaNumber.Create(9));
      converted = OverloadSelector.ConvertArguments(args, choice);
      Assert.AreEqual(new object[] { "a", 9, 2 }, converted);

      args = new LuaMultiValue(new LuaString("a"), LuaNumber.Create(9), LuaNumber.Create(10));
      converted = OverloadSelector.ConvertArguments(args, choice);
      Assert.AreEqual(new object[] { "a", 9, 10 }, converted);
    }

    [Test]
    public void ConvertArguments_AddsParams() {
      var choice = new Choice(new[] { typeof(string), typeof(int[]) }, hasParams: true);

      var args = new LuaMultiValue(new LuaString("a"), LuaNumber.Create(1), LuaNumber.Create(2));
      var converted = OverloadSelector.ConvertArguments(args, choice);
      Assert.AreEqual(new object[] { "a", new int[] { 1, 2 } }, converted);

      args = new LuaMultiValue(new LuaString("a"), LuaNumber.Create(1));
      converted = OverloadSelector.ConvertArguments(args, choice);
      Assert.AreEqual(new object[] { "a", new int[] { 1 } }, converted);

      args = new LuaMultiValue(new LuaString("a"));
      converted = OverloadSelector.ConvertArguments(args, choice);
      Assert.AreEqual(new object[] { "a", new int[0] }, converted);
    }
  }
}
