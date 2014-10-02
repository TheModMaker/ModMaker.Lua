using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        /// <summary>
        /// Contains the string libraries.
        /// </summary>
        static class String
        {
            public static ILuaTable Initialize(ILuaEnvironment E)
            {
                ILuaTable str = new LuaTableNet();
                str.SetItemRaw("byte", new Byte(E));
                str.SetItemRaw("char", new Char(E));
                str.SetItemRaw("find", new find(E));
                str.SetItemRaw("format", new foramt(E));
                str.SetItemRaw("gmatch", new gmatch(E));
                str.SetItemRaw("gsub", new gsub(E));
                str.SetItemRaw("len", new len(E));
                str.SetItemRaw("lower", new lower(E));
                str.SetItemRaw("match", new match(E));
                str.SetItemRaw("rep", new rep(E));
                str.SetItemRaw("reverse", new reverse(E));
                str.SetItemRaw("sub", new sub(E));
                str.SetItemRaw("upper", new upper(E));

                return str;
            }

            sealed class gmatchHelper : LuaFrameworkMethod
            {
                MatchCollection _col;
                int i = 0;

                public gmatchHelper(ILuaEnvironment E, MatchCollection col)
                    : base(E, "gmatch")
                {
                    this._col = col;
                }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (_col == null || _col.Count <= i)
                        return new MultipleReturn();
                    Match m = _col[i++];

                    return new MultipleReturn(m.Groups.Cast<Group>().Select(c => c.Value));
                }
            }
            sealed class gsubHelper
            {
                string _string;
                ILuaTable _table;
                IMethod _meth;

                public gsubHelper(object o)
                {
                    this._string = o as string;
                    this._table = o as ILuaTable;
                    this._meth = o as IMethod;
                    if (_string == null && _table == null && _meth == null)
                        throw new ArgumentException("Third argument to function 'string.gsub' must be a string, table, or function.");
                }

                public string Match(Match match)
                {
                    string ret = null;

                    if (_string != null)
                    {
                        ret = Regex.Replace(_string, @"%[0-9%]", m =>
                        {
                            if (m.Value == "%%")
                                return "%";
                            int i = int.Parse(m.Groups[0].Value.Substring(1), CultureInfo.InvariantCulture);
                            return i == 0 ? match.Value : (match.Groups.Count > i ? match.Groups[i].Value : "");
                        });
                    }
                    else if (_table != null)
                    {
                        object o = _table.GetItemRaw(match.Value);
                        if (o != null && o as bool? != false)
                        {
                            ret = o.ToString();
                        }
                    }
                    else if (_meth != null)
                    {
                        var obj = _meth.Invoke(null, match.Groups.Cast<Group>().Where((g, i) => i > 0).Select(c => c.Value).ToArray());
                        if (obj != null && obj.Count > 0)
                        {
                            object o = obj[0];
                            if (o != null && o as bool? != false)
                            {
                                ret = o.ToString();
                            }
                        }
                    }

                    return ret == null ? match.Value : ret;
                }
            }

            sealed class Byte : LuaFrameworkMethod
            {
                public Byte(ILuaEnvironment E) : base(E, "string.Byte") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'string.byte'.");

                    string s = args[0] as string;
                    object obj = args[1];

                    if (s == null)
                        throw new ArgumentException("First argument to function 'string.byte' must be a string.");
                    if (obj != null && !(obj is double))
                        throw new ArgumentException("Second argument to function 'string.byte' must be an integer.");

                    int i = Convert.ToInt32(obj as double? ?? 1);
                    int j = Convert.ToInt32(args[2] ?? i, CultureInfo.CurrentCulture);

                    i--;
                    j--;
                    if (i < 0)
                        i = s.Length + i;
                    if (j < 0)
                        j = s.Length + j;
                    if (i < 1)
                        i = 1;
                    if (j >= s.Length)
                        j = s.Length - 1;
                    if (i < j)
                        return new MultipleReturn();

                    return new MultipleReturn(s.Substring(i, j - i).Select(c => (double)c));
                }
            }
            sealed class Char : LuaFrameworkMethod
            {
                public Char(ILuaEnvironment E) : base(E, "string.Char") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    StringBuilder str = new StringBuilder();

                    for (int j = 0; j < args.Length; j++)
                    {
                        object obj = args[j];
                        if (!(obj is double))
                            throw new ArgumentException("All arguments to function 'string.char' must be numbers.");

                        int i = Convert.ToInt32((double)obj);
                        if (i > char.MaxValue)
                        {
                            int high = (i >> 16) & 0xff;
                            int low = i & 0xff;

                            if (!char.IsSurrogatePair((char)high, (char)low))
                                throw new ArgumentException("Argument to function 'string.char' is outside the range of a char.");

                            str.Append((char)high);
                            str.Append((char)low);
                        }
                        else
                            str.Append((char)i);
                    }

                    return new MultipleReturn((object)str.ToString());
                }
            }
            sealed class find : LuaFrameworkMethod
            {
                public find(ILuaEnvironment E) : base(E, "string.find") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting two arguments to function 'string.find'.");

                    string str = args[0] as string;
                    string pattern = args[1] as string;
                    double start = (args.Length > 2 ? args[2] : null) as double? ?? 1;
                    bool plain = (args.Length > 3 ? args[3] : null) as bool? ?? false;
                    if (str == null)
                        throw new ArgumentException("First argument to function 'string.find' must be a string.");
                    if (pattern == null)
                        throw new ArgumentException("Second argument to function 'string.find' must be a string.");

                    int startx = start > 0 ? (int)start - 1 : str.Length + (int)start;
                    if (plain)
                    {
                        int i = str.IndexOf(pattern, startx, StringComparison.CurrentCulture);
                        if (i == -1)
                            return new MultipleReturn();
                        else
                            return new MultipleReturn((double)i, (double)(i + pattern.Length));
                    }

                    Regex reg = new Regex(pattern);
                    Match mat = reg.Match(str, startx);
                    return mat == null ? new MultipleReturn() :
                        new MultipleReturn(
                            new object[] { (double)mat.Index, (double)(mat.Index + mat.Length) }
                                .Then(mat.Captures.Cast<Capture>()
                                    .Select(c => c.Value))
                            );
                }
            }
            sealed class foramt : LuaFrameworkMethod
            {
                public foramt(ILuaEnvironment E) : base(E, "string.foramt") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'string.format'");

                    string s = args[0] as string;
                    if (s == null)
                        throw new ArgumentException("First argument to function 'string.format' must be a string.");

                    return new MultipleReturn((object)string.Format(CultureInfo.CurrentCulture, s, args.Cast<object>().Where((obj, i) => i > 0).ToArray()));
                }
            }
            sealed class gmatch : LuaFrameworkMethod
            {
                public gmatch(ILuaEnvironment E) : base(E, "string.gmatch") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting two arguments to function 'string.gmatch'");

                    string s = args[0] as string;
                    string pattern = args[1] as string;
                    if (s == null)
                        throw new ArgumentException("First argument to function 'string.gmatch' must be a string.");
                    if (pattern == null)
                        throw new ArgumentException("Second argument to function 'string.gmatch' must be a string.");

                    return new MultipleReturn(new gmatchHelper(Environment, Regex.Matches(s, pattern)));
                }
            }
            sealed class gsub : LuaFrameworkMethod
            {
                public gsub(ILuaEnvironment E) : base(E, "string.gsub") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 3)
                        throw new ArgumentException("Expecting three arguments to function 'string.gsub'");

                    string s = args[0] as string;
                    string pattern = args[1] as string;
                    object repl = args[2];
                    if (s == null)
                        throw new ArgumentException("First argument to function 'string.gsub' must be a string.");
                    if (pattern == null)
                        throw new ArgumentException("Second argument to function 'string.gsub' must be a string.");

                    return new MultipleReturn((object)Regex.Replace(s, pattern, new gsubHelper(repl).Match));
                }
            }
            sealed class len : LuaFrameworkMethod
            {
                public len(ILuaEnvironment E) : base(E, "string.len") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'string.len'.");

                    string s = args[0] as string;
                    if (s == null)
                        throw new ArgumentException("First argument to function 'string.len' must be a string.");

                    return new MultipleReturn((double)s.Length);
                }
            }
            sealed class lower : LuaFrameworkMethod
            {
                public lower(ILuaEnvironment E) : base(E, "string.lower") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'string.lower'.");

                    string s = args[0] as string;
                    if (s == null)
                        throw new ArgumentException("First argument to function 'string.lower' must be a string.");

                    return new MultipleReturn((object)s.ToLower(CultureInfo.CurrentCulture));
                }
            }
            sealed class match : LuaFrameworkMethod
            {
                public match(ILuaEnvironment E) : base(E, "string.match") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting two arguments to function 'string.match'.");

                    string str = args[0] as string;
                    string pattern = args[1] as string;
                    double start = (args.Length > 2 ? args[2] : null) as double? ?? 1;
                    if (str == null)
                        throw new ArgumentException("First argument to function 'string.match' must be a string.");
                    if (pattern == null)
                        throw new ArgumentException("Second argument to function 'string.match' must be a string.");

                    Regex reg = new Regex(pattern);
                    int startx = start > 0 ? (int)start - 1 : str.Length + (int)start;
                    Match mat = reg.Match(str, startx);
                    return mat == null ? new MultipleReturn() : new MultipleReturn(mat.Captures.Cast<Capture>().Select(c => c.Value));
                }
            }
            sealed class rep : LuaFrameworkMethod
            {
                public rep(ILuaEnvironment E) : base(E, "string.rep") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting at least two arguments to function 'string.rep'.");

                    string s = args[0] as string;
                    object obj = args[1];
                    string sep = (args.Length > 2 ? args[2] : null) as string ?? "";

                    if (s == null)
                        throw new ArgumentException("First argument to function 'string.rep' must be a string.");
                    if (obj == null || !(obj is double))
                        throw new ArgumentException("First argument to function 'string.rep' must be a number.");

                    StringBuilder str = new StringBuilder();
                    double limit = (double)obj;
                    for (int i = 0; i < limit - 1; i++)
                    {
                        str.Append(s);
                        str.Append(sep);
                    }
                    str.Append(s);
                    return new MultipleReturn((object)str.ToString());
                }
            }
            sealed class reverse : LuaFrameworkMethod
            {
                public reverse(ILuaEnvironment E) : base(E, "string.reverse") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'string.reverse'.");

                    string s = args[0] as string;
                    if (s == null)
                        throw new ArgumentException("First argument to function 'string.reverse' must be a string.");

                    return new MultipleReturn((object)new string(s.Reverse().ToArray()));
                }
            }
            sealed class sub : LuaFrameworkMethod
            {
                public sub(ILuaEnvironment E) : base(E, "string.sub") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting at least two arguments to function 'string.sub'.");

                    string s = args[0] as string;
                    object obj = args[1];
                    if (s == null)
                        throw new ArgumentException("First argument to function 'string.sub' must be a string.");
                    if (obj == null || !(obj is double))
                        throw new ArgumentException("Second argument to function 'string.sub' must be an integer.");

                    int i = Convert.ToInt32((double)obj);
                    int j = Convert.ToInt32((args.Length > 2 ? args[2] : null) ?? i, CultureInfo.CurrentCulture);

                    i--;
                    j--;
                    if (i < 0)
                        i = s.Length + i;
                    if (j < 0)
                        j = s.Length + j;
                    if (i < 1)
                        i = 1;
                    if (j >= s.Length)
                        j = s.Length - 1;
                    if (i < j)
                        return new MultipleReturn((object)"");

                    return new MultipleReturn((object)s.Substring(i, j - i));
                }
            }
            sealed class upper : LuaFrameworkMethod
            {
                public upper(ILuaEnvironment E) : base(E, "string.upper") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'string.upper'.");

                    string s = args[0] as string;
                    if (s == null)
                        throw new ArgumentException("First argument to function 'string.upper' must be a string.");

                    return new MultipleReturn((object)s.ToUpper(CultureInfo.CurrentCulture));
                }
            }
        }
    }
}
