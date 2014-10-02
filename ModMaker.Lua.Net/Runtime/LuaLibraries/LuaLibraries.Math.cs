using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        /// <summary>
        /// Contains the math Lua libraries.  Contains a single visible member
        /// Initialize that creates a new table for the methods.
        /// </summary>
        static class Math
        {
            /// <summary>
            /// A helper class for the functions that simply pass one or
            /// two arguments to the respective System.Math function.
            /// </summary>
            sealed class MathHelper : LuaFrameworkMethod
            {
                Func<double, double> f1;
                Func<double, double, double> f2;

                public MathHelper(ILuaEnvironment E, string name, Func<double, double> func)
                    : base(E, name)
                {
                    this.f1 = func;
                    this.f2 = null;
                }
                public MathHelper(ILuaEnvironment E, string name, Func<double, double, double> func)
                    : base(E, name)
                {
                    this.f1 = null;
                    this.f2 = func;
                }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    args = args ?? new object[0];

                    if (f1 != null && args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function '" + Name + "'.");
                    if (f2 != null && args.Length < 2)
                        throw new ArgumentException("Expecting two arguments to function '" + Name + "'.");

                    object obj = args[0];

                    if (!(obj is double))
                        throw new ArgumentException("First argument to '" + Name + "' must be a number.");
                    double num = (double)obj;

                    if (f1 != null)
                        return new MultipleReturn(f1(num));
                    else
                    {
                        obj = args[1];
                        if (!(obj is double))
                            throw new ArgumentException("Second argument to '" + Name + "' must be a number.");

                        return new MultipleReturn(f2(num, (double)obj));
                    }
                }
            }

            /// <summary>
            /// Creates a new Table that contains all the math functions.
            /// </summary>
            /// <returns>A new table with the math functions.</returns>
            public static ILuaTable Initialize(ILuaEnvironment E)
            {
                ILuaTable math = new LuaTableNet();
                math.SetItemRaw("abs", new MathHelper(E, "math.abs", System.Math.Abs));
                math.SetItemRaw("acos", new MathHelper(E, "math.acos", System.Math.Acos));
                math.SetItemRaw("asin", new MathHelper(E, "math.asin", System.Math.Asin));
                math.SetItemRaw("atan", new MathHelper(E, "math.atan", System.Math.Atan));
                math.SetItemRaw("atan2", new MathHelper(E, "math.atan2", System.Math.Atan2));
                math.SetItemRaw("ceil", new MathHelper(E, "math.ceil", System.Math.Ceiling));
                math.SetItemRaw("cos", new MathHelper(E, "math.cos", System.Math.Cos));
                math.SetItemRaw("cosh", new MathHelper(E, "math.cosh", System.Math.Cosh));
                math.SetItemRaw("deg", new Math.deg(E));
                math.SetItemRaw("exp", new MathHelper(E, "math.exp", System.Math.Exp));
                math.SetItemRaw("floor", new MathHelper(E, "math.floor", System.Math.Floor));
                math.SetItemRaw("fmod", new MathHelper(E, "math.fmod", System.Math.IEEERemainder));
                math.SetItemRaw("frexp", new Math.frexp(E));
                math.SetItemRaw("huge", double.PositiveInfinity);
                math.SetItemRaw("ldexp", new Math.ldexp(E));
                math.SetItemRaw("log", new Math.log(E));
                math.SetItemRaw("max", new Math.max(E));
                math.SetItemRaw("min", new Math.min(E));
                math.SetItemRaw("modf", new Math.modf(E));
                math.SetItemRaw("pi", System.Math.PI);
                math.SetItemRaw("pow", new MathHelper(E, "math.pow", System.Math.Pow));
                math.SetItemRaw("rad", new Math.rad(E));
                math.SetItemRaw("random", new Math.random(E));
                math.SetItemRaw("randomseed", new Math.randomseed(E));
                math.SetItemRaw("sin", new MathHelper(E, "math.sin", System.Math.Sin));
                math.SetItemRaw("sinh", new MathHelper(E, "math.sinh", System.Math.Sinh));
                math.SetItemRaw("sqrt", new MathHelper(E, "math.sqrt", System.Math.Sqrt));
                math.SetItemRaw("tan", new MathHelper(E, "math.tan", System.Math.Tan));
                math.SetItemRaw("tanh", new MathHelper(E, "math.tanh", System.Math.Tanh));

                return math;
            }

            sealed class deg : LuaFrameworkMethod
            {
                public deg(ILuaEnvironment E) : base(E, "math.deg") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'math.deg'.");
                    if (!(args[0] is double))
                        throw new ArgumentException("First argument to 'math.deg' must be a number.");

                    return new MultipleReturn(((double)args[0] * 180 / System.Math.PI));
                }
            }
            sealed class frexp : LuaFrameworkMethod
            {
                public frexp(ILuaEnvironment E) : base(E, "math.frexp") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'math.frexp'.");

                    object obj = args[0];

                    if (obj == null || !(obj is double))
                        throw new ArgumentException("First argument to 'math.frexp' must be a number.");

                    double d = (double)obj;
                    double m, e;

                    if (d == 0)
                    {
                        return new MultipleReturn(0, 0);
                    }

                    bool b = d < 0;
                    d = b ? -d : d;
                    e = System.Math.Ceiling(System.Math.Log(d, 2));
                    m = d / System.Math.Pow(2, e);
                    m = b ? -m : m;

                    return new MultipleReturn(m, e);
                }
            }
            sealed class ldexp : LuaFrameworkMethod
            {
                public ldexp(ILuaEnvironment E) : base(E, "math.ldexp") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting two arguments to function 'math.ldexp'.");

                    object obj = args[0];
                    object obj2 = args[1];

                    if (obj == null || !(obj is double))
                        throw new ArgumentException("First argument to 'math.ldexp' must be a number.");
                    if (obj2 == null || !(obj2 is double) || ((double)obj2 % 1 != 0))
                        throw new ArgumentException("Second argument to 'math.ldexp' must be a integer.");

                    return new MultipleReturn((double)obj * System.Math.Pow(2.0, (double)obj2));
                }
            }
            sealed class log : LuaFrameworkMethod
            {
                public log(ILuaEnvironment E) : base(E, "math.log") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'math.log'.");

                    object obj = args[0];
                    object obj2 = args[1];

                    if (obj == null || !(obj is double))
                        throw new ArgumentException("First argument to 'math.log' must be a number.");
                    if (obj2 != null && !(obj2 is double))
                        throw new ArgumentException("Second argument to 'math.log' must be a number.");

                    if (obj2 != null)
                        return new MultipleReturn(System.Math.Log((double)obj, (double)obj2));
                    else
                        return new MultipleReturn(System.Math.Log((double)obj));
                }
            }
            sealed class max : LuaFrameworkMethod
            {
                public max(ILuaEnvironment E) : base(E, "math.max") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'math.max'.");

                    object obj = args[0];

                    if (obj == null || !(obj is double))
                        throw new ArgumentException("First argument to 'math.max' must be a number.");

                    double ret = (double)obj;

                    for (int i = 1; i < args.Length; i++)
                    {
                        object obj2 = args[0];
                        if (obj2 == null || !(obj2 is double))
                            throw new ArgumentException("Argument number '" + i + "' to 'math.max' must be a number.");

                        double d = (double)obj2;
                        if (d > ret)
                            ret = d;
                    }

                    return new MultipleReturn(ret);
                }
            }
            sealed class min : LuaFrameworkMethod
            {
                public min(ILuaEnvironment E) : base(E, "math.min") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'math.min'.");
                    if (args[0] == null || !(args[0] is double))
                        throw new ArgumentException("First argument to 'math.min' must be a number.");

                    double ret = (double)args[0];
                    for (int i = 1; i < args.Length; i++)
                    {
                        object obj2 = args[i];
                        if (obj2 == null || !(obj2 is double))
                            throw new ArgumentException("Argument number '" + i + "' to 'math.min' must be a number.");

                        double d = (double)obj2;
                        if (d < ret)
                            ret = d;
                    }

                    return new MultipleReturn(ret);
                }
            }
            sealed class modf : LuaFrameworkMethod
            {
                public modf(ILuaEnvironment E) : base(E, "math.modf") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'math.modf'.");
                    if (args[0] == null || !(args[0] is double))
                        throw new ArgumentException("First argument to 'math.modf' must be a number.");

                    double d = (double)args[0];
                    return new MultipleReturn(System.Math.Floor(d), (d - System.Math.Floor(d)));
                }
            }
            sealed class rad : LuaFrameworkMethod
            {
                public rad(ILuaEnvironment E) : base(E, "math.rad") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args == null || args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'math.rad'.");
                    if (args[0] == null || !(args[0] is double))
                        throw new ArgumentException("First argument to 'math.rad' must be a number.");

                    return new MultipleReturn(((double)args[0] * System.Math.PI / 180));
                }
            }
            sealed class random : LuaFrameworkMethod
            {
                public random(ILuaEnvironment E) : base(E, "math.random") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    object obj = args.Length > 0 ? args[0] : null;
                    object obj2 = args.Length > 1 ? args[1] : null;

                    if (obj != null && !(obj is double))
                        throw new ArgumentException("First argument to 'math.random' must be a number.");
                    if (obj2 != null && !(obj2 is double))
                        throw new ArgumentException("Second argument to 'math.random' must be a number.");

                    if (obj == null)
                    {
                        lock (_randLock)
                            return new MultipleReturn(Rand.NextDouble());
                    }
                    else
                    {
                        double m = (double)obj;
                        if (obj2 == null)
                        {
                            lock (_randLock)
                                return new MultipleReturn((double)Rand.Next((int)m));
                        }
                        else
                        {
                            double n = (double)obj2;

                            lock (_randLock)
                                return new MultipleReturn((double)Rand.Next((int)m, (int)n));
                        }
                    }
                }
            }
            sealed class randomseed : LuaFrameworkMethod
            {
                public randomseed(ILuaEnvironment E) : base(E, "math.randomseed") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'math.randomseed'.");

                    object obj = args[0];

                    if (obj == null || !(obj is double) || ((double)obj % 1 != 0))
                        throw new ArgumentException("First argument to 'math.randomseed' must be an integer.");

                    lock (_randLock)
                    {
                        Rand = new Random((int)(double)obj);
                    }
                    return new MultipleReturn();
                }
            }

            static Random Rand = new Random(Guid.NewGuid().GetHashCode());
            static object _randLock = new object();
        }
    }
}