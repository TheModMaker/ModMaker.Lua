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
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace ModMaker.Lua {
  /// <summary>
  /// A static class that contains several helper methods.
  /// </summary>
  static class NetHelpers {
    /// <summary>
    /// Converts the given enumerable over bytes to a base-16 string of the object, (e.g.
    /// "1463E5FF").
    /// </summary>
    /// <param name="item">The enumerable to get data from.</param>
    /// <returns>The enumerable as a string.</returns>
    public static string ToStringBase16(this IEnumerable<byte> item) {
      StringBuilder ret = new StringBuilder();
      foreach (var i in item) {
        ret.Append(i.ToString("X2", CultureInfo.InvariantCulture));
      }

      return ret.ToString();
    }
    /// <summary>
    /// Creates an array of the given type and stores it in a returned local.
    /// </summary>
    /// <param name="gen">The generator to inject the code into.</param>
    /// <param name="type">The type of the array.</param>
    /// <param name="size">The size of the array.</param>
    /// <returns>A local builder that now contains the array.</returns>
    public static LocalBuilder CreateArray(this ILGenerator gen, Type type, int size) {
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
    public static double ReadNumber(TextReader input) {
      // TODO: Check whether this is used in the parser (move to ModMaker.Lua) and make this
      // consistent with the spec.
      StringBuilder build = new StringBuilder();

      int c = input.Peek();
      int l = c;
      bool hex = false;
      CultureInfo ci = CultureInfo.CurrentCulture;
      if (c == '0') {
        input.Read();
        c = input.Peek();
        if (c == 'x' || c == 'X') {
          input.Read();
          hex = true;
        }
      }

      var sep = ci.NumberFormat.NumberDecimalSeparator[0];
      while (c != -1 && (char.IsNumber((char)c) ||
                         (hex && ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))) ||
                         (!hex && (c == sep || c == '-' || (l != '.' && c == 'e'))))) {
        input.Read();
        build.Append((char)c);
        l = c;
        c = input.Peek();
      }

      return double.Parse(build.ToString(), ci);
    }

    /// <summary>
    /// Creates a new Method definition in the given Type that has the same definition as the given
    /// method.
    /// </summary>
    /// <param name="name">The name of the new method.</param>
    /// <param name="tb">The type to define the method.</param>
    /// <param name="otherMethod">The other method definition.</param>
    /// <returns>A new method clone.</returns>
    public static MethodBuilder CloneMethod(TypeBuilder tb, string name, MethodInfo otherMethod) {
      var attr = otherMethod.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.NewSlot);
      var param = otherMethod.GetParameters();
      Type[] paramType = new Type[param.Length];
      Type[][] optional = new Type[param.Length][];
      Type[][] required = new Type[param.Length][];
      for (int i = 0; i < param.Length; i++) {
        paramType[i] = param[i].ParameterType;
        optional[i] = param[i].GetOptionalCustomModifiers();
        required[i] = param[i].GetRequiredCustomModifiers();
      }

      return tb.DefineMethod(
          name, attr, otherMethod.CallingConvention, otherMethod.ReturnType,
          otherMethod.ReturnParameter.GetRequiredCustomModifiers(),
          otherMethod.ReturnParameter.GetOptionalCustomModifiers(), paramType, required, optional);
    }
  }
}
