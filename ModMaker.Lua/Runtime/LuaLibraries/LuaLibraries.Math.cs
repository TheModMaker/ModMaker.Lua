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
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Runtime {
  static partial class LuaStaticLibraries {
    static class Math {
      public static void Initialize(ILuaEnvironment env) {
        ILuaValue math = new LuaTable(env);
        Register(env, math, (Func<double, double>)System.Math.Abs, "abs");
        Register(env, math, (Func<double, double>)System.Math.Asin, "asin");
        Register(env, math, (Func<double, double>)System.Math.Atan, "atan");
        Register(env, math, (Func<double, double, double>)System.Math.Atan2, "atan2");
        Register(env, math, (Func<double, double>)System.Math.Ceiling, "ceil");
        Register(env, math, (Func<double, double>)System.Math.Cos, "cos");
        Register(env, math, (Func<double, double>)System.Math.Cosh, "cosh");
        Register(env, math, (Func<double, double>)deg);
        Register(env, math, (Func<double, double>)System.Math.Exp, "exp");
        Register(env, math, (Func<double, double>)System.Math.Floor, "floor");
        Register(env, math, (Func<double, double, double>)System.Math.IEEERemainder, "fmod");
        Register(env, math, (Func<double, double[]>)frexp);
        math.SetIndex(new LuaString("huge"), LuaNumber.Create(double.PositiveInfinity));
        Register(env, math, (Func<double, double, double>)ldexp);
        Register(env, math, (Func<double, double, double>)log);
        Register(env, math, (Func<double, double[], double>)max);
        Register(env, math, (Func<double, double[], double>)min);
        Register(env, math, (Func<double, double[]>)modf);
        math.SetIndex(new LuaString("pi"), LuaNumber.Create(System.Math.PI));
        Register(env, math, (Func<double, double, double>)System.Math.Pow, "pow");
        Register(env, math, (Func<double, double>)rad);
        Register(env, math, (Func<int?, int?, double>)random);
        Register(env, math, (Action<int>)randomseed);
        Register(env, math, (Func<double, double>)System.Math.Sin, "sin");
        Register(env, math, (Func<double, double>)System.Math.Sinh, "sinh");
        Register(env, math, (Func<double, double>)System.Math.Sqrt, "sqrt");
        Register(env, math, (Func<double, double>)System.Math.Tan, "tan");
        Register(env, math, (Func<double, double>)System.Math.Tanh, "tanh");

        env.GlobalsTable.SetItemRaw(new LuaString("math"), math);
      }

      static double deg(double arg) {
        return (arg * 180 / System.Math.PI);
      }
      [MultipleReturn]
      static double[] frexp(double d) {
        double m, e;

        if (d == 0) {
          return new double[] { 0, 0 };
        }

        bool neg = d < 0;
        d = System.Math.Abs(d);
        e = System.Math.Ceiling(System.Math.Log(d, 2));
        m = d / System.Math.Pow(2, e);
        m = neg ? -m : m;

        return new double[] { m, e };
      }
      static double ldexp(double m, double e) {
        return m * System.Math.Pow(2.0, e);
      }
      static double log(double x, double base_ = System.Math.E) {
        return System.Math.Log(x, base_);
      }
      static double max(double x, params double[] args) {
        for (int i = 0; i < args.Length; i++) {
          if (args[i] > x) {
            x = args[i];
          }
        }
        return x;
      }
      static double min(double x, params double[] args) {
        for (int i = 0; i < args.Length; i++) {
          if (args[i] < x) {
            x = args[i];
          }
        }
        return x;
      }
      [MultipleReturn]
      static double[] modf(double d) {
        var floor = d >= 0 ? System.Math.Floor(d) : System.Math.Ceiling(d);
        return new[] { floor, d - floor };
      }
      static double rad(double arg) {
        return (arg * System.Math.PI / 180);
      }
      static double random(int? min = null, int? max = null) {
        if (min == null) {
          return _rand.NextDouble();
        } else if (max == null) {
          return _rand.Next(1, min.Value);
        } else {
          return _rand.Next(min.Value, max.Value);
        }
      }
      static void randomseed(int seed) {
        lock (_randLock) {
          _rand = new Random(seed);
        }
      }

      static Random _rand = new Random(Guid.NewGuid().GetHashCode());
      static readonly object _randLock = new object();
    }
  }
}
