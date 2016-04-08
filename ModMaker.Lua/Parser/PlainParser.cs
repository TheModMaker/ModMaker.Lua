using ModMaker.Lua.Parser.Items;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;

namespace ModMaker.Lua.Parser
{
    /// <summary>
    /// Defines a default parser.  This parses Lua code into an IParseItem tree.
    /// This can be extended to modify it's behaviour.
    /// </summary>
    public class PlainParser : IParser
    {
        /// <summary>
        /// A set of reserved keywords.  Do not modify the set.
        /// </summary>
        protected static readonly ICollection<string> _reserved = new List<string>() { "and", "break", "do", 
            "else", "elseif", "end", "false", "for", "function", "goto", "if",  "in", "local", "nil", "not",
            "or", "repeat", "return", "then", "true", "until", "while", "class", "ref" };
        /// <summary>
        /// A cache of parsed trees.  If accessing or modifying this, aquire a 
        /// lock on _lock.
        /// </summary>
        protected static Dictionary<string, IParseItem> _cache = new Dictionary<string, IParseItem>();
        /// <summary>
        /// A lock object used to modify the cache.
        /// </summary>
        protected static readonly object _lock = new object();

        /// <summary>
        /// Contains the read functions that are indexed by the first token text.
        /// </summary>
        protected Dictionary<string, ReadStatement> Functions;

        /// <summary>
        /// A delegate that reads a statement from the input and returns the
        /// object that was read.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected delegate IParseStatement ReadStatement(ITokenizer input, ref Token prev);

        /// <summary>
        /// Creates a new parser object.
        /// </summary>
        public PlainParser() 
        {
            this.Functions = new Dictionary<string, ReadStatement>();
            this.Functions.Add("::",        this.ReadLabel);
            this.Functions.Add("if",        this.ReadIf);
            this.Functions.Add("for",       this.ReadFor);
            this.Functions.Add("class",     this.ReadClass);
            this.Functions.Add("function",  this.ReadFunction);
            this.Functions.Add("local",     this.ReadLocal);
            this.Functions.Add("break",     this.ReadBreak);
            this.Functions.Add("repeat",    this.ReadRepeat);
            this.Functions.Add("goto",      this.ReadGoto);
            this.Functions.Add("do",        this.ReadDo);
            this.Functions.Add("while",     this.ReadWhile);
        }

        /// <summary>
        /// Gets or sets whether or not to use a cache of parsed values.
        /// </summary>
        public bool UseCache { get; set; }

        /// <summary>
        /// Parses the given Lua code into a IParseItem tree.
        /// </summary>
        /// <param name="input">The Lua code to parse.</param>
        /// <param name="name">The name of the chunk, used for exceptions.</param>
        /// <param name="hash">The hash of the Lua code, can be null.</param>
        /// <returns>The code as an IParseItem tree.</returns>
        /// <remarks>Simply calls Parse(Tokenizer, string, bool) with force:false.</remarks>
        public IParseItem Parse(ITokenizer input, string name, string hash)
        {
            if (input == null)
                throw new ArgumentNullException("input");

            // check if the chunk is already loaded
            if (UseCache)
            {
                lock (_lock)
                {
                    if (_cache != null && hash != null && _cache.ContainsKey(hash))
                        return _cache[hash];
                }
            }

            // parse the chunk
            Token temp = new Token();
            IParseItem read = ReadBlock(input, ref temp);
            Token end = Read(input, ref temp);
            if (end.Value != null)
                throw new SyntaxException(string.Format(Resources.TokenEOF, end.Value), input.Name, end);

            // store the loaded chunk in the cache
            lock (_lock)
            {
                if (_cache != null && hash != null)
                    _cache[hash] = read;
            }

            return read;
        }

        /// <summary>
        /// Parses the given string dump using the given parser.
        /// </summary>
        /// <param name="dump">The string dump of the Lua code.</param>
        /// <param name="name">The name of the input, used for debugging.</param>
        /// <param name="parser">The parser to use to parse the code.</param>
        /// <returns>The parses IParseItem tree.</returns>
        /// <exception cref="System.ArgumentNullException">If parser or dump is null.</exception>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If the dump contains
        /// invalid Lua code.</exception>
        public static IParseItem Parse(IParser parser, string dump, string name)
        {
            if (parser == null)
                throw new ArgumentNullException("parser");
            if (dump == null)
                throw new ArgumentNullException("dump");

            TextElementEnumerator reader = StringInfo.GetTextElementEnumerator(dump);
            Tokenizer input = new Tokenizer(reader, name);

            return parser.Parse(input, name, null);
        }

        #region Read Functions

        /// <summary>
        /// Reads a block of code from the input.  Any end tokens
        /// should not be read and are handled by the parrent call
        /// (e.g. 'end' or 'until').
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append the total token onto.</param>
        /// <returns>The item that was read.</returns>
        protected virtual BlockItem ReadBlock(ITokenizer input, ref Token prev)
        {
            BlockItem ret = new BlockItem();
            Token total = input.Peek();
            total.Value = "";
            Token name;

            while ((name = input.Peek()).Value != null)
            {
                if (Functions.ContainsKey(name.Value))
                {
                    var temp = Functions[name.Value](input, ref total);
                    ret.AddItem(temp);
                }
                else if (name.Value == "return")
                {
                    ret.Return = ReadReturn(input, ref total);
                    return ret;
                }
                else if (name.Value == ";")
                {
                    Read(input, ref total); // read ';'
                }
                else if (name.Value == "end" || name.Value == "else" || name.Value == "elseif" ||
                    name.Value == "until")
                {
                    // don'type read as it will be handled by the parrent
                    prev.Append(total); // don't add 'end' to the prev 
                    ret.Debug = total;  //   or the current block, this 
                    return ret;         //   end belongs to the parrent.
                }
                else
                {
                    Token debug = name;
                    debug.Value = "";
                    var exp = ReadExp(input, ref debug);
                    if (exp is FuncCallItem)
                    {
                        (exp as FuncCallItem).Statement = true;
                        ret.AddItem((FuncCallItem)exp);
                    }
                    else if (exp is LiteralItem)
                    {
                        throw new SyntaxException(
                            "A literal is not a variable.",
                            input.Name, debug);
                    }
                    else if (exp is NameItem || exp is IndexerItem)
                    {
                        var i = ReadAssignment(input, ref debug, false, (IParseVariable)exp);
                        ret.AddItem(i);
                    }
                    else
                        throw new SyntaxException(
                            string.Format(Resources.TokenStatement, name.Value),
                            input.Name, debug);

                    total.Append(debug);
                }
            } // end While

            // only gets here if this is the global function
            ret.Debug = total;
            ret.Return = ret.Return ?? new ReturnItem();
            return ret;
        }

        /// <summary>
        /// Reads a local statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadLocal(ITokenizer input, ref Token prev)
        {
            var debug = input.Read(); // read 'local'
            if (debug.Value != "local")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "local", "ReadLocal"));

            var name = input.Peek();
            if (name.Value == "function")
            {
                prev.Append(debug);
                return ReadFunctionHelper(input, ref prev, true, true);
            }
            else
            {
                Read(input, ref debug); // read name
                if (!IsName(name.Value))
                    throw new SyntaxException(
                        string.Format(Resources.TokenNotAName, "local", name.Value),
                        input.Name, name);
                if (_reserved.Contains(name.Value))
                    throw new SyntaxException(
                        string.Format(Resources.TokenReserved, name.Value),
                        input.Name, name);

                var i = ReadAssignment(input, ref debug, true, new NameItem(name.Value) { Debug = name });
                prev.Append(debug);
                return i;
            }
        }
        /// <summary>
        /// Reads a class statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadClass(ITokenizer input, ref Token prev)
        {
            var debug = input.Read(); // read 'class'
            string sname = null;
            List<string> imp = new List<string>();
            if (debug.Value != "class")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "class", "ReadClass"));

            if (input.Peek().Value.StartsWith("'", StringComparison.Ordinal) ||
                input.Peek().Value.StartsWith("\"", StringComparison.Ordinal))
            {
                var name = Read(input, ref debug);
                sname = name.Value.Substring(1);
                if (input.Peek().Value == "(")
                {
                    Read(input, ref debug); // read '('
                    while (input.Peek().Value != ")")
                    {
                        // read the name
                        name = Read(input, ref debug);
                        if (!IsName(name.Value))
                            throw new SyntaxException(
                                string.Format(Resources.TokenNotAName, "class", name.Value),
                                input.Name, name);
                        imp.Add(name.Value);

                        // read ','
                        name = Read(input, ref debug);
                        if (name.Value != ",")
                            throw new SyntaxException(
                                string.Format(Resources.TokenInvalid, name.Value, "class"),
                                input.Name, name);
                    }
                    Read(input, ref debug); // read ')'
                }
            }
            else
            {
                var name = Read(input, ref debug);
                sname = name.Value;
                if (!IsName(sname))
                    throw new SyntaxException(
                        string.Format(Resources.TokenNotAName, "class", name.Value),
                        input.Name, name);
                if (input.Peek().Value == ":")
                {
                    do
                    {
                        // simply include the '.' in the name.
                        string n = "";
                        do
                        {
                            Read(input, ref debug); // read ':' or ','
                            n += (n == "" ? "" : ".") + Read(input, ref debug).Value;
                        } while (input.Peek().Value == ".");

                        imp.Add(n);
                    } while (input.Peek().Value == ",");
                }
            }

            prev.Append(debug);
            return new ClassDefItem(sname, imp.ToArray()) { Debug = debug };
        }
        /// <summary>
        /// Reads a return statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual ReturnItem ReadReturn(ITokenizer input, ref Token prev)
        {
            var debug = input.Read(); // read 'return'
            ReturnItem r = new ReturnItem();
            if (debug.Value != "return")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "return", "ReadReturn"));

            var name = input.Peek();
            if (name.Value != "end" && name.Value != "until" && name.Value != "elseif" &&
                name.Value != "else")
            {
                r.AddExpression(ReadExp(input, ref debug));
                while (input.Peek().Value == ",")
                {
                    Read(input, ref debug); // read ','
                    r.AddExpression(ReadExp(input, ref debug));
                }

                if (input.Peek().Value == ";")
                {
                    Read(input, ref debug); // read ';'
                }

                // look at the next token for validation but keep it in the
                //  reader for the parrent.
                name = input.Peek();
                if (name.Value != "end" && name.Value != "until" && name.Value != "elseif" &&
                    name.Value != "else" && !IsNullOrWhiteSpace(name.Value))
                    throw new SyntaxException(
                        Resources.ReturnAtEnd,
                        input.Name, debug);
            }

            prev.Append(debug);
            r.Debug = debug;
            return r;
        }
        /// <summary>
        /// Reads a normal function statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadFunction(ITokenizer input, ref Token prev)
        {
            return ReadFunctionHelper(input, ref prev, true, false);
        }
        /// <summary>
        /// Reads a for statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadFor(ITokenizer input, ref Token prev)
        {
            var debug = input.Read(); // read 'for'
            if (debug.Value != "for")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "for", "ReadFor"));

            // read a name
            var name = Read(input, ref debug);
            if (!IsName(name.Value))
                throw new SyntaxException(
                    string.Format(Resources.TokenNotAName, "for", name.Value),
                    input.Name, name);
            if (_reserved.Contains(name.Value))
                throw new SyntaxException(
                    string.Format(Resources.TokenReserved, name.Value),
                    input.Name, name);

            // numeric for
            if (input.Peek().Value == "=")
            {
                var ret = ReadNumberFor(input, ref debug, name);
                prev.Append(debug);
                return ret;
            }
            // generic for statement
            else
            {
                var ret = ReadGenericFor(input, ref debug, name);
                prev.Append(debug);
                return ret;
            }
        }
        /// <summary>
        /// Reads an if statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadIf(ITokenizer input, ref Token prev)
        {
            var debug = input.Read(); // read 'if'
            IfItem i = new IfItem();
            if (debug.Value != "if")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "if", "ReadIf"));

            // read the initial expression
            i.Exp = ReadExp(input, ref debug);

            // read 'then'
            var name = Read(input, ref debug);
            if (name.Value != "then")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalid, name.Value, "if"),
                    input.Name, debug);

            // read the block
            var readBlock = ReadBlock(input, ref debug);
            i.Block = readBlock;

            // handle elseif(s)
            while ((name = input.Peek()).Value == "elseif")
            {
                Read(input, ref debug); // read 'elseif'

                // read the expression
                var readExp = ReadExp(input, ref debug);

                // read 'then'
                name = Read(input, ref debug);
                if (name.Value != "then")
                    throw new SyntaxException(
                        string.Format(Resources.TokenInvalid, name.Value, "elseif"),
                        input.Name, debug);

                // read the block
                readBlock = ReadBlock(input, ref debug);
                i.AddElse(readExp, readBlock);
            }

            // handle else
            if (name.Value != "else" && name.Value != "end")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalid, name.Value, "if"),
                    input.Name, debug);
            if (name.Value == "else")
            {
                Read(input, ref debug); // read 'else'

                // read the block
                readBlock = ReadBlock(input, ref debug);
                i.ElseBlock = readBlock;
            }

            // read 'end'
            name = Read(input, ref debug);
            if (name.Value != "end")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalid, name.Value, "if"),
                    input.Name, debug);

            prev.Append(debug);
            i.Debug = debug;
            return i;
        }
        /// <summary>
        /// Reads a repeat statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadRepeat(ITokenizer input, ref Token prev)
        {
            var debug = input.Read(); // read 'repeat'
            RepeatItem repeat = new RepeatItem();
            if (debug.Value != "repeat")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "repeat", "ReadRepeat"));

            // read the block
            repeat.Block = ReadBlock(input, ref debug);

            // read 'until'
            var name = Read(input, ref debug);
            if (name.Value != "until")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, name.Value, "repeat", "until"),
                    input.Name, name);

            // read the expression
            repeat.Expression = ReadExp(input, ref debug);

            prev.Append(debug);
            repeat.Debug = debug;
            return repeat;
        }
        /// <summary>
        /// Reads a label statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadLabel(ITokenizer input, ref Token prev)
        {
            var debug = input.Read(); // read '::'
            if (debug.Value != "::")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "::", "ReadLabel"));

            // read the label
            Token label = Read(input, ref debug);

            // read '::'
            var name = Read(input, ref debug);
            if (name.Value != "::")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, name.Value, "label", "::"),
                    input.Name, debug);

            prev.Append(debug);
            return new LabelItem(label.Value) { Debug = debug };
        }
        /// <summary>
        /// Reads a break statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadBreak(ITokenizer input, ref Token prev)
        {
            var ret = input.Read();
            if (ret.Value != "break")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "break", "ReadBreak"));

            prev.Append(ret);
            return new GotoItem("<break>") { Debug = ret };
        }
        /// <summary>
        /// Reads a goto statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadGoto(ITokenizer input, ref Token prev)
        {
            var debug = input.Read(); // read 'goto'
            if (debug.Value != "goto")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "goto", "ReadGoto"));
            
            // read the target
            var name = Read(input, ref debug);
            if (!IsName(name.Value))
                throw new SyntaxException(
                    string.Format(Resources.TokenNotAName, "goto", name.Value),
                    input.Name, debug);

            if (_reserved.Contains(name.Value))
                throw new SyntaxException(
                    string.Format(Resources.TokenReserved, name.Value),
                    input.Name, name);

            prev.Append(debug);
            return new GotoItem(name.Value) { Debug = debug };
        }
        /// <summary>
        /// Reads a do statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadDo(ITokenizer input, ref Token prev)
        {
            var debug = input.Read(); // read 'do'
            if (debug.Value != "do")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "do", "ReadDo"));

            // read the block
            var ret = ReadBlock(input, ref debug);

            // ensure that it ends with 'end'
            Token end = Read(input, ref debug);
            if (end.Value != "end")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, end.Value, "do", "end"),
                    input.Name, end);
            
            prev.Append(debug);
            return ret;
        }
        /// <summary>
        /// Reads a while statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="prev">The token to append what is read into.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadWhile(ITokenizer input, ref Token prev)
        {
            var debug = input.Read(); // read 'while'
            WhileItem w = new WhileItem();
            if (debug.Value != "while")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "while", "ReadWhile"));
            
            // read the expression
            w.Exp = ReadExp(input, ref debug);

            // read 'do'
            var name = Read(input, ref debug);
            if (name.Value != "do")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, name.Value, "while", "do"),
                    input.Name, name);

            // read the block
            w.Block = ReadBlock(input, ref debug);

            // read 'end'
            name = Read(input, ref debug);
            if (name.Value != "end")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, name.Value, "while", "end"),
                    input.Name, name);

            prev.Append(debug);
            w.Debug = debug;
            return w;
        }

        #endregion

        #region Read Helpers

        /// <summary>
        /// Contains information for a unary expression.  Used in ReadPrefixExp.
        /// </summary>
        private struct UnaryInfo
        {
            /// <summary>
            /// Creates a new instance of UnaryInfo.
            /// </summary>
            /// <param name="version">The version of the operation.</param>
            /// <param name="pos">The starting position of the token.</param>
            /// <param name="line">The starting line of the token.</param>
            public UnaryInfo(int version, long pos, long line)
            {
                Version = version;
                StartPos = pos;
                StartLine = line;
            }

            /// <summary>
            /// Contains the version of the unary operation, 1 - negation,
            /// 2 - not, 3 - length.
            /// </summary>
            public int Version;
            /// <summary>
            /// The starting position of the token.
            /// </summary>
            public long StartPos;
            /// <summary>
            /// The starting line of the token.
            /// </summary>
            public long StartLine;
        }

        /// <summary>
        /// Reads an assignment statement from the input.  The input is currently
        /// after the first name, on the comma or equal sign.  The debug token
        /// contains the name and should contain the entire statement.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="debug">Currently contains the first name, and after
        /// should contain the entire statement.</param>
        /// <param name="local">True if this is a local definition, otherwise false.</param>
        /// <param name="variable">The first variable that was read.</param>
        /// <returns>The statement that was read.</returns>
        protected virtual AssignmentItem ReadAssignment(ITokenizer input, ref Token debug, bool local, IParseVariable variable)
        {
            // read each of the variable names
            AssignmentItem assign = new AssignmentItem(local);
            assign.AddName(variable);
            while (input.Peek().Value == ",")
            {
                Read(input, ref debug); // read ','

                // read the left-hand-expression
                var exp = ReadExp(input, ref debug);
                if ((local && !(exp is NameItem)) || (!local && !(exp is IParseVariable)))
                    throw new SyntaxException(Resources.NameOrExpForVar, input.Name, debug);
                assign.AddName((IParseVariable)exp);
            }

            // read the initial values
            if (input.Peek().Value == "=")
            {
                Read(input, ref debug); // read '='
                assign.AddItem(ReadExp(input, ref debug));

                while (input.Peek().Value == ",")
                {
                    Read(input, ref debug); // read ','
                    assign.AddItem(ReadExp(input, ref debug));
                }
            }
            else if (!local)
                throw new SyntaxException(
                    string.Format(Resources.InvalidDefinition, "assignment"),
                    input.Name, debug);

            assign.Debug = debug;
            return assign;
        }
        /// <summary>
        /// Reads part of a generic for loop from the input.  The input is
        /// currently on the token after the first name and debug contains
        /// the parts read for the 'for' loop.  'name' contains the name of
        /// the first variable.
        /// </summary>
        /// <param name="input">Where to read input from/</param>
        /// <param name="debug">The token that currently holds what was read
        /// so far in the for statement and should after contain the entire loop.</param>
        /// <param name="name">The token that contains the name of the variable.</param>
        /// <returns>The loop object that was read.</returns>
        protected virtual ForGenItem ReadGenericFor(ITokenizer input, ref Token debug, Token name)
        {
            // read the variables
            List<NameItem> names = new List<NameItem>();
            names.Add(new NameItem(name.Value) { Debug = name });

            while (input.Peek().Value == ",")
            {
                Read(input, ref debug); // read ','

                // read the name
                name = Read(input, ref debug);
                if (!IsName(name.Value))
                    throw new SyntaxException(
                        string.Format(Resources.TokenNotAName, "for", name.Value),
                        input.Name, name);
                if (_reserved.Contains(name.Value))
                    throw new SyntaxException(
                        string.Format(Resources.TokenReserved, name.Value),
                        input.Name, name);

                names.Add(new NameItem(name.Value) { Debug = name });
            }

            // check for 'in'
            name = Read(input, ref debug);
            if (name.Value != "in")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, name.Value, "for", "in"),
                    input.Name, name);

            // read the expression-list
            ForGenItem f = new ForGenItem(names);
            f.AddExpression(ReadExp(input, ref debug));
            while (input.Peek().Value == ",")
            {
                Read(input, ref debug); // read ","
                f.AddExpression(ReadExp(input, ref debug));
            }

            // check for 'do'
            name = Read(input, ref debug);
            if (name.Value != "do")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, name.Value, "for", "do"),
                    input.Name, name);

            // read the chunk
            f.Block = ReadBlock(input, ref debug);

            // read 'end'
            name = Read(input, ref debug);
            if (name.Value != "end")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, name.Value, "for", "end"),
                    input.Name, name);

            f.Debug = debug;
            return f;
        }
        /// <summary>
        /// Reads part of a numerical for loop from the input.  The input is 
        /// currently on the equals sign '=' and the debug token currently
        /// contains the parts read for the 'for' loop.  'name' contains the
        /// name of the variable.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="debug">The token that currently holds what was read
        /// so far in the for statement and should after contain the entire loop.</param>
        /// <param name="name">The token that contains the name of the variable.</param>
        /// <returns>The loop object that was read.</returns>
        protected virtual ForNumItem ReadNumberFor(ITokenizer input, ref Token debug, Token name)
        {
            // read "="
            var temp = Read(input, ref debug);
            if (temp.Value != "=")
                throw new InvalidOperationException(string.Format(Resources.MustBeOn, "=", "ReadNumberFor"));

            // get the 'start' value
            var start = ReadExp(input, ref debug);

            // read ','
            temp = Read(input, ref debug);
            if (temp.Value != ",")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, temp.Value, "for", ","),
                    input.Name, temp);

            // get the 'limit'
            var limit = ReadExp(input, ref debug);

            // read ','
            IParseExp step = null;
            if (input.Peek().Value == ",")
            {
                Read(input, ref debug);

                // read the 'step'
                step = ReadExp(input, ref debug);
            }

            ForNumItem i = new ForNumItem(new NameItem(name.Value) { Debug = name }, start, limit, step);

            // check for 'do'
            name = Read(input, ref debug);
            if (name.Value != "do")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, name.Value, "for", "do"),
                    input.Name, name);

            // read the block
            i.Block = ReadBlock(input, ref debug);

            // read 'end'
            name = Read(input, ref debug);
            if (name.Value != "end")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, name.Value, "for", "end"),
                    input.Name, name);

            i.Debug = debug;
            return i;
        }
        /// <summary>
        /// Reads a prefix-expression from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="token">The token to append the total token onto.</param>
        /// <returns>The expression that was read.</returns>
        protected virtual IParseExp ReadPrefixExp(ITokenizer input, ref Token token)
        {
            Stack<UnaryInfo> ex = new Stack<UnaryInfo>();
            IParseExp o = null;
            Token last, debug = input.Peek();
            debug.Value = "";

            // check for unary operators
            last = input.Peek();
            while (last.Value == "-" || last.Value == "not" || last.Value == "#")
            {
                Read(input, ref debug);

                if (last.Value == "-")
                {
                    ex.Push(new UnaryInfo(1, last.StartPos, last.StartLine));
                }
                else if (last.Value == "not")
                {
                    ex.Push(new UnaryInfo(2, last.StartPos, last.StartLine));
                }
                else
                {
                    Contract.Assert(last.Value == "#");
                    ex.Push(new UnaryInfo(3, last.StartPos, last.StartLine));
                }
                last = input.Peek();
            }

            // check for literals
            last = input.Peek();
            int over = -1;
            if (last.Value != null)
            {
                NumberFormatInfo ni = CultureInfo.CurrentCulture.NumberFormat;
                if (last.Value != "..." && (char.IsNumber(last.Value, 0) || last.Value.StartsWith(ni.NumberDecimalSeparator, StringComparison.CurrentCulture)))
                {
                    Read(input, ref debug); // read the number.
                    try
                    {
                        o = new LiteralItem(double.Parse(last.Value, CultureInfo.CurrentCulture)) { Debug = last };
                    }
                    catch (FormatException e)
                    {
                        throw new SyntaxException(Resources.BadNumberFormat, input.Name, last, e);
                    }
                }
                else if (last.Value.StartsWith("&", StringComparison.Ordinal))
                {
                    Read(input, ref debug);
                    try
                    {
                        o = new LiteralItem(Convert.ToDouble(long.Parse(last.Value.Substring(1),
                            NumberStyles.AllowHexSpecifier, CultureInfo.CurrentCulture))) { Debug = last };
                    }
                    catch (FormatException e)
                    {
                        throw new SyntaxException(Resources.BadNumberFormat, input.Name, last, e);
                    }
                }
                else if (last.Value.StartsWith("\"", StringComparison.Ordinal))
                {
                    Read(input, ref debug);
                    o = new LiteralItem(last.Value.Substring(1)) { Debug = last };
                }
                else if (last.Value.StartsWith("{", StringComparison.Ordinal))
                {
                    o = ReadTable(input, ref debug);
                }
                else if (last.Value == "(")
                {
                    Read(input, ref debug);
                    o = ReadExp(input, ref debug);
                    last = Read(input, ref debug);
                    if (last.Value != ")")
                        throw new SyntaxException(string.Format(Resources.TokenInvalidExpecting, last.Value, "expression", ")"),
                            input.Name, last);
                }
                else if (last.Value == "true")
                {
                    Read(input, ref debug);
                    o = new LiteralItem(true) { Debug = last };
                }
                else if (last.Value == "false")
                {
                    Read(input, ref debug);
                    o = new LiteralItem(false) { Debug = last };
                }
                else if (last.Value == "nil")
                {
                    Read(input, ref debug);
                    o = new LiteralItem(null) { Debug = last };
                }
                else if (last.Value == "function")
                {
                    o = ReadFunctionHelper(input, ref debug, false, false);
                }
                else
                {
                    // allow for specifying overloads on global variables
                    if (last.Value.IndexOf('`') != -1)
                    {
                        if (!int.TryParse(last.Value.Substring(last.Value.IndexOf('`') + 1), out over))
                            throw new InvalidOperationException(Resources.OnlyNumbersInOverload);

                        last.Value = last.Value.Substring(0, last.Value.IndexOf('`'));
                    }

                    Read(input, ref debug);
                    o = new NameItem(last.Value) { Debug = last };
                }
            }

            // read function calls and indexers
            {
                string inst = null;
                bool cont = true;
                while (cont)
                {
                    last = input.Peek();
                    last.Value = last.Value ?? "";
                    if (last.Value == ".")
                    {
                        Read(input, ref debug);
                        if (over != -1)
                            throw new SyntaxException(Resources.FunctionCallAfterOverload, input.Name, last);
                        if (inst != null)
                            throw new SyntaxException(Resources.IndexerAfterInstance, input.Name, last);

                        last = Read(input, ref debug);

                        // allow for specifying an overload
                        if (last.Value.IndexOf('`') != -1)
                        {
                            if (!int.TryParse(last.Value.Substring(last.Value.IndexOf('`') + 1), out over))
                                throw new InvalidOperationException(Resources.OnlyNumbersInOverload);

                            last.Value = last.Value.Substring(0, last.Value.IndexOf('`'));
                        }

                        if (!IsName(last.Value))
                            throw new SyntaxException(string.Format(Resources.TokenNotAName, "indexer", last.Value),
                                input.Name, last);
                        if (!(o is IParsePrefixExp))
                            throw new SyntaxException(Resources.IndexAfterExpression, input.Name, last);

                        o = new IndexerItem(o, new LiteralItem(last.Value) { Debug = last }) { Debug = debug };
                    }
                    else if (last.Value == ":")
                    {
                        Read(input, ref debug);
                        if (over != -1)
                            throw new SyntaxException(Resources.FunctionCallAfterOverload, input.Name, last);
                        if (inst != null)
                            throw new SyntaxException(Resources.OneInstanceCall, input.Name, last);
                        inst = Read(input, ref debug).Value;
                        if (!IsName(inst))
                            throw new SyntaxException(string.Format(Resources.TokenNotAName, "indexer", last.Value),
                                input.Name, last);
                    }
                    else if (last.Value == "[")
                    {
                        Read(input, ref debug);
                        if (over != -1)
                            throw new SyntaxException(Resources.FunctionCallAfterOverload,
                                input.Name, last);
                        if (inst != null)
                            throw new SyntaxException(Resources.IndexerAfterInstance, input.Name, last);

                        var temp = ReadExp(input, ref debug);
                        last = Read(input, ref debug);
                        o = new IndexerItem(o, temp) { Debug = debug };
                        if (last.Value != "]")
                            throw new SyntaxException(
                                string.Format(Resources.TokenInvalidExpecting, last.Value, "indexer", "]"),
                                input.Name, last);
                    }
                    else if (last.Value.StartsWith("\"", StringComparison.Ordinal))
                    {
                        Read(input, ref debug);
                        FuncCallItem temp = new FuncCallItem(o, inst, over) { Debug = debug };
                        o = temp;
                        temp.AddItem(new LiteralItem(last.Value.Substring(1)), false);
                        inst = null;
                        over = -1;
                    }
                    else if (last.Value == "{")
                    {
                        var temp = ReadTable(input, ref debug);
                        FuncCallItem func = new FuncCallItem(o, inst, over) { Debug = debug };
                        o = func;
                        func.AddItem(temp, false);
                        inst = null;
                        over = -1;
                    }
                    else if (last.Value == "(")
                    {
                        Read(input, ref debug);
                        FuncCallItem func = new FuncCallItem(o, inst, over);
                        o = func;
                        inst = null;
                        over = -1;
                        while (input.Peek().Value != ")" && input.Peek().Value != null)
                        {
                            bool? byRef = null;
                            if (input.Peek().Value == "@")
                            {
                                byRef = false;
                                Read(input, ref debug);
                            }
                            else if (input.Peek().Value == "ref")
                            {
                                Read(input, ref debug);
                                if (input.Peek().Value == "(")
                                {
                                    Read(input, ref debug);
                                    byRef = true;
                                }
                                else
                                    byRef = false;
                            }

                            var temp = ReadExp(input, ref debug);
                            if (byRef != null && !(temp is NameItem) && !(temp is IndexerItem))
                                throw new SyntaxException(Resources.OnlyVarByReference, input.Name, last);
                            if (temp == null)
                                throw new SyntaxException(string.Format(Resources.InvalidDefinition, "function call"),
                                    input.Name, last);
                            func.AddItem(temp, byRef != null);

                            if (byRef == true && (last = input.Read()).Value != ")")
                                throw new SyntaxException(Resources.RefOneArgument,
                                    input.Name, last);

                            if (input.Peek().Value == ",")
                                Read(input, ref debug);
                            else if (input.Peek().Value == ")")
                                break;
                            else
                                throw new SyntaxException(string.Format(Resources.TokenInvalidExpecting2, input.Peek().Value, "function call", ",", ")"), input.Name, last);
                        }

                        if (input.Peek() == null)
                            throw new SyntaxException(string.Format(Resources.UnexpectedEOF, "function call"),
                                input.Name, last);
                        Read(input, ref debug);
                        func.Debug = debug;
                    }
                    else
                    {
                        if (inst != null)
                            throw new SyntaxException(Resources.InstanceMissingArgs, input.Name, last);
                        if (over != -1)
                            throw new SyntaxException(Resources.OverloadMissingArgs, input.Name, last);
                        cont = false;
                    }
                }
            }

            // read exponents
            // HACK: This is needed here because the power operator has
            //   higher precedence than the unary operators.  Rather than
            //   have unary operators handled in ReadExp, they are handled
            //   so exponents need to be handled before we apply the
            //   unary operators.
            if (input.Peek().Value == "^")
            {
                Read(input, ref debug);
                var temp = ReadPrefixExp(input, ref debug);
                BinOpItem item = new BinOpItem(o, BinaryOperationType.Power, temp) { Debug = debug };
                o = item;
            }

            // now apply the unary operators
            while (ex.Count > 0)
            {
                var loc = ex.Pop();
                Token tok = new Token(debug.Value, loc.StartPos, debug.EndPos, loc.StartLine, debug.EndLine);
                switch (loc.Version)
                {
                    case 1: // neg
                        if (o is LiteralItem)
                        {
                            object oo = (o as LiteralItem).Value;
                            if (!(oo is double))
                                throw new SyntaxException(Resources.InvalidUnary,
                                    input.Name, debug);

                            o = new LiteralItem(-(double)oo) { Debug = tok };
                        }
                        else
                            o = new UnOpItem(o, UnaryOperationType.Minus) { Debug = tok };
                        break;
                    case 2: // not
                        o = new UnOpItem(o, UnaryOperationType.Not) { Debug = tok };
                        break;
                    case 3: // len
                        o = new UnOpItem(o, UnaryOperationType.Length) { Debug = tok };
                        break;
                }
            }

            // finaly return
            token.Append(debug);
            return o;
        }
        /// <summary>
        /// Reads an expression from the input and returns the
        /// item that was read.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="precedence">The precedence of the previous expression
        /// or -1 if a root.</param>
        /// <param name="token">The Token that represents the entire expression
        /// should be appended to this variable.</param>
        /// <returns>The expression that was read.</returns>
        protected virtual IParseExp ReadExp(ITokenizer input, ref Token token, int precedence = -1)
        {
            Token debug = input.Peek();
            debug.Value = "";
            IParseExp cur = ReadPrefixExp(input, ref debug);
            BinOpItem ret = null;

        start:
            Token last = input.Peek();
            BinaryOperationType type = GetOperationType(last.Value);
            int nPrec = GetPrecedence(type);
            if (nPrec != -1 && (precedence == -1 || precedence > nPrec))
            {
                Read(input, ref debug); // read the exp
                var temp = ReadExp(input, ref debug, nPrec);
                ret = new BinOpItem(ret ?? cur, type, temp);
                ret.Debug = debug;
                goto start;
            }

            token.Append(debug);
            return ret ?? cur;
        }
        /// <summary>
        /// Reads a function from the input.  Input must either be on the word 'function' or
        /// on the next token.  If it is on 'function' and canName is true, it will give
        /// the function the read name; otherwise it will give it a null name.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="token">The token to append the read Token to.</param>
        /// <param name="canName">True if the function can have a name, otherwise false.</param>
        /// <param name="local">True if this function is a local definition, otherwise false.</param>
        /// <returns>The function definition that was read.</returns>
        protected virtual FuncDefItem ReadFunctionHelper(ITokenizer input, ref Token token, bool canName, bool local)
        {
            IParseVariable name = null;
            string inst = null;
            Token last = input.Peek(), debug = last;
            if (last.Value == "function")
            {
                input.Read(); // read 'function'
                last = input.Peek();
                if (IsName(last.Value))
                {
                    Token nameTok = input.Read(); // read name
                    name = new NameItem(last.Value) { Debug = last };

                    // handle indexers
                    last = input.Peek();
                    while (last.Value == ".")
                    {
                        Read(input, ref nameTok); // read '.'
                        last = input.Peek();
                        if (!IsName(last.Value))
                            break;

                        name = new IndexerItem(name, new LiteralItem(last.Value) { Debug = last }) { Debug = nameTok };
                        Read(input, ref nameTok);
                    }

                    if (input.Peek().Value == ":")
                    {
                        Read(input, ref nameTok);
                        inst = Read(input, ref nameTok).Value;
                        if (!IsName(inst))
                            throw new SyntaxException(string.Format(Resources.TokenInvalid, last.Value, "function"),
                                input.Name, last);
                    }
                    debug.Append(nameTok);
                }
            }
            if (name != null && !canName)
                throw new SyntaxException(Resources.FunctionCantHaveName, input.Name, debug);

            FuncDefItem ret = new FuncDefItem(name, local);
            ret.InstanceName = inst;
            last = Read(input, ref debug);
            if (last.Value != "(")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, last.Value, "function", "("),
                    input.Name, last);

            last = input.Peek();
            while (last.Value != ")")
            {
                Token temp = Read(input, ref debug); // read the name
                if (!IsName(last.Value) && last.Value != "...")
                    throw new SyntaxException(string.Format(Resources.TokenInvalid, last.Value, "function"),
                        input.Name, temp);
                ret.AddArgument(new NameItem(last.Value) { Debug = last });

                last = input.Peek();
                if (last.Value == ",")
                    Read(input, ref debug);
                else if (last.Value != ")")
                    throw new SyntaxException(
                        string.Format(Resources.TokenInvalidExpecting2, last.Value, "function", ",", ")"),
                        input.Name, last);

                last = input.Peek();
            }
            if (last.Value != ")")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, last.Value, "function", ")"),
                    input.Name, last);
            Read(input, ref debug); // read ')'

            BlockItem chunk = ReadBlock(input, ref debug);
            chunk.Return = chunk.Return ?? new ReturnItem();
            ret.Block = chunk;
            last = Read(input, ref debug);
            if (last.Value != "end")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, last.Value, "function", "end"),
                    input.Name, last);

            token.Append(debug);
            ret.Debug = debug;
            return ret;
        }
        /// <summary>
        /// Reads a table from the input.  Input must be either on the starting '{'.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="token">The token to append the read Tokenm to.</param>
        /// <returns>The table that was read.</returns>
        protected virtual TableItem ReadTable(ITokenizer input, ref Token token)
        {
            Token debug = input.Read();
            if (debug.Value != "{")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, debug.Value, "table", "{"),
                    input.Name, debug);

            TableItem ret = new TableItem();
            Token last = input.Peek();
            while (last.Value != "}")
            {
                if (last.Value == "[")
                {
                    Read(input, ref debug); // read the "["

                    var temp = ReadExp(input, ref debug);
                    if (temp == null)
                        throw new SyntaxException(string.Format(Resources.InvalidDefinition, "table"),
                            input.Name, debug);

                    // read ']'
                    last = Read(input, ref debug);
                    if (last.Value != "]")
                        throw new SyntaxException(
                            string.Format(Resources.TokenInvalidExpecting, last.Value, "table", "]"), 
                            input.Name, last);

                    // read '='
                    last = Read(input, ref debug);
                    if (last.Value != "=")
                        throw new SyntaxException(
                            string.Format(Resources.TokenInvalidExpecting, last.Value, "table", "="), 
                            input.Name, last);

                    // read the expression
                    var val = ReadExp(input, ref debug);
                    if (val == null)
                        throw new SyntaxException(string.Format(Resources.InvalidDefinition, "table"),
                            input.Name, debug);

                    ret.AddItem(temp, val);
                }
                else
                {
                    var val = ReadExp(input, ref debug);
                    if (input.Peek().Value == "=")
                    {
                        Read(input, ref debug); // read '='

                        NameItem name = val as NameItem;
                        if (name == null)
                            throw new SyntaxException(string.Format(Resources.InvalidDefinition, "table"),
                                input.Name, debug);

                        // read the expression
                        var exp = ReadExp(input, ref debug);
                        ret.AddItem(new LiteralItem(name.Name), exp);
                    }
                    else
                    {
                        ret.AddItem(null, val);
                    }
                }

                if (input.Peek().Value != "," && input.Peek().Value != ";")
                    break;
                else
                    Read(input, ref debug);
                last = input.Peek();
            } // end While

            Token end = Read(input, ref debug); // read the "}"
            if (end.Value != "}")
                throw new SyntaxException(
                    string.Format(Resources.TokenInvalidExpecting, end.Value, "table", "}"), 
                    input.Name, end);

            ret.Debug = debug;
            token.Append(debug);
            return ret;
        }

        #endregion

        #region Helper Functions

        /// <summary>
        /// Checks whether the given string can represent
        /// a Lua name, does not check if the name is reserved.
        /// </summary>
        /// <param name="name">The value to check.</param>
        /// <returns>True if the value can be a name, oterwise false.</returns>
        protected static bool IsName(string name)
        {
            if (IsNullOrWhiteSpace(name))
                return false;

            var en = StringInfo.GetTextElementEnumerator(name);

            // check that the first character is a letter or an underscore,
            //   first character cannot be a number.
            if (en.MoveNext() && !char.IsLetter(en.GetTextElement(), 0) && en.GetTextElement() != "_")
                return false;

            while (en.MoveNext())
            {
                string s = en.GetTextElement();
                if (!char.IsLetterOrDigit(s, 0) && s != "_" && s != "`")
                    return false;
            }

            return true;
        }
        /// <summary>
        /// Reads a token from the tokenizer and appends the read
        /// value to the given token.
        /// </summary>
        /// <param name="input">Where to get the input from.</param>
        /// <param name="token">A token to append the read token to.</param>
        /// <returns>The token that was read.</returns>
        protected static Token Read(ITokenizer input, ref Token token)
        {
            Token ret = input.Read();
            token.Append(ret);
            return ret;
        }
        /// <summary>
        /// Returns the BinaryOperationType for the given string operand.
        /// </summary>
        /// <param name="frag">The string operand to get for.</param>
        /// <returns>The BinaryOperationType for the given frag.</returns>
        protected static BinaryOperationType GetOperationType(string frag)
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
        /// <summary>
        /// Gets the precedence of the given operation.  A smaller number
        /// means that the operation should be applied before ones with
        /// larger numbers.
        /// </summary>
        /// <param name="type">The type to get for.</param>
        /// <returns>The precedence of the given operation.</returns>
        protected static int GetPrecedence(BinaryOperationType type)
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
        /// <summary>
        /// Determines if the given string is null, empty, or consists of only
        /// whitespace.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>Whether the value is null, empty, or whitespace.</returns>
        protected static bool IsNullOrWhiteSpace(string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            foreach (var c in value)
            {
                if (!char.IsWhiteSpace(c))
                    return false;
            }

            return true;
        }

        #endregion
    }
}
