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
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
    /// Invokes the given method while throwing the inner exception.  This ensures that the
    /// TargetInvocationException is not thrown an instead the inner exception is thrown.
    /// </summary>
    /// <param name="method">The method to invoke.</param>
    /// <param name="target">The target of the invocation.</param>
    /// <param name="args">The arguments to pass to the method.</param>
    /// <returns>Any value returned from the method.</returns>
    public static object DynamicInvoke(MethodBase method, object target, object[] args) {
      try {
        return method.Invoke(target, args);
      } catch (TargetInvocationException e) {
        Exception inner = e.InnerException;
        if (inner == null) {
          throw;
        }

        ExceptionDispatchInfo.Capture(inner).Throw();
        throw inner;  // Shouldn't happen.
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
        var attr = targetType.GetCustomAttribute<LuaIgnoreAttribute>(true);
        MemberInfo[] members = targetType.GetMember(name)
            .Where(m => !m.IsDefined(typeof(LuaIgnoreAttribute), true) &&
                        (attr == null || attr.IsMemberVisible(targetType, m.Name)))
            .ToArray();
        // TODO: Implement accessibility.
        //if (Base == null || Base.Length == 0 ||
        //    (userData != null && !userData.IsMemberVisible(name)) ||
        //    (ignAttr != null && !ignAttr.IsMemberVisible(type, name)))
        //  throw new InvalidOperationException(
        //      "'" + name + "' is not a visible member of type '" + type + "'.");

        if (members.Length == 0 || typeof(MemberInfo).IsAssignableFrom(targetType) ||
            name == "GetType") {
          // Note that reflection types are always opaque to Lua.
          // TODO: Consider how to get settings here to make this configurable.
          if (value != null) {
            Type t = targetType;
            throw new InvalidOperationException(
                $"The property '{name}' on '{t.FullName}' doesn't exist or isn't visible.");
          }
          return LuaNil.Nil;
        }
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

      if (members[0] is FieldInfo field) {
        if (value == null) {
          return LuaValueBase.CreateValue(field.GetValue(target));
        } else {
          // Must try to convert the given type to the requested type.  This will use both implicit
          // and explicit casts for user-defined types by default, SetValue only works if the
          // backing type is the same as or derived from the FieldType.  It does not even support
          // implicit numerical conversion
          var convert =
              typeof(ILuaValue).GetMethod(nameof(ILuaValue.As)).MakeGenericMethod(field.FieldType);
          field.SetValue(target, DynamicInvoke(convert, value, null));
          return null;
        }
      } else if (members[0] is PropertyInfo property) {
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

          return LuaValueBase.CreateValue(DynamicInvoke(meth, target, null));
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

          var convert = typeof(ILuaValue).GetMethod(nameof(ILuaValue.As))
              .MakeGenericMethod(property.PropertyType);
          property.SetValue(target, DynamicInvoke(meth, value, null), null);
          return null;
        }
      } else if (members[0] is MethodInfo method) {
        if (value != null) {
          throw new InvalidOperationException("Cannot set the value of a method.");
        }
        if (method.IsSpecialName) {
          throw new InvalidOperationException($"Cannot call special method '{method.Name}'.");
        }

        return new LuaOverloadFunction(method.Name, members.Cast<MethodInfo>(),
                                       Enumerable.Repeat(target, members.Length));
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
          object valueObj = DynamicInvoke(
              typeof(ILuaValue).GetMethod(nameof(ILuaValue.As)).MakeGenericMethod(arrayType),
              value, null);

          targetArray.SetValue(valueObj, args);
          return value;
        }
      }

      // Setting also requires the last arg be the 'value'
      if (value != null) {
        indicies = indicies.AdjustResults(indicies.Count + 1);
        indicies[indicies.Count - 1] = value;
      }

      // Find the valid method
      string name = targetType == typeof(string) ? "Chars" : "Item";
      var methods = targetType.GetMethods()
          .Where(m => m.Name == (value == null ? "get_" + name : "set_" + name) &&
                      !m.IsDefined(typeof(LuaIgnoreAttribute), true))
          .ToArray();
      var choices = methods.Select(m => new OverloadSelector.Choice(m)).ToArray();
      int index = OverloadSelector.FindOverload(choices, indicies);

      if (index < 0) {
        throw new InvalidOperationException(
            "Unable to find a visible indexer that matches the provided arguments for type '" +
            target.GetType() + "'.");
      }

      object[] values = OverloadSelector.ConvertArguments(indicies, choices[index]);
      if (value == null) {
        return LuaValueBase.CreateValue(DynamicInvoke(methods[index], target, values));
      } else {
        DynamicInvoke(methods[index], target, values);
        return value;
      }
    }
  }
}
