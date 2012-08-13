using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ModMaker.Lua.Runtime
{
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
    /// Defines a thread in Lua.  Cannot be created in c#, use the coroutine library in Lua
    /// to create a thread.  Threads in Lua execute synchronously.
    /// </summary>
    [LuaIgnore]
    public sealed class LuaThread
    {
        internal static Dictionary<int, LuaThread> _cache = new Dictionary<int, LuaThread>();

        object _signal = new object();
        object[] _com = null;
        Exception E = null;
        LuaMethod meth;
        Thread thread;

        /// <summary>
        /// Gets the current status of the thread.
        /// </summary>
        public LuaThreadStatus Status { get; internal set; }
        /// <summary>
        /// Gets whether the thread was started by Lua.  If false, will throw an error if
        /// Resume is called or passed to the Lua coroutine library.
        /// </summary>
        public bool IsLua { get; private set; }

        internal LuaThread(LuaMethod meth)
        {
            this.IsLua = true;
            this.meth = meth;
            this.Status = LuaThreadStatus.Suspended;
            thread = new Thread(Do);

            lock (_cache)
            {
                _cache[thread.ManagedThreadId] = this;
            }
        }
        internal LuaThread(Thread thread)
        {
            this.IsLua = false;
            this.Status = LuaThreadStatus.Suspended;
            this.thread = thread;

            lock (_cache)
            {
                _cache[thread.ManagedThreadId] = this;
            }
        }
        /// <summary>
        /// Disposes the Thread and stops the underlying thread.
        /// </summary>
        ~LuaThread()
        {
            if (thread.ThreadState != ThreadState.Stopped && IsLua)
            {
                thread.Abort();
                thread.Join();
                thread = null;
            }
        }

        /// <summary>
        /// Resumes execution of the thread.  This is the same as calling coroutine.resume in Lua.
        /// </summary>
        /// <param name="args">Any arguments to pass.</param>
        /// <returns>The values returned from the function or passed to coroutine.yield.</returns>
        public object[] Resume(params object[] args)
        {
            object[] o = ResumeInternal(args);
            if (o[0] as bool? == false)
                throw (Exception)o[2];
            return o;
        }

        internal object[] Yield(object[] args)
        {
            if (!IsLua)
                throw new InvalidOperationException("Cannot yield a thread that has been created outside Lua.");

            lock (this)
            {
                _com = (object[])args.Clone();
                this.Status = LuaThreadStatus.Suspended;
            }

            Thread.Sleep(100);
            lock (_signal)
            {
                Monitor.Pulse(_signal);
                Monitor.Wait(_signal);
            }

            return (object[])_com.Clone();
        }
        internal object[] ResumeInternal(object[] args)
        {
            if (!IsLua)
                return new object[] { false, "Cannot resume a thread that has been created outside Lua.", 
                    new InvalidOperationException("Cannot resume a thread that has been created outside Lua.") };

            lock (this)
            {
                _com = (object[])args.Clone();
                this.Status = LuaThreadStatus.Running;
            }

            if (thread.ThreadState == ThreadState.Unstarted)
            {
                thread.Start();

                Thread.Sleep(100);
                lock (_signal)
                    Monitor.Wait(_signal);
            }
            else if (thread.ThreadState == ThreadState.Stopped)
            {
                this.Status = LuaThreadStatus.Dead;
                return new object[] { false, "cannot resume dead coroutine", new InvalidOperationException("Cannot resume dead coroutine.") };
            }
            else
            {
                lock (_signal)
                {
                    Monitor.Pulse(_signal);
                    Monitor.Wait(_signal);
                }
            }

            Thread.Sleep(100);

            lock (this)
            {
                this.Status = (thread.ThreadState == ThreadState.Stopped ? LuaThreadStatus.Dead : LuaThreadStatus.Suspended);
                if (this.E == null)
                    return new object[] { true }.Then((object[])_com.Clone()).ToArray();
                else
                    return new object[] { false, this.E.Message, this.E };
            }
        }

        void Do()
        {
            try
            {
                var ret = meth.InvokeInternal((object[])_com.Clone(), -1);
                object[] ar = ret.ToArray();

                lock (this)
                    this._com = ar;
            }
            catch (Exception e)
            {
                lock (this)
                    this.E = e;
            }
            finally
            {
                lock (_signal)
                    Monitor.Pulse(_signal);
            }
        }
    }
}
