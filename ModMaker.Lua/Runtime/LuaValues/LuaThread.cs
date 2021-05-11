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
using System.Reflection;
using System.Threading;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines a thread in Lua.  Cannot be created in C#, use the coroutine library in Lua to create
  /// a thread.  Threads in Lua execute synchronously.
  /// </summary>
  public sealed class LuaThread : LuaValueBase, ILuaThread {
    readonly ILuaValue _method = null;
    readonly Thread _backing;
    readonly object _handle = new object();
    readonly bool _releaseBacking = false;
    Exception _exception = null;
    ILuaMultiValue _args = null;

    internal LuaThread() {
      Status = LuaThreadStatus.Running;
      IsLua = false;
    }
    internal LuaThread(ILuaValue method, Thread thread = null) {
      Status = LuaThreadStatus.Suspended;
      IsLua = true;
      _method = method;
      _backing = thread ?? new Thread(_do);
      _releaseBacking = thread != null;
      if (thread == null)
        _backing.Start();
    }
    ~LuaThread() {
      if (_releaseBacking)
        _backing.Abort();
    }

    public override LuaValueType ValueType { get { return LuaValueType.Thread; } }

    public event EventHandler<YieldEventArgs> OnYield;
    public LuaThreadStatus Status { get; private set; }
    public bool IsLua { get; }

    public ILuaMultiValue Resume(ILuaMultiValue args) {
      lock (_handle) {
        if (!IsLua) {
          throw new InvalidOperationException(
              "Cannot resume a thread that has been created outside Lua.");
        }
        if (Status == LuaThreadStatus.Running) {
          throw new InvalidOperationException("Cannot resume a running thread.");
        }
        if (Status == LuaThreadStatus.Complete) {
          throw new InvalidOperationException("Cannot resume a dead thread.");
        }

        args ??= new LuaMultiValue();
        _args = args;
        Status = LuaThreadStatus.Running;

        Monitor.Pulse(_handle);
        while (Status == LuaThreadStatus.Running) {
          Monitor.Wait(_handle);
        }

        if (_exception != null) {
          throw new TargetInvocationException(_exception);
        }

        ILuaMultiValue ret = Interlocked.Exchange(ref _args, null);
        return ret ?? new LuaMultiValue();
      }
    }
    public ILuaMultiValue Yield(ILuaMultiValue args) {
      lock (_handle) {
        if (!IsLua) {
          throw new InvalidOperationException(
              "Cannot resume a thread that has been created outside Lua.");
        }
        if (Status != LuaThreadStatus.Running) {
          throw new InvalidOperationException("Thread must be running to yield.");
        }

        // Fire the OnYield event.
        var e = new YieldEventArgs(args);
        _callOnYield(e);

        // If the yield is rejected, simply return the arguments.
        if (e.RejectYield) {
          return e.ReturnArguments;
        }

        args ??= new LuaMultiValue();
        _args = args;
        Status = LuaThreadStatus.Suspended;

        Monitor.Pulse(_handle);
        while (Status != LuaThreadStatus.Running) {
          Monitor.Wait(_handle);
        }

        ILuaMultiValue ret = Interlocked.Exchange(ref _args, null);
        return ret ?? new LuaMultiValue();
      }
    }

    public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
      return _arithmeticBase(type, other) ?? ((ILuaValueVisitor)other).Arithmetic(type, this);
    }
    public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self) {
      return self.ArithmeticFrom(type, this);
    }

    public override bool Equals(ILuaValue other) {
      return ReferenceEquals(this, other);
    }
    public override bool Equals(object obj) {
      return ReferenceEquals(this, obj);
    }
    public override int GetHashCode() {
      return base.GetHashCode();
    }

    /// <summary>
    /// Calls the OnYield event handler.
    /// </summary>
    /// <param name="e">The arguments to the event.</param>
    private void _callOnYield(YieldEventArgs e) {
      OnYield?.Invoke(this, e);
    }

    internal void _do() {
      try {
        lock (_handle) {
          while (Status != LuaThreadStatus.Running) {
            Monitor.Wait(_handle);
          }
        }

        ILuaMultiValue args = Interlocked.Exchange(ref _args, null);
        ILuaMultiValue ret = _method.Invoke(LuaNil.Nil, false, args);

        lock (_handle) {
          _args = ret;
          _exception = null;
          Status = LuaThreadStatus.Complete;
          Monitor.Pulse(_handle);
        }
      } catch (Exception e) {
        lock (_handle) {
          _exception = e;
          Status = LuaThreadStatus.Complete;
          Monitor.Pulse(_handle);
        }
      }
    }
  }
}
