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
using System.Collections;
using System.Reflection.Emit;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;
using ModMaker.Lua.Runtime;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Compiler
{
    /// <summary>
    /// Defines a visitor object that helps compile code with CodeCompiler.
    /// </summary>
    sealed class CompilerVisitor : IParseItemVisitor
    {
        ChunkBuilder compiler;

        /// <summary>
        /// A Helper used for compiling function call.  This is used to fake the
        /// prefix in an indexer item.  This simply reads from the prefix local.
        /// </summary>
        sealed class IndexerHelper : IParseExp
        {
            ILGenerator gen;
            LocalBuilder prefix;

            public IndexerHelper(ILGenerator gen, LocalBuilder prefix)
            {
                this.gen = gen;
                this.prefix = prefix;
            }

            public Token Debug { get; set; }
            public object UserData { get; set; }

            public IParseItem Accept(IParseItemVisitor visitor)
            {
                gen.Emit(OpCodes.Ldloc, prefix);
                return this;
            }
        }

        /// <summary>
        /// Creates a new instance of CompilerVisitor.
        /// </summary>
        /// <param name="compiler">The creating object used to help generate code.</param>
        public CompilerVisitor(ChunkBuilder compiler)
        {
            this.compiler = compiler;
        }

        /// <summary>
        /// Called when the item is a binary expression item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Accept.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(BinOpItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ILGenerator gen = compiler.CurrentGenerator;

            if (target.OperationType == BinaryOperationType.And ||
                target.OperationType == BinaryOperationType.Or)
            {
                // object temp = {Lhs};
                var end = gen.DefineLabel();
                var temp = compiler.CreateTemporary(typeof(ILuaValue));
                target.Lhs.Accept(this);
                gen.Emit(OpCodes.Stloc, temp);

                // Push Lhs onto the stack, if going to end, this will be the result.
                gen.Emit(OpCodes.Ldloc, temp);

                // if (temp.IsTrue) goto end;
                gen.Emit(OpCodes.Ldloc, temp);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetProperty(nameof(ILuaValue.IsTrue)).GetGetMethod());
                if (target.OperationType == BinaryOperationType.And)
                {
                    // We want to break if the value is truthy and it's an OR,
                    // or it's falsy and it's an AND.

                    // Boolean negation.
                    gen.Emit(OpCodes.Ldc_I4_1);
                    gen.Emit(OpCodes.Xor);
                }
                gen.Emit(OpCodes.Brtrue, end);

                // Replace Lhs on stack with Rhs.
                gen.Emit(OpCodes.Pop);
                target.Rhs.Accept(this);

                // :end
                gen.MarkLabel(end);
            }
            else
            {
                //! push {Lhs}.Arithmetic({OperationType}, {Rhs})
                target.Lhs.Accept(this);
                gen.Emit(OpCodes.Ldc_I4, (int)target.OperationType);
                target.Rhs.Accept(this);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.Arithmetic)));
            }

            return target;
        }
        /// <summary>
        /// Called when the item is a block item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Accept.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(BlockItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            using (compiler.LocalBlock())
            {
                foreach (IParseItem child in target.Children)
                    child.Accept(this);

                if (target.Return != null)
                {
                    // return {Return};
                    target.Return.Accept(this);
                    compiler.CurrentGenerator.Emit(OpCodes.Ret);
                }
            }

            return target;
        }
        /// <summary>
        /// Called when the item is a class definition item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Accept.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(ClassDefItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ILGenerator gen = compiler.CurrentGenerator;

            // string[] loc = new string[{implements.Count}];
            LocalBuilder loc = compiler.CreateArray(typeof(string), target.Implements.Count);

            int i = 0;
            foreach (var item in target.Implements)
            {
                // loc[{i}] = {implements[i]};
                gen.Emit(OpCodes.Ldloc, loc);
                gen.Emit(OpCodes.Ldc_I4, (i++));
                gen.Emit(OpCodes.Ldstr, item);
                gen.Emit(OpCodes.Stelem, typeof(string));
            }

            // E.Runtime.CreateClassValue(loc, {name});
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Ldstr, target.Name);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateClassValue)));
            compiler.RemoveTemporary(loc);

            return target;
        }
        /// <summary>
        /// Called when the item is a for generic item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Accept.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(ForGenItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ILGenerator gen = compiler.CurrentGenerator;
            target.Break.UserData = target.Break.UserData ?? gen.DefineLabel();
            Label start = gen.DefineLabel(), end = (Label)target.Break.UserData;
            LocalBuilder ret = compiler.CreateTemporary(typeof(ILuaMultiValue));
            LocalBuilder enumerable = compiler.CreateTemporary(typeof(IEnumerable<ILuaMultiValue>));
            LocalBuilder enumerator = compiler.CreateTemporary(typeof(IEnumerator<ILuaMultiValue>));

            using (compiler.LocalBlock())
            {
                // temp = new ILuaValue[...];
                var temp = compiler.CreateArray(typeof(ILuaValue), target.Expressions.Count);

                for (int i = 0; i < target.Expressions.Count; i++)
                {
                    // temp[{i}] = {item};
                    gen.Emit(OpCodes.Ldloc, temp);
                    gen.Emit(OpCodes.Ldc_I4, i);
                    target.Expressions[i].Accept(this);
                    gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
                }

                // enumerable = E.Runtime.GenericLoop(E, new LuaMultiValue(temp));
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
                gen.Emit(OpCodes.Ldloc, temp);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateMultiValue)));
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.GenericLoop)));
                gen.Emit(OpCodes.Stloc, enumerable);
                compiler.RemoveTemporary(temp);

                // enumerator = enumerable.GetEnumerator();
                gen.Emit(OpCodes.Ldloc, enumerable);
                gen.Emit(OpCodes.Callvirt, typeof(IEnumerable<ILuaMultiValue>).GetMethod(nameof(IEnumerable.GetEnumerator)));
                gen.Emit(OpCodes.Stloc, enumerator);

                // try {
                Label endTry = gen.BeginExceptionBlock();
                gen.MarkLabel(start);

                // if (!enumerator.MoveNext) goto end;
                gen.Emit(OpCodes.Ldloc, enumerator);
                gen.Emit(OpCodes.Callvirt, typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext)));
                gen.Emit(OpCodes.Brfalse, end);

                // ILuaMultiValue ret = enumerator.Current;
                gen.Emit(OpCodes.Ldloc, enumerator);
                gen.Emit(OpCodes.Callvirt, typeof(IEnumerator<ILuaMultiValue>)
                                               .GetProperty(nameof(IEnumerator<ILuaMultiValue>.Current)).GetGetMethod());
                gen.Emit(OpCodes.Stloc, ret);
                compiler.RemoveTemporary(enumerator);

                for (int i = 0; i < target.Names.Count; i++)
                {
                    // {_names[i]} = ret[{i}];
                    var field = compiler.DefineLocal(target.Names[i]);
                    field.StartSet();
                    gen.Emit(OpCodes.Ldloc, ret);
                    gen.Emit(OpCodes.Ldc_I4, i);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaMultiValue).GetMethod("get_Item"));
                    field.EndSet();
                }
                compiler.RemoveTemporary(ret);

                // {Block}
                target.Block.Accept(this);

                // goto start;
                gen.Emit(OpCodes.Br, start);

                // end:
                gen.MarkLabel(end);

                // } finally {
                gen.Emit(OpCodes.Leave, endTry);
                gen.BeginFinallyBlock();

                // if (enumerable != null) enumerable.Dispose();
                Label endFinally = gen.DefineLabel();
                gen.Emit(OpCodes.Ldloc, enumerable);
                gen.Emit(OpCodes.Brfalse, endFinally);
                gen.Emit(OpCodes.Ldloc, enumerable);
                gen.Emit(OpCodes.Callvirt, typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose)));
                gen.MarkLabel(endFinally);
                compiler.RemoveTemporary(enumerable);

                // }
                gen.EndExceptionBlock();
            }

            return target;
        }
        /// <summary>
        /// Called when the item is a for numerical item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Accept.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(ForNumItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ILGenerator gen = compiler.CurrentGenerator;
            target.Break.UserData = target.Break.UserData ?? gen.DefineLabel();
            Label start = gen.DefineLabel();
            Label end = (Label)target.Break.UserData;
            Label sj = gen.DefineLabel(), err = gen.DefineLabel();
            LocalBuilder d = compiler.CreateTemporary(typeof(double?));
            LocalBuilder val = compiler.CreateTemporary(typeof(double));
            LocalBuilder step = compiler.CreateTemporary(typeof(double));
            LocalBuilder limit = compiler.CreateTemporary(typeof(double));

            using (compiler.LocalBlock())
            {
                // d = {Start}.AsDouble();
                target.Start.Accept(this);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.AsDouble)));
                gen.Emit(OpCodes.Stloc, d);

                // if (d.HasValue) goto sj;
                gen.Emit(OpCodes.Ldloca, d);
                gen.Emit(OpCodes.Callvirt, typeof(double?).GetProperty(nameof(Nullable<double>.HasValue)).GetGetMethod());
                gen.Emit(OpCodes.Brtrue, sj);

                // err:
                gen.MarkLabel(err);

                // throw new InvalidOperationException("The Start, Limit, and Step of a for loop must result in numbers.");
                gen.Emit(OpCodes.Ldstr, Resources.LoopMustBeNumbers);
                gen.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(new Type[] { typeof(string) }));
                gen.Emit(OpCodes.Throw);

                // sj:
                gen.MarkLabel(sj);

                // val = d.Value;
                gen.Emit(OpCodes.Ldloca, d);
                gen.Emit(OpCodes.Callvirt, typeof(double?).GetProperty(nameof(Nullable<double>.Value)).GetGetMethod());
                gen.Emit(OpCodes.Stloc, val);

                if (target.Step != null)
                {
                    // d = {Step}.AsDouble();
                    target.Step.Accept(this);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.AsDouble)));
                    gen.Emit(OpCodes.Stloc, d);

                    // if (!d.HasValue) goto err;
                    gen.Emit(OpCodes.Ldloca, d);
                    gen.Emit(OpCodes.Callvirt, typeof(double?).GetProperty(nameof(Nullable<double>.HasValue)).GetGetMethod());
                    gen.Emit(OpCodes.Brfalse, err);

                    // step = d.Value;
                    gen.Emit(OpCodes.Ldloca, d);
                    gen.Emit(OpCodes.Callvirt, typeof(double?).GetProperty(nameof(Nullable<double>.Value)).GetGetMethod());
                }
                else
                {
                    // step = 1.0;
                    gen.Emit(OpCodes.Ldc_R8, 1.0);
                }
                gen.Emit(OpCodes.Stloc, step);

                // d = {Limit}.AsDouble();
                target.Limit.Accept(this);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.AsDouble)));
                gen.Emit(OpCodes.Stloc, d);

                // if (!d.HasValue) goto err;
                gen.Emit(OpCodes.Ldloca, d);
                gen.Emit(OpCodes.Callvirt, typeof(double?).GetProperty(nameof(Nullable<double>.HasValue)).GetGetMethod());
                gen.Emit(OpCodes.Brfalse, err);

                // limit = d.Value;
                gen.Emit(OpCodes.Ldloca, d);
                gen.Emit(OpCodes.Callvirt, typeof(double?).GetProperty(nameof(Nullable<double>.Value)).GetGetMethod());
                gen.Emit(OpCodes.Stloc, limit);
                compiler.RemoveTemporary(d);

                // start:
                gen.MarkLabel(start);

                // if (!((step > 0) & (val <= limit)) | ((step <= 0) & (val >= limit))) goto end;
                gen.Emit(OpCodes.Ldloc, step);
                gen.Emit(OpCodes.Ldc_R8, 0.0);
                gen.Emit(OpCodes.Cgt);
                gen.Emit(OpCodes.Ldloc, val);
                gen.Emit(OpCodes.Ldloc, limit);
                gen.Emit(OpCodes.Cgt);
                gen.Emit(OpCodes.Ldc_I4_1);
                gen.Emit(OpCodes.Xor);
                gen.Emit(OpCodes.And);
                gen.Emit(OpCodes.Ldloc, step);
                gen.Emit(OpCodes.Ldc_R8, 0.0);
                gen.Emit(OpCodes.Cgt);
                gen.Emit(OpCodes.Ldc_I4_1);
                gen.Emit(OpCodes.Xor);
                gen.Emit(OpCodes.Ldloc, val);
                gen.Emit(OpCodes.Ldloc, limit);
                gen.Emit(OpCodes.Clt);
                gen.Emit(OpCodes.Ldc_I4_1);
                gen.Emit(OpCodes.Xor);
                gen.Emit(OpCodes.And);
                gen.Emit(OpCodes.Or);
                gen.Emit(OpCodes.Brfalse, end);

                // {name} = E.Runtime.CreateValue((object)val);
                var field = compiler.DefineLocal(target.Name);
                field.StartSet();
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
                gen.Emit(OpCodes.Ldloc, val);
                gen.Emit(OpCodes.Box, typeof(double));
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateValue)));
                field.EndSet();

                // {Block}
                target.Block.Accept(this);

                // val += step;
                gen.Emit(OpCodes.Ldloc, val);
                gen.Emit(OpCodes.Ldloc, step);
                gen.Emit(OpCodes.Add);
                gen.Emit(OpCodes.Stloc, val);
                compiler.RemoveTemporary(val);
                compiler.RemoveTemporary(step);
                compiler.RemoveTemporary(limit);

                // goto start;
                gen.Emit(OpCodes.Br, start);

                // end:
                gen.MarkLabel(end);
            }
            return target;
        }
        /// <summary>
        /// Called when the item is a function call item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(FuncCallItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            //// load the args into an array.
            ILGenerator gen = compiler.CurrentGenerator;
            LocalBuilder f = compiler.CreateTemporary(typeof(ILuaValue));
            LocalBuilder self = compiler.CreateTemporary(typeof(object));

            /* add 'self' if instance call */
            if (target.InstanceName != null)
            {
                // self = {Prefix};
                target.Prefix.Accept(this);
                gen.Emit(OpCodes.Stloc, self);

                // f = self.GetIndex(temp);
                gen.Emit(OpCodes.Ldloc, self);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
                gen.Emit(OpCodes.Ldstr, target.InstanceName);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateValue)));
                gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.GetIndex)));
                gen.Emit(OpCodes.Stloc, f);
            }
            else if (target.Prefix is IndexerItem)
            {
                // self = {Prefix};
                IndexerItem item = (IndexerItem)target.Prefix;
                item.Prefix.Accept(this);
                gen.Emit(OpCodes.Stloc, self);

                // Store the old value to restore later, add a dummy.
                var tempPrefix = item.Prefix;
                item.Prefix = new IndexerHelper(gen, self);

                // f = {Prefix};
                target.Prefix.Accept(this);
                gen.Emit(OpCodes.Stloc, f);

                // Restore the old value
                item.Prefix = tempPrefix;
            }
            else
            {
                // self = LuaNil.Nil;
                gen.Emit(OpCodes.Ldnull);
                gen.Emit(OpCodes.Ldfld, typeof(LuaNil).GetField(nameof(LuaNil.Nil), BindingFlags.Static | BindingFlags.Public));
                gen.Emit(OpCodes.Stloc, self);

                // f = {Prefix};
                target.Prefix.Accept(this);
                gen.Emit(OpCodes.Stloc, f);
            }

            // var args = new ILuaValue[...];
            LocalBuilder args = compiler.CreateArray(typeof(ILuaValue), target.Arguments.Count);
            for (int i = 0; i < target.Arguments.Count; i++)
            {
                // args[i] = {item};
                gen.Emit(OpCodes.Ldloc, args);
                gen.Emit(OpCodes.Ldc_I4, i);
                target.Arguments[i].Expression.Accept(this);
                if (i + 1 == target.Arguments.Count && target.IsLastArgSingle)
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.Single)));
                gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
            }

            // var rargs = E.Runtime.CreateMultiValue(args);
            var rargs = compiler.CreateTemporary(typeof(ILuaMultiValue));
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
            gen.Emit(OpCodes.Ldloc, args);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateMultiValue)));
            gen.Emit(OpCodes.Stloc, rargs);
            compiler.RemoveTemporary(args);

            //! push f.Invoke(self, {!!InstanceName}, {Overload}, rargs);
            gen.Emit(OpCodes.Ldloc, f);
            gen.Emit(OpCodes.Ldloc, self);
            gen.Emit(target.InstanceName != null ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Ldc_I4, target.Overload);
            gen.Emit(OpCodes.Ldloc, rargs);
            if (target.IsTailCall)
                gen.Emit(OpCodes.Tailcall);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.Invoke)));
            compiler.RemoveTemporary(f);
            compiler.RemoveTemporary(self);

            //! pop
            if (target.Statement)
                gen.Emit(OpCodes.Pop);

            // support byRef
            for (int i = 0; i < target.Arguments.Count; i++)
            {
                if (target.Arguments[i].IsByRef)
                {
                    AssignValue(target.Arguments[i].Expression, false, null,
                        () =>
                        {
                            // $value = rargs[{i}];
                            gen.Emit(OpCodes.Ldloc, rargs);
                            gen.Emit(OpCodes.Ldc_I4, i);
                            gen.Emit(OpCodes.Callvirt, typeof(ILuaMultiValue).GetMethod("get_Item"));
                        });
                }
            }
            compiler.RemoveTemporary(rargs);

            return target;
        }
        /// <summary>
        /// Called when the item is a function definition item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(FuncDefItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var gen = compiler.CurrentGenerator;
            ChunkBuilder.VarDefinition field = null;
            string name = null;
            bool store = false;

            if (target.Local)
            {
                // local function definition.
                if (target.InstanceName != null)
                    throw new SyntaxException(Resources.InstanceLocalMethod, target.Debug);
                if (!(target.Prefix is NameItem))
                    throw new SyntaxException(Resources.IndexerLocalMethod, target.Debug);

                NameItem namei = (NameItem)target.Prefix;
                name = namei.Name;
                field = compiler.DefineLocal(namei);
                field.StartSet();
            }
            else if (target.Prefix != null)
            {
                if (target.InstanceName != null)
                {
                    // instance function definition.
                    name = null;
                    if (target.Prefix is NameItem)
                        name = ((NameItem)target.Prefix).Name;
                    else
                        name = (string)((LiteralItem)((IndexerItem)target.Prefix).Expression).Value;
                    name += ":" + target.InstanceName;

                    // {Prefix}.SetIndex({InstanceName}, {ImplementFunction(..)})
                    target.Prefix.Accept(this);
                    gen.Emit(OpCodes.Ldarg_1);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
                    gen.Emit(OpCodes.Ldstr, target.InstanceName);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateValue)));
                    store = true;
                }
                else if (target.Prefix is IndexerItem)
                {
                    // global function definition with indexer
                    // {Prefix}.SetIndex({Expression}, {ImplementFunction(..)})
                    IndexerItem index = (IndexerItem)target.Prefix;
                    name = (string)((LiteralItem)index.Expression).Value;
                    index.Prefix.Accept(this);
                    index.Expression.Accept(this);
                    store = true;
                }
                else
                {
                    // global function definition with name
                    name = ((NameItem)target.Prefix).Name;
                    field = compiler.FindVariable((NameItem)target.Prefix);
                    field.StartSet();
                }
            }

            compiler.ImplementFunction(this, target, name);

            if (field != null)
                field.EndSet();
            else if (store)
                gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.SetIndex)));

            return target;
        }
        /// <summary>
        /// Called when the item is a goto item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(GotoItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (target.Target == null)
                throw new InvalidOperationException(Resources.ErrorResolveLabel);
            compiler.CurrentGenerator.Emit(OpCodes.Br, (Label)target.Target.UserData);

            return target;
        }
        /// <summary>
        /// Called when the item is an if item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(IfItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ILGenerator gen = compiler.CurrentGenerator;
            Label next = gen.DefineLabel(), end = gen.DefineLabel();

            // if (!{Exp}.IsTrue) goto next;
            target.Expression.Accept(this);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetProperty(nameof(ILuaValue.IsTrue)).GetGetMethod());
            gen.Emit(OpCodes.Brfalse, next);

            // {Block}
            target.Block.Accept(this);

            // goto end;
            gen.Emit(OpCodes.Br, end);

            // next:
            gen.MarkLabel(next);
            foreach (var item in target.Elses)
            {
                // if (!{item.Item1}.IsTrue) goto next;
                next = gen.DefineLabel();
                item.Expression.Accept(this);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetProperty(nameof(ILuaValue.IsTrue)).GetGetMethod());
                gen.Emit(OpCodes.Brfalse, next);

                // {item.Item2}
                item.Block.Accept(this);

                // goto end;
                gen.Emit(OpCodes.Br, end);

                // next:
                gen.MarkLabel(next);
            }
            if (target.ElseBlock != null)
                target.ElseBlock.Accept(this);

            // end:
            gen.MarkLabel(end);

            return target;
        }
        /// <summary>
        /// Called when the item is an indexer item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(IndexerItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            //! push {Prefix}.GetIndex({Expression})
            var gen = compiler.CurrentGenerator;
            target.Prefix.Accept(this);
            target.Expression.Accept(this);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.GetIndex)));

            return target;
        }
        /// <summary>
        /// Called when the item is a label item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(LabelItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.UserData = target.UserData ?? compiler.CurrentGenerator.DefineLabel();
            compiler.CurrentGenerator.MarkLabel((Label)target.UserData);

            return target;
        }
        /// <summary>
        /// Called when the item is a literal item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(LiteralItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ILGenerator gen = compiler.CurrentGenerator;
            object Value = target.Value;

            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
            if (Value == null)
            {
                gen.Emit(OpCodes.Ldnull);
            }
            else if (Value is bool)
            {
                if ((bool)Value == false)
                    gen.Emit(OpCodes.Ldc_I4_0);
                else
                    gen.Emit(OpCodes.Ldc_I4_1);
                gen.Emit(OpCodes.Box, typeof(bool));
            }
            else if (Value is double)
            {
                gen.Emit(OpCodes.Ldc_R8, (double)Value);
                gen.Emit(OpCodes.Box, typeof(double));
            }
            else if (Value is string)
            {
                gen.Emit(OpCodes.Ldstr, Value as string);
            }
            else
                throw new InvalidOperationException(Resources.InvalidLiteralType);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateValue)));

            return target;
        }
        /// <summary>
        /// Called when the item is a name item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(NameItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            // get the value of the given name and push onto stack.
            var field = compiler.FindVariable(target);
            field.Get();

            return target;
        }
        /// <summary>
        /// Called when the item is a repeat item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(RepeatItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ILGenerator gen = compiler.CurrentGenerator;
            target.Break.UserData = target.Break.UserData ?? gen.DefineLabel();
            Label start = gen.DefineLabel();
            Label end = (Label)target.Break.UserData;

            // start:
            gen.MarkLabel(start);

            // {Block}
            target.Block.Accept(this);

            // if (!{Exp}.IsTrue) goto start;
            target.Expression.Accept(this);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetProperty(nameof(ILuaValue.IsTrue)).GetGetMethod());
            gen.Emit(OpCodes.Brfalse, start);

            // end:
            gen.MarkLabel(end);

            return target;
        }
        /// <summary>
        /// Called when the item is a return item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(ReturnItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ILGenerator gen = compiler.CurrentGenerator;
            if (target.Expressions.Count == 1 && target.Expressions[0] is FuncCallItem &&
                !target.IsLastExpressionSingle)
            {
                ((FuncCallItem)target.Expressions[0]).IsTailCall = true;
                target.Expressions[0].Accept(this);
                return target;
            }

            // ILuaValue[] loc = new ILuaValue[{Expressions.Count}];
            LocalBuilder loc = compiler.CreateArray(typeof(ILuaValue), target.Expressions.Count);

            for (int i = 0; i < target.Expressions.Count; i++)
            {
                // loc[{i}] = {Expressions[i]};
                gen.Emit(OpCodes.Ldloc, loc);
                gen.Emit(OpCodes.Ldc_I4, i);
                target.Expressions[i].Accept(this);
                if (i + 1 == target.Expressions.Count && target.IsLastExpressionSingle)
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.Single)));
                gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
            }

            //! push E.Runtime.CreateMultiValue(loc)
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateMultiValue)));
            compiler.RemoveTemporary(loc);

            return target;
        }
        /// <summary>
        /// Called when the item is a table item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(TableItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var gen = compiler.CurrentGenerator;
            var loc = compiler.CreateTemporary(typeof(ILuaValue));

            // loc = E.Runtime.CreateTable();
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateTable)));
            gen.Emit(OpCodes.Stloc, loc);

            foreach (var item in target.Fields)
            {
                // Does not need to use SetItemRaw because there is no Metatable.
                // loc.SetIndex({item.Item1}, {item.Item2});
                gen.Emit(OpCodes.Ldloc, loc);
                item.Key.Accept(this);
                item.Value.Accept(this);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.SetIndex)));
            }

            //! push loc;
            gen.Emit(OpCodes.Ldloc, loc);
            compiler.RemoveTemporary(loc);

            return target;
        }
        /// <summary>
        /// Called when the item is a unary operation item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(UnOpItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ILGenerator gen = compiler.CurrentGenerator;

            //! push {Target}.Minus();
            target.Target.Accept(this);
            switch (target.OperationType)
            {
                case UnaryOperationType.Minus:
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.Minus)));
                    break;
                case UnaryOperationType.Not:
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.Not)));
                    break;
                case UnaryOperationType.Length:
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.Length)));
                    break;
            }

            return target;
        }
        /// <summary>
        /// Called when the item is an assignment item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(AssignmentItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ILGenerator gen = compiler.CurrentGenerator;

            // ILuaValue[] loc = new ILuaValue[{target.Expressions.Count}];
            LocalBuilder loc = compiler.CreateArray(typeof(ILuaValue), target.Expressions.Count);
            // ILuaValue[] names = new ILuaValue[{target.Names.Count}];
            LocalBuilder names = compiler.CreateArray(typeof(ILuaValue), target.Names.Count);

            // have to evaluate the name indexer expressions before
            //   setting the values otherwise the following will fail:
            // i, t[i] = i+1, 20
            for (int i = 0; i < target.Names.Count; i++)
            {
                if (target.Names[i] is IndexerItem)
                {
                    IndexerItem item = (IndexerItem)target.Names[i];
                    gen.Emit(OpCodes.Ldloc, names);
                    gen.Emit(OpCodes.Ldc_I4, i);
                    item.Expression.Accept(this);
                    gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
                }
            }

            for (int i = 0; i < target.Expressions.Count; i++)
            {
                // loc[{i}] = {exps[i]};
                gen.Emit(OpCodes.Ldloc, loc);
                gen.Emit(OpCodes.Ldc_I4, i);
                target.Expressions[i].Accept(this);
                if (i + 1 == target.Expressions.Count && target.IsLastExpressionSingle)
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.Single)));
                gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
            }

            // ILuaMultiValue exp = E.Runtime.CreateMultiValue(loc);
            LocalBuilder exp = compiler.CreateTemporary(typeof(ILuaMultiValue));
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetProperty(nameof(ILuaEnvironment.Runtime)).GetGetMethod());
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod(nameof(ILuaRuntime.CreateMultiValue)));
            gen.Emit(OpCodes.Stloc, exp);
            compiler.RemoveTemporary(loc);

            for (int i = 0; i < target.Names.Count; i++)
            {
                AssignValue(target.Names[i], target.Local,
                    !(target.Names[i] is IndexerItem) ? (Action)null : () =>
                    {
                        // only called if the target object is an indexer item.

                        // $index = names[{i}];
                        gen.Emit(OpCodes.Ldloc, names);
                        gen.Emit(OpCodes.Ldc_I4, i);
                        gen.Emit(OpCodes.Ldelem, typeof(ILuaValue));
                    },
                    () =>
                    {
                        // $value = exp[{i}];
                        gen.Emit(OpCodes.Ldloc, exp);
                        gen.Emit(OpCodes.Ldc_I4, i);
                        gen.Emit(OpCodes.Callvirt, typeof(ILuaMultiValue).GetMethod("get_Item"));
                    });
            }
            compiler.RemoveTemporary(exp);
            compiler.RemoveTemporary(names);

            return target;
        }
        /// <summary>
        /// Called when the item is a while item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(WhileItem target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ILGenerator gen = compiler.CurrentGenerator;
            target.Break.UserData = target.Break.UserData ?? gen.DefineLabel();
            Label start = gen.DefineLabel();
            Label end = (Label)target.Break.UserData;

            // start:
            gen.MarkLabel(start);

            // if (!{Exp}.IsTrue) goto end;
            target.Expression.Accept(this);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetProperty(nameof(ILuaValue.IsTrue)).GetGetMethod());
            gen.Emit(OpCodes.Brfalse, end);

            // {Block}
            target.Block.Accept(this);

            // goto start;
            gen.Emit(OpCodes.Br, start);

            // end:
            gen.MarkLabel(end);

            return target;
        }

        /// <summary>
        /// Assigns the values of the parse item to the given value.
        /// </summary>
        /// <param name="target">The item to assign the value to (e.g. NameItem).</param>
        /// <param name="local">Whether this is a local definition.</param>
        /// <param name="getIndex">A function to get the index of the object,
        /// pass null to use the default.</param>
        /// <param name="getValue">A function to get the value to set to.</param>
        void AssignValue(IParseItem target, bool local, Action getIndex, Action getValue)
        {
            ILGenerator gen = compiler.CurrentGenerator;
            ChunkBuilder.VarDefinition field;
            if (local)
            {
                field = compiler.DefineLocal((NameItem)target);
            }
            else if (target is IndexerItem)
            {
                IndexerItem name = (IndexerItem)target;
                // {name.Prefix}.SetIndex({name.Expression}, value);
                name.Prefix.Accept(this);
                if (getIndex != null)
                    getIndex();
                else
                    name.Expression.Accept(this);
                getValue();
                gen.Emit(OpCodes.Callvirt, typeof(ILuaValue).GetMethod(nameof(ILuaValue.SetIndex)));
                return;
            }
            else // names[i] is NameItem
            {
                NameItem item = (NameItem)target;
                field = compiler.FindVariable(item);
            }

            // envField = value;
            field.StartSet();
            getValue();
            field.EndSet();
        }
    }
}
