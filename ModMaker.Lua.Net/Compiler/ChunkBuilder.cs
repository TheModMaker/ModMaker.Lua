using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Compiler
{
    /// <summary>
    /// Helps build a chunk by managing nested types and scopes.  Also 
    /// generates some code to load locals and the current environment.
    /// </summary>
    sealed class ChunkBuilder
    {
        /// <summary>
        /// Defines a nested type for a nested function.  Each nested function
        /// that also has nested methods will have a nested type.  If
        /// a function does not have any nested functions it does not have a
        /// nested type.  The function is defined in the parrent nest and any
        /// local variables defined in this function that is captured is 
        /// defined as a field in the nested type.  Then any nested functions
        /// are defined in the nested type.  This is how C# handles captures 
        /// with lambda expressions.
        /// </summary>
        sealed class NestInfo
        {
            /// <summary>
            /// A static ID to generate unique names across all types.
            /// </summary>
            static int ID = 1;
            /// <summary>
            /// Defines the captures for this nest.
            /// </summary>
            HashSet<NameItem> captures;
            /// <summary>
            /// Contains the members that are defined in this type.
            /// </summary>
            public HashSet<string> members;

            /// <summary>
            /// Gets a dictionary of local variables that can be reused indexed
            /// by the type of the variable.
            /// </summary>
            public Dictionary<Type, Stack<LocalBuilder>> FreeLocals { get; private set; }
            /// <summary>
            /// Gets the ILGenerator for this nest.  This generator belongs to 
            /// the type of the parrent nest but is only used for generating 
            /// code for this nest.
            /// </summary>
            public ILGenerator Generator { get; private set; }
            /// <summary>
            /// Gets the type definition for this nested type.  It is
            /// created in the constructor.
            /// </summary>
            public TypeBuilder TypeDef { get; private set; }
            /// <summary>
            /// Gets the parrent nest object, is null for the root nest object.
            /// </summary>
            public NestInfo Parrent { get; private set; }
            /// <summary>
            /// Gets the field defined in this type that holds the parrent 
            /// instance.  This may not exist if the type does not capture
            /// any locals from the parrent type.
            /// </summary>
            public FieldBuilder ParrentInst { get; private set; }
            /// <summary>
            /// Gets the local variable that holds an instance to this type.
            /// </summary>
            public LocalBuilder ThisInst { get; private set; }
            /// <summary>
            /// Gets the local variable definitions for this nest.  These are
            /// the variables defined in the defining method.  The indicies in
            /// the list are the scopes of the variables and the dictionary
            /// maps the name to the field in the type.
            /// </summary>
            public Stack<Dictionary<string, VarDefinition>> Locals { get; private set; }

            /// <summary>
            /// Creates a new nest with the given parrent.
            /// </summary>
            /// <param name="parrent">The parrent nest.</param>
            /// <param name="gen">The generator used to generate code for this
            /// function.</param>
            /// <param name="storeParrent">True to create a field that stores 
            /// the parrent instance; otherwise false.</param>
            /// <param name="captures">The local variables that have been 
            /// captured by nested functions.</param>
            /// <param name="createType">True to create a nested type, otherwise
            /// false.</param>
            public NestInfo(NestInfo parrent, ILGenerator gen, NameItem[] captures, bool createType, bool storeParrent)
            {
                this.FreeLocals = new Dictionary<Type, Stack<LocalBuilder>>();
                this.members = new HashSet<string>();
                this.captures = new HashSet<NameItem>(captures);
                this.Parrent = parrent;
                this.Generator = gen;
                this.Locals = new Stack<Dictionary<string, VarDefinition>>();
                this.Locals.Push(new Dictionary<string, VarDefinition>());

                if (createType)
                {
                    // create the type and constructor.
                    this.TypeDef = parrent.TypeDef.DefineNestedType("<>c__DisplayClass" + (ID++),
                        TypeAttributes.NestedPublic | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);
                    var ctor = this.TypeDef.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[0]);
                    var cgen = ctor.GetILGenerator();

                    // base();
                    cgen.Emit(OpCodes.Ldarg_0);
                    cgen.Emit(OpCodes.Call, typeof(object).GetConstructor(new Type[0]));
                    cgen.Emit(OpCodes.Ret);

                    if (storeParrent)
                        this.ParrentInst = this.TypeDef.DefineField("CS$<>__locals", parrent.TypeDef, FieldAttributes.Public);
                    else
                        this.ParrentInst = null;

                    // create the local definition
                    // ThisInst = new TypeDef();
                    this.ThisInst = gen.DeclareLocal(this.TypeDef);
                    gen.Emit(OpCodes.Newobj, ctor);
                    gen.Emit(OpCodes.Stloc, this.ThisInst);

                    if (storeParrent)
                    {
                        // ThisInst.ParrentInst = this;
                        gen.Emit(OpCodes.Ldloc, this.ThisInst);
                        gen.Emit(OpCodes.Ldarg_0);
                        gen.Emit(OpCodes.Stfld, this.ParrentInst);
                    }
                }
                else
                {
                    this.TypeDef = null;
                    this.ThisInst = null;
                    this.ParrentInst = null;
                }
            }
            NestInfo(TypeBuilder tb)
            {
                this.FreeLocals = new Dictionary<Type, Stack<LocalBuilder>>();
                this.captures = new HashSet<NameItem>();
                this.members = new HashSet<string>();
                this.Locals = new Stack<Dictionary<string, VarDefinition>>();
                this.Locals.Push(new Dictionary<string, VarDefinition>());
                this.Parrent = null;
                this.ParrentInst = null;
                this.Generator = null;
                this.TypeDef = tb;
                this.ThisInst = null;
            }

            /// <summary>
            /// Creates the root nest node from the given TypeBuilder.
            /// </summary>
            /// <param name="tb">The type builder to create for.</param>
            /// <param name="gen">The ILGenerator for the global function.</param>
            /// <param name="captures">The captures for the global function.</param>
            /// <param name="createType">Whether to create a type for the global 
            /// function.</param>
            /// <returns>The new root nest node.</returns>
            public static NestInfo Create(TypeBuilder tb, ILGenerator gen, NameItem[] captures, bool createType)
            {
                NestInfo temp = new NestInfo(tb);
                return new NestInfo(temp, gen, captures, createType, false);
            }

            /// <summary>
            /// Searches this type for a given local variable and returns an
            /// object to manipulate it's value.
            /// </summary>
            /// <param name="name">The Lua name of the variable.</param>
            /// <returns>A variable that will manipulate it's value or null if
            /// not found.</returns>
            public VarDefinition FindLocal(NameItem name)
            {
                // the iterator will return in the order they would be pop'd.
                foreach (var item in Locals)
                {
                    if (item.ContainsKey(name.Name))
                        return item[name.Name];
                }
                return null;
            }
            /// <summary>
            /// Defines a new Local variable and returns the field that 
            /// represents it.
            /// </summary>
            /// <param name="name">The Lua name of the variable.</param>
            /// <returns>The variable that represents the local.</returns>
            public VarDefinition DefineLocal(NameItem name)
            {
                if (captures.Contains(name))
                {
                    string mName = name.Name;
                    if (members.Contains(mName))
                    {
                        int i = 0;
                        while (members.Contains(mName + "_" + i))
                            i++;
                        mName += "_" + i;
                    }

                    members.Add(mName);
                    var field = TypeDef.DefineField(mName, typeof(object), FieldAttributes.Public);
                    return Locals.Peek()[name.Name] = new CapturedVarDef(Generator, ThisInst, field);
                }
                else
                {
                    var loc = Generator.DeclareLocal(typeof(object));
                    return Locals.Peek()[name.Name] = new LocalVarDef(Generator, loc);
                }
            }
            /// <summary>
            /// Returns an object that starts a new local block when created 
            /// and will end it when Dispose is called.  For use with 'using'
            /// keyword.
            /// </summary>
            /// <returns>A helper object that will end the scope when Dispose
            /// is called.</returns>
            public IDisposable LocalBlock()
            {
                Locals.Push(new Dictionary<string, VarDefinition>());
                return Helpers.Disposable(() =>
                {
                    Locals.Pop();
                });
            }
        }

        NestInfo curNest;
        int _mid = 1;

        /// <summary>
        /// Creates a new ChunkBuilder and initializes the state.
        /// </summary>
        /// <param name="tb">The root type of this chunk.</param>
        /// <param name="captures">An array of the global captures.</param>
        /// <param name="createType">True to create a nested type for the global
        /// function, this means that there are nested functions.</param>
        public ChunkBuilder(TypeBuilder/*!*/ tb, NameItem[]/*!*/ captures, bool createType)
        {
            //// ILuaEnviormnent $Env;
            var field = tb.DefineField("$Env", typeof(ILuaEnvironment), FieldAttributes.Private);

            //// MultipleReturn $Invoke(ILuaEnvironment E, object[] args);
            var method = tb.DefineMethod("$Invoke",
                MethodAttributes.Public | MethodAttributes.HideBySig,
                typeof(MultipleReturn),
                new[] { typeof(ILuaEnvironment), typeof(object[]) });
            curNest = NestInfo.Create(tb, method.GetILGenerator(), captures, createType);

            AddGetEnvironment(tb, field);
            AddSetEnvironment(tb, field);
            AddInvoke(tb, method, field);
            AddConstructor(tb, field);
        }
        /// <summary>
        /// Adds a constructor that accepts a single ILuaEnvironment argument
        /// to the given type builder object.
        /// </summary>
        /// <param name="tb">The type builder to add to.</param>
        /// <param name="envField">The field that stores the environment.</param>
        static void AddConstructor(TypeBuilder/*!*/ tb, FieldBuilder/*!*/ envField)
        {
            //// .ctor(ILuaEnvironment/*!*/ E);
            var ctor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                new[] { typeof(ILuaEnvironment) });
            var gen = ctor.GetILGenerator();

            // this.$Env = E;
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, envField);
            gen.Emit(OpCodes.Ret);
        }
        /// <summary>
        /// Adds the two overloads of IMethod.Invoke that simply calls the
        /// given method.
        /// </summary>
        /// <param name="tb">The type builder to add to.</param>
        /// <param name="realMethod">The real method to call.</param>
        /// <param name="envField">The field that contains the environment.</param>
        static void AddInvoke(TypeBuilder/*!*/ tb, MethodBuilder/*!*/ realMethod,
            FieldBuilder/*!*/ envField)
        {
            //// MultipleReturn IMethod.$Invoke(int overload, int[] byRef, object[] args);
            MethodBuilder mb = tb.DefineMethod("$Invoke",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(MultipleReturn),
                new Type[] { typeof(int), typeof(int[]), typeof(object[]) });
            var gen = mb.GetILGenerator();

            // return this.$Invoke(this.Environment, args);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, envField);
            gen.Emit(OpCodes.Ldarg_3);
            gen.Emit(OpCodes.Callvirt, realMethod);
            gen.Emit(OpCodes.Ret);
            tb.DefineMethodOverride(mb, typeof(IMethod).GetMethod("Invoke",
                new[] { typeof(int), typeof(int[]), typeof(object[]) }));

            //// MultipleReturn IMethod.$Invoke(int[] byRef, object[] args);
            mb = tb.DefineMethod("$Invoke",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(MultipleReturn),
                new Type[] { typeof(int[]), typeof(object[]) });
            gen = mb.GetILGenerator();

            // return this.$Invoke(this.Environment, args);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, envField);
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Callvirt, realMethod);
            gen.Emit(OpCodes.Ret);
            tb.DefineMethodOverride(mb, typeof(IMethod).GetMethod("Invoke",
                new[] { typeof(int[]), typeof(object[]) }));
        }
        /// <summary>
        /// Adds a get_Environment method to the given type builder object.
        /// </summary>
        /// <param name="tb">The type builder to add to.</param>
        /// <param name="envField">The envField that holds the environment.</param>
        static void AddGetEnvironment(TypeBuilder/*!*/ tb, FieldBuilder/*!*/ envField)
        {
            //// ILuaEnvironment IMethod.get_Environment();
            var getEnv = tb.DefineMethod("get_Environment",
                MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Public,
                typeof(ILuaEnvironment),
                new Type[0]);
            var gen = getEnv.GetILGenerator();

            // return this.$Env;
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, envField);
            gen.Emit(OpCodes.Ret);
        }
        /// <summary>
        /// Adds a set_Environment method to the given type builder object.
        /// </summary>
        /// <param name="tb">The type builder to add to.</param>
        /// <param name="envField">The envField that holds the environment.</param>
        static void AddSetEnvironment(TypeBuilder/*!*/ tb, FieldBuilder/*!*/ envField)
        {
            //// void IMethod.set_Environment(ILuaEnvironment value);
            var getEnv = tb.DefineMethod("set_Environment",
                MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Public,
                null,
                new[] { typeof(ILuaEnvironment) });
            var gen = getEnv.GetILGenerator();

            // if (value == null) throw new ArgumentNullException("value");
            var end = gen.DefineLabel();
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Brtrue, end);
            gen.Emit(OpCodes.Ldstr, "value");
            gen.Emit(OpCodes.Newobj, typeof(ArgumentNullException).GetConstructor(new[] { typeof(string) }));
            gen.Emit(OpCodes.Throw);

            // this.$Env = value;
            gen.MarkLabel(end);
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Stfld, envField);
            gen.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Gets the ILGenerator for the current function.
        /// </summary>
        public ILGenerator CurrentGenerator { get { return curNest.Generator; } }

        /// <summary>
        /// Compiles the current code into am IMethod.
        /// </summary>
        /// <param name="E">The current environment.</param>
        /// <returns>A new IMethod compiled from the current code.</returns>
        public IMethod CreateChunk(ILuaEnvironment/*!*/ E)
        {
            if (curNest == null)
                throw new InvalidOperationException();

            if (curNest.TypeDef != null)
                curNest.TypeDef.CreateType();
            Type t = curNest.Parrent.TypeDef.CreateType();
            return LuaGlobalMethod.Create(E, t);
        }
        /// <summary>
        /// Starts a local-variable scope block and returns an object that will
        /// end the scope when Dispose is called. This is to be used with the 
        /// 'using' keyword.
        /// </summary>
        /// <returns>An object that will end the local block when 'Dispose' is
        /// called.</returns>
        public IDisposable LocalBlock()
        {
            return curNest.LocalBlock();
        }

        /// <summary>
        /// Implements a function definition based on a given function definition.
        /// </summary>
        /// <param name="funcName">The simple name of the function, can be null.</param>
        /// <param name="visitor">The current visitor object.</param>
        /// <param name="function">The function to generate for.</param>
        public void ImplementFunction(IParseItemVisitor/*!*/ visitor, FuncDefItem/*!*/ function, string funcName)
        {
            NameItem[] args = function.Arguments.ToArray();
            if (function.InstanceName != null) // TODO: Add ability to capture self.
                args = new[] { new NameItem("self") }.Union(args).ToArray();

            funcName = funcName ?? "<>__" + (_mid++);
            string name = curNest.members.Contains(funcName) ? funcName + "_" + (_mid++) : funcName;
            MethodBuilder mb = curNest.TypeDef.DefineMethod(name, MethodAttributes.Public,
                typeof(MultipleReturn),
                new Type[] { typeof(ILuaEnvironment), typeof(object[]) });
            var gen = mb.GetILGenerator();
            curNest = new NestInfo(curNest, gen, function.FunctionInformation.CapturedLocals,
                function.FunctionInformation.HasNested, function.FunctionInformation.CapturesParrent);

            // arg_2 = E.Runtime.FixArgs(arg_2, {args.Length});
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            gen.Emit(OpCodes.Ldarg_2);
            gen.Emit(OpCodes.Ldc_I4, args.Length);
            gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("FixArgs"));
            gen.Emit(OpCodes.Starg, 2);

            // if this is an instance method, create a BaseAccessor object to help types.
            if (function.InstanceName != null)
            {
                var field = curNest.DefineLocal(new NameItem("base")); // TODO: Add ability to capture base.

                // {nest.ChildInst}.base = new BaseAcccessor(E, arg_2[0]);
                field.StartSet();
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Ldarg_2);
                gen.Emit(OpCodes.Ldc_I4, 0);
                gen.Emit(OpCodes.Ldelem, typeof(object));
                gen.Emit(OpCodes.Newobj, typeof(BaseAccessor).GetConstructor(
                    new[] { typeof(ILuaEnvironment), typeof(object) }));
                field.EndSet();
            }

            for (int i = 0; i < args.Length; i++)
            {
                var field = curNest.DefineLocal(args[i]);

                // {field} = arg_2[{indicies}];
                field.StartSet();
                gen.Emit(OpCodes.Ldarg_2);
                gen.Emit(OpCodes.Ldc_I4, i);
                gen.Emit(OpCodes.Ldelem, typeof(object));
                field.EndSet();
            }

            function.Block.Accept(visitor);

            if (curNest.TypeDef != null)
                curNest.TypeDef.CreateType();

            curNest = curNest.Parrent;
            // push a pointer to the new method onto the stack of the previous nest method
            //   the above line restores the nest to the previous state and this code will
            //   push the new method.
            //! push E.Runtime.CreateFunction( E, {name}, {nest.TypeDef}.GetMethod({name}), {nest.ThisInst != null ? nest.NestInst : arg_0} );
            curNest.Generator.Emit(OpCodes.Ldarg_1);
            curNest.Generator.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
            curNest.Generator.Emit(OpCodes.Ldarg_1);
            curNest.Generator.Emit(OpCodes.Ldstr, name);
            curNest.Generator.Emit(OpCodes.Ldtoken, curNest.TypeDef);
            curNest.Generator.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", new Type[] { typeof(RuntimeTypeHandle) }));
            curNest.Generator.Emit(OpCodes.Ldstr, name);
            curNest.Generator.Emit(OpCodes.Callvirt, typeof(Type).GetMethod("GetMethod", new[] { typeof(string) }));
            if (curNest.ThisInst != null)
                curNest.Generator.Emit(OpCodes.Ldloc, curNest.ThisInst);
            else
                curNest.Generator.Emit(OpCodes.Ldarg_0);
            curNest.Generator.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("CreateFunction"));
        }
        /// <summary>
        /// Searches for a variable with the given name and returns an object
        /// used to get/set it's value.  There are three kinds of variables: 
        /// Local, Captured, and Global.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <returns>An object used to generate code for this variable.</returns>
        public VarDefinition FindVariable(NameItem/*!*/ name)
        {
            // search in the current nest
            var varDef = curNest.FindLocal(name);
            if (varDef != null)
                return varDef;

            // search for parrent captures
            var fields = new List<FieldBuilder>();
            var cur = curNest.Parrent;
            while (cur != null)
            {
                varDef = cur.FindLocal(name);
                if (varDef != null)
                {
                    if (varDef is LocalVarDef)
                        throw new InvalidOperationException();

                    fields.Add(((CapturedVarDef)varDef).field);
                    return new CapturedParVarDef(CurrentGenerator, fields.ToArray());
                }

                fields.Add(cur.ParrentInst);
                cur = cur.Parrent;
            }

            // still not found, it is a global variable
            return new GlobalVarDef(CurrentGenerator, name.Name);
        }
        /// <summary>
        /// Defines a new local variable and returns an object used to get/set 
        /// it's value.  There are two possible variable types: Local and 
        /// Captured.  Which one is chosen depends on whether the variable is 
        /// captured in the FunctionInfo used to create the current function.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <returns>An object used to get/set it's value.</returns>
        public VarDefinition DefineLocal(NameItem/*!*/ name)
        {
            return curNest.DefineLocal(name);
        }

        /// <summary>
        /// Creates a new temporary variable and returns the local used to
        /// use it.  This may also use a variable from the cache.  When the
        /// variable is no longer used, call RemoveTemporary(LocalBuilder).
        /// </summary>
        /// <param name="type">The type of the local variable.</param>
        /// <returns>The local builder that defines the variable.</returns>
        public LocalBuilder CreateTemporary(Type type)
        {
            if (curNest.FreeLocals.ContainsKey(type))
            {
                var temp = curNest.FreeLocals[type];
                if (temp.Count > 0)
                {
                    return temp.Pop();
                }
            }

            return CurrentGenerator.DeclareLocal(type);
        }
        /// <summary>
        /// Creates an array of the given type and stores it in a returned local.
        /// </summary>
        /// <param name="arrayType">The type of the array.</param>
        /// <param name="size">The size of the array.</param>
        /// <returns>A local builder that now contains the array.</returns>
        public LocalBuilder CreateArray(Type arrayType, int size)
        {
            var ret = CreateTemporary(arrayType.MakeArrayType());
            CurrentGenerator.Emit(OpCodes.Ldc_I4, size);
            CurrentGenerator.Emit(OpCodes.Newarr, arrayType);
            CurrentGenerator.Emit(OpCodes.Stloc, ret);

            return ret;
        }
        /// <summary>
        /// Marks the given local variable as not being used anymore.  This
        /// allows the variable to be used again in other code.
        /// </summary>
        /// <param name="local">The local variable that is no longer used.</param>
        public void RemoveTemporary(LocalBuilder local)
        {
            if (!curNest.FreeLocals.ContainsKey(local.LocalType))
                curNest.FreeLocals.Add(local.LocalType, new Stack<LocalBuilder>());

            curNest.FreeLocals[local.LocalType].Push(local);
        }

        #region public interface VariableDefinition

        /// <summary>
        /// A helper interface used to generate code for different variable 
        /// definitions.  There are three scopes: Global, Local, and Captured.
        /// </summary>
        public interface VarDefinition
        {
            /// <summary>
            /// Starts the setting of the variable.  For example, pushing 
            /// 'this' onto the stack.
            /// </summary>
            void StartSet();
            /// <summary>
            /// Ends the setting of the variable.  For example, emitting
            /// OpCodes.Stloc.
            /// </summary>
            void EndSet();
            /// <summary>
            /// Pushes the value of the variable onto the stack.
            /// </summary>
            void Get();
        }

        /// <summary>
        /// Defines a global variable.
        /// </summary>
        sealed class GlobalVarDef : VarDefinition
        {
            ILGenerator gen;
            string name;

            public GlobalVarDef(ILGenerator/*!/*/ gen, string/*!/*/ name)
            {
                this.gen = gen;
                this.name = name;
            }

            public void StartSet()
            {
                // part of:
                // E.Runtime.SetIndex(E, E.GlobalsTable, {name}, value);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_GlobalsTable"));
                gen.Emit(OpCodes.Ldstr, name);
            }
            public void EndSet()
            {
                // end of:
                // E.Runtime.SetIndex(E, E.GlobalsTable, {name}, value);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("SetIndex"));
            }
            public void Get()
            {
                //! push E.Runtime.GetIndex(E, E.GlobalsTable, {name})
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_Runtime"));
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaEnvironment).GetMethod("get_GlobalsTable"));
                gen.Emit(OpCodes.Ldstr, name);
                gen.Emit(OpCodes.Callvirt, typeof(ILuaRuntime).GetMethod("GetIndex"));
            }
        }
        /// <summary>
        /// Defines a local variable definition, one that is only referenced 
        /// by the current function.
        /// </summary>
        sealed class LocalVarDef : VarDefinition
        {
            ILGenerator gen;
            LocalBuilder local;

            public LocalVarDef(ILGenerator/*!*/ gen, LocalBuilder/*!*/ loc)
            {
                this.gen = gen;
                this.local = loc;
            }

            public void StartSet()
            {
                // do nothing.
            }
            public void EndSet()
            {
                // local = {value};
                gen.Emit(OpCodes.Stloc, local);
            }
            public void Get()
            {
                gen.Emit(OpCodes.Ldloc, local);
            }
        }
        /// <summary>
        /// Defines a local variable that has been captured by nested functions
        /// and is stored in a field in a nested type.  This version is used 
        /// for when called from the defining function so only an instance to 
        /// the child type is needed to access the variable.
        /// </summary>
        sealed class CapturedVarDef : VarDefinition
        {
            ILGenerator gen;
            LocalBuilder thisInst;
            public FieldBuilder field;

            public CapturedVarDef(ILGenerator/*!*/ gen, LocalBuilder/*!*/ thisInst, FieldBuilder/*!*/ field)
            {
                this.gen = gen;
                this.thisInst = thisInst;
                this.field = field;
            }

            public void StartSet()
            {
                // part of:
                // thisInst.field = {value}
                gen.Emit(OpCodes.Ldloc, thisInst);
            }
            public void EndSet()
            {
                // part of:
                // thisInst.field = {value}
                gen.Emit(OpCodes.Stfld, field);
            }
            public void Get()
            {
                gen.Emit(OpCodes.Ldloc, thisInst);
                gen.Emit(OpCodes.Ldfld, field);
            }
        }
        /// <summary>
        /// Defines a local variable that has been captured by nested functions
        /// and is stored in a parrent nest.  This version loads the variable 
        /// from parrent nest types.
        /// </summary>
        sealed class CapturedParVarDef : VarDefinition
        {
            ILGenerator gen;
            FieldBuilder[] fields;

            public CapturedParVarDef(ILGenerator/*!*/ gen, FieldBuilder[]/*!*/ fields)
            {
                this.gen = gen;
                this.fields = fields;
            }

            public void StartSet()
            {
                gen.Emit(OpCodes.Ldarg_0);
                for (int i = 0; i < fields.Length - 1; i++)
                    gen.Emit(OpCodes.Ldfld, fields[i]);
            }
            public void EndSet()
            {
                gen.Emit(OpCodes.Stfld, fields[fields.Length - 1]);
            }
            public void Get()
            {
                gen.Emit(OpCodes.Ldarg_0);
                for (int i = 0; i < fields.Length; i++)
                    gen.Emit(OpCodes.Ldfld, fields[i]);
            }
        }

        #endregion
    }
}
