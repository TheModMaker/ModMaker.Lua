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
    static void _withParamsPrimative(params int[] _) { }
    static void _withParamsNullablePrimative(params int?[] _) { }

    [Test]
    public void Choice_MethodInfo_Empty() {
      Action func = () => { };
      var choice = new Choice(func.GetMethodInfo()!);
      Assert.AreEqual(choice.FormalArguments, new Type[0]);
      Assert.AreEqual(choice.Nullable, new bool[0]);
      Assert.AreEqual(choice.OptionalCount, 0);
      Assert.IsFalse(choice.HasParams);
      Assert.IsFalse(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_Args() {
      Func<int, bool, Base, int> func = (int a, bool b, Base c) => 0;
      var choice = new Choice(func.GetMethodInfo()!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(int), typeof(bool), typeof(Base) });
      Assert.AreEqual(choice.Nullable, new[] { false, false, true });
      Assert.AreEqual(choice.OptionalCount, 0);
      Assert.IsFalse(choice.HasParams);
      Assert.IsFalse(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_NonNullAttribute() {
      var flags = BindingFlags.Static | BindingFlags.NonPublic;
      var choice = new Choice(typeof(OverloadSelectorTest).GetMethod(nameof(_withNotNull), flags)!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(object) });
      Assert.AreEqual(choice.Nullable, new[] { false });
      Assert.AreEqual(choice.OptionalCount, 0);
      Assert.IsFalse(choice.HasParams);
      Assert.IsFalse(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_Params() {
      var flags = BindingFlags.Static | BindingFlags.NonPublic;
      var choice = new Choice(typeof(OverloadSelectorTest).GetMethod(nameof(_withParams), flags)!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(string[]) });
      Assert.AreEqual(choice.Nullable, new[] { true });
      Assert.AreEqual(choice.OptionalCount, 0);
      Assert.IsTrue(choice.HasParams);
      Assert.IsTrue(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_ParamsPrimative() {
      var flags = BindingFlags.Static | BindingFlags.NonPublic;
      var choice =
          new Choice(typeof(OverloadSelectorTest).GetMethod(nameof(_withParamsPrimative), flags)!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(int[]) });
      Assert.AreEqual(choice.Nullable, new[] { true });
      Assert.AreEqual(choice.OptionalCount, 0);
      Assert.IsTrue(choice.HasParams);
      Assert.IsFalse(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_ParamsNullablePrimative() {
      var flags = BindingFlags.Static | BindingFlags.NonPublic;
      var choice = new Choice(typeof(OverloadSelectorTest).GetMethod(
          nameof(_withParamsNullablePrimative), flags)!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(int?[]) });
      Assert.AreEqual(choice.Nullable, new[] { true });
      Assert.AreEqual(choice.OptionalCount, 0);
      Assert.IsTrue(choice.HasParams);
      Assert.IsTrue(choice.ParamsNullable);
    }

    [Test]
    public void Choice_MethodInfo_ByRef() {
      TestDelegate func = (ref int a) => { };
      var choice = new Choice(func.GetMethodInfo()!);
      Assert.AreEqual(choice.FormalArguments, new[] { typeof(int) });
      Assert.AreEqual(choice.Nullable, new[] { false });
      Assert.AreEqual(choice.OptionalCount, 0);
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
      var a = new Choice(new[] { typeof(Base), typeof(Base) }, optCount: 1);
      var b = new Choice(new[] { typeof(Other) });
      _runTest(a, b, new[] { typeof(Derived) }, bValid: false);
    }

    [Test]
    public void Compare_Optional_NotEnough() {
      var a = new Choice(new[] { typeof(Base), typeof(Base) }, optCount: 1);
      var b = new Choice(new[] { typeof(Other) });
      _runNeither(a, b, new Type[0]);
    }

    [Test]
    public void Compare_Optional_MoreParamsWins() {
      var a = new Choice(new[] { typeof(Base), typeof(Base) }, optCount: 1);
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
  }
}
