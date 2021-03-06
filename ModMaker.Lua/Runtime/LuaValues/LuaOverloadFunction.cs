// Copyright 2016 Jacob Trimble
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines a method that contains several user-defined methods and choses the method to invoke
  /// based on runtime types of the arguments and does any needed conversion so it will work.
  /// </summary>
  public class LuaOverloadFunction : LuaFunction {
    readonly List<Tuple<MethodInfo, object>> _methods;
    readonly List<OverloadSelector.Choice> _choices;
    Tuple<MethodInfo, object> _defaultMethod = null;
    // TODO: Add a constructor that accepts Delegate[].

    /// <summary>
    /// Creates a new LuaOverloadMethod with the given choices.
    /// </summary>
    /// <param name="E">The current environment.</param>
    /// <param name="name">The name of the method, used for errors.</param>
    /// <param name="methods">The method choices, cannot be null.</param>
    /// <param name="targets">The targets for the methods, cannot be null.</param>
    /// <exception cref="System.ArgumentNullException">If methods or targets is null.</exception>
    /// <exception cref="System.ArgumentException">If the length of methods
    /// is not equal to that of targets.</exception>
    public LuaOverloadFunction(string name, IEnumerable<MethodInfo> methods,
                               IEnumerable<object> targets) : base(name) {
      _methods = methods.Zip(targets, Tuple.Create).ToList();
      for (int i = 0; i < _methods.Count; i++) {
        var param = _methods[i].Item1.GetParameters();
        if (param.Length == 1 && param[0].ParameterType == typeof(LuaMultiValue)) {
          _defaultMethod = _methods[i];
          _methods.RemoveAt(i);
          break;
        }
      }

      _choices = _methods.Select(p => new OverloadSelector.Choice(p.Item1)).ToList();
    }

    /// <summary>
    /// Adds the given overload to the possible set of functions to call.
    /// </summary>
    /// <param name="d">The delegate to add.</param>
    public void AddOverload(Delegate d) {
      _methods.Add(new Tuple<MethodInfo, object>(d.Method, d.Target));
      _choices.Add(new OverloadSelector.Choice(d.Method));
    }

    /// <summary>
    /// Creates a new LuaOverloadFunction that uses the given overload only.
    /// </summary>
    /// <param name="index">The index of the overload to use.</param>
    /// <returns>A new overload function.</returns>
    public LuaOverloadFunction GetOverload(int index) {
      if (index < 0 || index >= _methods.Count)
        throw new ArgumentException("Overload index outside range");

      return new LuaOverloadFunction(Name, new[] { _methods[index].Item1 },
                                     new[] { _methods[index].Item2 });
    }

    public override LuaMultiValue Invoke(ILuaValue self, bool methodCall, LuaMultiValue args) {
      int index = OverloadSelector.FindOverload(_choices.ToArray(), args);
      object[] realArgs;
      Tuple<MethodInfo, object> method;
      if (index >= 0) {
        method = _methods[index];
        realArgs = OverloadSelector.ConvertArguments(args, _choices[index]);
      } else if (_defaultMethod != null) {
        method = _defaultMethod;
        realArgs = new[] { args };
      } else {
        throw new ArgumentException(
            $"No overload of method '{Name}' could be found with specified parameters.");
      }

      // Invoke the selected method
      object retObj = Helpers.DynamicInvoke(method.Item1, method.Item2, realArgs);

      // Restore by-reference variables.
      int min = Math.Min(realArgs.Length, args.Count);
      for (int i = 0; i < min; i++) {
        args[i] = LuaValueBase.CreateValue(realArgs[i]);
      }

      if (retObj is LuaMultiValue ret) {
        return ret;
      }

      // Convert the return type and return
      Type returnType = method.Item1.ReturnType;
      if (method.Item1.IsDefined(typeof(MultipleReturnAttribute), true)) {
        if (typeof(IEnumerable).IsAssignableFrom(returnType)) {
          // TODO: Support restricted variables.
          IEnumerable tempE = (IEnumerable)retObj;
          return LuaMultiValue.CreateMultiValueFromObj(tempE.Cast<object>().ToArray());
        } else {
          throw new InvalidOperationException(
            "Methods marked with MultipleReturnAttribute must return a type compatible with " +
            "IEnumerable.");
        }
      } else {
        return LuaMultiValue.CreateMultiValueFromObj(retObj);
      }
    }
  }
}
