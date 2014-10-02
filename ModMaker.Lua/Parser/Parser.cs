using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.IO;
using System.Reflection;
using ModMaker.Lua.Runtime;
using System.Security.Cryptography;
using System.Reflection.Emit;

namespace ModMaker.Lua.Parser
{
    class PlainParser
    {
        static string[] __reserved = new[] { "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "goto", "if", 
                                 "in", "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while", "class" };
        static Dictionary<string, LuaChunk> _loaded = new Dictionary<string, LuaChunk>();
        
        /// <summary>
        /// This is the tokenizer for the parser.  This reads from the TextReader
        /// and returns the next token.  This automatically ignores whitespace
        /// and comments.
        /// </summary>
        class Tokenizer
        {
            TextElementEnumerator reader;
            string name, peek;
            long pos, line;

            public Tokenizer(TextElementEnumerator reader, string name)
            {
                this.reader = reader;
                this.name = name;
                this.pos = 0;
                this.line = 1;
                reader.MoveNext(); // start the enumerator
            }

            public long Position { get { return pos; } }
            public long Line { get { return line; } }
        
            /// <summary>
            /// Reads a token from the TextReader.
            /// </summary>
            /// <returns>The string token or null on EOF.</returns>
            public string Read()
            {
                if (peek != null)
                {
                    string ret = peek;
                    peek = null;
                    return ret;
                }
                else
                    return InternalRead();
            }
            public string Peek()
            {
                if (peek == null)
                    peek = InternalRead();

                return peek;
            }

            string InternalRead()
            {
            start:
                ReadWhitespace(reader, name, ref pos, ref line);
                string last = Read(reader, ref pos, ref line);
                if (last == null)
                    return null;

                // goto start if it is a comment
                if (last == "-")
                {
                    last = Peek(reader);
                    if (last == "-")
                    {
                        Read(reader, ref pos, ref line); // read '-'
                        ReadComment(reader, name, ref pos, ref line);
                        goto start;
                    }
                    else
                        return "-";
                }

                // read an identifier (e.g. 'function' or '_x')
                if (char.IsLetter(last, 0) || last == "_")
                {
                    StringBuilder ret = new StringBuilder();
                    long _pos = pos;
                    bool over = false;
                    ret.Append(last);
                    last = Peek(reader);
                    while (last != null && (char.IsLetterOrDigit(last, 0) || last == "_" || last == "`"))
                    {
                        if (over)
                        {
                            if (last == "`")
                                throw new SyntaxException("Can only specify one grave(`) in an overload.", line, _pos, name);
                            if (!char.IsDigit(last, 0))
                                throw new SyntaxException("An overload can only have numbers.", line, _pos, name);
                        }
                        else if (last == "`")
                        {
                            over = true;
                        }
                        Read(reader, ref pos, ref line);
                        ret.Append(last);
                        last = Peek(reader);
                    }

                    return ret.ToString();
                }

                // read indexer, concat, and ...
                if (last == ".")
                {
                    if (Peek(reader) == ".")
                    {
                        Read(reader, ref pos, ref line); // read "."
                        if (Peek(reader) == ".")
                        {
                            Read(reader, ref pos, ref line); // read "."
                            return "...";
                        }
                        else
                            return "..";
                    }
                }

                // read a number
                if (char.IsNumber(last, 0) || last == CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)
                {
                    // return ReadNumber(reader, name, ref pos, ref line).ToString(CultureInfo.InvariantCulture);
                    //
                    // this version does nothing to check for a valid number, that is done in the parser.
                    //   this only supports 0xNNN notation for hexadecimal numbers (where NNN is a char.IsNumber char).
                    StringBuilder ret = new StringBuilder();
                    CultureInfo ci = CultureInfo.CurrentCulture;
                    string l = last;
                    bool hex = false;
                    if (last == "0" && Peek(reader).ToLower(CultureInfo.InvariantCulture) == "x")
                    {
                        hex = true;
                        ret.Append("&");
                        Read(reader, ref pos, ref line); // read the 'x'
                        last = Peek(reader);
                    }
                    else
                    {
                        ret.Append(last);
                        last = Peek(reader);
                    }

                    while (char.IsNumber(last, 0) || (!hex && (last == ci.NumberFormat.NumberDecimalSeparator || (l != "." && last == "e"))))
                    {
                        Read(reader, ref pos, ref line);
                        ret.Append(last);
                        l = last;
                        last = Peek(reader);
                    }

                    // ensure that aa single decimal point can also be '..' or '...' (see below).
                    if (ret.ToString() != ".")
                        return ret.ToString();
                    else
                        last = ".";
                }

                // read a literal string
                if (last == "\"" || last == "'")
                {
                    return ReadString(reader, last == "'" ? -1 : -2, name, ref pos, ref line);
                }

                // handle "["
                if (last == "[")
                {
                    last = Peek(reader);
                    if (last == "[" || last == "=")
                    {
                        int dep = 0;
                        while (last == "=")
                        {
                            Read(reader, ref pos, ref line); // read "="
                            dep++;
                            last = Peek(reader);
                        }
                        if (last != "[")
                            throw new SyntaxException("Invalid long string definition.", line, pos, name);

                        Read(reader, ref pos, ref line); // read "["
                        return ReadString(reader, dep, name, ref pos, ref line);
                    }
                    else
                        return "[";
                }

                // read ::
                if (last == ":")
                {
                    if (Peek(reader) == ":")
                    {
                        Read(reader, ref pos, ref line); // read ":"
                        return "::";
                    }
                    else
                        return ":";
                }

                // read comparison operatos
                if (last == ">" || last == "<" || last == "~" || last == "=")
                {
                    if (Peek(reader) == "=")
                    {
                        last += "=";
                        Read(reader, ref pos, ref line); // read "="
                    }
                    else if (last == "~")
                        throw new SyntaxException("Invalid token '~'.", line, pos, name);

                    return last;
                }

                // otherwise simply return the read text-element
                return last;
            }
            static double ReadNumber(TextElementEnumerator input, string name, ref long pos, ref long line)
            {
                bool hex = false;
                double val = 0, exp = 0, dec = 0;
                int decC = 0;
                bool negV = false, negE = false;
                if (Peek(input) == "-")
                {
                    negV = true;
                    Read(input, ref pos, ref line); // read "-"
                }
                if (Peek(input) == "0" && (Read(input, ref pos, ref line) != null && (Peek(input) == "x" || Peek(input) == "X")))
                {
                    hex = true;
                    Read(input, ref pos, ref line);
                }

                bool b = true;
                int stat = 0; // 0-val, 1-dec, 2-exp
                string c;
                while (b && Peek(input) != null)
                {
                    switch (c = Peek(input).ToLower(CultureInfo.InvariantCulture))
                    {
                        case "0":
                        case "1":
                        case "2":
                        case "3":
                        case "4":
                        case "5":
                        case "6":
                        case "7":
                        case "8":
                        case "9":
                            Read(input, ref pos, ref line);
                            if (stat == 0)
                            {
                                val *= (hex ? 16 : 10);
                                val += int.Parse(c, CultureInfo.InvariantCulture);
                            }
                            else if (stat == 1)
                            {
                                dec *= (hex ? 16 : 10);
                                dec += int.Parse(c, CultureInfo.InvariantCulture);
                                decC++;
                            }
                            else
                            {
                                exp *= (hex ? 16 : 10);
                                exp += int.Parse(c, CultureInfo.InvariantCulture);
                            }
                            break;
                        case "a":
                        case "b":
                        case "c":
                        case "d":
                        case "f":
                            Read(input, ref pos, ref line);
                            if (!hex)
                            {
                                b = false; break;
                            }
                            if (stat == 0)
                            {
                                val *= 16;
                                val += int.Parse(c, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                            }
                            else if (stat == 1)
                            {
                                dec *= 16;
                                dec += int.Parse(c, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                decC++;
                            }
                            else
                            {
                                exp *= 16;
                                exp += int.Parse(c, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                            }
                            break;
                        case "e":
                        case "p":
                            Read(input, ref pos, ref line);
                            if ((hex && c == "p") || (!hex && c == "e"))
                            {
                                if (stat == 2)
                                    throw new SyntaxException("Can only have exponent designator('e' or 'p') per number.", line, pos, name);
                                stat = 2;

                                if (Peek(input) != null)
                                    throw new SyntaxException("Must specify at least one number for the exponent.", line, pos, name);
                                if (Peek(input) == "+" || (Peek(input) == "-" && (negE = true == true)))
                                {
                                    Read(input, ref pos, ref line);
                                    if (Peek(input) == null)
                                        throw new SyntaxException("Must specify at least one number for the exponent.", line, pos, name);
                                }

                                if ("0123456789".Contains(Peek(input)))
                                {
                                    exp = int.Parse(Read(input, ref pos, ref line), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                    break;
                                }
                                else if (hex && "abcdefABCDEF".Contains(Peek(input)))
                                {
                                    exp = int.Parse(Read(input, ref pos, ref line), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                    break;
                                }
                                throw new SyntaxException("Must specify at least one number for the exponent.", line, pos, name);
                            }
                            else if (hex && c == "e")
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
                        case ".":
                            Read(input, ref line, ref pos);
                            if (stat == 0)
                                stat = 1;
                            else
                                throw new SyntaxException("A number can only have one decimal point(.).", line, pos, name);
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
                    throw new SyntaxException("Number outside range of double.", line, pos, name);
                return val;
            }
            /// <summary>
            /// Reads a text-element fro the TextReader, automatically increases
            /// position and handles newlines.
            /// </summary>
            /// <param name="reader">The TextReader to read from.</param>
            /// <param name="pos">Contains the current position in the file.</param>
            /// <param name="line">Contains the line number in the file.</param>
            /// <returns>The next text element or null on EOF.</returns>
            static string Read(TextElementEnumerator reader, ref long pos, ref long line)
            {
                string ret = null;
                try
                {
                    ret = reader.GetTextElement();
                    reader.MoveNext();
                }
                catch (Exception) { }

                if (ret == null)
                    return null;

                pos += ret.Length;
                if (ret == "\r" || ret == "\n")
                {
                    if (ret == "\r")
                    {
                        string temp = Peek(reader);
                        if (temp == "\n")
                            reader.MoveNext();
                        ret = "\n";
                    }
                    line++;
                    pos = 0;
                }

                return ret;
            }
            static void ReadComment(TextElementEnumerator reader, string name, ref long pos, ref long line)
            {
                long l = line, c = pos - 2;
                int dep = -1;
                string cc;
                if (reader.GetTextElement() == "[")
                {
                    dep = 0;
                    Read(reader, ref pos, ref line);
                    while ((cc = Read(reader, ref pos, ref line)) != null)
                    {
                        if (cc == "=")
                            dep++;
                        else if (cc == "\n")
                            return;
                        else
                        {
                            if (cc != "[")
                                dep = -1;
                            break;
                        }
                    }
                }

                int cdep = -1;
                while ((cc = Read(reader, ref pos, ref line)) != null)
                {
                    if (dep == -1)
                    {
                        if (cc == "\n")
                            return;
                    }
                    else
                    {
                        if (cc == "\n")
                            cdep = -1;
                        else if (cdep != -1)
                        {
                            if (cc == "]")
                            {
                                if (cdep == dep)
                                    return;
                                else
                                    cdep = -1;
                            }
                            else if (cc != "=")
                                cdep = -1;
                            else
                                cdep++;
                        }
                        else if (cc == "]")
                        {
                            cdep = 0;
                        }
                    }
                }

                if (Peek(reader) == null && dep != -1)
                    throw new SyntaxException("Expecting end of long comment that started at:", l, c, name);
            }
            static void ReadWhitespace(TextElementEnumerator reader, string name, ref long pos, ref long line)
            {
                while (Peek(reader) != null && char.IsWhiteSpace(Peek(reader), 0) && Read(reader, ref pos, ref line) != null)
                    ;
            }
            static string ReadString(TextElementEnumerator reader, int dep, string name, ref long pos, ref long line)
            {
                StringBuilder str = new StringBuilder();
                str.Append("\"");
                while (Peek(reader) != null)
                {
                    string c = Read(reader, ref pos, ref line);
                    if (c == "'" && dep == -1)
                        return str.ToString();
                    else if (c == "\"" && dep == -2)
                        return str.ToString();
                    else if (c == "\n" && dep < 0)
                        throw new SyntaxException("Unfinished string literal.", line, pos, name);
                    else if (c == "]" && dep >= 0)
                    {
                        int j = 0;
                        while (Peek(reader) == "=")
                        {
                            j++;
                            Read(reader, ref pos, ref line);
                        }

                        if (Peek(reader) != "]" || j != dep)
                        {
                            str.Append(']');
                            str.Append('=', j);
                        }
                        else
                        {
                            Read(reader, ref pos, ref line);
                            return str.ToString();
                        }
                    }
                    else if (c == "\\")
                    {
                        if (dep >= 0)
                        {
                            str.Append("\\");
                            continue;
                        }

                        c = Read(reader, ref pos, ref line);
                        if (c == "\'" || c == "\"" || c == "\\")
                            str.Append(c);
                        else if (c == "\n")
                        {
                            str.Append("\n");
                        }
                        else if (c == "z")
                            ReadWhitespace(reader, name, ref pos, ref line);
                        else if (c == "n")
                            str.Append('\n');
                        else if (c == "a")
                            str.Append('\a');
                        else if (c == "b")
                            str.Append('\b');
                        else if (c == "f")
                            str.Append('\f');
                        else if (c == "r")
                            str.Append('\r');
                        else if (c == "t")
                            str.Append('\t');
                        else if (c == "v")
                            str.Append('\v');
                        else if (c == "x")
                        {
                            int ii = 0;
                            c = Read(reader, ref pos, ref line);
                            if (!"0123456789ABCDEFabcdef".Contains(c))
                                throw new SyntaxException("Invalid escape sequence '\\x" + c + "'", line, pos, name);
                            ii = int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                            c = Read(reader, ref pos, ref line);
                            if (!"0123456789ABCDEFabcdef".Contains(c))
                                throw new SyntaxException("Invalid escape sequence '\\x" + ii.ToString("x") + c + "'", line, pos, name);
                            ii = (ii >> 16) + int.Parse(c.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                            str.Append((char)ii);
                        }
                        else if ("0123456789".Contains(c))
                        {
                            int ii = 0;
                            if (!"0123456789".Contains(Peek(reader)))
                                continue;
                            c = Read(reader, ref pos, ref line);
                            ii = int.Parse(c, CultureInfo.InvariantCulture);
                            if ("0123456789".Contains(Peek(reader)))
                            {
                                c = Read(reader, ref pos, ref line);
                                ii = (ii * 10) + int.Parse(c, CultureInfo.InvariantCulture);
                                if ("0123456789".Contains(Peek(reader)))
                                {
                                    c = Read(reader, ref pos, ref line);
                                    ii = (ii * 10) + int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                                }
                            }
                            str.Append((char)ii);
                        }
                        else
                            throw new SyntaxException("Invalid escape sequence '\\" + c + "'.", line, pos, name);
                    }
                    else
                        str.Append(c);
                }

                return str.ToString();
            }
            static string Peek(TextElementEnumerator reader)
            {
                try
                {
                    return reader.GetTextElement();
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        Tokenizer _reader;
        string _name;
        bool _glt = false;

        long _pos { get { return _reader.Position; } }
        long _line { get { return _reader.Line; } }

        PlainParser(TextElementEnumerator reader, string name)
        {
            this._reader = new Tokenizer(reader, name);
            this._name = name;
        }

        /// <summary>
        /// Loads a chunk using the given text reader.
        /// </summary>
        /// <param name="E">The current environment to use.</param>
        /// <param name="dump">The Lua code to parse.</param>
        /// <param name="name">The name of the chunk.</param>
        /// <param name="force">False to try to load from the cache, true to load even if it is cached.</param>
        /// <returns>The loaded chunk.</returns>
        public static LuaChunk LoadChunk(LuaEnvironment E, string dump, string name = null, bool force = false)
        {
            if (dump == null)
                throw new ArgumentNullException("dump");

            // dump the text reader to a string so we can use a
            //  TextElementEnumerator.  This removes support for
            //  incredibly large files; however it would be likely
            //  that we could not store the DynamicAssembly or
            //  the IParseItem tree of a file that size.
            byte[] hash = SHA512.Create().ComputeHash(Encoding.Unicode.GetBytes(dump));
            string shash = hash.ToStringBase16().ToUpper(CultureInfo.InvariantCulture);
            if (!force)
            {
                lock (_loaded)
                    if (_loaded.ContainsKey(shash))
                        return _loaded[shash].Clone(E);
            }

            // load the file and store it in the cache
            LuaChunk load = new PlainParser(StringInfo.GetTextElementEnumerator(dump), name).Load(E);
            lock (_loaded)
                _loaded[shash] = load;

            return load;
        }

        LuaChunk Load(LuaEnvironment E)
        {
            IParseItem chunk = ReadChunk();
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
            return cb.CreateChunk(E);
        }

        #region Read Function
        IParseItem ReadChunk(IParseItem parrent = null)
        {
            BlockItem ret = new BlockItem();
            string name = null;

            while (_reader.Peek() != null)
            {
                switch (name = _reader.Peek())
                {
                    #region case ";"
                    case ";":
                        _reader.Read(); // read ';'
                        continue;
                    #endregion
                    #region case "::"
                    case "::":
                        {
                            _reader.Read(); // read '::'
                            string label = _reader.Read();
                            name = _reader.Read();
                            if (name != "::")
                                throw new SyntaxException("Invalid label definition.", _line, _pos, _name);

                            ret.AddItem(new LabelItem(label));
                        }
                        break;
                    #endregion
                    #region case "break"
                    case "break":
                        _reader.Read(); // read 'break'
                        ret.AddItem(new GotoItem("<break>", _line, _pos - 5));
                        _glt = true;
                        break;
                    #endregion
                    #region case "goto"
                    case "goto":
                        {
                            _reader.Read(); // read 'goto'
                            name = _reader.Read();
                            if (!IsName(name))
                                throw new SyntaxException("Must specify a name for the target of a goto statement.", _line, _pos - name.Length, _name);
                            if (__reserved.Contains(name))
                                throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - name.Length, _name);
                            ret.AddItem(new GotoItem(name, _line, _pos - name.Length));
                            _glt = true;
                            break;
                        }
                    #endregion
                    #region case "do"
                    case "do":
                        _reader.Read(); // read 'do'
                        ret.AddItem(ReadChunk(ret));
                        if (_reader.Read() != "end")
                            throw new SyntaxException("Expecting 'end' at end of local block.",
                                _line, _pos, _name);
                        break;
                    #endregion
                    #region case "end"
                    case "end":
                        // don't read as it will be handled by the parrent
                        if (parrent == null)
                            throw new SyntaxException("Invalid token 'end' in global chunk.", _line, _pos - 3, _name);
                        return ret;
                    #endregion
                    #region case "else/elseif"
                    case "else":
                    case "elseif":
                        // don't read as it will be handled by the parrent
                        if (parrent == null)
                            throw new SyntaxException("Invalid token '" + name + "' in global chunk.", _line, _pos - name.Length, _name);
                        else if (!(parrent is IfItem))
                            throw new SyntaxException("'" + name + "' is only valid in an if block.", _line, _pos - name.Length, _name);

                        return ret;
                    #endregion
                    #region case "until"
                    case "until":
                        // don't read as it will be handled by the parrent
                        if (parrent == null)
                            throw new SyntaxException("Invalid token 'until' in global chunk.", _line, _pos - 5, _name);
                        else if (!(parrent is RepeatItem))
                            throw new SyntaxException("'until' is only valid in an repeat block.", _line, _pos - 5, _name);

                        return ret;
                    #endregion
                    #region case "while"
                    case "while":
                        {
                            _reader.Read(); // read 'while'
                            WhileItem w = new WhileItem();
                            w.Exp = ReadExp();
                            name = _reader.Read();
                            if (name != "do")
                                throw new SyntaxException("Invalid token '" + name + "' in while definition.", _line, _pos - name.Length, _name);
                            w.Block = ReadChunk(w);
                            name = _reader.Read();
                            if (name != "end")
                                throw new SyntaxException("Invalid token '" + name + "' in while definition.", _line, _pos - name.Length, _name);
                            ret.AddItem(w);
                            break;
                        }
                    #endregion
                    #region case "repeat"
                    case "repeat":
                        {
                            _reader.Read(); // read 'repeat'
                            RepeatItem r = new RepeatItem();
                            r.Block = ReadChunk(r);
                            name = _reader.Read();
                            if (name != "until")
                                throw new SyntaxException("Invalid token '" + name + "' in repeat definition.", _line, _pos - name.Length, _name);
                            r.Exp = ReadExp();
                            ret.AddItem(r);
                            break;
                        }
                    #endregion
                    #region case "if"
                    case "if":
                        {
                            _reader.Read(); // read 'if'
                            IfItem i = new IfItem();
                            i.Exp = ReadExp();
                            name = _reader.Read();
                            if (name != "then")
                                throw new SyntaxException("Invalid token '" + name + "' in if definition.", _line, _pos - name.Length, _name);
                            i.Block = ReadChunk(i);

                            // handle elseif(s)
                            while ((name = _reader.Peek()) == "elseif")
                            {
                                _reader.Read(); // read 'elseif'
                                IParseItem e = ReadExp();
                                name = _reader.Read();
                                if (name != "then")
                                    throw new SyntaxException("Invalid token '" + name + "' in elseif definition.", _line, _pos - name.Length, _name);
                                i.AddItem(e, ReadChunk(i));
                            }

                            // handle else
                            if (name != "else" && name != "end")
                                throw new SyntaxException("Invalid token '" + name + "' in if definition.", _line, _pos - name.Length, _name);
                            if (name == "else")
                            {
                                _reader.Read(); // read 'else'
                                i.ElseBlock = ReadChunk(i);
                            }
                            if ((name = _reader.Read()) != "end")
                                throw new SyntaxException("Invalid token '" + name + "' in if definition.", _line, _pos - name.Length, _name);
                            ret.AddItem(i);
                            break;
                        }
                    #endregion
                    #region case "for"
                    case "for":
                        {
                            _reader.Read(); // read 'for'
                            name = _reader.Read();
                            if (!IsName(name))
                                throw new SyntaxException("Invalid token in 'for' definition, '" + name + "' is not a name.", 
                                    _line, _pos - name.Length, _name);
                            if (__reserved.Contains(name))
                                throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - name.Length, _name);

                            // numeric for
                            if (_reader.Peek() == "=")
                            {
                                _reader.Read(); // read "="
                                ForNumItem i = new ForNumItem(name);

                                // get the 'start' value
                                i.Start = ReadExp();
                                if ((name = _reader.Read()) != ",")
                                    throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _pos - name.Length, _name);

                                // get the 'limit'
                                i.Limit = ReadExp();
                                if (_reader.Peek() == ",")
                                {
                                    _reader.Read(); // read ","
                                    i.Step = ReadExp();
                                }

                                // check for 'do'
                                name = _reader.Read();
                                if (name != "do")
                                    throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _pos - name.Length, _name);

                                // read the chunk
                                i.Block =  ReadChunk(i);
                                if ((name = _reader.Read()) != "end")
                                    throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _pos - name.Length, _name);
                                ret.AddItem(i);
                            }
                            // generic for statement
                            else
                            {
                                // read the variables
                                List<string> names = new List<string>();
                                names.Add(name);
                                while (_reader.Peek() == ",")
                                {
                                    _reader.Read(); // read ","
                                    name = _reader.Read();
                                    if (!IsName(name))
                                        throw new SyntaxException("Invalid token in 'for' definition, '" + name + "' is not a name.",
                                            _line, _pos - name.Length, _name);
                                    if (__reserved.Contains(name))
                                        throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - name.Length, _name);
                                    names.Add(name);
                                }

                                // check for 'in'
                                name = _reader.Read();
                                if (name != "in")
                                    throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _pos - name.Length, _name);

                                // read the expression-list
                                ForGenItem f = new ForGenItem(names);
                                f.AddItem(ReadExp());
                                while (_reader.Peek() == ",")
                                {
                                    _reader.Read(); // read ","
                                    f.AddItem(ReadExp());
                                }

                                // check for 'do'
                                name = _reader.Read();
                                if (name != "do")
                                    throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _pos - name.Length, _name);
                                
                                // read the chunk
                                f.Block = ReadChunk(f);
                                if ((name = _reader.Read()) != "end")
                                    throw new SyntaxException("Invalid token '" + name + "' in for definition.", _line, _pos - name.Length, _name);
                                ret.AddItem(f);
                            }
                            break;
                        }
                    #endregion
                    #region case "function"
                    case "function":
                        {
                            ret.AddItem(ReadFunction(true, false));
                            break;
                        }
                    #endregion
                    #region case "local"
                    case "local":
                        {
                            long l = _line, cc = _pos;
                            _reader.Read(); // read 'local'
                            name = _reader.Read();
                            if (name == "function")
                            {
                                ret.AddItem(ReadFunction(true, true));
                            }
                            else
                            {
                                if (!IsName(name))
                                    throw new SyntaxException("Invalid token in 'local' definition, '" + name + "' is not a name.",
                                        _line, _pos - name.Length, _name);
                                if (__reserved.Contains(name))
                                    throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - name.Length, _name);

                                // read each of the variable names
                                VarInitItem i = new VarInitItem(true);
                                i.AddName(new NameItem(name));
                                while (_reader.Peek() == ",")
                                {
                                    _reader.Read(); // read ','
                                    name = _reader.Read();

                                    if (!IsName(name))
                                        throw new SyntaxException("Invalid token in 'local' definition, '" + name + "' is not a name.",
                                            _line, _pos - name.Length, _name);
                                    if (__reserved.Contains(name))
                                        throw new SyntaxException("Invalid name, '" + name + "' is reserved.", _line, _pos - name.Length, _name);
                                    i.AddName(new NameItem(name));
                                }

                                // read the initial values
                                if (_reader.Peek() == "=")
                                {
                                    _reader.Read(); // read '='
                                    i.AddItem(ReadExp());
                                    while (_reader.Peek() == ",")
                                    {
                                        _reader.Read(); // read ','
                                        i.AddItem(ReadExp());
                                    }
                                }
                                ret.AddItem(i);
                            }
                            break;
                        }
                    #endregion
                    #region case "class"
                    case "class":
                        {
                            string sname = null;
                            List<string> imp = new List<string>();
                            long l = _line, cc = _pos;
                            _reader.Read(); // read 'class'
                            if (_reader.Peek().StartsWith("'", StringComparison.InvariantCulture) || 
                                _reader.Peek().StartsWith("\"", StringComparison.InvariantCulture))
                            {
                                sname = _reader.Read().Substring(1);
                                if (_reader.Peek() == "(")
                                {
                                    _reader.Read(); // read '('
                                    while (_reader.Peek() != ")")
                                    {
                                        name = _reader.Read();
                                        if (!IsName(name))
                                            throw new SyntaxException("Invalid token in 'class' definition, '" + name + "' is not a name.",
                                                l, cc, _name);
                                        imp.Add(name);
                                        if ((name = _reader.Read()) != ",")
                                            throw new SyntaxException("Invalid token in 'class' definition '" + name + "'.", l, cc, _name);
                                    }
                                    _reader.Read(); // read ')'
                                }
                            }
                            else
                            {
                                sname = _reader.Read();
                                if (!IsName(sname))
                                    throw new SyntaxException("Invalid token in 'class' definition, '" + name + "' is not a name.",
                                        l, cc, _name);
                                if (_reader.Peek() == ":")
                                {
                                    do
                                    {
                                        // simply include the '.' in the name.
                                        string n = "";
                                        do
                                        {
                                            _reader.Read(); // read ':' or ','
                                            n += (n == "" ? "" : ".") + _reader.Read();
                                        } while (_reader.Peek() == ".");

                                        imp.Add(n);
                                    } while (_reader.Peek() == ",");
                                }
                            }
                            ret.AddItem(new ClassDefItem(sname, imp.ToArray(), l, cc));
                            break;
                        }
                    #endregion
                    #region case "return"
                    case "return":
                        {
                            _reader.Read(); // read 'return'
                            IParseItem r = new ReturnItem();
                            name = _reader.Peek();
                            if (name != "end" && name != "until" && name != "elseif" && name != "else")
                            {
                                r.AddItem(ReadExp());
                                while (_reader.Peek() == ",")
                                {
                                    _reader.Read(); // read ','
                                    r.AddItem(ReadExp());
                                }

                                if (_reader.Peek() == ";")
                                {
                                    _reader.Read(); // read ';'
                                }

                                // look at the next token for validation but keep it in the
                                //  reader for the parrent.
                                name = _reader.Peek();
                                if (name != "end" && name != "until" && name != "elseif" && name != "else" && !string.IsNullOrWhiteSpace(name))
                                    throw new SyntaxException("The return statement must be the last statement in a block.",
                                        _line, _pos - name.Length, _name);
                            }
                            ret.AddItem(r);
                            return ret;
                        }
                    #endregion
                    #region default
                    default:
                        {
                            long l = _line, cc = _pos;
                            string tname = name;
                            IParseItem exp = ReadExp();
                            if (exp is FuncCallItem)
                            {
                                (exp as FuncCallItem).Statement = true;
                                ret.AddItem(exp);
                            }
                            else if (exp is LiteralItem)
                            {
                                throw new SyntaxException("A literal is not a variable.", l, cc, _name);
                            }
                            else if (exp is NameItem || exp is IndexerItem)
                            {
                                VarInitItem i = new VarInitItem(false);
                                i.AddName(exp);
                                while (_reader.Peek() != "=")
                                {
                                    if ((name = _reader.Read()) != ",")
                                        throw new SyntaxException("Invalid toke '" + name + "' in variable definitions.",
                                            l, cc, _name);
                                    exp = ReadExp();
                                    if (!(exp is NameItem))
                                        throw new SyntaxException("Must give a name for a variable definiton", l, cc, _name);
                                    i.AddName(exp);
                                }

                                do
                                {
                                    _reader.Read(); // read '=' or ','
                                    i.AddItem(ReadExp());
                                } while (_reader.Peek() == ",");
                                ret.AddItem(i);
                            }
                            else
                                throw new SyntaxException("Invalid token '" + tname + "', expecting start of statement.", l, cc, _name);
                            break;
                        }
                    #endregion
                } // end Switch
            } // end While

            ret.AddItem(new ReturnItem());
            return ret;
        }

        /// <summary>
        /// Reads a simple expression, this is close to the prefixexp
        /// in the Lua Specification.  This will read a literal followed
        /// by any number of function calls or indexers followed by any
        /// number of exponent operations (e.g. -2 ^ 34 or abs(34, 1)).
        /// </summary>
        /// <returns>The expression that was read.</returns>
        IParseItem ReadSimpExp()
        {
            Stack<int> ex = new Stack<int>(); // 1 - neg, 2 - not, 3 - len
            IParseItem o = null;

            // check for unary operators
            {
            start:
                if (_reader.Peek() == "-")
                {
                    _reader.Read();
                    ex.Push(1);
                    goto start;
                }
                else if (_reader.Peek() == "not")
                {
                    _reader.Read();
                    ex.Push(2);
                    goto start;
                }
                else if (_reader.Peek() == "#")
                {
                    _reader.Read();
                    ex.Push(3);
                    goto start;
                }
            }

            // check for literals
            string last = _reader.Peek();
            NumberFormatInfo ni = CultureInfo.CurrentCulture.NumberFormat;
            if (char.IsNumber(last, 0) || last.StartsWith(ni.NumberDecimalSeparator, StringComparison.CurrentCulture))
            {
                _reader.Read();
                try
                {
                    o = new LiteralItem(double.Parse(last, CultureInfo.CurrentCulture));
                }
                catch (FormatException e)
                {
                    throw new SyntaxException("Incorrect number format.", _line, _pos, _name, e);
                }
            }
            else if (last.StartsWith("&", StringComparison.InvariantCulture))
            {
                _reader.Read();
                try
                {
                    o = new LiteralItem(Convert.ToDouble(int.Parse(last.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.CurrentCulture)));
                }
                catch (FormatException e)
                {
                    throw new SyntaxException("Incorrect number format.", _line, _pos, _name, e);
                }
            }
            else if (last.StartsWith("\"", StringComparison.InvariantCulture))
            {
                _reader.Read();
                o = new LiteralItem(last.Substring(1));
            }
            else if (last.StartsWith("{", StringComparison.InvariantCulture))
            {
                o = ReadTable();
            }
            else if (last == "(")
            {
                _reader.Read();
                o = ReadExp();
                if (_reader.Read() != ")")
                    throw new SyntaxException("Invalid expression.", _line, _pos, _name);
            }
            else if (last == "true")
            {
                _reader.Read();
                o = new LiteralItem(true);
            }
            else if (last == "false")
            {
                _reader.Read();
                o = new LiteralItem(false);
            }
            else if (last == "nil")
            {
                _reader.Read();
                o = new LiteralItem(null);
            }
            else if (last == "function")
            {
                o = ReadFunction(false, false);
            }
            else
            {
                _reader.Read();
                o = new NameItem(last);
            }

            // read function calls and indexers
            {
                string inst = null;
            start:
                last = _reader.Peek() ?? "";
                if (last == ".")
                {
                    _reader.Read();
                    if (inst != null)
                        throw new SyntaxException("Cannot use an indexer after an instance call.", _line, _pos, _name);
                    last = _reader.Read();
                    if (!IsName(last))
                        throw new SyntaxException("Must specify a name for an indexer.", _line, _pos, _name);
                    o = new IndexerItem(o, new LiteralItem(last));
                    goto start;
                }
                else if (last == ":")
                {
                    _reader.Read();
                    if (inst != null)
                        throw new SyntaxException("Can only specify one instance call.", _line, _pos, _name);
                    inst = _reader.Read();
                    if (!IsName(inst))
                        throw new SyntaxException("Must specify a name for an instance call.", _line, _pos, _name);
                    goto start;
                }
                else if (last == "[")
                {
                    _reader.Read();
                    if (inst != null)
                        throw new SyntaxException("Cannot use an indexer after an instance call.", _line, _pos, _name);
                    IParseItem temp = ReadExp();
                    o = new IndexerItem(o, temp);
                    last = _reader.Read();
                    if (last != "]")
                        throw new SyntaxException("Invalid token '" + last + "' in indexer definition.", _line, _pos, _name);
                    goto start;
                }
                else if (last.StartsWith("\"", StringComparison.InvariantCulture))
                {
                    _reader.Read();
                    o = new FuncCallItem(o, inst);
                    o.AddItem(new LiteralItem(last.Substring(1)));
                    inst = null;
                    goto start;
                }
                else if (last == "{")
                {
                    IParseItem temp = ReadTable();
                    o = new FuncCallItem(o, inst);
                    o.AddItem(temp);
                    inst = null;
                    goto start;
                }
                else if (last == "(")
                {
                    _reader.Read();
                    o = new FuncCallItem(o, inst);
                    inst = null;
                    while (_reader.Peek() != ")" && _reader.Peek() != null)
                    {
                        IParseItem temp = ReadExp();
                        if (temp == null)
                            throw new SyntaxException("Invalid function call.", _line, _pos, _name);
                        o.AddItem(temp);

                        if (_reader.Peek() == ",")
                            _reader.Read();
                        else if (_reader.Peek() == ")")
                            break;
                        else
                            throw new SyntaxException("Invalid function call.", _line, _pos, _name);
                    }
                    if (_reader.Peek() == null)
                        throw new SyntaxException("Unexpected end of file in function call.", _line, _pos, _name);
                    _reader.Read();
                    goto start;
                }
                if (inst != null)
                    throw new SyntaxException("Invalid instance function call.", _line, _pos, _name);
            }
            
            // read exponents
            // HACK: This is needed here because the power operator has
            //   higher precedence than the unary operators.  Rather than
            //   have unary operators handled in ReadExp, they are handled
            //   so exponents need to be handled before we apply the
            //   unary operators.
            if (_reader.Peek() == "^")
            {
                _reader.Read();
                IParseItem temp = ReadSimpExp();
                BinOpItem item = new BinOpItem(o, BinaryOperationType.Power);
                item.Rhs = temp;
                o = item;
            }

            // now apply the unary operators
            while (ex.Count > 0)
            {
                switch (ex.Pop())
                {
                    case 1: // neg
                        if (o is LiteralItem)
                        {
                            object oo = (o as LiteralItem).Item;
                            if (!(oo is double))
                                throw new SyntaxException("Cannot use unary minus on a string, bool, or nil.", _line, _pos, _name);

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

            // finaly return
            return o;
        }
        IParseItem ReadExp(int prec = -1)
        {
            IParseItem cur = ReadSimpExp();
            BinOpItem ret = null;

        start:
            string last = _reader.Peek();
            BinaryOperationType type = GetType(last);
            int nPrec = GetPrec(type);
            if (nPrec != -1 && (prec == -1 || prec > nPrec))
            {
                _reader.Read(); // read 'and'
                ret = new BinOpItem(ret ?? cur, type);
                ret.Rhs = ReadExp(nPrec);
                goto start;
            }

            return ret ?? cur;
        }
        /// <summary>
        /// Reads a TableItem from the reader.  It
        /// can be started on the { and will remove
        /// the ending }.
        /// </summary>
        /// <returns>The table that was read.</returns>
        TableItem ReadTable()
        {
            if (_reader.Peek() == "{")
                _reader.Read();

            TableItem ret = new TableItem();
            string last = _reader.Peek();
            while ((last = _reader.Peek()) != "}")
            {
                if (last == "[")
                {
                    _reader.Read(); // read the "["
                    IParseItem temp = ReadExp();
                    if (temp == null)
                        throw new SyntaxException("Invalid table definition.", _line, _pos, _name);
                    if (_reader.Read() != "]")
                        throw new SyntaxException("Invalid table definition, expecting ']'.", _line, _pos, _name);
                    if (_reader.Read() != "=")
                        throw new SyntaxException("Invalid table definition, expecting '='.", _line, _pos, _name);
                    IParseItem val = ReadExp();
                    if (val == null)
                        throw new SyntaxException("Invalid table definition.", _line, _pos, _name);
                    ret.AddItem(temp, val);
                }
                else
                {
                    IParseItem val = ReadExp();
                    if (_reader.Peek() == "=")
                    {
                        _reader.Read();
                        NameItem name = val as NameItem;
                        if (name == null)
                            throw new SyntaxException("Invalid table definition.", _line, _pos, _name);
                        IParseItem exp = ReadExp();
                        ret.AddItem(new LiteralItem(name.Name), exp);
                    }
                    else
                    {
                        ret.AddItem(null, val);
                    }
                }

                if (_reader.Peek() != "," && _reader.Peek() != ";")
                {
                    if (_reader.Peek() != "}")
                        throw new SyntaxException("Invalid table definition, expecting end of table '}'.", _line, _pos, _name);
                }
                else
                    _reader.Read();
            } // end While

            _reader.Read(); // read the "}"
            return ret;
        }
        /// <summary>
        /// Reads a function definition from the reader.  It can
        /// optionally be on the 'function' keyword.  If so, it will read
        /// a name also (unless name is false).
        /// </summary>
        /// <param name="canName">True if a name is allowed, otherwise false.</param>
        /// <param name="local">True if the function is defined as local, otherwise false.</param>
        /// <returns>The function that was read.</returns>
        FuncDef ReadFunction(bool canName, bool local)
        {
            long pos = _pos, line = _line;
            IParseItem name = null;
            string last = _reader.Peek(), inst = null;
            if (last == "function")
            {
                _reader.Read();
                last = _reader.Peek();
                if (IsName(last))
                {
                    _reader.Read();
                    name = new NameItem(last);
                    while ((last = _reader.Peek()) == ".")
                    {
                        _reader.Read();
                        last = _reader.Peek();
                        if (!IsName(last))
                            break;
                        name = new IndexerItem(name, new LiteralItem(last));
                        _reader.Read();
                    }
                    if (_reader.Peek() == ":")
                    {
                        _reader.Read();
                        inst = _reader.Read();
                        if (!IsName(inst))
                            throw new SyntaxException("Invalid toke '" + last + "' in function definition.", line, pos, _name);
                    }
                }
            }
            if (name != null && !canName)
                throw new SyntaxException("Functions cannot have names in expressions.", line, pos, _name);

            FuncDef ret = new FuncDef(name, line, pos);
            ret.InstanceName = inst;
            if ((last = _reader.Peek()) != "(")
                throw new SyntaxException("Invalid token '" + last + "' in function definition.", line, pos - last.Length, _name);
            _reader.Read();

            while ((last = _reader.Peek()) != ")")
            {
                _reader.Read(); // read the name
                if (!IsName(last) && last != "...")
                    throw new SyntaxException("Invalid token '" + last + "' in function definition.", _line, _pos - last.Length, _name);
                ret.AddParam(last);

                if ((last = _reader.Peek()) == ",")
                    _reader.Read();
                else if (last != ")")
                    throw new SyntaxException("Invalid token '" + last + "' in function definition.", _line, _pos - last.Length, _name);
            }
            if (last != ")")
                throw new SyntaxException("Invalid token '" + last + "' in function definition.", _line, _pos - last.Length, _name);
            _reader.Read(); // read ')'

            IParseItem chunk = ReadChunk(ret);
            ret.Block = chunk;
            ret.Block.AddItem(new ReturnItem());
            last = _reader.Read();
            if (last != "end")
                throw new SyntaxException("Invalid token '" + last + "' in function definition.", _line, _pos - last.Length, _name);

            return ret;
        }
        #endregion

        #region Helper Functions
        static bool IsName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var en = StringInfo.GetTextElementEnumerator(name);
            bool b = true;
            while (en.MoveNext())
            {
                string s=en.GetTextElement();
                if (!char.IsLetter(s, 0) && s != "_" && (b || !char.IsDigit(s, 0)))
                    return false;
                b = false;
            }

            return true;
        }
        static int GetPrec(BinaryOperationType type)
        {
            switch (type)
            {
                case BinaryOperationType.Multiply:
                case BinaryOperationType.Divide:
                case BinaryOperationType.Modulo:
                    return 3;
                case BinaryOperationType.Add:
                case BinaryOperationType.Subtract:
                    return 4;
                case BinaryOperationType.Concat:
                    return 5;
                case BinaryOperationType.Gt:
                case BinaryOperationType.Lt:
                case BinaryOperationType.Gte:
                case BinaryOperationType.Lte:
                case BinaryOperationType.Equals:
                case BinaryOperationType.NotEquals:
                    return 6;
                case BinaryOperationType.And:
                    return 7;
                case BinaryOperationType.Or:
                    return 8;
                default:
                    return -1;
            }
        }
        static BinaryOperationType GetType(string frag)
        {
            switch (frag)
            {
                case "+":
                    return BinaryOperationType.Add;
                case "-":
                    return BinaryOperationType.Subtract;
                case "*":
                    return BinaryOperationType.Multiply;
                case "/":
                    return BinaryOperationType.Divide;
                case "^":
                    return BinaryOperationType.Power;
                case "%":
                    return BinaryOperationType.Modulo;
                case "..":
                    return BinaryOperationType.Concat;
                case ">":
                    return BinaryOperationType.Gt;
                case "<":
                    return BinaryOperationType.Lt;
                case ">=":
                    return BinaryOperationType.Gte;
                case "<=":
                    return BinaryOperationType.Lte;
                case "==":
                    return BinaryOperationType.Equals;
                case "~=":
                    return BinaryOperationType.NotEquals;
                case "and":
                    return BinaryOperationType.And;
                case "or":
                    return BinaryOperationType.Or;
                default:
                    return BinaryOperationType.Unknown;
            }
        }
        #endregion
    }
}
