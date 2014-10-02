<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser
{
    class FuncDef : IParseItem
    {
        IParseItem _name;
        string f_name;
        List<string> _args;
        long line, col;
        ILGenerator gen = null;
        object nest = null;

        public FuncDef(IParseItem name, long line, long col, bool local = false)
        {
            this._name = name;
            this._args = new List<string>();
            this.Local = local;
            this.line = line;
            this.col = col;
        }

        public bool Local { get; private set; }
        public string InstanceName { get; set; }
        public IParseItem Block { get; set; }
        public ParseType Type { get { return ParseType.Statement | ParseType.Expression; } }

        public void GenerateIL(ChunkBuilderNew eb)
        {
            string name = null;
            if (Local)
            {
                if (InstanceName != null)
                    throw new SyntaxException("Cannot define instance methods for local methods.", line, col);

                if (_name is NameItem)
                    eb.DefineLocal(name = (_name as NameItem).Name, true);
                else
                    throw new SyntaxException("Cannot use indexers(.) in local definition.", line, col);
            }
            else if (_name != null)
            {
                if (InstanceName != null)
                {
                    // loc = RuntimeHelper.Indexer({_E}, {_name}, {InstanceName})
                    LocalBuilder loc = eb.CurrentGenerator.DeclareLocal(typeof(object));
                    eb.PushEnv();
                    _name.GenerateIL(eb);
                    eb.CurrentGenerator.Emit(OpCodes.Ldstr, InstanceName);
                    eb.CurrentGenerator.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("Indexer"));
                    eb.CurrentGenerator.Emit(OpCodes.Stloc, loc);

                    //! push loc
                    eb.CurrentGenerator.Emit(OpCodes.Ldloca, loc);

                    if (_name is NameItem)
                        name = (_name as NameItem).Name;
                    else
                        name = ((_name as IndexerItem).Expression as LiteralItem).Item as string;
                    name += ":" + InstanceName;
                }
                else
                {
                    if (_name is NameItem)
                        (_name as NameItem).Set = true;
                    else
                        (_name as IndexerItem).Set = true;

                    _name.GenerateIL(eb);
                    if (_name is NameItem)
                        name = (_name as NameItem).Name;
                    else
                        name = ((_name as IndexerItem).Expression as LiteralItem).Item as string;
                }
            }
            eb.ImplimentFunc(Block, _args.ToArray(), name, InstanceName != null, gen, nest, f_name);
            if (_name != null)
                eb.CurrentGenerator.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("SetValue"));
        }
        public void AddParam(string arg)
        {
            _args.Add(arg);
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to FuncDef.");
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            string name;
            if (InstanceName != null)
                name = InstanceName;
            else if (_name == null)
                name = null;
            else if (_name is NameItem)
                name = (_name as NameItem).Name;
            else
                name = ((_name as IndexerItem).Expression as NameItem).Name;

            if (Local)
                tree.DefineLocal();
            
            var b = tree.StartBlock(false);
                cb.DefineFunc(name, out f_name, out nest);
                gen = cb.CurrentGenerator;
                Block.ResolveLabels(cb, tree);
            tree.EndBlock(b);
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;

namespace ModMaker.Lua.Parser.Items
{
    class FuncDef : IParseItem
    {
        IParseItem _name;
        string f_name;
        List<string> _args;
        long line, col;
        ILGenerator gen = null;
        object nest = null;

        public FuncDef(IParseItem name, long line, long col, bool local = false)
        {
            this._name = name;
            this._args = new List<string>();
            this.Local = local;
            this.line = line;
            this.col = col;
        }

        public bool Local { get; private set; }
        public string InstanceName { get; set; }
        public IParseItem Block { get; set; }
        public ParseType Type { get { return ParseType.Statement | ParseType.Expression; } }

        public void GenerateIL(ChunkBuilderNew eb)
        {
            string name = null;
            if (Local)
            {
                if (InstanceName != null)
                    throw new SyntaxException("Cannot define instance methods for local methods.", line, col);

                if (_name is NameItem)
                    eb.DefineLocal(name = (_name as NameItem).Name, true);
                else
                    throw new SyntaxException("Cannot use indexers(.) in local definition.", line, col);
            }
            else if (_name != null)
            {
                if (InstanceName != null)
                {
                    // loc = RuntimeHelper.Indexer({_E}, {_name}, {InstanceName})
                    LocalBuilder loc = eb.CurrentGenerator.DeclareLocal(typeof(object));
                    eb.PushEnv();
                    _name.GenerateIL(eb);
                    eb.CurrentGenerator.Emit(OpCodes.Ldstr, InstanceName);
                    eb.CurrentGenerator.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("Indexer"));
                    eb.CurrentGenerator.Emit(OpCodes.Stloc, loc);

                    //! push loc
                    eb.CurrentGenerator.Emit(OpCodes.Ldloca, loc);

                    if (_name is NameItem)
                        name = (_name as NameItem).Name;
                    else
                        name = ((_name as IndexerItem).Expression as LiteralItem).Item as string;
                    name += ":" + InstanceName;
                }
                else
                {
                    if (_name is NameItem)
                        (_name as NameItem).Set = true;
                    else
                        (_name as IndexerItem).Set = true;

                    _name.GenerateIL(eb);
                    if (_name is NameItem)
                        name = (_name as NameItem).Name;
                    else
                        name = ((_name as IndexerItem).Expression as LiteralItem).Item as string;
                }
            }
            eb.ImplimentFunc(Block, _args.ToArray(), name, InstanceName != null, gen, nest, f_name);
            if (_name != null)
                eb.CurrentGenerator.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("SetValue"));
        }
        public void AddParam(string arg)
        {
            _args.Add(arg);
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to FuncDef.");
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            string name;
            if (InstanceName != null)
                name = InstanceName;
            else if (_name == null)
                name = null;
            else if (_name is NameItem)
                name = (_name as NameItem).Name;
            else
                name = ((_name as IndexerItem).Expression as NameItem).Name;

            if (Local)
                tree.DefineLocal();
            
            var b = tree.StartBlock(false);
                cb.DefineFunc(name, out f_name, out nest);
                gen = cb.CurrentGenerator;
                Block.ResolveLabels(cb, tree);
            tree.EndBlock(b);
        }
    }
}
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
