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

using System;
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
    protected delegate IParseStatement ReadStatement(Lexer input);

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

    public IParseItem Parse(Stream input, Encoding encoding, string name) {
      var lexer = new Lexer(input, encoding, name);
      if (input == null) {
        throw new ArgumentNullException(nameof(input));
      }

      // parse the chunk
      IParseItem read = _readBlock(lexer);
      if (!lexer.PeekType(TokenType.None)) {
        throw lexer.SyntaxError(MessageId.ExpectingEof);
      }

      return read;
    }

    /// <summary>
    /// Parses the given string dump using the given parser.
    /// </summary>
    /// <param name="parser">The parser to use to parse the code.</param>
    /// <param name="dump">The string dump of the Lua code.</param>
    /// <param name="name">The name of the input, used for debugging.</param>
    /// <returns>The parses IParseItem tree.</returns>
    /// <exception cref="System.ArgumentNullException">If parser or dump is null.</exception>
    public static IParseItem Parse(IParser parser, string dump, string name) {
      if (parser == null) {
        throw new ArgumentNullException(nameof(parser));
      }
      if (dump == null) {
        throw new ArgumentNullException(nameof(dump));
      }

      var encoding = Encoding.UTF8;
      var stream = new MemoryStream(encoding.GetBytes(dump));
      return parser.Parse(stream, encoding, name);
    }

    #region Read Functions

    /// <summary>
    /// Reads a block of code from the input.  Any end tokens should not be read and are handled by
    /// the parent call (e.g. 'end' or 'until').
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The item that was read.</returns>
    protected virtual BlockItem _readBlock(Lexer input) {
      var debug = input.Peek();
      IList<IParseStatement> statements = new List<IParseStatement>();

      Token cur;
      bool includeReturn = true;
      while ((cur = input.Peek()).Type != TokenType.None) {
        if (_functions.ContainsKey(cur.Type)) {
          statements.Add(_functions[cur.Type](input));
        } else if (cur.Type == TokenType.Return) {
          var ret = _readReturn(input);
          return new BlockItem(statements.ToArray()) {
            Return = ret,
            Debug = _makeDebug(input, debug, ret.Debug),
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
          IParseExp exp = _readExp(input, out _);
          if (exp is FuncCallItem funcCall) {
            funcCall.Statement = true;
            statements.Add(funcCall);
          } else if (exp is NameItem || exp is IndexerItem) {
            statements.Add(_readAssignment(input, cur, false, (IParseVariable)exp));
          } else {
            throw input.SyntaxError(MessageId.ExpectedStatementStart, cur);
          }
        }
      }

      // Only gets here if this is the global function
      DebugInfo debugInfo = statements.Count == 0
          ? _makeDebug(input, debug)
          : _makeDebug(input, debug, statements[statements.Count - 1].Debug);
      return new BlockItem(statements.ToArray()) {
        Return = includeReturn ? new ReturnItem() : null,
        Debug = debugInfo,
      };
    }

    /// <summary>
    /// Reads a local statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readLocal(Lexer input) {
      Token debug = input.Expect(TokenType.Local);
      if (input.PeekType(TokenType.Function)) {
        FuncDefItem ret = _readFunctionHelper(input, canName: true, local: true);
        ret.Debug = _makeDebug(input, debug, ret.Debug);
        return ret;
      } else {
        Token name = input.Expect(TokenType.Identifier);
        NameItem nameItem = new NameItem(name.Value) { Debug = _makeDebug(input, name) };
        return _readAssignment(input, debug, local: true, nameItem);
      }
    }
    /// <summary>
    /// Reads a class statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readClass(Lexer input) {
      Token debug = input.Expect(TokenType.Class);
      var implements = new List<string>();

      string className;
      Token end;
      if (input.PeekType(TokenType.StringLiteral)) {
        end = input.Expect(TokenType.StringLiteral);
        className = end.Value;
        if (input.ReadIfType(TokenType.BeginParen)) {
          do {
            Token name = input.Expect(TokenType.Identifier);
            implements.Add(name.Value);
          } while (input.ReadIfType(TokenType.Comma));
          end = input.Expect(TokenType.EndParen);
        }
      } else {
        end = input.Expect(TokenType.Identifier);
        className = end.Value;
        if (input.PeekType(TokenType.Colon)) {
          do {
            input.Read();  // Skip the ':' or ','.  Simply include the '.' in the name.
            end = input.Expect(TokenType.Identifier);
            string name = end.Value;
            while (input.ReadIfType(TokenType.Indexer)) {
              end = input.Expect(TokenType.Identifier);
              name += "." + end.Value;
            }
            implements.Add(name);
          } while (input.PeekType(TokenType.Comma));
        }
      }

      return new ClassDefItem(className, implements.ToArray()) {
        Debug = _makeDebug(input, debug, end),
      };
    }
    /// <summary>
    /// Reads a return statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual ReturnItem _readReturn(Lexer input) {
      Token debug = input.Expect(TokenType.Return);
      var values = new List<IParseExp>();

      bool isParentheses = false;
      if (!input.PeekType(TokenType.End) && !input.PeekType(TokenType.Until) &&
          !input.PeekType(TokenType.ElseIf) && !input.PeekType(TokenType.Else) &&
          !input.PeekType(TokenType.None)) {
        values.Add(_readExp(input, out isParentheses));
        while (input.ReadIfType(TokenType.Comma)) {
          values.Add(_readExp(input, out isParentheses));
        }

        input.ReadIfType(TokenType.Semicolon);
      }

      return new ReturnItem(values.ToArray()) {
        Debug = values.Count == 0 ? _makeDebug(input, debug)
                                  : _makeDebug(input, debug, values[values.Count - 1].Debug),
        IsLastExpressionSingle = isParentheses,
      };
    }
    /// <summary>
    /// Reads a normal function statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readFunction(Lexer input) {
      return _readFunctionHelper(input, canName: true, local: false);
    }
    /// <summary>
    /// Reads a for statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readFor(Lexer input) {
      Token debug = input.Expect(TokenType.For);
      Token name = input.Expect(TokenType.Identifier);
      var nameItem = new NameItem(name.Value) { Debug = _makeDebug(input, name) };

      if (input.ReadIfType(TokenType.Assign)) {
        // Numeric for
        IParseExp start = _readExp(input, out _);
        input.Expect(TokenType.Comma);
        IParseExp limit = _readExp(input, out _);

        IParseExp step = null;
        if (input.ReadIfType(TokenType.Comma)) {
          step = _readExp(input, out _);
        }

        Token do_ = input.Expect(TokenType.Do);
        var block = _readBlock(input);
        Token end = input.Expect(TokenType.End);
        return new ForNumItem(nameItem, start, limit, step, block) {
          Debug = _makeDebug(input, debug, end),
          ForDebug = _makeDebug(input, debug, do_),
          EndDebug = _makeDebug(input, end),
        };
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
        exps.Add(_readExp(input, out _));
        while (input.ReadIfType(TokenType.Comma)) {
          exps.Add(_readExp(input, out _));
        }

        Token do_ = input.Expect(TokenType.Do);
        var block = _readBlock(input);
        Token end = input.Expect(TokenType.End);
        return new ForGenItem(names.ToArray(), exps.ToArray(), block) {
          Debug = _makeDebug(input, debug, end),
          ForDebug = _makeDebug(input, debug, do_),
          EndDebug = _makeDebug(input, end),
        };
      }
    }
    /// <summary>
    /// Reads an if statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readIf(Lexer input) {
      Token debug = input.Expect(TokenType.If);

      var exp = _readExp(input, out _);
      Token firstThen = input.Expect(TokenType.Then);
      var block = _readBlock(input);

      var elseIfs = new List<IfItem.ElseInfo>();
      while (true) {
        Token elseIf = input.Peek();
        if (!input.ReadIfType(TokenType.ElseIf))
          break;

        IParseExp elseExp = _readExp(input, out _);
        Token then = input.Expect(TokenType.Then);
        BlockItem elseIfBlock = _readBlock(input);
        elseIfs.Add(new IfItem.ElseInfo(elseExp, elseIfBlock, _makeDebug(input, elseIf, then)));
      }

      BlockItem elseBlock = null;
      Token elseToken = input.Peek();
      if (input.ReadIfType(TokenType.Else)) {
        elseBlock = _readBlock(input);
      }
      Token end = input.Expect(TokenType.End);
      return new IfItem(exp, block, elseIfs.ToArray(), elseBlock) {
        Debug = _makeDebug(input, debug, end),
        IfDebug = _makeDebug(input, debug, firstThen),
        ElseDebug = elseToken.Type == TokenType.Else ? _makeDebug(input, elseToken)
                                                     : new DebugInfo(),
        EndDebug = _makeDebug(input, end),
      };
    }
    /// <summary>
    /// Reads a repeat statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readRepeat(Lexer input) {
      Token debug = input.Expect(TokenType.Repeat);
      var block = _readBlock(input);
      Token repeat = input.Expect(TokenType.Until);
      var exp = _readExp(input, out _);
      return new RepeatItem(exp, block) {
        Debug = _makeDebug(input, debug, exp.Debug),
        RepeatDebug = _makeDebug(input, debug),
        UntilDebug = _makeDebug(input, repeat, exp.Debug),
      };
    }
    /// <summary>
    /// Reads a label statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readLabel(Lexer input) {
      Token debug = input.Expect(TokenType.Label);
      Token label = input.Expect(TokenType.Identifier);
      Token end = input.Expect(TokenType.Label);
      return new LabelItem(label.Value) { Debug = _makeDebug(input, debug, end) };
    }
    /// <summary>
    /// Reads a break statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readBreak(Lexer input) {
      Token ret = input.Expect(TokenType.Break);
      return new GotoItem("<break>") { Debug = _makeDebug(input, ret) };
    }
    /// <summary>
    /// Reads a goto statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readGoto(Lexer input) {
      Token debug = input.Expect(TokenType.Goto);
      Token name = input.Expect(TokenType.Identifier);
      return new GotoItem(name.Value) { Debug = _makeDebug(input, debug, name) };
    }
    /// <summary>
    /// Reads a do statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readDo(Lexer input) {
      input.Expect(TokenType.Do);
      BlockItem ret = _readBlock(input);
      input.Expect(TokenType.End);
      return ret;
    }
    /// <summary>
    /// Reads a while statement from the input.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The object that was read.</returns>
    protected virtual IParseStatement _readWhile(Lexer input) {
      Token debug = input.Expect(TokenType.While);
      var exp = _readExp(input, out _);
      Token do_ = input.Expect(TokenType.Do);
      var block = _readBlock(input);
      Token end = input.Expect(TokenType.End);
      return new WhileItem(exp, block) {
        Debug = _makeDebug(input, debug, end),
        WhileDebug = _makeDebug(input, debug, do_),
        EndDebug = _makeDebug(input, end),
      };
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
    protected virtual AssignmentItem _readAssignment(Lexer input, Token debug, bool local,
                                                     IParseVariable variable) {
      var names = new List<IParseVariable>() { variable };
      while (input.ReadIfType(TokenType.Comma)) {
        var curDebug = input.Peek();
        var exp = _readExp(input, out _);
        if (local && !(exp is NameItem)) {
          throw input.SyntaxError(MessageId.LocalMustBeIdentifier, curDebug);
        }
        if (!local && !(exp is IParseVariable)) {
          throw input.SyntaxError(MessageId.AssignmentMustBeVariable, curDebug);
        }
        names.Add((IParseVariable)exp);
      }

      bool isParentheses = false;
      var exps = new List<IParseExp>();
      if (input.ReadIfType(TokenType.Assign)) {
        exps.Add(_readExp(input, out isParentheses));

        while (input.ReadIfType(TokenType.Comma)) {
          exps.Add(_readExp(input, out isParentheses));
        }
      } else if (!local) {
        input.Expect(TokenType.Assign);
      }

      DebugInfo endDebug =
          exps.Count == 0 ? names[names.Count - 1].Debug : exps[exps.Count - 1].Debug;
      return new AssignmentItem(names.ToArray(), exps.ToArray()) {
        Debug = _makeDebug(input, debug, endDebug),
        Local = local,
        IsLastExpressionSingle = isParentheses,
      };
    }
    /// <summary>
    /// Reads a prefix-expression from the input.
    /// </summary>
    /// <param name="input">The input to read from.</param>
    /// <returns>The parsed expression.</returns>
    protected virtual IParseExp _readPrefixExp(Lexer input, out bool isParentheses) {
      Token debug = input.Peek();
      IParseExp ret;
      if (input.ReadIfType(TokenType.BeginParen)) {
        isParentheses = true;
        ret = _readExp(input, out _);
        input.Expect(TokenType.EndParen);
      } else {
        isParentheses = false;
        Token name = input.Expect(TokenType.Identifier);
        ret = new NameItem(name.Value) { Debug = _makeDebug(input, name) };
      }

      while (true) {
        if (input.ReadIfType(TokenType.BeginBracket)) {
          isParentheses = false;
          IParseExp temp = _readExp(input, out _);
          ret = new IndexerItem(ret, temp) { Debug = _makeDebug(input, debug, temp.Debug) };
          input.Expect(TokenType.EndBracket);
        } else if (input.ReadIfType(TokenType.Indexer)) {
          isParentheses = false;
          Token token = input.Expect(TokenType.Identifier);
          var name = new LiteralItem(token.Value) { Debug = _makeDebug(input, token) };
          ret = new IndexerItem(ret, name) { Debug = _makeDebug(input, debug, token) };
        } else {
          string instName = null;
          if (input.ReadIfType(TokenType.Colon)) {
            instName = input.Expect(TokenType.Identifier).Value;
          }

          bool isLastSingle = false;
          var args = new List<FuncCallItem.ArgumentInfo>();
          DebugInfo debugInfo;
          if (input.PeekType(TokenType.BeginTable)) {
            IParseExp table = _readTable(input);
            args.Add(new FuncCallItem.ArgumentInfo(table, false));
            debugInfo = _makeDebug(input, debug, table.Debug);
          } else if (input.PeekType(TokenType.StringLiteral)) {
            Token token = input.Expect(TokenType.StringLiteral);
            args.Add(new FuncCallItem.ArgumentInfo(new LiteralItem(token.Value) {
                                                     Debug = _makeDebug(input, token),
                                                   },
                                                   false));
            debugInfo = _makeDebug(input, debug, token);
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

                args.Add(new FuncCallItem.ArgumentInfo(_readExp(input, out isLastSingle), isRef));
                if (isRefParen) {
                  input.Expect(TokenType.EndParen);
                }
              } while (input.ReadIfType(TokenType.Comma));
            }
            Token end = input.Expect(TokenType.EndParen);
            debugInfo = _makeDebug(input, debug, end);
          } else {
            break;
          }
          isParentheses = false;
          ret = new FuncCallItem(ret, args.ToArray()) {
            Debug = debugInfo,
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
    protected virtual IParseExp _readExp(Lexer input, out bool isParentheses, int precedence = -1) {
      Token debug = input.Peek();
      IParseExp ret;
      var unOpType = _getUnaryOperationType(input.Peek().Type);
      isParentheses = false;
      if (unOpType != UnaryOperationType.Unknown) {
        input.Read();
        int unaryPrec = 11;
        if (unaryPrec > precedence && precedence >= 0) {
          unaryPrec = precedence;
        }
        ret = new UnOpItem(_readExp(input, out _, unaryPrec), unOpType) {
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
        ret = _readTable(input);
      } else if (input.PeekType(TokenType.Function)) {
        ret = _readFunctionHelper(input, false, false);
      } else {
        ret = _readPrefixExp(input, out isParentheses);
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
        IParseExp other = _readExp(input, out _, newPrecedence + extra);
        ret = new BinOpItem(ret, binOpType, other) {
          Debug = _makeDebug(input, debug, other.Debug),
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
    protected virtual FuncDefItem _readFunctionHelper(Lexer input, bool canName, bool local) {
      Token debug = input.Expect(TokenType.Function);

      IParseVariable name = null;
      string instName = null;
      if (input.PeekType(TokenType.Identifier)) {
        Token temp = input.Expect(TokenType.Identifier);
        name = new NameItem(temp.Value) { Debug = _makeDebug(input, temp) };
        while (input.ReadIfType(TokenType.Indexer)) {
          temp = input.Expect(TokenType.Identifier);
          var literal = new LiteralItem(temp.Value) { Debug = _makeDebug(input, temp) };
          name = new IndexerItem(name, literal) { Debug = _makeDebug(input, debug, temp) };
        }

        if (input.ReadIfType(TokenType.Colon)) {
          instName = input.Expect(TokenType.Identifier).Value;
        }
      }
      if (name != null && !canName) {
        throw input.SyntaxError(MessageId.FunctionNameWhenExpression, debug);
      }
      if (name == null && canName) {
        throw input.SyntaxError(MessageId.FunctionNameWhenStatement, debug);
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
      BlockItem chunk = _readBlock(input);
      Token end = input.Expect(TokenType.End);
      chunk.Return ??= new ReturnItem();

      return new FuncDefItem(args.ToArray(), chunk) {
        Debug = _makeDebug(input, debug, end),
        InstanceName = instName,
        Prefix = name,
        Local = local,
      };
    }
    /// <summary>
    /// Reads a table from the input.  Input must be either on the starting '{'.
    /// </summary>
    /// <param name="input">Where to read input from.</param>
    /// <returns>The table that was read.</returns>
    protected virtual TableItem _readTable(Lexer input) {
      Token debug = input.Expect(TokenType.BeginTable);

      double id = 1;
      var values = new List<KeyValuePair<IParseExp, IParseExp>>();
      while (!input.PeekType(TokenType.EndTable)) {
        if (input.ReadIfType(TokenType.BeginBracket)) {
          IParseExp temp = _readExp(input, out _);
          input.Expect(TokenType.EndBracket);
          input.Expect(TokenType.Assign);
          IParseExp val = _readExp(input, out _);
          values.Add(new KeyValuePair<IParseExp, IParseExp>(temp, val));
        } else {
          IParseExp val = _readExp(input, out _);
          if (input.ReadIfType(TokenType.Assign)) {
            if (!(val is NameItem name)) {
              throw input.SyntaxError(MessageId.TableKeyMustBeName, debug);
            }

            IParseExp exp = _readExp(input, out _);
            values.Add(new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(name.Name), exp));
          } else {
            values.Add(new KeyValuePair<IParseExp, IParseExp>(new LiteralItem(id++), val));
          }
        }

        if (!input.ReadIfType(TokenType.Comma) && !input.ReadIfType(TokenType.Semicolon)) {
          break;
        }
      }
      Token end = input.Expect(TokenType.EndTable);
      return new TableItem(values.ToArray()) { Debug = _makeDebug(input, debug, end) };
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
    /// <param name="token">The token to use.</param>
    /// <param name="end">The end token to use; if not given, just uses start token.</param>
    /// <returns>The new DebugInfo instance.</returns>
    protected static DebugInfo _makeDebug(Lexer lexer, Token token, Token? end = null) {
      long endPos = end == null ? token.EndPos : end.Value.EndPos;
      long endLine = end == null ? token.EndLine : end.Value.EndLine;
      return new DebugInfo(lexer.Name, token.StartPos, token.StartLine, endPos, endLine);
    }
    /// <summary>
    /// Creates a DebugInfo based on the given Token and DebugInfo.
    /// </summary>
    /// <param name="token">The token to use.</param>
    /// <param name="end">The end DebugInfo.</param>
    /// <returns>The new DebugInfo instance.</returns>
    protected static DebugInfo _makeDebug(Lexer lexer, Token token, DebugInfo end) {
      return new DebugInfo(lexer.Name, token.StartPos, token.StartLine, end.EndPos, end.EndLine);
    }

    #endregion
  }
}
