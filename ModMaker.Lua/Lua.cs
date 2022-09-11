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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using ModMaker.Lua.Compiler;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;

#nullable enable

[assembly: InternalsVisibleTo("Unit Tests")]

namespace ModMaker.Lua {
  /// <summary>
  /// The main manager of a Lua state. Manages lua chunks, the environments, and registering types
  /// and global methods.
  /// </summary>
  [LuaIgnore]
  public sealed class Lua {
    /// <summary>
    /// Creates a new Lua object using the default settings.
    /// </summary>
    public Lua() : this(null) { }
    /// <summary>
    /// Creates a new Lua object using the given settings.
    /// </summary>
    /// <param name="settings">The settings to use.</param>
    public Lua(LuaSettings? settings) {
      ReflectionMembers.EnsureInitialized();
      Environment = new LuaEnvironmentNet(
          settings ?? new LuaSettings(Console.OpenStandardInput(), Console.OpenStandardOutput()));
    }

    /// <summary>
    /// Gets or sets whether to internally use dynamic types.  Without dynamic types, this framework
    /// does not support proper tail calls.
    /// </summary>
    public static bool UseDynamicTypes { get; set; }

    /// <summary>
    /// Gets or sets the global Lua variable with the given name.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    public dynamic this[string name] {
      get { return Environment[name]; }
      set { Environment[name] = LuaValueBase.CreateValue((object)value); }
    }

    /// <summary>
    /// Gets the settings for the current environment.
    /// </summary>
    public LuaSettings Settings { get { return Environment.Settings; } }
    /// <summary>
    /// Gets or sets the global environment.
    /// </summary>
    public ILuaEnvironment Environment { get; set; }

    /// <summary>
    /// Registers a delegate with the given name for use with this Lua object.
    /// </summary>
    /// <param name="func">The delegate to register.</param>
    /// <param name="name">
    /// The name of the delegate.  If null, will use the function name.
    /// </param>
    public void Register(Delegate func, string? name = null) {
      if (func.GetInvocationList().Length > 1) {
        throw new MulticastNotSupportedException(Resources.MulticastNotSupported);
      }

      Environment.RegisterDelegate(func, name ?? func.Method.Name);
    }
    /// <summary>
    /// Registers a type for use within Lua.
    /// </summary>
    /// <param name="type">The type to register, cannot be null.</param>
    public void Register(Type type) {
      Environment.RegisterType(type, type.Name);
    }

    /// <summary>
    /// Reads and executes the file at the path specified.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="args">The arguments to pass.</param>
    /// <returns>The values returned from the file.</returns>
    public object?[] DoFile(string path, params object[] args) {
      var ret = CompileFile(path);
      return ret.Invoke(LuaNil.Nil, false, LuaMultiValue.CreateMultiValueFromObj(args))
          .Select(v => v.GetValue())
          .ToArray();
    }
    /// <summary>
    /// Reads and executes the file from the given stream.
    /// </summary>
    /// <param name="stream">The stream to read the file from.</param>
    /// <param name="args">The arguments to pass.</param>
    /// <returns>The values returned from the file.</returns>
    public object?[] DoFile(Stream stream, params object[] args) {
      var ret = CompileFile(stream);
      return ret.Invoke(LuaNil.Nil, false, LuaMultiValue.CreateMultiValueFromObj(args))
          .Select(v => v.GetValue())
          .ToArray();
    }
    /// <summary>
    /// Executes the specified text.
    /// </summary>
    /// <param name="chunk">The chunk to execute.</param>
    /// <param name="args">The arguments to pass.</param>
    /// <returns>The values returned from the file.</returns>
    public object?[] DoText(string chunk, params object[] args) {
      var ret = CompileText(chunk);
      return ret.Invoke(LuaNil.Nil, false, LuaMultiValue.CreateMultiValueFromObj(args))
          .Select(v => v.GetValue())
          .ToArray();
    }

    /// <summary>
    /// Reads and compiles the given file into an executable object.
    /// </summary>
    /// <param name="path">The path to the file to load.</param>
    /// <param name="name">The name to give the chunk.</param>
    /// <param name="encoding">
    /// The encoding to read the file in.  If not given, will attempt to use byte-order marks to
    /// guess; otherwise will fallback to UTF-8.
    /// </param>
    /// <returns>The loaded chunk.</returns>
    public ILuaValue CompileFile(string path, string name = "", Encoding? encoding = null) {
      using FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read);
      return CompileFile(fs, name ?? Path.GetFileName(path), encoding);
    }
    /// <summary>
    /// Reads and compiles the given file into an executable object.
    /// </summary>
    /// <param name="stream">The stream to load the script from.</param>
    /// <param name="name">The name to give the chunk.</param>
    /// <param name="encoding">
    /// The encoding to read the file in.  If not given, will attempt to use byte-order marks to
    /// guess; otherwise will fallback to UTF-8.
    /// </param>
    /// <returns>The loaded chunk.</returns>
    public ILuaValue CompileFile(Stream stream, string name = "", Encoding? encoding = null) {
      return Environment.CodeCompiler.Compile(
          Environment, Environment.Parser.Parse(stream, encoding ?? Settings.Encoding, name), name);
    }
    /// <summary>
    /// Compiles the given text into an executable object.
    /// </summary>
    /// <param name="chunk">The Lua script to load from.</param>
    /// <param name="name">The name to give the chunk.</param>
    /// <returns>The loaded chunk.</returns>
    public ILuaValue CompileText(string chunk, string name = "") {
      return Environment.CodeCompiler.Compile(
          Environment, Environment.Parser.Parse(chunk, name), name);
    }
  }
}
