using ModMaker.Lua.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace ModMaker.Lua
{
    /// <summary>
    /// A static class that contains several helper methods.
    /// </summary>
    static class NetHelpers
    {
        /// <summary>
        /// The global module builder object.
        /// </summary>
        static ModuleBuilder _mb = null;
        /// <summary>
        /// A type id number for no-conflict naming.
        /// </summary>
        static int _tid = 1;

        /// <summary>
        /// Converts the given enumerable over bytes to a base-16 string of
        /// the object, (e.g. "1463E5FF").
        /// </summary>
        /// <param name="item">The enumerable to get data from.</param>
        /// <returns>The enumerable as a string.</returns>
        public static string ToStringBase16(this IEnumerable<byte> item)
        {
            StringBuilder ret = new StringBuilder();
            foreach (var i in item)
                ret.Append(i.ToString("X2", CultureInfo.InvariantCulture));

            return ret.ToString();
        }
        /// <summary>
        /// Creates an array of the given type and stores it in a returned local.
        /// </summary>
        /// <param name="gen">The generator to inject the code into.</param>
        /// <param name="type">The type of the array.</param>
        /// <param name="size">The size of the array.</param>
        /// <returns>A local builder that now contains the array.</returns>
        public static LocalBuilder CreateArray(this ILGenerator gen, Type type, int size)
        {
            var ret = gen.DeclareLocal(type.MakeArrayType());
            gen.Emit(OpCodes.Ldc_I4, size);
            gen.Emit(OpCodes.Newarr, type);
            gen.Emit(OpCodes.Stloc, ret);
            return ret;
        }
        /// <summary>
        /// Reads a number from a text reader.
        /// </summary>
        /// <param name="input">The input to read from.</param>
        /// <returns>The number read.</returns>
        public static double ReadNumber(TextReader input)
        {
            // TODO: Check whether this is used in the parser (move to ModMaker.Lua) and make this
            //  consitent with the spec.
            StringBuilder build = new StringBuilder();

            int c = input.Peek();
            int l = c;
            bool hex = false;
            CultureInfo ci = CultureInfo.CurrentCulture;
            if (c == '0')
            {
                input.Read();
                c = input.Peek();
                if (c == 'x' || c == 'X')
                {
                    input.Read();
                    hex = true;
                }
            }

            while (c != -1 && (char.IsNumber((char)c) || (hex && ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))) ||
                (!hex && (c == ci.NumberFormat.NumberDecimalSeparator[0] || c == '-' || (l != '.' && c == 'e')))))
            {
                input.Read();
                build.Append((char)c);
                l = c;
                c = input.Peek();
            }

            return double.Parse(build.ToString(), ci);
        }
        /// <summary>
        /// Gets the global module builder object for types that are generated
        /// by the framework and never saved to disk.
        /// </summary>
        /// <returns>A ModuleBuilder object to generate code with.</returns>
        public static ModuleBuilder GetModuleBuilder()
        {
            if (_mb == null)
            {
                var ab = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("DynamicAssembly"), AssemblyBuilderAccess.Run);
                _mb = ab.DefineDynamicModule("DynamicAssembly.dll");
            }
            return _mb;
        }
        /// <summary>
        /// Defines a new type in the glboal module builder with the given prefix.
        /// </summary>
        /// <param name="prefix">The name prefix of the type.</param>
        /// <returns>The newly created type.</returns>
        public static TypeBuilder DefineGlobalType(string prefix)
        {
            return _mb.DefineType(prefix + "_" + (_tid++));
        }

        /// <summary>
        /// Creates a new Method definition in the given Type that has the same
        /// definition as the given method.
        /// </summary>
        /// <param name="name">The name of the new method.</param>
        /// <param name="tb">The type to define the method.</param>
        /// <param name="otherMethod">The other method definition.</param>
        /// <returns>A new method clone.</returns>
        public static MethodBuilder CloneMethod(TypeBuilder tb, string name, MethodInfo otherMethod)
        {
            var attr = otherMethod.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.NewSlot);
            var param = otherMethod.GetParameters();
            Type[] paramType = new Type[param.Length];
            Type[][] optional = new Type[param.Length][];
            Type[][] required = new Type[param.Length][];
            for (int i = 0; i < param.Length; i++)
            {
                paramType[i] = param[i].ParameterType;
                optional[i] = param[i].GetOptionalCustomModifiers();
                required[i] = param[i].GetRequiredCustomModifiers();
            }

            return tb.DefineMethod(name, attr, otherMethod.CallingConvention,
                otherMethod.ReturnType, otherMethod.ReturnParameter.GetRequiredCustomModifiers(),
                otherMethod.ReturnParameter.GetOptionalCustomModifiers(), paramType, required, optional);
        }
    }
}