using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua
{
    /// <summary>
    /// The main manager of a Lua state. Manages lua chunks, the environments, 
    /// and registering types and global methods.
    /// </summary>
    [LuaIgnore]
    public sealed class Lua
    {
        static bool __dynamic = false;
        ILuaEnvironment _E;
        List<IMethod> _chunks;

        /// <summary>
        /// Creates a new Lua object using the default environment, runtime, 
        /// and compiler.
        /// </summary>
        public Lua()
            : this((ILuaEnvironment)null) { }
        /// <summary>
        /// Creates a new Lua object using the default environment, runtime, 
        /// and compiler using the given settings.
        /// </summary>
        /// <param name="settings">The settings to use, cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">If settings is null.</exception>
        public Lua(LuaSettings settings)
            : this(new LuaEnvironmentNet(settings)) { }
        /// <summary>
        /// Creates a new Lua object using the given lua environment.
        /// </summary>
        /// <param name="environment">The environment that Lua will execute in;
        /// if null, will use the default.</param>
        public Lua(ILuaEnvironment environment)
        {
            if (environment == null)
            {
                environment = new LuaEnvironmentNet(
                    new LuaSettings(
                        Console.OpenStandardInput(), 
                        Console.OpenStandardOutput()
                    )
                );
            }

            this._chunks = new List<IMethod>();
            this._E = environment;
        }

        /// <summary>
        /// Gets or sets whether to internally use dynamic types.  Without
        /// dynamic types, this framework does not support proper tail calls.
        /// </summary>
        public static bool UseDynamicTypes { get { return __dynamic; } set { __dynamic = value; } }

        /// <summary>
        /// Gets the settings for the current environment.
        /// </summary>
        public LuaSettings Settings { get { return _E.Settings; } }
        /// <summary>
        /// Gets or sets the global environment.  Value cannot be changed while
        /// parsing.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">When trying to set 
        /// the environment to null.</exception>
        public ILuaEnvironment Environment 
        {
            get { return _E; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                lock (this)
                    _E = value;
            }
        }
        /// <summary>
        /// Gets the chunk at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index in the order they were 
        /// loaded.</param>
        /// <returns>The lua chunk at that index.</returns>
        public IMethod this[int index] { get { return _chunks[index]; } }

        /// <summary>
        /// Registers a delegate for use with this Lua object.
        /// </summary>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <exception cref="System.ArgumentException">When there is another 
        /// method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func 
        /// has more than one item in the InvokationList.</exception>
        public void Register(Delegate func)
        {
            this.Register(func, null);
        }
        /// <summary>
        /// Registers a delegate with the given name for use with this Lua 
        /// object.
        /// </summary>
        /// <param name="func">The delegate to register, cannot be null.</param>
        /// <param name="name">The name of the delegate.  If null, will use the
        /// function name.</param>
        /// <exception cref="System.ArgumentException">When there is another 
        /// method or type with the same name registered.</exception>
        /// <exception cref="System.ArgumentNullException">When func is null.</exception>
        /// <exception cref="System.MulticastNotSupportedException">When func 
        /// has more than one item in the InvokationList.</exception>
        public void Register(Delegate func, string name)
        {
            if (func == null)
                throw new ArgumentNullException("func");
            if (func.GetInvocationList().Length > 1)
                throw new MulticastNotSupportedException(Resources.MulticastNotSupported);
            if (name == null)
                name = func.Method.Name;

            lock (this)
            {
                _E.RegisterDelegate(func, name);
            }
        }
        /// <summary>
        /// Registers a type for use within Lua.
        /// </summary>
        /// <param name="type">The type to register, cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">When type is null.</exception>
        /// <exception cref="System.ArgumentException">When there is another 
        /// method or type with the same name registered.</exception>
        public void Register(Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            lock (this)
            {
                _E.RegisterType(type, type.Name);
            }
        }

        /// <summary>
        /// Executes all the chunks in order.  Any exception thrown by Lua code
        /// is given back by this call.
        /// </summary>
        /// <param name="args">The arguments to pass to each chunk.</param>
        /// <returns>The union of the results of each chunk.</returns>
        public object[] Execute(params object[] args)
        {
            lock (this)
            {
                List<object> ret = new List<object>();
                foreach (var item in _chunks)
                {
                    ret.AddRange(item.Invoke(null, false, null, args));
                }
                return ret.ToArray();
            }
        }
        /// <summary>
        /// Executes one of the chunks.  Any exception thrown by Lua code is
        /// given back by this call.
        /// </summary>
        /// <param name="args">The arguments to the chunk.</param>
        /// <param name="index">The index of the loaded chunk.</param>
        /// <returns>The results of the chunk.</returns>
        /// <exception cref="System.IndexOutOfRangeException">If the given 
        /// index is less than zero or greater than the number of loaded chunks.</exception>
        public object[] Execute(int index, params object[] args)
        {
            lock (this)
            {
                if (index >= _chunks.Count || index < 0)
                    throw new IndexOutOfRangeException(Resources.ChunkOutOfRange);
                return _chunks[index].Invoke(null, false, null, args).Values;
            }
        }

        /// <summary>
        /// Loads and executes the file at the path specified.  The chunk is
        /// loaded into this object and can be indexed by Execute and the
        /// indexder.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="args">The arguments to pass.</param>
        /// <returns>The values returned from the file.</returns>
        /// <exception cref="System.ArgumentNullException">If path is null or empty.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the given file.</exception>
        public object[] DoFile(string path, params object[] args)
        {
            var ret = Load(path);
            return ret.Invoke(null, false, null, args).Values;
        }
        /// <summary>
        /// Loads and executes the file from the given stream.  The chunk is
        /// loaded into this object and can be indexed by Execute and the
        /// indexder.
        /// </summary>
        /// <param name="stream">The stream to read the file from.</param>
        /// <param name="args">The arguments to pass.</param>
        /// <returns>The values returned from the file.</returns>
        /// <exception cref="System.ArgumentNullException">If stream is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the given file.</exception>
        public object[] DoFile(Stream stream, params object[] args)
        {
            var ret = Load(stream);
            return ret.Invoke(null, false, null, args).Values;
        }
        /// <summary>
        /// Loads and executes the specified text. The chunk is
        /// loaded into this object and can be indexed by Execute and the
        /// indexder.
        /// </summary>
        /// <param name="chunk">The chunk to execute.</param>
        /// <param name="args">The arguments to pass.</param>
        /// <returns>The values returned from the file.</returns>
        /// <exception cref="System.ArgumentNullException">If chunk is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the given file.</exception>
        public object[] DoText(string chunk, params object[] args)
        {
            var ret = LoadText(chunk);
            return ret.Invoke(null, false, null, args).Values;
        }

        #region public IMethod Load(...)

        /// <summary>
        /// Loads a LuaChunk from a specified file.
        /// </summary>
        /// <param name="path">The path to the file to load.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentException">path is a zero-length 
        /// string, contains only white space, or contains one or more invalid
        /// characters as defined by System.IO.Path.InvalidPathChars</exception>
        /// <exception cref="System.ArgumentNullException">If path is null.</exception>
        /// <exception cref="System.UnauthorizedAccessException">If this code
        /// does not have sufficient rights to read from the file.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException">The specified 
        /// path is invalid, (for example, it is on an unmapped drive).</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the file cannot
        /// be found.</exception>
        /// <exception cref="System.IO.PathTooLongException">The specified path,
        /// file name, or both exceed the system-defined maximum length. For 
        /// example, on Windows-based platforms, paths must be less than 248
        /// characters, and file names must be less than 260 characters.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are 
        /// syntax errors in the file.</exception>
        public IMethod Load(string path)
        {
            int i;
            return Load(path, null, false, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a specified file.
        /// </summary>
        /// <param name="path">The path to the file to load.</param>
        /// <param name="override">True to load the file even if it is in the cache, otherwise false.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentException">path is a zero-length 
        /// string, contains only white space, or contains one or more invalid
        /// characters as defined by System.IO.Path.InvalidPathChars</exception>
        /// <exception cref="System.ArgumentNullException">If path is null.</exception>
        /// <exception cref="System.UnauthorizedAccessException">If this code
        /// does not have sufficient rights to read from the file.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException">The specified 
        /// path is invalid, (for example, it is on an unmapped drive).</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the file cannot
        /// be found.</exception>
        /// <exception cref="System.IO.PathTooLongException">The specified path,
        /// file name, or both exceed the system-defined maximum length. For 
        /// example, on Windows-based platforms, paths must be less than 248
        /// characters, and file names must be less than 260 characters.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are 
        /// syntax errors in the file.</exception>
        public IMethod Load(string path, bool @override)
        {
            int i;
            return Load(path, null, @override, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a specified file.
        /// </summary>
        /// <param name="path">The path to the file to load.</param>
        /// <param name="name">The name to give the chunk.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentException">path is a zero-length 
        /// string, contains only white space, or contains one or more invalid
        /// characters as defined by System.IO.Path.InvalidPathChars</exception>
        /// <exception cref="System.ArgumentNullException">If path is null.</exception>
        /// <exception cref="System.UnauthorizedAccessException">If this code
        /// does not have sufficient rights to read from the file.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException">The specified 
        /// path is invalid, (for example, it is on an unmapped drive).</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the file cannot
        /// be found.</exception>
        /// <exception cref="System.IO.PathTooLongException">The specified path,
        /// file name, or both exceed the system-defined maximum length. For 
        /// example, on Windows-based platforms, paths must be less than 248
        /// characters, and file names must be less than 260 characters.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are 
        /// syntax errors in the file.</exception>
        public IMethod Load(string path, string name)
        {
            int i;
            return Load(path, name, false, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a specified file.
        /// </summary>
        /// <param name="path">The path to the file to load.</param>
        /// <param name="override">True to load the file even if it is in the 
        /// cache, otherwise false.</param>
        /// <param name="name">The name to give the chunk.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentException">path is a zero-length 
        /// string, contains only white space, or contains one or more invalid
        /// characters as defined by System.IO.Path.InvalidPathChars</exception>
        /// <exception cref="System.ArgumentNullException">If path is null.</exception>
        /// <exception cref="System.UnauthorizedAccessException">If this code
        /// does not have sufficient rights to read from the file.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException">The specified 
        /// path is invalid, (for example, it is on an unmapped drive).</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the file cannot
        /// be found.</exception>
        /// <exception cref="System.IO.PathTooLongException">The specified path,
        /// file name, or both exceed the system-defined maximum length. For 
        /// example, on Windows-based platforms, paths must be less than 248
        /// characters, and file names must be less than 260 characters.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are 
        /// syntax errors in the file.</exception>
        public IMethod Load(string path, string name, bool @override)
        {
            int i;
            return Load(path, name, @override, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a specified file.
        /// </summary>
        /// <param name="path">The path to the file to load.</param>
        /// <param name="override">True to load the file even if it is in the 
        /// cache, otherwise false.</param>
        /// <param name="index">Stores the index of the loaded module.</param>
        /// <param name="name">The name to give the chunk.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentException">path is a zero-length 
        /// string, contains only white space, or contains one or more invalid
        /// characters as defined by System.IO.Path.InvalidPathChars</exception>
        /// <exception cref="System.ArgumentNullException">If path is null.</exception>
        /// <exception cref="System.UnauthorizedAccessException">If this code
        /// does not have sufficient rights to read from the file.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException">The specified 
        /// path is invalid, (for example, it is on an unmapped drive).</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the file cannot
        /// be found.</exception>
        /// <exception cref="System.IO.PathTooLongException">The specified path,
        /// file name, or both exceed the system-defined maximum length. For 
        /// example, on Windows-based platforms, paths must be less than 248
        /// characters, and file names must be less than 260 characters.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are 
        /// syntax errors in the file.</exception>
        public IMethod Load(string path, string name, bool @override, out int index)
        {
            if (path == null)
                throw new ArgumentNullException("path");

            using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read))
            {
                return this.Load(fs, name, @override, out index);
            }
        }
        /// <summary>
        /// Loads a LuaChunk from a specified stream.
        /// </summary>
        /// <param name="stream">The stream to load the script from.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentNullException">If stream is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the file.</exception>
        public IMethod Load(Stream stream)
        {
            int i;
            return this.Load(stream, null, false, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a specified stream.
        /// </summary>
        /// <param name="stream">The stream to load the script from.</param>
        /// <param name="name">The name to give the chunk.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentNullException">If stream is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the file.</exception>
        public IMethod Load(Stream stream, string name)
        {
            int i;
            return this.Load(stream, name, false, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a specified stream.
        /// </summary>
        /// <param name="stream">The stream to load the script from.</param>
        /// <param name="override">True to load the file even if it is in the cache, otherwise false.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentNullException">If stream is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the file.</exception>
        public IMethod Load(Stream stream, bool @override)
        {
            int i;
            return this.Load(stream, null, @override, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a specified stream.
        /// </summary>
        /// <param name="stream">The stream to load the script from.</param>
        /// <param name="name">The name to give the chunk.</param>
        /// <param name="override">True to load the file even if it is in the 
        /// cache, otherwise false.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentNullException">If stream is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the file.</exception>
        public IMethod Load(Stream stream, string name, bool @override)
        {
            int i;
            return Load(stream, name, @override, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a specified stream.
        /// </summary>
        /// <param name="stream">The stream to load the script from.</param>
        /// <param name="name">The name to give the chunk.</param>
        /// <param name="override">True to load the file even if it is in the 
        /// cache, otherwise false.</param>
        /// <param name="index">Stores the index of the loaded module.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentNullException">If stream is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the file.</exception>
        public IMethod Load(Stream stream, string name, bool @override, out int index)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            using (TextReader c = new StreamReader(stream))
            {
                lock (this)
                {
                    var temp = Environment.Parser.UseCache;
                    Environment.Parser.UseCache = !@override;
                    var ret = Environment.CodeCompiler.Compile(
                        Environment,
                        PlainParser.Parse(Environment.Parser, c.ReadToEnd(), name),
                        name);
                    Environment.Parser.UseCache = temp;

                    if (!_chunks.Contains(ret))
                        _chunks.Add(ret);
                    index = _chunks.IndexOf(ret);
                    return ret;
                }
            }
        }
        /// <summary>
        /// Loads a LuaChunk from a pre-loaded string.
        /// </summary>
        /// <param name="chunk">The Lua script to load from.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentNullException">If chunk is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the file.</exception>
        public IMethod LoadText(string chunk)
        {
            int i;
            return this.LoadText(chunk, null, false, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a pre-loaded string.
        /// </summary>
        /// <param name="chunk">The Lua script to load from.</param>
        /// <param name="name">The name to give the chunk.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentNullException">If chunk is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the file.</exception>
        public IMethod LoadText(string chunk, string name)
        {
            int i;
            return this.LoadText(chunk, name, false, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a pre-loaded string.
        /// </summary>
        /// <param name="chunk">The Lua script to load from.</param>
        /// <param name="override">True to load the file even if it is in the cache, otherwise false.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentNullException">If chunk is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the file.</exception>
        public IMethod LoadText(string chunk, bool @override)
        {
            int i;
            return this.LoadText(chunk, null, @override, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a pre-loaded string.
        /// </summary>
        /// <param name="chunk">The Lua script to load from.</param>
        /// <param name="name">The name to give the chunk.</param>
        /// <param name="override">True to load the file even if it is in the cache, otherwise false.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentNullException">If chunk is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the file.</exception>
        public IMethod LoadText(string chunk, string name, bool @override)
        {
            int i;
            return this.LoadText(chunk, name, @override, out i);
        }
        /// <summary>
        /// Loads a LuaChunk from a pre-loaded string.
        /// </summary>
        /// <param name="chunk">The Lua script to load from.</param>
        /// <param name="name">The name to give the chunk.</param>
        /// <param name="override">True to load the file even if it is in the cache, otherwise false.</param>
        /// <param name="index">Stores the index of the loaded module.</param>
        /// <returns>The loaded chunk.</returns>
        /// <exception cref="System.ArgumentNullException">If chunk is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is 
        /// syntax errors in the file.</exception>
        public IMethod LoadText(string chunk, string name, bool @override, out int index)
        {
            if (chunk == null)
                throw new ArgumentNullException("chunk");

            lock (this)
            {
                var temp = Environment.Parser.UseCache;
                Environment.Parser.UseCache = !@override;
                var ret = Environment.CodeCompiler.Compile(
                    Environment,
                    PlainParser.Parse(Environment.Parser, chunk, name),
                    name);
                Environment.Parser.UseCache = temp;

                if (!_chunks.Contains(ret))
                    _chunks.Add(ret);
                index = _chunks.IndexOf(ret);
                return ret;
            }
        }

        #endregion

        #region public static dynamic GetVariable(...)

        /// <summary>
        /// Gets a variable from a given Lua file using the default
        /// environment and the default settings.
        /// </summary>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="name">The name of the variable to get.</param>
        /// <returns>The value of the variable or null if not found.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static dynamic GetVariable(string path, string name)
        {
            return GetVariables(new LuaEnvironmentNet(new LuaSettings()), path, new[] { name })[0];
        }
        /// <summary>
        /// Gets a variable from a given Lua file using the default environment and
        /// the given settings.
        /// </summary>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="name">The name of the variable to get.</param>
        /// <param name="settings">The settings used to load the chunk.</param>
        /// <returns>The value of the variable or null if not found.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static dynamic GetVariable(LuaSettings settings, string path, string name)
        {
            return GetVariables(new LuaEnvironmentNet(settings), path, new[] { name })[0];
        }
        /// <summary>
        /// Gets a variable from a given Lua file using the given environment.
        /// </summary>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="name">The name of the variable to get.</param>
        /// <param name="E">The environment used to load the chunk.</param>
        /// <returns>The value of the variable or null if not found.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static dynamic GetVariable(ILuaEnvironment E, string path, string name)
        {
            return GetVariables(E, path, new[] { name })[0];
        }
        /// <summary>
        /// Gets variables from a given Lua file using the default
        /// environment and the default settings.
        /// </summary>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="names">The names of the variables to get.</param>
        /// <returns>The value of the variables.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format -or- if names contains a null string.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static dynamic[] GetVariables(string path, params string[] names)
        {
            return GetVariables(new LuaEnvironmentNet(new LuaSettings()), path, names);
        }
        /// <summary>
        /// Gets variables from a given Lua file using the default environment and
        /// the given settings.
        /// </summary>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="names">The names of the variables to get.</param>
        /// <param name="settings">The settings used to load the chunk.</param>
        /// <returns>The value of the variables.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format -or- if names contains a null string.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static dynamic[] GetVariables(LuaSettings settings, string path, params string[] names)
        {
            return GetVariables(new LuaEnvironmentNet(settings), path, names);
        }
        /// <summary>
        /// Gets variables from a given Lua file using the given environment.
        /// </summary>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="names">The names of the variables to get.</param>
        /// <param name="E">The environment used to load the chunk.</param>
        /// <returns>The value of the variables.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format -or- if names contains a null string.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static dynamic[] GetVariables(ILuaEnvironment E, string path, params string[] names)
        {
            if (names == null)
                throw new ArgumentNullException("names");
            if (path == null)
                throw new ArgumentNullException("path");
            if (E == null)
                throw new ArgumentNullException("E");
            if (names.Contains(null))
                throw new ArgumentException(string.Format(Resources.CannotContainNull, "names"));

            path = Path.GetFullPath(path);
            if (!File.Exists(path))
                throw new FileNotFoundException();

            var ret = E.CodeCompiler.Compile(E,
                PlainParser.Parse(E.Parser, File.ReadAllText(path), Path.GetFileNameWithoutExtension(path)),
                Path.GetFileNameWithoutExtension(path));
            ret.Invoke(null, false, new int[0], new object[0]);
            return names.Select(s => E[s]).ToArray();
        }
        /// <summary>
        /// Gets a variable from a given Lua file using the default
        /// environment and the default settings.
        /// </summary>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="name">The name of the variable to get.</param>
        /// <returns>The value of the variable or null if not found.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="System.InvalidCastException">If the variable
        /// could not be converted to the given type.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static T GetVariable<T>(string path, string name)
        {
            return GetVariables<T>(new LuaEnvironmentNet(new LuaSettings()), path, new[] { name })[0];
        }
        /// <summary>
        /// Gets a variable from a given Lua file using the default
        /// environment with the given settings.
        /// </summary>
        /// <param name="settings">The settings used to load the chunk.</param>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="name">The name of the variable to get.</param>
        /// <returns>The value of the variable or null if not found.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="System.InvalidCastException">If the variable
        /// could not be converted to the given type.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static T GetVariable<T>(LuaSettings settings, string path, string name)
        {
            return GetVariables<T>(new LuaEnvironmentNet(settings), path, new[] { name })[0];
        }
        /// <summary>
        /// Gets a variable from a given Lua file using the given environment.
        /// </summary>
        /// <param name="E">The environment used to load the chunk.</param>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="name">The name of the variable to get.</param>
        /// <returns>The value of the variable or null if not found.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="System.InvalidCastException">If the variable
        /// could not be converted to the given type.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static T GetVariable<T>(ILuaEnvironment E, string path, string name)
        {
            return GetVariables<T>(E, path, new[] { name })[0];
        }
        /// <summary>
        /// Gets a variables from a given Lua file using the default
        /// environment and the default settings.
        /// </summary>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="names">The names of the variable to get.</param>
        /// <returns>The values of the variables or null if not found.  The array is
        /// never null.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format -or- if names contains a null string.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="System.InvalidCastException">If one of the variables
        /// could not be converted to the given type.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static T[] GetVariables<T>(string path, params string[] names)
        {
            return GetVariables<T>(new LuaEnvironmentNet(new LuaSettings()), path, names);
        }
        /// <summary>
        /// Gets a variables from a given Lua file using the default
        /// environment and the given settings.
        /// </summary>
        /// <param name="settings">The settings used to load the chunk.</param>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="names">The names of the variable to get.</param>
        /// <returns>The values of the variables or null if not found.  The array is
        /// never null.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format -or- if names contains a null string.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="System.InvalidCastException">If one of the variables
        /// could not be converted to the given type.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static T[] GetVariables<T>(LuaSettings settings, string path, params string[] names)
        {
            return GetVariables<T>(new LuaEnvironmentNet(settings), path, names);
        }
        /// <summary>
        /// Gets a variables from a given Lua file using the given environment.
        /// </summary>
        /// <param name="E">The environment used to load the chunk.</param>
        /// <param name="path">The path to the Lua file.</param>
        /// <param name="names">The names of the variable to get.</param>
        /// <returns>The values of the variables or null if not found.  The array is
        /// never null.</returns>
        /// <exception cref="System.ArgumentException">If path is not in the 
        /// correct format -or- if names contains a null string.</exception>
        /// <exception cref="System.ArgumentNullException">If any arguments are null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">If the given file
        /// chould not be found.</exception>
        /// <exception cref="System.InvalidCastException">If one of the variables
        /// could not be converted to the given type.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there are
        /// syntax errors in the file.</exception>
        public static T[] GetVariables<T>(ILuaEnvironment E, string path, params string[] names)
        {
            if (names == null)
                throw new ArgumentNullException("names");
            if (path == null)
                throw new ArgumentNullException("path");
            if (E == null)
                throw new ArgumentNullException("E");
            if (names.Contains(null))
                throw new ArgumentException(string.Format(Resources.CannotContainNull, "names"));

            path = Path.GetFullPath(path);
            if (!File.Exists(path))
                throw new FileNotFoundException();

            var ret = E.CodeCompiler.Compile(E,
                PlainParser.Parse(E.Parser, File.ReadAllText(path), Path.GetFileNameWithoutExtension(path)),
                Path.GetFileNameWithoutExtension(path));
            ret.Invoke(null, false, new int[0], new object[0]);
            return names.Select(s => (T)E.Runtime.ConvertType(E[s], typeof(T))).ToArray();
        }

        #endregion
    }
}