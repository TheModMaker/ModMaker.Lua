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
        Register(env, io, (Func<ILuaValue, LuaMultiValue>)close);
        Register(env, io, (Func<ILuaValue, LuaMultiValue>)flush);
        Register(env, io, (Func<ILuaValue, ILuaTable?>)input);
        Register(env, io, (Func<LuaMultiValue, object>)lines);
        Register(env, io, (Func<string, string, object?>)open);
        Register(env, io, (Func<ILuaValue, ILuaTable?>)output);
        Register(env, io, (Func<LuaMultiValue, LuaMultiValue>)read);
        Register(env, io, (Func<ILuaTable>)tmpfile);
        Register(env, io, (Func<ILuaValue, string?>)type);
        Register(env, io, (Func<LuaMultiValue, LuaMultiValue>)write);

        _input = env.Settings.Stdin;
        _output = env.Settings.Stdout;
        var globals = env.GlobalsTable;
        globals.SetItemRaw(new LuaString("io"), io);
        Register(env, globals, (Func<string, LuaMultiValue>)dofile);
        Register(env, globals, (Func<ILuaValue, string, string, ILuaTable?, LuaMultiValue>)load);
        Register(env, globals, (Func<string, string, ILuaTable?, LuaMultiValue>)loadfile);
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
      static LuaMultiValue close(ILuaValue file) {
        if (!_getStream(file, kOutput, kRequireStream, out Stream? s, out LuaMultiValue? ret))
          return ret;

        try {
          s.Close();
          return new LuaMultiValue(_createFile(s, LuaEnvironment.CurrentEnvironment));
        } catch (Exception e) {
          return LuaMultiValue.CreateMultiValueFromObj(null, e.Message, e);
        }
      }
      static LuaMultiValue flush(ILuaValue file) {
        if (!_getStream(file, kOutput, kRequireStream, out Stream? s, out LuaMultiValue? ret))
          return ret;

        try {
          s.Flush();
          return new LuaMultiValue(_createFile(s, LuaEnvironment.CurrentEnvironment));
        } catch (Exception e) {
          return LuaMultiValue.CreateMultiValueFromObj(null, e.Message, e);
        }
      }
      static ILuaTable? input(ILuaValue obj) {
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
          return null;
        }

        return _createFile(_input, LuaEnvironment.CurrentEnvironment);
      }
      static object lines(LuaMultiValue args) {
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
        return new LinesHelper(LuaEnvironment.CurrentEnvironment, close, s, a);
      }
      [MultipleReturn]
      static object?[] open(string s, string mode = "r") {
        FileMode fileMode;
        FileAccess access;
        bool seek = false;
        mode = mode.ToLowerInvariant();

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
            return new object?[] { null, "Second argument must be a valid string mode." };
        }

        try {
          using (Stream stream = File.Open(s, fileMode, access)) {
            if (seek && stream.CanSeek) {
              stream.Seek(0, SeekOrigin.End);
            }

            return new[] { _createFile(stream, LuaEnvironment.CurrentEnvironment) };
          }
        } catch (Exception e) {
          return new object?[] { null, e.Message, e };
        }
      }
      static ILuaTable? output(ILuaValue obj) {
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
          return null;
        }

        return _createFile(_output, LuaEnvironment.CurrentEnvironment);
      }
      static LuaMultiValue read(LuaMultiValue args) {
        if (!_getStream(args[0], kInput, kAnyValue, out Stream? s, out LuaMultiValue? ret))
          return ret;
        int start = s == _input ? 0 : 1;

        int[] a = _parse(args.Skip(start), "io.read");
        return _read(a, new StreamReader(s));
      }
      static LuaMultiValue seek(ILuaValue file, string originStr = "cur", long off = 0) {
        if (!_getStream(file, kInput, kRequireStream, out Stream? s, out LuaMultiValue? ret))
          return ret;
        SeekOrigin origin;
        if (originStr == "set") {
          origin = SeekOrigin.Begin;
        } else if (originStr == "cur") {
          origin = SeekOrigin.Current;
        } else if (originStr == "end") {
          origin = SeekOrigin.End;
        } else {
          throw new ArgumentException("First argument to function file:seek must be a string.");
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
      static ILuaTable tmpfile() {
        string str = Path.GetTempFileName();
        Stream s = File.Open(str, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        return _createFile(s, LuaEnvironment.CurrentEnvironment);
      }
      static string? type(ILuaValue obj) {
        if (obj.GetValue() is Stream) {
          return "file";
        } else if (obj.ValueType == LuaValueType.Table) {
          Stream? s = ((ILuaTable)obj).GetItemRaw(_stream) as Stream;
          return s == null ? null : "file";
        } else {
          return null;
        }
      }
      static LuaMultiValue write(LuaMultiValue args) {
        if (!_getStream(args[0], kOutput, kAnyValue, out Stream? s, out LuaMultiValue? ret))
          return ret;
        int start = s == _output ? 0 : 1;

        var E = LuaEnvironment.CurrentEnvironment;
        try {
          for (int i = start; i < args.Count; i++) {
            var temp = args[i].GetValue();
            if (temp is double dbl) {
              var bt = (E.Settings.Encoding ?? Encoding.UTF8).GetBytes(
                  dbl.ToString(CultureInfo.InvariantCulture));
              s.Write(bt, 0, bt.Length);
            } else if (temp is string str) {
              var bt = (E.Settings.Encoding ?? Encoding.UTF8).GetBytes(str);
              s.Write(bt, 0, bt.Length);
            } else {
              throw new ArgumentException("Arguments to io.write must be a string or number.");
            }
          }

          return new LuaMultiValue(_createFile(s, E));
        } catch (ArgumentException) {
          throw;
        } catch (Exception e) {
          return LuaMultiValue.CreateMultiValueFromObj(null, e.Message, e);
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
        Register(env, ret, (Func<ILuaValue, LuaMultiValue>)close);
        Register(env, ret, (Func<ILuaValue, LuaMultiValue>)flush);
        Register(env, ret, (Func<LuaMultiValue, object>)lines);
        Register(env, ret, (Func<LuaMultiValue, LuaMultiValue>)read);
        Register(env, ret, (Func<ILuaValue, string, long, LuaMultiValue>)seek);
        Register(env, ret, (Func<LuaMultiValue, LuaMultiValue>)write);

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
      static LuaMultiValue dofile(string file) {
        if (!File.Exists(file)) {
          throw new FileNotFoundException("Unable to locate file at '" + file + "'.");
        }

        string chunk = File.ReadAllText(file);
        var E = LuaEnvironment.CurrentEnvironment;
        var parsed = E.Parser.Parse(chunk, Path.GetFileNameWithoutExtension(file));
        var r = E.CodeCompiler.Compile(E, parsed, "");
        return r.Invoke(LuaMultiValue.Empty);
      }
      static LuaMultiValue load(ILuaValue ld, string source = "", string mode = "bt",
                                ILuaTable? env = null) {
        string chunk;
        if (ld.ValueType == LuaValueType.Function) {
          chunk = "";
          while (true) {
            var ret = ld.Invoke(LuaMultiValue.Empty);
            if (ret[0].ValueType == LuaValueType.String) {
              var str = ret[0].GetValue() as string;
              if (string.IsNullOrEmpty(str)) {
                break;
              } else {
                chunk += str;
              }
            } else {
              break;
            }
          }
        } else if (ld.ValueType == LuaValueType.String) {
          chunk = (string)ld.GetValue()!;
        } else {
          throw new ArgumentException("First argument to 'load' must be a string or a method.");
        }

        if (mode != "t" && mode != "bt")
          throw new NotSupportedException("The only mode supported by load is 't' or 'bt'.");
        if (env != null)
          throw new NotSupportedException("Custom environment not supported");

        try {
          var E = LuaEnvironment.CurrentEnvironment;
          var parsed = E.Parser.Parse(chunk, source);
          return new LuaMultiValue(E.CodeCompiler.Compile(E, parsed, source));
        } catch (Exception e) {
          return LuaMultiValue.CreateMultiValueFromObj(null, e.Message);
        }
      }
      static LuaMultiValue loadfile(string file, string mode = "bt", ILuaTable? env = null) {
        return load(new LuaString(File.ReadAllText(file)), Path.GetFileName(file), mode, env);
      }
    }
  }
}
