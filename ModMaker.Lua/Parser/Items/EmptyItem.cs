using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Parser.Items
{
    class EmptyItem : IParseItem
    {
        public ParseType Type { get { return (ParseType)131071; } }

        public void AddItem(IParseItem item)
        {
            throw new NotImplementedException();
        }
        public void WaitOne()
        {
            throw new NotImplementedException();
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            throw new NotImplementedException();
        }
        public void GenerateILNew(ChunkBuilderNew cb)
        {
            throw new NotImplementedException();
        }
        public bool HasNested()
        {
            throw new NotImplementedException();
        }
    }
}
