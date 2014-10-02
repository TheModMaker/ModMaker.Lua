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
            public static ILuaTable Initialize(ILuaEnvironment E)
            {
                ILuaTable bit32 = new LuaTableNet();
                bit32.SetItemRaw("arshift", new arshift(E));
                bit32.SetItemRaw("band", new band(E));
                bit32.SetItemRaw("bnot", new bnot(E));
                bit32.SetItemRaw("bor", new bor(E));
                bit32.SetItemRaw("btest", new btest(E));
                bit32.SetItemRaw("bxor", new bxor(E));
                bit32.SetItemRaw("extract", new extract(E));
                bit32.SetItemRaw("replace", new replace(E));
                bit32.SetItemRaw("lrotate", new lrotate(E));
                bit32.SetItemRaw("lshift", new lshift(E));
                bit32.SetItemRaw("rrotate", new rrotate(E));
                bit32.SetItemRaw("rshift", new rshift(E));

                return bit32;
            }

            abstract class BitBase : LuaFrameworkMethod
            {
                bool atLeast;
                protected BitBase(ILuaEnvironment E, string name, bool atleast)
                    : base(E, "bit32" + name)
                {
                    this.atLeast = atleast;
                }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 2)
                        throw new ArgumentException("Expecting " + (atLeast ? "at least " : "") + "two arguments to function '" + Name + "'.");

                    object obj = args[0];
                    if (!(obj is double))
                        throw new ArgumentException("First arguments to '" + Name + "' must be a number.");
                    int r = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                    obj = args[1];
                    if (!(obj is double))
                        throw new ArgumentException("Second arguments to '" + Name + "' must be a number.");
                    int i = (int)((double)obj % System.Math.Pow(2, 32));

                    return Invoke(r, i, args);
                }

                protected abstract MultipleReturn Invoke(int a, int b, object[] args);
            }

            sealed class arshift : BitBase
            {
                public arshift(ILuaEnvironment E) : base(E, "arshift", false) { }

                protected override MultipleReturn Invoke(int r, int i, object[] args)
                {
                    if (i < 0 || (r & (1 << 31)) == 0)
                    {
                        i *= -1;

                        if (System.Math.Abs(i) > 31)
                            return new MultipleReturn(0.0);
                        else if (i >= 0)
                            return new MultipleReturn((double)(uint)(r << i));
                        else
                            return new MultipleReturn((double)(uint)(r >> -i));
                    }
                    else
                    {
                        if (i >= 31)
                            r = -1;
                        else
                            r = ((r >> i) | ~(-1 >> i));

                        return new MultipleReturn((double)(uint)r);
                    }
                }
            }
            sealed class band : LuaFrameworkMethod
            {
                public band(ILuaEnvironment E) : base(E, "bit32.band") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'bit32.band'.");

                    object obj = args[0];
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.band' must be numbers.");

                    uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                    for (int i = 1; i < args.Length; i++)
                    {
                        obj = args[i];
                        if (!(obj is double))
                            throw new ArgumentException("Arguments to 'bit32.band' must be numbers.");

                        ret &= (uint)((double)obj % System.Math.Pow(2, 32));
                    }
                    return new MultipleReturn((double)ret);
                }
            }
            sealed class bnot : LuaFrameworkMethod
            {
                public bnot(ILuaEnvironment E) : base(E, "bit32.bnot") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting one argument to function 'bit32.bnot'.");

                    object obj = args[0];

                    if (obj is double)
                    {
                        uint x = (uint)((double)obj % System.Math.Pow(2, 32));

                        return new MultipleReturn((double)(-1u - x));
                    }
                    else
                        throw new ArgumentException("First argument to function 'bit32.bnot' must be a number.");
                }
            }
            sealed class bor : LuaFrameworkMethod
            {
                public bor(ILuaEnvironment E) : base(E, "bit32.bor") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'bit32.bor'.");

                    object obj = args[0];
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.bor' must be numbers.");

                    uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                    for (int i = 1; i < args.Length; i++)
                    {
                        obj = args[i];
                        if (!(obj is double))
                            throw new ArgumentException("Arguments to 'bit32.bor' must be numbers.");

                        ret |= (uint)((double)obj % System.Math.Pow(2, 32));
                    }
                    return new MultipleReturn((double)ret);
                }
            }
            sealed class btest : LuaFrameworkMethod
            {
                public btest(ILuaEnvironment E) : base(E, "bit32.btest") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'bit32.btest'.");

                    object obj = args[0];
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.btest' must be numbers.");

                    uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                    for (int i = 1; i < args.Length; i++)
                    {
                        obj = args[i];
                        if (!(obj is double))
                            throw new ArgumentException("Arguments to 'bit32.btest' must be numbers.");

                        ret &= (uint)((double)obj % System.Math.Pow(2, 32));
                    }
                    return new MultipleReturn(ret != 0);
                }
            }
            sealed class bxor : LuaFrameworkMethod
            {
                public bxor(ILuaEnvironment E) : base(E, "bit32.bxor") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args.Length < 1)
                        throw new ArgumentException("Expecting at least one argument to function 'bit32.bxor'.");

                    object obj = args[0];
                    if (!(obj is double))
                        throw new ArgumentException("Arguments to 'bit32.bxor' must be numbers.");

                    uint ret = (uint)((double)obj % System.Math.Pow(2, 32));

                    for (int i = 1; i < args.Length; i++)
                    {
                        obj = args[i];
                        if (!(obj is double))
                            throw new ArgumentException("Arguments to 'bit32.bxor' must be numbers.");

                        ret ^= (uint)((double)obj % System.Math.Pow(2, 32));
                    }
                    return new MultipleReturn((double)ret);
                }
            }
            sealed class extract : BitBase
            {
                public extract(ILuaEnvironment E) : base(E, "extract", true) { }

                protected override MultipleReturn Invoke(int n, int field, object[] args)
                {
                    object obj = args.Length > 2 ? args[2] : null;
                    int width = 1;
                    if (obj is double)
                        width = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                    if (field > 31 || width + field > 31)
                        throw new ArgumentException("Attempt to access bits outside the allowed range.");
                    if (width < 1)
                        throw new ArgumentException("Cannot specify a zero width.");

                    int m = (~((-1 << 1) << ((width - 1))));
                    return new MultipleReturn((double)((n >> field) & m));
                }
            }
            sealed class replace : LuaFrameworkMethod
            {
                public replace(ILuaEnvironment E) : base(E, "bit32.replace") { }

                protected override MultipleReturn InvokeInternal(object[] args)
                {
                    if (args == null || args.Length < 3)
                        throw new ArgumentException("Expecting at least three arguments to function 'bit32.replace'.");

                    object obj = args[0];
                    if (!(obj is double))
                        throw new ArgumentException("First argument to 'bit32.replace' must be a number.");
                    int r = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                    obj = args[1];
                    if (!(obj is double))
                        throw new ArgumentException("Second argument to 'bit32.replace' must be a number.");
                    int v = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                    obj = args[2];
                    if (!(obj is double))
                        throw new ArgumentException("Third argument to 'bit32.replace' must be a number.");
                    int field = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                    obj = args.Length > 3 ? args[3] : null;
                    int width = 1;
                    if (obj is double)
                        width = (int)(uint)((double)obj % System.Math.Pow(2, 32));

                    if (field > 31 || field < 0 || width < 1 || width + field > 31)
                        throw new ArgumentException("Attempt to access bits outside the allowed range.");

                    int m = (~((-1 << 1) << ((width - 1))));
                    v &= m;

                    return new MultipleReturn((double)(uint)((r & ~(m << field)) | (v << field)));
                }
            }
            sealed class lrotate : BitBase
            {
                public lrotate(ILuaEnvironment E) : base(E, "lrotate", false) { }

                protected override MultipleReturn Invoke(int x, int disp, object[] args)
                {
                    if (disp >= 0)
                        return new MultipleReturn((double)((uint)(x << disp) | (uint)(x >> (32 - disp))));
                    else
                        return new MultipleReturn((double)((uint)(x >> -disp) | (uint)(x << (32 + disp))));
                }
            }
            sealed class lshift : BitBase
            {
                public lshift(ILuaEnvironment E) : base(E, "lshift", false) { }

                protected override MultipleReturn Invoke(int x, int disp, object[] args)
                {
                    if (System.Math.Abs(disp) > 31)
                        return new MultipleReturn(0.0);
                    else if (disp >= 0)
                        return new MultipleReturn((double)(uint)(x << disp));
                    else
                        return new MultipleReturn((double)(uint)(x >> -disp));
                }
            }
            sealed class rrotate : BitBase
            {
                public rrotate(ILuaEnvironment E) : base(E, "rrotate", false) { }

                protected override MultipleReturn Invoke(int x, int disp, object[] args)
                {
                    if (disp < 0)
                        return new MultipleReturn((double)((uint)(x << -disp) | (uint)(x >> (32 + disp))));
                    else
                        return new MultipleReturn((double)((uint)(x >> disp) | (uint)(x << (32 - disp))));
                }
            }
            sealed class rshift : BitBase
            {
                public rshift(ILuaEnvironment E) : base(E, "rshift", false) { }

                protected override MultipleReturn Invoke(int x, int disp, object[] args)
                {
                    if (System.Math.Abs(disp) > 31)
                        return new MultipleReturn(0.0);
                    else if (disp >= 0)
                        return new MultipleReturn((double)(uint)(x >> disp));
                    else
                        return new MultipleReturn((double)(uint)(x << -disp));
                }
            }
        }
    }
}
