using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection.Emit;

namespace ModMaker.Lua.Parser
{
    class LabelTree
    {
        public class TreeItem
        {
            public TreeItem Parrent, Child, Previous, Next;
            public string Name;
            public Label? Label;
            public GotoItem Item;
            public bool Passable;

            public TreeItem(TreeItem parrent, TreeItem prev, bool pass)
            {
                this.Parrent = parrent;
                this.Previous = prev;
                this.Passable = pass;
            }
            public TreeItem(TreeItem parrent, TreeItem prev, string name, Label label)
            {
                this.Parrent = parrent;
                this.Previous = prev;
                this.Name = name;
                this.Label = label;
                this.Passable = true;
            }
            public TreeItem(TreeItem parrent, TreeItem prev, string name, GotoItem label)
            {
                this.Parrent = parrent;
                this.Previous = prev;
                this.Name = name;
                this.Item = label;
                this.Passable = true;
            }
        }

        TreeItem cur, _root;

        public LabelTree()
        {
            cur = new TreeItem(null, null, true);
            _root = cur;
        }

        public TreeItem StartBlock(bool pass)
        {
            TreeItem ret = cur;
            TreeItem n = new TreeItem(cur, null, pass);
            cur.Child = n;
            cur = n;
            return ret;
        }
        public void EndBlock(TreeItem item)
        {
            cur = item;
            var n = new TreeItem(null, cur, true);
            cur.Next = n;
            cur = n;
        }

        public void DefineLabel(string name, Label l)
        {
            TreeItem n = new TreeItem(null, cur, name, l);
            cur.Next = n;
            cur = n;
        }
        public void GotoLabel(string name, GotoItem item)
        {
            TreeItem n = new TreeItem(null, cur, name, item);
            cur.Next = n;
            cur = n;
        }

        public void DefineLocal()
        {
            TreeItem n = new TreeItem(cur, null, true);
            cur.Child = n;
            cur = n;
        }

        public void Resolve()
        {
            cur = _root;
            while(true)
            {
                // if item is a goto, locate the respective label.
                if (cur.Item != null)
                {
                    FindLabel(cur.Item);
                }

                // move through the tree to the next item.
                if (cur.Child != null)
                    cur = cur.Child;
                else if (cur.Next != null)
                    cur = cur.Next;
                else
                {
                    // step backwards to parrent
                    while (cur.Previous != null)
                        cur = cur.Previous;
                    if (cur.Parrent != null)
                        cur = cur.Parrent;
                    else
                        break;
                    cur.Child = null;
                }
            }
        }
        void FindLabel(GotoItem item)
        {
            TreeItem c = cur;
            List<TreeItem> found = new List<TreeItem>();
            while (true)
            {
                if (c.Label != null && c.Name == item.Name)
                    found.Add(c);

                if (c.Previous != null)
                    c = c.Previous;
                else
                {
                    if (found.Count == 1)
                    {
                        item.Label = found[0].Label;
                        return;
                    }
                    else if (found.Count > 1)
                        throw new SyntaxException("Multiple valid labels for goto item.", item.Line, item.Col);

                    if (c.Parrent == null || !c.Passable)
                        throw new SyntaxException("Unable to locate label '" + item.Name + "' for goto item.", item.Line, item.Col);
                    c = c.Parrent;

                    // move to the end of the scope.
                    while (c.Next != null)
                        c = c.Next;
                }
            }
        }
    }
}
