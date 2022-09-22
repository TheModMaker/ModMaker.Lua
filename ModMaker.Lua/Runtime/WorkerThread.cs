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
using System.Threading;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Defines a helper thread used for LuaThread objects.  This will execute multiple LuaThread
  /// objects over its lifetime.
  /// </summary>
  sealed class WorkerThread {
    /// <summary>
    /// The max wait time in milliseconds to wait for a new task.
    /// </summary>
    const int _maxWaitTime = 1000;

    /// <summary>
    /// Contains the status of the worker thread.
    /// </summary>
    enum Status {
      /// <summary>
      /// The thread is waiting for a new task.
      /// </summary>
      Waiting,
      /// <summary>
      /// The thread is working on a LuaThread object.
      /// </summary>
      Working,
      /// <summary>
      /// The thread should shutdown.
      /// </summary>
      Shutdown,
    }

    readonly object _lock = new object();
    readonly ThreadPool _owner;
    readonly Thread _backing;
    WeakReference<LuaCoroutineThread>? _target;
    Status _status;

    /// <summary>
    /// Creates a new WorkerThread object.
    /// </summary>
    /// <param name="owner">The factory that created this object.</param>
    public WorkerThread(ThreadPool owner) {
      _status = Status.Waiting;
      _owner = owner;
      _backing = new Thread(_execute) { IsBackground = true };
      _backing.Start();
    }
    ~WorkerThread() {
      lock (_lock) {
        _status = Status.Shutdown;
        if (_target?.TryGetTarget(out LuaCoroutineThread? target) ?? false)
          target.Shutdown();
        Monitor.Pulse(_lock);
      }
      _backing.Join();
    }


    /// <summary>
    /// Makes the current thread execute the given method.
    /// </summary>
    /// <param name="target">The method to execute.</param>
    public LuaCoroutineThread DoWork(ILuaValue target) {
      if (_status != Status.Waiting) {
        throw new InvalidOperationException(
            "The worker thread must be waiting to get a new task.");
      }

      var ret = new LuaCoroutineThread(target, _backing);
      _target = new WeakReference<LuaCoroutineThread>(ret);
      _status = Status.Working;
      lock (_lock) {
        Monitor.Pulse(_lock);
      }
      return ret;
    }

    /// <summary>
    /// The method that is executed on the backing thread.
    /// </summary>
    void _execute() {
      while (true) {
        lock (_lock) {
          while (_status == Status.Waiting) {
            if (!Monitor.Wait(_lock, _maxWaitTime)) {
              _status = Status.Shutdown;
            }
          }

          if (_status == Status.Shutdown) {
            if (_owner != null)
              _owner._shutdownThread(this);
            return;
          }

          _target!.TryGetTarget(out LuaCoroutineThread? target);
          target!.Run();

          _status = Status.Waiting;
          _owner._doneWorking(this);
        }
      }
    }
  }
}
