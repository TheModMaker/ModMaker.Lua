// Copyright 2022 Jacob Trimble
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
using System.Reflection;

// Non-null fields are initialized with reflection, so the compiler doesn't know they are set.
#pragma warning disable 8618
// Fields sometimes need to match C# internals, so may violate style.
#pragma warning disable IDE1006

namespace ModMaker.Lua.Compiler {
  /// <summary>
  /// Holds cached reflection members used by the compiler.  This itself use reflection to populate
  /// its fields in the static constructor.  Each field in this file must match the associated field
  /// in the type (except for constructors).  Fields will never be null.
  /// </summary>
  static class ReflectionMembers {
    static ReflectionMembers() {
      foreach (Type this_type in typeof(ReflectionMembers).GetNestedTypes()) {
        var ref_type = this_type.GetCustomAttribute<ReflectFieldsForAttribute>()!.Type;
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        foreach (FieldInfo field in this_type.GetFields(flags)) {
          var args_attr = field.GetCustomAttribute<ArgsAttribute>();
          MemberInfo? member;
          if (args_attr != null) {
            member = field.FieldType == typeof(ConstructorInfo)
                         ? (MemberInfo?)ref_type.GetConstructor(args_attr.Args) :
                           ref_type.GetMethod(field.Name, flags, null, args_attr.Args, null);
          } else if (field.FieldType == typeof(ConstructorInfo)) {
            var ctors = ref_type.GetConstructors();
            if (ctors.Length != 1)
              throw new Exception($"Ambiguous or missing constructor in {this_type.Name}");
            member = ctors[0];
          } else {
            var members = ref_type.GetMember(field.Name, flags);
            if (members == null || members.Length == 0)
              throw new Exception($"Unknown member {field.Name} on type {this_type.Name}");
            if (members.Length > 1)
              throw new Exception($"Ambiguous member {field.Name} on type {this_type.Name}");
            member = members[0];
          }

          if (member == null)
            throw new Exception($"Unknown member {field.Name} on type {this_type.Name}");
          if (!field.FieldType.IsAssignableFrom(member.GetType()))
            throw new Exception($"Incorrect member type in {this_type.Name}.{field.Name}");
          field.SetValue(null, member);
        }
      }
    }

    /// <summary>
    /// Calling this method will ensure the fields in this file are initialized.  Because this uses
    /// nested types, this static constructor won't be called when accessing the nested classes.  To
    /// make the nested classes simpler, this method exists to ensure the above static constructor
    /// gets called.
    /// </summary>
    public static void EnsureInitialized() { }

    /// <summary>
    /// This attribute indicates which type to pull members from.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    sealed class ReflectFieldsForAttribute : Attribute {
      public Type Type { get; set; }
      public ReflectFieldsForAttribute(Type type) {
        Type = type;
      }
    }

    /// <summary>
    /// This is used to specifies the argument types for a method or constructor to search for.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    sealed class ArgsAttribute : Attribute {
      public Type[] Args { get; set; }
      public ArgsAttribute(Type[] args) {
        Args = args;
      }
    }

    [ReflectFieldsFor(typeof(Runtime.ILuaValue))]
    public static class ILuaValue {
      public static MethodInfo get_IsTrue;
      public static MethodInfo get_ValueType;

      public static MethodInfo GetValue;
      public static MethodInfo AsDouble;
      public static MethodInfo As;
      public static MethodInfo GetIndex;
      public static MethodInfo SetIndex;
      public static MethodInfo Invoke;
      public static MethodInfo Arithmetic;
      public static MethodInfo Minus;
      public static MethodInfo Not;
      public static MethodInfo Length;
      public static MethodInfo RawLength;
      public static MethodInfo Single;
    }

    [ReflectFieldsFor(typeof(Runtime.ILuaEnvironment))]
    public static class ILuaEnvironment {
      public static MethodInfo get_Settings;
      public static MethodInfo get_GlobalsTable;
      public static MethodInfo get_Runtime;
      public static MethodInfo get_CodeCompiler;
      public static MethodInfo get_Parser;

      public static MethodInfo get_Item;  // Indexer

      public static MethodInfo RegisterDelegate;
      public static MethodInfo RegisterType;
    }

    [ReflectFieldsFor(typeof(Runtime.ILuaRuntime))]
    public static class ILuaRuntime {
      public static MethodInfo get_CurrentThread;

      public static MethodInfo GenericLoop;
      public static MethodInfo CreateThread;
      public static MethodInfo CreateClassValue;
    }

    [ReflectFieldsFor(typeof(Runtime.LuaValues.LuaMultiValue))]
    public static class LuaMultiValue {
      public static ConstructorInfo Constructor;

      public static MethodInfo get_Item;  // Indexer

      public static MethodInfo CreateMultiValueFromObj;
    }

    [ReflectFieldsFor(typeof(Runtime.LuaValues.LuaValueBase))]
    public static class LuaValueBase {
      public static MethodInfo CreateValue;
    }

    [ReflectFieldsFor(typeof(Runtime.LuaValues.LuaNil))]
    public static class LuaNil {
      public static FieldInfo Nil;
    }

    [ReflectFieldsFor(typeof(Runtime.LuaValues.LuaTable))]
    public static class LuaTable {
      public static ConstructorInfo Constructor;
    }

    [ReflectFieldsFor(typeof(Runtime.LuaValues.LuaDefinedFunction))]
    public static class LuaDefinedFunction {
      public static ConstructorInfo Constructor;
    }

    [ReflectFieldsFor(typeof(System.IDisposable))]
    public static class IDisposable {
      public static MethodInfo Dispose;
    }

    [ReflectFieldsFor(typeof(double?))]
    public static class NullableDouble {
      public static MethodInfo get_Value;
      public static MethodInfo get_HasValue;
    }

    [ReflectFieldsFor(typeof(object))]
    public static class Object_ {
      public static ConstructorInfo Constructor;
      public static new MethodInfo ReferenceEquals;
    }

    [ReflectFieldsFor(typeof(Type))]
    public static class Type_ {
      public static MethodInfo GetTypeFromHandle;
      [Args(new[] { typeof(string) })]
      public static MethodInfo GetMethod;
    }

    [ReflectFieldsFor(typeof(System.InvalidOperationException))]
    public static class InvalidOperationException {
      [Args(new[] { typeof(string) })]
      public static ConstructorInfo StringConstructor;
    }

    [ReflectFieldsFor(typeof(System.NotSupportedException))]
    public static class NotSupportedException {
      [Args(new[] { typeof(string) })]
      public static ConstructorInfo StringConstructor;
    }

    [ReflectFieldsFor(typeof(System.Linq.Enumerable))]
    public static class Enumerable {
      public static MethodInfo Skip;
      public static MethodInfo ToArray;
    }

    [ReflectFieldsFor(typeof(System.Collections.IEnumerator))]
    public static class IEnumerator {
      public static MethodInfo get_Current;
      public static MethodInfo MoveNext;
    }

    [ReflectFieldsFor(typeof(IEnumerable<Runtime.LuaValues.LuaMultiValue>))]
    public static class IEnumerableLuaMultiValue {
      public static MethodInfo GetEnumerator;
    }

    [ReflectFieldsFor(typeof(IEnumerator<Runtime.LuaValues.LuaMultiValue>))]
    public static class IEnumeratorLuaMultiValue {
      public static MethodInfo get_Current;
    }
  }
}
