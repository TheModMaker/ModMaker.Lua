using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        static class Math
        {
            public static void Initialize(ILuaEnvironment E)
            {
                ILuaValue math = E.Runtime.CreateTable();
                Register(E, math, (Func<double, double>)System.Math.Abs, "abs");
                Register(E, math, (Func<double, double>)System.Math.Asin, "asin");
                Register(E, math, (Func<double, double>)System.Math.Atan, "atan");
                Register(E, math, (Func<double, double, double>)System.Math.Atan2, "atan2");
                Register(E, math, (Func<double, double>)System.Math.Ceiling, "ceil");
                Register(E, math, (Func<double, double>)System.Math.Cos, "cos");
                Register(E, math, (Func<double, double>)System.Math.Cosh, "cosh");
                Register(E, math, (Func<double, double>)deg);
                Register(E, math, (Func<double, double>)System.Math.Exp, "exp");
                Register(E, math, (Func<double, double>)System.Math.Floor, "floor");
                Register(E, math, (Func<double, double, double>)System.Math.IEEERemainder, "fmod");
                Register(E, math, (Func<double, double[]>)frexp);
                math.SetIndex(E.Runtime.CreateValue("huge"),
                              E.Runtime.CreateValue(double.PositiveInfinity));
                Register(E, math, (Func<double, double, double>)ldexp);
                Register(E, math, (Func<double, double, double>)log);
                Register(E, math, (Func<double, double[], double>)max);
                Register(E, math, (Func<double, double[], double>)min);
                Register(E, math, (Func<double, double[]>)modf);
                math.SetIndex(E.Runtime.CreateValue("pi"),
                              E.Runtime.CreateValue(System.Math.PI));
                Register(E, math, (Func<double, double, double>)System.Math.Pow, "pow");
                Register(E, math, (Func<double, double>)rad);
                Register(E, math, (Func<int?, int?, double>)random);
                Register(E, math, (Action<int>)randomseed);
                Register(E, math, (Func<double, double>)System.Math.Sin, "sin");
                Register(E, math, (Func<double, double>)System.Math.Sinh, "sinh");
                Register(E, math, (Func<double, double>)System.Math.Sqrt, "sqrt");
                Register(E, math, (Func<double, double>)System.Math.Tan, "tan");
                Register(E, math, (Func<double, double>)System.Math.Tanh, "tanh");

                E.GlobalsTable.SetItemRaw(E.Runtime.CreateValue("math"), math);
            }

            static double deg(double arg)
            {
                return (arg * 180 / System.Math.PI);
            }
            [MultipleReturn]
            static double[] frexp(double d)
            {
                double m, e;

                if (d == 0)
                {
                    return new double[] { 0, 0 };
                }

                bool neg = d < 0;
                d = System.Math.Abs(d);
                e = System.Math.Ceiling(System.Math.Log(d, 2));
                m = d / System.Math.Pow(2, e);
                m = neg ? -m : m;

                return new double[] { m, e };
            }
            static double ldexp(double m, double e)
            {
                return m * System.Math.Pow(2.0, e);
            }
            static double log(double x, double base_ = System.Math.E)
            {
                return System.Math.Log(x, base_);
            }
            static double max(double x, params double[] args)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] > x)
                        x = args[i];
                }
                return x;
            }
            static double min(double x, params double[] args)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] < x)
                        x = args[i];
                }
                return x;
            }
            [MultipleReturn]
            static double[] modf(double d)
            {
                var floor = d >= 0 ? System.Math.Floor(d) : System.Math.Ceiling(d);
                return new[] { floor, (d - floor) };
            }
            static double rad(double arg)
            {
                return (arg * System.Math.PI / 180);
            }
            static double random(int? min = null, int? max = null)
            {
                if (min == null)
                    return rand_.NextDouble();
                else if (max == null)
                    return rand_.Next(min.Value);
                else
                    return rand_.Next(min.Value, max.Value);
            }
            static void randomseed(int seed)
            {
                lock(randLock_)
                {
                    rand_ = new Random(seed);
                }
            }

            static Random rand_ = new Random(Guid.NewGuid().GetHashCode());
            static object randLock_ = new object();
        }
    }
}