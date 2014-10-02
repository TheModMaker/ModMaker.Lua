using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections;
using System.Reflection;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        /// <summary>
        /// Contains the coroutine framework functions.
        /// </summary>
        static class Coroutine
        {
            public static ILuaTable Initialize(ILuaEnvironment E)
            {
                ILuaTable coroutine = new LuaTableNet();
                coroutine.SetItemRaw("create", new create(E));
                coroutine.SetItemRaw("resume", new resume(E));
                coroutine.SetItemRaw("running", new running(E));
                coroutine.SetItemRaw("status", new status(E));
                coroutine.SetItemRaw("wrap", new wrap(E));
                coroutine.SetItemRaw("yield", new yield(E));

                return coroutine;
            }

            sealed class WrapHelper : LuaFrameworkMethod
            {
                LuaThread thread;

                public WrapHelper(ILuaEnvironment E, LuaThread thread)
                    : base(E, "wrap")
                {
                    this.thread = thread;
                }
                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    object[] obj = thread.Resume(args.Cast<object>().ToArray());
                    return new MultipleReturn(obj);
                }
            }

            sealed class create : LuaFrameworkMethod
            {
                public create(ILuaEnvironment E) : base(E, "coroutine.create") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'coroutine.create'.");

                    IMethod meth = args[0] as IMethod;
                    if (meth == null)
                        throw new ArgumentException("First argument to function 'coroutine.create' must be a function.");
                    if (!(Environment is ILuaEnvironmentNet))
                        throw new InvalidOperationException("Coroutines only work with the NET version of the environment.");

                    return new MultipleReturn(((ILuaEnvironmentNet)Environment).ThreadFactory.Create(meth));
                }
            }
            sealed class resume : LuaFrameworkMethod
            {
                public resume(ILuaEnvironment E) : base(E, "coroutine.resume") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'coroutine.resume'.");

                    LuaThread meth = args[0] as LuaThread;
                    if (meth == null)
                        throw new ArgumentException("First argument to function 'coroutine.resume' must be a thread.");

                    try
                    {
                        return new MultipleReturn(new object[] { true }.Then(meth.Resume(args.Cast<object>().Where((o, i) => i > 0).ToArray())));
                    }
                    catch (Exception e)
                    {
                        if (e.Message == "Cannot resume a dead thread.")
                            return new MultipleReturn(false, "cannot resume dead coroutine");
                        else
                            return new MultipleReturn(false, e.Message, e);
                    }
                }
            }
            sealed class running : LuaFrameworkMethod
            {
                public running(ILuaEnvironment E) : base(E, "coroutine.running") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (!(Environment is ILuaEnvironmentNet))
                        throw new InvalidOperationException("Coroutines only work with the NET version of the environment.");
                    LuaThread t = ((ILuaEnvironmentNet)Environment).ThreadFactory.Search(Thread.CurrentThread.ManagedThreadId);
                    t = t ?? new LuaThread();

                    return new MultipleReturn(t, !t.IsLua);
                }
            }
            sealed class status : LuaFrameworkMethod
            {
                public status(ILuaEnvironment E) : base(E, "coroutine.status") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'coroutine.status'.");

                    LuaThread thread = args[0] as LuaThread;
                    if (thread == null)
                        throw new ArgumentException("First argument to function 'coroutine.status' must be a thread.");

                    return new MultipleReturn((object)thread.Status.ToString().ToLowerInvariant());
                }
            }
            sealed class wrap : LuaFrameworkMethod
            {
                public wrap(ILuaEnvironment E) : base(E, "coroutine.wrap") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'coroutine.wrap'.");

                    IMethod meth = args[0] as IMethod;
                    if (meth == null)
                        throw new ArgumentException("First argument to function 'coroutine.wrap' must be a function.");
                    if (!(Environment is ILuaEnvironmentNet))
                        throw new InvalidOperationException("Coroutines only work with the NET version of the environment.");

                    return new MultipleReturn(new WrapHelper(Environment, ((ILuaEnvironmentNet)Environment).ThreadFactory.Create(meth)));
                }
            }
            sealed class yield : LuaFrameworkMethod
            {
                public yield(ILuaEnvironment E) : base(E, "coroutine.yield") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (!(Environment is ILuaEnvironmentNet))
                        throw new InvalidOperationException("Coroutines only work with the NET version of the environment.");
                    LuaThread t = ((ILuaEnvironmentNet)Environment).ThreadFactory.Search(Thread.CurrentThread.ManagedThreadId);
                    if (t == null)
                        throw new InvalidOperationException("Cannot yield the main thread.");
                    args = args ?? new object[0];

                    return new MultipleReturn(t.DoYield(args.Cast<object>().ToArray()));
                }
            }
        }
    }
}
