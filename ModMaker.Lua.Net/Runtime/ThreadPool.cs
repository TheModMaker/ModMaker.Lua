using ModMaker.Lua.Runtime.LuaValues;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines the default thread pool.  This reduces the number of threads created by this
    /// framework.  This object must be disposed prior to closing the application or it will
    /// not close.  Do not store in a static variable.
    /// </summary>
    public sealed class ThreadPool
    {
        /// <summary>
        /// The minimum number of waiting threads.
        /// </summary>
        const int MIN_THREAD_COUNT = 5;
        /// <summary>
        /// The fraction of total threads that should be waiting.
        /// </summary>
        const double WAITING_THREAD_TARGET = 0.75;

        SortedList<int, WorkerThread> threads_ = new SortedList<int, WorkerThread>();
        Queue<WorkerThread> waitingThreads_ = new Queue<WorkerThread>();
        object lock_ = new object();
        bool disposed_ = false;
        ILuaEnvironment E_;

        /// <summary>
        /// Creates a new ThreadPool.
        /// </summary>
        public ThreadPool(ILuaEnvironment E)
        {
            E_ = E;
        }
        /// <summary>
        /// Releases the current instance.
        /// </summary>
        ~ThreadPool()
        {
            Dispose(false);
        }

        /// <summary>
        /// Creates a new LuaThread for the given action.
        /// </summary>
        /// <param name="action">The method to invoke.</param>
        /// <returns>A new LuaThread object that will invoke the given method.</returns>
        /// <exception cref="System.ArgumentNullException">If action is null.</exception>
        public ILuaThread Create(ILuaValue action)
        {
            lock (lock_)
            {
                CheckDisposed();
                ResizePool();
                WorkerThread thread = waitingThreads_.Dequeue();

                thread.DoWork(action);
                return thread.Target;
            }
        }
        /// <summary>
        /// Searches the factory for the thread that executes on the given ManagedThreadId.
        /// </summary>
        /// <param name="managedId">The ManagedThreadId of the thread to search.</param>
        /// <returns>The thread for that Id or null if not found.</returns>
        public LuaThread Search(int managedId)
        {
            lock (lock_)
            {
                CheckDisposed();

                WorkerThread worker;
                if (threads_.TryGetValue(managedId, out worker))
                    return worker.Target;
                else
                    return new LuaThreadNet();
            }
        }
        /// <summary>
        /// Called when a thread is done working.
        /// </summary>
        /// <param name="thread">The thread that is done working.</param>
        internal void DoneWorking(WorkerThread thread)
        {
            lock (lock_)
            {
                waitingThreads_.Enqueue(thread);
            }
        }
        /// <summary>
        /// Called when a thread shuts down.
        /// </summary>
        /// <param name="thread">The thread that is shutting down.</param>
        internal void ShutdownThread(WorkerThread thread)
        {
            lock (lock_)
            {
                threads_.Remove(thread.ID);

                // Search the waiting threads, remove the given thread can stop early because order
                // does not matter.
                int max = waitingThreads_.Count;
                for (int i = 0; i < max; i++)
                {
                    var temp = waitingThreads_.Dequeue();
                    if (temp == thread)
                        break;

                    waitingThreads_.Enqueue(temp);
                }
            }
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

            lock (lock_)
            {
                foreach (var item in threads_)
                {
                    item.Value.Dispose();
                }

                waitingThreads_.Clear();

                threads_ = null;
                waitingThreads_ = null;
            }
        }

        /// <summary>
        /// Resizes the thread pool to fit a new thread object and removes extra threads.
        /// </summary>
        void ResizePool()
        {
            lock (lock_)
            {
                // Check that we have at least one waiting thread.
                if (waitingThreads_.Count == 0)
                {
                    var temp = new WorkerThread(this, E_);
                    waitingThreads_.Enqueue(temp);
                    threads_.Add(temp.ID, temp);
                }

                // Remove extra threads.
                while (waitingThreads_.Count > (threads_.Count * WAITING_THREAD_TARGET + MIN_THREAD_COUNT))
                {
                    var temp = waitingThreads_.Dequeue();
                    threads_.Remove(temp.ID);
                    temp.Dispose();
                }
            }
        }
        /// <summary>
        /// Checks whether the object is disposed and throws an exception if it is.
        /// </summary>
        void CheckDisposed()
        {
            if (disposed_)
                throw new ObjectDisposedException(ToString());
        }
    }
}
