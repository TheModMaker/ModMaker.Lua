using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;

namespace ModMaker.Lua.Parser.Items
{
    class GotoItem : IParseItem
    {
        public GotoItem(string label, long line, long col)
        {
            this.Name = label;
            this.Line = line;
            this.Col = col;
        }

        public ParseType Type { get { return ParseType.Statement; } }
        public Label? Label { get; set; }
        public string Name { get; private set; }
        public long Line { get; private set; }
        public long Col { get; private set; }

        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to GotoItem.");
        }
        public void GenerateILNew(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            if (Label == null)
                throw new InvalidOperationException("Must call IParseItem.ResolveLabels before calling IParseItem.GenerateIL.");
            gen.Emit(OpCodes.Br, Label.Value);
        }
        public void WaitOne()
        {
            // Do nothing
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            tree.GotoLabel(Name, this);
        }
        public bool HasNested()
        {
            return false;
        }
    }
}
