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
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Runtime
{
    static partial class LuaStaticLibraries
    {
        static class Bit32
        {
            public static void Initialize(ILuaEnvironment E)
            {
                ILuaValue bit32 = E.Runtime.CreateTable();
                Register(E, bit32, (Func<double, int, uint>)arshift);
                Register(E, bit32, (Func<double[], uint>)band);
                Register(E, bit32, (Func<double, uint>)bnot);
                Register(E, bit32, (Func<double[], uint>)bor);
                Register(E, bit32, (Func<double[], bool>)btest);
                Register(E, bit32, (Func<double[], uint>)bxor);
                Register(E, bit32, (Func<double, int, int, uint>)extract);
                Register(E, bit32, (Func<double, double, int, int, uint>)replace);
                Register(E, bit32, (Func<double, int, uint>)lrotate);
                Register(E, bit32, (Func<double, int, uint>)lshift);
                Register(E, bit32, (Func<double, int, uint>)rrotate);
                Register(E, bit32, (Func<double, int, uint>)rshift);

                E.GlobalsTable.SetItemRaw(E.Runtime.CreateValue("bit32"), bit32);
            }

            // NOTE: This uses double as an argument since using Convert.ToUint will fail to
            // convert larger numbers.  So we accept a double and then cast to uint manually
            // which will truncate the number to 2^32.

            [IgnoreExtraArguments]
            static uint arshift(double x, int disp)
            {
                if (System.Math.Abs(disp) > 31)
                    return x >= 0x800000 && disp > 0 ? 0xffffffff : 0;

                var xAsInt = (int)((uint)x & 0xffffffff);
                if (disp >= 0)
                    return (uint)(xAsInt >> disp);
                else
                    return (uint)xAsInt << -disp;
            }
            static uint band(params double[] args)
            {
                return args.Select(a => (uint)a).Aggregate(uint.MaxValue, (a, b) => a & b);
            }
            [IgnoreExtraArguments]
            static uint bnot(double x)
            {
                return ~(uint)x;
            }
            static uint bor(params double[] args)
            {
                return args.Select(a => (uint)a).Aggregate(0u, (a, b) => a | b);
            }
            static bool btest(params double[] args)
            {
                return band(args) != 0;
            }
            static uint bxor(params double[] args)
            {
                return args.Select(a => (uint)a).Aggregate(0u, (a, b) => a ^ b);
            }
            [IgnoreExtraArguments]
            static uint extract(double sourceDouble, int field, int width = 1)
            {
                if (width + field > 31 || field < 0 || width < 0)
                {
                    throw new ArgumentException(
                            "Attempt to access bits outside the allowed range.");
                }

                uint source = (uint)sourceDouble;
                uint mask = (uint)((1 << width) - 1);
                return ((source >> field) & mask);
            }
            [IgnoreExtraArguments]
            static uint replace(double sourceDouble, double replDouble, int field, int width = 1)
            {
                uint source = (uint)sourceDouble;
                uint repl = (uint)replDouble;
                if (width + field > 31 || field < 0 || width < 0)
                {
                    throw new ArgumentException(
                            "Attempt to access bits outside the allowed range.");
                }

                uint mask = (1u << width) - 1;
                repl &= mask;
                source &= ~(mask << field);
                return (source | (repl << field));
            }
            [IgnoreExtraArguments]
            static uint lrotate(double xDouble, int disp)
            {
                // % will still remain negative.
                uint x = (uint)xDouble;
                disp %= 32;
                if (disp >= 0)
                    return ((x << disp) | (x >> (32 - disp)));
                else
                    return ((x >> -disp) | (x << (32 + disp)));
            }
            [IgnoreExtraArguments]
            static uint lshift(double xDouble, int disp)
            {
                uint x = (uint)xDouble;
                if (System.Math.Abs(disp) > 31)
                    return 0;
                else if (disp >= 0)
                    return x << disp;
                else
                    return x >> -disp;
            }
            [IgnoreExtraArguments]
            static uint rrotate(double xDouble, int disp)
            {
                // % will still remain negative.
                uint x = (uint)xDouble;
                disp %= 32;
                if (disp >= 0)
                    return ((x >> disp) | (x << (32 - disp)));
                else
                    return ((x << -disp) | (x >> (32 + disp)));
            }
            [IgnoreExtraArguments]
            static uint rshift(double xDouble, int disp)
            {
                uint x = (uint)xDouble;
                if (System.Math.Abs(disp) > 31)
                    return 0;
                else if (disp >= 0)
                    return x >> disp;
                else
                    return x << -disp;
            }
        }
    }
}
