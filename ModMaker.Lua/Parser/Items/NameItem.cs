<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;

namespace ModMaker.Lua.Parser
{
    class NameItem : IParseItem
    {
        public NameItem(string name)
        {
            this.Name = name;
        }

        public ParseType Type { get { return ParseType.Variable | ParseType.NameList | ParseType.FuncName | ParseType.PrefixExp | ParseType.Expression; } }
        public string Name { get; set; }
        public bool Set { get; set; }

        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to NameItem.");
        }
        public void GenerateIL(ChunkBuilderNew eb)
        {
            eb.ResolveName(Name, Set);
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            // Do nothing.
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;

namespace ModMaker.Lua.Parser.Items
{
    class NameItem : IParseItem
    {
        public NameItem(string name)
        {
            this.Name = name;
        }

        public ParseType Type { get { return ParseType.Variable | ParseType.NameList | ParseType.FuncName | ParseType.PrefixExp | ParseType.Expression; } }
        public string Name { get; set; }
        public bool Set { get; set; }

        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to NameItem.");
        }
        public void GenerateIL(ChunkBuilderNew eb)
        {
            eb.ResolveName(Name, Set);
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            // Do nothing.
        }
    }
}
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
