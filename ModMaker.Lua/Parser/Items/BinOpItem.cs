using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using ModMaker.Lua.Runtime;
using System.Reflection;

namespace ModMaker.Lua.Parser.Items
{
    enum BinaryOperationType
    {
        Unknown,
        Add,
        Subtract,
        Multiply,
        Divide,
        Power,
        Modulo,
        Concat,
        Gt,
        Lt,
        Gte,
        Lte,
        Equals,
        NotEquals,
        And,
        Or,
    }

    class BinOpItem : IParseItem
    {
        public BinOpItem(IParseItem lhs, BinaryOperationType type)
        {
            if ((lhs.Type & ParseType.Expression) != ParseType.Expression)
                throw new ArgumentException("Lhs of binary operation must be an expression.");
            this.Lhs = lhs;
            this.OperationType = type;
        }

        public IParseItem Lhs { get; private set; }
        public IParseItem Rhs { get; set; }
        public BinaryOperationType OperationType { get; private set; }
        public ParseType Type { get { return ParseType.Expression; } }

        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to BinOpItem.");
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            Lhs.ResolveLabels(cb, tree);
            Rhs.ResolveLabels(cb, tree);
        }
        public void GenerateIL(ChunkBuilderNew cb)
        {
            ILGenerator gen = cb.CurrentGenerator;

            //! push RuntimeHelper.ResolveBinaryOperation({Lhs}, {OperationType}, {Rhs})
            Lhs.GenerateIL(cb);
            gen.Emit(OpCodes.Ldc_I4, (int)OperationType);
            Rhs.GenerateIL(cb);
            gen.Emit(OpCodes.Call, typeof(RuntimeHelper).GetMethod("ResolveBinaryOperation", BindingFlags.Public | BindingFlags.Static));
        }
    }
}