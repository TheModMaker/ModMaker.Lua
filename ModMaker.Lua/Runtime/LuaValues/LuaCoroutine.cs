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
  public sealed class LuaCoroutine : LuaValueBase {
    static readonly ConditionalWeakTable<ILuaEnvironment, Stack<LuaCoroutine>> _coroutines =
        new ConditionalWeakTable<ILuaEnvironment, Stack<LuaCoroutine>>();
    static readonly LuaCoroutine _main = new LuaCoroutine(null);
    readonly ILuaCoroutineImpl? _impl;

    internal LuaCoroutine(ILuaCoroutineImpl? impl) {
      _impl = impl;
    }
    ~LuaCoroutine() {
      _impl?.Shutdown();
    }

    public static LuaCoroutine Current(ILuaEnvironment env) {
      if (_coroutines.TryGetValue(env, out var result)) {
        if (result.Count > 0)
          return result.Peek();
      }
      return _main;
    }

    public override LuaValueType ValueType { get { return LuaValueType.Thread; } }

    public LuaCoroutineStatus Status {
      get {
        if (_impl != null)
          return _impl.Status;
        // Get the status of the "main" thread for a Lua instance.
        // TODO: Should be bound object, so we should use that environment.
        try {
          if (_coroutines.TryGetValue(LuaEnvironment.CurrentEnvironment, out var stack))
            return stack.Count > 0 ? LuaCoroutineStatus.Waiting : LuaCoroutineStatus.Running;
        } catch (InvalidOperationException) { }
        return LuaCoroutineStatus.Running;
      }
    }
    public bool IsLua { get { return _impl != null; } }

    public LuaMultiValue Resume(LuaMultiValue args) {
      if (_impl == null)
        throw new InvalidOperationException("Cannot resume the main coroutine");
      var stack = _coroutines.GetOrCreateValue(LuaEnvironment.CurrentEnvironment);
      stack.Push(this);
      LuaMultiValue ret;
      try {
        ret = _impl.Resume(args);
      } finally {
        stack.Pop();
      }
      return ret;
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
