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
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using System.Security.Permissions;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Compiler
{
    /// <summary>
    /// Defines an ICodeCompiler to compile the IParseItem tree into IL code.
    /// </summary>
    [LuaIgnore]
    public sealed class CodeCompiler : ICodeCompiler
    {
        List<string> _types = new List<string>();
        List<Type> _delegateTypes = new List<Type>();
        AssemblyBuilder _ab;
        ModuleBuilder _mb;
        int _tid = 1;

        /// <summary>
        /// Creates a new CodeCompiler object.
        /// </summary>
        public CodeCompiler()
        {
            // create a new dynamic assembly to store the Lua code
            _ab = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("DynamicAssembly"), AssemblyBuilderAccess.RunAndSave);
            _mb = _ab.DefineDynamicModule("DynamicAssembly.dll");
        }

        /// <summary>
        /// Saves the compiled code to disk.
        /// </summary>
        /// <param name="name">The name/path to save to.</param>
        /// <exception cref="System.ArgumentException">If the name is not a
        /// valid file path.</exception>
        /// <exception cref="System.ArgumentNullException">If name is null or 
        /// empty.</exception>
        /// <exception cref="System.NotSupportedException">If the implementation
        /// does not support saving to disk.</exception>
        /// <exception cref="System.UnauthorizedAccessException">If the code does
        /// not have sufficient permissions to save to disk.</exception>
        [SecuritySafeCritical]
        public void Save(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                name += ".dll";
            _ab.Save("DynamicAssembly.dll");
            File.Move("DynamicAssembly.dll", name);
        }
        /// <summary>
        /// Saves the compiled code to disk, optionally overriting the file if 
        /// it exists.
        /// </summary>
        /// <param name="name">The name/path to save to.</param>
        /// <param name="doOverride">True to override the file if it exists, 
        /// otherwise false.</param>
        /// <exception cref="System.ArgumentException">If the name is not a
        /// valid file path.</exception>
        /// <exception cref="System.ArgumentNullException">If name is null or 
        /// empty.</exception>
        /// <exception cref="System.NotSupportedException">If the implementation
        /// does not support saving to disk.</exception>
        /// <exception cref="System.UnauthorizedAccessException">If the code does
        /// not have sufficient permissions to save to disk.</exception>
        [SecuritySafeCritical]
        public void Save(string name, bool doOverride)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            if (File.Exists(name) && doOverride)
                File.Delete(name);
            if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                name += ".dll";

            _ab.Save("DynamicAssembly.dll");
            File.Move("DynamicAssembly.dll", name);
        }

        /// <summary>
        /// Compiles an IParseItem tree indo an IModule object so that it can 
        /// be executed.
        /// </summary>
        /// <param name="name">The name to given the module, can be null to 
        /// auto-generate.</param>
        /// <param name="E">The current environment.</param>
        /// <param name="item">The item to compile.</param>
        /// <returns>A compiled version of the object.</returns>
        /// <exception cref="System.ArgumentNullException">If E or item is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is
        /// syntax errors in the item tree.</exception>
        public ILuaValue Compile(ILuaEnvironment E, IParseItem item, string name)
        {
            if (E == null)
                throw new ArgumentNullException(nameof(E));
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            // get the name
            name = name ?? "<>_func_" + (_tid++);
            if (_types.Contains(name))
            {
                int i = 0;
                while (_types.Contains(name + i))
                    i++;
                name += i;
            }

            // resolve labels
            GetInfoVisitor lVisitor = new GetInfoVisitor();
            lVisitor.Resolve(item);

            // create the type
            TypeBuilder tb = _mb.DefineType(name, TypeAttributes.Public | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed,
                typeof(LuaValueBase), Type.EmptyTypes);
            ChunkBuilder cb = new ChunkBuilder(tb, lVisitor.GlobalCaptures, lVisitor.GlobalNested);

            // compile the code
            CompilerVisitor cVisitor = new CompilerVisitor(cb);
            item.Accept(cVisitor);
            var ret = cb.CreateChunk(E);
            return ret;
        }
        /// <summary>
        /// Creates a delegate that can be called to call the given IMethod.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="type">The type of the delegate.</param>
        /// <param name="method">The method to call.</param>
        /// <returns>A delegate that is used to call the method.</returns>
        /// <exception cref="System.ArgumentException">If type is not a delegate
        /// type.</exception>
        /// <exception cref="System.ArgumentNullException">If any argument is null.</exception>
        /// <exception cref="System.NotSupportedException">If this implementation
        /// does not support created delegates.</exception>
        public Delegate CreateDelegate(ILuaEnvironment E, Type type, ILuaValue method)
        {
            if (E == null)
                throw new ArgumentNullException(nameof(E));
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            if (!typeof(Delegate).IsAssignableFrom(type.BaseType))
                throw new ArgumentException(Resources.DeriveFromDelegate);

            // search through the cache for a compatible delegate helper
            object target; // TODO: Make CreateDelegate more efficient.
            for (int i = 0; i < _delegateTypes.Count; i++)
            {
                target = Activator.CreateInstance(_delegateTypes[i], E, method);
                Delegate d = Delegate.CreateDelegate(type, target, _delegateTypes[i].GetMethod("Do"), false);
                if (d != null)
                    return d;
            }

            Type temp = CreateDelegateType(type.GetMethod("Invoke"));
            _delegateTypes.Add(temp);

            target = Activator.CreateInstance(temp, E, method);
            return Delegate.CreateDelegate(type, target, temp.GetMethod("Do"));
        }

        Type CreateDelegateType(MethodInfo delegateMethod)
        {
            var args = delegateMethod.GetParameters();

            TypeBuilder tb = NetHelpers.DefineGlobalType("$DelegateHelper");
            FieldBuilder meth = tb.DefineField("meth", typeof(ILuaValue), FieldAttributes.Private);
            FieldBuilder E = tb.DefineField("E", typeof(ILuaEnvironment), FieldAttributes.Private);
            MethodBuilder mb = NetHelpers.CloneMethod(tb, "Do", delegateMethod);
            ILGenerator gen = mb.GetILGenerator();

            // loc = new object[{args.Length}];
            LocalBuilder loc = gen.CreateArray(typeof(object), args.Length);

            for (int i = 0; i < args.Length; i++)
            {
                // loc[i] = arg_{i+1};
                gen.Emit(OpCodes.Ldloc, loc);
                gen.Emit(OpCodes.Ldc_I4, i);
                gen.Emit(OpCodes.Ldarg, i+1);
                if (args[i].ParameterType.IsValueType)
                    gen.Emit(OpCodes.Box, args[i].ParameterType);
                gen.Emit(OpCodes.Stelem, typeof(object));
            }

            // ILuaMultiValue methodArgs = E.Runtime.CreateMultiValueFromObj(loc);
            LocalBuilder methodArgs = gen.DeclareLocal(typeof(ILuaMultiValue));
            gen.Emit(OpCodes.Ldfld, E);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateMultiValueFromObj)));
            gen.Emit(OpCodes.Stloc, methodArgs);

            // ret = this.meth.Invoke(LuaNil.Nil, false, -1, methodArgs)
            LocalBuilder ret = gen.DeclareLocal(typeof(ILuaMultiValue));
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, meth);
            gen.Emit(OpCodes.Ldnull);
            gen.Emit(OpCodes.Ldfld, typeof(LuaNil).GetField(nameof(LuaNil.Nil), BindingFlags.Static | BindingFlags.Public));
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Ldc_I4_M1);
            gen.Emit(OpCodes.Ldloc, methodArgs);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.Invoke)));
            gen.Emit(OpCodes.Stloc, ret);

            // Store any by-ref parameters in the arguments
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].IsOut)
                {
                    // arg_{i+1} = methodArgs[{i}].As<T>();
                    gen.Emit(OpCodes.Ldloc, methodArgs);
                    gen.Emit(OpCodes.Ldc_I4, i);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaMultiValue).GetMethod("get_Item"));
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.As))
                                                                .MakeGenericMethod(args[i].ParameterType));
                    gen.Emit(OpCodes.Starg, i+1);
                }
            }

            // return ret.As<{info.Return}>();
            if (delegateMethod.ReturnType != null && delegateMethod.ReturnType != typeof(void))
            {
                gen.Emit(OpCodes.Ldloc, ret);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.As))
                                                            .MakeGenericMethod(delegateMethod.ReturnType));
            }
            gen.Emit(OpCodes.Ret);

            //// public <>_type_(ILuaEnvironment E, ILuaValue method)
            ConstructorBuilder cb = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                new[] { typeof(ILuaEnvironment), typeof(ILuaValue) });
            gen = cb.GetILGenerator();

            // base();
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, typeof(object).GetConstructor(new Type[0]));

            // this.E = E;
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, E);

            // this.meth = method;
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Stfld, meth);
            gen.Emit(OpCodes.Ret);

            return tb.CreateType();
        }
    }
}
