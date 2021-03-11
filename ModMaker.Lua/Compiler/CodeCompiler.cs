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
using System.Reflection;
using System.Reflection.Emit;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Compiler {
  /// <summary>
  /// Defines an ICodeCompiler to compile the IParseItem tree into IL code.
  /// </summary>
  [LuaIgnore]
  public sealed class CodeCompiler : ICodeCompiler {
    readonly List<string> _types = new List<string>();
    readonly List<Type> _delegateTypes = new List<Type>();
    readonly AssemblyBuilder _ab;
    readonly ModuleBuilder _mb;
    int _tid = 1;

    /// <summary>
    /// Creates a new CodeCompiler object.
    /// </summary>
    public CodeCompiler() {
      _ab = AssemblyBuilder.DefineDynamicAssembly(
          new AssemblyName("DynamicAssembly"), AssemblyBuilderAccess.Run);
      _mb = _ab.DefineDynamicModule("DynamicAssembly.dll");
    }

    public ILuaValue Compile(ILuaEnvironment env, IParseItem item, string name) {
      if (env == null) {
        throw new ArgumentNullException(nameof(env));
      }

      if (item == null) {
        throw new ArgumentNullException(nameof(item));
      }

      name ??= "<>_func_" + (_tid++);
      if (_types.Contains(name)) {
        int i = 0;
        while (_types.Contains(name + i)) {
          i++;
        }

        name += i;
      }

      GetInfoVisitor lVisitor = new GetInfoVisitor();
      lVisitor.Resolve(item);

      TypeBuilder tb = _mb.DefineType(
          name, TypeAttributes.Public | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed,
          typeof(LuaValueBase), Type.EmptyTypes);
      ChunkBuilder cb = new ChunkBuilder(tb, lVisitor._globalCaptures, lVisitor._globalNested);

      CompilerVisitor cVisitor = new CompilerVisitor(cb);
      item.Accept(cVisitor);
      var ret = cb.CreateChunk(env);
      return ret;
    }
    public Delegate CreateDelegate(ILuaEnvironment env, Type type, ILuaValue method) {
      if (env == null) {
        throw new ArgumentNullException(nameof(env));
      }

      if (type == null) {
        throw new ArgumentNullException(nameof(type));
      }

      if (method == null) {
        throw new ArgumentNullException(nameof(method));
      }

      if (!typeof(Delegate).IsAssignableFrom(type.BaseType)) {
        throw new ArgumentException(Resources.DeriveFromDelegate);
      }

      // Search through the cache for a compatible delegate helper
      object target; // TODO: Make CreateDelegate more efficient.
      for (int i = 0; i < _delegateTypes.Count; i++) {
        target = Activator.CreateInstance(_delegateTypes[i], env, method);
        Delegate d = Delegate.CreateDelegate(type, target, _delegateTypes[i].GetMethod("Do"),
                                             false);
        if (d != null) {
          return d;
        }
      }

      Type temp = _createDelegateType(type.GetMethod("Invoke"));
      _delegateTypes.Add(temp);

      target = Activator.CreateInstance(temp, env, method);
      return Delegate.CreateDelegate(type, target, temp.GetMethod("Do"));
    }

    Type _createDelegateType(MethodInfo delegateMethod) {
      var args = delegateMethod.GetParameters();

      TypeBuilder tb = NetHelpers.DefineGlobalType("$DelegateHelper");
      FieldBuilder meth = tb.DefineField("meth", typeof(ILuaValue), FieldAttributes.Private);
      FieldBuilder env = tb.DefineField("E", typeof(ILuaEnvironment), FieldAttributes.Private);
      MethodBuilder mb = NetHelpers.CloneMethod(tb, "Do", delegateMethod);
      ILGenerator gen = mb.GetILGenerator();

      // loc = new object[{args.Length}];
      LocalBuilder loc = gen.CreateArray(typeof(object), args.Length);

      for (int i = 0; i < args.Length; i++) {
        // loc[i] = arg_{i+1};
        gen.Emit(OpCodes.Ldloc, loc);
        gen.Emit(OpCodes.Ldc_I4, i);
        gen.Emit(OpCodes.Ldarg, i + 1);
        if (args[i].ParameterType.IsValueType) {
          gen.Emit(OpCodes.Box, args[i].ParameterType);
        }

        gen.Emit(OpCodes.Stelem, typeof(object));
      }

      // ILuaMultiValue methodArgs = E.Runtime.CreateMultiValueFromObj(loc);
      LocalBuilder methodArgs = gen.DeclareLocal(typeof(ILuaMultiValue));
      gen.Emit(OpCodes.Ldfld, env);
      gen.Emit(OpCodes.Callvirt,
               typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
      gen.Emit(OpCodes.Ldloc, loc);
      gen.Emit(OpCodes.Callvirt,
               typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateMultiValueFromObj)));
      gen.Emit(OpCodes.Stloc, methodArgs);

      // ret = this.meth.Invoke(LuaNil.Nil, false, methodArgs)
      LocalBuilder ret = gen.DeclareLocal(typeof(ILuaMultiValue));
      gen.Emit(OpCodes.Ldarg_0);
      gen.Emit(OpCodes.Ldfld, meth);
      gen.Emit(OpCodes.Ldnull);
      gen.Emit(
          OpCodes.Ldfld,
          typeof(LuaNil).GetField(nameof(LuaNil.Nil), BindingFlags.Static | BindingFlags.Public));
      gen.Emit(OpCodes.Ldc_I4_0);
      gen.Emit(OpCodes.Ldloc, methodArgs);
      gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.Invoke)));
      gen.Emit(OpCodes.Stloc, ret);

      // Store any by-ref parameters in the arguments
      for (int i = 0; i < args.Length; i++) {
        if (args[i].IsOut) {
          // arg_{i+1} = methodArgs[{i}].As<T>();
          gen.Emit(OpCodes.Ldloc, methodArgs);
          gen.Emit(OpCodes.Ldc_I4, i);
          gen.Emit(OpCodes.Callvirt, typeof(ILuaMultiValue).GetMethod("get_Item"));
          gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.As))
                                                      .MakeGenericMethod(args[i].ParameterType));
          gen.Emit(OpCodes.Starg, i + 1);
        }
      }

      // return ret.As<{info.Return}>();
      if (delegateMethod.ReturnType != null && delegateMethod.ReturnType != typeof(void)) {
        gen.Emit(OpCodes.Ldloc, ret);
        gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.As))
                                                    .MakeGenericMethod(delegateMethod.ReturnType));
      }
      gen.Emit(OpCodes.Ret);

      //// public <>_type_(ILuaEnvironment E, ILuaValue method)
      ConstructorBuilder cb = tb.DefineConstructor(
          MethodAttributes.Public, CallingConventions.Standard,
          new[] { typeof(ILuaEnvironment), typeof(ILuaValue) });
      gen = cb.GetILGenerator();

      // base();
      gen.Emit(OpCodes.Ldarg_0);
      gen.Emit(OpCodes.Call, typeof(object).GetConstructor(new Type[0]));

      // this.E = E;
      gen.Emit(OpCodes.Ldarg_0);
      gen.Emit(OpCodes.Ldarg_1);
      gen.Emit(OpCodes.Stfld, env);

      // this.meth = method;
      gen.Emit(OpCodes.Ldarg_0);
      gen.Emit(OpCodes.Ldarg_2);
      gen.Emit(OpCodes.Stfld, meth);
      gen.Emit(OpCodes.Ret);

      return tb.CreateType();
    }
  }
}
