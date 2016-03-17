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
                Register(E, bit32, (Func<double, double, uint>)arshift);
                Register(E, bit32, (Func<uint[], uint>)band);
                Register(E, bit32, (Func<uint, uint>)bnot);
                Register(E, bit32, (Func<uint[], uint>)bor);
                Register(E, bit32, (Func<uint[], bool>)btest);
                Register(E, bit32, (Func<uint[], uint>)bxor);
                Register(E, bit32, (Func<uint, int, int, uint>)extract);
                Register(E, bit32, (Func<uint, uint, int, int, uint>)replace);
                Register(E, bit32, (Func<uint, int, uint>)lrotate);
                Register(E, bit32, (Func<uint, int, uint>)lshift);
                Register(E, bit32, (Func<uint, int, uint>)rrotate);
                Register(E, bit32, (Func<uint, int, uint>)rshift);

                E.GlobalsTable.SetItemRaw(E.Runtime.CreateValue("bit32"), bit32);
            }

            static uint arshift(double x, double disp)
            {
                if (System.Math.Abs(disp) > 31)
                    return 0;

                return (uint)(x / System.Math.Pow(2, disp));
            }
            static uint band(params uint[] args)
            {
                return args.Aggregate((a, b) => a & b);
            }
            static uint bnot(uint x)
            {
                return ~x;
            }
            static uint bor(params uint[] args)
            {
                return args.Aggregate((a, b) => a | b);
            }
            static bool btest(params uint[] args)
            {
                return bor(args) != 0;
            }
            static uint bxor(params uint[] args)
            {
                return args.Aggregate((a, b) => a ^ b);
            }
            static uint extract(uint source, int field, int width = 1)
            {
                if (field > 31 || width + field > 31 || field < 0 || width < 0)
                    throw new ArgumentException("Attempt to access bits outside the allowed range.");

                uint mask = (uint)((1 << width) - 1);
                return ((source >> field) & mask);
            }
            static uint replace(uint source, uint repl, int field, int width = 1)
            {
                if (field > 31 || width + field > 31 || field < 0 || width < 0)
                    throw new ArgumentException("Attempt to access bits outside the allowed range.");

                uint mask = (uint)((1 << width) - 1);
                repl &= mask;
                source &= ~(mask << field);
                return (source | (repl << field));
            }
            static uint lrotate(uint x, int disp)
            {
                // % will still remain negative.
                disp %= 32;
                if (disp >= 0)
                    return ((x << disp) | (x >> (32 - disp)));
                else
                    return ((x >> -disp) | (x << (32 + disp)));
            }
            static uint lshift(uint x, int disp)
            {
                if (disp > 31)
                    return 0;
                else if (disp >= 0)
                    return x << disp;
                else
                    return x >> -disp;
            }
            static uint rrotate(uint x, int disp)
            {
                // % will still remain negative.
                disp %= 32;
                if (disp >= 0)
                    return ((x >> disp) | (x << (32 - disp)));
                else
                    return ((x << -disp) | (x >> (32 + disp)));
            }
            static uint rshift(uint x, int disp)
            {
                if (disp > 31)
                    return 0;
                else if (disp >= 0)
                    return x >> disp;
                else
                    return x << -disp;
            }
        }
    }
}
