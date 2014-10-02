using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;
using System.Threading;
using System.Diagnostics;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        /// <summary>
        /// Contains the OS Lua libraries.  Contains a single visible member
        /// Initialize that creates a new table for the methods.
        /// </summary>
        static class OS
        {
            static Stopwatch stop = Stopwatch.StartNew();

            public static ILuaTable Initialize(ILuaEnvironment E)
            {
                ILuaTable os = new LuaTableNet();
                os.SetItemRaw("clock", new OS.clock(E));
                os.SetItemRaw("date", new OS.date(E));
                os.SetItemRaw("difftime", new OS.difftime(E));
                os.SetItemRaw("exit", new OS.exit(E));
                os.SetItemRaw("getenv", new OS.getenv(E));
                os.SetItemRaw("remove", new OS.remove(E));
                os.SetItemRaw("rename", new OS.rename(E));
                os.SetItemRaw("setlocale", new OS.setlocale(E));
                os.SetItemRaw("time", new OS.time(E));
                os.SetItemRaw("tmpname", new OS.tmpname(E));

                return os;
            }

            sealed class clock : LuaFrameworkMethod
            {
                public clock(ILuaEnvironment E) : base(E, "os.clock") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    return new MultipleReturn(stop.Elapsed.TotalSeconds);
                }
            }
            sealed class date : LuaFrameworkMethod
            {
                public date(ILuaEnvironment E) : base(E, "os.date") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    string format = (args.Length > 0 ? args[0] : null) as string ?? "%c";
                    object obj = args.Length > 1 ? args[1] : null;
                    DateTimeOffset time;

                    if (obj is double)
                        time = new DateTime(Convert.ToInt64((double)obj));
                    else if (obj is DateTime)
                        time = (DateTime)obj;
                    else if (obj is DateTimeOffset)
                        time = ((DateTimeOffset)obj);
                    else
                        time = DateTimeOffset.Now;

                    if (format.Length > 0 && format[0] == '!')
                    {
                        format = format.Substring(1);
                        time = time.ToUniversalTime();
                    }

                    if (format == "*type")
                    {
                        ILuaTable tab = new LuaTableNet();
                        tab.SetItemRaw("year", Convert.ToDouble(time.Year));
                        tab.SetItemRaw("month", Convert.ToDouble(time.Month));
                        tab.SetItemRaw("day", Convert.ToDouble(time.Day));
                        tab.SetItemRaw("hour", Convert.ToDouble(time.Hour));
                        tab.SetItemRaw("min", Convert.ToDouble(time.Minute));
                        tab.SetItemRaw("sec", Convert.ToDouble(time.Second));
                        tab.SetItemRaw("wday", Convert.ToDouble(((int)time.DayOfWeek) + 1));
                        tab.SetItemRaw("yday", Convert.ToDouble(time.DayOfYear));

                        return new MultipleReturn((object)tab);
                    }

                    StringBuilder ret = new StringBuilder();
                    for (int i = 0; i < format.Length; i++)
                    {
                        if (format[i] == '%')
                        {
                            i++;
                            switch (format[i])
                            {
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
                                case 'U':
                                    throw new NotSupportedException("The %U format specifier is not supported by this framework.");
                                case 'w':
                                    ret.Append((int)time.DayOfWeek);
                                    break;
                                case 'W':
                                    throw new NotSupportedException("The %W format specifier is not supported by this framework.");
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
                                    throw new ArgumentException("Unrecognised format specifier %" + format[i] + " in function 'os.date'.");
                            }
                        }
                        else
                            ret.Append(format[i]);
                    }
                    return new MultipleReturn((object)ret.ToString());
                }
            }
            sealed class difftime : LuaFrameworkMethod
            {
                public difftime(ILuaEnvironment E) : base(E, "os.difftime") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting two arguments to function 'os.difftime'.");

                    object obj1 = args[0];
                    object obj2 = args[1];

                    if (obj1 == null || !(obj1 is double))
                        throw new ArgumentException("First argument to function 'os.difftime' must be a number.");
                    if (obj2 == null || !(obj2 is double))
                        throw new ArgumentException("Second argument to function 'os.difftime' must be a number.");

                    double d2 = (double)obj1, d1 = (double)obj2;

                    return new MultipleReturn(d2 - d1);
                }
            }
            sealed class exit : LuaFrameworkMethod
            {
                public exit(ILuaEnvironment E) : base(E, "os.exit") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    object code = args != null && args.Length > 0 ? args[0] : null;
                    object close = args != null && args.Length > 1 ? args[1] : null;

                    Environment.Settings.CallQuit(Environment, code, close);
                    return new MultipleReturn();
                }
            }
            sealed class getenv : LuaFrameworkMethod
            {
                public getenv(ILuaEnvironment E) : base(E, "os.getenv") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'os.getenv'.");

                    string var = args[0] as string;

                    if (var == null)
                        throw new ArgumentException("First argument to function 'os.getenv' must be a string.");

                    var ret = System.Environment.GetEnvironmentVariable(var);
                    return new MultipleReturn((object)ret);
                }
            }
            sealed class remove : LuaFrameworkMethod
            {
                public remove(ILuaEnvironment E) : base(E, "os.remove") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args == null || args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'os.remove'.");

                    string var = args[0] as string;

                    if (var == null)
                        throw new ArgumentException("First argument to function 'os.remove' must be a string.");

                    if (File.Exists(var))
                    {
                        try
                        {
                            File.Delete(var);
                            return new MultipleReturn(true);
                        }
                        catch (Exception e)
                        {
                            return new MultipleReturn(null, e.Message, e);
                        }
                    }
                    else if (Directory.Exists(var))
                    {
                        if (Directory.EnumerateFileSystemEntries(var).Count() > 0)
                            return new MultipleReturn(null, "Specified directory is not empty.");

                        try
                        {
                            Directory.Delete(var);
                            return new MultipleReturn(true);
                        }
                        catch (Exception e)
                        {
                            return new MultipleReturn(null, e.Message, e);
                        }
                    }
                    else
                        return new MultipleReturn(null, "Specified filename does not exist.");
                }
            }
            sealed class rename : LuaFrameworkMethod
            {
                public rename(ILuaEnvironment E) : base(E, "os.rename") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting two arguments to function 'os.rename'.");

                    string old = args[0] as string;
                    string neww = args[1] as string;

                    if (old == null)
                        throw new ArgumentException("First argument to function 'os.rename' must be a string.");
                    if (neww == null)
                        throw new ArgumentException("Second argument to function 'os.rename' must be a string.");

                    if (File.Exists(old))
                    {
                        try
                        {
                            File.Move(old, neww);
                            return new MultipleReturn(true);
                        }
                        catch (Exception e)
                        {
                            return new MultipleReturn(null, e.Message, e);
                        }
                    }
                    else if (Directory.Exists(old))
                    {
                        try
                        {
                            Directory.Move(old, neww);
                            return new MultipleReturn(true);
                        }
                        catch (Exception e)
                        {
                            return new MultipleReturn(null, e.Message, e);
                        }
                    }
                    else
                        return new MultipleReturn(null, "Specified path does not exist.");
                }
            }
            sealed class setlocale : LuaFrameworkMethod
            {
                public setlocale(ILuaEnvironment E) : base(E, "os.setlocale") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    string locale = (args.Length > 0 ? args[0] : null) as string;

                    if (locale == null)
                        return new MultipleReturn((object)Thread.CurrentThread.CurrentCulture.Name);
                    else
                    {
                        try
                        {
                            CultureInfo ci = CultureInfo.GetCultureInfo(locale);
                            if (ci == null)
                                return new MultipleReturn();

                            Thread.CurrentThread.CurrentCulture = ci;
                            return new MultipleReturn((object)ci.Name);
                        }
                        catch (Exception)
                        {
                            return new MultipleReturn();
                        }
                    }
                }
            }
            sealed class time : LuaFrameworkMethod
            {
                public time(ILuaEnvironment E) : base(E, "os.time") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    DateTime time;
                    object table = args != null && args.Length > 0 ? args[0] : null;

                    if (table != null)
                    {
                        if (table is ILuaTable)
                        {
                            ILuaTable t = table as ILuaTable;
                            int year, month, day, hour, min, sec;

                            object obj = t.GetItemRaw("year");
                            if (!(obj is double))
                                throw new ArgumentException("First argument to function 'os.time' is not a valid time table.");
                            year = Convert.ToInt32((double)obj);

                            obj = t.GetItemRaw("month");
                            if (!(obj is double))
                                throw new ArgumentException("First argument to function 'os.time' is not a valid time table.");
                            month = Convert.ToInt32((double)obj);

                            obj = t.GetItemRaw("day");
                            if (!(obj is double))
                                throw new ArgumentException("First argument to function 'os.time' is not a valid time table.");
                            day = Convert.ToInt32((double)obj);

                            obj = t.GetItemRaw("hour");
                            if (obj is double)
                                hour = Convert.ToInt32((double)obj);
                            else
                                hour = 12;

                            obj = t.GetItemRaw("min");
                            if (obj is double)
                                min = Convert.ToInt32((double)obj);
                            else
                                min = 0;

                            obj = t.GetItemRaw("sec");
                            if (obj is double)
                                sec = Convert.ToInt32((double)obj);
                            else
                                sec = 0;

                            time = new DateTime(year, month, day, hour, min, sec);
                        }
                        else if (table is DateTime)
                            time = (DateTime)table;
                        else if (table is DateTimeOffset)
                            time = ((DateTimeOffset)table).LocalDateTime;
                        else
                            throw new ArgumentException("First argument to function 'os.time' must be a table.");
                    }
                    else
                        time = DateTime.Now;

                    return new MultipleReturn(Convert.ToDouble(time.Ticks));
                }
            }
            sealed class tmpname : LuaFrameworkMethod
            {
                public tmpname(ILuaEnvironment E) : base(E, "os.tmpname") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    return new MultipleReturn((object)Path.GetTempFileName());
                }
            }
        }
    }
}
