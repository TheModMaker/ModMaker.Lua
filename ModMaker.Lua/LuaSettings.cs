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
using System.IO;
using System.Text;

namespace ModMaker.Lua {
  /// <summary>
  /// Defines which libraries the Lua code will have access to. Use Bitwise-Or to use multiple.
  /// </summary>
  [Flags]
  public enum LuaLibraries : byte {
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
  /// The event args for an exit event.
  /// </summary>
  public sealed class ExitEventArgs : EventArgs {
    /// <summary>
    /// Gets the exit code given to os.exit(code[, close]).
    /// </summary>
    public int Code { get; private set; }

    internal ExitEventArgs(int code) {
      Code = code;
    }
  }

  /// <summary>
  /// Defines the settings for a Lua object.
  /// </summary>
  public sealed class LuaSettings {
    EventHandler<ExitEventArgs>? _onquit;
    LuaLibraries _libs;
    Encoding? _enc;
    Stream? _in;
    Stream? _out;
    bool _native_symbols;
    readonly bool _readonly;

    /// <summary>
    /// Creates a new instance of LuaSettings with the default values.  Note this doesn't set
    /// standard in/out, this may cause problems with print and io libraries.  See
    /// Console.OpenStandard*.
    /// </summary>
    public LuaSettings() : this(null, null) { }
    /// <summary>
    /// Creates a new instance of LuaSettings with the default values that uses the given streams
    /// for input.
    /// </summary>
    /// <param name="stdin">The standard input stream.</param>
    /// <param name="stdout">The standard output stream.</param>
    public LuaSettings(Stream? stdin, Stream? stdout) {
      _readonly = false;
      _onquit = null;
      _libs = LuaLibraries.All;
      _in = stdin;
      _out = stdout;
#if DEBUG
      _native_symbols = true;
#else
      _native_symbols = false;
#endif
    }
    /// <summary>
    /// Creates a read-only copy of the given settings.
    /// </summary>
    /// <param name="copy">The settings to copy.</param>
    LuaSettings(LuaSettings copy) {
      _readonly = true;
      _onquit = copy._onquit;
      _libs = copy._libs;
      _enc = copy._enc;
      _in = copy._in;
      _out = copy._out;
      _native_symbols = copy._native_symbols;
    }

    /// <summary>
    /// Creates a new copy of the current settings as a read-only version.
    /// </summary>
    /// <returns>A new copy of the current LuaSettings.</returns>
    public LuaSettings AsReadOnly() {
      return new LuaSettings(this);
    }

    /// <summary>
    /// Gets or sets the libraries that the Lua code has access too. The library must have
    /// Permission to access these and will throw PermissionExceptions if it does not, when the code
    /// is run.  If null, use the defaults, which is all of them.
    /// </summary>
    public LuaLibraries Libraries {
      get { return _libs; }
      set {
        _checkReadonly();
        _libs = value;
      }
    }
    /// <summary>
    /// Gets or sets the encoding to use for reading/writing to a file.  If null, will use UTF8.
    /// When reading, will try to read using the file encoding.
    /// </summary>
    public Encoding? Encoding {
      get { return _enc; }
      set {
        _checkReadonly();
        _enc = value;
      }
    }

    /// <summary>
    /// If true, the generated code will include native debugging symbols.  This can allow debugging
    /// Lua code in Visual Studio.  This does NOT affect the Lua "debug" library.
    /// </summary>
    public bool AddNativeDebugSymbols {
      get { return _native_symbols; }
      set {
        _checkReadonly();
        _native_symbols = value;
      }
    }

    /// <summary>
    /// Raised when the Lua code calls os.close.  The sender is the Environment that the code is in.
    /// If e.Close is true after raising, it will call Environment.Exit.
    /// </summary>
    public event EventHandler<ExitEventArgs> Quit {
      add {
        _checkReadonly();
        _onquit += value;
      }
      remove {
        _checkReadonly();
        _onquit -= value;
      }
    }

    /// <summary>
    /// Gets or sets the stream to get stdin from.
    /// </summary>
    public Stream? Stdin {
      get { return _in; }
      set {
        _checkReadonly();
        _in = value;
      }
    }
    /// <summary>
    /// Gets or sets the stream to send stdout to.
    /// </summary>
    public Stream? Stdout {
      get { return _out; }
      set {
        _checkReadonly();
        _out = value;
      }
    }

    /// <summary>
    /// Raises the Quit event.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="code">The value to use for the code.</param>
    /// <param name="close">Whether to call Environment.Exit.</param>
    internal void _callQuit(object? sender, int code) {
      ExitEventArgs e = new ExitEventArgs(code);
      if (sender != null && _onquit != null) {
        _onquit(sender, e);
        //if (e.Close) {
        //  Environment.Exit(e.Code);
        //}
      }
    }

    /// <summary>
    /// Checks whether the settings is read-only and throws an exception if it is.
    /// </summary>
    void _checkReadonly() {
      if (_readonly) {
        throw new InvalidOperationException(Resources.ReadonlySettings);
      }
    }
  }
}
