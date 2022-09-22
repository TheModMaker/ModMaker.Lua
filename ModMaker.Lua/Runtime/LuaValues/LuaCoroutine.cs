// Copyright 2016 Jacob Trimble
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
using System.Runtime.CompilerServices;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines a coroutine in Lua.  Cannot be created in C#, use the coroutine library in Lua to
  /// create a thread.  Threads in Lua execute synchronously.
  /// </summary>
  public sealed class LuaCoroutine : LuaValueBase, ILuaBoundValue {
    static readonly ConditionalWeakTable<ILuaEnvironment, Stack<LuaCoroutine>> _coroutines =
        new ConditionalWeakTable<ILuaEnvironment, Stack<LuaCoroutine>>();
    static readonly ConditionalWeakTable<ILuaEnvironment, LuaCoroutine> _main =
        new ConditionalWeakTable<ILuaEnvironment, LuaCoroutine>();
    readonly ILuaCoroutineImpl? _impl;

    internal LuaCoroutine(ILuaEnvironment env, ILuaCoroutineImpl? impl) {
      _impl = impl;
      Environment = env;
    }
    ~LuaCoroutine() {
      _impl?.Shutdown();
    }

    public static LuaCoroutine Current(ILuaEnvironment env) {
      if (_coroutines.TryGetValue(env, out var result)) {
        if (result.Count > 0)
          return result.Peek();
      }
      return _main.GetValue(env, (e) => new LuaCoroutine(e, null));
    }

    public override LuaValueType ValueType { get { return LuaValueType.Thread; } }
    public ILuaEnvironment Environment { get; private set; }

    public LuaCoroutineStatus Status {
      get {
        if (_impl != null)
          return _impl.Status;
        // Get the status of the "main" thread for a Lua instance.
        if (_coroutines.TryGetValue(Environment, out var stack))
          return stack.Count > 0 ? LuaCoroutineStatus.Waiting : LuaCoroutineStatus.Running;
        return LuaCoroutineStatus.Running;
      }
    }
    public bool IsLua { get { return _impl != null; } }

    public LuaMultiValue Resume(LuaMultiValue args) {
      if (_impl == null)
        throw new InvalidOperationException("Cannot resume the main coroutine");
      using (LuaEnvironment._setEnvironment(Environment)) {
        var stack = _coroutines.GetOrCreateValue(Environment);
        stack.Push(this);
        LuaMultiValue ret;
        try {
          ret = _impl.Resume(args);
        } finally {
          stack.Pop();
        }
        return ret;
      }
    }
    public static LuaMultiValue Yield(LuaMultiValue args) {
      ILuaCoroutineImpl impl;
      {
        var stack = _coroutines.GetOrCreateValue(LuaEnvironment.CurrentEnvironment);
        if (stack.Count == 0)
          throw new InvalidOperationException("No coroutines to yield to");
        impl = stack.Peek()._impl!;
      }
      return impl.Yield(args);
    }

    object ILuaBoundValue.CloneIntoEnvironment(ILuaEnvironment env) {
      throw new NotImplementedException();
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
      return _arithmeticBase(type, other) ?? ((ILuaValueVisitor)other).Arithmetic(type, this);
    }
    public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self) {
      return self.ArithmeticFrom(type, this);
    }

    public override bool Equals(ILuaValue? other) {
      return ReferenceEquals(this, other);
    }
    public override bool Equals(object? obj) {
      return ReferenceEquals(this, obj);
    }
    public override int GetHashCode() {
      return base.GetHashCode();
    }
  }
}
