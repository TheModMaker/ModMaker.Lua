// Copyright 2014 Jacob Trimble
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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ModMaker.Lua.Runtime.LuaValues;

#if NET48
// .NET 4.8 doesn't have NotNullWhen, so we get these false-positive warnings for null values.
#pragma warning disable 8602, 8603, 8604
#endif

namespace ModMaker.Lua.Runtime {
  static partial class LuaStaticLibraries {
    static class IO {
      const bool kInput = false;
      const bool kOutput = true;
      const bool kRequireStream = true;
      const bool kAnyValue = false;

      static readonly LuaString _stream = new LuaString("Stream");
      static Stream? _input = null;
      static Stream? _output = null;

      public static void Initialize(ILuaEnvironment env) {
        ILuaTable io = new LuaTable();
        io.SetItemRaw(new LuaString("close"), new close(env));
        io.SetItemRaw(new LuaString("flush"), new flush(env));
        io.SetItemRaw(new LuaString("input"), new input(env));
        io.SetItemRaw(new LuaString("lines"), new lines(env));
        io.SetItemRaw(new LuaString("open"), new open(env));
        io.SetItemRaw(new LuaString("output"), new output(env));
        io.SetItemRaw(new LuaString("read"), new read(env));
        io.SetItemRaw(new LuaString("tmpfile"), new tmpfile(env));
        io.SetItemRaw(new LuaString("type"), new type(env));
        io.SetItemRaw(new LuaString("write"), new write(env));

        _input = env.Settings.Stdin;
        _output = env.Settings.Stdout;
        var globals = env.GlobalsTable;
        globals.SetItemRaw(new LuaString("io"), io);
        globals.SetItemRaw(new LuaString("dofile"), new dofile(env));
        globals.SetItemRaw(new LuaString("load"), new load(env));
        globals.SetItemRaw(new LuaString("loadfile"), new loadfile(env));
      }

      sealed class LinesHelper : LuaFunction {
        StreamReader? _stream;
        readonly bool _close;
        readonly int[] _ops; // -4 = *L, -3 = *l, -2 = *a, -1 = *n

        public LinesHelper(ILuaEnvironment env, bool close, Stream stream, int[] ops)
            : base(env, "io.lines") {
          _stream = new StreamReader(stream);
          _close = close;
          _ops = ops;
        }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          if (_stream == null) {
            return LuaMultiValue.Empty;
          }

          var ret = _read(_ops, _stream);

          if (_stream.EndOfStream) {
            if (_close) {
              _stream.Close();
            }

            _stream = null;
          }

          return ret;
        }
      }

      // io functions
      sealed class close : LuaFunction {
        public close(ILuaEnvironment env) : base(env, "io.close") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          if (!_getStream(args[0],kOutput, kRequireStream, out Stream? s, out LuaMultiValue? ret))
            return ret;

          try {
            s.Close();
            return new LuaMultiValue(_createFile(s, Environment));
          } catch (Exception e) {
            return LuaMultiValue.CreateMultiValueFromObj(null, e.Message, e);
          }
        }
      }
      sealed class flush : LuaFunction {
        public flush(ILuaEnvironment env) : base(env, "io.flush") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          if (!_getStream(args[0], kOutput, kRequireStream, out Stream? s, out LuaMultiValue? ret))
            return ret;

          try {
            s.Flush();
            return new LuaMultiValue(_createFile(s, Environment));
          } catch (Exception e) {
            return LuaMultiValue.CreateMultiValueFromObj(null, e.Message, e);
          }
        }
      }
      sealed class input : LuaFunction {
        public input(ILuaEnvironment env) : base(env, "io.input") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          ILuaValue obj = args[0];

          if (obj != null) {
            if (obj.ValueType == LuaValueType.String) {
              Stream s = File.OpenRead((string)obj.GetValue()!);
              _input = s;
            } else if (obj.ValueType == LuaValueType.Table) {
              Stream? s = ((ILuaTable)obj).GetItemRaw(_stream) as Stream;
              if (s == null) {
                throw new InvalidOperationException(
                    "First argument to function 'io.input' must be a file-stream or a string " +
                    "path.");
              }

              _input = s;
            } else if (obj is Stream st) {
              _input = st;
            } else {
              throw new InvalidOperationException(
                  "First argument to function 'io.input' must be a file-stream or a string path.");
            }
          } else if (_input == null) {
            return LuaMultiValue.Empty;
          }

          return new LuaMultiValue(_createFile(_input, Environment));
        }
      }
      sealed class lines : LuaFunction {
        public lines(ILuaEnvironment env) : base(env, "io.lines") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          Stream? s;
          bool close;
          int start;
          if (args[0].GetValue() is string oString) {
            s = File.OpenRead(oString);
            close = true;
            start = 1;
          } else {
            if (!_getStream(args[0], kInput, kAnyValue, out s, out LuaMultiValue? ret))
              return ret;
            close = false;
            start = s == _input ? 0 : 1;
          }

          int[] a = _parse(args.Skip(start), "io.lines");
          return new LuaMultiValue(new LinesHelper(Environment, close, s, a));
        }
      }
      sealed class open : LuaFunction {
        public open(ILuaEnvironment env) : base(env, "io.open") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          string? s = args[0].GetValue() as string;
          string? mode = args[1].GetValue() as string;
          FileMode fileMode;
          FileAccess access;
          bool seek = false;
          mode = mode?.ToLowerInvariant();

          if (string.IsNullOrWhiteSpace(s)) {
            return LuaMultiValue.CreateMultiValueFromObj(
                null, "First argument must be a string filename.");
          }

          switch (mode) {
            case "r":
            case "rb":
            case "":
            case null:
              fileMode = FileMode.Open;
              access = FileAccess.Read;
              break;
            case "w":
            case "wb":
              fileMode = FileMode.Create;
              access = FileAccess.Write;
              break;
            case "a":
            case "ab":
              fileMode = FileMode.OpenOrCreate;
              access = FileAccess.ReadWrite;
              seek = true;
              break;
            case "r+":
            case "r+b":
              fileMode = FileMode.Open;
              access = FileAccess.ReadWrite;
              break;
            case "w+":
            case "w+b":
              fileMode = FileMode.Create;
              access = FileAccess.ReadWrite;
              break;
            case "a+":
            case "a+b":
              fileMode = FileMode.OpenOrCreate;
              access = FileAccess.ReadWrite;
              seek = true;
              break;
            default:
              return LuaMultiValue.CreateMultiValueFromObj(
                  null, "Second argument must be a valid string mode.");
          }

          try {
            using (Stream stream = File.Open(s, fileMode, access)) {
              if (seek && stream.CanSeek) {
                stream.Seek(0, SeekOrigin.End);
              }

              return new LuaMultiValue(_createFile(stream, Environment));
            }
          } catch (Exception e) {
            return LuaMultiValue.CreateMultiValueFromObj(null, e.Message, e);
          }
        }
      }
      sealed class output : LuaFunction {
        public output(ILuaEnvironment env) : base(env, "io.output") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          ILuaValue obj = args[0];
          if (obj != LuaNil.Nil) {
            if (obj.ValueType == LuaValueType.String) {
              Stream s = File.OpenRead((string)obj.GetValue()!);
              _output = s;
            } else if (obj.ValueType == LuaValueType.Table) {
              Stream? s = ((ILuaTable)obj).GetItemRaw(_stream) as Stream;
              if (s == null) {
                throw new InvalidOperationException("First argument to function 'io.output' must " +
                                                    "be a file-stream or a string path.");
              }

              _output = s;
            } else if (obj is Stream st) {
              _output = st;
            } else {
              throw new InvalidOperationException(
                  "First argument to function 'io.output' must be a file-stream or a string path.");
            }
          } else if (_output == null) {
            return LuaMultiValue.Empty;
          }

          return new LuaMultiValue(_createFile(_output, Environment));
        }
      }
      sealed class read : LuaFunction {
        public read(ILuaEnvironment env) : base(env, "io.read") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          if (!_getStream(args[0], kInput, kAnyValue, out Stream? s, out LuaMultiValue? ret))
            return ret;
          int start = s == _input ? 0 : 1;

          int[] a = _parse(args.Skip(start), "io.read");
          return _read(a, new StreamReader(s));
        }
      }
      sealed class seek : LuaFunction {
        public seek(ILuaEnvironment env) : base(env, "io.seek") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          if (!_getStream(args[0], kInput, kAnyValue, out Stream? s, out LuaMultiValue? ret))
            return ret;
          SeekOrigin origin = SeekOrigin.Current;
          long off = 0;

          if (args.Count > 1) {
            string? str = args[1].GetValue() as string;
            if (str == "set") {
              origin = SeekOrigin.Begin;
            } else if (str == "cur") {
              origin = SeekOrigin.Current;
            } else if (str == "end") {
              origin = SeekOrigin.End;
            } else {
              throw new ArgumentException("First argument to function file:seek must be a string.");
            }

            if (args.Count > 2) {
              object? obj = args[2].GetValue();
              if (obj is double dbl) {
                off = Convert.ToInt64(dbl);
              } else {
                throw new ArgumentException(
                    "Second argument to function file:seek must be a number.");
              }
            }
          }

          if (!s.CanSeek) {
            return LuaMultiValue.CreateMultiValueFromObj(
                null, "Specified stream cannot be seeked.");
          }

          try {
            return LuaMultiValue.CreateMultiValueFromObj(Convert.ToDouble(s.Seek(off, origin)));
          } catch (Exception e) {
            return LuaMultiValue.CreateMultiValueFromObj(null, e.Message, e);
          }
        }
      }
      sealed class tmpfile : LuaFunction {
        public tmpfile(ILuaEnvironment env) : base(env, "io.tmpfile") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          string str = Path.GetTempFileName();
          Stream s = File.Open(str, FileMode.OpenOrCreate, FileAccess.ReadWrite);
          return new LuaMultiValue(_createFile(s, Environment));
        }
      }
      sealed class type : LuaFunction {
        public type(ILuaEnvironment env) : base(env, "io.type") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          ILuaValue obj = args[0];

          if (obj.GetValue() is Stream) {
            return LuaMultiValue.CreateMultiValueFromObj("file");
          } else if (obj.ValueType == LuaValueType.Table) {
            Stream? s = ((ILuaTable)obj).GetItemRaw(_stream) as Stream;
            return LuaMultiValue.CreateMultiValueFromObj(s == null ? null : "file");
          } else {
            return LuaMultiValue.Empty;
          }
        }
      }
      sealed class write : LuaFunction {
        public write(ILuaEnvironment env) : base(env, "io.write") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          if (!_getStream(args[0], kOutput, kAnyValue, out Stream? s, out LuaMultiValue? ret))
            return ret;
          int start = s == _output ? 0 : 1;

          try {
            for (int i = start; i < args.Count; i++) {
              var temp = args[i].GetValue();
              if (temp is double dbl) {
                var bt = (Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(
                    dbl.ToString(CultureInfo.InvariantCulture));
                s.Write(bt, 0, bt.Length);
              } else if (temp is string str) {
                var bt = (Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(str);
                s.Write(bt, 0, bt.Length);
              } else {
                throw new ArgumentException("Arguments to io.write must be a string or number.");
              }
            }

            return new LuaMultiValue(_createFile(s, Environment));
          } catch (ArgumentException) {
            throw;
          } catch (Exception e) {
            return LuaMultiValue.CreateMultiValueFromObj(null, e.Message, e);
          }
        }
      }

      // helper functions
      static int[] _parse(IEnumerable<ILuaValue> args, string func) {
        List<int> v = new List<int>();

        foreach (var item in args) {
          object? obj = item.GetValue();
          if (obj is double d) {
            if (d < 0) {
              throw new ArgumentOutOfRangeException(
                  $"Arguments to {func} must be a positive integer.", (Exception?)null);
            }

            v.Add(Convert.ToInt32(d));
          } else if (obj is string st) {
            if (st == "*index") {
              v.Add(-1);
            } else if (st == "*a") {
              v.Add(-2);
            } else if (st == "*l") {
              v.Add(-3);
            } else if (st == "*L") {
              v.Add(-4);
            } else {
              throw new ArgumentException("Only the following strings are valid as arguments to " +
                                          func + ": '*index', '*a', '*l', or '*L'.");
            }
          } else {
            throw new ArgumentException(
                $"Arguments to function {func} must be a number or a string.");
          }
        }

        return v.ToArray();
      }
      static LuaMultiValue _read(int[] opts, StreamReader s) {
        List<ILuaValue> ret = new List<ILuaValue>();

        foreach (var item in opts) {
          switch (item) {
            case -4:
              ret.Add(s.EndOfStream ? (ILuaValue)LuaNil.Nil : new LuaString(s.ReadLine() + "\n"));
              break;
            case -3:
              ret.Add(s.EndOfStream ? (ILuaValue)LuaNil.Nil : new LuaString(s.ReadLine()!));
              break;
            case -2:
              ret.Add(s.EndOfStream ? (ILuaValue)LuaNil.Nil : new LuaString(s.ReadToEnd()));
              break;
            case -1:
              if (s.EndOfStream) {
                ret.Add(LuaNil.Nil);
              } else {
                double d = Helpers.ReadNumber(s);
                ret.Add(LuaNumber.Create(d));
              }
              break;
            default:
              if (s.EndOfStream) {
                ret.Add(LuaNil.Nil);
              } else {
                char[] c = new char[item];
                s.Read(c, 0, item);
                ret.Add(new LuaString(new string(c)));
              }
              break;
          }
        }

        return new LuaMultiValue(ret.ToArray());
      }
      static ILuaTable _createFile(Stream backing, ILuaEnvironment env) {
        ILuaTable ret = new LuaTable();
        ret.SetItemRaw(_stream, new LuaUserData<Stream>(backing));
        ret.SetItemRaw(new LuaString("close"), new close(env));
        ret.SetItemRaw(new LuaString("flush"), new flush(env));
        ret.SetItemRaw(new LuaString("lines"), new lines(env));
        ret.SetItemRaw(new LuaString("read"), new read(env));
        ret.SetItemRaw(new LuaString("seek"), new seek(env));
        ret.SetItemRaw(new LuaString("write"), new write(env));

        return ret;
      }
      static bool _getStream(ILuaValue file, bool output, bool requireStream,
                             [NotNullWhen(true)] out Stream? s,
                             [NotNullWhen(false)] out LuaMultiValue? result) {
        s = null;
        if (file.ValueType == LuaValueType.Table) {
          s = ((ILuaTable)file).GetItemRaw(_stream).GetValue() as Stream;
        } else if (file.ValueType == LuaValueType.UserData) {
          s = file.GetValue() as Stream;
        }
        if (s == null) {
          if (requireStream) {
            result = LuaMultiValue.CreateMultiValueFromObj(
                null, "Specified argument is not a valid file stream.");
            return false;
          }

          if (output) {
            if (_output == null) {
              result = LuaMultiValue.CreateMultiValueFromObj(null, "No default output file set.");
              return false;
            }
            s = _output;
          } else {
            if (_input == null) {
              result = LuaMultiValue.CreateMultiValueFromObj(null, "No default input file set.");
              return false;
            }
            s = _input;
          }
        }
        result = null;
        return true;
      }

      // global functions
      sealed class dofile : LuaFunction {
        public dofile(ILuaEnvironment env) : base(env, "io.dofile") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          if (args.Count < 1) {
            throw new ArgumentException("Expecting one argument to function 'dofile'.");
          }

          string? file = args[0].GetValue() as string;

          if (string.IsNullOrEmpty(file)) {
            throw new ArgumentException("First argument to 'loadfile' must be a file path.");
          }

          if (!File.Exists(file)) {
            throw new FileNotFoundException("Unable to locate file at '" + file + "'.");
          }

          string chunk = File.ReadAllText(file);
          var parsed = Environment.Parser.Parse(chunk, Path.GetFileNameWithoutExtension(file));
          var r = Environment.CodeCompiler.Compile(Environment, parsed, "");
          return r.Invoke(LuaMultiValue.Empty);
        }
      }
      sealed class load : LuaFunction {
        public load(ILuaEnvironment env) : base(env, "io.load") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          if (args.Count < 1) {
            throw new ArgumentException("Expecting at least one argument to function 'load'.");
          }

          ILuaValue ld = args[0];
          string chunk;

          if (ld.ValueType == LuaValueType.Function) {
            chunk = "";
            while (true) {
              var ret = ld.Invoke(LuaMultiValue.Empty);
              if (ret[0].ValueType == LuaValueType.String) {
                if (string.IsNullOrEmpty(ret[0].GetValue() as string)) {
                  break;
                } else {
                  chunk += ret[0].GetValue() as string;
                }
              } else {
                break;
              }
            }
          } else if (ld.ValueType == LuaValueType.String) {
            chunk = ld.GetValue() as string ?? "";
          } else {
            throw new ArgumentException("First argument to 'load' must be a string or a method.");
          }

          try {
            var parsed = Environment.Parser.Parse(chunk, "");
            return new LuaMultiValue(Environment.CodeCompiler.Compile(Environment, parsed, ""));
          } catch (Exception e) {
            return LuaMultiValue.CreateMultiValueFromObj(null, e.Message);
          }
        }
      }
      sealed class loadfile : LuaFunction {
        public loadfile(ILuaEnvironment env) : base(env, "io.loadfile") { }

        protected override LuaMultiValue _invokeInternal(LuaMultiValue args) {
          if (args.Count < 1) {
            throw new ArgumentException("Expecting at least one argument to function 'loadfile'.");
          }

          string? file = args[0].GetValue() as string;
          string? mode = args[1].GetValue() as string;

          if (string.IsNullOrEmpty(file)) {
            throw new ArgumentException("First argument to 'loadfile' must be a file path.");
          }

          if (!File.Exists(file)) {
            throw new FileNotFoundException("Unable to locate file at '" + file + "'.");
          }

          if (string.IsNullOrEmpty(mode) && args.Count > 1) {
            throw new ArgumentException("Second argument to 'loadfile' must be a string mode.");
          }

          if (mode != "type") {
            throw new ArgumentException("The only mode supported by loadfile is 'type'.");
          }

          string chunk = File.ReadAllText(file);
          try {
            var parsed = Environment.Parser.Parse(chunk, Path.GetFileNameWithoutExtension(file));
            return new LuaMultiValue(Environment.CodeCompiler.Compile(Environment, parsed, ""));
          } catch (Exception e) {
            return LuaMultiValue.CreateMultiValueFromObj(null, e.Message);
          }
        }
      }
    }
  }
}
