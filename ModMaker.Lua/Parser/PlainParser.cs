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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Parser {
  /// <summary>
  /// Defines a default parser.  This parses Lua code into an IParseItem tree. This can be extended
  /// to modify its behavior.
  /// </summary>
  public class PlainParser : IParser {
    /// <summary>
    /// Contains the read functions that are indexed by the first token text.
    /// </summary>
    readonly IDictionary<TokenType, ReadStatement> _functions =
        new Dictionary<TokenType, ReadStatement>();

    /// <summary>
    /// A delegate that reads a statement from the input and returns the object that was read.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected delegate IParseStatement ReadStatement(Lexer input, LocalsResolver resolver);

    /// <summary>
    /// Creates a new parser object.
    /// </summary>
    public PlainParser() {
      _functions.Add(TokenType.Label, _readLabel);
      _functions.Add(TokenType.If, _readIf);
      _functions.Add(TokenType.For, _readFor);
      _functions.Add(TokenType.Class, _readClass);
      _functions.Add(TokenType.Function, _readFunction);
      _functions.Add(TokenType.Local, _readLocal);
      _functions.Add(TokenType.Break, _readBreak);
      _functions.Add(TokenType.Repeat, _readRepeat);
      _functions.Add(TokenType.Goto, _readGoto);
      _functions.Add(TokenType.Do, _readDo);
      _functions.Add(TokenType.While, _readWhile);
    }

    public GlobalItem Parse(Stream input, Encoding? encoding, string name) {
      return ParseCommon(new BufferedStringReader(input, encoding), name);
    }

    public GlobalItem Parse(string input, string name) {
      return ParseCommon(new BufferedStringReader(input), name);
    }

    public GlobalItem ParseCommon(BufferedStringReader input, string name) {
      var messages = new CompilerMessageCollection(MessageLevel.Error);
      var lexer = new Lexer(messages, input, name);
      var resolver = new LocalsResolver(messages);

      // parse the chunk
      GlobalItem ret;
      using (resolver.DefineFunction()) {
        Token start = lexer.Peek();
        BlockItem read = _readBlock(lexer, resolver);
        if (!lexer.PeekType(TokenType.None)) {
          lexer.SyntaxError(MessageId.ExpectingEof);
        }
        ret = new GlobalItem(read) {
          Debug = _makeDebug(lexer, start),
          FunctionInformation = resolver.GetFunctionInfo(),
        };
      }

      if (messages.ShouldThrow())
        throw messages.MakeException();
      return ret;
    }

    #region Read Functions

    /// <summary>
    /// Reads a block of code from the input.  Any end tokens should not be read and are handled by
    /// the parent call (e.g. 'end' or 'until').
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The item that was read.</returns>
    protected virtual BlockItem _readBlock(Lexer input, LocalsResolver resolver) {
      var debug = input.Peek();
      IList<IParseStatement> statements = new List<IParseStatement>();

      Token cur;
      bool includeReturn = true;
      using (resolver.DefineBlock()) {
        while ((cur = input.Peek()).Type != TokenType.None) {
          if (_functions.ContainsKey(cur.Type)) {
            statements.Add(_functions[cur.Type](input, resolver));
          } else if (cur.Type == TokenType.Return) {
            var ret = _readReturn(input, resolver);
            return new BlockItem(statements.ToArray()) {
              Return = ret,
              Debug = _makeDebug(input, debug),
            };
          } else if (cur.Type == TokenType.Semicolon) {
            input.Expect(TokenType.Semicolon);
          } else if (cur.Type == TokenType.End || cur.Type == TokenType.Else ||
                     cur.Type == TokenType.ElseIf || cur.Type == TokenType.Until) {
            // Don't read as it will be handled by the parent or the current block, this end belongs
            // to the parent.
            includeReturn = false;
            break;
          } else {
            IParseExp exp = _readExp(input, resolver, out _);
            if (exp is FuncCallItem funcCall) {
              funcCall.Statement = true;
              statements.Add(funcCall);
            } else if (exp is NameItem || exp is IndexerItem) {
              statements.Add(_readAssignment(input, resolver, cur, false, (IParseVariable)exp));
            } else {
              input.SyntaxError(MessageId.ExpectedStatementStart, cur);
            }
          }
        }
      }

      // Only gets here if this is the global function
      return new BlockItem(statements.ToArray()) {
        Return = includeReturn ? new ReturnItem() : null,
        Debug = _makeDebug(input, debug),
      };
    }

    /// <summary>
    /// Reads a local statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readLocal(Lexer input, LocalsResolver resolver) {
      Token debug = input.Expect(TokenType.Local);
      if (input.PeekType(TokenType.Function)) {
        FuncDefItem ret = _readFunctionHelper(input, resolver, canName: true, local: true);
        ret.Debug = _makeDebug(input, debug);
        resolver.DefineLocals(new[] { (NameItem)ret.Prefix! });
        return ret;
      } else {
        Token name = input.Expect(TokenType.Identifier);
        NameItem nameItem = new NameItem(name.Value) { Debug = _makeDebug(input, name) };
        var ret = _readAssignment(input, resolver, debug, local: true, nameItem);
        resolver.DefineLocals(ret.Names.Cast<NameItem>());
        return ret;
      }
    }
    /// <summary>
    /// Reads a class statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readClass(Lexer input, LocalsResolver resolver) {
      Token debug = input.Expect(TokenType.Class);
      var implements = new List<IParseExp>();

      NameItem className;
      if (input.PeekType(TokenType.StringLiteral)) {
        // class "foo"
        // class "foo" (a, b, c)
        Token name = input.Expect(TokenType.StringLiteral);
        className = new NameItem(name.Value) { Debug = _makeDebug(input, name) };
        if (input.ReadIfType(TokenType.BeginParen)) {
          do {
            implements.Add(_readExp(input, resolver, out _));
          } while (input.ReadIfType(TokenType.Comma));
          input.Expect(TokenType.EndParen);
        }
      } else {
        // class foo
        // class foo : a, b, c
        Token name = input.Expect(TokenType.Identifier);
        className = new NameItem(name.Value) { Debug = _makeDebug(input, name) };
        if (input.PeekType(TokenType.Colon)) {
          do {
            input.Read();  // Skip the ':' or ','
            implements.Add(_readExp(input, resolver, out _));
          } while (input.PeekType(TokenType.Comma));
        }
      }
      resolver.ResolveName(className.Name);

      return new ClassDefItem(className, implements.ToArray()) {
        Debug = _makeDebug(input, debug),
      };
    }
    /// <summary>
    /// Reads a return statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual ReturnItem _readReturn(Lexer input, LocalsResolver resolver) {
      Token debug = input.Expect(TokenType.Return);
      var values = new List<IParseExp>();

      bool isParentheses = false;
      if (!input.PeekType(TokenType.End) && !input.PeekType(TokenType.Until) &&
          !input.PeekType(TokenType.ElseIf) && !input.PeekType(TokenType.Else) &&
          !input.PeekType(TokenType.None)) {
        values.Add(_readExp(input, resolver, out isParentheses));
        while (input.ReadIfType(TokenType.Comma)) {
          values.Add(_readExp(input, resolver, out isParentheses));
        }

        input.ReadIfType(TokenType.Semicolon);
      }

      return new ReturnItem(values.ToArray()) {
        Debug = _makeDebug(input, debug),
        IsLastExpressionSingle = isParentheses,
      };
    }
    /// <summary>
    /// Reads a normal function statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readFunction(Lexer input, LocalsResolver resolver) {
      return _readFunctionHelper(input, resolver, canName: true, local: false);
    }
    /// <summary>
    /// Reads a for statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readFor(Lexer input, LocalsResolver resolver) {
      Token debug = input.Expect(TokenType.For);
      Token name = input.Expect(TokenType.Identifier);
      var nameItem = new NameItem(name.Value) { Debug = _makeDebug(input, name) };

      if (input.ReadIfType(TokenType.Assign)) {
        // Numeric for
        IParseExp start = _readExp(input, resolver, out _);
        input.Expect(TokenType.Comma);
        IParseExp limit = _readExp(input, resolver, out _);

        IParseExp? step = null;
        if (input.ReadIfType(TokenType.Comma)) {
          step = _readExp(input, resolver, out _);
        }

        using (resolver.DefineBlock()) {
          var @break = new LabelItem("<break>");
          resolver.DefineLocals(new[] { nameItem });
          resolver.DefineLabel(@break);

          Token do_ = input.Expect(TokenType.Do);
          var block = _readBlock(input, resolver);
          Token end = input.Expect(TokenType.End);
          return new ForNumItem(nameItem, start, limit, step, block) {
            Break = @break,
            Debug = _makeDebug(input, debug),
            ForDebug = _makeDebug(input, debug, do_),
            EndDebug = _makeDebug(input, end),
          };
        }
      } else {
        // Generic for statement

        // Read the variables
        var names = new List<NameItem>() { nameItem };
        while (input.ReadIfType(TokenType.Comma)) {
          Token token = input.Expect(TokenType.Identifier);
          names.Add(new NameItem(token.Value) { Debug = _makeDebug(input, token) });
        }
        input.Expect(TokenType.In);

        // Read the expression-list
        var exps = new List<IParseExp>();
        exps.Add(_readExp(input, resolver, out _));
        while (input.ReadIfType(TokenType.Comma)) {
          exps.Add(_readExp(input, resolver, out _));
        }

        using (resolver.DefineBlock()) {
          var @break = new LabelItem("<break>");
          resolver.DefineLocals(names);
          resolver.DefineLabel(@break);

          Token do_ = input.Expect(TokenType.Do);
          var block = _readBlock(input, resolver);
          Token end = input.Expect(TokenType.End);
          return new ForGenItem(names.ToArray(), exps.ToArray(), block) {
            Break = @break,
            Debug = _makeDebug(input, debug),
            ForDebug = _makeDebug(input, debug, do_),
            EndDebug = _makeDebug(input, end),
          };
        }
      }
    }
    /// <summary>
    /// Reads an if statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readIf(Lexer input, LocalsResolver resolver) {
      Token debug = input.Expect(TokenType.If);

      var exp = _readExp(input, resolver, out _);
      Token firstThen = input.Expect(TokenType.Then);
      var block = _readBlock(input, resolver);

      var elseIfs = new List<IfItem.ElseInfo>();
      while (true) {
        Token elseIf = input.Peek();
        if (!input.ReadIfType(TokenType.ElseIf))
          break;

        IParseExp elseExp = _readExp(input, resolver, out _);
        Token then = input.Expect(TokenType.Then);
        BlockItem elseIfBlock = _readBlock(input, resolver);
        elseIfs.Add(new IfItem.ElseInfo(elseExp, elseIfBlock, _makeDebug(input, elseIf, then)));
      }

      BlockItem? elseBlock = null;
      Token elseToken = input.Peek();
      if (input.ReadIfType(TokenType.Else)) {
        elseBlock = _readBlock(input, resolver);
      }
      Token end = input.Expect(TokenType.End);
      return new IfItem(exp, block, elseIfs.ToArray(), elseBlock) {
        Debug = _makeDebug(input, debug),
        IfDebug = _makeDebug(input, debug, firstThen),
        ElseDebug = elseToken.Type == TokenType.Else ? _makeDebug(input, elseToken, elseToken)
                                                     : new DebugInfo(),
        EndDebug = _makeDebug(input, end),
      };
    }
    /// <summary>
    /// Reads a repeat statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readRepeat(Lexer input, LocalsResolver resolver) {
      Token debug = input.Expect(TokenType.Repeat);
      using (resolver.DefineBlock()) {
        var @break = new LabelItem("<break>");
        resolver.DefineLabel(@break);

        var block = _readBlock(input, resolver);
        Token repeat = input.Expect(TokenType.Until);
        var exp = _readExp(input, resolver, out _);
        return new RepeatItem(exp, block) {
          Break = @break,
          Debug = _makeDebug(input, debug),
          RepeatDebug = _makeDebug(input, debug, debug),
          UntilDebug = _makeDebug(input, repeat),
        };
      }
    }
    /// <summary>
    /// Reads a label statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readLabel(Lexer input, LocalsResolver resolver) {
      Token debug = input.Expect(TokenType.Label);
      Token label = input.Expect(TokenType.Identifier);
      input.Expect(TokenType.Label);
      var ret = new LabelItem(label.Value) { Debug = _makeDebug(input, debug) };
      resolver.DefineLabel(ret);
      return ret;
    }
    /// <summary>
    /// Reads a break statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readBreak(Lexer input, LocalsResolver resolver) {
      Token debug = input.Expect(TokenType.Break);
      var ret = new GotoItem("<break>") { Debug = _makeDebug(input, debug) };
      resolver.DefineGoto(ret);
      return ret;
    }
    /// <summary>
    /// Reads a goto statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readGoto(Lexer input, LocalsResolver resolver) {
      Token debug = input.Expect(TokenType.Goto);
      Token name = input.Expect(TokenType.Identifier);
      var ret = new GotoItem(name.Value) { Debug = _makeDebug(input, debug) };
      resolver.DefineGoto(ret);
      return ret;
    }
    /// <summary>
    /// Reads a do statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readDo(Lexer input, LocalsResolver resolver) {
      input.Expect(TokenType.Do);
      BlockItem ret = _readBlock(input, resolver);
      input.Expect(TokenType.End);
      return ret;
    }
    /// <summary>
    /// Reads a while statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readWhile(Lexer input, LocalsResolver resolver) {
      Token debug = input.Expect(TokenType.While);
      using (resolver.DefineBlock()) {
        var @break = new LabelItem("<break>");
        resolver.DefineLabel(@break);

        var exp = _readExp(input, resolver, out _);
        Token do_ = input.Expect(TokenType.Do);
        var block = _readBlock(input, resolver);
        Token end = input.Expect(TokenType.End);
        return new WhileItem(exp, block) {
          Break = @break,
          Debug = _makeDebug(input, debug),
          WhileDebug = _makeDebug(input, debug, do_),
          EndDebug = _makeDebug(input, end, end),
        };
      }
    }

    #endregion

    #region Read Helpers

    /// <summary>
    /// Reads an assignment statement from the input.  The input is currently after the first name,
    /// on the comma or equal sign.  The debug token contains the name.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <param name="debug">The first name.</param>
    /// <param name="local">True if this is a local definition, otherwise false.</param>
    /// <param name="variable">The first variable that was read.</param>
    /// <returns>The statement that was read.</returns>
    protected virtual AssignmentItem _readAssignment(Lexer input, LocalsResolver resolver,
                                                     Token debug, bool local,
                                                     IParseVariable variable) {
      var names = new List<IParseVariable>() { variable };
      while (input.ReadIfType(TokenType.Comma)) {
        var curDebug = input.Peek();
        var exp = _readExp(input, resolver, out _);
        if (local && !(exp is NameItem)) {
          input.SyntaxError(MessageId.LocalMustBeIdentifier, curDebug);
          throw input.MakeException();
        }
        if (!local && !(exp is IParseVariable)) {
          input.SyntaxError(MessageId.AssignmentMustBeVariable, curDebug);
          throw input.MakeException();
        }
        names.Add((IParseVariable)exp);
      }

      bool isParentheses = false;
      var exps = new List<IParseExp>();
      if (input.ReadIfType(TokenType.Assign)) {
        exps.Add(_readExp(input, resolver, out isParentheses));

        while (input.ReadIfType(TokenType.Comma)) {
          exps.Add(_readExp(input, resolver, out isParentheses));
        }
      } else if (!local) {
        input.Expect(TokenType.Assign);
      }

      return new AssignmentItem(names.ToArray(), exps.ToArray()) {
        Debug = _makeDebug(input, debug),
        Local = local,
        IsLastExpressionSingle = isParentheses,
      };
    }
    /// <summary>
    /// Reads a prefix-expression from the input.
    /// </summary>
    /// <param name="input">The input to read from.</param>
    /// <returns>The parsed expression.</returns>
    protected virtual IParseExp _readPrefixExp(Lexer input, LocalsResolver resolver,
                                               out bool isParentheses) {
      Token debug = input.Peek();
      IParseExp ret;
      if (input.ReadIfType(TokenType.BeginParen)) {
        isParentheses = true;
        ret = _readExp(input, resolver, out _);
        input.Expect(TokenType.EndParen);
      } else {
        isParentheses = false;
        Token name = input.Expect(TokenType.Identifier);
        resolver.ResolveName(name.Value);
        ret = new NameItem(name.Value) { Debug = _makeDebug(input, name) };
      }

      while (true) {
        if (input.ReadIfType(TokenType.BeginBracket)) {
          isParentheses = false;
          IParseExp temp = _readExp(input, resolver, out _);
          ret = new IndexerItem(ret, temp) { Debug = _makeDebug(input, debug) };
          input.Expect(TokenType.EndBracket);
        } else if (input.ReadIfType(TokenType.Indexer)) {
          isParentheses = false;
          Token token = input.Expect(TokenType.Identifier);
          var name = new LiteralItem(token.Value) { Debug = _makeDebug(input, token) };
          ret = new IndexerItem(ret, name) { Debug = _makeDebug(input, debug) };
        } else {
          string? instName = null;
          if (input.ReadIfType(TokenType.Colon)) {
            instName = input.Expect(TokenType.Identifier).Value;
          }

          bool isLastSingle = false;
          var args = new List<FuncCallItem.ArgumentInfo>();
          if (input.PeekType(TokenType.BeginTable)) {
            IParseExp table = _readTable(input, resolver);
            args.Add(new FuncCallItem.ArgumentInfo(table, false));
          } else if (input.PeekType(TokenType.StringLiteral)) {
            Token token = input.Expect(TokenType.StringLiteral);
            args.Add(new FuncCallItem.ArgumentInfo(new LiteralItem(token.Value) {
                                                     Debug = _makeDebug(input, token),
                                                   },
                                                   false));
          } else if (input.ReadIfType(TokenType.BeginParen)) {
            if (!input.PeekType(TokenType.EndParen)) {
              do {
                bool isRef = input.ReadIfType(TokenType.Ref);
                bool isRefParen = false;
                if (isRef) {
                  isRefParen = input.ReadIfType(TokenType.BeginParen);
                } else {
                  isRef = input.ReadIfType(TokenType.RefSymbol);
                }

                args.Add(new FuncCallItem.ArgumentInfo(
                    _readExp(input, resolver, out isLastSingle), isRef));
                if (isRefParen) {
                  input.Expect(TokenType.EndParen);
                }
              } while (input.ReadIfType(TokenType.Comma));
            }
            input.Expect(TokenType.EndParen);
          } else {
            break;
          }
          isParentheses = false;
          ret = new FuncCallItem(ret, args.ToArray()) {
            Debug = _makeDebug(input, debug),
            InstanceName = instName,
            IsLastArgSingle = isLastSingle,
          };
        }
      }
      return ret;
    }
    /// <summary>
    /// Reads an expression from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <param name="precedence">The precedence of the previous expression or -1 if a root.</param>
    /// <returns>The expression that was read.</returns>
    protected virtual IParseExp _readExp(Lexer input, LocalsResolver resolver,
                                         out bool isParentheses, int precedence = -1) {
      Token debug = input.Peek();
      IParseExp ret;
      var unOpType = _getUnaryOperationType(debug.Type);
      isParentheses = false;
      if (unOpType != UnaryOperationType.Unknown) {
        input.Read();
        int unaryPrec = 11;
        if (unaryPrec > precedence && precedence >= 0) {
          unaryPrec = precedence;
        }
        ret = new UnOpItem(_readExp(input, resolver, out _, unaryPrec), unOpType) {
          Debug = _makeDebug(input, debug),
        };
      } else if (input.ReadIfType(TokenType.Nil)) {
        ret = new LiteralItem(null) { Debug = _makeDebug(input, debug) };
      } else if (input.ReadIfType(TokenType.False)) {
        ret = new LiteralItem(false) { Debug = _makeDebug(input, debug) };
      } else if (input.ReadIfType(TokenType.True)) {
        ret = new LiteralItem(true) { Debug = _makeDebug(input, debug) };
      } else if (input.ReadIfType(TokenType.NumberLiteral)) {
        ret = new LiteralItem(Helpers.ParseNumber(debug.Value)) {
          Debug = _makeDebug(input, debug),
        };
      } else if (input.ReadIfType(TokenType.StringLiteral)) {
        ret = new LiteralItem(debug.Value) { Debug = _makeDebug(input, debug) };
      } else if (input.ReadIfType(TokenType.Elipsis)) {
        ret = new NameItem("...") { Debug = _makeDebug(input, debug) };
      } else if (input.PeekType(TokenType.BeginTable)) {
        ret = _readTable(input, resolver);
      } else if (input.PeekType(TokenType.Function)) {
        ret = _readFunctionHelper(input, resolver, false, false);
      } else {
        ret = _readPrefixExp(input, resolver, out isParentheses);
      }

      while (true) {
        BinaryOperationType binOpType = _getBinaryOperationType(input.Peek().Type);
        int newPrecedence = _getPrecedence(binOpType);
        if (binOpType == BinaryOperationType.Unknown ||
            (newPrecedence < precedence && precedence >= 0)) {
          break;
        }
        input.Read();

        // For left-associative operations, use a lower precedence so the nested call doesn't read
        // more than it should.  a+b+c should be (a+b)+c, so we need the first add to be its own
        // item and then have that should be the lhs of another add.  Note this only works if
        // operations of the same precedence have the same associativity.
        int extra = _isRightAssociative(binOpType) ? 0 : 1;
        IParseExp other = _readExp(input, resolver, out _, newPrecedence + extra);
        ret = new BinOpItem(ret, binOpType, other) {
          Debug = _makeDebug(input, debug),
        };
        isParentheses = false;
      }
      return ret;
    }
    /// <summary>
    /// Reads a function from the input.  Input must be on the word 'function'.  If canName is true,
    /// it will give the function the read name; otherwise it will give it a null name.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <param name="canName">True if the function can have a name, otherwise false.</param>
    /// <param name="local">True if this function is a local definition, otherwise false.</param>
    /// <returns>The function definition that was read.</returns>
    protected virtual FuncDefItem _readFunctionHelper(Lexer input, LocalsResolver resolver,
                                                      bool canName, bool local) {
      Token debug = input.Expect(TokenType.Function);

      IParseVariable? name = null;
      string? instName = null;
      if (input.PeekType(TokenType.Identifier)) {
        Token temp = input.Expect(TokenType.Identifier);
        resolver.ResolveName(temp.Value);
        name = new NameItem(temp.Value) { Debug = _makeDebug(input, temp) };
        while (input.ReadIfType(TokenType.Indexer)) {
          temp = input.Expect(TokenType.Identifier);
          var literal = new LiteralItem(temp.Value) { Debug = _makeDebug(input, temp) };
          name = new IndexerItem(name, literal) { Debug = _makeDebug(input, debug) };
        }

        if (input.ReadIfType(TokenType.Colon)) {
          instName = input.Expect(TokenType.Identifier).Value;
        }
      }
      if (name != null && !canName) {
        input.SyntaxError(MessageId.FunctionNameWhenExpression, debug);
      }
      if (name == null && canName) {
        input.SyntaxError(MessageId.FunctionNameWhenStatement, debug);
      }

      var args = new List<NameItem>();
      input.Expect(TokenType.BeginParen);
      if (!input.PeekType(TokenType.EndParen)) {
        do {
          Token temp = input.PeekType(TokenType.Elipsis) ?
                           input.Expect(TokenType.Elipsis) :
                           input.Expect(TokenType.Identifier);
          args.Add(new NameItem(temp.Value) { Debug = _makeDebug(input, temp) });
          if (temp.Value == "...") {
            break;
          }
        } while (input.ReadIfType(TokenType.Comma));
      }
      input.Expect(TokenType.EndParen);
      using (resolver.DefineFunction()) {
        resolver.DefineLocals(args);

        BlockItem chunk = _readBlock(input, resolver);
        input.Expect(TokenType.End);
        chunk.Return ??= new ReturnItem();

        return new FuncDefItem(args.ToArray(), chunk) {
          Debug = _makeDebug(input, debug),
          FunctionInformation = resolver.GetFunctionInfo(),
          InstanceName = instName,
          Prefix = name,
          Local = local,
        };
      }
    }
    /// <summary>
    /// Reads a table from the input.  Input must be either on the starting '{'.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The table that was read.</returns>
    protected virtual TableItem _readTable(Lexer input, LocalsResolver resolver) {
      Token debug = input.Expect(TokenType.BeginTable);

      double id = 1;
      var values = new List<KeyValuePair<IParseExp, IParseExp>>();
      while (!input.PeekType(TokenType.EndTable)) {
        if (input.ReadIfType(TokenType.BeginBracket)) {
          IParseExp temp = _readExp(input, resolver, out _);
          input.Expect(TokenType.EndBracket);
          input.Expect(TokenType.Assign);
          IParseExp val = _readExp(input, resolver, out _);
          values.Add(new KeyValuePair<IParseExp, IParseExp>(temp, val));
        } else {
          Token valToken = input.Peek();
          IParseExp val = _readExp(input, resolver, out _);
          if (input.ReadIfType(TokenType.Assign)) {
            if (!(val is NameItem name)) {
              input.SyntaxError(MessageId.TableKeyMustBeName, valToken);
              // Add a dummy value so we can return multiple errors.
              name = new NameItem("");
            }

            IParseExp exp = _readExp(input, resolver, out _);
            values.Add(new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(name.Name), exp));
          } else {
            values.Add(new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(id++), val));
          }
        }

        if (!input.ReadIfType(TokenType.Comma) && !input.ReadIfType(TokenType.Semicolon)) {
          break;
        }
      }
      input.Expect(TokenType.EndTable);
      return new TableItem(values.ToArray()) { Debug = _makeDebug(input, debug) };
    }

    #endregion

    #region Helper Functions

    /// <summary>
    /// Gets the precedence of the given operation.  A smaller number means that the operation
    /// should be applied before ones with larger numbers.
    /// </summary>
    /// <param name="type">The type to get for.</param>
    /// <returns>The precedence of the given operation.</returns>
    protected static int _getPrecedence(BinaryOperationType type) {
      switch (type) {
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
    protected static UnaryOperationType _getUnaryOperationType(TokenType type) {
      switch (type) {
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
    protected static BinaryOperationType _getBinaryOperationType(TokenType type) {
      switch (type) {
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
    protected static bool _isRightAssociative(BinaryOperationType type) {
      switch (type) {
        case BinaryOperationType.Concat:
        case BinaryOperationType.Power:
          return true;
        default:
          return false;
      }
    }
    /// <summary>
    /// Creates a DebugInfo based on the given Token.
    /// </summary>
    /// <param name="lexer">The input reader to use.</param>
    /// <param name="token">The token to use.</param>
    /// <param name="end">The end of the debug info, or null to use the current read pos.</param>
    /// <returns>The new DebugInfo instance.</returns>
    protected static DebugInfo _makeDebug(Lexer lexer, Token token, Token? end = null) {
      end ??= lexer.Previous;
      return new DebugInfo(lexer.Name, token.StartPos, token.StartLine, end.Value.EndPos,
                           end.Value.EndLine);
    }

    #endregion
  }
}
