using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Dynamic;
using System.Reflection.Emit;

namespace ModMaker.Lua.Runtime
{
    delegate MultipleReturn LuaFunc(LuaParameters par);
    
    /// <summary>
    /// Defines a method that is given to Lua.  This can contain one or
    /// more CLR methods or a single Lua method.
    /// </summary>
    public sealed class LuaMethod : DynamicObject
    {
        MethodInfo[] _meth;
        object[] _targets;
        LuaFunc _func;
        string name;
        LuaEnvironment E;

        // this class creates a dynamic type that is used to
        //   convert to delegate types.  It resembles this:
        //
        // class _tb
        // {
        //      LuaMethod meth;
        //
        //      public _tb(LuaMethod meth)
        //      {
        //           this.meth = meth;
        //      }
        //
        //      public $type$ Do(...)
        //      {
        //          object[] args = new[] { ... };
        //          MultipleReturn ret = this.meth.InvokeInternal(args, -1);
        //          return RuntimeHelper.ConvertReturnType(ret[0], $type$);
        //      }
        // }
        internal class DelegateHelper
        {
            struct DelegateInfo
            {
                public Type[] Args;
                public Type Return;

                public DelegateInfo(Type ret, Type[] args)
                {
                    this.Args = args;
                    this.Return = ret;
                }
                
                public override bool Equals(object obj)
                {
                    if (obj is DelegateInfo)
                    {
                        DelegateInfo info = (DelegateInfo)obj;
                        if (info.Args.Length != this.Args.Length)
                            return false;
                        for (int i = 0; i < this.Args.Length; i++)
                        {
                            if (info.Args[i] != this.Args[i])
                                return false;
                        }

                        return (this.Return == info.Return);
                    }
                    return false;
                }
                public override int GetHashCode()
                {
                    int ret = Return.GetHashCode();
                    foreach (var item in Args)
                        ret ^= item.GetHashCode();
                    return ret;
                }
            }

            static Dictionary<DelegateInfo, Type> _types = new Dictionary<DelegateInfo, Type>();
            static ModuleBuilder _mb;
            static int _tid = 1;

            static DelegateHelper()
            {
                StrongNameKeyPair kp = new StrongNameKeyPair(Resources.Dynamic);
                AssemblyName n = new AssemblyName("DynamicAssembly");
                n.KeyPair = kp;
                AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(n, AssemblyBuilderAccess.Run);
                _mb = ab.DefineDynamicModule("Name.dll");
            }
            
            public static Delegate CreateDelegate(Type t, LuaMethod meth)
            {
                Type[] args = t.GetMethod("Invoke").GetParameters().Select(p => p.ParameterType).ToArray();
                DelegateInfo info = new DelegateInfo(t.GetMethod("Invoke").ReturnType, args);
                Type type;
                if (!_types.TryGetValue(info, out type))
                {
                    type = CreateType(info);
                    _types[info] = type;
                }

                return Delegate.CreateDelegate(t, Activator.CreateInstance(type, meth), type.GetMethod("Do"));
            }
            static Type CreateType(DelegateInfo info)
            {
                TypeBuilder tb = _mb.DefineType("<>_type_" + (_tid++));
                FieldBuilder meth = tb.DefineField("meth", typeof(LuaMethod), FieldAttributes.Private);
                MethodBuilder mb = tb.DefineMethod("Do", MethodAttributes.Public, info.Return, info.Args);
                ILGenerator gen = mb.GetILGenerator();
                LocalBuilder loc = gen.DeclareLocal(typeof(object[]));
                LocalBuilder ret = gen.DeclareLocal(typeof(MultipleReturn));
                
                // loc = new object[{info.Args.Length}];
                gen.Emit(OpCodes.Ldc_I4, info.Args.Length);
                gen.Emit(OpCodes.Newarr, typeof(object));
                gen.Emit(OpCodes.Stloc, loc);

                for (int i = 0; i < info.Args.Length; i++)
                {
                    // loc[{i}] = arg_{i};
                    gen.Emit(OpCodes.Ldloc, loc);
                    gen.Emit(OpCodes.Ldc_I4, i);
                    gen.Emit(OpCodes.Ldarg, i + 1);
                    if (info.Args[i].IsValueType)
                        gen.Emit(OpCodes.Box, info.Args[i]);
                    gen.Emit(OpCodes.Stelem, typeof(object));
                }

                // ret = this.meth.InvokeInternal(loc, -1);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, meth);
                gen.Emit(OpCodes.Ldloc, loc);
                gen.Emit(OpCodes.Ldc_I4_M1);
                gen.Emit(OpCodes.Callvirt, typeof(LuaMethod).GetMethod("InvokeInternal", BindingFlags.NonPublic | BindingFlags.Instance));
                gen.Emit(OpCodes.Stloc, ret);

                // return RuntimeHelper.ConvertReturnType(ret, {info.Return});
                gen.Emit(OpCodes.Ldloc, ret);
                gen.Emit(OpCodes.Ldtoken, info.Return);
                gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("ConvertReturnType"));
                if (info.Return == null || info.Return == typeof(void))
                    gen.Emit(OpCodes.Pop);
                gen.Emit(OpCodes.Ret);

                // public <>_type_(LuaMethod meth)
                ConstructorBuilder cb = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(LuaMethod) });
                gen = cb.GetILGenerator();

                // base();
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Call, typeof(object).GetConstructor(new Type[0]));

                // this.meth = meth;
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stfld, meth);
                gen.Emit(OpCodes.Ret);

                return tb.CreateType();
            }
        }

        internal LuaMethod(LuaFunc func, LuaEnvironment E)
        {
            this._func = func;
            this.name = func.Method.Name;
            this.E = E;
        }
        internal LuaMethod(MethodInfo meth, object target, string name, LuaEnvironment E)
        {
            this._func = (LuaFunc)Delegate.CreateDelegate(typeof(LuaFunc), target, meth);
            this.name = name;
            this.E = E;
        }
        internal LuaMethod(IEnumerable<MethodInfo> meth, IEnumerable<object> targets, string name, LuaEnvironment E)
        {
            this._meth = meth.ToArray();
            this._targets = targets.ToArray();
            if (this._targets.Length == 1)
                this._targets = Enumerable.Range(1, _meth.Length).Select(i => _targets[0]).ToArray();
            if (this._targets.Length != this._meth.Length)
                throw new ArgumentException("The number of methods must equal the number of targets or there must be one target.");
            this.name = name;
            this.E = E;
        }

        /// <summary>
        /// The name of the method or null if none could be found.
        /// </summary>
        public string Name { get { return name; } }
        /// <summary>
        /// Gets whether the current method points to function defined in Lua code.
        /// </summary>
        public bool IsLua { get { return _func != null; } }
        
        internal MultipleReturn InvokeInternal(object[] args, int over)
        {
            if (_func != null)
            {
                if (over != -1)
                    throw new ArgumentException("Cannot specify the overload of a Lua function.");

                return _func(new LuaParameters(args, E));
            }

            object[] r_args;
            Tuple<MethodBase, object> meth;

            r_args = new object[args.Length];
            for (int i = 0; i < r_args.Length; i++)
            {
                object o = RuntimeHelper.GetValue(args[i]);
                LuaUserData u = o as LuaUserData;
                if (u != null)
                {
                    if (!u.Pass)
                        throw new ArgumentException("One of the arguments to function '" + name + "' cannot be passed to C#.");

                    o = u.Value;
                }
                if (o is LuaType)
                    o = (o as LuaType).Type;
                r_args[i] = o;
            }

            meth = RuntimeHelper.GetCompatibleMethod(
                over == -1 ? _meth.Select((m, i) => new Tuple<MethodBase, object>(m, _targets[i])).ToArray() : 
                    new[] { new Tuple<MethodBase, object>(_meth[over], _targets[over]) },
                r_args);
            if (meth == null)
                throw new ArgumentException("No overload of method '" + name + "' could be found with specified parameters.");

            object ret = meth.Item1.Invoke(meth.Item2, r_args);
            if (ret is MultipleReturn)
                return (MultipleReturn)ret;
            if (ret is ReturnInfo)
                return (ret as ReturnInfo).CreateReturn();

            MultipleReturn r;
            if (meth.Item1.GetCustomAttributes(typeof(MultipleReturnAttribute), false).Length > 0)
            {
                if (typeof(IEnumerable).IsAssignableFrom((meth.Item1 as MethodInfo).ReturnType))
                {
                    r = new MultipleReturn(ret as IEnumerable ?? new object[0]);
                }
                else
                    throw new InvalidOperationException(
                        "Methods marked with MultipleReturnAttribute must return a type compatible with IEnumerable.");
            }
            else
            {
                if ((meth.Item1 as MethodInfo).ReturnType != typeof(void))
                    r = new MultipleReturn(new[] { ret });
                else
                    r = new MultipleReturn();
            }

            return r;
        }
        internal void AddOverload(MethodInfo m, object target)
        {
            if (_func != null)
                throw new InvalidOperationException("Cannot overload a Lua function.");

            this._meth = this._meth.Union(new[] { m }).ToArray();
            this._targets = this._targets.Union(new[] { target }).ToArray();
        }

        /// <summary>
        /// Invokes the method with the specified parameters.
        /// </summary>
        /// <param name="args">The arguments to pass.</param>
        /// <returns>The returned values.</returns>
        public object[] Invoke(params object[] args)
        {
            var ret = InvokeInternal(args, -1);
            return ret == null ? null : ret.ToArray();
        }

        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            var ret = InvokeInternal(args, -1);
            result = ret == null ? null : ret.ToArray();
            return true;
        }
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (typeof(Delegate).IsAssignableFrom(binder.Type))
            {
                result = DelegateHelper.CreateDelegate(binder.Type, this);
                return true;
            }
            else if (binder.Type == typeof(LuaMethod))
            {
                result = _meth;
                return true;
            }
            return base.TryConvert(binder, out result);
        }
    }
}