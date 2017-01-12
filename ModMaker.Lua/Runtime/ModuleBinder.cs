using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ModMaker.Lua.Runtime;
using System.Globalization;
using ModMaker.Lua.Compiler;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines how Lua looks for external modules.  When 'require' is called from
    /// Lua, it calls IModuleBinder.Load.
    /// </summary>
    public interface IModuleBinder
    {
        /// <summary>
        /// Searches and loads the module acording to the binder.
        /// </summary>
        /// <param name="E">The environment to load to.</param>
        /// <param name="name">The name of the module to find.</param>
        /// <returns>The loaded module, or null if it could not be loaded.</returns>
        ILuaValue Load(ILuaEnvironment E, string name);
    }

    /// <summary>
    /// Defines the default binding behaviour similar to that of the Lua
    /// Language Specification.
    /// </summary>
    public sealed class ModuleBinder : IModuleBinder
    {
        Dictionary<string, ILuaValue> _loaded = new Dictionary<string, ILuaValue>();

        /// <summary>
        /// Creates a new ModuleBinder with default settings.
        /// </summary>
        public ModuleBinder()
        {
            this.Path = @"!\?.lua;!\?.dll;!\?\init.lua;.\?.lua;.\?.dll;.\?\init.lua";
            this.AllowAssemblies = false;
            this.AllowLua = true;
            this.WhitelistPublicKeys = null;
        }

        /// <summary>
        /// Gets or sets the search-path for modules.
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        /// Gets or sets whether to allow .NET assemblies to run.  Any code that
        /// implements IMethod is valid.  Default: false.
        /// </summary>
        public bool AllowAssemblies { get; set; }
        /// <summary>
        /// Gets or sets whether to allow uncompiled(source) Lua code.
        /// Default: true.
        /// </summary>
        public bool AllowLua { get; set; }
        /// <summary>
        /// Gets or sets the list of public-keys that are allowed
        /// to load from.  These must be full public keys.  Set to
        /// null to allow any assembly.  Include a null entry to allow
        /// weakly-named assemblies.
        /// </summary>
        /// <remarks>
        /// This value overrides ModuleBinder.AllowCompiledLua.
        /// </remarks>
        public string[] WhitelistPublicKeys { get; set; }

        /// <summary>
        /// Searches and loads the module acording to the settings.
        /// </summary>
        /// <param name="name">The name of the module to find.</param>
        /// <param name="E">The environment to load to.</param>
        /// <returns>The loaded module, or null if it could not be loaded.</returns>
        /// <exception cref="System.ArgumentNullException">If E or name is null.</exception>
        public ILuaValue Load(ILuaEnvironment E, string name)
        {
            if (E == null)
                throw new ArgumentNullException("E");
            if (name == null)
                throw new ArgumentNullException("name");

            if (_loaded.ContainsKey(name))
                return _loaded[name];

            List<Exception> _e = new List<Exception>();
            foreach (string s in this.Path.Split(';'))
            {
                string path = s.Replace("?", name);
                if (File.Exists(path))
                {
                    ILuaValue o = ProcessFile(path, name, name, E, _e);
                    if (o != null)
                    {
                        _loaded.Add(name, o);
                        return o;
                    }
                }

                int i = name.IndexOf('.');
                while (i != -1)
                {
                    path = s.Replace("?", name.Substring(0, i));
                    if (File.Exists(path))
                    {
                        ILuaValue o = ProcessFile(path, name, name.Substring(i + 1), E, _e);
                        if (o != null)
                        {
                            _loaded.Add(name, o);
                            return o;
                        }
                    }
                    i = name.IndexOf('.', i + 1);
                }
            }
            throw new AggregateException("Unable to load module '" + name +
                "' because a valid module could not be located.  See $Exception.InnerExceptions to see why.", _e);
        }

        ILuaValue ProcessFile(string path, string name, string partname, ILuaEnvironment E, List<Exception> _e)
        {
            if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                if (!this.AllowLua)
                {
                    _e.Add(new InvalidOperationException("Cannot load file '" + path + "' because ModuleBinder.AllowLua is set to false."));
                    return null;
                }

                var item = PlainParser.Parse(E.Parser, System.IO.File.ReadAllText(path), System.IO.Path.GetFileNameWithoutExtension(path));
                var chunk = E.CodeCompiler.Compile(E, item, null);
                return chunk.Invoke(LuaNil.Nil, false, -1, LuaMultiValue.Empty).Single();
            }
            else if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var aname = AssemblyName.GetAssemblyName(path);

                // validate against whitelist
                if (WhitelistPublicKeys != null)
                {
                    bool valid = false;
                    foreach (var key in WhitelistPublicKeys)
                    {
                        if (key == null)
                        {
                            if (aname.GetPublicKey() == null)
                                valid = true;
                        }
                        else
                        {
                            if (aname.GetPublicKey().ToStringBase16().ToLowerInvariant() == key.ToLowerInvariant())
                                valid = true;
                        }
                    }
                    if (!valid)
                    {
                        _e.Add(new InvalidOperationException("Cannot load file '" + path +
                            "' because the assembly's public key is not in the white-list."));
                        return null;
                    }
                }

                // Process the assembly
                if (AllowAssemblies)
                {
                    var a = Assembly.LoadFrom(path);
                    var types = a.GetTypes();
                    if (types == null || types.Length == 0)
                    {
                        _e.Add(new InvalidOperationException("Cannot load file '" + path + "' because it does not define any types."));
                        return null;
                    }
                    else if (types.Length > 1)
                    {
                        HashSet<Type> validTypes = new HashSet<Type>();

                        // check for the whole name
                        foreach (var item in types.Where(t => (t.Name == name || t.FullName == name) && ValidType(t)))
                            validTypes.Add(item);

                        // remove the first part of the name that is in the filename.
                        foreach (var item in types.Where(t => (t.Name == partname || t.FullName == partname) && ValidType(t)))
                            validTypes.Add(item);

                        if (validTypes.Count > 1)
                        {
                            _e.Add(new InvalidOperationException("Cannot load file '" + path + "' because more than one type matches the given name."));
                            return null;
                        }
                        else if (validTypes.Count == 0)
                        {
                            _e.Add(new InvalidOperationException("Cannot load file '" + path + "' because no types match the given name."));
                            return null;
                        }
                        else
                            return ProcessType(validTypes.First(), E);
                    }
                    else
                        return ProcessType(types[0], E);
                }
            }
            return null;
        }
        static bool ValidType(Type t)
        {
            return typeof(ILuaValue).IsAssignableFrom(t) && 
                (t.GetConstructor(new Type[0]) != null || t.GetConstructor(new[] { typeof(ILuaEnvironment) }) != null);
        }
        static ILuaValue ProcessType(Type t, ILuaEnvironment E)
        {
            if (!typeof(ILuaValue).IsAssignableFrom(t))
                return null;

            ConstructorInfo ci = t.GetConstructor(new Type[0]);
            if (ci == null)
            {
                ci = t.GetConstructor(new Type[] { typeof(ILuaEnvironment) });
                if (ci != null)
                {
                    ILuaValue mod = (ILuaValue)ci.Invoke(new[] { E });
                    return mod.Invoke(LuaNil.Nil, false, -1, E.Runtime.CreateMultiValue()).Single();
                }
                return null;
            }
            else
            {
                ILuaValue mod = (ILuaValue)ci.Invoke(null);
                return mod.Invoke(LuaNil.Nil, false, -1, E.Runtime.CreateMultiValue()).Single();
            }
        }
    }
}