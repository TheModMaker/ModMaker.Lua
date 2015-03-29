using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;
using ModMaker.Lua.Parser;
using System.Globalization;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        /// <summary>
        /// Contains the io framekwork functions.
        /// </summary>
        static class IO
        {
            static Stream _input = null, _output = null;

            public static void Initialize(ILuaEnvironment E)
            {
                ILuaTable io = new LuaTableNet();
                io.SetItemRaw("close", new close(E));
                io.SetItemRaw("flush", new flush(E));
                io.SetItemRaw("input", new input(E));
                io.SetItemRaw("lines", new lines(E));
                io.SetItemRaw("open", new open(E));
                io.SetItemRaw("output", new output(E));
                io.SetItemRaw("read", new read(E));
                io.SetItemRaw("tmpfile", new tmpfile(E));
                io.SetItemRaw("type", new type(E));
                io.SetItemRaw("write", new write(E));

                _input = E.Settings.Stdin;
                _output = E.Settings.Stdout;
                var _globals = E.GlobalsTable;
                _globals.SetItemRaw("io", io);
                _globals.SetItemRaw("dofile", new dofile(E));
                _globals.SetItemRaw("load", new load(E));
                _globals.SetItemRaw("loadfile", new loadfile(E));
                _globals.SetItemRaw("_STDIN", _CreateFile(E.Settings.Stdin, E));
                _globals.SetItemRaw("_STDOUT", _CreateFile(E.Settings.Stdout, E));
            }

            sealed class LinesHelper : LuaFrameworkMethod
            {
                StreamReader _stream;
                bool _close;
                int[] _ops; // -4 = *L, -3 = *l, -2 = *a, -1 = *n

                public LinesHelper(ILuaEnvironment E, bool close, Stream stream, int[] ops)
                    : base(E, "io.lines")
                {
                    this._stream = new StreamReader(stream);
                    this._close = close;
                    this._ops = ops;
                }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (_stream == null)
                        return new MultipleReturn(Enumerable.Range(1, _ops.Length).Select(i => (object)null));

                    var ret = _read(_ops, _stream);

                    if (_stream.EndOfStream)
                    {
                        if (_close)
                            _stream.Close();

                        _stream = null;
                    }

                    return ret;
                }
            }
            class Remove
            {
                List<Stream> st = new List<Stream>();
                static Remove instance = new Remove();
                static object _lock = new object();

                ~Remove()
                {
                    foreach (var item in st)
                        item.Close();

                    st.Clear();
                    st = null;
                }
                Remove()
                {

                }

                public static void Add(Stream s)
                {
                    lock (_lock)
                    {
                        if (!instance.st.Contains(s))
                            instance.st.Add(s);
                    }
                }
            }

            // io functions
            sealed class close : LuaFrameworkMethod
            {
                public close(ILuaEnvironment E) : base(E, "io.close") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    object file = args.Length > 0 ? args[0] : null;
                    Stream s;

                    if (file == null)
                    {
                        if (_output == null)
                            return new MultipleReturn(null, "No default output file set.");
                        s = _output;
                    }
                    else
                    {
                        if (file is ILuaTable)
                        {
                            s = (file as ILuaTable).GetItemRaw("Stream") as Stream;
                            if (s == null)
                                return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                        }
                        else if (file is Stream)
                        {
                            s = file as Stream;
                        }
                        else
                            return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                    }

                    try
                    {
                        s.Close();
                        return new MultipleReturn((object)_CreateFile(s, Environment));
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
            }
            sealed class flush : LuaFrameworkMethod
            {
                public flush(ILuaEnvironment E) : base(E, "io.flush") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    object file = args.Length > 0 ? args[0] : null;
                    Stream s;

                    if (file == null)
                    {
                        if (_output == null)
                            return new MultipleReturn(null, "No default output file set.");
                        s = _output;
                    }
                    else
                    {
                        if (file is ILuaTable)
                        {
                            s = (file as ILuaTable).GetItemRaw("Stream") as Stream;
                            if (s == null)
                                return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                        }
                        else if (file is Stream)
                        {
                            s = file as Stream;
                        }
                        else
                            return new MultipleReturn(null, "Specified argument is not a valid file stream.");
                    }
                    try
                    {
                        _output.Flush();
                        return new MultipleReturn((object)_CreateFile(_output, Environment));
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
            }
            sealed class input : LuaFrameworkMethod
            {
                public input(ILuaEnvironment E) : base(E, "io.input") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    object obj = args.Length > 0 ? args[0] : null;

                    if (obj != null)
                    {
                        if (obj is string)
                        {
                            Stream s = File.OpenRead(obj as string);
                            _input = s;
                        }
                        else if (obj is ILuaTable)
                        {
                            Stream s = (obj as ILuaTable).GetItemRaw("Stream") as Stream;
                            if (s == null)
                                throw new InvalidOperationException("First argument to function 'io.input' must be a file-stream or a string path.");

                            _input = s;
                        }
                        else if (obj is Stream)
                        {
                            _input = obj as Stream;
                        }
                        else
                            throw new InvalidOperationException("First argument to function 'io.input' must be a file-stream or a string path.");
                    }

                    return new MultipleReturn((object)_CreateFile(_input, Environment));
                }
            }
            sealed class lines : LuaFrameworkMethod
            {
                public lines(ILuaEnvironment E) : base(E, "io.lines") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    object obj = args.Length > 0 ? args[0] : null;
                    bool close;
                    Stream s;
                    int start = 0;
                    string oString = obj as string;

                    if (oString != null)
                    {
                        if (oString[0] != '*')
                        {
                            s = File.OpenRead(oString);
                            close = true;
                            start = 1;
                        }
                        else
                        {
                            s = _input;
                            close = false;
                            start = 0;
                        }
                    }
                    else if (obj is ILuaTable)
                    {
                        s = ((ILuaTable)obj).GetItemRaw("Stream") as Stream;
                        if (s == null)
                            throw new ArgumentException("First argument to io.lines must be a file-stream or a file path, make sure to use file:lines.");
                        close = false;
                        start = 1;
                    }
                    else if (obj is Stream)
                    {
                        s = obj as Stream;
                        close = false;
                        start = 1;
                    }
                    else
                    {
                        s = _input;
                        close = false;
                        start = 0;
                    }

                    int[] a = _parse(args.Cast<object>().Where((o1, i1) => i1 >= start), "io.lines");

                    return new MultipleReturn(new LinesHelper(Environment, close, s, a));
                }
            }
            sealed class open : LuaFrameworkMethod
            {
                public open(ILuaEnvironment E) : base(E, "io.open") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    string s = (args.Length > 0 ? args[0] : null) as string;
                    string mode = (args.Length > 1 ? args[1] : null) as string;
                    FileMode fileMode;
                    FileAccess access;
                    bool seek = false;
                    mode = mode == null ? null : mode.ToLowerInvariant();

                    if (string.IsNullOrWhiteSpace(s))
                        return new MultipleReturn(null, "First argument must be a string filename.");

                    switch (mode)
                    {
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
                            return new MultipleReturn(null, "Second argument must be a valid string mode.");
                    }

                    try
                    {
                        using (Stream stream = File.Open(s, fileMode, access))
                        {
                            if (seek && stream.CanSeek)
                                stream.Seek(0, SeekOrigin.End);

                            return new MultipleReturn((object)_CreateFile(stream, Environment));
                        }
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
            }
            sealed class output : LuaFrameworkMethod
            {
                public output(ILuaEnvironment E) : base(E, "io.output") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    object obj = args.Length > 0 ? args[0] : null;

                    if (obj != null)
                    {
                        if (obj is string)
                        {
                            Stream s = File.OpenRead(obj as string);
                            _output = s;
                        }
                        else if (obj is ILuaTable)
                        {
                            Stream s = (obj as ILuaTable).GetItemRaw("Stream") as Stream;
                            if (s == null)
                                throw new InvalidOperationException("First argument to function 'io.output' must be a file-stream or a string path.");

                            _output = s;
                        }
                        else if (obj is Stream)
                        {
                            _output = obj as Stream;
                        }
                        else
                            throw new InvalidOperationException("First argument to function 'io.output' must be a file-stream or a string path.");
                    }

                    return new MultipleReturn((object)_CreateFile(_output, Environment));
                }
            }
            sealed class read : LuaFrameworkMethod
            {
                public read(ILuaEnvironment E) : base(E, "io.read") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    object obj = args.Length > 0 ? args[0] : null;
                    Stream s;
                    int start = 0;

                    if (obj is ILuaTable)
                    {
                        s = ((ILuaTable)obj).GetItemRaw("Stream") as Stream;
                        if (s == null)
                            throw new ArgumentException("First argument to io.read must be a file-stream or a file path, make sure to use file:read.");
                        start = 1;
                    }
                    else if (obj is Stream)
                    {
                        s = obj as Stream;
                        start = 1;
                    }
                    else
                    {
                        s = _input;
                        start = 0;
                    }

                    int[] a = _parse(args.Cast<object>().Where((o1, i1) => i1 >= start), "io.read");

                    return _read(a, new StreamReader(s));
                }
            }
            sealed class seek : LuaFrameworkMethod
            {
                public seek(ILuaEnvironment E) : base(E, "io.seek") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    Stream s = (args.Length > 0 ? args[0] : null) as Stream;
                    SeekOrigin origin = SeekOrigin.Current;
                    long off = 0;

                    if (s == null)
                    {
                        ILuaTable table = args[0] as ILuaTable;
                        if (table != null)
                            s = table.GetItemRaw("Stream") as Stream;

                        if (s == null)
                            throw new ArgumentException("First real argument to function file:seek must be a file-stream, make sure to use file:seek.");
                    }

                    if (args.Length > 1)
                    {
                        string str = args[1] as string;
                        if (str == "set")
                            origin = SeekOrigin.Begin;
                        else if (str == "cur")
                            origin = SeekOrigin.Current;
                        else if (str == "end")
                            origin = SeekOrigin.End;
                        else
                            throw new ArgumentException("First argument to function file:seek must be a string.");

                        if (args.Length > 2)
                        {
                            object obj = args[2];
                            if (obj is double)
                                off = Convert.ToInt64((double)obj);
                            else
                                throw new ArgumentException("Second argument to function file:seek must be a number.");
                        }
                    }

                    if (!s.CanSeek)
                        return new MultipleReturn(null, "Specified stream cannot be seeked.");

                    try
                    {
                        return new MultipleReturn(Convert.ToDouble(s.Seek(off, origin)));
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
            }
            sealed class tmpfile : LuaFrameworkMethod
            {
                public tmpfile(ILuaEnvironment E) : base(E, "io.tmpfile") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    string str = Path.GetTempFileName();
                    Stream s = File.Open(str, FileMode.OpenOrCreate, FileAccess.ReadWrite);

                    Remove.Add(s);
                    return new MultipleReturn(_CreateFile(s, Environment));
                }
            }
            sealed class type : LuaFrameworkMethod
            {
                public type(ILuaEnvironment E) : base(E, "io.type") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    object obj = args.Length > 0 ? args[0] : null;

                    if (obj is Stream)
                    {
                        return new MultipleReturn((object)"file");
                    }
                    else if (obj is ILuaTable)
                    {
                        Stream s = ((ILuaTable)obj).GetItemRaw("Stream") as Stream;
                        return new MultipleReturn((object)(s == null ? null : "file"));
                    }
                    else
                        return new MultipleReturn(null);
                }
            }
            sealed class write : LuaFrameworkMethod
            {
                public write(ILuaEnvironment E) : base(E, "io.write") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    object obj = args.Length > 0 ? args[0] : null;
                    Stream s;
                    int start = 0;

                    if (obj is ILuaTable)
                    {
                        s = ((ILuaTable)obj).GetItemRaw("Stream") as Stream;
                        if (s == null)
                            return new MultipleReturn(null, "First argument must be a file-stream or a file path.");
                        start = 1;
                    }
                    else if (obj is Stream)
                    {
                        s = obj as Stream;
                        start = 1;
                    }
                    else
                    {
                        s = _output;
                        start = 0;
                    }

                    try
                    {
                        for (int i = start; i < args.Length; i++)
                        {
                            obj = args[i];
                            if (obj is double)
                            {
                                var bt = (Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(((double)obj).ToString(CultureInfo.InvariantCulture));
                                s.Write(bt, 0, bt.Length);
                            }
                            else if (obj is string)
                            {
                                var bt = (Environment.Settings.Encoding ?? Encoding.UTF8).GetBytes(obj as string);
                                s.Write(bt, 0, bt.Length);
                            }
                            else
                                throw new ArgumentException("Arguments to io.write must be a string or number.");
                        }

                        return new MultipleReturn(_CreateFile(s, Environment));
                    }
                    catch (ArgumentException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message, e);
                    }
                }
            }

            // helper functions
            static int[] _parse(IEnumerable args, string func)
            {
                List<int> v = new List<int>();

                foreach (var item in args)
                {
                    object obj = item;
                    if (obj is double)
                    {
                        double d = (double)obj;
                        if (d < 0)
                            throw new ArgumentOutOfRangeException("Arguments to " + func + " must be a positive integer.", (Exception)null);

                        v.Add(Convert.ToInt32(d));
                    }
                    else if (obj is string)
                    {
                        string st = obj as string;

                        if (st == "*index")
                            v.Add(-1);
                        else if (st == "*a")
                            v.Add(-2);
                        else if (st == "*l")
                            v.Add(-3);
                        else if (st == "*L")
                            v.Add(-4);
                        else
                            throw new ArgumentException("Only the following strings are valid as arguments to " + func + ": '*index', '*a', '*l', or '*L'.");
                    }
                    else
                        throw new ArgumentException("Arguments to function " + func + " must be a number or a string.");
                }

                return v.ToArray();
            }
            static MultipleReturn _read(int[] opts, StreamReader s)
            {
                List<object> ret = new List<object>();

                foreach (var item in opts)
                {
                    switch (item)
                    {
                        case -4:
                            ret.Add(s.EndOfStream ? null : s.ReadLine() + "\n");
                            break;
                        case -3:
                            ret.Add(s.EndOfStream ? null : s.ReadLine());
                            break;
                        case -2:
                            ret.Add(s.EndOfStream ? null : s.ReadToEnd());
                            break;
                        case -1:
                            if (s.EndOfStream)
                                ret.Add(null);
                            else
                            {
                                double? d = NetHelpers.ReadNumber(s);
                                if (d.HasValue)
                                    ret.Add(d.Value);
                                else
                                    ret.Add(null);
                            }
                            break;
                        default:
                            if (s.EndOfStream)
                                ret.Add(null);
                            else
                            {
                                char[] c = new char[item];
                                s.Read(c, 0, item);
                                ret.Add(new string(c));
                            }
                            break;
                    }
                }

                return new MultipleReturn(ret);
            }
            static ILuaTable _CreateFile(Stream backing, ILuaEnvironment E)
            {
                ILuaTable ret = new LuaTableNet();
                ret.SetItemRaw("Stream", backing);
                ret.SetItemRaw("close", new close(E));
                ret.SetItemRaw("flush", new flush(E));
                ret.SetItemRaw("lines", new lines(E));
                ret.SetItemRaw("read", new read(E));
                ret.SetItemRaw("seek", new seek(E));
                ret.SetItemRaw("write", new write(E));

                return ret;
            }

            // global functions 
            sealed class dofile : LuaFrameworkMethod
            {
                public dofile(ILuaEnvironment E) : base(E, "io.dofile") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'dofile'.");

                    string file = args[0] as string;

                    if (string.IsNullOrEmpty(file))
                        throw new ArgumentException("First argument to 'loadfile' must be a file path.");
                    if (!File.Exists(file))
                        throw new FileNotFoundException("Unable to locate file at '" + file + "'.");

                    string chunk = File.ReadAllText(file);
                    var r = Environment.CodeCompiler.Compile(Environment,
                        PlainParser.Parse(Environment.Parser, chunk, Path.GetFileNameWithoutExtension(file)), null);
                    return new MultipleReturn((IEnumerable)r.Invoke(null, false, null, new object[0]));
                }
            }
            sealed class load : LuaFrameworkMethod
            {
                public load(ILuaEnvironment E) : base(E, "io.load") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'load'.");

                    object ld = args[0];
                    string chunk;

                    if (ld is IMethod)
                    {
                        chunk = "";
                        while (true)
                        {
                            var ret = (ld as IMethod).Invoke(null, false, null, new object[0]);
                            if (ret[0] is string)
                            {
                                if (string.IsNullOrEmpty(ret[0] as string))
                                    break;
                                else
                                    chunk += ret[0] as string;
                            }
                            else
                                break;
                        }
                    }
                    else if (ld is string)
                    {
                        chunk = ld as string;
                    }
                    else
                        throw new ArgumentException("First argument to 'load' must be a string or a method.");

                    try
                    {
                        return new MultipleReturn(Environment.CodeCompiler.Compile(Environment, PlainParser.Parse(Environment.Parser, chunk, null), null));
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message);
                    }
                }
            }
            sealed class loadfile : LuaFrameworkMethod
            {
                public loadfile(ILuaEnvironment E) : base(E, "io.loadfile") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'loadfile'.");

                    string file = args[0] as string;
                    string mode = (args.Length > 1 ? args[1] : null) as string;

                    if (string.IsNullOrEmpty(file))
                        throw new ArgumentException("First argument to 'loadfile' must be a file path.");
                    if (!File.Exists(file))
                        throw new FileNotFoundException("Unable to locate file at '" + file + "'.");
                    if (string.IsNullOrEmpty(mode) && args.Length > 1)
                        throw new ArgumentException("Second argument to 'loadfile' must be a string mode.");
                    if (mode != "type")
                        throw new ArgumentException("The only mode supported by loadfile is 'type'.");

                    string chunk = File.ReadAllText(file);
                    try
                    {
                        return new MultipleReturn(Environment.CodeCompiler.Compile(Environment,
                            PlainParser.Parse(Environment.Parser, chunk, Path.GetFileNameWithoutExtension(file)), null));
                    }
                    catch (Exception e)
                    {
                        return new MultipleReturn(null, e.Message);
                    }
                }
            }
        }
    }
}
