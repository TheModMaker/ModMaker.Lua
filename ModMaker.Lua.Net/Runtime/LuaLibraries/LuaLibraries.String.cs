using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        /// <summary>
        /// Contains the string libraries.
        /// </summary>
        static class String
        {
            public static void Initialize(ILuaEnvironment E)
            {
                var str = E.Runtime.CreateTable();
                Register(E, str, (Func<string, int, int?, IEnumerable<char>>)byte_, "byte");
                Register(E, str, (Func<int[], string>)char_, "char");
                Register(E, str, (Func<string, string, int, bool, object[]>)find);
                Register(E, str, (Func<string, object[], string>)format);
                Register(E, str, (Func<string, string, object>)gmatch);
                Register(E, str, (Func<string, string, ILuaValue, string>)gsub);
                Register(E, str, (Func<string, int>)len);
                Register(E, str, (Func<string, string>)lower);
                Register(E, str, (Func<string, string, int, IEnumerable<string>>)match);
                Register(E, str, (Func<string, int, string, string>)rep);
                Register(E, str, (Func<string, string>)reverse);
                Register(E, str, (Func<string, int, int?, string>)sub);
                Register(E, str, (Func<string, string>)upper);

                E.GlobalsTable.SetItemRaw(E.Runtime.CreateValue("string"), str);
            }

            [MultipleReturn]
            static IEnumerable<char> byte_(string source, int i = 1, int? j = null)
            {
                return sub(source, i, j);
            }
            static string char_(params int[] chars)
            {
                StringBuilder ret = new StringBuilder(chars.Length);
                foreach (int c in chars)
                {
                    if (c <= 0xFFFF)
                    {
                        // This may be a surrogate pair, assume they know what they are doing.
                        ret.Append((char)c);
                    }
                    else
                    {
                        int mask = (1 << 10) - 1;
                        int point = c - 0x10000;
                        int high = point >> 10;
                        int low = point & mask;
                        ret.Append((char)(high + 0xD800));
                        ret.Append((char)(low + 0xDC00));
                    }
                }
                return ret.ToString();
            }
            [MultipleReturn]
            static object[] find(string source, string pattern, int start = 1, bool plain = false)
            {
                start = normalizeIndex_(source.Length, start);
                if (plain)
                {
                    int i = source.IndexOf(pattern, start, StringComparison.CurrentCulture);
                    if (i == -1)
                        return new object[0];
                    else
                        return new object[] { i, (i + pattern.Length) };
                }

                Regex reg = new Regex(pattern);
                Match match = reg.Match(source, start);
                if (match == null)
                    return new object[0];

                return new object[] { match.Index, (match.Index + match.Length) }
                    .Concat(match.Captures.Cast<Capture>().Select(c => c.Value))
                    .ToArray();
            }
            static string format(string format, params object[] args)
            {
                return System.String.Format(format, args);
            }
            static object gmatch(string source, string pattern)
            {
                var helper = new gmatchIter(Regex.Matches(source, pattern));
                return (Func<object[], string[]>)helper.gmatch_iter;
            }
            static string gsub(string source, string pattern, ILuaValue repl)
            {
                return Regex.Replace(source, pattern, new gsubHelper(repl).Match);
            }
            static int len(string str)
            {
                return str.Length;
            }
            static string lower(string str)
            {
                return str.ToLower(CultureInfo.CurrentCulture);
            }
            [MultipleReturn]
            static IEnumerable<string> match(string source, string pattern, int start = 1)
            {
                start = normalizeIndex_(source.Length, start);
                Regex reg = new Regex(pattern);
                Match match = reg.Match(source, start);
                if (match == null)
                    return new string[0];

                return match.Captures.Cast<Capture>().Select(c => c.Value);
            }
            static string rep(string str, int rep, string sep = null)
            {
                if (rep < 1)
                    return "";

                StringBuilder ret = new StringBuilder((str.Length + sep.Length) * rep);
                for (int i = 0; i < rep - 1; i++)
                {
                    ret.Append(str);
                    ret.Append(sep);
                }
                ret.Append(str);
                return ret.ToString();
            }
            static string reverse(string str)
            {
                return new string(str.Reverse().ToArray());
            }
            static string sub(string source, int i = 1, int? j = null)
            {
                int start = normalizeIndex_(source.Length, i);
                int end = j != null ? normalizeIndex_(source.Length, j.Value) : start;
                if (start > end)
                    return "";

                return source.Substring(start - 1, end - start + 1);
            }
            static string upper(string str)
            {
                return str.ToUpper(CultureInfo.CurrentCulture);
            }

            class gmatchIter
            {
                public gmatchIter(MatchCollection matches)
                {
                    this.matches = matches;
                    this.index = 0;
                }

                [MultipleReturn]
                public string[] gmatch_iter(params object[] dummy)
                {
                    if (matches == null || index >= matches.Count)
                        return new string[0];

                    Match cur = matches[index];
                    index++;

                    return cur.Groups.Cast<Group>().Select(c => c.Value).ToArray();
                }

                MatchCollection matches;
                int index;
            }
            class gsubHelper
            {
                public gsubHelper(ILuaValue value)
                {
                    if (value.ValueType == LuaValueType.String)
                        string_ = (string)value.GetValue();
                    else if (value.ValueType == LuaValueType.Table)
                        table_ = (ILuaTable)value;
                    else if (value.ValueType == LuaValueType.Function)
                        method_ = value;
                    else
                        throw new ArgumentException("Third argument to function 'string.gsub' must be a string, table, or function.");
                }

                public string Match(Match match)
                {
                    if (string_ != null)
                    {
                        return Regex.Replace(string_, @"%[0-9%]", m =>
                        {
                            if (m.Value == "%%")
                                return "%";
                            int i = int.Parse(m.Groups[0].Value.Substring(1));
                            return i == 0 ? match.Value : (match.Groups.Count > i ? match.Groups[i].Value : "");
                        });
                    }
                    else if (table_ != null)
                    {
                        ILuaValue value = table_.GetItemRaw(new LuaString(match.Value));
                        if (value != null && value.IsTrue)
                            return value.ToString();
                    }
                    else if (method_ != null)
                    {
                        var groups = match.Groups.Cast<Group>().Skip(1).Select(c => c.Value).ToArray();
                        var args = LuaMultiValue.CreateMultiValueFromObj(groups);
                        ILuaMultiValue obj = method_.Invoke(LuaNil.Nil, false, -1, args);
                        if (obj != null && obj.Count > 0)
                        {
                            ILuaValue value = obj[0];
                            if (value != null && value.IsTrue)
                                return value.ToString();
                        }
                    }

                    return match.Value;
                }

                string string_;
                ILuaTable table_;
                ILuaValue method_;
            }
        }
    }
}
