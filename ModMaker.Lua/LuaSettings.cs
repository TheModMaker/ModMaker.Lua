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
    /// Defines which libraries the Lua code will have
    /// access to. Use Bitwise-Or to use multiple.
    /// </summary>
    [Flags]
    public enum LuaLibraries
    {
        /// <summary>
        /// Default value, changed to LuaLibraries.All when
        /// given to the Lua object.
        /// </summary>
        UseDefaults = 0,
        /// <summary>
        /// Register the string library to Lua.
        /// </summary>
        String = 1 << 0,
        /// <summary>
        /// Register the table library to Lua.
        /// </summary>
        Table = 1 << 1,
        /// <summary>
        /// Register the math library to Lua.
        /// </summary>
        Math = 1 << 2,
        /// <summary>
        /// Register the io library to Lua.
        /// </summary>
        IO = 1 << 3,
        /// <summary>
        /// Register the os library to Lua.
        /// </summary>
        OS = 1 << 4,
        /// <summary>
        /// Register the coroutine library to Lua.
        /// </summary>
        Coroutine = 1 << 5,
        /// <summary>
        /// Register the module library to Lua.
        /// </summary>
        Modules = 1 << 6,
        /// <summary>
        /// Register the bit32 library to Lua.
        /// </summary>
        Bit32 = 1 << 7,

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
        /// Lua can only derrive from types that are registered.
        /// </summary>
        Registered,
        /// <summary>
        /// Lua can derrive from types that are registered and defined in the .NET framework.
        /// </summary>
        System,
        /// <summary>
        /// Lua can derrive from any type that is defined in CurrentDomain.GetAssemblies().
        /// </summary>
        All,
    }

    /// <summary>
    /// The event args for an exit event.
    /// </summary>
    public class ExitEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the exit code given to os.exit(code[, close]).
        /// </summary>
        public int Code { get; private set; }
        /// <summary>
        /// Gets or sets whether Lua should call Environment.Exit after returning.  
        /// Default: false.
        /// </summary>
        public bool Close { get; set; }

        internal ExitEventArgs(object code, object close)
        {
            code = RuntimeHelper.GetValue(code);
            close = RuntimeHelper.GetValue(close);
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

            if (close == null || close as bool? == false)
                this.Close = false;
            else
                this.Close = true;
        }
    }

    /// <summary>
    /// Defines the settings for a Lua object.
    /// </summary>
    public struct LuaSettings
    {
        /// <summary>
        /// Gets or sets the name of the Lua object, for use with debugging, can be null.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Gets or sets the libraries that the Lua code has access too.
        /// The library must have Permission to access these and
        /// will throw PermissionExceptions if it does not, when the 
        /// code is run.  If null, use the defaults, which is all
        /// but Debug.
        /// </summary>
        public LuaLibraries Libraries { get; set; }
        /// <summary>
        /// Gets or sets which types Lua defined classes can derrive from.
        /// </summary>
        public LuaClassAccess ClassAccess { get; set; }
        /// <summary>
        /// Gets or sets the encoding to use for reading/writing to a file.  If null,
        /// will use UTF8.  When reading, will try to read using the file encoding.
        /// </summary>
        public Encoding Encoding { get; set; }
        /// <summary>
        /// Gets or sets the binder to use when loading modules.  Set
        /// to null to use the default binder.
        /// </summary>
        public IModuleBinder ModuleBinder { get; set; }
        /// <summary>
        /// Gets or sets whether to allow non-seekable streams.
        /// Default: false.
        /// </summary>
        public bool AllowNonSeekStreams { get; set; }

        /// <summary>
        /// Raised when the Lua code calls os.close.  The sender
        /// is the Environment that the code is in. If e.Close
        /// is true after raising, it will call Environment.Exit.
        /// </summary>
        public event EventHandler<ExitEventArgs> OnQuit;

        /// <summary>
        /// Gets or sets the stream to get stdin from.
        /// This can be null to use Console.OpenStandardInput().
        /// </summary>
        public Stream Stdin { get; set; }
        /// <summary>
        /// Gets or sets the stream to send stdout to.
        /// This can be null to use Console.OpenStandardOutput().
        /// </summary>
        public Stream Stdout { get; set; }

        internal void CallQuit(object sender, object code, object close)
        {
            ExitEventArgs e = new ExitEventArgs(code, close);
            if (sender != null)
            {
                OnQuit(sender, e);
                if (e.Close)
                {
                    Environment.Exit(e.Code);
                }
            }
        }
    }
}