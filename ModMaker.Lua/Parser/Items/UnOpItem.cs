<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;
using System.Reflection;

namespace ModMaker.Lua.Parser
{
    enum UnaryOperationType
    {
        Unknown,
        Minus,
        Not,
        Length,
    }

    class UnOpItem : IParseItem
    {
        public UnOpItem(IParseItem target, UnaryOperationType type)
        {
            if ((target.Type & ParseType.Expression) != ParseType.Expression)
                throw new ArgumentException("Target of unary operation must be an expression.");
            this.Target = target;
            this.OperationType = type;
        }

        public IParseItem Target { get; private set; }
        public UnaryOperationType OperationType { get; private set; }
        public ParseType Type { get { return ParseType.Expression; } }

        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to UnOpItem.");
        }
        public void GenerateIL(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;

            // RuntimeHelper.ResolveUnaryOperation({OperationType}, {Target})
            gen.Emit(OpCodes.Ldc_I4, (int)OperationType);
            Target.GenerateIL(eb);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("ResolveUnaryOperation", BindingFlags.Public | BindingFlags.Static));
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            Target.ResolveLabels(cb, tree);
        }
    }
=======
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;
using System.Reflection;

namespace ModMaker.Lua.Parser.Items
{
    enum UnaryOperationType
    {
        Unknown,
        Minus,
        Not,
        Length,
    }

    class UnOpItem : IParseItem
    {
        public UnOpItem(IParseItem target, UnaryOperationType type)
        {
            if ((target.Type & ParseType.Expression) != ParseType.Expression)
                throw new ArgumentException("Target of unary operation must be an expression.");
            this.Target = target;
            this.OperationType = type;
        }

        public IParseItem Target { get; private set; }
        public UnaryOperationType OperationType { get; private set; }
        public ParseType Type { get { return ParseType.Expression; } }

        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to UnOpItem.");
        }
        public void GenerateIL(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;

            // RuntimeHelper.ResolveUnaryOperation({OperationType}, {Target})
            gen.Emit(OpCodes.Ldc_I4, (int)OperationType);
            Target.GenerateIL(eb);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("ResolveUnaryOperation", BindingFlags.Public | BindingFlags.Static));
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            Target.ResolveLabels(cb, tree);
        }
    }
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
}