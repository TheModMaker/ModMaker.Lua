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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines a thread in Lua.  Cannot be created in C#, use the coroutine library in Lua
    /// to create a thread.  Threads in Lua execute synchronously.
    /// </summary>
    public sealed class LuaThreadNet : LuaThread
    {
        object handle_ = new object();
        Exception exception_ = null;
        ILuaEnvironment E_;
        ILuaMultiValue args_;
        ILuaValue method_;
        Thread backing_;
        bool releaseBacking_;

        /// <summary>
        /// Creates a new LuaThread object that represents the main thread.
        /// </summary>
        internal LuaThreadNet()
        {
            Status = LuaThreadStatus.Running;
            IsLua = false;
            releaseBacking_ = false;
        }
        /// <summary>
        /// Creates a new LuaThread object that calls the given method.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="method">The method to invoke.</param>
        internal LuaThreadNet(ILuaEnvironment E, ILuaValue method)
        {
            this.IsLua = true;
            this.E_ = E;
            this.method_ = method;
            this.backing_ = new Thread(Do);
            this.releaseBacking_ = true;
        }
        /// <summary>
        /// Creates a new LuaThread object that calls the given method and
        /// executes on the given thread.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <param name="method">The method to invoke.</param>
        /// <param name="thread">The thread that will execute this thread.</param>
        internal LuaThreadNet(ILuaEnvironment E, Thread thread, ILuaValue method)
        {
            this.IsLua = true;
            this.E_ = E;
            this.method_ = method;
            this.backing_ = thread;
            this.releaseBacking_ = false;
        }

        /// <summary>
        /// Suspends the current thread to allow the wating thread to execute.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the thread
        /// is running or dead -or- if this is not a Lua thread.</exception>
        /// <exception cref="System.Reflection.TargetInvocationException">If
        /// the thread throws an exception.</exception>
        public override ILuaMultiValue Resume(ILuaMultiValue args)
        {
            lock (handle_)
            {
                if (!IsLua)
                {
                    throw new InvalidOperationException(
                        "Cannot resume a thread that has been created outside Lua.");
                }
                if (Status == LuaThreadStatus.Running)
                {
                    throw new InvalidOperationException(
                        "Cannot resume a running thread.");
                }
                if (Status == LuaThreadStatus.Complete)
                {
                    throw new InvalidOperationException(
                        "Cannot resume a dead thread.");
                }

                if (backing_.ThreadState == ThreadState.Unstarted)
                {
                    backing_.Start();
                }

                args = args ?? E_.Runtime.CreateMultiValue();
                args_ = args;
                Status = LuaThreadStatus.Running;

                Monitor.Pulse(handle_);
                while (Status == LuaThreadStatus.Running)
                {
                    Monitor.Wait(handle_);
                }

                if (exception_ != null)
                    throw new TargetInvocationException(exception_);

                ILuaMultiValue ret = Interlocked.Exchange(ref args_, null);
                return ret ?? E_.Runtime.CreateMultiValue();
            }
        }
        /// <summary>
        /// Yields the calling thread.
        /// </summary>
        /// <param name="args">The arguments to return from Resume.</param>
        /// <returns>The objects passed to Resume.</returns>
        /// <exception cref="System.InvalidOperationException">If the thread
        /// is not already running -or- if this is not a Lua thread.</exception>
        public override ILuaMultiValue Yield(ILuaMultiValue args)
        {
            lock (handle_)
            {
                if (!IsLua)
                {
                    throw new InvalidOperationException(
                        "Cannot resume a thread that has been created outside Lua.");
                }
                if (Status != LuaThreadStatus.Running)
                    throw new InvalidOperationException("Thread must be running to yield.");

                // Fire the OnYield event.
                var e = new YieldEventArgs(args);
                CallOnYield(e);

                // If the yield is rejected, simply return the arguments.
                if (e.RejectYield)
                    return e.ReturnArguments;

                args = args ?? E_.Runtime.CreateMultiValue();
                args_ = args;
                Status = LuaThreadStatus.Suspended;

                Monitor.Pulse(handle_);
                while (Status != LuaThreadStatus.Running)
                {
                    Monitor.Wait(handle_);
                }

                ILuaMultiValue ret = Interlocked.Exchange(ref args_, null);
                return ret ?? E_.Runtime.CreateMultiValue();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or
        /// resetting unmanaged resources.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (releaseBacking_)
                {
                    backing_.Abort();
                    backing_.Join();
                }
                backing_ = null;
            }
        }

        internal void Do()
        {
            try
            {
                lock (handle_)
                {
                    while (Status != LuaThreadStatus.Running)
                    {
                        Monitor.Wait(handle_);
                    }
                }

                ILuaMultiValue args = Interlocked.Exchange(ref args_, null);
                ILuaMultiValue ret = method_.Invoke(LuaNil.Nil, false, -1, args);

                lock (handle_)
                {
                    args_ = ret;
                    exception_ = null;
                    Status = LuaThreadStatus.Complete;
                    Monitor.Pulse(handle_);
                }
            }
            catch (Exception e)
            {
                lock (handle_)
                {
                    exception_ = e;
                    Status = LuaThreadStatus.Complete;
                    Monitor.Pulse(handle_);
                }
            }
        }
    }
}
