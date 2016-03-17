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
        class Coroutine
        {
            ILuaEnvironment E_;

            public Coroutine(ILuaEnvironment E)
            {
                E_ = E;
            }

            public void Initialize()
            {
                ILuaTable coroutine = E_.Runtime.CreateTable();
                Register(E_, coroutine, (Func<ILuaValue, ILuaValue>)create);
                Register(E_, coroutine, (Func<ILuaThread, ILuaValue[], IEnumerable<ILuaValue>>)resume);
                Register(E_, coroutine, (Func<object[]>)running);
                Register(E_, coroutine, (Func<ILuaThread, string>)status);
                Register(E_, coroutine, (Func<ILuaValue, object>)wrap);
                Register(E_, coroutine, (Func<ILuaValue[], ILuaMultiValue>)yield);

                E_.GlobalsTable.SetItemRaw(E_.Runtime.CreateValue("coroutine"), coroutine);
            }

            ILuaValue create(ILuaValue method)
            {
                if (method.ValueType != LuaValueType.Function)
                    throw new ArgumentException("First argument to function 'coroutine.create' must be a function.");

                return E_.Runtime.CreateThread(method);
            }
            [MultipleReturn]
            IEnumerable<ILuaValue> resume(ILuaThread thread, params ILuaValue[] args)
            {
                try
                {
                    ILuaMultiValue ret = thread.Resume(E_.Runtime.CreateMultiValue(args));
                    return new[] { E_.Runtime.CreateValue(true) }.Concat(ret);
                }
                catch (Exception e)
                {
                    if (e.Message == "Cannot resume a dead thread.")
                        return E_.Runtime.CreateMultiValueFromObj(false, "cannot resume dead coroutine");
                    else
                        return E_.Runtime.CreateMultiValueFromObj(false, e.Message, e);
                }
            }
            [MultipleReturn]
            object[] running()
            {
                ILuaThread thread = E_.Runtime.CurrentThread;
                return new object[] { thread, !thread.IsLua };
            }
            [IgnoreExtraArguments]
            string status(ILuaThread thread)
            {
                return thread.Status.ToString().ToLowerInvariant();
            }
            object wrap(ILuaValue func)
            {
                if (func.ValueType != LuaValueType.Function)
                {
                    throw new ArgumentException(
                        "First argument to function 'coroutine.wrap' must be a function.");
                }

                var thread = E_.Runtime.CreateThread(func);
                return (Func<ILuaMultiValue, ILuaMultiValue>)thread.Resume;
            }
            ILuaMultiValue yield(params ILuaValue[] args)
            {
                ILuaThread thread = E_.Runtime.CurrentThread;
                if (!thread.IsLua)
                    throw new InvalidOperationException("Cannot yield the main thread.");

                return thread.Yield(E_.Runtime.CreateMultiValue(args));
            }
        }
    }
}
