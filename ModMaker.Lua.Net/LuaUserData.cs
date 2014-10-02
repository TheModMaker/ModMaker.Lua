using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua.Runtime;
using System.Reflection;
using System.Globalization;

namespace ModMaker.Lua
{
    /// <summary>
    /// Defines a special value that is passed to Lua.  This is a wrapper
    /// arround a value that modifies what members are visible to Lua code.
    /// This can make a single variable behave as if it a given type and can
    /// make some members invisible.
    /// </summary>
    /// <remarks>
    /// This type is derived by the generic version that allows for static tpe
    /// safety.  Converting from the non-generic version to the generic version 
    /// is possible at runtime using ConvertTo.
    /// </remarks>
    public class LuaUserData
    {
        /// <summary>
        /// Gets the backing object value.
        /// </summary>
        public object Backing { get; private set; }
        /// <summary>
        /// Gets the type that this object is behaving as.  Can be null if it 
        /// is simply the backing type.
        /// </summary>
        public Type BehavesAs { get; private set; }
        /// <summary>
        /// Gets the members that Lua has access to.  If this is not
        /// null, then a visible member MUST be in this array.
        /// </summary>
        public string[] AccessMembers { get; private set; }
        /// <summary>
        /// Gets the members that Lua does not have access to.  Can be null.
        /// </summary>
        public string[] IgnoreMembers { get; private set; }
        /// <summary>
        /// If true, then Lua can only access members defined by the given type,
        /// otherwise it has access to inherited members.  Ignored if BahavesAs
        /// is not null.
        /// </summary>
        public bool DefinedOnly { get; private set; }
        /// <summary>
        /// Gets or sets a value that determines if a value can be passed back
        /// to C#.  This is used for special Lua values that should not be
        /// visible to C#.
        /// </summary>
        public bool CanPass { get; set; }

        /// <summary>
        /// Creates a new LuaUserData object with the given backing object 
        /// where it behaves as the generic type.
        /// </summary>
        /// <param name="backing">The backing object, cannot be null.</param>
        /// <param name="allInvisible">True if all members are invisible;
        /// false if all members are visible.</param>
        /// <exception cref="System.ArgumentNullException">If backing is null.</exception>
        public LuaUserData(object backing, bool allInvisible)
            : this(backing, null, false, allInvisible ? new string[0] : null, null) { }
        /// <summary>
        /// Creates a new LuaUserData object where the given members are 
        /// visible.  If the array is empty or null, then all members are 
        /// visible; otherwise only the given members are visible.
        /// </summary>
        /// <param name="backing">The backing object, cannot be null.</param>
        /// <param name="accessMembers">The members that are visible, if null
        /// or empty then all members are visible; otherwise only the given 
        /// members are visible.</param>
        /// <exception cref="System.ArgumentNullException">If backing is null.</exception>
        /// <exception cref="System.ArgumentException">If accessMembers contains
        /// a null entry.</exception>
        public LuaUserData(object backing, params string[] accessMembers)
            : this(backing, null, false,
            accessMembers == null || accessMembers.Length == 0 ? null : accessMembers,
            null) { }
        /// <summary>
        /// Creates a new LuaUserData object where the backing object behaves 
        /// like an object of the given type.
        /// </summary>
        /// <param name="backing">The backing object, cannot be null.</param>
        /// <param name="behavesAs">The type that this object will behave as;
        /// cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">If backing or behavesAs is null.</exception>
        /// <exception cref="System.ArgumentException">If the backing object
        /// is not of a compatible type with behavesAs.</exception>
        public LuaUserData(object backing, Type behavesAs)
            : this(backing, behavesAs, false, null, null) { }
        /// <summary>
        /// Creates a new LuaUserData object based on the given information.
        /// </summary>
        /// <param name="backing">The backing object, cannot be null.</param>
        /// <param name="behavesAs">The type that this object will behave as;
        /// cannot be null and backing must derive from it.</param>
        /// <param name="definedOnly">True if only defined members are visible;
        /// otherwise false.</param>
        /// <param name="accessMembers">An array of members that are visible
        /// to Lua; null to ignore.</param>
        /// <param name="ignoreMembers">An array of members that are not visible
        /// to Lua; null to ignore.</param>
        /// <exception cref="System.ArgumentNullException">If backing is null.</exception>
        /// <exception cref="System.ArgumentException">If accessMembers or
        /// ignoreMembers contains a null string -or- if backing does not
        /// derive from behavesAs.</exception>
        public LuaUserData(object backing, Type behavesAs, bool definedOnly,
            string[] accessMembers, params string[] ignoreMembers)
        {
            if (backing == null)
                throw new ArgumentNullException("backing");
            if (accessMembers != null && accessMembers.Contains(null))
                throw new ArgumentException(string.Format(Resources.CannotContainNull, "accessMembers"));
            if (ignoreMembers != null && ignoreMembers.Contains(null))
                throw new ArgumentException(string.Format(Resources.CannotContainNull, "ignoreMembers"));
            if (backing is Int16 || backing is Int32 || backing is Int64 || backing is UInt16 || backing is UInt32 ||
                backing is UInt64 || backing is Decimal || backing is Single)
            {
                backing = Convert.ToDouble(backing, CultureInfo.InvariantCulture);
                if (behavesAs != null)
                    behavesAs = typeof(double);
            }

            LuaUserData user = backing as LuaUserData;
            if (user != null)
            {
                this.Backing = user.Backing;
                this.DefinedOnly = user.DefinedOnly | definedOnly;
                this.CanPass = user.CanPass;

                if (behavesAs != null)
                {
                    if (!behavesAs.IsAssignableFrom(user.Backing.GetType()))
                        throw new ArgumentException(Resources.BehavesAsMustDeriveVar);
                    this.BehavesAs = behavesAs;
                }
                else
                    this.BehavesAs = user.BehavesAs;

                // combine access members so it is more restrictive
                if (accessMembers == null && user.AccessMembers == null)
                    this.AccessMembers = null;
                else
                {
                    string[] temp1 = accessMembers ?? new string[0];
                    string[] temp2 = user.AccessMembers ?? new string[0];

                    this.AccessMembers = temp1.Intersect(temp2).ToArray();
                }

                // combine ignore members so it is more restrictive
                if (ignoreMembers == null && user.IgnoreMembers == null)
                    this.IgnoreMembers = null;
                else
                {
                    string[] temp1 = ignoreMembers ?? new string[0];
                    string[] temp2 = user.IgnoreMembers ?? new string[0];

                    this.IgnoreMembers = temp1.Union(temp2).ToArray();
                }
            }
            else
            {
                if (behavesAs != null && !behavesAs.IsAssignableFrom(backing.GetType()))
                    throw new ArgumentException(Resources.BehavesAsMustDeriveVar);

                this.Backing = backing;
                this.BehavesAs = behavesAs;
                this.DefinedOnly = definedOnly;
                this.AccessMembers = accessMembers == null ? null : (string[])accessMembers.Clone();
                this.IgnoreMembers = ignoreMembers == null ? null : (string[])ignoreMembers.Clone();
                this.CanPass = true;
            }
        }

        /// <summary>
        /// Checks whether a given member is visible to Lua code.
        /// </summary>
        /// <param name="member">The name of the member.</param>
        /// <returns>True if the member is visible, otherwise false.</returns>
        public bool IsMemberVisible(string member)
        {
            Type backing = Backing.GetType();
            if (AccessMembers != null)
            {
                // the member must be in AccessMembers if it is not null
                if (!AccessMembers.Contains(member))
                    return false;
            }
            if (IgnoreMembers != null)
            {
                // the member must not be in IgnoreMembers if not null
                if (IgnoreMembers.Contains(member))
                    return false;
            }
            if (BehavesAs != null)
            {
                // sanity check, the backing type must inherit from BehavesAs type.
                if (!BehavesAs.IsAssignableFrom(backing))
                    throw new InvalidOperationException(Resources.BehavesAsMustDeriveVar);

                // than this variable is pretending to be another type, so
                //   check that type.  Favor having a LuaIgnoreAttribute
                var temp = BehavesAs.GetCustomAttributes(typeof(LuaIgnoreAttribute), true);
                if (temp != null && temp.Length > 0)
                {
                    if (!((LuaIgnoreAttribute)temp[0]).IsMemberVisible(backing, member))
                        return false;
                }
                else
                {
                    // the type must define the member without LuaIgnore
                    return BehavesAs.GetMembers().Where(m => m.Name == member &&
                        m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0).Any();
                }
            }
            // otherwise check the backing type, ignore this value if BehavesAs
            //   is not null.
            else if (DefinedOnly)
            {
                // must only use DeclaredOnly members
                if (backing.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == member &&
                        m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0).Count() == 0)
                    return false;
            }
            else
            {
                if (backing.GetMembers().Where(m => m.Name == member &&
                    m.GetCustomAttributes(typeof(LuaIgnoreAttribute), true).Length == 0).Count() == 0)
                    return false;
            }

            // there is no reason for it to not be visible, so it is
            return true;
        }
        /// <summary>
        /// Converts the current instance to a generic LuaUserData of the given
        /// type.  Returns null if the type is not compatible.
        /// </summary>
        /// <returns>A new generic LuaUserData object or null if not compatible.</returns>
        /// <typeparam name="T">The generic type to convert to.</typeparam>
        public LuaUserData<T> ConvertTo<T>()
        {
            // ignore BehavesAs becuse this is only used when passing
            //   an argument back to C# so there is no need
            Type targetType = Backing.GetType();
            if (!typeof(T).IsAssignableFrom(targetType))
                return null;

            return new LuaUserData<T>((T)Backing, BehavesAs, DefinedOnly,
                AccessMembers, IgnoreMembers);
        }

        /// <summary>
        /// Creates a new LuaUserData object from the given attribute.
        /// </summary>
        /// <param name="attribute">The attribute instance to build from; ignored
        /// if null.</param>
        /// <param name="target">The target object.</param>
        /// <returns>A new LuaUserData instance.</returns>
        public static LuaUserData CreateFrom(object target, LuaIgnoreAttribute attribute)
        {
            if (attribute != null)
                return new LuaUserData(target, attribute.BehavesAs, attribute.DefinedOnly, attribute.AccessMembers, attribute.IgnoreMembers);
            else
                return new LuaUserData(target, false);
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return Backing.ToString();
        }
        /// <summary>
        /// Determines whether the specified System.Object is equal to the 
        /// current System.Object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified System.Object is equal to the current
        /// System.Object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is LuaUserData)
                obj = ((LuaUserData)obj).Backing;

            return Backing.Equals(obj);
        }
        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>A hash code for the current System.Object.</returns>
        public override int GetHashCode()
        {
            return Backing.GetHashCode();
        }
    }

    /// <summary>
    /// A generic version of LuaUserData.  This is the same as LuaUserData,
    /// except that it ensures type-safety by forcing an object to be the
    /// given type.  This should be used in overloads to ensure that an
    /// argument is of a specific type.  Note that this only supports
    /// inheritence for type conversion, no user-defined casts.
    /// </summary>
    /// <typeparam name="T">The type of the backing variable.</typeparam>
    public sealed class LuaUserData<T> : LuaUserData
    {
        /// <summary>
        /// Gets the backing object as the generic type.
        /// </summary>
        public T BackingGeneric { get { return (T)Backing; } }

        /// <summary>
        /// Creates a new LuaUserData object with the given backing object 
        /// where it behaves as the generic type.
        /// </summary>
        /// <param name="backing">The backing object, cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">If backing is null.</exception>
        public LuaUserData(T backing)
            : base(backing, typeof(T), false, null, null) { }
        /// <summary>
        /// Creates a new LuaUserData object where the backing object behaves
        /// like an object of the given type.
        /// </summary>
        /// <param name="backing">The backing object, cannot be null.</param>
        /// <param name="behavesAs">The type that this object will behave as;
        /// cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">If backing or behavesAs is null.</exception>
        /// <exception cref="System.ArgumentException">If the type of the backing
        /// object is not compatible with behavesAs.</exception>
        public LuaUserData(T backing, Type behavesAs)
            : base(backing, behavesAs, false, null, null) { }
        /// <summary>
        /// Creates a new LuaUserData object based on the given information.
        /// </summary>
        /// <param name="backing">The backing object, cannot be null.</param>
        /// <param name="behavesAs">The type that this object will behave as;
        /// cannot be null and backing must derive from it.</param>
        /// <param name="definedOnly">True if only defined members are visible;
        /// otherwise false.</param>
        /// <param name="accessMembers">An array of members that are visible
        /// to Lua; null to ignore.</param>
        /// <param name="ignoreMembers">An array of members that are not visible
        /// to Lua; null to ignore.</param>
        /// <exception cref="System.ArgumentNullException">If backing is null.</exception>
        /// <exception cref="System.ArgumentException">If accessMembers or
        /// ignoreMembers contains a null string -or- if backing does not
        /// derive from behavesAs.</exception>
        public LuaUserData(T backing, Type behavesAs, bool definedOnly,
            string[] accessMembers, params string[] ignoreMembers)
            : base(backing, behavesAs, definedOnly, accessMembers, ignoreMembers) { }
        /// <summary>
        /// Creats a new LuaUserData from the given non-generic version of
        /// LuaUserData.
        /// </summary>
        /// <param name="from">The original LuaUserData object.</param>
        public LuaUserData(LuaUserData from)
            : base(from.Backing, from.BehavesAs, from.DefinedOnly,
            from.AccessMembers, from.IgnoreMembers) { }
    }
}
