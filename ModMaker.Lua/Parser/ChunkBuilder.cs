using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua.Runtime;
using System.Reflection.Emit;
using System.Reflection;
using System.Security.Permissions;
using System.Security;
using System.Collections;

namespace ModMaker.Lua.Parser
{
    class ChunkBuilderNew
    {
        class Nest
        {
            static int ID = 1;

            public List<Dictionary<string, FieldBuilder>> Locals;
            public Nest Parrent;
            public FieldBuilder ParrentInst;
            public FieldBuilder Env;
            public LocalBuilder ChildInst;
            public TypeBuilder Type;
            public ConstructorBuilder Constructor;

            public Nest(Nest parrent)
            {
                this.Parrent = parrent;
                this.Locals = new List<Dictionary<string, FieldBuilder>>();
                this.Locals.Add(new Dictionary<string, FieldBuilder>());

                this.Type = parrent.Type.DefineNestedType("<>_NestedType_" + (ID++), TypeAttributes.NestedPublic | TypeAttributes.Class);
                this.ParrentInst = Type.DefineField("<>_parrent", parrent.Type, FieldAttributes.Public);
                this.Env = Type.DefineField("<>_E", typeof(LuaEnvironment), FieldAttributes.Public);

                this.Constructor = Type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { parrent.Type, typeof(LuaEnvironment) });
                var gen = Constructor.GetILGenerator();

                // this.{ParrentInst} = arg_1;
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stfld, ParrentInst);
                // this.{Env} = arg_2;
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_2);
                gen.Emit(OpCodes.Stfld, Env);
                gen.Emit(OpCodes.Ret);
            }
            public Nest(TypeBuilder type)
            {
                this.Parrent = this;
                this.ParrentInst = null;
                this.Locals = new List<Dictionary<string, FieldBuilder>>();
                this.Locals.Add(new Dictionary<string, FieldBuilder>());

                this.Type = type;
                this.Env = Type.DefineField("<>_E", typeof(LuaEnvironment), FieldAttributes.Public);
                ConstructorBuilder cb = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                    new Type[] { typeof(LuaEnvironment) });
                ILGenerator gen = cb.GetILGenerator();
                // this.{Env} = arg_1;
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stfld, Env);
                gen.Emit(OpCodes.Ret);
            }

            public FieldBuilder FindLocal(string name)
            {
                foreach (var item in Locals)
                {
                    if (item.ContainsKey(name))
                        return item[name];
                }
                return null;
            }
            public FieldBuilder DefineField(string name)
            {
                FieldBuilder ret = Type.DefineField(name, typeof(object), FieldAttributes.Public);
                Locals[Locals.Count - 1][name] = ret;
                return ret;
            }
        }

        List<Nest> _types;
        int _mid = 1;

        [SecuritySafeCritical]
        [ReflectionPermission(SecurityAction.Demand, RestrictedMemberAccess=true)]
        public ChunkBuilderNew(TypeBuilder tb)
        {
            /* get the type ready */
            this._types = new List<Nest>();
            this._types.Add(new Nest(tb));
        }

        public ILGenerator CurrentGenerator { get; set; }

        public void PushEnv()
        {
            CurrentGenerator.Emit(OpCodes.Ldarg_0);
            CurrentGenerator.Emit(OpCodes.Ldfld, _types[_types.Count - 1].Parrent.Env);
        }

        public void Using(string name)
        {
            PushEnv();
            CurrentGenerator.Emit(OpCodes.Ldstr, name);
            CurrentGenerator.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("AddUsing"));
        }
        public void DefineClass(string name, string[] impliments, long line, long col)
        {
            ILGenerator gen = CurrentGenerator;
            LocalBuilder loc = gen.DeclareLocal(typeof(List<string>));
            // loc = new List<string>();
            gen.Emit(OpCodes.Newobj, typeof(List<string>).GetConstructor(new Type[0]));
            gen.Emit(OpCodes.Stloc, loc);
            foreach (var item in impliments)
            {
                // loc.Add({item});
                gen.Emit(OpCodes.Ldloc, loc);
                gen.Emit(OpCodes.Ldstr, item);
                gen.Emit(OpCodes.Callvirt, typeof(List<Type>).GetMethod("Add"));
            }

            // RuntimeHelper.DefineClass(this.{E}, loc, {name});
            PushEnv();
            gen.Emit(OpCodes.Ldloc, loc);
            gen.Emit(OpCodes.Ldstr, name);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("DefineClass", BindingFlags.Public | BindingFlags.Static));
        }

        public void ImplimentFunc(IParseItem block, string[] args, string funcName, bool instance, ILGenerator gen, object n, string name)
        {
            ILGenerator old = CurrentGenerator;
            Nest nest = n as Nest;
            if (instance)
                args = new[] { "self" }.Union(args)
                    .ToArray();
            if (gen == null)
            {
                nest = new Nest(_types[_types.Count - 1]);
                name = "<>_" + funcName + "_" + (_mid++);
                MethodBuilder mb = nest.Parrent.Type.DefineMethod(name, MethodAttributes.Public, 
                    typeof(MultipleReturn), 
                    new Type[] { typeof(LuaParameters) });
                gen = mb.GetILGenerator();
            }
            CurrentGenerator = gen;
            _types.Add(nest);

/*StartBlock*/nest.Locals.Add(new Dictionary<string,FieldBuilder>());
            nest.ChildInst = CurrentGenerator.DeclareLocal(nest.Type);
            // {nest.ChildInst} = new {nest.Constructor}(this, this.{E});
            CurrentGenerator.Emit(OpCodes.Ldarg_0);
            CurrentGenerator.Emit(OpCodes.Ldarg_0);
            CurrentGenerator.Emit(OpCodes.Ldfld, nest.Parrent.Env);
            CurrentGenerator.Emit(OpCodes.Newobj, nest.Constructor);
            CurrentGenerator.Emit(OpCodes.Stloc, nest.ChildInst);
            for (int i = 0; i < args.Length; i++)
            {
                FieldBuilder field = nest.DefineField(args[i]);

                // {nest.ChildInst}.{field} = arg_1.GetArg({i});
                CurrentGenerator.Emit(OpCodes.Ldloc, nest.ChildInst);
                CurrentGenerator.Emit(OpCodes.Ldarg_1);
                CurrentGenerator.Emit(OpCodes.Ldc_I4, i);
                CurrentGenerator.Emit(OpCodes.Callvirt, typeof(LuaParameters).GetMethod("GetArg"));
                CurrentGenerator.Emit(OpCodes.Stfld, field);
            }
            block.GenerateILNew(this);
/*EndBlock*/nest.Locals.RemoveAt(nest.Locals.Count - 1);
            nest.Type.CreateType();
            _types.RemoveAt(_types.Count - 1);

            CurrentGenerator = old;
            // RuntimeHelper.GetFunction(this.{E}, {nest.Parrent.Type}, {name}, {nest.Parrent.ChildInst != null ? nest.Parrent.ChildInst : arg_0});
            PushEnv();
            old.Emit(OpCodes.Ldtoken, nest.Parrent.Type);
            old.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", new Type[] { typeof(RuntimeTypeHandle) }));
            old.Emit(OpCodes.Ldstr, name);
            if (nest.Parrent.ChildInst != null)
                old.Emit(OpCodes.Ldloc, nest.Parrent.ChildInst);
            else
                old.Emit(OpCodes.Ldarg_0);
            old.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("GetFunction", BindingFlags.Public | BindingFlags.Static));
        }
        public void DefineFunc(string funcName, out string name, out object nest)
        {
            nest = new Nest(_types[_types.Count - 1]);
            name = "<>_" + funcName + "_" + (_mid++);
            MethodBuilder mb = (nest as Nest).Parrent.Type.DefineMethod(name, MethodAttributes.Public,
                typeof(MultipleReturn),
                new Type[] { typeof(LuaParameters) });
            CurrentGenerator = mb.GetILGenerator();
        }
        public void DefineLocal(string name, bool stack)
        {
            var field = _types[_types.Count - 1].DefineField(name);

            if (stack)
            {
                if (_types[_types.Count - 1].ChildInst != null)
                    CurrentGenerator.Emit(OpCodes.Ldloc, _types[_types.Count - 1].ChildInst);
                else
                    CurrentGenerator.Emit(OpCodes.Ldarg_0);
                CurrentGenerator.Emit(OpCodes.Ldflda, field);
            }
        }
        public void ResolveName(string name, bool set)
        {
            int i = -1;
            FieldBuilder field = null;
            for (int t = _types.Count - 1; t >= 0; t--)
            {
                Nest nest = _types[t];
                if ((field = nest.FindLocal(name)) != null)
                {
                    i = t;
                    break;
                }
            }
            if (i != -1)
            {
                if (_types[_types.Count - 1].ChildInst != null)
                    CurrentGenerator.Emit(OpCodes.Ldloc, _types[_types.Count - 1].ChildInst);
                else
                    CurrentGenerator.Emit(OpCodes.Ldarg_0);
                for (int t = _types.Count - 1; t > i; t--)
                {
                    Nest nest = _types[t];
                    CurrentGenerator.Emit(OpCodes.Ldfld, nest.ParrentInst);
                }
                CurrentGenerator.Emit(set ? OpCodes.Ldflda : OpCodes.Ldfld, field);
                return;
            }

            // RuntimeHelper.GetGlobal(this.{E}, {name});
            CurrentGenerator.Emit(OpCodes.Ldarg_0);
            CurrentGenerator.Emit(OpCodes.Ldfld, _types[_types.Count - 1].Parrent.Env);
            CurrentGenerator.Emit(OpCodes.Ldstr, name);
            CurrentGenerator.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("GetGlobal", BindingFlags.Public | BindingFlags.Static));
            if (set)
            {
                LocalBuilder loc = CurrentGenerator.DeclareLocal(typeof(object));
                CurrentGenerator.Emit(OpCodes.Stloc, loc);
                CurrentGenerator.Emit(OpCodes.Ldloca, loc);
            }
        }

        public void DefineGlobalFunc()
        {
            if (_types.Count != 1)
                throw new InvalidOperationException();

            MethodBuilder mb = _types[0].Type.DefineMethod("<>_global_", MethodAttributes.Public, 
                typeof(MultipleReturn), 
                new Type[] { typeof(LuaParameters) });
            CurrentGenerator = mb.GetILGenerator();

            MethodBuilder mb2 = _types[0].Type.DefineMethod("<>_global_helper_", MethodAttributes.Public | MethodAttributes.Virtual, 
                typeof(object[]), 
                new Type[] { typeof(object[]) });
            ILGenerator gen = mb2.GetILGenerator();
            // return Enumerable.ToArray(this.{mb}(new LuaParameters(arg_1, this.{E})));
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, _types[_types.Count - 1].Parrent.Env);
            gen.Emit(OpCodes.Newobj, typeof(LuaParameters).GetConstructor(new Type[] { typeof(object[]), typeof(LuaEnvironment) }));
            gen.Emit(OpCodes.Callvirt, mb);
            gen.Emit(OpCodes.Call, typeof(Enumerable).GetMethod("ToArray").MakeGenericMethod(typeof(object)));
            gen.Emit(OpCodes.Ret);
            _types[0].Type.DefineMethodOverride(mb2, typeof(IModule).GetMethod("Execute"));
        }
        public LuaChunk CreateChunk(LuaEnvironment E)
        {
            if (_types.Count != 1)
                throw new InvalidOperationException();

            /* generate the type */
            Type t = _types[0].Type.CreateType();
            return new LuaChunk(E, t);
        }

        public void StartBlock()
        {
            _types[_types.Count - 1].Locals.Add(new Dictionary<string, FieldBuilder>());
        }
        public void EndBlock()
        {
            _types[_types.Count - 1].Locals.RemoveAt(_types[_types.Count - 1].Locals.Count - 1);
        }
    }
}