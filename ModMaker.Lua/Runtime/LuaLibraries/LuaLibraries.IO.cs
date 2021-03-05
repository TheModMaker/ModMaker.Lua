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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  static partial class LuaStaticLibraries {
    static class IO {
      static readonly LuaString _stream = new LuaString("Stream");
      static Stream _input = null;
      static Stream _output = null;

      public static void Initialize(ILuaEnvironment env) {
        ILuaTable io = new LuaValues.LuaTable();
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
        var _globals = env.GlobalsTable;
        _globals.SetItemRaw(new LuaString("io"), io);
        _globals.SetItemRaw(new LuaString("dofile"), new dofile(env));
        _globals.SetItemRaw(new LuaString("load"), new load(env));
        _globals.SetItemRaw(new LuaString("loadfile"), new loadfile(env));
        _globals.SetItemRaw(new LuaString("_STDIN"), _createFile(env.Settings.Stdin, env));
        _globals.SetItemRaw(new LuaString("_STDOUT"), _createFile(env.Settings.Stdout, env));
      }

      sealed class LinesHelper : LuaFrameworkFunction {
        StreamReader _stream;
        readonly bool _close;
        readonly int[] _ops; // -4 = *L, -3 = *l, -2 = *a, -1 = *n

        public LinesHelper(ILuaEnvironment env, bool close, Stream stream, int[] ops)
            : base(env, "io.lines") {
          _stream = new StreamReader(stream);
          _close = close;
          _ops = ops;
        }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          if (_stream == null) {
            return LuaMultiValue.Empty;
          }

          var ret = _read(_ops, _stream, Environment);

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
      sealed class close : LuaFrameworkFunction {
        public close(ILuaEnvironment env) : base(env, "io.close") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          var t = _getStream(args[0], Environment, out Stream s);
          if (t != null) {
            return t;
          }

          try {
            s.Close();
            return Environment.Runtime.CreateMultiValue(_createFile(s, Environment));
          } catch (Exception e) {
            return Environment.Runtime.CreateMultiValueFromObj(null, e.Message, e);
          }
        }
      }
      sealed class flush : LuaFrameworkFunction {
        public flush(ILuaEnvironment env) : base(env, "io.flush") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          var t = _getStream(args[0], Environment, out _);
          if (t != null) {
            return t;
          }

          try {
            _output.Flush();
            return Environment.Runtime.CreateMultiValue(_createFile(_output, Environment));
          } catch (Exception e) {
            return Environment.Runtime.CreateMultiValueFromObj(null, e.Message, e);
          }
        }
      }
      sealed class input : LuaFrameworkFunction {
        public input(ILuaEnvironment env) : base(env, "io.input") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          ILuaValue obj = args[0];

          if (obj != null) {
            if (obj.ValueType == LuaValueType.String) {
              Stream s = File.OpenRead(obj.GetValue() as string);
              _input = s;
            } else if (obj.ValueType == LuaValueType.Table) {
              Stream s = ((ILuaTable)obj).GetItemRaw(_stream) as Stream;
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
          }

          return Environment.Runtime.CreateMultiValue(_createFile(_input, Environment));
        }
      }
      sealed class lines : LuaFrameworkFunction {
        public lines(ILuaEnvironment env) : base(env, "io.lines") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          ILuaValue obj = args[0];
          bool close;
          Stream s;
          int start;
          string oString = obj.GetValue() as string;

          if (oString != null) {
            if (oString[0] != '*') {
              s = File.OpenRead(oString);
              close = true;
              start = 1;
            } else {
              s = _input;
              close = false;
              start = 0;
            }
          } else if (obj.ValueType == LuaValueType.Table) {
            s = ((ILuaTable)obj).GetItemRaw(_stream) as Stream;
            if (s == null) {
              throw new ArgumentException("First argument to io.lines must be a file-stream or a " +
                                          "file path, make sure to use file:lines.");
            }

            close = false;
            start = 1;
          } else if (obj.GetValue() is Stream st) {
            s = st;
            close = false;
            start = 1;
          } else {
            s = _input;
            close = false;
            start = 0;
          }

          int[] a = _parse(args.Skip(start), "io.lines");
          return Environment.Runtime.CreateMultiValue(new LinesHelper(Environment, close, s, a));
        }
      }
      sealed class open : LuaFrameworkFunction {
        public open(ILuaEnvironment env) : base(env, "io.open") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          string s = args[0].GetValue() as string;
          string mode = args[1].GetValue() as string;
          FileMode fileMode;
          FileAccess access;
          bool seek = false;
          mode = mode?.ToLowerInvariant();

          if (string.IsNullOrWhiteSpace(s)) {
            return Environment.Runtime.CreateMultiValueFromObj(
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
              return Environment.Runtime.CreateMultiValueFromObj(
                  null, "Second argument must be a valid string mode.");
          }

          try {
            using (Stream stream = File.Open(s, fileMode, access)) {
              if (seek && stream.CanSeek) {
                stream.Seek(0, SeekOrigin.End);
              }

              return Environment.Runtime.CreateMultiValue(_createFile(stream, Environment));
            }
          } catch (Exception e) {
            return Environment.Runtime.CreateMultiValueFromObj(null, e.Message, e);
          }
        }
      }
      sealed class output : LuaFrameworkFunction {
        public output(ILuaEnvironment env) : base(env, "io.output") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          ILuaValue obj = args[0];
          if (obj != LuaNil.Nil) {
            if (obj.ValueType == LuaValueType.String) {
              Stream s = File.OpenRead(obj.GetValue() as string);
              _output = s;
            } else if (obj.ValueType == LuaValueType.Table) {
              Stream s = ((ILuaTable)obj).GetItemRaw(_stream) as Stream;
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
          }

          return Environment.Runtime.CreateMultiValue(_createFile(_output, Environment));
        }
      }
      sealed class read : LuaFrameworkFunction {
        public read(ILuaEnvironment env) : base(env, "io.read") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          ILuaValue obj = args[0];
          Stream s;
          int start;

          if (obj.ValueType == LuaValueType.Table) {
            s = ((ILuaTable)obj).GetItemRaw(_stream) as Stream;
            if (s == null) {
              throw new ArgumentException("First argument to io.read must be a file-stream or a " +
                                          "file path, make sure to use file:read.");
            }

            start = 1;
          } else if (obj.GetValue() is Stream st) {
            s = st;
            start = 1;
          } else {
            s = _input;
            start = 0;
          }

          int[] a = _parse(args.Skip(start), "io.read");
          return _read(a, new StreamReader(s), Environment);
        }
      }
      sealed class seek : LuaFrameworkFunction {
        public seek(ILuaEnvironment env) : base(env, "io.seek") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          Stream s = args[0].GetValue() as Stream;
          SeekOrigin origin = SeekOrigin.Current;
          long off = 0;

          if (s == null) {
            ILuaTable table = args[0] as ILuaTable;
            if (table != null) {
              s = table.GetItemRaw(_stream) as Stream;
            }

            if (s == null) {
              throw new ArgumentException("First real argument to function file:seek must be a " +
                                          "file-stream, make sure to use file:seek.");
            }
          }

          if (args.Count > 1) {
            string str = args[1].GetValue() as string;
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
              object obj = args[2].GetValue();
              if (obj is double) {
                off = Convert.ToInt64((double)obj);
              } else {
                throw new ArgumentException(
                    "Second argument to function file:seek must be a number.");
              }
            }
          }

          if (!s.CanSeek) {
            return Environment.Runtime.CreateMultiValueFromObj(
                null, "Specified stream cannot be seeked.");
          }

          try {
            return Environment.Runtime.CreateMultiValueFromObj(
                Convert.ToDouble(s.Seek(off, origin)));
          } catch (Exception e) {
            return Environment.Runtime.CreateMultiValueFromObj(null, e.Message, e);
          }
        }
      }
      sealed class tmpfile : LuaFrameworkFunction {
        public tmpfile(ILuaEnvironment env) : base(env, "io.tmpfile") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          string str = Path.GetTempFileName();
          Stream s = File.Open(str, FileMode.OpenOrCreate, FileAccess.ReadWrite);
          return Environment.Runtime.CreateMultiValue(_createFile(s, Environment));
        }
      }
      sealed class type : LuaFrameworkFunction {
        public type(ILuaEnvironment env) : base(env, "io.type") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          ILuaValue obj = args[0];

          if (obj.GetValue() is Stream) {
            return Environment.Runtime.CreateMultiValueFromObj("file");
          } else if (obj.ValueType == LuaValueType.Table) {
            Stream s = ((ILuaTable)obj).GetItemRaw(_stream) as Stream;
            return Environment.Runtime.CreateMultiValueFromObj(s == null ? null : "file");
          } else {
            return LuaMultiValue.Empty;
          }
        }
      }
      sealed class write : LuaFrameworkFunction {
        public write(ILuaEnvironment env) : base(env, "io.write") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          ILuaValue obj = args[0];
          Stream s;
          int start;

          if (obj.ValueType == LuaValueType.Table) {
            s = ((ILuaTable)obj).GetItemRaw(_stream) as Stream;
            if (s == null) {
              return Environment.Runtime.CreateMultiValueFromObj(
                  null, "First argument must be a file-stream or a file path.");
            }

            start = 1;
          } else if (obj is Stream st) {
            s = st;
            start = 1;
          } else {
            s = _output;
            start = 0;
          }

          try {
            for (int i = start; i < args.Count; i++) {
              var temp = args[i].GetValue();
              if (temp is double) {
                var bt = (Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(
                    ((double)temp).ToString(CultureInfo.InvariantCulture));
                s.Write(bt, 0, bt.Length);
              } else if (temp is string) {
                var bt = (Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(temp as string);
                s.Write(bt, 0, bt.Length);
              } else {
                throw new ArgumentException("Arguments to io.write must be a string or number.");
              }
            }

            return Environment.Runtime.CreateMultiValue(_createFile(s, Environment));
          } catch (ArgumentException) {
            throw;
          } catch (Exception e) {
            return Environment.Runtime.CreateMultiValueFromObj(null, e.Message, e);
          }
        }
      }

      // helper functions
      static int[] _parse(IEnumerable<ILuaValue> args, string func) {
        List<int> v = new List<int>();

        foreach (var item in args) {
          object obj = item.GetValue();
          if (obj is double d) {
            if (d < 0) {
              throw new ArgumentOutOfRangeException(
                  $"Arguments to {func} must be a positive integer.", (Exception)null);
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
      static ILuaMultiValue _read(int[] opts, StreamReader s, ILuaEnvironment env) {
        List<ILuaValue> ret = new List<ILuaValue>();

        foreach (var item in opts) {
          switch (item) {
            case -4:
              ret.Add(env.Runtime.CreateValue(s.EndOfStream ? null : s.ReadLine() + "\n"));
              break;
            case -3:
              ret.Add(env.Runtime.CreateValue(s.EndOfStream ? null : s.ReadLine()));
              break;
            case -2:
              ret.Add(env.Runtime.CreateValue(s.EndOfStream ? null : s.ReadToEnd()));
              break;
            case -1:
              if (s.EndOfStream) {
                ret.Add(null);
              } else {
                double? d = NetHelpers.ReadNumber(s);
                if (d.HasValue) {
                  ret.Add(env.Runtime.CreateValue(d.Value));
                } else {
                  ret.Add(LuaNil.Nil);
                }
              }
              break;
            default:
              if (s.EndOfStream) {
                ret.Add(null);
              } else {
                char[] c = new char[item];
                s.Read(c, 0, item);
                ret.Add(env.Runtime.CreateValue(new string(c)));
              }
              break;
          }
        }

        return env.Runtime.CreateMultiValue(ret.ToArray());
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
      static ILuaMultiValue _getStream(ILuaValue file, ILuaEnvironment env, out Stream s) {
        s = null;
        if (file == LuaNil.Nil) {
          if (_output == null) {
            return env.Runtime.CreateMultiValueFromObj(null, "No default output file set.");
          }

          s = _output;
        } else {
          if (file.ValueType == LuaValueType.Table) {
            s = ((ILuaTable)file).GetItemRaw(_stream).GetValue() as Stream;
            if (s == null) {
              return env.Runtime.CreateMultiValueFromObj(
                  null, "Specified argument is not a valid file stream.");
            }
          } else if (file.ValueType == LuaValueType.UserData) {
            s = file.GetValue() as Stream;
          } else {
            return env.Runtime.CreateMultiValueFromObj(
                null, "Specified argument is not a valid file stream.");
          }
        }
        return null;
      }

      // global functions
      sealed class dofile : LuaFrameworkFunction {
        public dofile(ILuaEnvironment env) : base(env, "io.dofile") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          if (args.Count < 1) {
            throw new ArgumentException("Expecting one argument to function 'dofile'.");
          }

          string file = args[0].GetValue() as string;

          if (string.IsNullOrEmpty(file)) {
            throw new ArgumentException("First argument to 'loadfile' must be a file path.");
          }

          if (!File.Exists(file)) {
            throw new FileNotFoundException("Unable to locate file at '" + file + "'.");
          }

          string chunk = File.ReadAllText(file);
          var parsed = PlainParser.Parse(Environment.Parser, chunk,
                                         Path.GetFileNameWithoutExtension(file));
          var r = Environment.CodeCompiler.Compile(Environment, parsed, null);
          return r.Invoke(LuaNil.Nil, false, -1, LuaMultiValue.Empty);
        }
      }
      sealed class load : LuaFrameworkFunction {
        public load(ILuaEnvironment env) : base(env, "io.load") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          if (args.Count < 1) {
            throw new ArgumentException("Expecting at least one argument to function 'load'.");
          }

          ILuaValue ld = args[0];
          string chunk;

          if (ld.ValueType == LuaValueType.Function) {
            chunk = "";
            while (true) {
              var ret = ld.Invoke(LuaNil.Nil, false, -1, Environment.Runtime.CreateMultiValue());
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
            chunk = ld.GetValue() as string;
          } else {
            throw new ArgumentException("First argument to 'load' must be a string or a method.");
          }

          try {
            var parsed = PlainParser.Parse(Environment.Parser, chunk, null);
            return Environment.Runtime.CreateMultiValue(
                Environment.CodeCompiler.Compile(Environment, parsed, null));
          } catch (Exception e) {
            return Environment.Runtime.CreateMultiValueFromObj(null, e.Message);
          }
        }
      }
      sealed class loadfile : LuaFrameworkFunction {
        public loadfile(ILuaEnvironment env) : base(env, "io.loadfile") { }

        protected override ILuaMultiValue InvokeInternal(ILuaMultiValue args) {
          if (args.Count < 1) {
            throw new ArgumentException("Expecting at least one argument to function 'loadfile'.");
          }

          string file = args[0].GetValue() as string;
          string mode = args[1].GetValue() as string;

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
            var parsed = PlainParser.Parse(Environment.Parser, chunk,
                                           Path.GetFileNameWithoutExtension(file));
            return Environment.Runtime.CreateMultiValue(
              Environment.CodeCompiler.Compile(Environment, parsed, null));
          } catch (Exception e) {
            return Environment.Runtime.CreateMultiValueFromObj(null, e.Message);
          }
        }
      }
    }
  }
}
