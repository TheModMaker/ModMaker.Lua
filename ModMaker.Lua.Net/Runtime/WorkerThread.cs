using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines a helper thread used for LuaThread objects.  This will
    /// execute multiple LuaThread objects over its lifetime.
    /// </summary>
    sealed class WorkerThread : IDisposable
    {
        /// <summary>
        /// Contains the status of the worker thread.
        /// </summary>
        public enum Status
        {
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

        object _lock = new object();
        bool _disposed = false;
        Status _status;
        Thread _backing;
        LuaThread _target;
        ThreadFactory _owner;

        /// <summary>
        /// Creates a new WorkerThread object.
        /// </summary>
        /// <param name="owner">The factory that created this object.</param>
        public WorkerThread(ThreadFactory/*!*/ owner)
        {
            this._backing = new Thread(Execute);
            this._backing.IsBackground = true;
            this._backing.Start();
            this._status = Status.Waiting;
            this._owner = owner;
        }
        ~WorkerThread()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the current target of the thread.
        /// </summary>
        public LuaThread Target { get { return _target; } }
        /// <summary>
        /// Gets the current status of the thread.
        /// </summary>
        public Status CurrentStatus { get { return _status; } }
        /// <summary>
        /// Gets the ID for the worker thread.
        /// </summary>
        public int ID { get { return _backing.ManagedThreadId; } }

        /// <summary>
        /// Makes the current thread execute the given method.
        /// </summary>
        /// <param name="target">The method to execute.</param>
        public void DoWork(IMethod/*!*/ target)
        {
            if (_status != Status.Waiting)
                throw new InvalidOperationException(
                    "The worker thread must be waiting to get a new task.");

            _target = new LuaThread(_backing, target);
            _status = Status.Working;
            lock (_lock)
                Monitor.Pulse(_lock);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, 
        /// releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }
        void Dispose(bool disposing)
        {
            if (disposing && _target != null)
                _target.Dispose();
            _target = null;

            if (disposing && _backing != null)
            {
                if (_status == Status.Working)
                    _backing.Abort();
                else
                {
                    _status = Status.Shutdown;
                    lock (_lock)
                        Monitor.Pulse(_lock);
                }
                _backing.Join();
            }
            _backing = null;
        }

        /// <summary>
        /// The method that is executed on the backing thread.
        /// </summary>
        void Execute()
        {
            while (true)
            {
                lock (_lock)
                {
                    while (_status == Status.Waiting)
                        if (!Monitor.Wait(_lock, 1000))
                            _status = Status.Shutdown;

                    if (_status == Status.Shutdown)
                    {
                        _owner.ShutdownThread(this);
                        return;
                    }

                    _target.Do();

                    _status = Status.Waiting;
                    _owner.DoneWorking(this);
                }
            }
        }
    }
}
