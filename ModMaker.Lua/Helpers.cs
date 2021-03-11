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
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua {
  /// <summary>
  /// A static class that contains several helper methods.
  /// </summary>
  static class Helpers {
    static readonly Action<Exception> _preserveStackTrace;

    static Helpers() {
      MethodInfo preserveStackTrace = typeof(Exception).GetMethod(
          "InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);
      _preserveStackTrace =
          (Action<Exception>)Delegate.CreateDelegate(typeof(Action<Exception>), preserveStackTrace);
    }

    /// <summary>
    /// Helper class that calls a given method when Dispose is called.
    /// </summary>
    sealed class DisposableHelper : IDisposable {
      readonly Action _act;
      bool _disposed = false;

      public DisposableHelper(Action act) {
        _act = act;
      }

      public void Dispose() {
        if (_disposed) {
          return;
        }

        _disposed = true;
        _act();
      }
    }

    /// <summary>
    /// Creates an IDisposable object that calls the given function when Dispose is called.
    /// </summary>
    /// <param name="act">The function to call on Dispose.</param>
    /// <returns>An IDisposable object.</returns>
    public static IDisposable Disposable(Action act) {
      return new DisposableHelper(act);
    }

    /// <summary>
    /// Parses the number in the given string.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <returns>The double value that was parsed.</returns>
    public static double ParseNumber(string value) {
      value = value.ToLower();
      string pattern;
      NumberStyles style;
      int @base;
      int exponent;
      if (value.StartsWith("0x")) {
        pattern = @"^0x([0-9a-f]*)(?:\.([0-9a-f]*))?(?:p([-+])?(\d+))?$";
        style = NumberStyles.AllowHexSpecifier;
        @base = 16;
        exponent = 2;
      } else {
        pattern = @"^(\d*)(?:\.(\d*))?(?:e([-+])?(\d+))?$";
        style = NumberStyles.Integer;
        @base = 10;
        exponent = 10;
      }

      var match = Regex.Match(value, pattern);
      if (match == null || !match.Success) {
        throw new ArgumentException("Invalid number format");
      }

      double ret = match.Groups[1].Value == "" ? 0 : long.Parse(match.Groups[1].Value, style);
      if (match.Groups[2].Value != "") {
        double temp = long.Parse(match.Groups[2].Value, style);
        ret += temp * Math.Pow(@base, -match.Groups[2].Value.Length);
      }
      if (match.Groups[4].Value != "") {
        double temp = long.Parse(match.Groups[4].Value);
        int mult = match.Groups[3].Value == "-" ? -1 : 1;
        ret *= Math.Pow(exponent, mult * temp);
      }
      return ret;
    }

    /// <summary>
    /// Retrieves a custom attribute applied to a member of a type. Parameters specify the member,
    /// and the type of the custom attribute to search for.
    /// </summary>
    /// <param name="element">
    /// An object derived from the System.Reflection.MemberInfo class that describes a constructor,
    /// event, field, method, or property member of a class.
    /// </param>
    /// <returns>
    /// A reference to the single custom attribute of type attributeType that is applied to element,
    /// or null if there is no such attribute.
    /// </returns>
    public static T GetCustomAttribute<T>(this MemberInfo element) where T : Attribute {
      return (T)Attribute.GetCustomAttribute(element, typeof(T));
    }
    /// <summary>
    /// Retrieves a custom attribute applied to a member of a type. Parameters specify the member,
    /// and the type of the custom attribute to search for.
    /// </summary>
    /// <param name="element">
    /// An object derived from the System.Reflection.MemberInfo class that describes a constructor,
    /// event, field, method, or property member of a class.
    /// </param>
    /// <returns>
    /// A reference to the single custom attribute of type attributeType that is applied to element,
    /// or null if there is no such attribute.
    /// </returns>
    public static T GetCustomAttribute<T>(this MemberInfo element, bool inherit) where T : Attribute {
      return (T)Attribute.GetCustomAttribute(element, typeof(T), inherit);
    }

    /// <summary>
    /// Checks whether two types are compatible and gets a conversion method if it can be.
    /// </summary>
    /// <param name="sourceType">The type of the original object.</param>
    /// <param name="destType">The type trying to convert to.</param>
    /// <param name="method">
    /// Will contains the resulting conversion method. This method will be static
    /// </param>
    /// <returns>Whether the types are compatible.</returns>
    public static bool TypesCompatible(Type sourceType, Type destType, out MethodInfo method) {
      method = null;

      if (sourceType == destType) {
        return true;
      }

      if (destType.IsGenericType &&
          destType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
        // If the destination is nullable, simply convert as the underlying type.
        destType = destType.GetGenericArguments()[0];
      }

      // NOTE: This only checks for derived classes and interfaces, this will not work for
      // implicit/explicit casts.
      if (destType.IsAssignableFrom(sourceType)) {
        return true;
      }

      // All numeric types are explicitly compatible but do not define a cast in their type.
      if (destType != typeof(bool) && destType != typeof(IntPtr) && destType != typeof(UIntPtr) &&
          destType.IsPrimitive && sourceType != typeof(bool) && sourceType != typeof(IntPtr) &&
          sourceType != typeof(UIntPtr) && sourceType.IsPrimitive) {
        // Although they are compatible, they need to be converted, get the
        // Convert.ToXX method.
        method = typeof(Convert).GetMethod("To" + destType.Name, new Type[] { sourceType });
        return true;
      }

      // Get any methods from source type that is not marked with LuaIgnoreAttribute and has
      // the name 'op_Explicit' or 'op_Implicit' and has a return type of the destination
      // type and a sole argument that is implicitly compatible with the source type.
      var attr = sourceType.GetCustomAttribute<LuaIgnoreAttribute>(true);
      var flags = BindingFlags.Static | BindingFlags.Public;
      bool isValidMethod(MethodInfo m) {
        return m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0 &&
            (attr == null || attr.IsMemberVisible(sourceType, m.Name)) &&
            (m.Name == "op_Explicit" || m.Name == "op_Implicit") &&
            m.ReturnType == destType && m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType.IsAssignableFrom(sourceType);
      }
      // Static methods aren't inherited, but we can use static methods in parent classes.
      IEnumerable<MethodInfo> possible = Enumerable.Empty<MethodInfo>();
      for (Type cur = sourceType; cur != null; cur = cur.BaseType) {
        possible = possible.Concat(cur.GetMethods(flags).Where(isValidMethod));
      }

      // Check for a cast in the destination type.  Don't inherit since the return type needs to
      // match destType, and inherited versions would return the base type.
      attr = destType.GetCustomAttribute<LuaIgnoreAttribute>(true);
      possible = possible.Concat(destType.GetMethods(flags).Where(isValidMethod));

      foreach (var choice in possible) {
        method = choice;
        if (choice.Name == "op_implicit") {
          return true;
        }
      }
      return method != null;
    }
    /// <summary>
    /// Searches the given methods for an overload that will work with the given arguments.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the method base (e.g. MethodInfo or ConstructorInfo).
    /// </typeparam>
    /// <param name="methods">The possible method choices.</param>
    /// <param name="args">The arguments to check.</param>
    /// <param name="resultMethod">Where the resulting method will be placed.</param>
    /// <param name="resultTarget">Where the respective target will be placed.</param>
    /// <returns>True if a compatible method was found, otherwise false.</returns>
    public static bool GetCompatibleMethod<T>(
        IEnumerable<Tuple<T, object>> methods, ILuaMultiValue args, out T resultMethod,
        out object resultTarget) where T : MethodBase {
      Tuple<T, object>[] methodsArray = methods.ToArray();
      OverloadSelector.Choice[] choices =
          methodsArray.Select((t) => new OverloadSelector.Choice(t.Item1)).ToArray();
      Tuple<Type, Type>[] values = args
          .Select((v) => v == null || v == LuaNil.Nil
                             ? null : new Tuple<Type, Type>(v.GetType(), v.GetValue()?.GetType()))
          .ToArray();
      int choice = OverloadSelector.FindOverload(choices, values);
      if (choice == -1) {
        resultMethod = null;
        resultTarget = null;
        return false;
      }
      resultMethod = methodsArray[choice].Item1;
      resultTarget = methodsArray[choice].Item2;
      return true;
    }
    /// <summary>
    /// Converts the given arguments so they can be passed to the given method.  It assumes the
    /// arguments are valid.
    /// </summary>
    /// <param name="args">The arguments to convert.</param>
    /// <param name="method">The method to call.</param>
    /// <returns>The arguments as they can be passed to the given method.</returns>
    public static object[] ConvertForArgs(ILuaMultiValue args, MethodBase method) {
      var param = method.GetParameters();

      var ret = new object[param.Length];
      var min = Math.Min(param.Length, args.Count);
      var rootMethod = typeof(ILuaValue).GetMethod(nameof(ILuaValue.As));

      bool hasParams = param.Length > 0 &&
          param[^1].GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0;

      if (param.Length == 1 && param[0].ParameterType == typeof(ILuaMultiValue)) {
        return new object[] { args };
      }

      // Convert formal parameters.
      for (int i = 0; i < min; i++) {
        // Skip params array since it's handled below.
        if (i == param.Length - 1 && hasParams) {
          continue;
        }

        var paramType = param[i].ParameterType;
        if (paramType.IsByRef) {
          paramType = paramType.GetElementType();
        }

        var asMethod = rootMethod.MakeGenericMethod(paramType);
        ret[i] = asMethod.Invoke(args[i], null);
      }

      // Get optional parameters.
      for (int i = min; i < param.Length; i++) {
        ret[i] = param[i].DefaultValue;
      }

      // Get params array.
      if (hasParams) {
        Type arrayType = param[^1].ParameterType.GetElementType();
        var asMethod = rootMethod.MakeGenericMethod(arrayType);
        int start = param.Length - 1;

        Array array = Array.CreateInstance(arrayType, Math.Max(args.Count - start, 0));
        for (int i = 0; i < array.Length; i++)
          array.SetValue(asMethod.Invoke(args[start + i], null), i);
        ret[^1] = array;
      }

      return ret;
    }

    /// <summary>
    /// Invokes the given method while throwing the inner exception.  This ensures that the
    /// TargetInvocationException is not thrown an instead the inner exception is thrown.
    /// </summary>
    /// <param name="method">The method to invoke.</param>
    /// <param name="target">The target of the invocation.</param>
    /// <param name="args">The arguments to pass to the method.</param>
    /// <returns>Any value returned from the method.</returns>
    public static object DynamicInvoke(MethodBase method, object target, object[] args) {
      // See: http://stackoverflow.com/a/1663549
      try {
        return method.Invoke(target, args);
      } catch (TargetInvocationException e) {
        Exception inner = e.InnerException;
        if (inner == null) {
          throw;
        }

        _preserveStackTrace(inner);
        throw inner;
      }
    }

    /// <summary>
    /// Gets or sets the given object to the given value.  Also handles accessibility correctly.
    /// </remarks>
    /// <param name="targetType">The type of the target object.</param>
    /// <param name="target">The target object.</param>
    /// <param name="index">The indexing object.</param>
    /// <param name="value">The value to set to.</param>
    /// <returns>The value for get or null if setting.</returns>
    public static ILuaValue GetSetMember(Type targetType, object target, ILuaValue index,
                                         ILuaValue value = null) {
      // TODO: Consider how to get settings here.
      /*if (!E.Settings.AllowReflection &&
          typeof(MemberInfo).IsAssignableFrom(targetType)) {
        // TODO: Move to resources.
        throw new InvalidOperationException(
            "Lua does not have access to reflection.  See LuaSettings.AllowReflection.");
      }*/

      if (index.ValueType == LuaValueType.Number || index.ValueType == LuaValueType.Table) {
        if (target == null) {
          throw new InvalidOperationException(
              "Attempt to call indexer on a static type.");
        }

        ILuaMultiValue args;
        if (index.ValueType == LuaValueType.Number) {
          args = new LuaMultiValue(new[] { value });
        } else {
          int len = index.Length().As<int>();
          object[] objArgs = new object[len];
          for (int i = 1; i <= len; i++) {
            ILuaValue item = index.GetIndex(LuaValueBase.CreateValue(i));
            if (item.ValueType == LuaValueType.Table) {
              throw new InvalidOperationException(
                  "Arguments to indexer cannot be a table.");
            }
            objArgs[i - 1] = item;
          }

          args = LuaMultiValue.CreateMultiValueFromObj(objArgs);
        }

        return _getSetIndex(targetType, target, args, value);
      } else if (index.ValueType == LuaValueType.String) {
        string name = index.As<string>();

        // Find all visible members with the given name
        MemberInfo[] members = targetType.GetMember(name)
            .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0)
            .ToArray();
        // TODO: Implement accessibility.
        //if (Base == null || Base.Length == 0 ||
        //    (userData != null && !userData.IsMemberVisible(name)) ||
        //    (ignAttr != null && !ignAttr.IsMemberVisible(type, name)))
        //  throw new InvalidOperationException(
        //      "'" + name + "' is not a visible member of type '" + type + "'.");

        return _getSetValue(members, target, value);
      } else {
        throw new InvalidOperationException(
            "Indices of a User-Defined type must be a string, number, or table.");
      }
    }

    static ILuaValue _getSetValue(MemberInfo[] members, object target, ILuaValue value = null) {
      // Perform the action on the given member.  Although this only checks the first member, the
      // only type that can return more than one with the same name is a method and can only be
      // other methods.
      if (members.Length == 0) {
        return LuaNil.Nil;
      }

      FieldInfo field = members[0] as FieldInfo;
      PropertyInfo property = members[0] as PropertyInfo;
      MethodInfo method = members[0] as MethodInfo;
      if (field != null) {

        if (value == null) {
          return LuaValueBase.CreateValue(field.GetValue(target));
        } else {
          // Must try to convert the given type to the requested type.  This will use both implicit
          // and explicit casts for user-defined types by default, SetValue only works if the
          // backing type is the same as or derived from the FieldType.  It does not even support
          // implicit numerical conversion
          var convert =
              typeof(ILuaValue).GetMethod(nameof(ILuaValue.As)).MakeGenericMethod(field.FieldType);
          field.SetValue(target, convert.Invoke(value, null));
          return null;
        }
      } else if (property != null) {

        if (value == null) {
          MethodInfo meth = property.GetGetMethod();
          if (meth == null) {
            throw new InvalidOperationException($"The property '{property.Name}' is write-only.");
          }
          // TODO: Implement accessibility.
          /*if (meth.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0 ||
              (userData != null && !userData.IsMemberVisible("get_" + name)) ||
              (ignAttr != null && !ignAttr.IsMemberVisible(type, "get_" + name)))
            throw new InvalidOperationException(
                "The get method for property '" + name + "' is inaccessible to Lua.");*/

          return LuaValueBase.CreateValue(method.Invoke(target, null));
        } else {
          MethodInfo meth = property.GetSetMethod();
          if (meth == null) {
            throw new InvalidOperationException($"The property '{property.Name}' is read-only.");
          }
          // TODO: Implement accessibility.
          /*if (meth.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length > 0 ||
              (userData != null && !userData.IsMemberVisible("set_" + name)) ||
              (ignAttr != null && !ignAttr.IsMemberVisible(type, "set_" + name)))
            throw new InvalidOperationException(
                "The set method for property '" + name + "' is inaccessible to Lua.");*/

          var convert = typeof(ILuaValue).GetMethod("As").MakeGenericMethod(property.PropertyType);
          property.SetValue(target, convert.Invoke(value, null), null);
          return null;
        }
      } else if (method != null) {
        if (value != null) {
          throw new InvalidOperationException("Cannot set the value of a method.");
        }

        if (method.IsSpecialName) {
          throw new InvalidOperationException($"Cannot call special method '{method.Name}'.");
        }

        return new LuaOverloadFunction(method.Name, new[] { method }, new[] { target });
      } else {
        throw new InvalidOperationException("Unrecognized member type " + members[0]);
      }
    }

    /// <summary>
    /// Gets or sets the given index to the given value.
    /// </remarks>
    /// <param name="targetType">The type of the target object.</param>
    /// <param name="target">The target object, or null for static access.</param>
    /// <param name="index">The indexing object.</param>
    /// <param name="value">The value to set to.</param>
    /// <returns>The value for get or value if setting.</returns>
    static ILuaValue _getSetIndex(Type targetType, object target, ILuaMultiValue indicies,
                                  ILuaValue value = null) {
      // Arrays do not actually define an 'Item' method so we need to access the indexer directly.
      if (target is Array targetArray) {
        // Convert the arguments to long.
        int[] args = new int[indicies.Count];
        for (int i = 0; i < indicies.Count; i++) {
          if (indicies[i].ValueType == LuaValueType.Number) {
            // TODO: Move to resources.
            throw new InvalidOperationException(
                "Arguments to indexer for an array can only be numbers.");
          } else {
            args[i] = indicies[i].As<int>();
          }
        }

        if (value == null) {
          return LuaValueBase.CreateValue(targetArray.GetValue(args));
        } else {
          // Convert to the array type.
          Type arrayType = targetArray.GetType().GetElementType();
          object valueObj = typeof(ILuaValue).GetMethod(nameof(ILuaValue.As))
              .MakeGenericMethod(arrayType).Invoke(value, null);

          targetArray.SetValue(valueObj, args);
          return value;
        }
      }

      // Setting also requires the last arg be the 'value'
      if (value != null) {
        indicies = indicies.AdjustResults(indicies.Count + 1);
        indicies[^1] = value;
      }

      // find the valid method
      string name = targetType == typeof(string) ? "Chars" : "Item";
      GetCompatibleMethod(
          targetType.GetMethods()
              .Where(m => m.Name == (value == null ? "get_" + name : "set_" + name))
              .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0)
              .Select(m => Tuple.Create(m, target)),
          indicies, out MethodInfo method, out _);

      if (method == null) {
        // TODO: Move to resources.
        throw new InvalidOperationException(
            "Unable to find a visible indexer that  matches the provided arguments for type '" +
            target.GetType() + "'.");
      }

      if (value == null) {
        return LuaValueBase.CreateValue(
            method.Invoke(target, indicies.Select(v => v.GetValue()).ToArray()));
      } else {
        method.Invoke(target, indicies.Select(v => v.GetValue()).ToArray());
        return value;
      }
    }
  }
}
