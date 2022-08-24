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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Compiler {
  /// <summary>
  /// Defines a visitor object that helps compile code with CodeCompiler.
  /// </summary>
  sealed class CompilerVisitor : IParseItemVisitor {
    readonly ChunkBuilder _compiler;
    readonly Dictionary<LabelItem, Label> _labels =
        new Dictionary<LabelItem, Label>(new ReferenceEqualsComparer<LabelItem>());

    /// <summary>
    /// A Helper used for compiling function call.  This is used to fake the prefix in an indexer
    /// item.  This simply reads from the prefix local.
    /// </summary>
    sealed class IndexerHelper : IParseExp {
      readonly ILGenerator _gen;
      readonly LocalBuilder _prefix;

      public IndexerHelper(ILGenerator gen, LocalBuilder prefix) {
        _gen = gen;
        _prefix = prefix;
      }

      public DebugInfo Debug { get; set; }

      public IParseItem Accept(IParseItemVisitor visitor) {
        _gen.Emit(OpCodes.Ldloc, _prefix);
        return this;
      }
    }

    /// <summary>
    /// Creates a new instance of CompilerVisitor.
    /// </summary>
    /// <param name="compiler">The creating object used to help generate code.</param>
    public CompilerVisitor(ChunkBuilder compiler) {
      _compiler = compiler;
    }

    public IParseItem Visit(BinOpItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      ILGenerator gen = _compiler.CurrentGenerator;

      if (target.OperationType == BinaryOperationType.And ||
          target.OperationType == BinaryOperationType.Or) {
        // object temp = {Lhs};
        var end = gen.DefineLabel();
        var temp = _compiler.CreateTemporary(typeof(ILuaValue));
        target.Lhs.Accept(this);
        gen.Emit(OpCodes.Stloc, temp);

        // Push Lhs onto the stack, if going to end, this will be the result.
        gen.Emit(OpCodes.Ldloc, temp);

        // if (temp.IsTrue) goto end;
        gen.Emit(OpCodes.Ldloc, temp);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.get_IsTrue);
        if (target.OperationType == BinaryOperationType.And) {
          // We want to break if the value is truthy and it's an OR, or it's falsy and it's an AND.

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
      } else {
        //! push {Lhs}.Arithmetic({OperationType}, {Rhs})
        target.Lhs.Accept(this);
        gen.Emit(OpCodes.Ldc_I4, (int)target.OperationType);
        target.Rhs.Accept(this);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.Arithmetic);
      }

      return target;
    }
    public IParseItem Visit(BlockItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      using (_compiler.LocalBlock()) {
        foreach (IParseItem child in target.Children) {
          child.Accept(this);
        }

        if (target.Return != null) {
          // return {Return};
          target.Return.Accept(this);
          _compiler.CurrentGenerator.Emit(OpCodes.Ret);
        }
      }

      return target;
    }
    public IParseItem Visit(ClassDefItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      ILGenerator gen = _compiler.CurrentGenerator;
      _compiler.MarkSequencePoint(target.Debug);

      // string[] loc = new string[{implements.Count}];
      LocalBuilder loc = _compiler.CreateArray(typeof(string), target.Implements.Count);

      int i = 0;
      foreach (var item in target.Implements) {
        // loc[{i}] = {implements[i]};
        gen.Emit(OpCodes.Ldloc, loc);
        gen.Emit(OpCodes.Ldc_I4, (i++));
        gen.Emit(OpCodes.Ldstr, item);
        gen.Emit(OpCodes.Stelem, typeof(string));
      }

      // E.Runtime.CreateClassValue(loc, {name});
      gen.Emit(OpCodes.Ldarg_1);
      gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaEnvironment.get_Runtime);
      gen.Emit(OpCodes.Ldloc, loc);
      gen.Emit(OpCodes.Ldstr, target.Name);
      gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaRuntime.CreateClassValue);
      _compiler.RemoveTemporary(loc);

      return target;
    }
    public IParseItem Visit(ForGenItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      ILGenerator gen = _compiler.CurrentGenerator;

      if (!_labels.ContainsKey(target.Break))
        _labels.Add(target.Break, gen.DefineLabel());
      Label start = gen.DefineLabel();
      Label end = _labels[target.Break];
      LocalBuilder ret = _compiler.CreateTemporary(typeof(LuaMultiValue));
      LocalBuilder enumerable = _compiler.CreateTemporary(typeof(IEnumerable<LuaMultiValue>));
      LocalBuilder enumerator = _compiler.CreateTemporary(typeof(IEnumerator<LuaMultiValue>));

      using (_compiler.LocalBlock()) {
        // temp = new ILuaValue[...];
        _compiler.MarkSequencePoint(target.ForDebug);
        var temp = _compiler.CreateArray(typeof(ILuaValue), target.Expressions.Length);

        for (int i = 0; i < target.Expressions.Length; i++) {
          // temp[{i}] = {item};
          gen.Emit(OpCodes.Ldloc, temp);
          gen.Emit(OpCodes.Ldc_I4, i);
          target.Expressions[i].Accept(this);
          gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
        }

        // enumerable = E.Runtime.GenericLoop(E, new LuaMultiValue(temp));
        gen.Emit(OpCodes.Ldarg_1);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaEnvironment.get_Runtime);
        gen.Emit(OpCodes.Ldarg_1);
        gen.Emit(OpCodes.Ldloc, temp);
        gen.Emit(OpCodes.Newobj, ReflectionMembers.LuaMultiValue.Constructor);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaRuntime.GenericLoop);
        gen.Emit(OpCodes.Stloc, enumerable);
        _compiler.RemoveTemporary(temp);

        // enumerator = enumerable.GetEnumerator();
        gen.Emit(OpCodes.Ldloc, enumerable);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.IEnumerableLuaMultiValue.GetEnumerator);
        gen.Emit(OpCodes.Stloc, enumerator);

        // try {
        Label endTry = gen.BeginExceptionBlock();
        gen.MarkLabel(start);

        // if (!enumerator.MoveNext()) goto end;
        gen.Emit(OpCodes.Ldloc, enumerator);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.IEnumerator.MoveNext);
        gen.Emit(OpCodes.Brfalse, end);

        // ILuaMultiValue ret = enumerator.Current;
        gen.Emit(OpCodes.Ldloc, enumerator);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.IEnumeratorLuaMultiValue.get_Current);
        gen.Emit(OpCodes.Stloc, ret);
        _compiler.RemoveTemporary(enumerator);

        for (int i = 0; i < target.Names.Length; i++) {
          // {_names[i]} = ret[{i}];
          var field = _compiler.DefineLocal(target.Names[i]);
          field.StartSet();
          gen.Emit(OpCodes.Ldloc, ret);
          gen.Emit(OpCodes.Ldc_I4, i);
          gen.Emit(OpCodes.Call, ReflectionMembers.LuaMultiValue.get_Item);
          field.EndSet();
        }
        _compiler.RemoveTemporary(ret);

        // {Block}
        target.Block.Accept(this);

        // goto start;
        gen.Emit(OpCodes.Br, start);

        // end:
        _compiler.MarkSequencePoint(target.EndDebug);
        gen.MarkLabel(end);

        // } finally {
        gen.Emit(OpCodes.Leave, endTry);
        gen.BeginFinallyBlock();

        // if (enumerable != null) enumerable.Dispose();
        Label endFinally = gen.DefineLabel();
        gen.Emit(OpCodes.Ldloc, enumerable);
        gen.Emit(OpCodes.Brfalse, endFinally);
        gen.Emit(OpCodes.Ldloc, enumerable);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.IDisposable.Dispose);
        gen.MarkLabel(endFinally);
        _compiler.RemoveTemporary(enumerable);

        // }
        gen.EndExceptionBlock();
      }

      return target;
    }
    public IParseItem Visit(ForNumItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      ILGenerator gen = _compiler.CurrentGenerator;

      if (!_labels.ContainsKey(target.Break))
        _labels.Add(target.Break, gen.DefineLabel());
      Label start = gen.DefineLabel();
      Label end = _labels[target.Break];
      Label sj = gen.DefineLabel();
      Label err = gen.DefineLabel();
      LocalBuilder d = _compiler.CreateTemporary(typeof(double?));
      LocalBuilder val = _compiler.CreateTemporary(typeof(double));
      LocalBuilder step = _compiler.CreateTemporary(typeof(double));
      LocalBuilder limit = _compiler.CreateTemporary(typeof(double));

      using (_compiler.LocalBlock()) {
        _compiler.MarkSequencePoint(target.ForDebug);
        // d = {Start}.AsDouble();
        target.Start.Accept(this);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.AsDouble);
        gen.Emit(OpCodes.Stloc, d);

        // if (d.HasValue) goto sj;
        gen.Emit(OpCodes.Ldloca, d);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.NullableDouble.get_HasValue);
        gen.Emit(OpCodes.Brtrue, sj);

        // err:
        gen.MarkLabel(err);

        // throw new InvalidOperationException(
        //     "The Start, Limit, and Step of a for loop must result in numbers.");
        gen.Emit(OpCodes.Ldstr, Resources.LoopMustBeNumbers);
        gen.Emit(OpCodes.Newobj, ReflectionMembers.InvalidOperationException.StringConstructor);
        gen.Emit(OpCodes.Throw);

        // sj:
        gen.MarkLabel(sj);

        // val = d.Value;
        gen.Emit(OpCodes.Ldloca, d);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.NullableDouble.get_Value);
        gen.Emit(OpCodes.Stloc, val);

        if (target.Step != null) {
          // d = {Step}.AsDouble();
          target.Step.Accept(this);
          gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.AsDouble);
          gen.Emit(OpCodes.Stloc, d);

          // if (!d.HasValue) goto err;
          gen.Emit(OpCodes.Ldloca, d);
          gen.Emit(OpCodes.Callvirt, ReflectionMembers.NullableDouble.get_HasValue);
          gen.Emit(OpCodes.Brfalse, err);

          // step = d.Value;
          gen.Emit(OpCodes.Ldloca, d);
          gen.Emit(OpCodes.Callvirt, ReflectionMembers.NullableDouble.get_Value);
        } else {
          // step = 1.0;
          gen.Emit(OpCodes.Ldc_R8, 1.0);
        }
        gen.Emit(OpCodes.Stloc, step);

        // d = {Limit}.AsDouble();
        target.Limit.Accept(this);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.AsDouble);
        gen.Emit(OpCodes.Stloc, d);

        // if (!d.HasValue) goto err;
        gen.Emit(OpCodes.Ldloca, d);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.NullableDouble.get_HasValue);
        gen.Emit(OpCodes.Brfalse, err);

        // limit = d.Value;
        gen.Emit(OpCodes.Ldloca, d);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.NullableDouble.get_Value);
        gen.Emit(OpCodes.Stloc, limit);
        _compiler.RemoveTemporary(d);

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

        // {name} = LuaValueBase.CreateValue((object)val);
        var field = _compiler.DefineLocal(target.Name);
        field.StartSet();
        gen.Emit(OpCodes.Ldloc, val);
        gen.Emit(OpCodes.Box, typeof(double));
        gen.Emit(OpCodes.Call, ReflectionMembers.LuaValueBase.CreateValue);
        field.EndSet();

        // {Block}
        target.Block.Accept(this);

        // val += step;
        gen.Emit(OpCodes.Ldloc, val);
        gen.Emit(OpCodes.Ldloc, step);
        gen.Emit(OpCodes.Add);
        gen.Emit(OpCodes.Stloc, val);
        _compiler.RemoveTemporary(val);
        _compiler.RemoveTemporary(step);
        _compiler.RemoveTemporary(limit);

        // goto start;
        gen.Emit(OpCodes.Br, start);

        // end:
        _compiler.MarkSequencePoint(target.EndDebug);
        gen.MarkLabel(end);  // Insert no-op so debugger can step on the "end" token.
        gen.Emit(OpCodes.Nop);
      }
      return target;
    }
    public IParseItem Visit(FuncCallItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      //// load the args into an array.
      ILGenerator gen = _compiler.CurrentGenerator;
      LocalBuilder f = _compiler.CreateTemporary(typeof(ILuaValue));
      LocalBuilder self = _compiler.CreateTemporary(typeof(object));
      if (target.Statement) {
        _compiler.MarkSequencePoint(target.Debug);
      }

      /* add 'self' if instance call */
      if (target.InstanceName != null) {
        // self = {Prefix};
        target.Prefix.Accept(this);
        gen.Emit(OpCodes.Stloc, self);

        // f = self.GetIndex(LuaValueBase.CreateValue({InstanceName}));
        gen.Emit(OpCodes.Ldloc, self);
        gen.Emit(OpCodes.Ldstr, target.InstanceName);
        gen.Emit(OpCodes.Call, ReflectionMembers.LuaValueBase.CreateValue);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.GetIndex);
        gen.Emit(OpCodes.Stloc, f);
      } else if (target.Prefix is IndexerItem item) {
        // self = {Prefix};
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
      } else {
        // self = LuaNil.Nil;
        gen.Emit(OpCodes.Ldnull);
        gen.Emit(OpCodes.Ldfld, ReflectionMembers.LuaNil.Nil);
        gen.Emit(OpCodes.Stloc, self);

        // f = {Prefix};
        target.Prefix.Accept(this);
        gen.Emit(OpCodes.Stloc, f);
      }

      // var args = new ILuaValue[...];
      LocalBuilder args = _compiler.CreateArray(typeof(ILuaValue), target.Arguments.Length);
      for (int i = 0; i < target.Arguments.Length; i++) {
        // args[i] = {item};
        gen.Emit(OpCodes.Ldloc, args);
        gen.Emit(OpCodes.Ldc_I4, i);
        target.Arguments[i].Expression.Accept(this);
        if (i + 1 == target.Arguments.Length && target.IsLastArgSingle) {
          gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.Single);
        }

        gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
      }

      // var rargs = new LuaMultiValue(args);
      var rargs = _compiler.CreateTemporary(typeof(LuaMultiValue));
      gen.Emit(OpCodes.Ldloc, args);
      gen.Emit(OpCodes.Newobj, ReflectionMembers.LuaMultiValue.Constructor);
      gen.Emit(OpCodes.Stloc, rargs);
      _compiler.RemoveTemporary(args);

      //! push f.Invoke(self, {!!InstanceName}, rargs);
      gen.Emit(OpCodes.Ldloc, f);
      gen.Emit(OpCodes.Ldloc, self);
      gen.Emit(target.InstanceName != null ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
      gen.Emit(OpCodes.Ldloc, rargs);
      if (target.IsTailCall) {
        gen.Emit(OpCodes.Tailcall);
      }

      gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.Invoke);
      _compiler.RemoveTemporary(f);
      _compiler.RemoveTemporary(self);

      //! pop
      if (target.Statement) {
        gen.Emit(OpCodes.Pop);
      }

      // support byRef
      for (int i = 0; i < target.Arguments.Length; i++) {
        if (target.Arguments[i].IsByRef) {
          _assignValue(target.Arguments[i].Expression, false, null, () => {
            // $value = rargs[{i}];
            gen.Emit(OpCodes.Ldloc, rargs);
            gen.Emit(OpCodes.Ldc_I4, i);
            gen.Emit(OpCodes.Call, ReflectionMembers.LuaMultiValue.get_Item);
          });
        }
      }
      _compiler.RemoveTemporary(rargs);

      return target;
    }
    public IParseItem Visit(FuncDefItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      var gen = _compiler.CurrentGenerator;
      ChunkBuilder.IVarDefinition field = null;
      string name = null;
      bool store = false;
      _compiler.MarkSequencePoint(target.Debug);

      if (target.Local) {
        // Local function definition
        // TODO: Allow returning multiple errors.
        if (target.InstanceName != null) {
          var msg =  new CompilerMessage(MessageLevel.Error, MessageId.LocalInstanceName,
                                         target.Debug);
          throw new CompilerException(new[] {  msg });
        }

        if (!(target.Prefix is NameItem)) {
          var msg =  new CompilerMessage(MessageLevel.Error, MessageId.LocalMethodIndexer,
                                         target.Debug);
          throw new CompilerException(new[] { msg });
        }

        NameItem namei = (NameItem)target.Prefix;
        name = namei.Name;
        field = _compiler.DefineLocal(namei);
        field.StartSet();
      } else if (target.Prefix != null) {
        if (target.InstanceName != null) {
          // Instance function definition
          name = null;
          if (target.Prefix is NameItem nameItem) {
            name = nameItem.Name;
          } else {
            name = (string)((LiteralItem)((IndexerItem)target.Prefix).Expression).Value;
          }

          name += ":" + target.InstanceName;

          // {Prefix}.SetIndex(LuaValueBase.CreateValue({InstanceName}), {ImplementFunction(...)})
          target.Prefix.Accept(this);
          gen.Emit(OpCodes.Ldstr, target.InstanceName);
          gen.Emit(OpCodes.Call, ReflectionMembers.LuaValueBase.CreateValue);
          store = true;
        } else if (target.Prefix is IndexerItem index) {
          // Global function definition with indexer
          // {Prefix}.SetIndex({Expression}, {ImplementFunction(..)})
          name = (string)((LiteralItem)index.Expression).Value;
          index.Prefix.Accept(this);
          index.Expression.Accept(this);
          store = true;
        } else {
          // Global function definition with name
          name = ((NameItem)target.Prefix).Name;
          field = _compiler.FindVariable((NameItem)target.Prefix);
          field.StartSet();
        }
      }

      _compiler.ImplementFunction(this, target, name);

      if (field != null) {
        field.EndSet();
      } else if (store) {
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.SetIndex);
      }

      return target;
    }
    public IParseItem Visit(GotoItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      if (target.Target == null) {
        throw new InvalidOperationException(Resources.ErrorResolveLabel);
      }

      _compiler.MarkSequencePoint(target.Debug);
      _compiler.CurrentGenerator.Emit(OpCodes.Br, _labels[target.Target]);

      return target;
    }
    public IParseItem Visit(IfItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      ILGenerator gen = _compiler.CurrentGenerator;
      Label next = gen.DefineLabel();
      Label end = gen.DefineLabel();

      // if (!{Exp}.IsTrue) goto next;
      _compiler.MarkSequencePoint(target.IfDebug);
      target.Expression.Accept(this);
      gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.get_IsTrue);
      gen.Emit(OpCodes.Brfalse, next);

      // {Block}
      target.Block.Accept(this);

      // goto end;
      gen.Emit(OpCodes.Br, end);

      // next:
      gen.MarkLabel(next);
      foreach (var item in target.Elses) {
        // if (!{item.Item1}.IsTrue) goto next;
        _compiler.MarkSequencePoint(item.Debug);
        next = gen.DefineLabel();
        item.Expression.Accept(this);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.get_IsTrue);
        gen.Emit(OpCodes.Brfalse, next);

        // {item.Item2}
        item.Block.Accept(this);

        // goto end;
        gen.Emit(OpCodes.Br, end);

        // next:
        gen.MarkLabel(next);
      }
      if (target.ElseBlock != null) {
        _compiler.MarkSequencePoint(target.ElseDebug);
        target.ElseBlock.Accept(this);
      }

      // end:
      _compiler.MarkSequencePoint(target.EndDebug);
      gen.MarkLabel(end);
      gen.Emit(OpCodes.Nop);  // Insert no-op so debugger can step on the "end" token.

      return target;
    }
    public IParseItem Visit(IndexerItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      //! push {Prefix}.GetIndex({Expression})
      var gen = _compiler.CurrentGenerator;
      target.Prefix.Accept(this);
      target.Expression.Accept(this);
      gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.GetIndex);

      return target;
    }
    public IParseItem Visit(LabelItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      if (!_labels.ContainsKey(target))
        _labels.Add(target, _compiler.CurrentGenerator.DefineLabel());
      _compiler.CurrentGenerator.MarkLabel(_labels[target]);

      return target;
    }
    public IParseItem Visit(LiteralItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      ILGenerator gen = _compiler.CurrentGenerator;
      object value = target.Value;

      if (value is null) {
        gen.Emit(OpCodes.Ldnull);
      } else if (value is bool b) {
        gen.Emit(b ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
        gen.Emit(OpCodes.Box, typeof(bool));
      } else if (value is double d) {
        gen.Emit(OpCodes.Ldc_R8, d);
        gen.Emit(OpCodes.Box, typeof(double));
      } else if (value is string s) {
        gen.Emit(OpCodes.Ldstr, s);
      } else {
        throw new InvalidOperationException(Resources.InvalidLiteralType);
      }

      gen.Emit(OpCodes.Call, ReflectionMembers.LuaValueBase.CreateValue);

      return target;
    }
    public IParseItem Visit(NameItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      // Get the value of the given name and push onto stack
      var field = _compiler.FindVariable(target);
      field.Get();

      return target;
    }
    public IParseItem Visit(RepeatItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      ILGenerator gen = _compiler.CurrentGenerator;
      if (!_labels.ContainsKey(target.Break))
        _labels.Add(target.Break, gen.DefineLabel());
      Label start = gen.DefineLabel();
      Label end = _labels[target.Break];

      // start:
      _compiler.MarkSequencePoint(target.RepeatDebug);
      gen.MarkLabel(start);
      gen.Emit(OpCodes.Nop);  // So the debugger can stop on "repeat".

      // {Block}
      target.Block.Accept(this);

      // if (!{Exp}.IsTrue) goto start;
      _compiler.MarkSequencePoint(target.UntilDebug);
      target.Expression.Accept(this);
      gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.get_IsTrue);
      gen.Emit(OpCodes.Brfalse, start);

      // end:
      gen.MarkLabel(end);

      return target;
    }
    public IParseItem Visit(ReturnItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      _compiler.MarkSequencePoint(target.Debug);
      ILGenerator gen = _compiler.CurrentGenerator;
      if (target.Expressions.Length == 1 && !target.IsLastExpressionSingle &&
          target.Expressions[0] is FuncCallItem func) {
        func.IsTailCall = true;
        target.Expressions[0].Accept(this);
        return target;
      }

      // ILuaValue[] loc = new ILuaValue[{Expressions.Count}];
      LocalBuilder loc = _compiler.CreateArray(typeof(ILuaValue), target.Expressions.Length);

      for (int i = 0; i < target.Expressions.Length; i++) {
        // loc[{i}] = {Expressions[i]};
        gen.Emit(OpCodes.Ldloc, loc);
        gen.Emit(OpCodes.Ldc_I4, i);
        target.Expressions[i].Accept(this);
        if (i + 1 == target.Expressions.Length && target.IsLastExpressionSingle) {
          gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.Single);
        }

        gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
      }

      //! push new LuaMultiValue(loc)
      gen.Emit(OpCodes.Ldloc, loc);
      gen.Emit(OpCodes.Newobj, ReflectionMembers.LuaMultiValue.Constructor);
      _compiler.RemoveTemporary(loc);

      return target;
    }
    public IParseItem Visit(TableItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      var gen = _compiler.CurrentGenerator;
      var loc = _compiler.CreateTemporary(typeof(ILuaValue));

      // loc = new LuaTable();
      gen.Emit(OpCodes.Newobj, ReflectionMembers.LuaTable.Constructor);
      gen.Emit(OpCodes.Stloc, loc);

      foreach (var item in target.Fields) {
        // Does not need to use SetItemRaw because there is no Metatable.
        // loc.SetIndex({item.Item1}, {item.Item2});
        gen.Emit(OpCodes.Ldloc, loc);
        item.Key.Accept(this);
        item.Value.Accept(this);
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.SetIndex);
      }

      //! push loc;
      gen.Emit(OpCodes.Ldloc, loc);
      _compiler.RemoveTemporary(loc);

      return target;
    }
    public IParseItem Visit(UnOpItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      ILGenerator gen = _compiler.CurrentGenerator;

      //! push {Target}.Minus();
      target.Target.Accept(this);
      switch (target.OperationType) {
        case UnaryOperationType.Minus:
          gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.Minus);
          break;
        case UnaryOperationType.Not:
          gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.Not);
          break;
        case UnaryOperationType.Length:
          gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.Length);
          break;
      }

      return target;
    }
    public IParseItem Visit(AssignmentItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      ILGenerator gen = _compiler.CurrentGenerator;
      _compiler.MarkSequencePoint(target.Debug);

      // ILuaValue[] loc = new ILuaValue[{target.Expressions.Count}];
      LocalBuilder loc = _compiler.CreateArray(typeof(ILuaValue), target.Expressions.Length);
      // ILuaValue[] names = new ILuaValue[{target.Names.Count}];
      LocalBuilder names = _compiler.CreateArray(typeof(ILuaValue), target.Names.Length);

      // Have to evaluate the name indexer expressions before setting the values otherwise the
      // following will fail:
      // i, t[i] = i+1, 20
      for (int i = 0; i < target.Names.Length; i++) {
        if (target.Names[i] is IndexerItem item) {
          gen.Emit(OpCodes.Ldloc, names);
          gen.Emit(OpCodes.Ldc_I4, i);
          item.Expression.Accept(this);
          gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
        }
      }

      for (int i = 0; i < target.Expressions.Length; i++) {
        // loc[{i}] = {exps[i]};
        gen.Emit(OpCodes.Ldloc, loc);
        gen.Emit(OpCodes.Ldc_I4, i);
        target.Expressions[i].Accept(this);
        if (i + 1 == target.Expressions.Length && target.IsLastExpressionSingle) {
          gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.Single);
        }

        gen.Emit(OpCodes.Stelem, typeof(ILuaValue));
      }

      // LuaMultiValue exp = new LuaMultiValue(loc);
      LocalBuilder exp = _compiler.CreateTemporary(typeof(LuaMultiValue));
      gen.Emit(OpCodes.Ldloc, loc);
      gen.Emit(OpCodes.Newobj, ReflectionMembers.LuaMultiValue.Constructor);
      gen.Emit(OpCodes.Stloc, exp);
      _compiler.RemoveTemporary(loc);

      for (int i = 0; i < target.Names.Length; i++) {
        _assignValue(
            target.Names[i], target.Local,
            !(target.Names[i] is IndexerItem) ? (Action)null : () => {
              // Only called if the target object is an indexer item

              // $index = names[{i}];
              gen.Emit(OpCodes.Ldloc, names);
              gen.Emit(OpCodes.Ldc_I4, i);
              gen.Emit(OpCodes.Ldelem, typeof(ILuaValue));
            },
            () => {
              // $value = exp[{i}];
              gen.Emit(OpCodes.Ldloc, exp);
              gen.Emit(OpCodes.Ldc_I4, i);
              gen.Emit(OpCodes.Callvirt, ReflectionMembers.LuaMultiValue.get_Item);
            });
      }
      _compiler.RemoveTemporary(exp);
      _compiler.RemoveTemporary(names);

      return target;
    }
    public IParseItem Visit(WhileItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      ILGenerator gen = _compiler.CurrentGenerator;
      if (!_labels.ContainsKey(target.Break))
        _labels.Add(target.Break, gen.DefineLabel());
      Label start = gen.DefineLabel();
      Label end = _labels[target.Break];

      // start:
      _compiler.MarkSequencePoint(target.WhileDebug);
      gen.MarkLabel(start);

      // if (!{Exp}.IsTrue) goto end;
      target.Expression.Accept(this);
      gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.get_IsTrue);
      gen.Emit(OpCodes.Brfalse, end);

      // {Block}
      target.Block.Accept(this);

      // goto start;
      gen.Emit(OpCodes.Br, start);

      // end:
      _compiler.MarkSequencePoint(target.EndDebug);
      gen.MarkLabel(end);
      gen.Emit(OpCodes.Nop);    // So the debugger can stop on "end".

      return target;
    }

    /// <summary>
    /// Assigns the values of the parse item to the given value.
    /// </summary>
    /// <param name="target">The item to assign the value to (e.g. NameItem).</param>
    /// <param name="local">Whether this is a local definition.</param>
    /// <param name="getIndex">
    /// A function to get the index of the object, pass null to use the default.
    /// </param>
    /// <param name="getValue">A function to get the value to set to.</param>
    void _assignValue(IParseItem target, bool local, Action getIndex, Action getValue) {
      ILGenerator gen = _compiler.CurrentGenerator;
      ChunkBuilder.IVarDefinition field;
      if (local) {
        field = _compiler.DefineLocal((NameItem)target);
      } else if (target is IndexerItem name) {
        // {name.Prefix}.SetIndex({name.Expression}, value);
        name.Prefix.Accept(this);
        if (getIndex != null) {
          getIndex();
        } else {
          name.Expression.Accept(this);
        }

        getValue();
        gen.Emit(OpCodes.Callvirt, ReflectionMembers.ILuaValue.SetIndex);
        return;
      } else {  // names[i] is NameItem
        NameItem item = (NameItem)target;
        field = _compiler.FindVariable(item);
      }

      // envField = value;
      field.StartSet();
      getValue();
      field.EndSet();
    }
  }
}
