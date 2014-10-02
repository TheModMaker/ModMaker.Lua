using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ModMaker.Lua.Runtime;
using System.Globalization;

namespace ModMaker.Lua.Runtime
{
    /// <summary>
    /// Defines how Lua looks for external modules.  When 'require' is called from
    /// Lua, first IModuleBinder.Loaded is called to search for a pre-loaded module,
    /// if it returns null, it calls IModuleBinder.Load.
    /// </summary>
    public interface IModuleBinder
    {
        /// <summary>
        /// Searches and loads the module acording to the binder.
        /// </summary>
        /// <param name="name">The name of the module to find.</param>
        /// <param name="E">The environment to load to.</param>
        /// <returns>The loaded module, or null if it could not be loaded.</returns>
        object Load(string name, LuaEnvironment E);
        /// <summary>
        /// Searches the binder for a module that has already been loaded.
        /// Return null to try to load the module.
        /// </summary>
        /// <param name="name">The name of the module to find.</param>
        /// <returns>The loaded module or null if not loaded.</returns>
        object Loaded(string name);
    }

    /// <summary>
    /// Defines the default binding behaviour similar to that of the Lua
    /// Language Specification.
    /// </summary>
    public sealed class ModuleBinder : IModuleBinder
    {
        Dictionary<string, object> _loaded = new Dictionary<string, object>();
        Lua lua;

        /// <summary>
        /// Creates a new ModuleBinder with default settings.
        /// </summary>
        public ModuleBinder(Lua L)
        {
            if (L == null)
                throw new ArgumentNullException("L");

            this.Path = @"!\?.lua;!\?.dll;!\?\init.lua;.\?.lua;.\?.dll;.\?\init.lua";
            this.AllowCompiledLua = true;
            this.AllowCSharp = false;
            this.AllowLua = true;
            this.WhitelistPublicKeys = null;
            this.lua = L;
        }

        /// <summary>
        /// Gets or sets the search-path for modules.
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        /// Gets or sets whether to allow compiled c# code that impliments IModule that was
        /// not generated with this framework. Default: false.
        /// </summary>
        public bool AllowCSharp { get; set; }
        /// <summary>
        /// Gets or sets whether to allow Lua code that was compiled with this framework.
        /// Default: true.
        /// </summary>
        public bool AllowCompiledLua { get; set; }
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
        /// <para>This value overrides ModuleBinder.AllowCompiledLua.</para>
        /// 
        /// <para>To allow any assembly built with this framework, include the
        /// following.  Note: it is possible to get this private key from
        /// this assembly so it is not guarenteed that the assembly was 
        /// built with this framework.</para>
        /// 
        /// <para>0024000004800000940000000602000000240000525341310004000001000100A1D901D065422E6E96435ABA4A4AC6A53107AF554EA558086D55E7C071B96415ECD22803EA06774E9087DE2CC90461B800D700BDA48406EC0120ECCD5BEC5CD2E843203028E398D7B48EA25CB9338E87EE9A05C0861DBC1F25C78EF7DADC67A43CE88EA36E0B9544ED0D680957CA522E095161EB4C3C66D67D19A87390DFCABE</para>
        /// </remarks>
        public string[] WhitelistPublicKeys { get; set; }

        /// <summary>
        /// Searches and loads the module acording to the settings.
        /// </summary>
        /// <param name="name">The name of the module to find.</param>
        /// <param name="E">The environment to load to.</param>
        /// <returns>The loaded module, or null if it could not be loaded.</returns>
        public object Load(string name, LuaEnvironment E)
        {
            List<Exception> _e = new List<Exception>();
            foreach (string s in this.Path.Split(';'))
            {
                string path = s.Replace("?", name);
                if (File.Exists(path))
                {
                    object o = ProcessFile(path, name, name, E, _e);
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
                        object o = ProcessFile(path, name, name.Substring(i + 1), E, _e);
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
        /// <summary>
        /// Searches the binder for a module that has already been loaded.
        /// Return null to try to load the module.
        /// </summary>
        /// <param name="name">The name of the module to find.</param>
        /// <returns>The loaded module or null if not loaded.</returns>
        public object Loaded(string name)
        {
            if (_loaded.ContainsKey(name))
                return _loaded[name];
            else
                return null;
        }

        object ProcessFile(string path, string name, string partname, LuaEnvironment E, List<Exception> _e)
        {
            if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                if (!this.AllowLua)
                {
                    _e.Add(new InvalidOperationException("Cannot load file '" + path + "' because ModuleBinder.AllowLua is set to false."));
                    return null;
                }

                var chunk = lua.Load(path);
                return chunk.Execute();
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
                            if (aname.GetPublicKey().ToStringBase16().ToLower(CultureInfo.InvariantCulture) == key.ToLower(CultureInfo.InvariantCulture))
                                valid = true;
                        }
                    }
                    if (!valid)
                    {
                        _e.Add(new InvalidOperationException("Cannot load file '" + path +
                            "' because the assembly's public key is not in the whitelist."));
                        return null;
                    }
                }

                // Load if compiled with this library
                Assembly a = null;
                if (AllowCompiledLua && aname.GetPublicKey().ToStringBase16().ToUpper(CultureInfo.InvariantCulture) == "0024000004800000940000000602000000240000525341310004000001000100A1D901D065422E6E96435ABA4A4AC6A53107AF554EA558086D55E7C071B96415ECD22803EA06774E9087DE2CC90461B800D700BDA48406EC0120ECCD5BEC5CD2E843203028E398D7B48EA25CB9338E87EE9A05C0861DBC1F25C78EF7DADC67A43CE88EA36E0B9544ED0D680957CA522E095161EB4C3C66D67D19A87390DFCABE")
                    a = Assembly.LoadFrom(path);

                // Load if c# code allowed
                if (AllowCSharp && a == null)
                    a = Assembly.LoadFrom(path);

                // Process the assembly
                if (a != null)
                {
                    var types = a.GetTypes();
                    if (types == null || types.Length == 0)
                    {
                        _e.Add(new InvalidOperationException("Cannot load file '" + path + "' because it does not define any types."));
                        return null;
                    }
                    else if (types.Length > 1)
                    {
                        List<Type> validTypes = new List<Type>();

                        // check for the whole name
                        validTypes.AddRangeUnique(types.Where(t => (t.Name == name || t.FullName == name) && ValidType(t)));

                        // remove the first part of the name that is in the filename.
                        validTypes.AddRangeUnique(types.Where(t => (t.Name == partname || t.FullName == partname) && ValidType(t)));

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
                            return ProcessType(validTypes[0], E);
                    }
                    else
                        return ProcessType(types[0], E);
                }
            }
            return null;
        }
        static bool ValidType(Type t)
        {
            return typeof(IModule).IsAssignableFrom(t) && t.GetConstructor(new Type[0]) != null;
        }
        static object ProcessType(Type t, LuaEnvironment E)
        {
            if (!typeof(IModule).IsAssignableFrom(t))
                return null;

            ConstructorInfo ci = t.GetConstructor(new Type[0]);
            if (ci == null)
            {
                ci = t.GetConstructor(new Type[] { typeof(LuaEnvironment) });
                if (ci != null)
                {
                    IModule mod = (IModule)ci.Invoke(new[] { E });
                    return mod.Execute(new object[0]);
                }
                return null;
            }
            else
            {
                IModule mod = (IModule)ci.Invoke(null);
                return mod.Execute(new object[0]);
            }
        }
    }
}