using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        /// The thread is suspended.  It is either called coroutine.yield, or it has not started
        /// yet.
        /// </summary>
        Suspended,
        /// <summary>
        /// The thread has completed execution.
        /// </summary>
        Complete,
    }

    /// <summary>
    /// Defines the event args for the OnYield event of a LuaThread.  This contains the arguments
    /// passed to Yield and the option of rejecting the yield and returning a given value.
    /// </summary>
    public sealed class YieldEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new YieldEventArgs object.
        /// </summary>
        /// <param name="args">The arguments given to Yield.</param>
        internal YieldEventArgs(ILuaMultiValue args)
        {
            this.Arguments = args;
            this.RejectYield = false;
            this.ReturnArguments = null;
        }

        /// <summary>
        /// Contains the arguments that were given to the Yield function.  This reference will be
        /// passed to Lua so any changes to these items will be reflected in Lua.
        /// </summary>
        public ILuaMultiValue Arguments;
        /// <summary>
        /// If RejectYield is true, this will be returned from the call to Yield.
        /// </summary>
        public ILuaMultiValue ReturnArguments;
        /// <summary>
        /// If set to true, then the current thread will not actually yield. Instead, it will
        /// simply return ReturnArguments.
        /// </summary>
        public bool RejectYield;
    }

    /// <summary>
    /// Defines a thread in Lua.  Cannot be created in C#, use the coroutine library in Lua
    /// to create a thread.  Threads in Lua execute synchronously.
    /// </summary>
    public interface ILuaThread : ILuaValue
    {
        /// <summary>
        /// Raised when this thread calls yield.  This is invoked prior to actually yielding and
        /// executes on the created thread.  This event, if handled, can also reject the yield or
        /// throw an exception to halt execution.
        /// </summary>
        event EventHandler<YieldEventArgs> OnYield;

        /// <summary>
        /// Gets the status of the thread.
        /// </summary>
        LuaThreadStatus Status { get; }
        /// <summary>
        /// Gets whether the thread was started by Lua.  If false, will throw an error if Resume is
        /// called or passed to the Lua coroutine library.
        /// </summary>
        bool IsLua { get; }
        
        /// <summary>
        /// Suspends the calling thread to allow the thread for this object to continue.
        /// </summary>
        /// <param name="args">The arguments to pass to the thread.</param>
        /// <returns>The values returned from the thread.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// If the thread is running or dead -or- if this is not a Lua thread.
        /// </exception>
        /// <exception cref="System.Reflection.TargetInvocationException">
        /// If the thread throws an exception.
        /// </exception>
        ILuaMultiValue Resume(ILuaMultiValue args);
        /// <summary>
        /// Must be called from the thread that is managed by this instance.  This suspends the
        /// current thread and causes the call to Resume to return the given values.  If Resume
        /// is called again, this method will return with the values given to that call.
        /// </summary>
        /// <param name="args">The arguments to return from Resume.</param>
        /// <returns>The objects passed to Resume.</returns>
        /// <exception cref="System.InvalidOperationException">
        /// If the thread is not already running -or- if this is not a Lua thread.
        /// </exception>
        ILuaMultiValue Yield(ILuaMultiValue args);
    }
}