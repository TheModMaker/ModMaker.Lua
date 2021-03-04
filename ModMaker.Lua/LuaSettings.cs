// Copyright 2012 Jacob Trimble
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security;
using ModMaker.Lua.Runtime;
using System.IO;

namespace ModMaker.Lua
{
    /// <summary>
    /// Defines which libraries the Lua code will have access to. Use Bitwise-Or
    /// to use multiple.
    /// </summary>
    [Flags]
    public enum LuaLibraries : byte
    {
        /// <summary>
        /// Use none of the libraries.
        /// </summary>
        None = 0,
        /// <summary>
        /// Register the standard library to Lua.
        /// </summary>
        Standard = 1 << 0,
        /// <summary>
        /// Register the string library to Lua.
        /// </summary>
        String = 1 << 1,
        /// <summary>
        /// Register the table library to Lua.
        /// </summary>
        Table = 1 << 2,
        /// <summary>
        /// Register the math library to Lua.
        /// </summary>
        Math = 1 << 3,
        /// <summary>
        /// Register the io library to Lua.
        /// </summary>
        IO = 1 << 4,
        /// <summary>
        /// Register the os library to Lua.
        /// </summary>
        OS = 1 << 5,
        /// <summary>
        /// Register the coroutine library to Lua.
        /// </summary>
        Coroutine = 1 << 6,
        /// <summary>
        /// Register the module library to Lua.
        /// </summary>
        Modules = 1 << 7,

        /// <summary>
        /// Register all the libraries to Lua.
        /// </summary>
        All = 255,
    }

    /// <summary>
    /// Defines what types Lua has access to when defining a new type.
    /// </summary>
    public enum LuaClassAccess
    {
        /// <summary>
        /// Lua can only derive from types that are registered.
        /// </summary>
        Registered,
        /// <summary>
        /// Lua can derive from types that are registered and defined in the
        /// .NET framework.
        /// </summary>
        System,
        /// <summary>
        /// Lua can derive from any type that is defined in
        /// CurrentDomain.GetAssemblies().
        /// </summary>
        All,
    }

    /// <summary>
    /// The event args for an exit event.
    /// </summary>
    public sealed class ExitEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the exit code given to os.exit(code[, close]).
        /// </summary>
        public int Code { get; private set; }

        internal ExitEventArgs(object code)
        {
            if (code != null)
            {
                if (code as bool? == true)
                    this.Code = 0;
                else if (code is double)
                    this.Code = (int)Math.Round((double)code);
                else
                    this.Code = 1;
            }
            else
                this.Code = 0;
        }
    }

    /// <summary>
    /// Defines the settings for a Lua object.
    /// </summary>
    public sealed class LuaSettings
    {
        EventHandler<ExitEventArgs> _onquit;
        LuaClassAccess _access;
        LuaLibraries _libs;
        Encoding _enc;
        Stream _in, _out;
        string _name;
        bool _nonSeek, _reflect, _ensureReturn, _readonly;

        /// <summary>
        /// Creates a new instance of LuaSettings with the default values.
        /// </summary>
        public LuaSettings()
            : this(null, null) { }
        /// <summary>
        /// Creates a new instance of LuaSettings with the default values that
        /// uses the given streams for input.
        /// </summary>
        /// <param name="stdin">The standard input stream.</param>
        /// <param name="stdout">The standard output stream.</param>
        public LuaSettings(Stream stdin, Stream stdout)
        {
            this._readonly = false;

            this._onquit = null;
            this._name = null;
            this._libs = LuaLibraries.All;
            this._access = LuaClassAccess.Registered;
            this._enc = null;
            this._nonSeek = false;
            this._reflect = false;
            this._ensureReturn = false;
            this._in = stdin;
            this._out = stdout;
        }
        /// <summary>
        /// Creates a read-only copy of the given settings.
        /// </summary>
        /// <param name="copy">The settings to copy.</param>
        LuaSettings(LuaSettings copy)
        {
            this._readonly = true;

            this._onquit = copy._onquit;
            this._name = copy._name;
            this._libs = copy._libs;
            this._access = CheckAccess(copy._access);
            this._enc = copy._enc ?? Encoding.UTF8;
            this._nonSeek = copy._nonSeek;
            this._reflect = copy._reflect;
            this._ensureReturn = copy._ensureReturn;
            this._in = copy._in;
            this._out = copy._out;
        }

        /// <summary>
        /// Creates a new copy of the current settings as a read-only version.
        /// This also sets the default values of Encoding, ModuleBinder, StdIn,
        /// and StdOut.
        /// </summary>
        /// <returns>A new copy of the current LuaSettings.</returns>
        public LuaSettings AsReadOnly()
        {
            return new LuaSettings(this);
        }

        /// <summary>
        /// Gets or sets the name of the Lua object, for use with debugging,
        /// can be null.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the settings are read-only.</exception>
        public string Name
        {
            get { return _name; }
            set
            {
                CheckReadonly();
                _name = value;
            }
        }
        /// <summary>
        /// Gets or sets the libraries that the Lua code has access too. The
        /// library must have Permission to access these and will throw
        /// PermissionExceptions if it does not, when the code is run.  If null,
        /// use the defaults, which is all of them.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the settings are read-only.</exception>
        public LuaLibraries Libraries
        {
            get { return _libs; }
            set
            {
                CheckReadonly();
                _libs = value;
            }
        }
        /// <summary>
        /// Gets or sets which types Lua defined classes can derive from.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the settings are read-only.</exception>
        public LuaClassAccess ClassAccess
        {
            get { return _access; }
            set
            {
                CheckReadonly();
                _access = value;
            }
        }
        /// <summary>
        /// Gets or sets the encoding to use for reading/writing to a file.  If
        /// null, will use UTF8.  When reading, will try to read using the file
        /// encoding.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the settings are read-only.</exception>
        public Encoding Encoding
        {
            get { return _enc; }
            set
            {
                CheckReadonly();
                _enc = value;
            }
        }
        /// <summary>
        /// Gets or sets whether to allow non-seekable streams.
        /// Default: false.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the settings are read-only.</exception>
        public bool AllowNonSeekStreams
        {
            get { return _nonSeek; }
            set
            {
                CheckReadonly();
                _nonSeek = value;
            }
        }
        /// <summary>
        /// Gets or sets whether Lua has access to .NET reflection.
        /// Default: false.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the settings are read-only.</exception>
        public bool AllowReflection
        {
            get { return _reflect; }
            set
            {
                CheckReadonly();
                _reflect = value;
            }
        }
        /// <summary>
        /// True to ensure that Lua only has access to the return type of a
        /// registered method; otherwise it has access to all members of
        /// the backing type.
        /// Default: false.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the settings are read-only.</exception>
        public bool EnsureReturnType
        {
            get { return _ensureReturn; }
            set
            {
                CheckReadonly();
                _ensureReturn = value;
            }
        }

        /// <summary>
        /// Raised when the Lua code calls os.close.  The sender
        /// is the Environment that the code is in. If e.Close
        /// is true after raising, it will call Environment.Exit.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the settings are read-only.</exception>
        public event EventHandler<ExitEventArgs> Quit
        {
            add
            {
                CheckReadonly();
                _onquit += value;
            }
            remove
            {
                CheckReadonly();
                _onquit -= value;
            }
        }

        /// <summary>
        /// Gets or sets the stream to get stdin from.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the settings are read-only.</exception>
        public Stream Stdin
        {
            get { return _in; }
            set
            {
                CheckReadonly();
                _in = value;
            }
        }
        /// <summary>
        /// Gets or sets the stream to send stdout to.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the settings are read-only.</exception>
        public Stream Stdout
        {
            get { return _out; }
            set
            {
                CheckReadonly();
                _out = value;
            }
        }

        /// <summary>
        /// Raises the Quit event.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="code">The value to use for the code.</param>
        /// <param name="close">Whether to call Environment.Exit.</param>
        internal void CallQuit(object sender, object code, object close)
        {
            ExitEventArgs e = new ExitEventArgs(code);
            if (sender != null && _onquit != null)
            {
                _onquit(sender, e);
                //if (e.Close)
                //{
                //    Environment.Exit(e.Code);
                //}
            }
        }

        /// <summary>
        /// Checks whether the settings is read-only and throws an exception
        /// if it is.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the settings are read-only.</exception>
        void CheckReadonly()
        {
            if (_readonly)
                throw new InvalidOperationException(Resources.ReadonlySettings);
        }
        /// <summary>
        /// Ensures that the LuaClassAccess is really one of the enumerated values.
        /// If it is invalid, it will use the default (Registered).
        /// </summary>
        /// <param name="access">The value to check.</param>
        /// <returns>A valid LuaClassAccess choice.</returns>
        static LuaClassAccess CheckAccess(LuaClassAccess access)
        {
            switch (access)
            {
                case LuaClassAccess.System:
                    return LuaClassAccess.System;
                case LuaClassAccess.All:
                    return LuaClassAccess.All;
                default:
                    return LuaClassAccess.Registered;
            }
        }
    }
}
