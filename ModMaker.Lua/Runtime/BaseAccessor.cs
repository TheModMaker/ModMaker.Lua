using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Globalization;

namespace ModMaker.Lua.Runtime
{
    class BaseAccessor
    {
        object target;

        public BaseAccessor(object obj)
        {
            this.target = obj;
        }

        public object GetValue(object index, LuaEnvironment E)
        {
            return GetSetItem(target, index, E);
        }
        public void SetValue(object index, object value, LuaEnvironment E)
        {
            GetSetItem(target, index, E, value);
        }

        static object GetSetItem(object o, object ind, LuaEnvironment E, object value = null)
        {
            Type t = o.GetType().BaseType;
            if (t.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length > 0)
                throw new InvalidOperationException("Cannot " + (value == null ? "get" : "set") +
                    " a member of a type marked with LuaIgnoreAttribute.");

            if (ind is double || ind is LuaTable)
            {
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
                Tuple<MethodInfo, object> meth = RuntimeHelper.GetCompatibleMethod(
                    t.GetMethods()
                        .Where(m => m.Name == (value == null ? "get_Item" : "set_Item"))
                        .Where(m => m.GetCustomAttributes(typeof(LuaIgnoreAttribute), false).Length == 0)
                        .Select(m => new Tuple<MethodInfo, object>(m, o))
                        .ToArray(),
                    ref args);

                if (meth == null)
                    throw new InvalidOperationException("Unable to find an indexer that matches the provided arguments for type '" +
                        o.GetType() + "'.");

                return Hack(meth.Item1, meth.Item2, args);
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
                if (Base == null || Base.Length == 0)
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
                                return field.GetValue(o);
                            }
                            else
                            {
                                value = RuntimeHelper.ConvertType(value, field.FieldType);
                                field.SetValue(o, value);
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

                                return Hack(meth, o);
                            }
                            else
                            {
                                MethodInfo meth = t.GetMethod("get_" + name, new Type[] { (Base[0] as PropertyInfo).PropertyType });
                                if (meth == null)
                                    throw new MemberAccessException("The property '" + name + "' is read-only.");

                                value = RuntimeHelper.ConvertType(value, (Base[0] as PropertyInfo).PropertyType);
                                Hack(meth, o, value);
                                return null;
                            }
                        }
                    case MemberTypes.Method:
                        {
                            if (value != null)
                                throw new InvalidOperationException("Cannot set the value of a method.");

                            var ret = HackCreate(Base.Select(m => (MethodInfo)m), o);
                            return new LuaMethod(ret.Item1, ret.Item2, name, E);
                            //return new LuaMethod(Base.Select(m => (MethodInfo)m), new[] { o }, name, E);
                        }
                    default:
                        throw new MemberAccessException("MemberTypes." + Base[0].MemberType + " is not supported.");
                }
            }
            else
                throw new InvalidOperationException("Indices of a User-Defined type must be a string, number, or table.");
        }
        static object Hack(MethodInfo meth, object target, params object[] args)
        {
            // Thanks to desco in StackOverflow in this question:
            //  http://stackoverflow.com/questions/3378010/how-to-invoke-non-virtually-the-original-implementation-of-a-virtual-method
            //  this invokes a method info without using a virtual call.

            Type t = target.GetType();
            var dm = new DynamicMethod("proxy", meth.ReturnType, new[] { t, typeof(object[]) }, t);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            for (int i = 0; i < args.Length; i++)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldelem, typeof(object));
                il.Emit(OpCodes.Unbox_Any, args[i].GetType());
            }
            il.Emit(OpCodes.Call, meth);
            il.Emit(OpCodes.Ret);
            var action = dm.CreateDelegate(typeof(Func<,,>).MakeGenericType(t, typeof(object[]), typeof(object)));
            return action.DynamicInvoke(target, args);
        }
        static Tuple<MethodInfo[], object[]> HackCreate(IEnumerable<MethodInfo> meths, object target)
        {
            // implimentation of Hack to work with a LuaMethod and to allow
            //   for run-time overload resolution.

            AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("Assembly"), AssemblyBuilderAccess.Run);
            ModuleBuilder _mb = ab.DefineDynamicModule("d");
            TypeBuilder tb = _mb.DefineType("Type");
            FieldBuilder field = tb.DefineField("Target", typeof(object), FieldAttributes.Private);

            // define the constructor
            ConstructorBuilder cb = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(object) });
            ILGenerator gen = cb.GetILGenerator();
            // base();
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, typeof(object).GetConstructor(new Type[0]));
            // this.Target = arg_1
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, field);
            gen.Emit(OpCodes.Ret);

            // define the method
            foreach (var meth in meths)
            {
                var param = meth.GetParameters();
                MethodBuilder mb = tb.DefineMethod("Do", MethodAttributes.Public, meth.ReturnType, param.Select(p => p.ParameterType).ToArray());
                gen = mb.GetILGenerator();
                // return this.Target.{meth}(...);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
                for (int i = 0; i < param.Length; i++)
                {
                    gen.Emit(OpCodes.Ldarg, (i + 1));
                }
                gen.Emit(OpCodes.Call, meth);
                gen.Emit(OpCodes.Ret);
            }

            // create the type and return
            Type ret = tb.CreateType();
            object retO = Activator.CreateInstance(ret, target);
            return new Tuple<MethodInfo[], object[]>(ret.GetMethods().Where(m => m.Name == "Do").ToArray(), new object[] { retO });
        }
    }
}
