using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ModMaker.Lua.Parser
{
    /// <summary>
    /// Defines a tokenizer that accepts a TextElementEnumerator and produces a
    /// stream of token for use in parsing.  It automatically ignores 
    /// whitespace and comments.  This type can be extended to alter it's behaviour.
    /// </summary>
    public class Tokenizer : ITokenizer
    {
        /// <summary>
        /// Contains the previous peeks to support push-back.
        /// </summary>
        Stack<Token> peek;
        /// <summary>
        /// Contains the input to the tokenizer.
        /// </summary>
        protected readonly TextElementEnumerator input;

        /// <summary>
        /// Gets the name of the current file, used for throwing exceptions.
        /// </summary>
        public string Name { get; protected set; }
        /// <summary>
        /// Gets the current (one-based) position in the current line.
        /// </summary>
        public long Position { get; protected set; }
        /// <summary>
        /// Gets the current (one-based) line number.
        /// </summary>
        public long Line { get; protected set; }

        /// <summary>
        /// Creates a new Tokenizer object that will read from the given input.
        /// </summary>
        /// <param name="input">Where to read input from.</param>
        /// <param name="name">The name of the input, used for debugging.</param>
        /// <exception cref="System.ArgumentNullException">If input is null.</exception>
        public Tokenizer(TextElementEnumerator input, string name)
        {
            if (input == null)
                throw new ArgumentNullException("reader");

            this.peek = new Stack<Token>();
            this.input = input;
            this.Name = name;
            this.Position = 1;
            this.Line = 1;
            input.MoveNext(); // start the enumerator
        }

        /// <summary>
        /// Reads a single token from the input stream and progresses the input.
        /// </summary>
        /// <returns>The token that was read.</returns>
        public Token Read()
        {
            if (peek.Count > 0)
                return peek.Pop();
            else
                return InternalRead();
        }
        /// <summary>
        /// Reads a single token but does not progress the input.
        /// </summary>
        /// <returns>The token that was read.</returns>
        public Token Peek()
        {
            if (peek.Count == 0)
                peek.Push(InternalRead());

            return peek.Peek();
        }
        /// <summary>
        /// Pushes a token back onto the tokenizer.  This will allow to reverse
        /// a read.
        /// </summary>
        /// <param name="token">The token to push-back.</param>
        public void PushBack(Token token)
        {
            peek.Push(token);
        }

        /// <summary>
        /// Reads a single token from the input stream.
        /// </summary>
        /// <returns>The token that was read or a null string token.</returns>
        /// <remarks>
        /// If it is at the end of the enumeration, it will return a token with
        /// a null string, the values of the other members are unspecified.
        /// </remarks>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is
        /// an error in the syntax of the input.</exception>
        protected virtual Token InternalRead()
        {
        start:
            ReadWhitespace();

            Token ret = new Token();
            ret.StartPos = Position;
            ret.StartLine = Line;

            string last = ReadElement();
            if (last == null)
                return new Token();

            // goto the start if this is a comment
            if (last == "-")
            {
                last = PeekElement();
                if (last == "-")
                {
                    ReadElement();
                    ReadComment();
                    goto start;
                }
                else
                    last = "-";
            }
            // read an identifier (e.g. 'foo' or '_cat').
            else if (char.IsLetter(last, 0) || last == "_")
            {
                StringBuilder str = new StringBuilder();
                bool over = false;
                str.Append(last);
                last = PeekElement();
                while (last != null && (char.IsLetterOrDigit(last, 0) || last == "_" || last == "`"))
                {
                    if (over)
                    {
                        if (last == "`")
                            throw new SyntaxException(Resources.OverloadOneGrave, Name, ret);
                        if (!char.IsDigit(last, 0))
                            throw new SyntaxException(Resources.OnlyNumbersInOverload, Name, ret);
                    }
                    else if (last == "`")
                    {
                        over = true;
                    }
                    ReadElement();
                    str.Append(last);
                    last = PeekElement();
                }

                last = str.ToString();
            }
            // read indexer, concat, and ...
            else if (last == ".")
            {
                if (PeekElement() == ".")
                {
                    ReadElement(); // read "."
                    if (PeekElement() == ".")
                    {
                        ReadElement(); // read "."
                        last = "...";
                    }
                    else
                        last = "..";
                }
            }
            // read a number
            else if (char.IsNumber(last, 0) || last == CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)
            {
                return ReadNumber(last);
            }
            // read a literal string
            else if (last == "\"" || last == "'")
            {
                return ReadString(last == "'" ? -1 : -2, ret.StartPos, ret.StartLine);
            }
            // handle "["
            else if (last == "[")
            {
                last = PeekElement();
                if (last == "[" || last == "=")
                {
                    int dep = 0;
                    while (last == "=")
                    {
                        ReadElement(); // read "="
                        dep++;
                        last = PeekElement();
                    }
                    if (last != "[")
                        throw new SyntaxException(string.Format(Resources.InvalidDefinition, "long string"), Name, ret);

                    ReadElement(); // read "["
                    if (PeekElement() == "\n")
                        ReadElement();
                    return ReadString(dep, ret.StartPos, ret.StartLine);
                }
                else
                    last = "[";
            }
            // read ::
            else if (last == ":")
            {
                if (PeekElement() == ":")
                {
                    ReadElement(); // read ":"
                    last = "::";
                }
            }
            // read comparison operatos
            else if (last == ">" || last == "<" || last == "~" || last == "=")
            {
                if (PeekElement() == "=")
                {
                    last += "=";
                    ReadElement(); // read "="
                }
                else if (last == "~")
                    throw new SyntaxException("Invalid token '~'.", Name, ret);
            }

            // otherwise simply return the read text-element
            ret.EndPos = Position;
            ret.EndLine = Line;
            ret.Value = last;
            return ret;
        }

        /// <summary>
        /// Helper function that reads any current whitespace.
        /// </summary>
        protected virtual void ReadWhitespace()
        {
            string temp = PeekElement();
            while (temp != null && char.IsWhiteSpace(temp, 0))
            {
                ReadElement();
                temp = PeekElement();
            }
        }
        /// <summary>
        /// Helper function that reads a comment from the input, it assumes that
        /// the first two chars '--' have been already been read and the input 
        /// is on the next char.
        /// </summary>
        /// <returns>The token that holds the comments, this is unlikely to be 
        /// used except for debugging.</returns>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is
        /// an error in the syntax of the input.</exception>
        protected virtual Token ReadComment()
        {
            Token ret = new Token();
            StringBuilder build = new StringBuilder();
            build.Append("--");
            ret.StartLine = Line;
            ret.StartPos = Position;

            int depth = -1;
            string temp;
            if (PeekElement() == "[")
            {
                depth = 0;
                build.Append(ReadElement());
                while ((temp = ReadElement()) != null)
                {
                    build.Append(temp);

                    if (temp == "=")
                        depth++;
                    else if (temp == "\n")
                    {
                        ret.EndLine = Line;
                        ret.EndPos = Position;
                        ret.Value = build.ToString();
                        return ret;
                    }
                    else
                    {
                        if (temp != "[")
                            depth = -1;
                        break;
                    }
                }
            }

            int curDepth = -1;
            while ((temp = ReadElement()) != null)
            {
                build.Append(temp);
                if (depth == -1)
                {
                    if (temp == "\n")
                        break;
                }
                else
                {
                    if (curDepth != -1)
                    {
                        if (temp == "]")
                        {
                            if (curDepth == depth)
                                break;
                            else
                                curDepth = -1;
                        }
                        else if (temp == "=")
                            curDepth++;
                        else
                            curDepth = -1;
                    }
                    else if (temp == "]")
                        curDepth = 0;
                }
            }

            ret.EndLine = Line;
            ret.EndPos = Position;
            ret.Value = build.ToString();

            if (PeekElement() == null && depth != curDepth)
                throw new SyntaxException(string.Format(Resources.MissingEnd, "long comment"), ret);
            return ret;
        }
        /// <summary>
        /// Helper function that reads a string from the input, it assumes that
        /// it is on the first character in the string.
        /// </summary>
        /// <param name="depth">The depth of the long-string or -1 for ' or 
        /// -2 for ".</param>
        /// <param name="line">The starting line of the string.</param>
        /// <param name="pos">The starting position of the string.</param>
        /// <returns>The token that represents the string read. The token should
        /// start with a ".</returns>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If there is
        /// an error in the syntax of the input.</exception>
        protected virtual Token ReadString(int depth, long pos, long line)
        {
            StringBuilder str = new StringBuilder();
            str.Append("\"");
            Token ret = new Token();
            ret.StartPos = pos;
            ret.StartLine = line;

            while (PeekElement() != null)
            {
                string temp = ReadElement();

                if (temp == "'" && depth == -1)
                    break;
                else if (temp == "\"" && depth == -2)
                    break;
                else if (temp == "\n" && depth < 0)
                    throw new SyntaxException(string.Format(Resources.MissingEnd, "string literal"), Name, ret);
                else if (temp == "]" && depth >= 0)
                {
                    int j = 0;
                    while (PeekElement() == "=")
                    {
                        j++;
                        ReadElement();
                    }

                    if (PeekElement() != "]" || j != depth)
                    {
                        // if this isn't the end of the string,
                        //   append the parts read already.
                        str.Append(']');
                        str.Append('=', j);
                    }
                    else
                    {
                        ReadElement();
                        break;
                    }
                }
                else if (temp == "\\")
                {
                    if (depth >= 0)
                    {
                        str.Append("\\");
                        continue;
                    }

                    temp = ReadElement();
                    switch (temp)
                    {
                        case "'":
                        case "\"":
                        case "\\":
                        case "\n":
                            str.Append(temp);
                            break;
                        case "z":
                            ReadWhitespace();
                            break;
                        case "n":
                            str.Append("\n");
                            break;
                        case "a":
                            str.Append('\a');
                            break;
                        case "b":
                            str.Append('\b');
                            break;
                        case "f":
                            str.Append('\f');
                            break;
                        case "repeat":
                            str.Append('\r');
                            break;
                        case "t":
                            str.Append('\t');
                            break;
                        case "v":
                            str.Append('\v');
                            break;
                        case "x":
                            {
                                int ii = 0;
                                temp = ReadElement();
                                if (!"0123456789ABCDEFabcdef".Contains(temp))
                                    throw new SyntaxException(string.Format(Resources.InvalidEscape, "x" + temp), Name, ret);
                                ii = int.Parse(temp, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                temp = ReadElement();
                                if (!"0123456789ABCDEFabcdef".Contains(temp))
                                    throw new SyntaxException(string.Format(Resources.InvalidEscape, "x" + ii.ToString("x", CultureInfo.CurrentCulture) + temp), Name, ret);
                                ii = (ii >> 16) + int.Parse(temp, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                                str.Append((char)ii);
                                break;
                            }
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
                            {
                                int ii = 0;
                                if (!"0123456789".Contains(PeekElement()))
                                    continue;
                                temp = ReadElement();
                                ii = int.Parse(temp, CultureInfo.InvariantCulture);
                                if ("0123456789".Contains(PeekElement()))
                                {
                                    temp = ReadElement();
                                    ii = (ii * 10) + int.Parse(temp, CultureInfo.InvariantCulture);
                                    if ("0123456789".Contains(PeekElement()))
                                    {
                                        temp = ReadElement();
                                        ii = (ii * 10) + int.Parse(temp, CultureInfo.InvariantCulture);
                                    }
                                }
                                str.Append((char)ii);
                                break;
                            }
                        default:
                            throw new SyntaxException(string.Format(Resources.InvalidEscape, temp), Name, ret);
                    }
                }
                else
                    str.Append(temp);
            }

            ret.EndPos = Position;
            ret.EndLine = Line;
            ret.Value = str.ToString();
            return ret;
        }
        /// <summary>
        /// Helper function that reads a number from the input.  The resulting 
        /// token should start with '&amp;' if the number is in hex format, 
        /// otherwise the number should be in a parseable double format.
        /// </summary>
        /// <param name="last">The first character of the number.</param>
        /// <returns>The token that was read.  It should start with '&amp;' if 
        /// the number is hex.</returns>
        protected virtual Token ReadNumber(string last)
        {
            Token ret = new Token();
            ret.StartPos = Position - (last == null ? 0 : last.Length);
            ret.StartLine = Line;

            // this version does nothing to check for a valid number, that is done in the parser.
            //   this only supports 0xNNN notation for hexadecimal numbers (where NNN is a char.IsNumber char or a-f or A-F).
            StringBuilder str = new StringBuilder();
            CultureInfo ci = CultureInfo.CurrentCulture;
            string l = last;
            bool hex = false;
            if (last == "0" && PeekElement().ToLowerInvariant() == "x")
            {
                hex = true;
                str.Append("&");
                ReadElement(); // read the 'x'
                last = PeekElement();
            }
            else
            {
                str.Append(last);
                last = PeekElement();
            }

            while (last != null && (char.IsNumber(last, 0) || (hex && ((last[0] >= 'a' && last[0] <= 'f') || (last[0] >= 'A' && last[0] <= 'F'))) ||
                (!hex && (last == ci.NumberFormat.NumberDecimalSeparator || last == "-" || (l != "." && last == "e")))))
            {
                ReadElement();
                str.Append(last);
                l = last;
                last = PeekElement();
            }

            ret.EndLine = Line;
            ret.EndPos = Position;
            ret.Value = str.ToString();
            return ret;
        }

        /// <summary>
        /// Reads a text-element from the input and moves forward. This also 
        /// converts '\r\n' to '\n' and changes the Position and Line.
        /// </summary>
        /// <returns>The text-element that was read or null if at the end.</returns>
        protected virtual string ReadElement()
        {
            string ret = null;
            try
            {
                ret = input.GetTextElement();
                input.MoveNext();
            }
            catch (Exception) { }

            if (ret == null)
                return null;

            Position += ret.Length;
            if (ret == "\r" || ret == "\n")
            {
                if (ret == "\r")
                {
                    string temp = PeekElement();
                    if (temp == "\n")
                        input.MoveNext();
                    ret = "\n";
                }
                Line++;
                Position = 1;
            }
            return ret;
        }
        /// <summary>
        /// Looks at the current text-element in the input without moving 
        /// forward.  Returns null if it is at the end of the stream.
        /// </summary>
        /// <returns>The current text-element or null if at the end.</returns>
        protected virtual string PeekElement()
        {
            try
            {
                string temp = input.GetTextElement();
                return temp == "\r" ? "\n" : temp;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}