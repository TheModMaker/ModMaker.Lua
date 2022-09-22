// Copyright 2022 Jacob Trimble
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
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines an implementation of Lua coroutines that uses a background thread to run the code.
  /// The thread is blocked while waiting to be resumed.
  /// </summary>
  sealed class LuaCoroutineThread : ILuaCoroutineImpl {
    readonly ILuaValue _method;
    readonly Thread _backing;
    readonly object _handle = new object();
    ExceptionDispatchInfo? _exception = null;
    LuaMultiValue _args = LuaMultiValue.Empty;
    bool _yielded = true;
    bool _shutdown = false;

    public LuaCoroutineThread(ILuaValue method, Thread thread) {
      _method = method;
      _backing = thread;
    }
    ~LuaCoroutineThread() {
      Shutdown();
    }

    public LuaCoroutineStatus Status {
      get {
        lock (_handle) {
          if (Thread.CurrentThread == _backing)
            return LuaCoroutineStatus.Running;
          else if (!_backing.IsAlive || _shutdown)
            return LuaCoroutineStatus.Complete;
          else if (_yielded)
            return LuaCoroutineStatus.Yielded;
          else
            return LuaCoroutineStatus.Waiting;
        }
      }
    }

    public LuaMultiValue Resume(LuaMultiValue args) {
      lock (_handle) {
        if (Thread.CurrentThread == _backing) {
          throw new InvalidOperationException("Cannot resume a running coroutine");
        }
        if (!_backing.IsAlive || _shutdown) {
          throw new InvalidOperationException("Cannot resume a dead coroutine");
        }

        _args = args;
        _yielded = false;
        Monitor.Pulse(_handle);

        while (_backing.IsAlive && !_yielded && !_shutdown) {
          Monitor.Wait(_handle);
        }
        if (_exception != null)
          _exception.Throw();

        return _args;
      }
    }
    public LuaMultiValue Yield(LuaMultiValue args) {
      lock (_handle) {
        if (Thread.CurrentThread != _backing) {
          throw new InvalidOperationException("Yield can only be called in the thread it manages");
        }
        if (_shutdown) {
          throw new InvalidOperationException("Coroutine is pending shutdown");
        }

        _args = args;
        _yielded = true;
        Monitor.PulseAll(_handle);

        while (_yielded && !_shutdown) {
          Monitor.Wait(_handle);
        }
        if (_shutdown)
          throw new LuaAbortException();

        return _args;
      }
    }

    public void Run() {
      try {
        LuaMultiValue args;
        lock (_handle) {
          while (_yielded && !_shutdown) {
            Monitor.Wait(_handle);
          }
          if (_shutdown)
            return;
          args = _args;
        }

        LuaMultiValue ret = _method.Invoke(args);

        lock (_handle) {
          _args = ret;
          _exception = null;
          _shutdown = true;
          Monitor.PulseAll(_handle);
        }
      } catch (Exception e) {
        lock (_handle) {
          _exception = ExceptionDispatchInfo.Capture(e);
          _shutdown = true;
          Monitor.PulseAll(_handle);
        }
      }
    }
    public void Shutdown() {
      lock (_handle) {
        _shutdown = true;
        Monitor.PulseAll(_handle);
      }
    }
  }
}
