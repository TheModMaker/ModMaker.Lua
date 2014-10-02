using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines the event args for the OnYield event of a LuaThread.  This 
    /// contains the arguments passed to Yield and the option of rejecting
    /// the yield and returning a given value.
    /// </summary>
    public sealed class YieldEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new YieldEventArgs object.
        /// </summary>
        /// <param name="args">The arguments given to Yield.</param>
        internal YieldEventArgs(object[]/*!*/ args)
        {
            this.Arguments = args;
            this.RejectYield = false;
            this.ReturnArguments = null;
        }

        /// <summary>
        /// Contains the arguments that were given to the Yield function.  This
        /// reference will be passed to Lua so any changes to these items will
        /// be reflected in Lua.
        /// </summary>
        public object[] Arguments;
        /// <summary>
        /// If RejectYield is true, this will be returned from the call to Yield.
        /// </summary>
        public object[] ReturnArguments;
        /// <summary>
        /// If set to true, then the current thread will not actually yield.
        /// Instead, it will simply return ReturnArguments.
        /// </summary>
        public bool RejectYield;
    }

    /// <summary>
    /// Determines the state of a Lua thread.
    /// </summary>
    public enum LuaThreadStatus
    {
        /// <summary>
        /// The thread is currently running.  This means that the C# code was called from
        /// this thread object.
        /// </summary>
        Running,
        /// <summary>
        /// The thread is suspended.  It is either called coroutine.yield, or it has
        /// not started yet.
        /// </summary>
        Suspended,
        /// <summary>
        /// The thread is waiting on another thread to complete.
        /// </summary>
        Normal,
        /// <summary>
        /// The thread has completed execution.
        /// </summary>
        Dead,
    }

    /// <summary>
    /// Defines a thread in Lua.  Cannot be created in C#, use the coroutine library in Lua
    /// to create a thread.  Threads in Lua execute synchronously.
    /// </summary>
    [LuaIgnore]
    public sealed class LuaThread : IDisposable
    {
        Thread _backing;
        bool _releaseBacking;
        object _handle = new object();
        Exception _e = null;
        IMethod _method;
        LuaThreadStatus _flag = LuaThreadStatus.Suspended;
        bool _disposed = false;
        object[] _args;

        /// <summary>
        /// Creates a new LuaThread object that represents a C# thread.
        /// </summary>
        internal LuaThread()
        {
            this._flag = LuaThreadStatus.Running;
            this.IsLua = false;
            this._releaseBacking = false;
        }
        /// <summary>
        /// Creates a new LuaThread object that calls the given method.
        /// </summary>
        /// <param name="method">The method to invoke.</param>
        internal LuaThread(IMethod/*!*/ method)
        {
            this.IsLua = true;
            this._method = method;
            this._backing = new Thread(Do);
            this._releaseBacking = true;
        }
        /// <summary>
        /// Creates a new LuaThread object that calls the given method and
        /// executes on the given thread.
        /// </summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="thread">The thread that will execute this thread.</param>
        internal LuaThread(Thread/*!*/ thread, IMethod/*!*/ method)
        {
            this.IsLua = true;
            this._method = method;
            this._backing = thread;
            this._releaseBacking = false;

        }
        /// <summary>
        /// Releases the current instance.
        /// </summary>
        ~LuaThread()
        {
            Dispose(false);
        }

        /// <summary>
        /// Raised when this thread calls yield.  This is invoked prior to
        /// actually yielding and executes on the created thread.  This event,
        /// if handled, can also reject the yield or throw an exception to
        /// halt execution.
        /// </summary>
        public event EventHandler<YieldEventArgs> OnYield;
        /// <summary>
        /// Gets the status of the thread.
        /// </summary>
        public LuaThreadStatus Status { get { return _flag; } }
        /// <summary>
        /// Gets whether the thread was started by Lua.  If false, will throw 
        /// an error if Resume is called or passed to the Lua coroutine library.
        /// </summary>
        public bool IsLua { get; private set; }

        /// <summary>
        /// Suspends the current thread to allow the wating thread to execute.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the thread
        /// is running or dead -or- if this is not a Lua thread.</exception>
        /// <exception cref="System.Reflection.TargetInvocationException">If
        /// the thread throws an exception.</exception>
        public object[] Resume(params object[] args)
        {
            lock (_handle)
            {
                if (!IsLua)
                    throw new InvalidOperationException(
                        "Cannot resume a thread that has been created outside Lua.");
                if (_flag == LuaThreadStatus.Running)
                    throw new InvalidOperationException(
                        "Cannot resume a running thread.");
                if (_flag == LuaThreadStatus.Dead)
                    throw new InvalidOperationException(
                        "Cannot resume a dead thread.");
                args = args ?? new object[0];

                if (_backing.ThreadState == ThreadState.Unstarted)
                {
                    _backing.Start();
                }

                _args = (object[])args.Clone();
                _flag = LuaThreadStatus.Running;

                Monitor.Pulse(_handle);

                while (_flag == LuaThreadStatus.Running)
                {
                    Monitor.Wait(_handle);
                }

                if (_e != null)
                    throw new TargetInvocationException(_e);

                object[] ret = _args ?? new object[0];
                _args = null;
                return (object[])ret.Clone();
            }
        }
        /// <summary>
        /// Yields the calling thread.
        /// </summary>
        /// <param name="args">The arguments to return from Resume.</param>
        /// <returns>The objects passed to Resume.</returns>
        /// <exception cref="System.InvalidOperationException">If the thread
        /// is not already running -or- if this is not a Lua thread.</exception>
        internal object[] DoYield(object[] args)
        {
            lock (_handle)
            {
                if (!IsLua)
                    throw new InvalidOperationException(
                        "Cannot resume a thread that has been created outside Lua.");
                if (_flag != LuaThreadStatus.Running)
                    throw new InvalidOperationException("Thread must be running to yield.");
                args = args ?? new object[0];

                if (OnYield != null)
                {
                    // fire the OnYield event.
                    var E = new YieldEventArgs(args);
                    OnYield(this, E);

                    // if the yield is rejected, simply return the arguments.
                    if (E.RejectYield)
                        return E.ReturnArguments;
                }

                _args = (object[])args.Clone();
                _flag = LuaThreadStatus.Suspended;

                Monitor.Pulse(_handle);

                while (_flag != LuaThreadStatus.Running)
                {
                    Monitor.Wait(_handle);
                }

                object[] temp = _args ?? new object[0];
                _args = null;
                return (object[])temp.Clone();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, 
        /// releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (_releaseBacking)
                {
                    _backing.Abort();
                    _backing.Join();
                }
                _backing = null;
            }
        }

        internal void Do()
        {
            try
            {
                lock (_handle)
                {
                    while (_flag != LuaThreadStatus.Running)
                    {
                        Monitor.Wait(_handle);
                    }
                }

                var args = _args ?? new object[0];
                _args = null; // make sure that they are not repeated
                var ret = _method.Invoke(null, args);

                lock (_handle)
                {
                    this._args = (object[])(ret.Values ?? new object[0]).Clone();
                    this._e = null;
                    this._flag = LuaThreadStatus.Dead;
                    Monitor.Pulse(_handle);
                }
            }
            catch (Exception e)
            {
                lock (_handle)
                {
                    this._e = e;
                    this._flag = LuaThreadStatus.Dead;
                    Monitor.Pulse(_handle);
                }
            }
        }
    }
}
