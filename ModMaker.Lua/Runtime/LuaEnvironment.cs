using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua.Parser.Items;
using System.Reflection.Emit;
using System.Threading;
using System.Reflection;
using System.Security.Permissions;
using System.Security;
using ModMaker.Lua.Parser;
using System.IO;
using System.Dynamic;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines the environment that Lua operates in.
    /// </summary>
    public class LuaEnvironment : DynamicObject
    {
        internal LuaTable _globals = new LuaTable();
        List<string> _types = new List<string>();
        AssemblyBuilder _ab;
        ModuleBuilder _mb;
        int _tid = 1;

        /// <summary>
        /// Gets or sets the global value with the specified name.
        /// </summary>
        /// <param name="name">The name of the global variable.</param>
        /// <returns>The value of the variable.</returns>
        public dynamic this[string name]
        {
            get
            {
                object o = RuntimeHelper.GetValue(_globals.GetItemRaw(name));
                if (o is LuaUserData)
                    o = (o as LuaUserData).Value;
                if (o is LuaType)
                    o = (o as LuaType).Type;
                return o;
            }
            set
            {
                _globals.SetItemRaw(name, (object)value);
            }
        }

        /// <summary>
        /// Creates a new environment with the given settings.
        /// </summary>
        /// <param name="settings">The settings to give the Environment.</param>
        public LuaEnvironment(LuaSettings settings)
        {
            StrongNameKeyPair kp = new StrongNameKeyPair(Resources.Dynamic);
            AssemblyName n = new AssemblyName("DynamicAssembly");
            n.KeyPair = kp;
            _ab = AppDomain.CurrentDomain.DefineDynamicAssembly(n, AssemblyBuilderAccess.RunAndSave);
            _mb = _ab.DefineDynamicModule("DynamicAssembly.dll");
            this.Settings = settings;
            LuaStaticLibraries.Initialize(this);
            InitializeTypes();
        }
        void InitializeTypes()
        {
            RegisterType(typeof(bool), "bool");
            RegisterType(typeof(byte), "byte");
            RegisterType(typeof(sbyte), "sbyte");
            RegisterType(typeof(short), "short");
            RegisterType(typeof(int), "int");
            RegisterType(typeof(long), "long");
            RegisterType(typeof(ushort), "ushort");
            RegisterType(typeof(uint), "uint");
            RegisterType(typeof(ulong), "ulong");
            RegisterType(typeof(float), "float");
            RegisterType(typeof(double), "double");
            RegisterType(typeof(decimal), "decimal");
            RegisterType(typeof(Int16), "Int16");
            RegisterType(typeof(Int32), "Int32");
            RegisterType(typeof(Int64), "Int64");
            RegisterType(typeof(UInt16), "UInt16");
            RegisterType(typeof(UInt32), "UInt32");
            RegisterType(typeof(UInt64), "UInt64");
            RegisterType(typeof(Single), "Single");
            RegisterType(typeof(Double), "Double");
            RegisterType(typeof(Decimal), "Decimal");
            RegisterType(typeof(String), "String");
            RegisterType(typeof(Byte), "Byte");
            RegisterType(typeof(SByte), "SByte");
            RegisterType(typeof(Boolean), "Boolean");
        }

        internal LuaSettings Settings { get; private set; }

        internal ChunkBuilderNew DefineChunkNew(string name = null)
        {
            lock (this)
            {
                string t = name ?? "<>_type_" + (_tid++);
                if (_types.Contains(t))
                {
                    t += (_tid++);
                }
                _types.Add(t);
                return new ChunkBuilderNew(_mb.DefineType(t, TypeAttributes.Public, null, new Type[] { typeof(IModule) }));
            }
        }
        internal void RegisterDelegate(Delegate d, string name)
        {
            lock (this)
            {
                object o = _globals.GetItemRaw(name);
                if (o != null)
                {
                    LuaMethod meth = o as LuaMethod;
                    if (meth == null)
                        throw new ArgumentException("An object with the name '" + name + "' already is registered.");

                    meth.AddOverload(d.Method, d.Target);
                }
                else
                    _globals.SetItemRaw(name, new LuaMethod(new[] { d.Method }, new[] { d.Target }, name, this));
            }
        }
        internal void RegisterType(Type t, string name)
        {
            lock (this)
            {
                object o = _globals.GetItemRaw(name);
                if (o != null)
                {
                    throw new ArgumentException("An object with the name '" + name + "' already is registered.");
                }
                else
                    _globals.SetItemRaw(name, new LuaType(t));
            }
        }
        
        /// <summary>
        /// Saves the environment to disk.
        /// </summary>
        /// <param name="name">The name/path to save to.</param>
        [SecuritySafeCritical]
        public void Save(string name)
        {
            new FileIOPermission(FileIOPermissionAccess.AllAccess, Path.GetDirectoryName(Path.GetFullPath("DynamicAssembly.dll"))).Demand();
            new FileIOPermission(FileIOPermissionAccess.AllAccess, Path.GetDirectoryName(Path.GetFullPath(name))).Demand();

            if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                name += ".dll";
            _ab.Save("DynamicAssembly.dll");
            File.Move("DynamicAssembly.dll", name);
        }
        /// <summary>
        /// Saves the environment to disk, optionally overriting the file if it exists.
        /// </summary>
        /// <param name="name">The name/path to save to.</param>
        /// <param name="_override">True to override the file if it exists, otherwise false.</param>
        [SecuritySafeCritical]
        public void Save(string name, bool _override)
        {
            new FileIOPermission(FileIOPermissionAccess.AllAccess, Path.GetDirectoryName(Path.GetFullPath("DynamicAssembly.dll"))).Demand();
            new FileIOPermission(FileIOPermissionAccess.AllAccess, Path.GetDirectoryName(Path.GetFullPath(name))).Demand();

            if (File.Exists(name) && _override)
                File.Delete(name);
            if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                name += ".dll";

            _ab.Save("DynamicAssembly.dll");
            File.Move("DynamicAssembly.dll", name);
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            foreach (var item in _globals)
                if (item.Key is string)
                    yield return item.Key as string;
        }
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.Type == typeof(LuaEnvironment))
            {
                result = this;
                return true;
            }
            return base.TryConvert(binder, out result);
        }
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (indexes != null && indexes.Length == 1)
            {
                object o = _globals._get(indexes[0]);
                result = RuntimeHelper.ConvertType(o, binder.ReturnType);
                return true;
            }
            else
                return base.TryGetIndex(binder, indexes, out result);
        }
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            if (indexes != null && indexes.Length == 1)
            {
                _globals._set(indexes[0], value);
                return true;
            }
            else
                return base.TrySetIndex(binder, indexes, value);
        }
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            object o = _globals._get(binder.Name);
            result = RuntimeHelper.ConvertType(o, binder.ReturnType);
            return true;
        }
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            _globals._set(binder.Name, value);
            return true;
        }
    }
}
