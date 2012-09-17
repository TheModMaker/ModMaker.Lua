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
    class LiteralItem : IParseItem
    {
        public LiteralItem(object item)
        {
            if (!(item is bool || item is double || item is string || item == null))
                throw new InvalidOperationException("A literal must be of type bool, double, string, or be null.");
            this.Item = item;
        }

        public ParseType Type { get { return ParseType.Expression; } }
        public object Item { get; private set; }

        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to LiteralItem.");
        }
        public void GenerateIL(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            if (Item == null)
                gen.Emit(OpCodes.Ldnull);
            else if (Item as bool? == false)
            {
                gen.Emit(OpCodes.Ldc_I4_0);
                gen.Emit(OpCodes.Box, typeof(bool));
            }
            else if (Item as bool? == true)
            {
                gen.Emit(OpCodes.Ldc_I4_1);
                gen.Emit(OpCodes.Box, typeof(bool));
            }
            else if (Item is double)
            {
                gen.Emit(OpCodes.Ldc_R8, Item as double? ?? 0);
                gen.Emit(OpCodes.Box, typeof(double));
            }
            else if (Item is string)
                gen.Emit(OpCodes.Ldstr, Item as string);
            else
                throw new InvalidOperationException("A literal must be of type bool, double, string, or be null.");
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
using ModMaker.Lua.Runtime;
using System.Reflection;

namespace ModMaker.Lua.Parser.Items
{
    class LiteralItem : IParseItem
    {
        public LiteralItem(object item)
        {
            if (!(item is bool || item is double || item is string || item == null))
                throw new InvalidOperationException("A literal must be of type bool, double, string, or be null.");
            this.Item = item;
        }

        public ParseType Type { get { return ParseType.Expression; } }
        public object Item { get; private set; }

        public void AddItem(IParseItem item)
        {
            throw new NotSupportedException("Cannot add items to LiteralItem.");
        }
        public void GenerateIL(ChunkBuilderNew eb)
        {
            ILGenerator gen = eb.CurrentGenerator;
            if (Item == null)
                gen.Emit(OpCodes.Ldnull);
            else if (Item as bool? == false)
            {
                gen.Emit(OpCodes.Ldc_I4_0);
                gen.Emit(OpCodes.Box, typeof(bool));
            }
            else if (Item as bool? == true)
            {
                gen.Emit(OpCodes.Ldc_I4_1);
                gen.Emit(OpCodes.Box, typeof(bool));
            }
            else if (Item is double)
            {
                gen.Emit(OpCodes.Ldc_R8, Item as double? ?? 0);
                gen.Emit(OpCodes.Box, typeof(double));
            }
            else if (Item is string)
                gen.Emit(OpCodes.Ldstr, Item as string);
            else
                throw new InvalidOperationException("A literal must be of type bool, double, string, or be null.");
        }
        public void ResolveLabels(ChunkBuilderNew cb, LabelTree tree)
        {
            // Do nothing.
        }
    }
}
>>>>>>> ca31a2f4607b904d0d7876c07b13afac67d2736e
