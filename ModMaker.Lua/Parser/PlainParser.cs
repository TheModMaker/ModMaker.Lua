using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Security;
using System.Threading;
using ModMaker.Lua.Parser.Items;
using System.Globalization;
using ModMaker.Lua.Runtime;
using System.Reflection.Emit;

namespace ModMaker.Lua.Parser
{
    class PlainParser
    {
        static char[] __nameStartChars = "_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToArray(), 
            __nameChars = "_0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToArray();
        static string[] __reserved = new[] { "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "goto", "if", 
                                 "in", "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while",
                                 "class", "property" };
        static Dictionary<string, LuaChunk> _loaded = new Dictionary<string, LuaChunk>();

        string _name;
        bool _glt = false;
        LuaChunk _output;
        TextReader _reader;
        long _line = 0, _colOff = 0, _pos = 0;

        PlainParser(TextReader reader, string name)
        {
            this._reader = reader;
            this._name = name;
            this._output = null;
        }

        public static LuaChunk LoadChunk(LuaEnvironment E, TextReader reader, byte[] hash, string name = null)
        {
            if (hash == null)
            {
                if (E.Settings.AllowNonSeekStreams)
                    hash = null;
                else
                    throw new InvalidOperationException("Unable to load given chunk because the stream cannot be seeked, see LuaSettings.AllowNonSeekStreams.");
            }
            else
            {
                string shash = hash.ToStringBase16().ToUpper(CultureInfo.InvariantCulture);
                lock (_loaded)
                    if (_loaded.ContainsKey(shash))
                        return _loaded[shash].Clone(E);
            }

            return new PlainParser(reader, name).LoadChunk(E, hash.ToStringBase16().ToUpper(CultureInfo.InvariantCulture));
        }

        LuaChunk LoadChunk(LuaEnvironment E, string hash)
        {
            _line = 1;
            _colOff = 0;
            _pos = 0;
            IParseItem chunk = ReadChunk().Item1;
            ChunkBuilderNew cb = E.DefineChunkNew(_name);
            cb.DefineGlobalFunc();

            /* resolve labels */
            if (_glt)
            {
                ILGenerator gen = cb.CurrentGenerator;
                LabelTree lt = new LabelTree();
                chunk.ResolveLabels(cb, lt);
                lt.Resolve();
                cb.CurrentGenerator = gen;
            }

            /* generate the chunk */
            chunk.GenerateIL(cb);
            _output = cb.CreateChunk(E);
            if (hash != null)
                lock (_loaded)
                    _loaded[hash] = _output;
            return _output;
        }

        int Read()
        {
            int ret = _reader.Read();
            if (ret != -1)
                _pos++;
            return ret;
        }
        bool CanRead
        {
            get
            {
                return _reader.Peek() != -1;
            }
        }
        Tuple<IParseItem, string> ReadChunk(IParseItem parrent = null)
        {
            IParseItem ret = new BlockItem();

            string name = null;
            bool reuse = false;
            while (CanRead)
            {
                ReadWhitespace();
                if (!reuse)
                {
                    name = ReadName(true);
                    if (string.IsNullOrWhiteSpace(name))
                        name = ((char)Read()).ToString();
                }
                reuse = false;
                if (name == null)
                    break;
                if (name[0] == '-' && _reader.Peek() == '-')
                {
                    Read();
                    if (_reader.Peek() != -1)
                        ReadComment();
                }
                else if (name[0] == ';' || name[0] == ' ' || name[0] == '\t' || name[0] == '\n' || name[0] == '\r')
                {
                    if (name[0] == '\n')
                    {
                        _line++; _colOff = _pos + 1;
                    }
                    continue;
                }
                else if (name[0] == ':' && _reader.Peek() == ':')
                {
                    Read();
                    if (CanRead)
                        throw new SyntaxException("Invalid Label definition.", _line, _pos - _colOff, _name);
                    string label = ReadName(false);
                    if (!CanRead || Read() != ':' || Read() != ':')
                        throw new SyntaxException("Invalid Label definition.", _line, _pos - _colOff, _name);

                    ret.AddItem(new LabelItem(label));
                }
                else if (__nameStartChars.Contains(name[0]))
                {
                    Tuple<IParseItem, string> rr;
                    switch (name)
                    {
                        #region case "break"
                        case "break":
                            ret.AddItem(new GotoItem("<break>", _line, _pos - _colOff - 5));
                            _glt = true;
                            break;
                        #endregion
                        #region case "goto"
                        case "goto":
                            name = ReadName(false);
                            if (__reserved.Contains(name))
                                throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - _colOff - name.Length, _name);
                            ret.AddItem(new GotoItem(name, _line, _pos - _colOff - name.Length));
                            _glt = true;
                            break;
                        #endregion
                        #region case "do"
                        case "do":
                            rr = ReadChunk(ret);
                            ret.AddItem(rr.Item1);
                            if (rr.Item2 != "end")
                                throw new SyntaxException("Expecting 'end' for end of local block.", _line, _pos - _colOff - 3, _name);
                            break;
                        #endregion
                        #region case "end"
                        case "end":
                            if (parrent == null)
                                throw new SyntaxException("Invalid token 'end' in global chunk.", _line, _pos - _colOff - 3, _name);
                            return new Tuple<IParseItem,string>(ret, "end");
                        #endregion
                        #region case "else/elseif"
                        case "elseif":
                        case "else":
                            if (parrent == null)
                                throw new SyntaxException("Invalid token '" + name + "' in global chunk.", _line, _pos - _colOff - name.Length, _name);
                            else if (!(parrent is IfItem))
                                throw new SyntaxException("'" + name + "' is only valid in an if block.", _line, _pos - _colOff - name.Length, _name);

                            return new Tuple<IParseItem,string>(ret, name);
                        #endregion
                        #region case "until"
                        case "until":
                            if (parrent == null)
                                throw new SyntaxException("Invalid token 'until' in global chunk.", _line, _pos - _colOff - 5, _name);
                            else if (!(parrent is RepeatItem))
                                throw new SyntaxException("'until' is only valid in an repeat block.", _line, _pos - _colOff - 5, _name);
                        
                            return new Tuple<IParseItem,string>(ret, "until");
                        #endregion
                        #region case "while"
                        case "while":
                            {
                                WhileItem w = new WhileItem();
                                rr = ReadExp();
                                w.Exp = rr.Item1;
                                name = rr.Item2 ?? ReadName(false);
                                if (name != "do")
                                    throw new SyntaxException("Invalid token '" + name + "' in while definition.", _line, _pos - _colOff - name.Length, _name);
                                rr = ReadChunk(w);
                                w.Block = rr.Item1;
                                if (rr.Item2 != "end")
                                    throw new SyntaxException("Invalid token '" + rr.Item2 + "' in while definition.", _line, _pos - _colOff - name.Length, _name);
                                ret.AddItem(w);
                            }
                            break;
                        #endregion
                        #region case "repeat"
                        case "repeat":
                            {
                                RepeatItem r = new RepeatItem();
                                rr = ReadChunk(r);
                                r.Block = rr.Item1;
                                if (rr.Item2 != "until")
                                    throw new SyntaxException("Invalid token '" + rr.Item2 + "' in repeat definition.", _line, _pos - _colOff - name.Length, _name);
                                rr = ReadExp();
                                r.Exp = rr.Item1;
                                ret.AddItem(r);
                                name = rr.Item2;
                                reuse = name != null;
                            }
                            break;
                        #endregion
                        #region case "if"
                        case "if":
                            {
                                IfItem i = new IfItem();
                                rr = ReadExp();
                                i.Exp = rr.Item1;
                                name = rr.Item2 ?? ReadName(false);
                                if (name != "then")
                                    throw new SyntaxException("Invalid token '" + name + "' in if definition.", _line, _pos - _colOff - name.Length, _name);
                                rr = ReadChunk(i);
                                i.Block = rr.Item1;
                                while (rr.Item2 == "elseif")
                                {
                                    rr = ReadExp();
                                    IParseItem e = rr.Item1;
                                    name = rr.Item2 ?? ReadName(false);
                                    if (name != "then")
                                        throw new SyntaxException("Invalid token '" + name + "' in elseif definition.", _line, _pos - _colOff - name.Length, _name);
                                    rr = ReadChunk(i);
                                    i.AddItem(e, rr.Item1);
                                }
                                if (rr.Item2 != "else" && rr.Item2 != "end")
                                    throw new SyntaxException("Invalid token '" + rr.Item2 + "' in if definition.", _line, _pos - _colOff - name.Length, _name);
                                if (rr.Item2 == "else")
                                {
                                    rr = ReadChunk(i);
                                    i.ElseBlock = rr.Item1;
                                }
                                if (rr.Item2 != "end")
                                    throw new SyntaxException("Invalid token '" + rr.Item2 + "' in if definition.", _line, _pos - _colOff - name.Length, _name);
                                ret.AddItem(i);
                            }
                            break;
                        #endregion
                        #region case "for"
                        case "for":
                            {
                                name = ReadName(false);
                                if (__reserved.Contains(name))
                                    throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - _colOff - name.Length, _name);
                                ReadWhitespace();
                                if (_reader.Peek() == '=')
                                {
                                    Read();
                                    ForNumItem i = new ForNumItem(name);
                                    rr = ReadExp();
                                    i.Start = rr.Item1;
                                    ReadWhitespace();
                                    if (rr.Item2 != null || Read() != ',')
                                        throw new SyntaxException("Invalid token '" + rr.Item2 ?? _reader.Peek() + "' in for definition.", _line, _pos - _colOff - 1, _name);
                                    rr = ReadExp();
                                    i.Limit = rr.Item1;
                                    ReadWhitespace();
                                    if (_reader.Peek() == ',')
                                    {
                                        Read();
                                        rr = ReadExp();
                                        i.Step = rr.Item1;
                                    }
                                    name = rr.Item2 ?? ReadName(false);
                                    if (name != "do")
                                        throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _pos - _colOff - name.Length, _name);
                                    rr = ReadChunk(i);
                                    i.Block = rr.Item1;
                                    if (rr.Item2 != "end")
                                        throw new SyntaxException("Invalid token '" + rr.Item2 + "' in for definition.", _line, _pos - _colOff - name.Length, _name);
                                    ret.AddItem(i);
                                }
                                else
                                {
                                    List<string> names = new List<string>();
                                    names.Add(name);
                                    while (_reader.Peek() == ',')
                                    {
                                        Read();
                                        name = ReadName(false);
                                        if (__reserved.Contains(name))
                                            throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - _colOff - name.Length, _name);
                                        names.Add(name);
                                        ReadWhitespace();
                                    }
                                    name = ReadName(false);
                                    if (name != "in")
                                        throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _pos - _colOff - name.Length, _name);
                                    
                                    ForGenItem f = new ForGenItem(names);
                                    rr = ReadExp();
                                    f.AddItem(rr.Item1);
                                    ReadWhitespace();
                                    while (rr.Item2 == null && _reader.Peek() == ',')
                                    {
                                        rr = ReadExp();
                                        f.AddItem(rr.Item1);
                                        ReadWhitespace();
                                    }

                                    name = rr.Item2 ?? ReadName(false);
                                    if (name != "do")
                                        throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _pos - _colOff - name.Length, _name);
                                    rr = ReadChunk(f);
                                    f.Block = rr.Item1;
                                    if (rr.Item2 != "end")
                                        throw new SyntaxException("Invalid token '" + rr.Item2 + "' in for definition.", _line, _pos - _colOff - name.Length, _name);
                                    ret.AddItem(f);
                                }
                            }
                            break;
                        #endregion
                        #region case "function"
                        case "function":
                            {
                                long l = _line, cc = _pos - _colOff - 8;
                                name = ReadName(false);
                                if (__reserved.Contains(name))
                                    throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - _colOff - name.Length, _name);
                                IParseItem n = new NameItem(name);
                                string inst = null;
                                while (_reader.Peek() == '.')
                                {
                                    Read();
                                    name = ReadName(false);
                                    n = new IndexerItem(n, new LiteralItem(name));
                                }
                                if (_reader.Peek() == ':')
                                {
                                    Read();
                                    inst = ReadName(true);
                                }
                                ReadWhitespace();
                                
                                ret.AddItem(ReadFunc(n, inst, l, cc));
                            }
                            break;
                        #endregion
                        #region case "local"
                        case "local":
                            {
                                long l = _line, cc = _pos - _colOff - 5;
                                name = ReadName(false);
                                if (name == "function")
                                {
                                    name = ReadName(false);
                                    if (__reserved.Contains(name))
                                        throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - _colOff - name.Length, _name);

                                    FuncDef f = new FuncDef(new NameItem(name), l, cc, true);
                                    bool b = false;
                                    ReadWhitespace();
                                    if (Read() != '(')
                                        throw new SyntaxException("Invalid token in function definition.", _line, _pos - _colOff - 1, _name);
                                    name = ReadName(false);
                                    if (name.Length == 0)
                                    {
                                        if (_reader.Peek() != ')')
                                        {
                                            char[] temp = new char[3];
                                            _reader.Read(temp, 0, 3);
                                            _pos += 3;
                                            if (temp[0] != '.' || temp[1] != '.' || temp[2] != '.')
                                                throw new SyntaxException("Invalid token in function definition.", _line, _pos - _colOff - 3, _name);
                                            f.AddParam("...");
                                            b = true;
                                        }
                                    }
                                    else
                                        f.AddParam(name);

                                    ReadWhitespace();
                                    while (!b && _reader.Peek() == ',')
                                    {
                                        Read();
                                        name = ReadName(false);
                                        if (name.Length == 0)
                                        {
                                            char[] temp = new char[3];
                                            _reader.Read(temp, 0, 3);
                                            _pos += 3;
                                            if (temp[0] != '.' || temp[1] != '.' || temp[2] != '.')
                                                throw new SyntaxException("Invalid token in function definition.", _line, _pos - _colOff - 3, _name);
                                            f.AddParam("...");
                                            break;
                                        }
                                        else
                                            f.AddParam(name);
                                        ReadWhitespace();
                                    }

                                    if (Read() != ')')
                                        throw new SyntaxException("Invalid token in function definition.", _line, _pos - _colOff - 1, _name);
                                    rr = ReadChunk(f);
                                    f.Block = rr.Item1;
                                    f.Block.AddItem(new ReturnItem());
                                    if (rr.Item2 != "end")
                                        throw new SyntaxException("Invalid token '" + rr.Item2 + "' in function definition.", _line, _pos - _colOff - name.Length, _name);
                                    ret.AddItem(f);
                                }
                                else
                                {
                                    if (__reserved.Contains(name))
                                        throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - _colOff - name.Length, _name);

                                    VarInitItem i = new VarInitItem(true);
                                    i.AddName(new NameItem(name));
                                    ReadWhitespace();
                                    while (_reader.Peek() == ',')
                                    {
                                        Read();
                                        name = ReadName(false);

                                        if (__reserved.Contains(name))
                                            throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - _colOff - name.Length, _name);
                                        i.AddName(new NameItem(name));
                                        ReadWhitespace();
                                    }

                                    if (_reader.Peek() == '=')
                                    {
                                        Read();
                                        rr = ReadExp();
                                        i.AddItem(rr.Item1);
                                        ReadWhitespace();
                                        while (rr.Item2 == null && _reader.Peek() == ',')
                                        {
                                            Read();
                                            rr = ReadExp();
                                            i.AddItem(rr.Item1);
                                            ReadWhitespace();
                                        }
                                        name = rr.Item2;
                                        reuse = name != null;
                                    }
                                    ret.AddItem(i);
                                }
                            }
                            break;
                        #endregion
                        #region case "class"
                        case "class":
                            {
                                string sname = null;
                                List<string> imp = new List<string>();
                                long l = _line, cc = _pos - _colOff - 5;
                                ReadWhitespace();
                                if (_reader.Peek() == '\'' || _reader.Peek() == '"')
                                {
                                    sname = ReadString();
                                    ReadWhitespace();
                                    if (_reader.Peek() == '(')
                                    {
                                        ReadWhitespace();
                                        while (_reader.Peek() != ')')
                                        {
                                            imp.Add(ReadName(false));
                                            ReadWhitespace();
                                            Read();
                                            ReadWhitespace();
                                        }
                                    }
                                }
                                else
                                {
                                    sname = ReadName(false);
                                    ReadWhitespace();
                                    if (_reader.Peek() == ':')
                                    {
                                        do
                                        {
                                            string n = "";
                                            do
                                            {
                                                Read();
                                                ReadWhitespace();
                                                n += (n == "" ? "" : ".") + ReadName(false);
                                                ReadWhitespace();
                                            } while (_reader.Peek() == '.');

                                            imp.Add(n);
                                        } while (_reader.Peek() == ',');
                                    }
                                }
                                ret.AddItem(new ClassDefItem(sname, imp.ToArray(), l, cc));
                            }
                            break;
                        #endregion
                        #region case "return"
                        case "return":
                            {
                                IParseItem r = new ReturnItem();
                                name = ReadName(true);
                                if (name != "end" && name != "until" && name != "elseif" && name != "else")
                                {
                                    rr = ReadExp(name: name);
                                    r.AddItem(rr.Item1);
                                    ReadWhitespace();
                                    while (rr.Item2 == null && _reader.Peek() == ',')
                                    {
                                        Read();
                                        rr = ReadExp();
                                        r.AddItem(rr.Item1);
                                        ReadWhitespace();
                                    }

                                    if (rr.Item2 == null && _reader.Peek() == ';')
                                    {
                                        Read();
                                        ReadWhitespace();
                                    }

                                    name = rr.Item2 ?? ReadName(false);
                                    if (name != "end" && name != "until" && name != "elseif" && name != "else" && !string.IsNullOrWhiteSpace(name))
                                        throw new SyntaxException("The return statement must be the last statement in a block.",
                                            _line, _pos - _colOff - name.Length, _name);
                                }
                                ret.AddItem(r);
                                return new Tuple<IParseItem,string>(ret, name);
                            }
                        #endregion
                        #region default
                        default:
                            {
                                var re = ReadSimpExp(name);
                                if (re.Item2)
                                    throw new SyntaxException("An expression is not a variable.", 
                                        _line, _pos - _colOff, _name);
                                IParseItem exp = re.Item1;
                                if (exp is FuncCallItem)
                                {
                                    (exp as FuncCallItem).Statement = true;
                                    ret.AddItem(exp);
                                }
                                else if (exp is LiteralItem)
                                {
                                    throw new SyntaxException("A literal is not a variable.",
                                        _line, _pos - _colOff, _name);                                        
                                }
                                else
                                {
                                    VarInitItem i = new VarInitItem(false);
                                    i.AddName(exp);
                                    ReadWhitespace();
                                    while (_reader.Peek() != '=')
                                    {
                                        if (Read() != ',')
                                            throw new SyntaxException("Invalid variable definitions.",
                                                _line, _pos - _colOff, _name);
                                        ReadWhitespace();
                                        re = ReadSimpExp();
                                        if (re.Item2)
                                            throw new SyntaxException("An expression is not a variable.",
                                                _line, _pos - _colOff, _name);
                                        exp = re.Item1;
                                        if (exp is FuncCallItem)
                                            throw new SyntaxException("A function call is not a variable.",
                                                _line, _pos - _colOff, _name);
                                        if (exp is LiteralItem)
                                            throw new SyntaxException("A literal is not a variable.",
                                                _line, _pos - _colOff, _name);
                                        i.AddName(exp);
                                        ReadWhitespace();
                                    }

                                    do
                                    {
                                        Read();
                                        ReadWhitespace();
                                        rr = ReadExp();
                                        i.AddItem(rr.Item1);
                                        ReadWhitespace();
                                    } while (rr.Item2 == null && _reader.Peek() == ',');
                                    ret.AddItem(i);

                                    name = rr.Item2;
                                    reuse = name != null;
                                }
                            }
                            break;
                        #endregion
                    }
                }
                else
                    throw new SyntaxException("Invalid identifier '" + name + "', expecting start of statement.",
                        _line, _pos - _colOff, _name);
            }

            ret.AddItem(new ReturnItem());
            return new Tuple<IParseItem,string>(ret, null);
        }
        IParseItem ReadTable()
        {
            if (_reader.Peek() == '{')
                Read();
            ReadWhitespace();

            if (_reader.Peek() == '}')
            {
                Read();
                return new TableItem();
            }
            TableItem ret = new TableItem();
            do
            {
                ReadWhitespace();
                if (_reader.Peek() == '[')
                {
                    Read();
                    var rr = ReadExp();
                    IParseItem s = rr.Item1;
                    ReadWhitespace();
                    if (rr.Item2 != null || Read() != ']')
                        throw new SyntaxException("Invalid table definition, expecting ']'.", 
                            _line, _pos - _colOff, _name);
                    ReadWhitespace();
                    if (Read() != '=')
                        throw new SyntaxException("Invalid table definition, expecting '='.", 
                            _line, _pos - _colOff, _name);
                    rr = ReadExp();
                    IParseItem p = rr.Item1;
                    ret.AddItem(s, p);
                    if (rr.Item2 != null)
                        throw new SyntaxException("Invalid table definition.", 
                            _line, _pos - _colOff, _name);
                }
                else
                {
                    long pos = _pos;
                    string name = ReadName(false);
                    ReadWhitespace();
                    if (string.IsNullOrEmpty(name) || _reader.Peek() != '=')
                    {
                        var rr = ReadExp(name:name);
                        IParseItem e = rr.Item1;
                        ret.AddItem(null, e);
                    }
                    else
                    {
                        Read();
                        var rr = ReadExp();
                        IParseItem e = rr.Item1;
                        ret.AddItem(new LiteralItem(name), e);
                    }
                }

                ReadWhitespace();
                if (_reader.Peek() != ',' && _reader.Peek() != ';')
                {
                    if (_reader.Peek() != '}')
                        throw new SyntaxException("Invalid table definition, expecting end of table '}'.",
                            _line, _pos - _colOff, _name);
                }
                else
                    Read();
            } while (_reader.Peek() != '}');
            Read();

            return ret;
        }
        IParseItem ReadFunc(IParseItem n, string inst, long l, long cc)
        {
            FuncDef f = new FuncDef(n, l, cc);
            f.InstanceName = inst;
            bool b = false;
            ReadWhitespace();
            if (Read() != '(')
                throw new SyntaxException("Invalid token in function definition.", l, cc, _name);
            string name = ReadName(false);
            if (name.Length == 0)
            {
                if (_reader.Peek() != ')')
                {
                    char[] temp = new char[3];
                    _reader.Read(temp, 0, 3);
                    if (temp[0] != '.' || temp[1] != '.' || temp[3] != '.')
                        throw new SyntaxException("Invalid token in function definition.", _line, _pos - _colOff - 3, _name);
                    f.AddParam("...");
                    b = true;
                }
            }
            else
                f.AddParam(name);

            ReadWhitespace();
            while (!b && _reader.Peek() == ',')
            {
                Read();
                name = ReadName(false);
                if (name.Length == 0)
                {
                    char[] temp = new char[3];
                    _reader.Read(temp, 0, 3);
                    if (temp[0] != '.' || temp[1] != '.' || temp[3] != '.')
                        throw new SyntaxException("Invalid token in function definition.", _line, _pos - _colOff - 3, _name);
                    f.AddParam("...");
                    break;
                }
                else
                    f.AddParam(name);
                ReadWhitespace();
            }

            if (Read() != ')')
                throw new SyntaxException("Invalid token in function definition.", _line, _pos - _colOff - 1, _name);
            var rr = ReadChunk(f);
            f.Block = rr.Item1;
            f.Block.AddItem(new ReturnItem());
            if (rr.Item2 != "end")
                throw new SyntaxException("Invalid token '" + rr.Item2 + "' in function definition.", _line, _pos - _colOff - name.Length, _name);

            return f;
        }
        double ReadNumber()
        {
            return RuntimeHelper.ReadNumber(_reader, _name, _line, _colOff, ref _pos);
        }
        string ReadName(bool allow)
        {
            // allow leading whitespace
            ReadWhitespace();

            if (!__nameStartChars.Contains((char)_reader.Peek()))
                return "";

            StringBuilder build = new StringBuilder();
            bool b = false;
            int c;
            while ((c = _reader.Peek()) != -1)
            {
                if (!__nameChars.Contains((char)c))
                {
                    if (c == '`')
                    {
                        if (!allow)
                            throw new SyntaxException("Cannot use the grave(`) in this context.", _line, _pos - _colOff, _name);

                        if (b)
                            throw new SyntaxException("Can only have one grave(`) in a name.",
                                _line, _pos - _colOff, _name);
                        else
                            b = true;
                    }
                    else
                        return build.ToString();
                }
                else if (b && !"0123456789".Contains((char)c))
                {
                    _pos++;
                    _reader.Read();
                    return build.ToString();
                }
                build.Append((char)c);
                _pos++;
                _reader.Read();
            }
            return build.ToString();
        }
        string ReadString()
        {
            int i = 0;
            bool read = true;
            if (_reader.Peek() == '\'')
                i = -1;
            else if (_reader.Peek() == '"')
                i = -2;
            else if (_reader.Peek() == '[')
            {
                Read();
                while (_reader.Peek() == '=')
                {
                    i++;
                    Read();
                }
                if (_reader.Peek() != '[')
                    throw new SyntaxException("Invalid long-string definition.", _line, _pos - _colOff, _name);
                Read();
                if (_reader.Peek() == '\n')
                    Read();
                read = false;
            }
            else
                return null;

            StringBuilder str = new StringBuilder();
            if (read)
                _reader.Read();
            while (CanRead)
            {
                int c = Read();
                if (c == '\'' && i == -1)
                    return str.ToString();
                else if (c == '"' && i == -2)
                    return str.ToString();
                else if (c == '\n' && i < 0)
                    throw new SyntaxException("Unfinished string literal.", _line, _pos - _colOff, _name);
                else if (c == ']' && i >= 0)
                {
                    int j = 0;
                    while (_reader.Peek() == '=')
                    {
                        j++; 
                        Read();
                    }

                    if (_reader.Peek() != ']' || j != i)
                    {
                        str.Append(']');
                        str.Append('=', j);
                    }
                    else
                    {
                        Read();
                        return str.ToString();
                    }
                }
                else if (c == '\\')
                {
                    if (i >= 0)
                    {
                        str.Append('\\');
                        continue;
                    }

                    c = Read();
                    if (c == '\'' || c == '"' || c == '\\')
                        str.Append((char)c);
                    else if (c == '\n')
                    {
                        str.Append('\n');
                        _line++;
                        _colOff = _pos;
                    }
                    else if (c == 'z')
                        ReadWhitespace();
                    else if (c == 'n')
                        str.Append('\n');
                    else if (c == 'a')
                        str.Append('\a');
                    else if (c == 'b')
                        str.Append('\b');
                    else if (c == 'f')
                        str.Append('\f');
                    else if (c == 'n')
                        str.Append('\n');
                    else if (c == 'r')
                        str.Append('\r');
                    else if (c == 't')
                        str.Append('\t');
                    else if (c == 'v')
                        str.Append('\v');
                    else if (c == 'x')
                    {
                        int ii = 0;
                        c = Read();
                        if (!"0123456789ABCDEFabcdef".Contains((char)c))
                            throw new SyntaxException("Invalid escape sequence '\\x" + c + "'", _line, _pos - _colOff, _name);
                        ii = int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        c = Read();
                        if (!"0123456789ABCDEFabcdef".Contains((char)c))
                            throw new SyntaxException("Invalid escape sequence '\\x" + ii.ToString("x") + c + "'", _line, _pos - _colOff, _name);
                        ii = (ii >> 16) + int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        str.Append((char)ii);
                    }
                    else if ("0123456789".Contains((char)c))
                    {
                        int ii = 0;
                        if (!"0123456789".Contains((char)_reader.Peek()))
                            continue;
                        c = Read();
                        ii = int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                        if ("0123456789".Contains((char)_reader.Peek()))
                        {
                            c = Read();
                            ii = (ii * 10) + int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                            if ("0123456789".Contains((char)_reader.Peek()))
                            {
                                c = Read();
                                ii = (ii * 10) + int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                            }
                        }
                        str.Append((char)ii);
                    }
                    else
                        throw new SyntaxException("Invalid escape sequence '\\" + c + "'.", _line, _pos - _colOff, _name);
                }
                else
                    str.Append((char)c);
            }

            return str.ToString();
        }
        void ReadWhitespace()
        {
            int c;
            while ((c = _reader.Peek()) != -1)
            {
                /*if (c == '-')
                {
                    Read();
                    if (_reader.Peek() == '-')
                    {
                        Read();
                        ReadComment();
                        continue;
                    }
                    return true;
                }
                else */if (c == ' ' || c == '\n' || c == '\t' || c == '\r')
                {
                    if (c == '\n')
                    {
                        _line++; _colOff = _pos + 1;
                    }
                    Read();
                    continue;
                }
                break;
            }
        }
        void ReadComment()
        {
            long l = _line, c = _pos - _colOff - 2;
            int dep = -1;
            int cc;
            if (_reader.Peek() == '[')
            {
                dep = 0;
                Read();
                while ((cc = Read()) != -1)
                {
                    if (cc == '=')
                        dep++;
                    else if (cc == '\n')
                    {
                        _line++;
                        _colOff = _pos + 1;
                        return;
                    }
                    else
                    {
                        if (cc != '[')
                            dep = -1;
                        break;
                    }
                }
            }

            int cdep = -1;
            while ((cc = Read()) != -1)
            {
                if (dep == -1)
                {
                    if (cc == '\n')
                    {
                        _line++;
                        _colOff = _pos + 1;
                        return;
                    }
                }
                else
                {
                    if (cc == '\n')
                    {
                        _line++;
                        _colOff = _pos + 1;
                        cdep = -1;
                    }
                    else if (cdep != -1)
                    {
                        if (cc == ']')
                        {
                            if (cdep == dep)
                                return;
                            else
                                cdep = -1;
                        }
                        else if (cc != '=')
                            cdep = -1;
                        else
                            cdep++;
                    }
                    else if (cc == ']')
                    {
                        cdep = 0;
                    }
                }
            }
            if (!CanRead && dep != -1)
                throw new SyntaxException("Expecting end of long comment that started at:", l, c, _name);
        }
        Tuple<IParseItem, bool> ReadSimpExp(string name = null)
        {
            Stack<int> ex = new Stack<int>(); // 1 - neg, 2 - not, 3 - len
            IParseItem o = null;
            bool b = false;

            if (string.IsNullOrWhiteSpace(name))
            {
                while (true)
                {
                    ReadWhitespace();
                    if (_reader.Peek() == '-')
                    {
                        Read();
                        ex.Push(1);
                        continue;
                    }
                    else if (_reader.Peek() == 'n')
                    {
                        Read();
                        name = "n";
                        if (_reader.Peek() == 'o')
                        {
                            Read();
                            name += "o";
                            if (_reader.Peek() == 't')
                            {
                                Read();
                                name += "t";
                                if (!__nameChars.Contains((char)_reader.Peek()))
                                {
                                    ex.Push(2);
                                    name = null;
                                    continue;
                                }
                            }
                        }
                        if (__nameChars.Contains((char)_reader.Peek()))
                            name += ReadName(true);
                    }
                    else if (_reader.Peek() == '#')
                    {
                        Read();
                        ex.Push(3);
                        continue;
                    }
                    break;
                }

                if (".0123456789".Contains((char)_reader.Peek()))
                {
                    double d = ReadNumber();
                    o = new LiteralItem(d);
                }
                else if (_reader.Peek() == '\'' || _reader.Peek() == '"')
                {
                    o = new LiteralItem(ReadString());
                }
                else if (_reader.Peek() == '{')
                {
                    o = ReadTable();
                }
                else if (_reader.Peek() == '(')
                {
                    Read();
                    var rr = ReadExp();
                    o = rr.Item1;
                    ReadWhitespace();
                    if (rr.Item2 != null || Read() != ')')
                        throw new SyntaxException("Invalid expression.", _line, _pos - _colOff, _name);
                }
            }

            if (o == null)
            {
                string inst = null;
                name = name ?? ReadName(true);
                if (name == "function")
                    return new Tuple<IParseItem, bool>(ReadFunc(null, null, _line, _pos - _colOff - 8), false);

                if (name == "nil")
                    o = new LiteralItem(null);
                else if (name == "false")
                    o = new LiteralItem(false);
                else if (name == "true")
                    o = new LiteralItem(true);
                else
                    o = new NameItem(name);

                while (true)
                {
                    ReadWhitespace();
                    if (_reader.Peek() == '.')
                    {
                        Read();
                        b = _reader.Peek() == '.';
                        if (!b)
                        {
                            if (inst != null)
                                throw new SyntaxException("Cannot use an indexer after an instance call.", _line, _pos - _colOff, _name);

                            name = ReadName(true);
                            o = new IndexerItem(o, new LiteralItem(name));
                            continue;
                        }
                        else
                            Read();
                    }
                    else if (_reader.Peek() == ':')
                    {
                        Read();
                        inst = ReadName(true);
                        continue;
                    }
                    else if (_reader.Peek() == '[')
                    {
                        if (inst != null)
                            throw new SyntaxException("Cannot use an indexer after an instance call.", _line, _pos - _colOff, _name);

                        Read();
                        var rr = ReadExp();
                        IParseItem exp = rr.Item1;
                        o = new IndexerItem(o, exp);
                        ReadWhitespace();
                        if (rr.Item2 != null || Read() != ']')
                            throw new SyntaxException("Invalid indexer.", _line, _pos - _colOff, _name);
                        continue;
                    }
                    else if (_reader.Peek() == '\'' || _reader.Peek() == '"')
                    {
                        string s = ReadString();
                        o = new FuncCallItem(o, inst);
                        inst = null;
                        o.AddItem(new LiteralItem(s));
                        continue;
                    }
                    else if (_reader.Peek() == '{')
                    {
                        IParseItem t = ReadTable();
                        o = new FuncCallItem(o, inst);
                        inst = null;
                        o.AddItem(t);
                        continue;
                    }
                    else if (_reader.Peek() == '(')
                    {
                        Read();
                        ReadWhitespace();
                        o = new FuncCallItem(o, inst);
                        inst = null;
                        while (_reader.Peek() != ')')
                        {
                            var rr = ReadExp();
                            if (rr.Item2 != null)
                                throw new SyntaxException("Invalid function call.", _line, _pos - _colOff, _name);
                            IParseItem e = rr.Item1;
                            ReadWhitespace();
                            o.AddItem(e);
                            if (_reader.Peek() == ',')
                                Read();
                            else if (_reader.Peek() == ')')
                                break;
                            else
                                throw new SyntaxException("Invalid function call.", _line, _pos - _colOff, _name);
                        }
                        Read();
                        continue;
                    }
                    break;
                }
                if (inst != null)
                    throw new SyntaxException("Invalid instance function call.", _line, _pos - _colOff, _name);
            }

            ReadWhitespace();
            if (_reader.Peek() == '^')
            {
                var rr = ReadSimpExp();
                IParseItem other = rr.Item1;
                b = b || rr.Item2;
                o = new BinOpItem(o, BinaryOperationType.Power);
                (o as BinOpItem).Rhs = other;
            }

            while (ex.Count > 0)
            {
                switch (ex.Pop())
                {
                    case 1: // neg
                        if (o is LiteralItem)
                        {
                            object oo = (o as LiteralItem).Item;
                            if (!(oo is double))
                                throw new SyntaxException("Cannot use unary minus on a string, bool, or nil.", _line, _pos - _colOff, _name);

                            o = new LiteralItem(-(double)oo);
                        }
                        else
                            o = new UnOpItem(o, UnaryOperationType.Minus);
                        break;
                    case 2: // not
                        o = new UnOpItem(o, UnaryOperationType.Not);
                        break;
                    case 3: // len
                        o = new UnOpItem(o, UnaryOperationType.Length);
                        break;
                }
            }
            return new Tuple<IParseItem,bool>(o, b);
        }
        Tuple<IParseItem, string> ReadExp(int prec = -1, string name = null)
        {
            var rr = ReadSimpExp(name);
            IParseItem cur = rr.Item1;
            BinOpItem ret = null;
            string frag = rr.Item2 ? ".." : null;
            while (true)
            {
                ReadWhitespace();
                int c = frag != null ? frag[0] : _reader.Peek();
                switch (c)
                {
                    case 'o':
                        {
                            bool d = false;
                            if (frag == null)
                                frag = ReadName(true);
                            if (frag == "or")
                                d = true;

                            if (d)
                            {
                                ret = new BinOpItem(ret ?? cur, BinaryOperationType.Or);
                                var rrr = ReadExp(8);
                                ret.Rhs = rrr.Item1;
                                frag = rrr.Item2;
                                continue;
                            }
                        }
                        break;
                    case 'a':
                        {
                            bool d = false;
                            if (frag == null)
                                frag = ReadName(true);
                            if (frag == "and")
                                d = true;

                            if (d)
                            {
                                if (prec == -1 || prec > 7)
                                {
                                    ret = new BinOpItem((prec == -1 ? ret : null) ?? cur, BinaryOperationType.And);
                                    var rrr = ReadExp(7);
                                    ret.Rhs = rrr.Item1;
                                    frag = rrr.Item2;
                                    continue;
                                }
                            }
                        }
                        break;
                    case '>':
                    case '<':
                    case '=':
                    case '~':
                        {
                            bool b;
                            if (frag == null)
                            {
                                frag = "" + (char)c;
                                Read();
                                if (_reader.Peek() == '=')
                                {
                                    b = true;
                                    frag += "=";
                                    Read();
                                }
                                else
                                {
                                    b = false;
                                    if (c == '~')
                                        throw new SyntaxException("Invalid token ~ in expression.", _line, _pos - _colOff, _name);
                                    if (c == '=')
                                        break;
                                }
                            }
                            else
                            {
                                b = frag.Length > 0;
                            }

                            var o = c == '>' ? b ? BinaryOperationType.Gte : BinaryOperationType.Gt :
                                c == '<' ? b ? BinaryOperationType.Lte : BinaryOperationType.Lt :
                                c == '=' ? BinaryOperationType.Equals : BinaryOperationType.NotEquals;
                            if (prec == -1 || prec > 6)
                            {
                                ret = new BinOpItem((prec == -1 ? ret : null) ?? cur, o);
                                var rrr = ReadExp(6);
                                ret.Rhs = rrr.Item1;
                                frag = rrr.Item2;
                                continue;
                            }
                        }
                        break;
                    case '.':
                        if (frag == null)
                        {
                            frag = ".";
                            Read();
                            if (_reader.Peek() != '.')
                                break;
                            Read();
                            frag = "..";
                        }
                        else
                        {
                            if (frag.Length < 2 || frag[1] != '.')
                                break;
                        }

                        if (prec == -1 || prec >= 5)
                        {
                            ret = new BinOpItem((prec == -1 ? ret : null) ?? cur, BinaryOperationType.Concat);
                            var rrr = ReadExp(5);
                            ret.Rhs = rrr.Item1;
                            frag = rrr.Item2;
                            continue;
                        }
                        break;
                    case '+':
                    case '-':
                        if (frag == null)
                        {
                            Read();
                            if (_reader.Peek() == '-')
                            {
                                Read();
                                ReadComment();
                                continue;
                            }
                        }
                        if (prec == -1 || prec > 4)
                        {
                            ret = new BinOpItem((prec == -1 ? ret : null) ?? cur, c == '+' ? BinaryOperationType.Add : BinaryOperationType.Subtract);
                            var rrr = ReadExp(4);
                            ret.Rhs = rrr.Item1;
                            frag = rrr.Item2;
                            continue;
                        }
                        frag = ((char)c).ToString();
                        break;
                    case '*':
                    case '/':
                    case '%':
                        if (prec == -1 || prec > 3)
                        {
                            if (frag == null)
                                Read();
                            ret = new BinOpItem((prec == -1 ? ret : null) ?? cur, 
                                c == '*' ? BinaryOperationType.Multiply : c == '%' ? BinaryOperationType.Modulo : BinaryOperationType.Divide);
                            var rrr = ReadExp(3);
                            ret.Rhs = rrr.Item1;
                            frag = rrr.Item2;
                            continue;
                        }
                        frag = ((char)c).ToString();
                        break;
                }
                break;
            }

            return new Tuple<IParseItem, string>(ret ?? cur, frag);
        }
    }
}