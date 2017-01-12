using ModMaker.Lua.Runtime.LuaValues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines a helper thread used for LuaThread objects.  This will execute multiple LuaThread
    /// objects over its lifetime.
    /// </summary>
    sealed class WorkerThread : IDisposable
    {
        /// <summary>
        /// The max wait time in miliseconds to wait for a new task.
        /// </summary>
        const int MAX_WAIT_TIME = 1000;

        /// <summary>
        /// Contains the status of the worker thread.
        /// </summary>
        enum Status
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

        object lock_ = new object();
        bool disposed_ = false;
        ILuaEnvironment E_;
        ThreadPool owner_;
        Status status_;
        Thread backing_;

        /// <summary>
        /// Creates a new WorkerThread object.
        /// </summary>
        /// <param name="owner">The factory that created this object.</param>
        /// <param name="E">The current environment.</param>
        public WorkerThread(ThreadPool owner, ILuaEnvironment E)
        {
            status_ = Status.Waiting;
            owner_ = owner;
            E_ = E;
            backing_ = new Thread(Execute);
            backing_.IsBackground = true;
            backing_.Start();
        }
        ~WorkerThread()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the current target of the thread.
        /// </summary>
        public LuaThreadNet Target { get; private set; }
        /// <summary>
        /// Gets the ID for the worker thread.
        /// </summary>
        public int ID { get { return backing_.ManagedThreadId; } }

        /// <summary>
        /// Makes the current thread execute the given method.
        /// </summary>
        /// <param name="target">The method to execute.</param>
        public void DoWork(ILuaValue target)
        {
            if (status_ != Status.Waiting)
            {
                throw new InvalidOperationException(
                    "The worker thread must be waiting to get a new task.");
            }

            Target = new LuaThreadNet(E_, backing_, target);
            status_ = Status.Working;
            lock (lock_)
                Monitor.Pulse(lock_);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, 
        /// releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!disposed_)
            {
                disposed_ = true;
                GC.SuppressFinalize(this);
                Dispose(true);
            }
        }
        void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            if (Target != null)
                Target.Dispose();
            Target = null;

            if (backing_ != null)
            {
                if (status_ == Status.Working)
                    backing_.Abort();
                else
                {
                    status_ = Status.Shutdown;
                    lock (lock_)
                        Monitor.Pulse(lock_);
                }
                backing_.Join();
            }
            backing_ = null;
        }

        /// <summary>
        /// The method that is executed on the backing thread.
        /// </summary>
        void Execute()
        {
            while (true)
            {
                lock (lock_)
                {
                    while (status_ == Status.Waiting)
                    {
                        if (!Monitor.Wait(lock_, MAX_WAIT_TIME))
                            status_ = Status.Shutdown;
                    }

                    if (status_ == Status.Shutdown)
                    {
                        owner_.ShutdownThread(this);
                        return;
                    }

                    Target.Do();

                    status_ = Status.Waiting;
                    owner_.DoneWorking(this);
                }
            }
        }
    }
}
