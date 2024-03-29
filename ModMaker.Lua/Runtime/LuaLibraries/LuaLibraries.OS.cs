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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  static partial class LuaStaticLibraries {
    class OS {
      static readonly Stopwatch _stop = Stopwatch.StartNew();

      public static void Initialize(ILuaEnvironment env) {
        ILuaTable os = new LuaTable(env);
        Register(env, os, (Func<double>)clock);
        Register(env, os, (Func<string, object, object>)date);
        Register(env, os, (Func<double, double, double>)difftime);
        Register(env, os, (Action<object>)exit);
        Register(env, os, (Func<string, string?>)getenv);
        Register(env, os, (Func<string, LuaMultiValue>)remove);
        Register(env, os, (Func<string, string, LuaMultiValue>)rename);
        Register(env, os, (Func<string, string?>)setlocale);
        Register(env, os, (Func<object, double>)time);
        Register(env, os, (Func<string>)tmpname);

        env.GlobalsTable.SetItemRaw(new LuaString("os"), os);
      }

      static double clock() {
        return _stop.Elapsed.TotalSeconds;
      }
      static object date(string format = "%c", object? source = null) {
        DateTime time;

        if (source is double d) {
          time = new DateTime((long)d);
        } else if (source is DateTime dt) {
          time = dt;
        } else {
          time = DateTime.Now;
        }

        if (format.StartsWith("!")) {
          format = format.Substring(1);
          time = time.ToUniversalTime();
        }

        if (format == "*t") {
          ILuaTable table = new LuaTable(LuaEnvironment.CurrentEnvironment);
          void set(string a, int b) {
            table.SetItemRaw(new LuaString(a), LuaNumber.Create(b));
          }
          set("year", time.Year);
          set("month", time.Month);
          set("day", time.Day);
          set("hour", time.Hour);
          set("min", time.Minute);
          set("sec", time.Second);
          set("wday", ((int)time.DayOfWeek) + 1);
          set("yday", time.DayOfYear);

          return table;
        }

        StringBuilder ret = new StringBuilder();
        for (int i = 0; i < format.Length; i++) {
          if (format[i] == '%') {
            i++;
            switch (format[i]) {
              case 'a':
                ret.Append(time.ToString("ddd", CultureInfo.CurrentCulture));
                break;
              case 'A':
                ret.Append(time.ToString("dddd", CultureInfo.CurrentCulture));
                break;
              case 'b':
                ret.Append(time.ToString("MMM", CultureInfo.CurrentCulture));
                break;
              case 'B':
                ret.Append(time.ToString("MMMM", CultureInfo.CurrentCulture));
                break;
              case 'c':
                ret.Append(time.ToString("F", CultureInfo.CurrentCulture));
                break;
              case 'd':
                ret.Append(time.ToString("dd", CultureInfo.CurrentCulture));
                break;
              case 'H':
                ret.Append(time.ToString("HH", CultureInfo.CurrentCulture));
                break;
              case 'I':
                ret.Append(time.ToString("hh", CultureInfo.CurrentCulture));
                break;
              case 'j':
                ret.Append(time.DayOfYear.ToString("d3", CultureInfo.CurrentCulture));
                break;
              case 'm':
                ret.Append(time.Month.ToString("d2", CultureInfo.CurrentCulture));
                break;
              case 'M':
                ret.Append(time.Minute.ToString("d2", CultureInfo.CurrentCulture));
                break;
              case 'p':
                ret.Append(time.ToString("tt", CultureInfo.CurrentCulture));
                break;
              case 'S':
                ret.Append(time.ToString("ss", CultureInfo.CurrentCulture));
                break;
              case 'U': {
                // See strftime
                DateTimeFormatInfo dfi = DateTimeFormatInfo.CurrentInfo;
                ret.AppendFormat(
                    "{0:02}",
                    dfi.Calendar.GetWeekOfYear(
                        time, CalendarWeekRule.FirstFullWeek, DayOfWeek.Sunday));
                break;
              }
              case 'V': {
                // See strftime
                DateTimeFormatInfo dfi = DateTimeFormatInfo.CurrentInfo;
                ret.AppendFormat(
                    "{0:02}", dfi.Calendar.GetWeekOfYear(
                        time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday));
                break;
              }
              case 'w':
                ret.Append((int)time.DayOfWeek);
                break;
              case 'W': {
                // See strftime
                DateTimeFormatInfo dfi = DateTimeFormatInfo.CurrentInfo;
                ret.AppendFormat(
                    "{0:02}",
                    dfi.Calendar.GetWeekOfYear(
                        time, CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday));
                break;
              }
              case 'x':
                ret.Append(time.ToString("d", CultureInfo.CurrentCulture));
                break;
              case 'X':
                ret.Append(time.ToString("T", CultureInfo.CurrentCulture));
                break;
              case 'y':
                ret.Append(time.ToString("yy", CultureInfo.CurrentCulture));
                break;
              case 'Y':
                ret.Append(time.ToString("yyyy", CultureInfo.CurrentCulture));
                break;
              case 'Z':
                ret.Append(time.ToString("%K", CultureInfo.CurrentCulture));
                break;
              case '%':
                ret.Append('%');
                break;
              default:
                throw new ArgumentException(
                    $"Unrecognized format specifier %{format[i]} in function 'os.date'.");
            }
          } else {
            ret.Append(format[i]);
          }
        }
        return ret.ToString();
      }
      static double difftime(double time1, double time2) {
        return time2 - time1;
      }
      static void exit(object? code = null) {
        int icode;
        if (code != null) {
          if (code as bool? == true) {
            icode = 0;
          } else if (code is double d) {
            icode = (int)System.Math.Round(d);
          } else {
            icode = 1;
          }
        } else {
          icode = 0;
        }
        LuaEnvironment.CurrentEnvironment.Settings._callQuit(LuaEnvironment.CurrentEnvironment,
                                                             icode);
      }
      static string? getenv(string name) {
        return Environment.GetEnvironmentVariable(name);
      }
      [MultipleReturn]
      static LuaMultiValue remove(string path) {
        if (File.Exists(path)) {
          return _pcallInternal(() => File.Delete(path));
        } else if (Directory.Exists(path)) {
          return _pcallInternal(() => Directory.Delete(path));
        } else {
          return LuaMultiValue.CreateMultiValueFromObj(false, "Specified filename does not exist.");
        }
      }
      [MultipleReturn]
      static LuaMultiValue rename(string old, string new_) {
        if (File.Exists(old)) {
          return _pcallInternal(() => File.Move(old, new_));
        } else if (Directory.Exists(old)) {
          return _pcallInternal(() => Directory.Move(old, new_));
        } else {
          return LuaMultiValue.CreateMultiValueFromObj(false, "Specified path does not exist.");
        }
      }
      static string? setlocale(string? name) {
        if (name == null) {
          return Thread.CurrentThread.CurrentCulture.Name;
        }
        try {
          CultureInfo ci = CultureInfo.GetCultureInfo(name);
          if (ci == null) {
            return null;
          }

          Thread.CurrentThread.CurrentCulture = ci;
          return ci.Name;
        } catch (Exception) {
          return null;
        }
      }
      static double time(object source) {
        DateTime time;

        if (source is ILuaTable table) {
          int year, month, day, hour, min, sec;
          int get(string name, bool req) {
            ILuaValue value = table.GetItemRaw(new LuaString(name));
            if (value == null || value.ValueType != LuaValueType.Number) {
              if (req) {
                throw new ArgumentException(
                    "First argument to function 'os.time' is not a valid time table.");
              } else {
                return 0;
              }
            } else {
              return value.As<int>();
            }
          }

          year = get("year", true);
          month = get("month", true);
          day = get("day", true);
          hour = get("hour", false);
          min = get("min", false);
          sec = get("sec", false);

          time = new DateTime(year, month, day, hour, min, sec);
        } else if (source is DateTime dt) {
          time = dt;
        } else if (source is DateTimeOffset dto) {
          time = dto.LocalDateTime;
        } else if (source != null) {
          throw new ArgumentException("First argument to function 'os.time' must be a table.");
        } else {
          time = DateTime.Now;
        }

        return time.Ticks;
      }
      static string tmpname() {
        return Path.GetTempFileName();
      }
    }
  }
}
