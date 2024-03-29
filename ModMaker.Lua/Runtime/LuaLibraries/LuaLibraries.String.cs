// Copyright 2014 Jacob Trimble
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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  static partial class LuaStaticLibraries {
    static class String {
      public static void Initialize(ILuaEnvironment env) {
        var str = new LuaTable(env);
        Register(env, str, (Func<string, int, int?, IEnumerable<int>>)byte_, "byte");
        Register(env, str, (Func<int[], string>)char_, "char");
        Register(env, str, (Func<string, string, int, bool, object[]>)find);
        Register(env, str, (Func<string, object[], string>)format);
        Register(env, str, (Func<string, string, object>)gmatch);
        Register(env, str, (Func<string, string, ILuaValue, int, string>)gsub);
        Register(env, str, (Func<string, int>)len);
        Register(env, str, (Func<string, string>)lower);
        Register(env, str, (Func<string, string, int, IEnumerable<string>>)match);
        Register(env, str, (Func<string, int, string, string>)rep);
        Register(env, str, (Func<string, string>)reverse);
        Register(env, str, (Func<string, int, int, string>)sub);
        Register(env, str, (Func<string, string>)upper);

        env.GlobalsTable.SetItemRaw(new LuaString("string"), str);
      }

      [MultipleReturn]
      static IEnumerable<int> byte_(string source, int i = 1, int? j = null) {
        return sub(source, i, j ?? i).Select(c => (int)c);
      }
      static string char_(params int[] chars) {
        StringBuilder ret = new StringBuilder(chars.Length);
        foreach (int c in chars) {
          if (c < 0) {
            throw new ArgumentException("Character out of range for 'string.char'.");
          } else if (c <= 0xFFFF) {
            // This may be a surrogate pair, assume they know what they are doing.
            ret.Append((char)c);
          } else {
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
      static object[] find(string source, string pattern, int start = 1, bool plain = false) {
        if (start >= source.Length) {
          return Array.Empty<object>();
        }

        start = normalizeIndex_(source.Length, start);
        if (plain) {
          int i = source.IndexOf(pattern, start - 1, StringComparison.CurrentCulture);
          if (i == -1) {
            return Array.Empty<object>();
          } else {
            return new object[] { i + 1, (i + pattern.Length) };
          }
        }

        Regex reg = new Regex(pattern);
        Match match = reg.Match(source, start - 1);
        if (match == null || !match.Success) {
          return Array.Empty<object>();
        }

        return new object[] { match.Index + 1, (match.Index + match.Length) }
            .Concat(match.Groups.Cast<Group>().Skip(1).Select(c => c.Value))
            .ToArray();
      }
      static string format(string format, params object[] args) {
        return string.Format(format, args);
      }
      static object gmatch(string source, string pattern) {
        var helper = new gmatchIter(Regex.Matches(source, pattern));
        return (Func<object[], string[]>)helper.gmatch_iter;
      }
      static string gsub(string source, string pattern, ILuaValue repl, int n = int.MaxValue) {
        return Regex.Replace(source, pattern, new gsubHelper(repl, n).Match);
      }
      static int len(string str) {
        return str.Length;
      }
      static string lower(string str) {
        return str.ToLower(CultureInfo.CurrentCulture);
      }
      [MultipleReturn]
      static IEnumerable<string> match(string source, string pattern, int start = 1) {
        start = normalizeIndex_(source.Length, start);
        Regex reg = new Regex(pattern);
        Match match = reg.Match(source, start - 1);
        if (!match.Success) {
          return Array.Empty<string>();
        }

        if (match.Groups.Count == 1) {
          return new[] { match.Value };
        } else {
          return match.Groups.Cast<Group>().Skip(1).Select(c => c.Value);
        }
      }
      static string rep(string str, int rep, string? sep = null) {
        if (rep < 1) {
          return "";
        }

        sep ??= "";
        StringBuilder ret = new StringBuilder((str.Length + sep.Length) * rep);
        for (int i = 0; i < rep - 1; i++) {
          ret.Append(str);
          ret.Append(sep);
        }
        ret.Append(str);
        return ret.ToString();
      }
      static string reverse(string str) {
        // Ensure the reverse does not break surrogate pairs.
        var matches = Regex.Matches(str, @"\p{IsHighSurrogates}\p{IsLowSurrogates}|.");
        return matches.Cast<Match>().Select(c => c.Value).Reverse().Aggregate("", (a, b) => a + b);
      }
      static string sub(string source, int i, int j = -1) {
        if (i > source.Length) {
          return "";
        }

        int start = normalizeIndex_(source.Length, i);
        int end = normalizeIndex_(source.Length, j);
        if (start > end) {
          return "";
        }

        return source.Substring(start - 1, end - start + 1);
      }
      static string upper(string str) {
        return str.ToUpper(CultureInfo.CurrentCulture);
      }

      class gmatchIter {
        public gmatchIter(MatchCollection matches) {
          _matches = matches;
          _index = 0;
        }

        [MultipleReturn]
        public string[] gmatch_iter(params object[] _) {
          if (_index >= _matches.Count) {
            return Array.Empty<string>();
          }

          Match cur = _matches[_index];
          _index++;

          if (cur.Groups.Count == 1) {
            return new[] { cur.Groups[0].Value };
          } else {
            return cur.Groups.Cast<Group>().Select(c => c.Value).Skip(1).ToArray();
          }
        }

        readonly MatchCollection _matches;
        int _index;
      }
      class gsubHelper {
        public gsubHelper(ILuaValue value, int max) {
          _count = 0;
          _max = max;

          if (value.ValueType == LuaValueType.String) {
            _string = (string)value.GetValue()!;
          } else if (value.ValueType == LuaValueType.Table) {
            _table = (ILuaTable)value;
          } else if (value.ValueType == LuaValueType.Function) {
            _method = value;
          } else {
            throw new ArgumentException(
                "Third argument to function 'string.gsub' must be a string, table, or function.");
          }
        }

        public string Match(Match match) {
          if (_count >= _max) {
            return match.Value;
          }

          _count++;
          if (_string != null) {
            return Regex.Replace(_string, @"%[0-9%]", m => {
              if (m.Value == "%%") {
                return "%";
              }

              int i = int.Parse(m.Groups[0].Value.Substring(1));
              return i == 0 ? match.Value : (match.Groups.Count > i ? match.Groups[i].Value : "");
            });
          } else if (_table != null) {
            string key = match.Groups.Count == 0 ? match.Value : match.Groups[1].Value;
            ILuaValue value = _table.GetItemRaw(new LuaString(key));
            if (value != null && value.IsTrue) {
              return value.ToString()!;
            }
          } else if (_method != null) {
            var groups = match.Groups.Cast<Group>().Skip(1).Select(c => c.Value).ToArray();
            var args = LuaMultiValue.CreateMultiValueFromObj(groups);
            LuaMultiValue obj = _method.Invoke(args);
            if (obj != null && obj.Count > 0) {
              ILuaValue value = obj[0];
              if (value != null && value.IsTrue) {
                return value.ToString()!;
              }
            }
          }

          return match.Value;
        }

        int _count;
        readonly int _max;
        readonly string? _string;
        readonly ILuaTable? _table;
        readonly ILuaValue? _method;
      }
    }
  }
}
