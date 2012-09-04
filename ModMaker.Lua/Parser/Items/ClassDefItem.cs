using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;

namespace ModMaker.Lua.Parser.Items
{
    class ClassDefItem : IParseItem
    {
        List<string> impliments;
        long line, col;

        public ClassDefItem(string name, string[] impliments, long line, long col)
        {
            this.Name = name;
            this.impliments = new List<string>(impliments);
            this.line = line;
            this.col = col;
        }

        public string Name { get; private set; }
        public ParseType Type { get { return ParseType.Statement; } }

        public void Add(string item)
        {
            impliments.Add(item);
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to ClassDefItem.");
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            // Do nothing.
        }
        public void GenerateIL(ChunkBuilderNew cb)
        {
            cb.DefineClass(Name, impliments.ToArray(), line, col);
        }
    }
}
