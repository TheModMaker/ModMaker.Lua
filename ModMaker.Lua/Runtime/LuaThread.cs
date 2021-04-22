// Copyright 2012 Jacob Trimble
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
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Defines a thread in Lua.  Cannot be created in C#, use the coroutine library in Lua to create
  /// a thread.  Threads in Lua execute synchronously.
  /// </summary>
  public sealed class LuaThreadNet : LuaThread {
    readonly object _handle = new object();
    readonly ILuaEnvironment _env;
    readonly ILuaValue _method;
    readonly bool _releaseBacking;
    Exception _exception = null;
    ILuaMultiValue _args;
    Thread _backing;

    /// <summary>
    /// Creates a new LuaThread object that represents the main thread.
    /// </summary>
    internal LuaThreadNet() {
      Status = LuaThreadStatus.Running;
      IsLua = false;
      _releaseBacking = false;
    }
    /// <summary>
    /// Creates a new LuaThread object that calls the given method.
    /// </summary>
    /// <param name="env">The current environment.</param>
    /// <param name="method">The method to invoke.</param>
    internal LuaThreadNet(ILuaEnvironment env, ILuaValue method) {
      IsLua = true;
      _env = env;
      _method = method;
      _backing = new Thread(_do);
      _releaseBacking = true;
    }
    /// <summary>
    /// Creates a new LuaThread object that calls the given method and executes on the given thread.
    /// </summary>
    /// <param name="env">The current environment.</param>
    /// <param name="method">The method to invoke.</param>
    /// <param name="thread">The thread that will execute this thread.</param>
    internal LuaThreadNet(ILuaEnvironment env, Thread thread, ILuaValue method) {
      IsLua = true;
      _env = env;
      _method = method;
      _backing = thread;
      _releaseBacking = false;
    }

    public override ILuaMultiValue Resume(ILuaMultiValue args) {
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

        if (_backing.ThreadState == ThreadState.Unstarted) {
          _backing.Start();
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
    public override ILuaMultiValue Yield(ILuaMultiValue args) {
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

    protected override void _dispose(bool disposing) {
      if (disposing) {
        if (_releaseBacking) {
          _backing.Abort();
          _backing.Join();
        }
        _backing = null;
      }
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
