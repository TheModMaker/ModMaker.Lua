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

using System.Collections.Generic;

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Defines the default thread pool.  This reduces the number of threads created by this
  /// framework.  This object must be disposed prior to closing the application or it will
  /// not close.  Do not store in a static variable.
  /// </summary>
  sealed class ThreadPool {
    /// <summary>
    /// The minimum number of waiting threads.
    /// </summary>
    const int _minThreadCount = 5;
    /// <summary>
    /// The fraction of total threads that should be waiting.
    /// </summary>
    const double _waitingThreadTarget = 0.75;

    readonly HashSet<WorkerThread> _threads = new HashSet<WorkerThread>();
    readonly Queue<WorkerThread> _waitingThreads = new Queue<WorkerThread>();
    readonly object _lock = new object();

    /// <summary>
    /// Creates a new ThreadPool.
    /// </summary>
    public ThreadPool() { }

    /// <summary>
    /// Creates a new LuaThread for the given action.
    /// </summary>
    /// <param name="action">The method to invoke.</param>
    /// <returns>A new LuaThread object that will invoke the given method.</returns>
    public ILuaCoroutineImpl Create(ILuaValue action) {
      lock (_lock) {
        _resizePool();
        WorkerThread thread = _waitingThreads.Dequeue();

        return thread.DoWork(action);
      }
    }
    /// <summary>
    /// Called when a thread is done working.
    /// </summary>
    /// <param name="thread">The thread that is done working.</param>
    internal void _doneWorking(WorkerThread thread) {
      lock (_lock) {
        _waitingThreads.Enqueue(thread);
      }
    }
    /// <summary>
    /// Called when a thread shuts down.
    /// </summary>
    /// <param name="thread">The thread that is shutting down.</param>
    internal void _shutdownThread(WorkerThread thread) {
      lock (_lock) {
        _threads.Remove(thread);

        // Search the waiting threads, remove the given thread.  Can stop early because order
        // does not matter.
        int max = _waitingThreads.Count;
        for (int i = 0; i < max; i++) {
          var temp = _waitingThreads.Dequeue();
          if (temp == thread) {
            break;
          }

          _waitingThreads.Enqueue(temp);
        }
      }
    }

    /// <summary>
    /// Resizes the thread pool to fit a new thread object and removes extra threads.
    /// </summary>
    void _resizePool() {
      lock (_lock) {
        // Check that we have at least one waiting thread.
        if (_waitingThreads.Count == 0) {
          var temp = new WorkerThread(this);
          _waitingThreads.Enqueue(temp);
          _threads.Add(temp);
        }

        // Remove extra threads.
        while (_waitingThreads.Count > (_threads.Count * _waitingThreadTarget + _minThreadCount)) {
          var temp = _waitingThreads.Dequeue();
          _threads.Remove(temp);
        }
      }
    }
  }
}
