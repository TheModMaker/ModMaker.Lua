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
        CharDecorator _input;
        long _line = 0, _colOff = 0;

        public PlainParser(CharDecorator input, string name = null)
        {
            this._input = input;
            this._name = name;
            this._output = null;
        }

        public LuaChunk Output { get { return _output; } }

        public IParseItem LoadFunc()
        {
            IParseItem ret = null;



            return ret;
        }
        public LuaChunk LoadChunk(LuaEnvironment E)
        {
            // check the SHA512 hash.
            string hash;
            {
                byte[] bhash = _input.GetHash();
                if (bhash == null)
                {
                    if (E.Settings.AllowNonSeekStreams)
                        hash = null;
                    else
                        throw new InvalidOperationException("Unable to load given chunk because the stream cannot be seeked, see LuaSettings.AllowNonSeekStreams.");
                }
                else
                {
                    hash = bhash.ToStringBase16().ToUpper(CultureInfo.InvariantCulture);
                    lock (_loaded)
                        if (_loaded.ContainsKey(hash))
                            return _loaded[hash];
                }
            }

            _line = 1;
            _colOff = 0;
            IParseItem chunk = ReadChunk();
            ChunkBuilderNew cb = E.DefineChunkNew(_name);

            /* wait for any running threads */
            chunk.WaitOne();
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
            chunk.GenerateILNew(cb);
            _output = cb.CreateChunk(E);
            if (hash != null)
                lock (_loaded)
                    _loaded[hash] = _output;
            return _output;
        }

        IParseItem ReadChunk(IParseItem parrent = null)
        {
            IParseItem ret = new BlockItem();

            char? c;
            while (_input.CanRead)
            {
                ReadWhitespace();
                c = _input.ReadChar();
                if (c == null)
                    break;
                if (c == '-' && _input.PeekChar() == '-')
                {
                    _input.ReadChar();
                    if (_input.CanRead)
                        ReadComment();
                }
                else if (c == ';' || c == ' ' || c == '\t' || c == '\n')
                {
                    if (c == '\n')
                    {
                        _line++; _colOff = _input.Position + 1;
                    }
                    continue;
                }
                else if (c == ':' && _input.PeekChar() == ':')
                {
                    _input.ReadChar();
                    if (_input.CanRead)
                        throw new SyntaxException("Invalid Label definition.", _line, _input.Position - _colOff, _name);
                    string label = ReadName(false);
                    if (!_input.CanRead || _input.ReadChar() != ':' || _input.ReadChar() != ':')
                        throw new SyntaxException("Invalid Label definition.", _line, _input.Position - _colOff, _name);

                    ret.AddItem(new LabelItem(label));
                }
                else if (__nameStartChars.Contains(c.Value))
                {
                    string name = c.Value + ReadName(true);
                    switch (name)
                    {
                        #region case "break"
                        case "break":
                            ret.AddItem(new GotoItem("<break>", _line, _input.Position - _colOff - 5));
                            _glt = true;
                            break;
                        #endregion
                        #region case "goto"
                        case "goto":
                            name = ReadName(false);
                            if (__reserved.Contains(name))
                                throw new SyntaxException("Invalid _name, '" + name + "' is reserved.", _line, _input.Position - _colOff - name.Length, _name);
                            ret.AddItem(new GotoItem(name, _line, _input.Position - _colOff - name.Length));
                            _glt = true;
                            break;
                        #endregion
                        #region case "do"
                        case "do":
                            ret.AddItem(ReadChunk(ret));
                            name = ReadName(false);
                            if (name != "end")
                                throw new SyntaxException("Expecting 'end' for end of local block.", _line, _input.Position - _colOff - 3, _name);
                            break;
                        #endregion
                        #region case "end"
                        case "end":
                            if (parrent == null)
                                throw new SyntaxException("Invalid token 'end' in global chunk.", _line, _input.Position - _colOff - 3, _name);
                            _input.Move(-3);
                            return ret;
                        #endregion
                        #region case "else/elseif"
                        case "elseif":
                        case "else":
                            if (parrent == null)
                                throw new SyntaxException("Invalid token '" + name + "' in global chunk.", _line, _input.Position - _colOff - name.Length, _name);
                            else if (!(parrent is IfItem))
                                throw new SyntaxException("'" + name + "' is only valid in an if block.", _line, _input.Position - _colOff - name.Length, _name);

                            _input.Move(-name.Length);
                            return ret;
                        #endregion
                        #region case "until"
                        case "until":
                            if (parrent == null)
                                throw new SyntaxException("Invalid token 'until' in global chunk.", _line, _input.Position - _colOff - 5, _name);
                            else if (!(parrent is RepeatItem))
                                throw new SyntaxException("'until' is only valid in an repeat block.", _line, _input.Position - _colOff - 5, _name);
                        
                            _input.Move(-5);
                            return ret;
                        #endregion
                        #region case "while"
                        case "while":
                            {
                                WhileItem w = new WhileItem();
                                w.Exp = ReadExp();
                                name = ReadName(false);
                                if (name != "do")
                                    throw new SyntaxException("Invalid token '" + name + "' in while definition.", _line, _input.Position - _colOff - name.Length, _name);
                                w.Block = ReadChunk(w);
                                name = ReadName(false);
                                if (name != "end")
                                    throw new SyntaxException("Invalid token '" + name + "' in while definition.", _line, _input.Position - _colOff - name.Length, _name);
                                ret.AddItem(w);
                            }
                            break;
                        #endregion
                        #region case "repeat"
                        case "repeat":
                            {
                                RepeatItem r = new RepeatItem();
                                r.Block = ReadChunk(r);
                                name = ReadName(false);
                                if (name != "until")
                                    throw new SyntaxException("Invalid token '" + name + "' in repeat definition.", _line, _input.Position - _colOff - name.Length, _name);
                                r.Exp = ReadExp();
                                ret.AddItem(r);
                            }
                            break;
                        #endregion
                        #region case "if"
                        case "if":
                            {
                                IfItem i = new IfItem();
                                i.Exp = ReadExp();
                                name = ReadName(false);
                                if (name != "then")
                                    throw new SyntaxException("Invalid token '" + name + "' in if definition.", _line, _input.Position - _colOff - name.Length, _name);
                                i.Block = ReadChunk(i);
                                name = ReadName(false);
                                while (name == "elseif")
                                {
                                    IParseItem e = ReadExp();
                                    name = ReadName(false);
                                    if (name != "then")
                                        throw new SyntaxException("Invalid token '" + name + "' in elseif definition.", _line, _input.Position - _colOff - name.Length, _name);
                                    IParseItem b = ReadChunk(i);
                                    i.AddItem(e, b);
                                    name = ReadName(false);
                                }
                                if (name != "else" && name != "end")
                                    throw new SyntaxException("Invalid token '" + name + "' in if definition.", _line, _input.Position - _colOff - name.Length, _name);
                                if (name == "else")
                                {
                                    i.ElseBlock = ReadChunk(i);
                                    name = ReadName(false);
                                }
                                if (name != "end")
                                    throw new SyntaxException("Invalid token '" + name + "' in if definition.", _line, _input.Position - _colOff - name.Length, _name);
                                ret.AddItem(i);
                            }
                            break;
                        #endregion
                        #region case "for"
                        case "for":
                            {
                                name = ReadName(false);
                                if (__reserved.Contains(name))
                                    throw new SyntaxException("Invalid _name, '" + name + "' is reserved.", _line, _input.Position - _colOff - name.Length, _name);
                                ReadWhitespace();
                                if (_input.PeekChar() == '=')
                                {
                                    _input.ReadChar();
                                    ForNumItem i = new ForNumItem(name);
                                    i.Start = ReadExp();
                                    ReadWhitespace();
                                    if (_input.ReadChar() != ',')
                                        throw new SyntaxException("Invalid token '" + _input.PeekChar() + "' in for definition.", _line, _input.Position - _colOff - 1, _name);
                                    i.Limit = ReadExp();
                                    ReadWhitespace();
                                    if (_input.PeekChar() == ',')
                                    {
                                        _input.ReadChar();
                                        i.Step = ReadExp();
                                    }
                                    name = ReadName(false);
                                    if (name != "do")
                                        throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _input.Position - _colOff - name.Length, _name);
                                    i.Block = ReadChunk(i);
                                    name = ReadName(false);
                                    if (name != "end")
                                        throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _input.Position - _colOff - name.Length, _name);
                                    ret.AddItem(i);
                                }
                                else
                                {
                                    List<string> names = new List<string>();
                                    names.Add(name);
                                    while (_input.PeekChar() == ',')
                                    {
                                        _input.ReadChar();
                                        name = ReadName(false);
                                        if (__reserved.Contains(name))
                                            throw new SyntaxException("Invalid _name, '" + name + "' is reserved.", _line, _input.Position - _colOff - name.Length, _name);
                                        names.Add(name);
                                        ReadWhitespace();
                                    }
                                    name = ReadName(false);
                                    if (name != "in")
                                        throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _input.Position - _colOff - name.Length, _name);
                                    
                                    ForGenItem f = new ForGenItem(names);
                                    f.AddItem(ReadExp());
                                    ReadWhitespace();
                                    while (_input.PeekChar() == ',')
                                    {
                                        f.AddItem(ReadExp());
                                        ReadWhitespace();
                                    }

                                    name = ReadName(false);
                                    if (name != "do")
                                        throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _input.Position - _colOff - name.Length, _name);
                                    f.Block = ReadChunk(f);
                                    name = ReadName(false);
                                    if (name != "end")
                                        throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _input.Position - _colOff - name.Length, _name);
                                    ret.AddItem(f);
                                }
                            }
                            break;
                        #endregion
                        #region case "function"
                        case "function":
                            {
                                long l = _line, cc = _input.Position - _colOff - 8;
                                name = ReadName(false);
                                if (__reserved.Contains(name))
                                    throw new SyntaxException("Invalid _name, '" + name + "' is reserved.", _line, _input.Position - _colOff - name.Length, _name);
                                IParseItem n = new NameItem(name);
                                string inst = null;
                                while (_input.PeekChar() == '.')
                                {
                                    _input.ReadChar();
                                    name = ReadName(false);
                                    n = new IndexerItem(n, new LiteralItem(name));
                                }
                                if (_input.PeekChar() == ':')
                                {
                                    _input.ReadChar();
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
                                long l = _line, cc = _input.Position - _colOff - 5;
                                name = ReadName(false);
                                if (name == "function")
                                {
                                    name = ReadName(false);
                                    if (__reserved.Contains(name))
                                        throw new SyntaxException("Invalid _name, '" + name + "' is reserved.", _line, _input.Position - _colOff - name.Length, _name);

                                    FuncDef f = new FuncDef(new NameItem(name), l, cc, true);
                                    bool b = false;
                                    ReadWhitespace();
                                    if (_input.ReadChar() != '(')
                                        throw new SyntaxException("Invalid token in function definition.", _line, _input.Position - _colOff - 1, _name);
                                    name = ReadName(false);
                                    if (name.Length == 0)
                                    {
                                        if (_input.PeekChar() != ')')
                                        {
                                            if (_input.Read(3) != "...")
                                                throw new SyntaxException("Invalid token in function definition.", _line, _input.Position - _colOff - 3, _name);
                                            f.AddParam("...");
                                            b = true;
                                        }
                                    }
                                    else
                                        f.AddParam(name);

                                    ReadWhitespace();
                                    while (!b && _input.PeekChar() == ',')
                                    {
                                        _input.ReadChar();
                                        name = ReadName(false);
                                        if (name.Length == 0)
                                        {
                                            if (_input.Read(3) != "...")
                                                throw new SyntaxException("Invalid token in function definition.", _line, _input.Position - _colOff - 3, _name);
                                            f.AddParam("...");
                                            break;
                                        }
                                        else
                                            f.AddParam(name);
                                        ReadWhitespace();
                                    }

                                    if (_input.ReadChar() != ')')
                                        throw new SyntaxException("Invalid token in function definition.", _line, _input.Position - _colOff - 1, _name);
                                    f.Block = ReadChunk(f);
                                    f.Block.AddItem(new ReturnItem());
                                    name = ReadName(false);
                                    if (name != "end")
                                        throw new SyntaxException("Invalid token '" + name + "' in function definition.", _line, _input.Position - _colOff - name.Length, _name);
                                    ret.AddItem(f);
                                }
                                else
                                {
                                    if (__reserved.Contains(name))
                                        throw new SyntaxException("Invalid _name, '" + name + "' is reserved.", _line, _input.Position - _colOff - name.Length, _name);

                                    VarInitItem i = new VarInitItem(true);
                                    i.AddName(new NameItem(name));
                                    ReadWhitespace();
                                    while (_input.PeekChar() == ',')
                                    {
                                        _input.ReadChar();
                                        name = ReadName(false);

                                        if (__reserved.Contains(name))
                                            throw new SyntaxException("Invalid _name, '" + name + "' is reserved.", _line, _input.Position - _colOff - name.Length, _name);
                                        i.AddName(new NameItem(name));
                                        ReadWhitespace();
                                    }

                                    if (_input.PeekChar() == '=')
                                    {
                                        _input.ReadChar();
                                        i.AddItem(ReadExp());
                                        ReadWhitespace();
                                        while (_input.PeekChar() == ',')
                                        {
                                            _input.ReadChar();
                                            i.AddItem(ReadExp());
                                            ReadWhitespace();
                                        }
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
                                long l = _line, cc = _input.Position - _colOff - 5;
                                ReadWhitespace();
                                if (_input.PeekChar() == '\'' || _input.PeekChar() == '"')
                                {
                                    sname = ReadString();
                                    ReadWhitespace();
                                    if (_input.PeekChar() == '(')
                                    {
                                        ReadWhitespace();
                                        while (_input.PeekChar() != ')')
                                        {
                                            imp.Add(ReadName(false));
                                            ReadWhitespace();
                                            if (_input.PeekChar() != ',')
                                                throw new SyntaxException("Invalid class definition.",
                                                    _line, _input.Position - _colOff, _name);
                                            _input.ReadChar();
                                            ReadWhitespace();
                                        }
                                    }
                                }
                                else
                                {
                                    sname = ReadName(false);
                                    ReadWhitespace();
                                    if (_input.PeekChar() == ':')
                                    {
                                        do
                                        {
                                            string n = "";
                                            do
                                            {
                                                _input.ReadChar();
                                                ReadWhitespace();
                                                n += (n == "" ? "" : ".") + ReadName(false);
                                                ReadWhitespace();
                                            } while (_input.PeekChar() == '.');

                                            imp.Add(n);
                                        } while (_input.PeekChar() == ',');
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
                                    r.AddItem(ReadExp(name: name));
                                    ReadWhitespace();
                                    while (_input.PeekChar() == ',')
                                    {
                                        _input.ReadChar();
                                        r.AddItem(ReadExp());
                                        ReadWhitespace();
                                    }

                                    if (_input.PeekChar() == ';')
                                    {
                                        _input.ReadChar();
                                        ReadWhitespace();
                                    }

                                    name = ReadName(false);
                                    if (name != "end" && name != "until" && name != "elseif" && name != "else" && !string.IsNullOrWhiteSpace(name))
                                        throw new SyntaxException("The return statement must be the last statement in a block.",
                                            _line, _input.Position - _colOff - name.Length, _name);
                                    _input.Move(-name.Length);
                                }
                                else
                                    _input.Move(-name.Length);
                                ret.AddItem(r);
                                return ret;
                            }
                        #endregion
                        #region default
                        default:
                            {
                                IParseItem exp = ReadSimpExp(name);
                                if (exp is FuncCallItem)
                                {
                                    (exp as FuncCallItem).Statement = true;
                                    ret.AddItem(exp);
                                }
                                else if (exp is LiteralItem)
                                {
                                    throw new SyntaxException("A literal is not a variable.",
                                        _line, _input.Position - _colOff, _name);
                                }
                                else
                                {
                                    VarInitItem i = new VarInitItem(false);
                                    i.AddName(exp);
                                    ReadWhitespace();
                                    while (_input.PeekChar() != '=')
                                    {
                                        if (_input.ReadChar() != ',')
                                            throw new SyntaxException("Invalid variable definitions.",
                                                _line, _input.Position - _colOff, _name);
                                        ReadWhitespace();
                                        exp = ReadSimpExp();
                                        if (exp is FuncCallItem)
                                            throw new SyntaxException("A function call is not a variable.",
                                                _line, _input.Position - _colOff, _name);
                                        if (exp is LiteralItem)
                                            throw new SyntaxException("A literal is not a variable.",
                                                _line, _input.Position - _colOff, _name);
                                        i.AddName(exp);
                                        ReadWhitespace();
                                    }

                                    do
                                    {
                                        _input.ReadChar();
                                        ReadWhitespace();
                                        i.AddItem(ReadExp());
                                        ReadWhitespace();
                                    } while (_input.PeekChar() == ',');
                                    ret.AddItem(i);
                                }
                            }
                            break;
                        #endregion
                    }
                }
                else
                    throw new SyntaxException("Invalid character '" + c + "', expecting start of statement.",
                        _line, _input.Position - _colOff, _name);
            }

            ret.AddItem(new ReturnItem());
            return ret;
        }
        IParseItem ReadTable()
        {
            if (_input.PeekChar() == '{')
                _input.ReadChar();
            ReadWhitespace();

            if (_input.PeekChar() == '}')
            {
                _input.ReadChar();
                return new TableItem();
            }
            TableItem ret = new TableItem();
            do
            {
                ReadWhitespace();
                if (_input.PeekChar() == '[')
                {
                    _input.ReadChar();
                    IParseItem s = ReadExp();
                    ReadWhitespace();
                    if (_input.ReadChar() != ']')
                        throw new SyntaxException("Invalid table definition, expecting ']'.", 
                            _line, _input.Position - _colOff, _name);
                    ReadWhitespace();
                    if (_input.ReadChar() != '=')
                        throw new SyntaxException("Invalid table definition, expecting '='.", 
                            _line, _input.Position - _colOff, _name);
                    IParseItem p = ReadExp();
                    ret.AddItem(s, p);
                }
                else
                {
                    long pos = _input.Position;
                    string name = ReadName(false);
                    ReadWhitespace();
                    if (string.IsNullOrEmpty(name) || _input.PeekChar() != '=')
                    {
                        _input.Move(pos - _input.Position);
                        IParseItem e = ReadExp();
                        ret.AddItem(null, e);
                    }
                    else
                    {
                        _input.ReadChar();
                        IParseItem e = ReadExp();
                        ret.AddItem(new LiteralItem(name), e);
                    }
                }

                ReadWhitespace();
                if (_input.PeekChar() != ',' && _input.PeekChar() != ';')
                {
                    if (_input.PeekChar() != '}')
                        throw new SyntaxException("Invalid table definition, expecting end of table '}'.",
                            _line, _input.Position - _colOff, _name);
                }
                else
                    _input.ReadChar();
            } while (_input.PeekChar() != '}');
            _input.ReadChar();

            return ret;
        }
        IParseItem ReadFunc(IParseItem n, string inst, long l, long cc)
        {
            FuncDef f = new FuncDef(n, l, cc);
            f.InstanceName = inst;
            bool b = false;
            ReadWhitespace();
            if (_input.ReadChar() != '(')
                throw new SyntaxException("Invalid token in function definition.", l, cc, _name);
            string name = ReadName(false);
            if (name.Length == 0)
            {
                if (_input.PeekChar() != ')')
                {
                    if (_input.Read(3) != "...")
                        throw new SyntaxException("Invalid token in function definition.", _line, _input.Position - _colOff - 3, _name);
                    f.AddParam("...");
                    b = true;
                }
            }
            else
                f.AddParam(name);

            ReadWhitespace();
            while (!b && _input.PeekChar() == ',')
            {
                _input.ReadChar();
                name = ReadName(false);
                if (name.Length == 0)
                {
                    if (_input.Read(3) != "...")
                        throw new SyntaxException("Invalid token in function definition.", _line, _input.Position - _colOff - 3, _name);
                    f.AddParam("...");
                    break;
                }
                else
                    f.AddParam(name);
                ReadWhitespace();
            }

            if (_input.ReadChar() != ')')
                throw new SyntaxException("Invalid token in function definition.", _line, _input.Position - _colOff - 1, _name);
            f.Block = ReadChunk(f);
            f.Block.AddItem(new ReturnItem());
            name = ReadName(false);
            if (name != "end")
                throw new SyntaxException("Invalid token '" + name + "' in function definition.", _line, _input.Position - _colOff - name.Length, _name);

            return f;
        }
        double ReadNumber()
        {
            long l = _line, col = _input.Position - _colOff;
            bool hex = false;
            double val = 0, exp = 0, dec = 0;
            int decC = 0;
            bool negV = false, negE = false;
            if (_input.PeekChar() == '-')
            {
                negV = true;
                _input.ReadChar();
            }
            if (_input.PeekChar() == '0' && (_input.PeekChar(1) == 'x' || _input.PeekChar(1) == 'X'))
            {
                hex = true;
                _input.Move(2);
            }

            bool b = true;
            int stat = 0; // 0-val, 1-dec, 2-exp
            char? c;
            while (b && _input.CanRead)
            {
                switch (c = _input.PeekChar())
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        _input.ReadChar();
                        if (stat == 0)
                        {
                            val *= (hex ? 16 : 10);
                            val += int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                        }
                        else if (stat == 1)
                        {
                            dec *= (hex ? 16 : 10);
                            dec += int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                            decC++;
                        }
                        else
                        {
                            exp *= (hex ? 16 : 10);
                            exp += int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                        }
                        break;
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'f':
                        _input.ReadChar();
                        if (!hex)
                        {
                            b = false; break;
                        }
                        if (stat == 0)
                        {
                            val *= 16;
                            val += int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        }
                        else if (stat == 1)
                        {
                            dec *= 16;
                            dec += int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                            decC++;
                        }
                        else
                        {
                            exp *= 16;
                            exp += int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        }
                        break;
                    case 'e':
                    case 'p':
                        _input.ReadChar();
                        if ((hex && c == 'p') || (!hex && c == 'e'))
                        {
                            if (stat == 2)
                                throw new SyntaxException("Can only have exponent designator('e' or 'p') per number.", l, col, _name);
                            stat = 2;

                            if (!_input.CanRead)
                                throw new SyntaxException("Must specify at least one number for the exponent.", l, col, _name);
                            if (_input.PeekChar() == '+' || (_input.PeekChar() == '-' && (negE = true == true)))
                            {
                                _input.Move(1);
                                if (!_input.CanRead)
                                    throw new SyntaxException("Must specify at least one number for the exponent.", l, col, _name);
                            }

                            if ("0123456789".Contains(_input.PeekChar() ?? '\0'))
                            {
                                exp = int.Parse(_input.ReadChar().ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                break;
                            }
                            else if (hex && "abcdefABCDEF".Contains(_input.PeekChar() ?? '\0'))
                            {
                                exp = int.Parse(_input.ReadChar().ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                break;
                            }
                            throw new SyntaxException("Must specify at least one number for the exponent.", l, col, _name);
                        }
                        else if (hex && c == 'e')
                        {
                            if (stat == 0)
                            {
                                val *= 16;
                                val += 14;
                            }
                            else if (stat == 1)
                            {
                                dec *= 16;
                                dec += 14;
                                decC++;
                            }
                            else
                            {
                                exp *= 16;
                                exp += 14;
                            }
                        }
                        else
                            b = false;
                        break;
                    case '.':
                        _input.ReadChar();
                        if (stat == 0)
                            stat = 1;
                        else
                            throw new SyntaxException("A number can only have one decimal point(.).", l, col, _name);
                        break;
                    default:
                        b = false;
                        break;
                }
            }
            while (decC-- > 0) dec *= 0.1;
            val += dec;
            if (negV) dec *= -1;
            val *= Math.Pow((hex ? 2 : 10), (negE ? -exp : exp));
            if (double.IsInfinity(val))
                throw new SyntaxException("Number outside range of double.", l, col, _name);
            return val;
        }
        string ReadName(bool allow)
        {
            // allow leading whitespace
            ReadWhitespace();

            if (!__nameStartChars.Contains(_input.PeekChar() ?? '\0'))
                return "";

            StringBuilder build = new StringBuilder();
            bool b = false;
            char? c;
            while ((c = _input.PeekChar()) != null)
            {
                if (!__nameChars.Contains(c ?? '\0'))
                {
                    if (c == '`')
                    {
                        if (!allow)
                            throw new SyntaxException("Cannot use the grave(`) in this context.", _line, _input.Position - _colOff, _name);

                        if (b)
                            throw new SyntaxException("Can only have one grave(`) in a name.",
                                _line, _input.Position - _colOff, _name);
                        else
                            b = true;
                    }
                    else
                        return build.ToString();
                }
                else if (b && !"0123456789".Contains(c ?? '\0'))
                {
                    _input.ReadChar();
                    return build.ToString();
                }
                build.Append(c);
                _input.ReadChar();
            }
            return build.ToString();
        }
        string ReadString()
        {
            int i = 0;
            if (_input.PeekChar() == '\'')
                i = -1;
            else if (_input.PeekChar() == '"')
                i = -2;
            else if (_input.PeekChar() == '[')
            {
                _input.ReadChar();
                while (_input.PeekChar() == '=')
                {
                    i++;
                    _input.ReadChar();
                }
                if (_input.PeekChar() != '[')
                    throw new SyntaxException("Invalid long-string definition.", _line, _input.Position - _colOff, _name);
                if (_input.PeekChar(1) == '\n')
                    _input.ReadChar();
            }
            else
                return null;

            StringBuilder str = new StringBuilder();
            _input.ReadChar();
            while (_input.CanRead)
            {
                char? c = _input.ReadChar();
                if (c == '\'' && i == -1)
                    return str.ToString();
                else if (c == '"' && i == -2)
                    return str.ToString();
                else if (c == '\n' && i < 0)
                    throw new SyntaxException("Unfinished string literal.", _line, _input.Position - _colOff, _name);
                else if (c == ']' && i >= 0)
                {
                    int j = 0;
                    while (_input.PeekChar() == '=')
                    {
                        j++;
                        _input.ReadChar();
                    }

                    if (_input.PeekChar() != ']' || j != i)
                    {
                        str.Append(']');
                        str.Append('=', j);
                    }
                    else
                    {
                        _input.ReadChar();
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

                    c = _input.ReadChar();
                    if (c == '\'' || c == '"' || c == '\\')
                        str.Append(c);
                    else if (c == '\n')
                    {
                        str.Append('\n');
                        _line++;
                        _colOff = _input.Position;
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
                        c = _input.ReadChar();
                        if (!"0123456789ABCDEFabcdef".Contains(c.Value))
                            throw new SyntaxException("Invalid escape sequence '\\x" + c + "'", _line, _input.Position - _colOff, _name);
                        ii = int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        c = _input.ReadChar();
                        if (!"0123456789ABCDEFabcdef".Contains(c.Value))
                            throw new SyntaxException("Invalid escape sequence '\\x" + ii.ToString("x") + c + "'", _line, _input.Position - _colOff, _name);
                        ii = (ii >> 16) + int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                        str.Append((char)ii);
                    }
                    else if ("0123456789".Contains(c.Value))
                    {
                        int ii = 0;
                        if (!"0123456789".Contains(_input.PeekChar() ?? '\0'))
                            continue;
                        c = _input.ReadChar();
                        ii = int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                        if ("0123456789".Contains(_input.PeekChar() ?? '\0'))
                        {
                            c = _input.ReadChar();
                            ii = (ii * 10) + int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                            if ("0123456789".Contains(_input.PeekChar() ?? '\0'))
                            {
                                c = _input.ReadChar();
                                ii = (ii * 10) + int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                            }
                        }
                        str.Append((char)ii);
                    }
                    else
                        throw new SyntaxException("Invalid escape sequence '\\" + c + "'.", _line, _input.Position - _colOff, _name);
                }
                else
                    str.Append(c.ToString());
            }

            return str.ToString();
        }
        void ReadWhitespace()
        {
            char? c;
            while ((c = _input.PeekChar()) != null)
            {
                if (c == '-' && _input.PeekChar(1) == '-')
                {
                    _input.Read(2);
                    ReadComment();
                    continue;
                }
                else if (c == ' ' || c == '\n' || c == '\t' || c == '\r')
                {
                    if (c == '\n')
                    {
                        _line++; _colOff = _input.Position + 1;
                    }
                    _input.ReadChar();
                    continue;
                }
                break;
            }
        }
        void ReadComment()
        {
            long l = _line, c = _input.Position - _colOff - 2;
            int dep = -1;
            char? cc;
            if (_input.PeekChar() == '[')
            {
                dep = 0;
                _input.ReadChar();
                while ((cc = _input.ReadChar()) != null)
                {
                    if (cc == '=')
                        dep++;
                    else if (cc == '\n')
                    {
                        _line++;
                        _colOff = _input.Position + 1;
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
            while ((cc = _input.ReadChar()) != null)
            {
                if (dep == -1)
                {
                    if (cc == '\n')
                    {
                        _line++;
                        _colOff = _input.Position + 1;
                        return;
                    }
                }
                else
                {
                    if (cc == '\n')
                    {
                        _line++;
                        _colOff = _input.Position + 1;
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
            if (!_input.CanRead && dep != -1)
                throw new SyntaxException("Expecting end of long comment that started at:", l, c, _name);
        }
        IParseItem ReadSimpExp(string name = null)
        {
            Stack<int> ex = new Stack<int>(); // 1 - neg, 2 - not, 3 - len
            IParseItem o = null;

            if (string.IsNullOrWhiteSpace(name))
            {
                while (true)
                {
                    ReadWhitespace();
                    if (_input.PeekChar() == '-')
                    {
                        _input.ReadChar();
                        ex.Push(1);
                        continue;
                    }
                    else if (_input.PeekChar() == 'n' && _input.PeekChar(1) == 'o' && _input.PeekChar(2) == 't' &&
                        !__nameStartChars.Contains(_input.PeekChar(3) ?? '\0'))
                    {
                        _input.Read(3);
                        ex.Push(2);
                        continue;
                    }
                    else if (_input.PeekChar() == '#')
                    {
                        _input.ReadChar();
                        ex.Push(3);
                        continue;
                    }
                    break;
                }

                if (".0123456789".Contains(_input.PeekChar() ?? '\0'))
                {
                    double d = ReadNumber();
                    o = new LiteralItem(d);
                }
                else if (_input.PeekChar() == '\'' || _input.PeekChar() == '"')
                {
                    o = new LiteralItem(ReadString());
                }
                else if (_input.PeekChar() == '{')
                {
                    o = ReadTable();
                }
                else if (_input.PeekChar() == '(')
                {
                    _input.ReadChar();
                    o = ReadExp();
                    ReadWhitespace();
                    if (_input.ReadChar() != ')')
                        throw new SyntaxException("Invalid expression.", _line, _input.Position - _colOff, _name);
                }
            }

            if (o == null)
            {
                string inst = null;
                name = name ?? ReadName(true);
                if (name == "function")
                    return ReadFunc(null, null, _line, _input.Position - _colOff - 8);

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
                    if (_input.PeekChar() == '.' && _input.PeekChar(1) != '.')
                    {
                        if (inst != null)
                            throw new SyntaxException("Cannot use an indexer after an instance call.", _line, _input.Position - _colOff, _name);

                        _input.ReadChar();
                        name = ReadName(true);
                        o = new IndexerItem(o, new LiteralItem(name));
                        continue;
                    }
                    else if (_input.PeekChar() == ':')
                    {
                        _input.ReadChar();
                        inst = ReadName(true);
                        continue;
                    }
                    else if (_input.PeekChar() == '[')
                    {
                        if (inst != null)
                            throw new SyntaxException("Cannot use an indexer after an instance call.", _line, _input.Position - _colOff, _name);

                        _input.ReadChar();
                        IParseItem exp = ReadExp();
                        o = new IndexerItem(o, exp);
                        ReadWhitespace();
                        if (_input.ReadChar() != ']')
                            throw new SyntaxException("Invalid indexer.", _line, _input.Position - _colOff, _name);
                        continue;
                    }
                    else if (_input.PeekChar() == '\'' || _input.PeekChar() == '"')
                    {
                        string s = ReadString();
                        o = new FuncCallItem(o, inst);
                        inst = null;
                        o.AddItem(new LiteralItem(s));
                        continue;
                    }
                    else if (_input.PeekChar() == '{')
                    {
                        IParseItem t = ReadTable();
                        o = new FuncCallItem(o, inst);
                        inst = null;
                        o.AddItem(t);
                        continue;
                    }
                    else if (_input.PeekChar() == '(')
                    {
                        _input.ReadChar();
                        ReadWhitespace();
                        o = new FuncCallItem(o, inst);
                        inst = null;
                        while (_input.PeekChar() != ')')
                        {
                            IParseItem e = ReadExp();
                            ReadWhitespace();
                            o.AddItem(e);
                            if (_input.PeekChar() == ',')
                                _input.ReadChar();
                            else if (_input.PeekChar() == ')')
                                break;
                            else
                                throw new SyntaxException("Invalid function call.", _line, _input.Position - _colOff, _name);
                        }
                        _input.ReadChar();
                        continue;
                    }
                    break;
                }
                if (inst != null)
                    throw new SyntaxException("Invalid instance function call.", _line, _input.Position - _colOff, _name);
            }

            ReadWhitespace();
            if (_input.PeekChar() == '^')
            {
                IParseItem other = ReadSimpExp();
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
                                throw new SyntaxException("Cannot use unary minus on a string, bool, or nil.", _line, _input.Position - _colOff, _name);

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
            return o;
        }
        IParseItem ReadExp(int prec = -1, string name = null)
        {
            IParseItem cur = ReadSimpExp(name);
            BinOpItem ret = null;
            while (true)
            {
                ReadWhitespace();
                char? c = _input.PeekChar();
                switch (c)
                {
                    case 'o':
                        if (_input.PeekChar(1) != 'r' || __nameChars.Contains(_input.PeekChar(2) ?? '\0'))
                            break;
                        if (prec == -1)
                        {
                            _input.Read(2);
                            ret = new BinOpItem(ret ?? cur, BinaryOperationType.Or);
                            ret.Rhs = ReadExp(8);
                            continue;
                        }
                        break;
                    case 'a':
                        if (_input.PeekChar(1) != 'n' || _input.PeekChar(2) != 'd' || __nameChars.Contains(_input.PeekChar(3) ?? '\0'))
                            break;
                        if (prec == -1)
                        {
                            _input.Read(3);
                            ret = new BinOpItem(ret ?? cur, BinaryOperationType.And);
                            ret.Rhs = ReadExp(7);
                            continue;
                        }
                        else if (prec > 7)
                        {
                            _input.Read(3);
                            ret = new BinOpItem(cur, BinaryOperationType.And);
                            ret.Rhs = ReadExp(7);
                            continue;
                        }
                        break;
                    case '>':
                    case '<':
                    case '=':
                    case '~':
                        {
                            bool b = _input.PeekChar(1) == '=';
                            if (c == '~' && !b)
                                throw new SyntaxException("Invalid token ~ in expression.", 
                                    _line, _input.Position - _colOff, _name);
                            if (c == '=' && !b)
                                break;
                            var o = c == '>' ? b ? BinaryOperationType.Gte : BinaryOperationType.Gt :
                                c == '<' ? b ? BinaryOperationType.Lte : BinaryOperationType.Lt :
                                c == '=' ? BinaryOperationType.Equals : BinaryOperationType.NotEquals;
                            if (prec == -1)
                            {
                                _input.Read(b ? 2 : 1);
                                ret = new BinOpItem(ret ?? cur, o);
                                ret.Rhs = ReadExp(6);
                                continue;
                            }
                            else if (prec > 6)
                            {
                                _input.Read(b ? 2 : 1);
                                ret = new BinOpItem(cur, o);
                                ret.Rhs = ReadExp(6);
                                continue;
                            }
                        }
                        break;
                    case '.':
                        if (_input.PeekChar(1) != '.')
                            break;
                        if (prec == -1)
                        {
                            _input.Read(2);
                            ret = new BinOpItem(ret ?? cur, BinaryOperationType.Concat);
                            ret.Rhs = ReadExp(5);
                            continue;
                        }
                        else if (prec >= 5)
                        {
                            _input.Read(2);
                            ret = new BinOpItem(cur, BinaryOperationType.Concat);
                            ret.Rhs = ReadExp(5);
                            continue;
                        }
                        break;
                    case '+':
                    case '-':
                        if (prec == -1)
                        {
                            _input.ReadChar();
                            ret = new BinOpItem(ret ?? cur, c == '+' ? BinaryOperationType.Add : BinaryOperationType.Subtract);
                            ret.Rhs = ReadExp(4);
                            continue;
                        }
                        else if (prec > 4)
                        {
                            _input.ReadChar();
                            ret = new BinOpItem(cur, c == '+' ? BinaryOperationType.Add : BinaryOperationType.Subtract);
                            ret.Rhs = ReadExp(4);
                            continue;
                        }
                        break;
                    case '*':
                    case '/':
                    case '%':
                        if (prec == -1)
                        {
                            _input.ReadChar();
                            ret = new BinOpItem(ret ?? cur, 
                                c == '*' ? BinaryOperationType.Multiply : c == '%' ? BinaryOperationType.Modulo : BinaryOperationType.Divide);
                            ret.Rhs = ReadExp(3);
                            continue;
                        }
                        else if (prec > 3)
                        {
                            _input.ReadChar();
                            ret = new BinOpItem(cur,
                                c == '*' ? BinaryOperationType.Multiply : c == '%' ? BinaryOperationType.Modulo : BinaryOperationType.Divide);
                            ret.Rhs = ReadExp(3);
                            continue;
                        }
                        break;
                }
                break;
            }

            return ret ?? cur;
        }
    }
}