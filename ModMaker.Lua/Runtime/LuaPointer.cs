using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Globalization;

namespace ModMaker.Lua.Runtime
{
    class LuaType
    {
        public LuaType(Type type)
        {
            this.Type = type;
        }

        public Type Type { get; private set; }

        public override string ToString()
        {
            return Type.ToString();
        }
    }

    class LuaPointerNew
    {
        object pref, ind;
        LuaEnvironment E;

        public LuaPointerNew(object pref, object ind, LuaEnvironment E)
        {
            this.pref = pref;
            this.ind = ind;
            this.E = E;
        }

        public object GetValue()
        {
            object o = pref is LuaPointerNew ? RuntimeHelper.GetValue(pref) : pref;
            if (o == null)
                throw new InvalidOperationException("Attempt to index a nil value.");
            if (o is MultipleReturn)
                o = RuntimeHelper.GetValue((o as MultipleReturn)[0]);

            if (o is double)
                throw new InvalidOperationException("Attempt to index a 'number' type.");
            else if (o is string)
                throw new InvalidOperationException("Attempt to index a 'string' type.");
            else if (o is bool)
                throw new InvalidOperationException("Attempt to index a 'boolean' type.");
            else if (o is LuaMethod)
                throw new InvalidOperationException("Attempt to index a 'method' type.");
            else if (o is LuaClassItem)
                throw new InvalidOperationException("Attempt to index a 'class definition item' type.");

            if (o is LuaTable)
            {
                return (o as LuaTable)._get(ind);
            }
            else if (o is LuaParameters)
            {
                return (o as LuaParameters).GetArg(Convert.ToInt32(ind, CultureInfo.InvariantCulture));
            }
            else if (o is LuaClass)
            {
                return new LuaUserData((o as LuaClass).GetItem(ind), new string[0], false);
            }
            else if (o is LuaType)
            {
                return GetSetItem((o as LuaType).Type, true);
            }
            else // if o is UserData
            {
                return GetSetItem(o, false);
            }
        }
        public void SetValue(object value)
        {
            object o = pref is LuaPointerNew ? RuntimeHelper.GetValue(pref) : pref;
            if (o == null)
                throw new InvalidOperationException("Attempt to index a nil value.");
            if (o is double)
                throw new InvalidOperationException("Attempt to index a 'number' type.");
            else if (o is string)
                throw new InvalidOperationException("Attempt to index a 'string' type.");
            else if (o is bool)
                throw new InvalidOperationException("Attempt to index a 'boolean' type.");
            else if (o is LuaMethod)
                throw new InvalidOperationException("Attempt to index a 'method' type.");

            if (o is LuaTable)
            {
                (o as LuaTable)._set(ind, value);
            }
            else if (o is LuaParameters)
            {
                (o as LuaParameters).SetArg(Convert.ToInt32(ind, CultureInfo.InvariantCulture), value);
            }
            else if (o is LuaClass)
            {
                (o as LuaClass).SetItem(ind, value);
            }
            else if (o is LuaUserData && (o as LuaUserData).Value is LuaClassItem)
            {
                ((o as LuaUserData).Value as LuaClassItem).SetItem(ind, value);
            }
            else if (o is LuaType)
            {
                GetSetItem((o as LuaType).Type, true, value);
            }
            else // if o is UserData
            {
                GetSetItem(o, false, value);
            }
        }

        object GetSetItem(object o, bool stat, object value = null)
        {
            object ind = this.ind;

            LuaUserData userDat = o as LuaUserData;
            o = userDat == null ? o : userDat.Value;
            Type t = stat ? o as Type : o.GetType();
            if (t.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length > 0)
                throw new InvalidOperationException("Cannot " + (value == null ? "get" : "set") + 
                    " a member of a type marked with LuaIgnoreAttribute.");

            if (ind is double || ind is LuaTable)
            {
                if (stat)
                    throw new InvalidOperationException("Attempt to call indexer on a static type.");

                List<object> param = new List<object>();
                if (ind is double)
                {
                    param.Add(ind);
                }
                else
                {
                    foreach (var item in (ind as LuaTable))
                    {
                        object oo = RuntimeHelper.GetValue(item.Value);
                        if (oo is LuaTable)
                            throw new InvalidOperationException("Arguments to indexer cannot be a table.");
                        param.Add(oo);
                    }
                }

                if (o is Array)
                {
                    List<long> i = new List<long>();
                    foreach (var item in param)
                    {
                        if (!(item is double))
                            throw new InvalidOperationException("Arguments to indexer for an array can only be numbers.");
                        else
                            i.Add(Convert.ToInt64(item, CultureInfo.InvariantCulture));
                    }

                    if (value != null)
                    {
                        (o as Array).SetValue(value, i.ToArray());
                        return null;
                    }
                    else
                        return (o as Array).GetValue(i.ToArray());
                }

                if (value != null)
                    param.Add(value);

                object[] args = param.ToArray();
                Tuple<MethodBase, object> meth = RuntimeHelper.GetCompatibleMethod(
                    t.GetMethods()
                        .Where(m => m.Name == (value == null ? "get_Item" : "set_Item"))
                        .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length == 0)
                        .Select(m => new Tuple<MethodBase, object>(m, o))
                        .ToArray(), 
                    args);

                if (meth == null || (userDat != null && userDat.Members != null && !userDat.Members.Contains("Item") && !userDat.Members.Contains(value == null ? "Item+" : "Item-")))
                    throw new InvalidOperationException("Unable to find an indexer that matches the provided arguments for type '" +
                        o.GetType() + "'.");

                return meth.Item1.Invoke(meth.Item2, args);
            }
            else if (ind is string)
            {
                string name = ind as string;
                int over = -1;
                if (name.Contains('`'))
                {
				    if (!int.TryParse(name.Substring(name.IndexOf('`') + 1), out over))
                        throw new InvalidOperationException("Only numbers are allowed after the grave(`) when specifying an overload.");

				    name = name.Substring(0, name.IndexOf('`'));
                }

                MemberInfo[] Base = t.GetMember(name)
                    .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length == 0)
                    .ToArray();
                if (Base == null || Base.Length == 0 || (userDat != null && userDat.Members != null && !userDat.Members.Contains(name) && !userDat.Members.Contains(name + (value == null ? "+" : "-"))))
                    throw new MissingMemberException("'" + name + "' is not a member of type '" + t + "'.");

                switch (Base[0].MemberType)
                {
                    case MemberTypes.Field:
                        {
                            if (over != -1)
                                throw new InvalidOperationException("Cannot specify an overload when accessing a field.");

                            FieldInfo field = (FieldInfo)Base[0];
                            if (value == null)
                            {
                                return field.GetValue(stat ? null : o);
                            }
                            else
                            {
                                value = RuntimeHelper.ConvertType(value, field.FieldType);
                                field.SetValue(stat ? null : o, value);
                                return null;
                            }
                        }
                    case MemberTypes.Property:
                        {
                            if (over != -1)
                                throw new InvalidOperationException("Cannot specify an overload when accessing a field.");

                            if (value == null)
                            {
                                MethodInfo meth = t.GetMethod("get_" + name, new Type[0]);
                                if (meth == null)
                                    throw new MemberAccessException("The property '" + name + "' is write-only.");

                                return meth.Invoke(stat ? null : o, null);
                            }
                            else
                            {
                                MethodInfo meth = t.GetMethod("get_" + name, new Type[] { (Base[0] as PropertyInfo).PropertyType });
                                if (meth == null)
                                    throw new MemberAccessException("The property '" + name + "' is read-only.");

                                value = RuntimeHelper.ConvertType(value, (Base[0] as PropertyInfo).PropertyType);
                                meth.Invoke(stat ? null : o, new object[] { value });
                                return null;
                            }
                        }
                    case MemberTypes.Method:
                        {
                            if (value != null)
                                throw new InvalidOperationException("Cannot set the value of a method.");

                            return new LuaMethod(Base.Select(m => (MethodInfo)m), new[] { stat ? null : o }, name, E);
                        }
                    default:
                        throw new MemberAccessException("MemberTypes." + Base[0].MemberType + " is not supported.");
                }
            }
            else
                throw new InvalidOperationException("Indices of a User-Defined type must be a string, number, or table.");
        }
    }
}