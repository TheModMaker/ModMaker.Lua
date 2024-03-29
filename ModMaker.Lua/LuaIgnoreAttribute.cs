// Copyright 2012 Jacob Trimble
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
using System.Linq;
using System.Reflection;

#nullable enable

namespace ModMaker.Lua {
  /// <summary>
  /// Attach to an type definition or member to control visibility to Lua code. If this is placed on
  /// a member, then the member is not visible to Lua.  If placed on a type, then this will alter
  /// which members are visible or not.
  /// </summary>
  /// <remarks>
  /// If there is ambiguity, then it favors a member not being visible. Attaching LuaIgnore to a
  /// member in an interface only has effect when the backing type has BehavesAs set to the
  /// interface type or if the value is a LuaUserData with the interface type. When determining
  /// method overloads, only members that are visible are considered.
  ///
  /// If LuaIgnore is marked on a return value or a value passed by reference (i.e. 'ref' or 'out')
  /// then it is a special return.  A special wrapper value is passed to Lua of type LuaUserData.
  /// The type is converted back when passed back to managed code, but behaves a little differently
  /// when in Lua code.  When determining if a member is visible to this variable, the extra
  /// information is added according to the LuaIgnore for how Lua got the variable.  The member
  /// visibility rules are the same for special returns, except they only apply to a single
  /// variable.  Lua cannot change a LuaUserData variable; however it is important to note that if
  /// your code accepts a variable that it may turn it into a normal variable.
  ///
  /// If you want to ensure that your code does not violate rules for LuaUserData, you can accept a
  /// LuaUserData object and manage it directly, then the object is not converted to it's underlying
  /// value.  If you want static type safety, then you can accept the generic version so only a
  /// variable of the given type will be accepted.  This also will ensure that it will select the
  /// correct overload.  Note that the type of the argument must either derive from the generic
  /// argument or implement it, overloads for user-defined casts are not supported for generics.
  /// For example, LuaUserData&lt;string&gt; is compatible with LuaUserData&lt;object&gt; but not
  /// LuaUserData&lt;int&gt;.  Also note that if the argument has a BehavesAs, then that type is the
  /// only type considered, even if the underlying type is compatible.
  ///
  /// Most variables are converted to LuaUserData and all variables are compatible with LuaUserData
  /// types.  This is to ensure type-safety for return values when the return type does not match
  /// the backing type.
  ///
  /// When determining if a member is visible to Lua, the following is done, in order.  If the
  /// object is a special return (see above) then that is searched. If AccessMembers is not null,
  /// then any member accessible to Lua must be in the array.  If IgnoreMembers is not null, then
  /// any member accessible to Lua must not be in the array.  If BehavesAs is not null, then it is
  /// searched.  If that type does not define LuaIgnore attribute, then that type must define the
  /// member and that version is used.  If it does defined LuaIgnore, then this method is applied to
  /// that type.  If DefinedOnly is true, than this type must explicitly define that member for it
  /// to be visible (i.e. inherited members are not visible).
  /// </remarks>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Constructor |
                  AttributeTargets.Interface | AttributeTargets.Event |
                  AttributeTargets.ReturnValue | AttributeTargets.Field | AttributeTargets.Method |
                  AttributeTargets.Property | AttributeTargets.Struct,
                  AllowMultiple = false, Inherited = true)]
  public sealed class LuaIgnoreAttribute : Attribute {
    /// <summary>
    /// Gets or sets the members that Lua has access to.  If this is not null, then a visible member
    /// MUST be in this array.
    /// </summary>
    public string[]? AccessMembers { get; set; }
    /// <summary>
    /// Gets or sets the members that Lua does not have access to.
    /// </summary>
    public string[] IgnoreMembers { get; set; }
    /// <summary>
    /// If true, then Lua can only access members defined by the given type, otherwise it has access
    /// to inherited members.
    /// </summary>
    public bool DefinedOnly { get; set; }
    /// <summary>
    /// Gets or sets the type that this class should behave as if it is. If it is null, then it is
    /// ignored.  The attached type must derive from or implement this type.
    /// </summary>
    public Type? BehavesAs { get; set; }

    /// <summary>
    /// Creates a new instance of LuaIgnoreAttribute where all members are not visible to Lua.
    /// </summary>
    public LuaIgnoreAttribute()
        : this(true) { }
    /// <summary>
    /// Creates a new instance of LuaIgnoreAttribute where the type or return value behaves as-if it
    /// is defined as the given type. Visibility is determined by the type.
    /// </summary>
    /// <param name="behavesAs">The type that the variable should behave as, cannot be null.</param>
    /// <exception cref="System.ArgumentNullException">If behavesAs is null.</exception>
    public LuaIgnoreAttribute(Type behavesAs) {
      AccessMembers = null;
      IgnoreMembers = Array.Empty<string>();
      DefinedOnly = false;
      BehavesAs = behavesAs;
    }
    /// <summary>
    /// Creates a new instance of LuaIgnoreAttribute where either all members are visible or
    /// invisible.
    /// </summary>
    /// <param name="allInvisible">
    /// True if all members are not visible to Lua; or false if all members are visible.
    /// </param>
    public LuaIgnoreAttribute(bool allInvisible) {
      AccessMembers = allInvisible ? new string[0] : null;
      IgnoreMembers = Array.Empty<string>();
      DefinedOnly = false;
      BehavesAs = null;
    }

    /// <summary>
    /// Checks whether a given member is visible to Lua code.
    /// </summary>
    /// <param name="backing">The backing type to check.</param>
    /// <param name="member">The name of the member.</param>
    /// <returns>True if the member is visible, otherwise false.</returns>
    public bool IsMemberVisible(Type backing, string member) {
      return IsMemberVisible(backing, member, AccessMembers, IgnoreMembers, DefinedOnly, BehavesAs);
    }

    /// <summary>
    /// Checks whether a given member is visible to Lua code.
    /// </summary>
    /// <param name="backing">The backing type to check.</param>
    /// <param name="member">The name of the member.</param>
    /// <param name="access">
    /// The members that Lua has access to.  If this is not null, then a visible member MUST be in
    /// this array.
    /// </param>
    /// <param name="ignore">The members that Lua does not have access to.  Can be null.</param>
    /// <param name="definedOnly">
    /// If true, then Lua can only access members defined by the given type, otherwise it has access
    /// to inherited members.
    /// </param>
    /// <param name="behavesAs">
    /// The type that this class should behave as if it is. If it is null, then it is ignored.  The
    /// attached type must derive from or implement this type.
    /// </param>
    /// <returns>True if the member is visible, otherwise false.</returns>
    public static bool IsMemberVisible(Type backing, string member, string[]? access,
                                       string[] ignore, bool definedOnly, Type? behavesAs) {

      if (access != null) {
        if (!access.Contains(member)) {
          return false;
        }
      }
      if (ignore.Contains(member)) {
        return false;
      }

      bool visibleMember(MemberInfo m) =>
          m.Name == member &&
          m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0;
      if (behavesAs != null) {
        if (!behavesAs.IsAssignableFrom(backing)) {
          throw new InvalidOperationException(Resources.BehavesAsMustDeriveType);
        }

        var temp = behavesAs.GetCustomAttribute<LuaIgnoreAttribute>(true);
        if (temp != null) {
          if (!temp.IsMemberVisible(backing, member)) {
            return false;
          }
        } else {
          if (!behavesAs.GetMembers().Any(visibleMember)) {
            return false;
          }
        }
      } else if (definedOnly) {
        // Must only use DeclaredOnly members
        var flags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance;
        if (!backing.GetMembers(flags).Any(visibleMember)) {
          return false;
        }
      } else {
        if (!backing.GetMembers().Any(visibleMember)) {
          return false;
        }
      }

      return true;
    }
  }
}
