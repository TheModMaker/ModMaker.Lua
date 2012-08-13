using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection.Emit;

namespace ModMaker.Lua.Parser.Items
{
    class AsyncItem : IParseItem
    {
        Thread _thread;
        string _chunk;
        IParseItem _expression;

        public AsyncItem(string chunk)
        {
            this._chunk = chunk;
            _thread = new Thread(Do);
            _thread.Start();
        }

        public IParseItem Item { get { lock (this) return _expression; } }
        public ParseType Type { get { throw new NotSupportedException("Cannot get the Type of an AsyncItem."); } }

        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to an AsyncItem.");
        }
        public void WaitOne()
        {
            _thread.Join();
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            throw new NotSupportedException("Must call IParseItem.WaitOne() before IParseItem.ResolveLabels().");
        }
        public void GenerateILNew(ChunkBuilderNew cb)
        {
            throw new NotSupportedException("Cannot generate IL for an AsyncItem.");
        }
        public bool HasNested()
        {
            throw new NotSupportedException("Cannot check for nested functions for an AsyncItem.");
        }

        void Do()
        {
            using (var c = new CharDecorator(_chunk))
            {
                IParseItem ret = new PlainParser(c).LoadFunc();
                lock (this)
                    this._expression = ret;
            }
        }
    }
}
