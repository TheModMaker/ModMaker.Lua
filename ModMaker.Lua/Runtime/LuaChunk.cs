<<<<<<< HEAD
﻿using System;
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
    [LuaIgnore]
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
        internal LuaChunk Clone(LuaEnvironment E)
        {
            return new LuaChunk(E, this._T);
        }

        /// <summary>
        /// Provides the implementation for operations that invoke an object. Classes
        ///     derived from the System.Dynamic.DynamicObject class can override this method
        ///     to specify dynamic behavior for operations such as invoking an object or
        ///     a delegate.
        /// </summary>
        /// <param name="binder">Provides information about the invoke operation.</param>
        /// <param name="args">The arguments that are passed to the object during the invoke operation.
        ///     For example, for the sampleObject(100) operation, where sampleObject is derived
        ///     from the System.Dynamic.DynamicObject class, args[0] is equal to 100.</param>
        /// <param name="result">The result of the object invocation.</param>
        /// <returns>true if the operation is successful; otherwise, false. If this method returns
        ///     false, the run-time binder of the language determines the behavior. (In most
        ///     cases, a language-specific run-time exception is thrown.</returns>
        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            object _o = Activator.CreateInstance(_T, _E);
            LuaFunc _act = (LuaFunc)Delegate.CreateDelegate(typeof(LuaFunc), _o, "<>_global_");

            var ret = _act(new LuaParameters(args, Environment));

            result = ret == null ? null : ret.ToArray();
            return true;
        }
        /// <summary>
        /// Provides implementation for type conversion operations. Classes derived from
        ///     the System.Dynamic.DynamicObject class can override this method to specify
        ///     dynamic behavior for operations that convert an object from one type to another.
        /// </summary>
        /// <param name="binder">Provides information about the conversion operation. The binder.Type property
        ///     provides the type to which the object must be converted. For example, for
        ///     the statement (String)sampleObject in C# (CType(sampleObject, Type) in Visual
        ///     Basic), where sampleObject is an instance of the class derived from the System.Dynamic.DynamicObject
        ///     class, binder.Type returns the System.String type. The binder.Explicit property
        ///     provides information about the kind of conversion that occurs. It returns
        ///     true for explicit conversion and false for implicit conversion.</param>
        /// <param name="result">The result of the type conversion operation.</param>
        /// <returns>true if the operation is successful; otherwise, false. If this method returns
        ///     false, the run-time binder of the language determines the behavior. (In most
        ///     cases, a language-specific run-time exception is thrown.)</returns>
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
=======
﻿using System;
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
        internal LuaChunk Clone(LuaEnvironment E)
        {
            return new LuaChunk(E, this._T);
        }

        /// <summary>
        /// Provides the implementation for operations that invoke an object. Classes
        ///     derived from the System.Dynamic.DynamicObject class can override this method
        ///     to specify dynamic behavior for operations such as invoking an object or
        ///     a delegate.
        /// </summary>
        /// <param name="binder">Provides information about the invoke operation.</param>
        /// <param name="args">The arguments that are passed to the object during the invoke operation.
        ///     For example, for the sampleObject(100) operation, where sampleObject is derived
        ///     from the System.Dynamic.DynamicObject class, args[0] is equal to 100.</param>
        /// <param name="result">The result of the object invocation.</param>
        /// <returns>true if the operation is successful; otherwise, false. If this method returns
        ///     false, the run-time binder of the language determines the behavior. (In most
        ///     cases, a language-specific run-time exception is thrown.</returns>
        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            object _o = Activator.CreateInstance(_T, _E);
            LuaFunc _act = (LuaFunc)Delegate.CreateDelegate(typeof(LuaFunc), _o, "<>_global_");

            var ret = _act(new LuaParameters(args, Environment));

            result = ret == null ? null : ret.ToArray();
            return true;
        }
        /// <summary>
        /// Provides implementation for type conversion operations. Classes derived from
        ///     the System.Dynamic.DynamicObject class can override this method to specify
        ///     dynamic behavior for operations that convert an object from one type to another.
        /// </summary>
        /// <param name="binder">Provides information about the conversion operation. The binder.Type property
        ///     provides the type to which the object must be converted. For example, for
        ///     the statement (String)sampleObject in C# (CType(sampleObject, Type) in Visual
        ///     Basic), where sampleObject is an instance of the class derived from the System.Dynamic.DynamicObject
        ///     class, binder.Type returns the System.String type. The binder.Explicit property
        ///     provides information about the kind of conversion that occurs. It returns
        ///     true for explicit conversion and false for implicit conversion.</param>
        /// <param name="result">The result of the type conversion operation.</param>
        /// <returns>true if the operation is successful; otherwise, false. If this method returns
        ///     false, the run-time binder of the language determines the behavior. (In most
        ///     cases, a language-specific run-time exception is thrown.)</returns>
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
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
}