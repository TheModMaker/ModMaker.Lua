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

using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// Determines the state of a Lua thread.
  /// </summary>
  public enum LuaCoroutineStatus {
    /// <summary>
    /// The thread is currently running.  This means that the C# code was called from this thread
    /// object.
    /// </summary>
    Running,
    /// <summary>
    /// The thread is suspended.  It is either called coroutine.yield, or it has not started yet.
    /// </summary>
    Yielded,
    /// <summary>
    /// The thread has called coroutine.resume and is waiting for the thread to stop.
    /// </summary>
    Waiting,
    /// <summary>
    /// The thread has completed execution.
    /// </summary>
    Complete,
  }

  /// <summary>
  /// Defines the implementation of a Lua coroutine.
  /// </summary>
  public interface ILuaCoroutineImpl {
    /// <summary>
    /// Gets the status of the thread.
    /// </summary>
    LuaCoroutineStatus Status { get; }

    /// <summary>
    /// Suspends the calling thread to allow the thread for this object to continue.
    /// </summary>
    /// <param name="args">The arguments to pass to the thread.</param>
    /// <returns>The values returned from the thread.</returns>
    LuaMultiValue Resume(LuaMultiValue args);
    /// <summary>
    /// Must be called from the thread that is managed by this instance.  This suspends the
    /// current thread and causes the call to Resume to return the given values.  If Resume
    /// is called again, this method will return with the values given to that call.
    /// </summary>
    /// <param name="args">The arguments to return from Resume.</param>
    /// <returns>The objects passed to Resume.</returns>
    LuaMultiValue Yield(LuaMultiValue args);

    /// <summary>
    /// Indicates the Lua object has been freed and the coroutine should shutdown.
    /// </summary>
    void Shutdown();
  }
}
