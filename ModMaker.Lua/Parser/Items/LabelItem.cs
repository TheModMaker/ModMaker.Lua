using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;
using System.IO;

namespace ModMaker.Lua.Parser
{
    class LabelItem : IParseItem
    {
        string name;
        Label? label;

        public LabelItem(string name)
        {
            this.name = name;
            this.label = null;
        }

        public ParseType Type { get { return ParseType.Statement; } }

        public void GenerateIL(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            if (label == null)
                throw new InvalidOperationException("Must call IParseItem.ResolveLabels before calling IParseItem.GenerateIL.");
            gen.MarkLabel(label.Value);
        }
        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to LabelItem.");
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            label = cb.CurrentGenerator.DefineLabel();
            tree.DefineLabel(name, label.Value);
        }
    }
}