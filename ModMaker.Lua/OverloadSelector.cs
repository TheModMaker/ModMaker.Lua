// Copyright 2021 Jacob Trimble
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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

#nullable enable

namespace ModMaker.Lua {
  /// <summary>
  /// A helper to select which overload to pick when calling an overloaded function.
  /// </summary>
  static class OverloadSelector {
    /// <summary>
    /// Defines the result of Compare.
    /// </summary>
    public enum CompareResult {
      /// <summary>
      /// Neither of the Choices are valid.
      /// </summary>
      Neither,
      /// <summary>
      /// Both Choices are equally good.
      /// </summary>
      Both,
      /// <summary>
      /// The "a" Choice is better.
      /// </summary>
      A,
      /// <summary>
      /// The "b" Choice is better.
      /// </summary>
      B,
    }

    public sealed class Choice {
      public Choice(MethodBase info) {
        ParameterInfo[] param = info.GetParameters();
        static bool nullableStruct(Type t) {
          return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
        static Type underlyingType(Type t) {
          if (nullableStruct(t))
            return t.GetGenericArguments()[0];
          else if (t.IsByRef)
            return t.GetElementType()!;
          else
            return t;
        }
        static bool nullableRef(Type t) {
          return !underlyingType(t).IsValueType;
        }
        static bool isNullableParam(ParameterInfo p) {
          // TODO: Parse nullable reference type metadata:
          // https://github.com/dotnet/roslyn/blob/main/docs/features/nullable-metadata.md
          if (p.IsDefined(typeof(NotNullAttribute)))
            return false;
          return nullableStruct(p.ParameterType) || nullableRef(p.ParameterType);
        }

        FormalArguments = param.Select((p) => underlyingType(p.ParameterType)).ToArray();
        Nullable = param.Select(isNullableParam).ToArray();
        OptionalCount = param.Count((p) => p.IsOptional);
        HasParams = param.Length > 0 && param[^1].IsDefined(typeof(ParamArrayAttribute));
        Type? paramType = !HasParams ? null : param[^1].ParameterType.GetElementType();
        ParamsNullable = HasParams && (nullableStruct(paramType!) || nullableRef(paramType!));
      }

      public Choice(Type[] args, bool[]? nullable = null, int optCount = 0,
                    bool hasParams = false, bool paramsNullable = false) {
        FormalArguments = args;
        Nullable = nullable ?? new bool[args.Length];
        OptionalCount = optCount;
        HasParams = hasParams;
        ParamsNullable = paramsNullable;
      }

      /// <summary>
      /// The types of each of the formal arguments.  If the argument is nullable, it should be the
      /// element type and Nullable should be "true" for this index.
      /// </summary>
      public readonly Type[] FormalArguments;
      /// <summary>
      /// Whether each value in FormalArguments can be null.
      /// </summary>
      public readonly bool[] Nullable;
      /// <summary>
      /// The number of arguments that are optional.
      /// </summary>
      public readonly int OptionalCount;
      /// <summary>
      /// True if the last argument is a "params" array.
      /// </summary>
      public readonly bool HasParams;
      /// <summary>
      /// If this has a "params" array, this field contains whether the array can contain null
      /// values.
      /// </summary>
      public readonly bool ParamsNullable;
    }

    /// <summary>
    /// Compares the two choices and determines which is better.  This also checks that the call is
    /// valid.  The values should be the arguments passed as-is.  Note that this ignores any extra
    /// arguments, but you must pass enough.
    ///
    /// Passing a "null" value in "values" indicates the value is a "null" value.  Otherwise, the
    /// first type is the "real" type of the value.  If given, the second type is the "inner" type
    /// that is also valid for this value.  For example, LuaUserData has an inner type for the value
    /// it holds.
    /// </summary>
    /// <param name="a">The first value to compare.</param>
    /// <param name="b">The second value to compare.</param>
    /// <param name="values">The type of the given arguments.</param>
    /// <returns>Less than 0 if a is better; greater than 0 if b is better; 0 if equal.</returns>
    public static CompareResult Compare(Choice a, Choice b, Tuple<Type, Type?>?[] values) {
      static bool compatible(Tuple<Type, Type?>? value, Type argType, bool nullable) {
        if (value == null) {
          return nullable;
        } else {
          bool ret = Helpers.TypesCompatible(value.Item1, argType, out _);
          if (!ret && value.Item2 != null) {
            return Helpers.TypesCompatible(value.Item2, argType, out _);
          }
          return ret;
        }
      }

      // This tracks which calls are valid; this will never equal Neither (since we return early).
      CompareResult result = CompareResult.Both;

      // Extra arguments are ignored; check the minimum arguments are given.
      int minArgsA = a.FormalArguments.Length - (a.HasParams ? 1 : a.OptionalCount);
      if (values.Length < minArgsA)
        result = CompareResult.B;
      int minArgsB = b.FormalArguments.Length - (b.HasParams ? 1 : b.OptionalCount);
      if (values.Length < minArgsB) {
        if (result == CompareResult.Both)
          result = CompareResult.A;
        else
          return CompareResult.Neither;
      }

      // Based on the C# overload resolution:
      // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#better-function-member
      // The basic idea is that only one can be better, we can't have some arguments better in each.
      // So if a is better for the first arg and b is better for the second, then it is ambiguous.
      int minLength = Math.Min(a.FormalArguments.Length, b.FormalArguments.Length);
      // This tracks which call is better.  If this is Both, then the call is ambiguous; Neither
      // means they are equally good.
      CompareResult argCast = CompareResult.Neither;
      for (var i = 0; i < values.Length; i++) {
        if (i < a.FormalArguments.Length - (a.HasParams ? 1 : 0)) {
          if (!compatible(values[i], a.FormalArguments[i], a.Nullable[i])) {
            if (result == CompareResult.Both)
              result = CompareResult.B;
            else if (result == CompareResult.A)
              return CompareResult.Neither;
          }
        }
        if (i < b.FormalArguments.Length - (b.HasParams ? 1 : 0)) {
          if (!compatible(values[i], b.FormalArguments[i], b.Nullable[i])) {
            if (result == CompareResult.Both)
              result = CompareResult.A;
            else if (result == CompareResult.B)
              return CompareResult.Neither;
          }
        }

        if (result == CompareResult.Both && i < minLength) {
          // Check which function has a better conversion for the type arguments.  Note that if one
          // is better, it must be better or the same for all arguments.
          switch (argCast) {
            case CompareResult.A:
              if (_isBetterConversionTarget(b.FormalArguments[i], b.Nullable[i],
                                            a.FormalArguments[i], a.Nullable[i], values[i])) {
                argCast = CompareResult.Both;
              }
              break;
            case CompareResult.B:
              if (_isBetterConversionTarget(a.FormalArguments[i], a.Nullable[i],
                                            b.FormalArguments[i], b.Nullable[i], values[i])) {
                argCast = CompareResult.Both;
              }
              break;
            case CompareResult.Neither:
              if (_isBetterConversionTarget(a.FormalArguments[i], a.Nullable[i],
                                            b.FormalArguments[i], b.Nullable[i], values[i])) {
                argCast = CompareResult.A;
              }
              if (_isBetterConversionTarget(b.FormalArguments[i], b.Nullable[i],
                                            a.FormalArguments[i], a.Nullable[i], values[i])) {
                argCast = CompareResult.B;
              }
              break;
          }
        }
      }

      // Check params array for validity.
      bool isParamsValid(Choice choice) {
        Type elemType = choice.FormalArguments[^1].GetElementType()!;
        return values.Skip(choice.FormalArguments.Length - 1)
            .All((v) => compatible(v, elemType, choice.ParamsNullable));
      }
      if (a.HasParams && (result == CompareResult.Both || result == CompareResult.A)) {
        if (!isParamsValid(a)) {
          if (result == CompareResult.Both)
            result = CompareResult.B;
          else
            return CompareResult.Neither;
        }
      }
      if (b.HasParams && (result == CompareResult.Both || result == CompareResult.B)) {
        if (!isParamsValid(b)) {
          if (result == CompareResult.Both)
            result = CompareResult.A;
          else
            return CompareResult.Neither;
        }
      }

      // Only return these after checking everything else so we know the call is valid.  We don't
      // want to early return for just A until we've checked all of A.
      if (result != CompareResult.Both)
        return result;
      if (argCast != CompareResult.Neither)
        return argCast;

      // Perform the tie-breaking rules.  Checks should be executed in the following order.
      if (a.FormalArguments.Length > b.FormalArguments.Length)
        return CompareResult.A;
      if (a.FormalArguments.Length < b.FormalArguments.Length)
        return CompareResult.B;
      if (a.OptionalCount < b.OptionalCount)
        return CompareResult.A;
      if (a.OptionalCount > b.OptionalCount)
        return CompareResult.B;

      return CompareResult.Both;
    }

    /// <summary>
    /// Finds the overload that matches the given arguments.  This will throw an exception if the
    /// call is ambiguous; but this will return null if there is no possible match.
    /// </summary>
    /// <param name="choices">The possible choices to call.</param>
    /// <param name="values">The types of the arguments given.</param>
    /// <returns>The index of the chosen overload, or -1 if none match.</returns>
    public static int FindOverload(Choice[] choices, Tuple<Type, Type?>?[] values) {
      int ret = -1;
      bool ambiguous = false;
      bool @checked = false;
      for (var index = 0; index < choices.Length; index++) {
        if (ret == -1) {
          ret = index;
          ambiguous = false;
          @checked = false;
        } else {
          @checked = true;
          var result = Compare(choices[ret], choices[index], values);
          switch (result) {
            case CompareResult.Neither:
              ret = -1;
              break;
            case CompareResult.Both:
              ambiguous = true;
              break;
            case CompareResult.A:
              break;
            case CompareResult.B:
              ret = index;
              ambiguous = false;
              break;
          }
        }
      }
      if (ambiguous)
        throw new AmbiguousMatchException();
      if (ret != -1 && !@checked &&
          Compare(choices[ret], choices[ret], values) != CompareResult.Both) {
        return -1;
      }
      return ret;
    }

    static bool _isBetterConversionTarget(Type a, bool aNullable, Type b, bool bNullable,
                                          Tuple<Type, Type?>? value) {
      // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#better-conversion-target
      static bool nullCast(bool a, bool b) {
        return b || !a;
      }
      // If the type matches the value, favor that.  This mainly affects numbers since they are all
      // considered the same.
      bool aMatches = value != null && (value.Item1 == a || value.Item2 == a);
      bool bMatches = value != null && (value.Item1 == b || value.Item2 == b);
      if (aMatches && !bMatches)
        return true;
      if (Helpers.TypesCompatible(b, a, out _) && nullCast(bNullable, aNullable))
        return false;

      // Note this also checks for number casts.  We don't care about Delegate/Task conversions.
      return Helpers.TypesCompatible(a, b, out _) && nullCast(aNullable, bNullable);
    }
  }
}
