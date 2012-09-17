using System;

namespace ModMaker.Lua.Parser
{
    [Flags]
    enum ParseType : int
    {
        Unknown = 0,
        Block = 1 << 1,
        Statement = 1 << 2,
        FuncName = 1 << 3,
        VarList = 1 << 4,
        Variable = 1 << 5,
        NameList = 1 << 6,
        ExpList = 1 << 7,
        Expression = 1 << 8,
        PrefixExp = 1 << 9,
        FuncCall = 1 << 10,
        Args = 1 << 11,
        FuncDef = 1 << 12,
        FuncBody = 1 << 13,
        ParList = 1 << 14,
        TableCtor = 1 << 15,
        FieldList = 1 << 16,
        Field = 1 << 17,
    }
    
    interface IParseItem
    {
        ParseType Type { get; }

        void GenerateIL(ChunkBuilderNew cb);
        void AddItem(IParseItem item);
        void ResolveLabels(ChunkBuilderNew cb, LabelTree tree);
    }
}