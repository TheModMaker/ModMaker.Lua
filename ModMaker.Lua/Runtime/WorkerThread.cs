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

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Defines a helper thread used for LuaThread objects.  This will execute multiple LuaThread
  /// objects over its lifetime.
  /// </summary>
  sealed class WorkerThread : IDisposable {
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
    readonly ILuaEnvironment _env;
    readonly ThreadPool _owner;
    bool _disposed = false;
    Status _status;
    Thread _backing;

    /// <summary>
    /// Creates a new WorkerThread object.
    /// </summary>
    /// <param name="owner">The factory that created this object.</param>
    /// <param name="env">The current environment.</param>
    public WorkerThread(ThreadPool owner, ILuaEnvironment env) {
      _status = Status.Waiting;
      _owner = owner;
      _env = env;
      _backing = new Thread(_execute);
      _backing.IsBackground = true;
      _backing.Start();
    }
    ~WorkerThread() {
      _dispose(false);
    }

    /// <summary>
    /// Gets the current target of the thread.
    /// </summary>
    public LuaThreadNet Target { get; private set; }
    /// <summary>
    /// Gets the ID for the worker thread.
    /// </summary>
    public int ID { get { return _backing.ManagedThreadId; } }

    /// <summary>
    /// Makes the current thread execute the given method.
    /// </summary>
    /// <param name="target">The method to execute.</param>
    public void DoWork(ILuaValue target) {
      if (_status != Status.Waiting) {
        throw new InvalidOperationException(
            "The worker thread must be waiting to get a new task.");
      }

      Target = new LuaThreadNet(_env, _backing, target);
      _status = Status.Working;
      lock (_lock) {
        Monitor.Pulse(_lock);
      }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting
    /// unmanaged resources.
    /// </summary>
    public void Dispose() {
      if (!_disposed) {
        _disposed = true;
        GC.SuppressFinalize(this);
        _dispose(true);
      }
    }
    void _dispose(bool disposing) {
      if (!disposing) {
        return;
      }

      if (Target != null) {
        Target.Dispose();
      }

      Target = null;

      if (_backing != null) {
        if (_status == Status.Working) {
          _backing.Abort();
        } else {
          _status = Status.Shutdown;
          lock (_lock) {
            Monitor.Pulse(_lock);
          }
        }
        _backing.Join();
      }
      _backing = null;
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
            _owner._shutdownThread(this);
            return;
          }

          Target._do();

          _status = Status.Waiting;
          _owner._doneWorking(this);
        }
      }
    }
  }
}
