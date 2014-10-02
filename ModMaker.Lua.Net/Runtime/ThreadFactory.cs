using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines a factory that creates LuaThread objects.  The default factory
    /// uses a thread pool to reuse threads rather than creating new ones.
    /// </summary>
    public interface IThreadFactory : IDisposable
    {
        /// <summary>
        /// Creates a new LuaThread for the given action.
        /// </summary>
        /// <param name="method">The method to invoke.</param>
        /// <returns>A new LuaThread object that will invoke the given method.</returns>
        /// <exception cref="System.ArgumentNullException">If method is null.</exception>
        LuaThread Create(IMethod method);
        /// <summary>
        /// Searches the factory for the thread that executes on the given
        /// ManagedThreadId.
        /// </summary>
        /// <param name="managedId">The ManagedThreadId of the thread to search.</param>
        /// <returns>The thread for that Id or null if not found.</returns>
        LuaThread Search(int managedId);
    }

    /// <summary>
    /// Defines the default thread factory.  This uses a thread pool to reduce
    /// the number of threads created by this framework.  This object must be
    /// disposed prior to closing the application or it will not close.  Do not
    /// store in a static variable.
    /// </summary>
    [LuaIgnore]
    public sealed class ThreadFactory : IThreadFactory
    {
        /// <summary>
        /// True to use a thread pool, otherwise create a new thread for
        /// each new LuaThread.
        /// </summary>
        public static bool UseThreadPool = true;
        /// <summary>
        /// Contains all the active threads.
        /// </summary>
        SortedList<int, WorkerThread> _threads = new SortedList<int, WorkerThread>();
        /// <summary>
        /// Contains the threads that are currently waiting.
        /// </summary>
        Queue<WorkerThread> _waitingThreads = new Queue<WorkerThread>();
        /// <summary>
        /// A locking object for the class.
        /// </summary>
        object _lock = new object();
        /// <summary>
        /// Contains whether the object has been disposed.
        /// </summary>
        bool _disposed = false;

        /// <summary>
        /// Releases the current instance.
        /// </summary>
        ~ThreadFactory()
        {
            Dispose(false);
        }

        /// <summary>
        /// Creates a new LuaThread for the given action.
        /// </summary>
        /// <param name="action">The method to invoke.</param>
        /// <returns>A new LuaThread object that will invoke the given method.</returns>
        /// <exception cref="System.ArgumentNullException">If action is null.</exception>
        public LuaThread Create(IMethod action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            if (!UseThreadPool)
                return new LuaThread(action);

            lock (_lock)
            {
                CheckDisposed();
                ResizePool();
                WorkerThread thread = _waitingThreads.Dequeue();

                thread.DoWork(action);
                return thread.Target;
            }
        }
        /// <summary>
        /// Searches the factory for the thread that executes on the given
        /// ManagedThreadId.
        /// </summary>
        /// <param name="managedId">The ManagedThreadId of the thread to search.</param>
        /// <returns>The thread for that Id or null if not found.</returns>
        public LuaThread Search(int managedId)
        {
            lock (_lock)
            {
                CheckDisposed();

                if (_threads.ContainsKey(managedId))
                    return _threads[managedId].Target;
                else
                    return null;
            }
        }
        /// <summary>
        /// Called when a thread is done working.
        /// </summary>
        /// <param name="thread">The thread that is done working.</param>
        internal void DoneWorking(WorkerThread thread)
        {
            lock (_lock)
            {
                _waitingThreads.Enqueue(thread);
            }
        }
        /// <summary>
        /// Called when a thread shuts down.
        /// </summary>
        /// <param name="thread">The thread that is shutting down.</param>
        internal void ShutdownThread(WorkerThread thread)
        {
            lock (_lock)
            {
                _threads.Remove(thread.ID);

                // search the waiting threads, remove the given thread
                // can stop early because order does not matter
                int max = _waitingThreads.Count;
                for (int i = 0; i < max; i++)
                {
                    var temp = _waitingThreads.Dequeue();
                    if (temp == thread)
                        break;

                    _waitingThreads.Enqueue(temp);
                }
            }
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
            lock (_lock)
            {
                if (disposing)
                {
                    foreach (var item in _threads)
                    {
                        item.Value.Dispose();
                    }

                    _waitingThreads.Clear();
                }

                _threads = null;
                _waitingThreads = null;
            }
        }

        /// <summary>
        /// Resizes the thread pool to fit a new thread object and
        /// removes extra threads.
        /// </summary>
        void ResizePool()
        {
            lock (_lock)
            {
                // check that we have at least one waiting thread
                if (_waitingThreads.Count == 0)
                {
                    var temp = new WorkerThread(this);
                    _waitingThreads.Enqueue(temp);
                    _threads.Add(temp.ID, temp);
                }

                // remove extra threads
                while (_waitingThreads.Count > (_threads.Count * 0.75 + 5))
                {
                    var temp = _waitingThreads.Dequeue();
                    _threads.Remove(temp.ID);
                    temp.Dispose();
                }
            }
        }
        /// <summary>
        /// Checks whether the object is disposed and throws an exception if it is.
        /// </summary>
        void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(ToString());
        }
    }
}
