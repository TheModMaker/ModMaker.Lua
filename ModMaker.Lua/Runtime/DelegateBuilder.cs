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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// A helper that creates Delegate types that call a LuaFunction object.
  /// </summary>
  class DelegateBuilder {
    struct Item {
      public readonly Type[] Parameters;
      public readonly Type[] ReturnModifiers;
      public readonly Type[][] RequiredModifiers;
      public readonly Type ReturnType;

      public readonly MethodBuilder Builder;
      public MethodInfo Method;

      public Item(TypeBuilder builder, MethodInfo method) {
        var param = method.GetParameters();
        Parameters = param.Select(p => p.ParameterType).ToArray();
        ReturnModifiers = method.ReturnParameter.GetRequiredCustomModifiers();
        RequiredModifiers = param.Select(p => p.GetRequiredCustomModifiers()).ToArray();
        ReturnType = method.ReturnType;

        Method = null;
        Builder = builder.DefineMethod(
            "Invoke", MethodAttributes.Public | MethodAttributes.Static,
            CallingConventions.Standard, ReturnType, ReturnModifiers, null,
            new[] { typeof(LuaFunction) }.Concat(Parameters).ToArray(),
            new[] { Type.EmptyTypes }.Concat(RequiredModifiers).ToArray(), null);
      }

      public bool Matches(MethodInfo method) {
        var param = method.GetParameters();
        if (method.ReturnType != ReturnType || Parameters.Length != param.Length ||
            !method.ReturnParameter.GetRequiredCustomModifiers().SequenceEqual(ReturnModifiers)) {
          return false;
        }

        for (int i = 0; i < param.Length; i++) {
          if (Parameters[i] != param[i].ParameterType ||
              !param[i].GetRequiredCustomModifiers().SequenceEqual(RequiredModifiers[i])) {
            return false;
          }
        }
        return true;
      }
    }

    readonly ModuleBuilder _mb;
    readonly List<Item> _items = new List<Item>();
    int _id = 1;

    public DelegateBuilder() {
      var ab = AssemblyBuilder.DefineDynamicAssembly(
          new AssemblyName("DelegateBuilder"), AssemblyBuilderAccess.Run);
      _mb = ab.DefineDynamicModule("DelegateBuilder.dll");
    }

    /// <summary>
    /// Creates a new Delegate that calls the given LuaFunction.
    /// </summary>
    /// <param name="type">The Delegate type to base on.</param>
    /// <param name="function">The function object to call.</param>
    /// <returns>The new Delegate object.</returns>
    public Delegate CreateDelegate(Type type, LuaFunction function) {
      Item item = _getItem(type);
      return Delegate.CreateDelegate(type, function, item.Method);
    }
    /// <summary>
    /// Creates a new Delegate that calls the given LuaFunction.
    /// </summary>
    /// <param name="function">The function object to call.</param>
    /// <returns>The new Delegate object.</returns>
    public T CreateDelegate<T>(LuaFunction function) where T : Delegate {
      return (T)CreateDelegate(typeof(T), function);
    }

    Item _getItem(Type type) {
      MethodInfo invoke = type.GetMethod("Invoke");
      foreach (Item item in _items) {
        if (item.Matches(invoke)) {
          return item;
        }
      }

      return _createItem(invoke);
    }

    Item _createItem(MethodInfo baseInfo) {
      TypeBuilder tb = _mb.DefineType($"_delegate<${_id++}>");
      Item item = new Item(tb, baseInfo);
      ILGenerator gen = item.Builder.GetILGenerator();

      // object[] objArgs = new object[{item.Parameters.Length];
      var objArgs = gen.CreateArray(typeof(object), item.Parameters.Length);
      for (int i = 0; i < item.Parameters.Length; i++) {
        Type t = item.Parameters[i];

        // objArgs[{i}] = arg_{i+1}
        gen.Emit(OpCodes.Ldloc, objArgs);
        gen.Emit(OpCodes.Ldc_I4, i);
        gen.Emit(OpCodes.Ldarg, i + 1);
        if (t.IsByRef) {
          t = t.GetElementType();
          gen.Emit(OpCodes.Ldobj, t);
        }
        if (t.IsValueType)
          gen.Emit(OpCodes.Box, t);
        gen.Emit(OpCodes.Stelem, typeof(object));
      }

      // LuaMultiValue args = LuaMultiValue.CreateMultiValueFromObj(objArgs);
      var args = gen.DeclareLocal(typeof(LuaMultiValue));
      gen.Emit(OpCodes.Ldloc, objArgs);
      gen.Emit(OpCodes.Call, typeof(LuaMultiValue)
                                 .GetMethod(nameof(LuaMultiValue.CreateMultiValueFromObj),
                                            BindingFlags.Static | BindingFlags.Public));
      gen.Emit(OpCodes.Stloc, args);

      // LuaMultiValue ret = arg0.Invoke(LuaNil.Nil, false, args);
      var ret = gen.DeclareLocal(typeof(LuaMultiValue));
      gen.Emit(OpCodes.Ldarg_0);
      gen.Emit(OpCodes.Ldsfld, typeof(LuaNil).GetField(nameof(LuaNil.Nil)));
      gen.Emit(OpCodes.Ldc_I4_0);
      gen.Emit(OpCodes.Ldloc, args);
      gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.Invoke)));
      gen.Emit(OpCodes.Stloc, ret);

      // return ret.As<T>();
      if (item.ReturnType != null && item.ReturnType != typeof(void)) {
        gen.Emit(OpCodes.Ldloc, ret);
        gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.As))
                                       .MakeGenericMethod(item.ReturnType));
      }
      gen.Emit(OpCodes.Ret);

      item.Method = tb.CreateType().GetMethod("Invoke", BindingFlags.Static | BindingFlags.Public);
      return item;
    }
  }
}