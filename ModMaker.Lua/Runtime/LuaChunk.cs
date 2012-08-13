using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security;
using ModMaker.Lua.Runtime;
using System.Threading;
using System.Dynamic;
using System.Reflection.Emit;
using System.Reflection;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines a module that can be loaded using the Lua
    /// function 'require'.
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// Executes the module with the given arguments.
        /// </summary>
        /// <param name="args">Any arguments to pass.</param>
        /// <returns>Any values returned from the module.</returns>
        object[] Execute(object[] args);
    }

    /// <summary>
    /// A piece of Lua code, this usually is from a file or a byte-stream.
    /// </summary>
    public sealed class LuaChunk : DynamicObject, IModule
    {
        LuaEnvironment _E;
        Type _T;

        internal LuaChunk(LuaEnvironment E, Type t)
        {
            this._E = E;
            this._T = t;
        }

        /// <summary>
        /// Gets or sets the environment that the chunk will run in.
        /// Cannot be changed while the code is running.
        /// </summary>
        public LuaEnvironment Environment
        {
            get { return _E; }
            set
            {
                lock (this)
                {
                    if (value == null)
                        throw new ArgumentNullException("value");

                    _E = value;
                }
            }
        }
        
        /// <summary>
        /// Excecutes the lua code in the chunk.
        /// </summary>
        public object[] Execute(params object[] args)
        {
            lock (this)
            {
                object _o = Activator.CreateInstance(_T, _E);
                //var m = _T.GetMethod("<>_global_");
                LuaFunc _act = (LuaFunc)Delegate.CreateDelegate(typeof(LuaFunc), _o, "<>_global_");

                var ret = _act(new LuaParameters(args, Environment));

                return ret.ToArray();
            }
        }
        internal LuaMethod ToMethod()
        {
            object _o = Activator.CreateInstance(_T, _E);
            LuaFunc _act = (LuaFunc)Delegate.CreateDelegate(typeof(LuaFunc), _o, "<>_global_");

            return new LuaMethod(_act.Method, _act.Target, "global function", _E);
        }

        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            object _o = Activator.CreateInstance(_T, _E);
            LuaFunc _act = (LuaFunc)Delegate.CreateDelegate(typeof(LuaFunc), _o, "<>_global_");

            var ret = _act(new LuaParameters(args, Environment));

            result = ret == null ? null : ret.ToArray();
            return true;
        }
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (typeof(Delegate).IsAssignableFrom(binder.Type))
            {
                result = LuaMethod.DelegateHelper.CreateDelegate(binder.Type, ToMethod());
                return true;
            }
            else if (binder.Type == typeof(LuaChunk))
            {
                result = this;
                return true;
            }
            return base.TryConvert(binder, out result);
        }
    }
}