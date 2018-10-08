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

using ModMaker.Lua.Parser.Items;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ModMaker.Lua.Parser
{
    /// <summary>
    /// Defines a default parser.  This parses Lua code into an IParseItem tree.
    /// This can be extended to modify it's behaviour.
    /// </summary>
    public class PlainParser : IParser
    {
        /// <summary>
        /// Contains the read functions that are indexed by the first token text.
        /// </summary>
        protected Dictionary<TokenType, ReadStatement> Functions;

        /// <summary>
        /// A delegate that reads a statement from the input and returns the
        /// object that was read.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected delegate IParseStatement ReadStatement(Tokenizer input);

        /// <summary>
        /// Creates a new parser object.
        /// </summary>
        public PlainParser()
        {
            this.Functions = new Dictionary<TokenType, ReadStatement>();
            this.Functions.Add(TokenType.Label,     this.ReadLabel);
            this.Functions.Add(TokenType.If,        this.ReadIf);
            this.Functions.Add(TokenType.For,       this.ReadFor);
            this.Functions.Add(TokenType.Class,     this.ReadClass);
            this.Functions.Add(TokenType.Function,  this.ReadFunction);
            this.Functions.Add(TokenType.Local,     this.ReadLocal);
            this.Functions.Add(TokenType.Break,     this.ReadBreak);
            this.Functions.Add(TokenType.Repeat,    this.ReadRepeat);
            this.Functions.Add(TokenType.Goto,      this.ReadGoto);
            this.Functions.Add(TokenType.Do,        this.ReadDo);
            this.Functions.Add(TokenType.While,     this.ReadWhile);
        }

        /// <summary>
        /// Parses the given Lua code into a IParseItem tree.
        /// </summary>
        /// <param name="input">The Lua code to parse.</param>
        /// <param name="encoding">The encoding that the stream uses.</param>
        /// <param name="name">The name of the chunk, used for exceptions.</param>
        /// <returns>The code as an IParseItem tree.</returns>
        /// <remarks>Simply calls Parse(Tokenizer, string, bool) with force:false.</remarks>
        public IParseItem Parse(Stream input, Encoding encoding, string name)
        {
            Tokenizer tok = new Tokenizer(input, encoding, name);
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            // parse the chunk
            IParseItem read = ReadBlock(tok);
            if (!tok.PeekType(TokenType.None))
                tok.SyntaxError("Expecting EOF");

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
                throw new ArgumentNullException(nameof(parser));
            if (dump == null)
                throw new ArgumentNullException(nameof(dump));

            var encoding = Encoding.UTF8;
            var stream = new MemoryStream(encoding.GetBytes(dump));
            return parser.Parse(stream, encoding, name);
        }

        #region Read Functions

        /// <summary>
        /// Reads a block of code from the input.  Any end tokens
        /// should not be read and are handled by the parrent call
        /// (e.g. 'end' or 'until').
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The item that was read.</returns>
        protected virtual BlockItem ReadBlock(Tokenizer input)
        {
            BlockItem ret = new BlockItem() { Debug = input.Peek() };

            Token cur;
            while ((cur = input.Peek()).Type != TokenType.None)
            {
                if (Functions.ContainsKey(cur.Type))
                {
                    ret.AddItem(Functions[cur.Type](input));
                }
                else if (cur.Type == TokenType.Return)
                {
                    ret.Return = ReadReturn(input);
                    return ret;
                }
                else if (cur.Type == TokenType.Semicolon)
                {
                    input.Expect(TokenType.Semicolon);
                }
                else if (cur.Type == TokenType.End || cur.Type == TokenType.Else ||
                         cur.Type == TokenType.ElseIf || cur.Type == TokenType.Until)
                {
                    // don't read as it will be handled by the parent
                    // or the current block, this end belongs to the parent.
                    return ret;
                }
                else
                {
                    bool ignored;
                    var exp = ReadExp(input, out ignored);
                    if (exp is FuncCallItem)
                    {
                        (exp as FuncCallItem).Statement = true;
                        ret.AddItem((FuncCallItem)exp);
                    }
                    else if (exp is LiteralItem)
                    {
                        throw new SyntaxException(
                            "A literal is not a variable.",
                            input.Name, cur);
                    }
                    else if (exp is NameItem || exp is IndexerItem)
                    {
                        var i = ReadAssignment(input, cur, false, (IParseVariable)exp);
                        ret.AddItem(i);
                    }
                    else
                        throw new SyntaxException(
                            string.Format(Resources.TokenStatement, cur.Value),
                            input.Name, cur);
                }
            } // end While

            // only gets here if this is the global function
            ret.Return = ret.Return ?? new ReturnItem();
            return ret;
        }

        /// <summary>
        /// Reads a local statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadLocal(Tokenizer input)
        {
            var debug = input.Expect(TokenType.Local);
            if (input.PeekType(TokenType.Function))
            {
                var ret = ReadFunctionHelper(input, true, true);
                ret.Debug = debug;
                return ret;
            }
            else
            {
                var name = input.Expect(TokenType.Identifier);
                var nameItem = new NameItem(name.Value) { Debug = name };
                return ReadAssignment(input, debug, true, nameItem);
            }
        }
        /// <summary>
        /// Reads a class statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadClass(Tokenizer input)
        {
            var debug = input.Expect(TokenType.Class);
            string className = null;
            List<string> imp = new List<string>();

            if (input.PeekType(TokenType.StringLiteral))
            {
                className = input.Expect(TokenType.StringLiteral).Value;
                if (input.ReadIfType(TokenType.BeginParen))
                {
                    while (!input.PeekType(TokenType.EndParen))
                    {
                        var name = input.Expect(TokenType.Identifier);
                        imp.Add(name.Value);
                        if (!input.ReadIfType(TokenType.Comma))
                            break;
                    }
                    input.Expect(TokenType.EndParen);
                }
            }
            else
            {
                className = input.Expect(TokenType.Identifier).Value;
                if (input.PeekType(TokenType.Colon))
                {
                    do
                    {
                        input.Read();  // Skip the ':' or ','
                        // Simply include the '.' in the name.
                        string name = input.Expect(TokenType.Identifier).Value;
                        while (input.ReadIfType(TokenType.Indexer))
                        {
                            name += "." + input.Expect(TokenType.Identifier).Value;
                        }
                        imp.Add(name);
                    } while (input.PeekType(TokenType.Comma));
                }
            }

            return new ClassDefItem(className, imp.ToArray()) { Debug = debug };
        }
        /// <summary>
        /// Reads a return statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual ReturnItem ReadReturn(Tokenizer input)
        {
            var debug = input.Expect(TokenType.Return);
            ReturnItem ret = new ReturnItem() { Debug = debug };

            var name = input.Peek();
            if (!input.PeekType(TokenType.End) && !input.PeekType(TokenType.Until) &&
                !input.PeekType(TokenType.ElseIf) && !input.PeekType(TokenType.Else))
            {
                bool isParentheses;
                ret.AddExpression(ReadExp(input, out isParentheses));
                while (input.ReadIfType(TokenType.Comma))
                {
                    ret.AddExpression(ReadExp(input, out isParentheses));
                }
                ret.IsLastExpressionSingle = isParentheses;

                input.ReadIfType(TokenType.Semicolon);
            }

            return ret;
        }
        /// <summary>
        /// Reads a normal function statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadFunction(Tokenizer input)
        {
            return ReadFunctionHelper(input, true, false);
        }
        /// <summary>
        /// Reads a for statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadFor(Tokenizer input)
        {
            var debug = input.Expect(TokenType.For);
            var name = input.Expect(TokenType.Identifier);
            var nameItem = new NameItem(name.Value) { Debug = name };
            bool ignored;

            if (input.ReadIfType(TokenType.Assign))
            {
                // Numeric for
                var start = ReadExp(input, out ignored);
                input.Expect(TokenType.Comma);
                var limit = ReadExp(input, out ignored);

                IParseExp step = null;
                if (input.ReadIfType(TokenType.Comma))
                {
                    step = ReadExp(input, out ignored);
                }

                ForNumItem ret = new ForNumItem(nameItem, start, limit, step) { Debug = debug };
                input.Expect(TokenType.Do);
                ret.Block = ReadBlock(input);
                input.Expect(TokenType.End);
                return ret;
            }
            else
            {
                // Generic for statement

                // Read the variables
                List<NameItem> names = new List<NameItem>() { nameItem };
                while (input.ReadIfType(TokenType.Comma))
                {
                    var token = input.Expect(TokenType.Identifier);
                    names.Add(new NameItem(token.Value) { Debug = token });
                }
                input.Expect(TokenType.In);

                // Read the expression-list
                ForGenItem ret = new ForGenItem(names) { Debug = debug };
                ret.AddExpression(ReadExp(input, out ignored));
                while (input.ReadIfType(TokenType.Comma))
                {
                    ret.AddExpression(ReadExp(input, out ignored));
                }

                input.Expect(TokenType.Do);
                ret.Block = ReadBlock(input);
                input.Expect(TokenType.End);
                return ret;
            }
        }
        /// <summary>
        /// Reads an if statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadIf(Tokenizer input)
        {
            var debug = input.Expect(TokenType.If);
            IfItem ret = new IfItem() { Debug = debug };

            bool ignored;
            ret.Exp = ReadExp(input, out ignored);
            input.Expect(TokenType.Then);
            ret.Block = ReadBlock(input);

            while (input.ReadIfType(TokenType.ElseIf))
            {
                var exp = ReadExp(input, out ignored);
                input.Expect(TokenType.Then);
                var block = ReadBlock(input);
                ret.AddElse(exp, block);
            }

            if (input.ReadIfType(TokenType.Else))
            {
                ret.ElseBlock = ReadBlock(input);
            }
            input.Expect(TokenType.End);
            return ret;
        }
        /// <summary>
        /// Reads a repeat statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadRepeat(Tokenizer input)
        {
            var debug = input.Expect(TokenType.Repeat);
            RepeatItem repeat = new RepeatItem() { Debug = debug };
            repeat.Block = ReadBlock(input);
            input.Expect(TokenType.Until);
            bool ignored;
            repeat.Expression = ReadExp(input, out ignored);
            return repeat;
        }
        /// <summary>
        /// Reads a label statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadLabel(Tokenizer input)
        {
            var debug = input.Expect(TokenType.Label);
            Token label = input.Expect(TokenType.Identifier);
            input.Expect(TokenType.Label);
            return new LabelItem(label.Value) { Debug = debug };
        }
        /// <summary>
        /// Reads a break statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadBreak(Tokenizer input)
        {
            var ret = input.Expect(TokenType.Break);
            return new GotoItem("<break>") { Debug = ret };
        }
        /// <summary>
        /// Reads a goto statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadGoto(Tokenizer input)
        {
            var debug = input.Expect(TokenType.Goto);
            var name = input.Expect(TokenType.Identifier);
            return new GotoItem(name.Value) { Debug = debug };
        }
        /// <summary>
        /// Reads a do statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadDo(Tokenizer input)
        {
            var debug = input.Expect(TokenType.Do);
            var ret = ReadBlock(input);
            input.Expect(TokenType.End);
            return ret;
        }
        /// <summary>
        /// Reads a while statement from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The object that was read.</returns>
        protected virtual IParseStatement ReadWhile(Tokenizer input)
        {
            var debug = input.Expect(TokenType.While);
            WhileItem ret = new WhileItem() { Debug = debug };
            bool ignored;
            ret.Exp = ReadExp(input, out ignored);
            input.Expect(TokenType.Do);
            ret.Block = ReadBlock(input);
            input.Expect(TokenType.End);
            return ret;
        }

        #endregion

        #region Read Helpers

        /// <summary>
        /// Reads an assignment statement from the input.  The input is currently
        /// after the first name, on the comma or equal sign.  The debug token
        /// contains the name.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="debug">The first name.</param>
        /// <param name="local">True if this is a local definition, otherwise false.</param>
        /// <param name="variable">The first variable that was read.</param>
        /// <returns>The statement that was read.</returns>
        protected virtual AssignmentItem ReadAssignment(Tokenizer input, Token debug, bool local, IParseVariable variable)
        {
            AssignmentItem assign = new AssignmentItem(local) { Debug = debug };
            assign.AddName(variable);
            while (input.ReadIfType(TokenType.Comma))
            {
                bool ignored;
                var exp = ReadExp(input, out ignored);
                if ((local && !(exp is NameItem)) || (!local && !(exp is IParseVariable)))
                    throw new SyntaxException(Resources.NameOrExpForVar, input.Name, debug);
                assign.AddName((IParseVariable)exp);
            }

            if (input.ReadIfType(TokenType.Assign))
            {
                bool isParentheses;
                assign.AddItem(ReadExp(input, out isParentheses));

                while (input.ReadIfType(TokenType.Comma))
                {
                    assign.AddItem(ReadExp(input, out isParentheses));
                }
                assign.IsLastExpressionSingle = isParentheses;
            }
            else if (!local)
            {
                throw new SyntaxException(
                    string.Format(Resources.InvalidDefinition, "assignment"),
                    input.Name, debug);
            }

            return assign;
        }
        /// <summary>
        /// Reads a prefix-expression from the input.
        /// </summary>
        /// <param name="input">The input to read from.</param>
        /// <returns>The parsed expression.</returns>
        protected virtual IParseExp ReadPrefixExp(Tokenizer input, out bool isParentheses)
        {
            Token debug = input.Peek();
            bool ignored;
            IParseExp ret;
            isParentheses = false;
            if (input.ReadIfType(TokenType.BeginParen))
            {
                isParentheses = true;
                ret = ReadExp(input, out ignored);
                input.Expect(TokenType.EndParen);
            }
            else
            {
                Token name = input.Expect(TokenType.Identifier);
                ret = new NameItem(name.Value) { Debug = name };
            }

            while (true)
            {
                if (input.ReadIfType(TokenType.BeginBracket))
                {
                    isParentheses = false;
                    var temp = ReadExp(input, out ignored);
                    ret = new IndexerItem(ret, temp) { Debug = debug };
                    input.Expect(TokenType.EndBracket);
                }
                else if (input.ReadIfType(TokenType.Indexer))
                {
                    isParentheses = false;
                    var token = input.Expect(TokenType.Identifier);
                    var name = new LiteralItem(token.Value) { Debug = token };
                    ret = new IndexerItem(ret, name) { Debug = debug };
                }
                else
                {
                    string instName = null;
                    int overload = -1;
                    if (input.ReadIfType(TokenType.Colon))
                    {
                        instName = input.Expect(TokenType.Identifier).Value;
                        int idx = instName.IndexOf('`');
                        if (idx >= 0)
                        {
                            if (!int.TryParse(instName.Substring(idx + 1), out overload))
                                input.SyntaxError(Resources.OnlyNumbersInOverload);
                            instName = instName.Substring(0, idx - 1);
                        }
                    }
                    else if (ret is NameItem)
                    {
                        NameItem name = ret as NameItem;
                        int idx = name.Name.IndexOf('`');
                        if (idx >= 0)
                        {
                            if (!int.TryParse(name.Name.Substring(idx + 1), out overload))
                                input.SyntaxError(Resources.OnlyNumbersInOverload);
                            name.Name = name.Name.Substring(0, idx - 1);
                        }
                    }

                    var func = new FuncCallItem(ret, instName, overload) { Debug = debug };
                    if (input.PeekType(TokenType.BeginTable))
                    {
                        func.AddItem(ReadTable(input), false);
                    }
                    else if (input.PeekType(TokenType.StringLiteral))
                    {
                        Token token = input.Expect(TokenType.StringLiteral);
                        func.AddItem(new LiteralItem(token.Value) { Debug = token }, false);
                    }
                    else if (input.ReadIfType(TokenType.BeginParen))
                    {
                        if (!input.PeekType(TokenType.EndParen))
                        {
                            bool isLastSingle = false;
                            do
                            {
                                bool isRef = input.ReadIfType(TokenType.Ref);
                                bool isRefParen = false;
                                if (isRef)
                                    isRefParen = input.ReadIfType(TokenType.BeginParen);
                                else
                                    isRef = input.ReadIfType(TokenType.RefSymbol);

                                func.AddItem(ReadExp(input, out isLastSingle), isRef);
                                if (isRefParen)
                                    input.Expect(TokenType.EndParen);
                            } while (input.ReadIfType(TokenType.Comma));
                            func.IsLastArgSingle = isLastSingle;
                        }
                        input.Expect(TokenType.EndParen);
                    }
                    else
                    {
                        break;
                    }
                    isParentheses = false;
                    ret = func;
                }
            }
            return ret;
        }
        /// <summary>
        /// Reads an expression from the input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="precedence">
        /// The precedence of the previous expression or -1 if a root.
        /// </param>
        /// <returns>The expression that was read.</returns>
        protected virtual IParseExp ReadExp(Tokenizer input, out bool isParentheses, int precedence = -1)
        {
            Token debug = input.Peek();
            bool ignored;
            IParseExp ret = null;
            var unOpType = GetUnaryOperationType(input.Peek().Type);
            isParentheses = false;
            if (unOpType != UnaryOperationType.Unknown)
            {
                input.Read();
                int unaryPrec = 11;
                if (unaryPrec > precedence && precedence >= 0)
                    unaryPrec = precedence;
                ret = new UnOpItem(ReadExp(input, out ignored, unaryPrec),
                                   unOpType) { Debug = debug };
            }
            else if (input.ReadIfType(TokenType.Nil))
            {
                ret = new LiteralItem(null) { Debug = debug };
            }
            else if (input.ReadIfType(TokenType.False))
            {
                ret = new LiteralItem(false) { Debug = debug };
            }
            else if (input.ReadIfType(TokenType.True))
            {
                ret = new LiteralItem(true) { Debug = debug };
            }
            else if (input.ReadIfType(TokenType.NumberLiteral))
            {
                ret = new LiteralItem(Helpers.ParseNumber(debug.Value)) { Debug = debug };
            }
            else if (input.ReadIfType(TokenType.StringLiteral))
            {
                ret = new LiteralItem(debug.Value) { Debug = debug };
            }
            else if (input.ReadIfType(TokenType.Elipsis))
            {
                ret = new NameItem("...") { Debug = debug };
            }
            else if (input.PeekType(TokenType.BeginTable))
            {
                ret = ReadTable(input);
            }
            else if (input.PeekType(TokenType.Function))
            {
                ret = ReadFunctionHelper(input, false, false);
            }
            else
            {
                ret = ReadPrefixExp(input, out isParentheses);
            }

            while (true)
            {
                BinaryOperationType binOpType = GetBinaryOperationType(input.Peek().Type);
                int newPrecedence = GetPrecedence(binOpType);
                if (binOpType == BinaryOperationType.Unknown ||
                    (newPrecedence < precedence && precedence >= 0))
                {
                    break;
                }
                input.Read();

                // For left-associative operations, use a lower precedence so the nested call
                // doesn't read more than it should.  a+b+c should be (a+b)+c, so we need the
                // first add to be its own item and then have that should be the lhs of another
                // add.  Note this only works if operations of the same precedence have the same
                // associativity.
                int extra = IsRightAssociative(binOpType) ? 0 : 1;
                IParseExp other = ReadExp(input, out ignored, newPrecedence + extra);
                ret = new BinOpItem(ret, binOpType, other) { Debug = debug };
                isParentheses = false;
            }
            return ret;
        }
        /// <summary>
        /// Reads a function from the input.  Input must be on the word
        /// 'function'.  If canName is true, it will give the function the
        /// read name; otherwise it will give it a null name.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="canName">True if the function can have a name, otherwise false.</param>
        /// <param name="local">True if this function is a local definition, otherwise false.</param>
        /// <returns>The function definition that was read.</returns>
        protected virtual FuncDefItem ReadFunctionHelper(Tokenizer input, bool canName, bool local)
        {
            Token debug = input.Expect(TokenType.Function);

            IParseVariable name = null;
            string inst = null;
            if (input.PeekType(TokenType.Identifier))
            {
                Token temp = input.Expect(TokenType.Identifier);
                name = new NameItem(temp.Value) { Debug = temp };
                while (input.ReadIfType(TokenType.Indexer))
                {
                    temp = input.Expect(TokenType.Identifier);
                    var literal = new LiteralItem(temp.Value) { Debug = temp };
                    name = new IndexerItem(name, literal) { Debug = name.Debug };
                }

                if (input.ReadIfType(TokenType.Colon))
                {
                    inst = input.Expect(TokenType.Identifier).Value;
                }
            }
            if (name != null && !canName)
                throw new SyntaxException(Resources.FunctionCantHaveName, input.Name, debug);

            FuncDefItem ret = new FuncDefItem(name, local) { Debug = debug, InstanceName = inst };
            input.Expect(TokenType.BeginParen);
            while (!input.PeekType(TokenType.EndParen))
            {
                Token temp = input.PeekType(TokenType.Elipsis) ?
                                 input.Expect(TokenType.Elipsis) :
                                 input.Expect(TokenType.Identifier);
                ret.AddArgument(new NameItem(temp.Value) { Debug = temp });

                if (!input.PeekType(TokenType.EndParen))
                    input.Expect(TokenType.Comma);
            }
            input.Expect(TokenType.EndParen);

            BlockItem chunk = ReadBlock(input);
            chunk.Return = chunk.Return ?? new ReturnItem();
            ret.Block = chunk;
            input.Expect(TokenType.End);
            return ret;
        }
        /// <summary>
        /// Reads a table from the input.  Input must be either on the starting '{'.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <returns>The table that was read.</returns>
        protected virtual TableItem ReadTable(Tokenizer input)
        {
            bool ignored;
            Token debug = input.Expect(TokenType.BeginTable);

            TableItem ret = new TableItem() { Debug = debug };
            Token last = input.Peek();
            while (!input.PeekType(TokenType.EndTable))
            {
                if (input.ReadIfType(TokenType.BeginBracket))
                {
                    var temp = ReadExp(input, out ignored);
                    input.Expect(TokenType.EndBracket);
                    input.Expect(TokenType.Assign);
                    var val = ReadExp(input, out ignored);
                    ret.AddItem(temp, val);
                }
                else
                {
                    var val = ReadExp(input, out ignored);
                    if (input.ReadIfType(TokenType.Assign))
                    {
                        NameItem name = val as NameItem;
                        if (name == null)
                            throw new SyntaxException(string.Format(Resources.InvalidDefinition, "table"),
                                input.Name, debug);

                        // read the expression
                        var exp = ReadExp(input, out ignored);
                        ret.AddItem(new LiteralItem(name.Name), exp);
                    }
                    else
                    {
                        ret.AddItem(null, val);
                    }
                }

                if (!input.ReadIfType(TokenType.Comma) &&
                    !input.ReadIfType(TokenType.Semicolon))
                {
                    break;
                }
            }
            input.Expect(TokenType.EndTable);
            return ret;
        }

        #endregion

        #region Helper Functions

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
                case BinaryOperationType.Or:
                    return 1;
                case BinaryOperationType.And:
                    return 2;
                case BinaryOperationType.Gt:
                case BinaryOperationType.Lt:
                case BinaryOperationType.Gte:
                case BinaryOperationType.Lte:
                case BinaryOperationType.Equals:
                case BinaryOperationType.NotEquals:
                    return 3;
                case BinaryOperationType.Concat:
                    return 8;
                case BinaryOperationType.Add:
                case BinaryOperationType.Subtract:
                    return 9;
                case BinaryOperationType.Multiply:
                case BinaryOperationType.Divide:
                case BinaryOperationType.Modulo:
                    return 10;
                case BinaryOperationType.Power:
                    return 12;
                default:
                    return -1;
            }
        }
        /// <summary>
        /// Returns the UnaryOperationType for the given token.
        /// </summary>
        protected static UnaryOperationType GetUnaryOperationType(TokenType type)
        {
            switch (type)
            {
                case TokenType.Subtract:
                    return UnaryOperationType.Minus;
                case TokenType.Not:
                    return UnaryOperationType.Not;
                case TokenType.Length:
                    return UnaryOperationType.Length;
                default:
                    return UnaryOperationType.Unknown;
            }
        }
        /// <summary>
        /// Returns the BinaryOperationType for the given token.
        /// </summary>
        protected static BinaryOperationType GetBinaryOperationType(TokenType type)
        {
            switch (type)
            {
                case TokenType.Add:
                    return BinaryOperationType.Add;
                case TokenType.Subtract:
                    return BinaryOperationType.Subtract;
                case TokenType.Multiply:
                    return BinaryOperationType.Multiply;
                case TokenType.Divide:
                    return BinaryOperationType.Divide;
                case TokenType.Power:
                    return BinaryOperationType.Power;
                case TokenType.Modulo:
                    return BinaryOperationType.Modulo;
                case TokenType.Concat:
                    return BinaryOperationType.Concat;
                case TokenType.Greater:
                    return BinaryOperationType.Gt;
                case TokenType.GreaterEquals:
                    return BinaryOperationType.Gte;
                case TokenType.Less:
                    return BinaryOperationType.Lt;
                case TokenType.LessEquals:
                    return BinaryOperationType.Lte;
                case TokenType.Equals:
                    return BinaryOperationType.Equals;
                case TokenType.NotEquals:
                    return BinaryOperationType.NotEquals;
                case TokenType.And:
                    return BinaryOperationType.And;
                case TokenType.Or:
                    return BinaryOperationType.Or;
                default:
                    return BinaryOperationType.Unknown;
            }
        }
        /// <summary>
        /// Returns whether the given operation is right associative.
        /// </summary>
        protected static bool IsRightAssociative(BinaryOperationType type)
        {
            switch (type)
            {
                case BinaryOperationType.Concat:
                case BinaryOperationType.Power:
                    return true;
                default:
                    return false;
            }
        }

        #endregion
    }
}
