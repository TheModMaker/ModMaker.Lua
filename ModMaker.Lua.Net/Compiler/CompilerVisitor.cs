using System;
using System.Collections;
using System.Reflection.Emit;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;
using ModMaker.Lua.Runtime;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace ModMaker.Lua.Compiler
{
    /// <summary>
    /// Defines a visitor object that helps compile code with CodeCompiler.
    /// </summary>
    sealed class CompilerVisitor : IParseItemVisitor
    {
        ChunkBuilder compiler;

        /// <summary>
        /// Creates a new instance of CompilerVisitor.
        /// </summary>
        /// <param name="compiler">The creating object used to help generate code.</param>
        public CompilerVisitor(ChunkBuilder/*!*/ compiler)
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
                throw new ArgumentNullException("target");

            ILGenerator gen = compiler.CurrentGenerator;

            //! push E.Runtime.ResolveBinaryOperation({Lhs}, {OperationType}, {Rhs})
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            target.Lhs.Accept(this);
            gen.Emit(OpCodes.Ldc_I4, (int)target.OperationType);
            target.Rhs.Accept(this);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("ResolveBinaryOperation"));
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
                throw new ArgumentNullException("target");

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
                throw new ArgumentNullException("target");

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

            // E.Runtime.DefineClass(E, loc, {name});
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Ldstr, target.Name);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("DefineClass"));
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
                throw new ArgumentNullException("target");

            ILGenerator gen = compiler.CurrentGenerator;
            target.Break.UserData = target.Break.UserData ?? gen.DefineLabel();
            Label start = gen.DefineLabel(), end = (Label)target.Break.UserData;
            LocalBuilder ret = compiler.CreateTemporary(typeof(MultipleReturn));
            LocalBuilder enumerable = compiler.CreateTemporary(typeof(IEnumerable<MultipleReturn>));
            LocalBuilder enumerator = compiler.CreateTemporary(typeof(IEnumerator<MultipleReturn>));

            using (compiler.LocalBlock())
            {
                // temp = new object[...];
                var temp = compiler.CreateArray(typeof(object), target.Expressions.Count);

                for (int i = 0; i < target.Expressions.Count; i++)
                {
                    // temp[{i}] = {item};
                    gen.Emit(OpCodes.Ldloc, temp);
                    gen.Emit(OpCodes.Ldc_I4, i);
                    target.Expressions[i].Accept(this);
                    gen.Emit(OpCodes.Stelem, typeof(object));
                }

                // enumerable = E.Runtime.GenericLoop(E, temp);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldloc, temp);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("GenericLoop"));
                gen.Emit(OpCodes.Stloc, enumerable);
                compiler.RemoveTemporary(temp);

                // enumerator = enumerable.GetEnumerator();
                gen.Emit(OpCodes.Ldloc, enumerable);
                gen.Emit(OpCodes.Callvirt, typeof(IEnumerable<MultipleReturn>).GetMethod("GetEnumerator"));
                gen.Emit(OpCodes.Stloc, enumerator);

                // try {
                Label endTry = gen.BeginExceptionBlock();
                gen.MarkLabel(start);

                // if (!enumerator.MoveNext) goto end;
                gen.Emit(OpCodes.Ldloc, enumerator);
                gen.Emit(OpCodes.Callvirt, typeof(IEnumerator).GetMethod("MoveNext"));
                gen.Emit(OpCodes.Brfalse, end);

                // MultipleReturn ret = enumerator.Current;
                gen.Emit(OpCodes.Ldloc, enumerator);
                gen.Emit(OpCodes.Callvirt, typeof(IEnumerator<MultipleReturn>).GetMethod("get_Current"));
                gen.Emit(OpCodes.Stloc, ret);
                compiler.RemoveTemporary(enumerator);

                for (int i = 0; i < target.Names.Count; i++)
                {
                    // {_names[i]} = ret[{i}];
                    var field = compiler.DefineLocal(target.Names[i]);
                    field.StartSet();
                    gen.Emit(OpCodes.Ldloc, ret);
                    gen.Emit(OpCodes.Ldc_I4, i);
                    gen.Emit(OpCodes.Callvirt, typeof(MultipleReturn).GetMethod("get_Item"));
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
                gen.Emit(OpCodes.Callvirt, typeof(IDisposable).GetMethod("Dispose"));
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
                throw new ArgumentNullException("target");

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
                // d = E.Runtime.ToNumber({Start});
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                target.Start.Accept(this);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("ToNumber"));
                gen.Emit(OpCodes.Stloc, d);

                // if (d.HasValue) goto sj;
                gen.Emit(OpCodes.Ldloca, d);
                gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_HasValue"));
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
                gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_Value"));
                gen.Emit(OpCodes.Stloc, val);

                if (target.Step != null)
                {
                    // d = E.Runtime.ToNumber({Step});
                    gen.Emit(OpCodes.Ldarg_1);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                    target.Step.Accept(this);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("ToNumber"));
                    gen.Emit(OpCodes.Stloc, d);

                    // if (!d.HasValue) goto err;
                    gen.Emit(OpCodes.Ldloca, d);
                    gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_HasValue"));
                    gen.Emit(OpCodes.Brfalse, err);

                    // step = d.Value;
                    gen.Emit(OpCodes.Ldloca, d);
                    gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_Value"));
                }
                else
                {
                    // step = 1.0;
                    gen.Emit(OpCodes.Ldc_R8, 1.0);
                }
                gen.Emit(OpCodes.Stloc, step);

                // d = E.Runtime.ToNumber({Limit});
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                target.Limit.Accept(this);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("ToNumber"));
                gen.Emit(OpCodes.Stloc, d);

                // if (!d.HasValue) goto err;
                gen.Emit(OpCodes.Ldloca, d);
                gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_HasValue"));
                gen.Emit(OpCodes.Brfalse, err);

                // limit = d.Value;
                gen.Emit(OpCodes.Ldloca, d);
                gen.Emit(OpCodes.Callvirt, typeof(double?).GetMethod("get_Value"));
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

                // RuntimeHelper.SetValue(ref {name}, (object)val);
                var field = compiler.DefineLocal(target.Name);
                field.StartSet();
                gen.Emit(OpCodes.Ldloc, val);
                gen.Emit(OpCodes.Box, typeof(double));
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
                throw new ArgumentNullException("target");

            /* load the args into an array */
            ILGenerator gen = compiler.CurrentGenerator;
            LocalBuilder f = compiler.CreateTemporary(typeof(object));

            // args = new object[...];
            LocalBuilder args = compiler.CreateArray(typeof(object),
                target.Arguments.Count + (target.InstanceName == null ? 0 : 1));
            // byref = new int[...];
            LocalBuilder byref = compiler.CreateArray(typeof(int), 
                target.Arguments.Count(t => t.IsByRef));

            // only need to create args array if there are aruments
            if (target.Arguments.Count > 0 || target.InstanceName != null)
            {
                /* add 'self' if instance call */
                if (target.InstanceName != null)
                {
                    // f = {Prefix};
                    target.Prefix.Accept(this);
                    gen.Emit(OpCodes.Stloc, f);

                    // args[0] = f;
                    gen.Emit(OpCodes.Ldloc, args);
                    gen.Emit(OpCodes.Ldc_I4_0);
                    gen.Emit(OpCodes.Ldloc, f);
                    gen.Emit(OpCodes.Stelem, typeof(object));

                    // f = E.Runtime.GetIndex(E, f, {Instance});
                    gen.Emit(OpCodes.Ldarg_1);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                    gen.Emit(OpCodes.Ldarg_1);
                    gen.Emit(OpCodes.Ldloc, f);
                    gen.Emit(OpCodes.Ldstr, target.InstanceName);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("GetIndex"));
                    gen.Emit(OpCodes.Stloc, f);
                }
                else
                {
                    // f = {Prefix};
                    target.Prefix.Accept(this);
                    gen.Emit(OpCodes.Stloc, f);
                }

                int bi = 0;
                for (int i = 0; i < target.Arguments.Count; i++)
                {
                    // args[...] = {item};
                    gen.Emit(OpCodes.Ldloc, args);
                    gen.Emit(OpCodes.Ldc_I4, i + (target.InstanceName == null ? 0 : 1));
                    target.Arguments[i].Expression.Accept(this);
                    gen.Emit(OpCodes.Stelem, typeof(object));

                    // add value to byRef
                    if (target.Arguments[i].IsByRef)
                    {
                        // byRef[{bi}] = {i};
                        gen.Emit(OpCodes.Ldloc, byref);
                        gen.Emit(OpCodes.Ldc_I4, bi++);
                        gen.Emit(OpCodes.Ldc_I4, i);
                        gen.Emit(OpCodes.Stelem, typeof(int));
                    }
                }

                // args = E.Runtime.FixArgs(args);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                gen.Emit(OpCodes.Ldloc, args);
                gen.Emit(OpCodes.Ldc_I4_M1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("FixArgs"));
                gen.Emit(OpCodes.Stloc, args);
            }
            else
            {
                // f = {Prefix};
                target.Prefix.Accept(this);
                gen.Emit(OpCodes.Stloc, f);
            }

            //! push E.Runtime.Invoke(E, f, {Overload}, args, byref);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldloc, f);
            gen.Emit(OpCodes.Ldc_I4, target.Overload);
            gen.Emit(OpCodes.Ldloc, args);
            gen.Emit(OpCodes.Ldloc, byref);
            if (target.IsTailCall)
                gen.Emit(OpCodes.Tailcall);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("Invoke"));
            compiler.RemoveTemporary(f);
            compiler.RemoveTemporary(byref);

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
                            // $value = args[{i}];
                            gen.Emit(OpCodes.Ldloc, args);
                            gen.Emit(OpCodes.Ldc_I4, i);
                            gen.Emit(OpCodes.Ldelem, typeof(object));
                        });
                }
            }
            compiler.RemoveTemporary(args);

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
                throw new ArgumentNullException("target");

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

                NameItem namei = target.Prefix as NameItem;
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
                        name = (target.Prefix as NameItem).Name;
                    else
                        name = ((target.Prefix as IndexerItem).Expression as LiteralItem).Value as string;
                    name += ":" + target.InstanceName;

                    // E.Runtime.SetIndex(E, {Prefix}, {InstanceName}, {ImplementFunction(..)})
                    gen.Emit(OpCodes.Ldarg_1);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                    gen.Emit(OpCodes.Ldarg_1);
                    target.Prefix.Accept(this);
                    gen.Emit(OpCodes.Ldstr, target.InstanceName);
                    store = true;
                }
                else if (target.Prefix is IndexerItem)
                {
                    // global function definition with indexer
                    IndexerItem index = target.Prefix as IndexerItem;
                    name = (index.Expression as LiteralItem).Value as string;
                    gen.Emit(OpCodes.Ldarg_1);
                    gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                    gen.Emit(OpCodes.Ldarg_1);
                    index.Prefix.Accept(this);
                    index.Expression.Accept(this);
                    store = true;
                }
                else
                {
                    // global function definition with name
                    name = (target.Prefix as NameItem).Name;
                    field = compiler.FindVariable(target.Prefix as NameItem);
                    field.StartSet();
                }
            }

            compiler.ImplementFunction(this, target, name);

            if (field != null)
                field.EndSet();
            else if (store)
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("SetIndex"));

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
                throw new ArgumentNullException("target");

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
                throw new ArgumentNullException("target");

            ILGenerator gen = compiler.CurrentGenerator;
            Label next = gen.DefineLabel(), end = gen.DefineLabel();

            // if (!E.Runtime.IsTrue({Exp}) goto next;
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            target.Exp.Accept(this);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("IsTrue"));
            gen.Emit(OpCodes.Brfalse, next);

            // {Block}
            target.Block.Accept(this);

            // goto end;
            gen.Emit(OpCodes.Br, end);

            // next:
            gen.MarkLabel(next);
            foreach (var item in target.Elses)
            {
                // if (!E.Runtime.IsTrue({item.Item1}) goto next;
                next = gen.DefineLabel();
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                item.Expression.Accept(this);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("IsTrue"));
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
                throw new ArgumentNullException("target");

            //! push RuntimeHelper.GetIndex(E, {Prefix}, {Expression})
            var gen = compiler.CurrentGenerator;
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            gen.Emit(OpCodes.Ldarg_1);
            target.Prefix.Accept(this);
            target.Expression.Accept(this);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("GetIndex"));

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
                throw new ArgumentNullException("target");

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
                throw new ArgumentNullException("target");

            ILGenerator gen = compiler.CurrentGenerator;
            object Value = target.Value;

            if (Value == null)
                gen.Emit(OpCodes.Ldnull);
            else if (Value as bool? == false)
            {
                gen.Emit(OpCodes.Ldc_I4_0);
                gen.Emit(OpCodes.Box, typeof(bool));
            }
            else if (Value as bool? == true)
            {
                gen.Emit(OpCodes.Ldc_I4_1);
                gen.Emit(OpCodes.Box, typeof(bool));
            }
            else if (Value is double)
            {
                gen.Emit(OpCodes.Ldc_R8, (double)Value);
                gen.Emit(OpCodes.Box, typeof(double));
            }
            else if (Value is string)
                gen.Emit(OpCodes.Ldstr, Value as string);
            else
                throw new InvalidOperationException(Resources.InvalidLiteralType);

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
                throw new ArgumentNullException("target");

            // get the vaue of the given name and push onto stack.
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
                throw new ArgumentNullException("target");

            ILGenerator gen = compiler.CurrentGenerator;
            target.Break.UserData = target.Break.UserData ?? gen.DefineLabel();
            Label start = gen.DefineLabel();
            Label end = (Label)target.Break.UserData;

            // start:
            gen.MarkLabel(start);

            // {Block}
            target.Block.Accept(this);

            // if (!E.Runtime.IsTrue({Exp}) goto start;
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            target.Expression.Accept(this);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("IsTrue"));
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
                throw new ArgumentNullException("target");

            ILGenerator gen = compiler.CurrentGenerator;
            if (target.Expressions.Count == 1 && target.Expressions[0] is FuncCallItem)
            {
                ((FuncCallItem)target.Expressions[0]).IsTailCall = true;
                target.Expressions[0].Accept(this);
                return target;
            }

            // object[] loc = new object[{Expressions.Count}];
            LocalBuilder loc = compiler.CreateArray(typeof(object), target.Expressions.Count);

            for (int i = 0; i < target.Expressions.Count; i++)
            {
                // loc[{i}] = {Expressions[i]};
                gen.Emit(OpCodes.Ldloc, loc);
                gen.Emit(OpCodes.Ldc_I4, i);
                target.Expressions[i].Accept(this);
                gen.Emit(OpCodes.Stelem, typeof(object));
            }

            //! push new MultipleReturn(loc)
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Newobj, typeof(MultipleReturn).GetConstructor(new Type[] { typeof(IEnumerable) }));
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
                throw new ArgumentNullException("target");

            var gen = compiler.CurrentGenerator;
            var loc = compiler.CreateTemporary(typeof(object));

            // loc = E.Runtime.CreateTable(E);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("CreateTable"));
            gen.Emit(OpCodes.Stloc, loc);

            foreach (var item in target.Fields)
            {
                // Does not need to use SetItemRaw becuase there is no Metatable.
                // E.Runtime.SetIndex(E. loc, {item.Item1}, {item.Item2});
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Ldloc, loc);
                item.Key.Accept(this);
                item.Value.Accept(this);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("SetIndex"));
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
                throw new ArgumentNullException("target");

            ILGenerator gen = compiler.CurrentGenerator;

            //! push E.Runtime.ResolveUnaryOperation({OperationType}, {Target})
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            gen.Emit(OpCodes.Ldc_I4, (int)target.OperationType);
            target.Target.Accept(this);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("ResolveUnaryOperation"));

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
                throw new ArgumentNullException("target");

            ILGenerator gen = compiler.CurrentGenerator;

            // object[] loc = new object[{target.Expressions.Count}];
            LocalBuilder loc = compiler.CreateArray(typeof(object), target.Expressions.Count);
            // object[] names = new object[{target.Names.Count}];
            LocalBuilder names = compiler.CreateArray(typeof(object), target.Names.Count);

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
                    gen.Emit(OpCodes.Stelem, typeof(object));
                }
            }

            for (int i = 0; i < target.Expressions.Count; i++)
            {
                // loc[{i}] = {exps[i]};
                gen.Emit(OpCodes.Ldloc, loc);
                gen.Emit(OpCodes.Ldc_I4, i);
                target.Expressions[i].Accept(this);
                gen.Emit(OpCodes.Stelem, typeof(object));
            }

            // loc = E.Runtime.FixArgs(loc, {target.Names.Count});
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Ldc_I4, target.Names.Count);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("FixArgs"));
            gen.Emit(OpCodes.Stloc, loc);

            for (int i = 0; i < target.Names.Count; i++)
            {
                AssignValue(target.Names[i], target.Local,
                    () =>
                    {
                        // only called if the target object is an indexer item.

                        // $index = names[{i}];
                        gen.Emit(OpCodes.Ldloc, names);
                        gen.Emit(OpCodes.Ldc_I4, i);
                        gen.Emit(OpCodes.Ldelem, typeof(object));
                    },
                    () =>
                    {
                        // $value = loc[{i}];
                        gen.Emit(OpCodes.Ldloc, loc);
                        gen.Emit(OpCodes.Ldc_I4, i);
                        gen.Emit(OpCodes.Ldelem, typeof(object));

                    });
            }
            compiler.RemoveTemporary(loc);
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
                throw new ArgumentNullException("target");

            ILGenerator gen = compiler.CurrentGenerator;
            target.Break.UserData = target.Break.UserData ?? gen.DefineLabel();
            Label start = gen.DefineLabel();
            Label end = (Label)target.Break.UserData;

            // start:
            gen.MarkLabel(start);

            // if (!E.Runtime.IsTrue({Exp}) goto end;
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            target.Exp.Accept(this);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("IsTrue"));
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
        void AssignValue(IParseItem/*!*/ target, bool local, Action getIndex, Action/*!*/ getValue)
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
                // E.Runtime.SetIndex(E, {name.Prefix}, {name.Expression}, value);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                gen.Emit(OpCodes.Ldarg_1);
                name.Prefix.Accept(this);
                if (getIndex != null)
                    getIndex();
                else
                    name.Expression.Accept(this);
                getValue();
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("SetIndex"));
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
