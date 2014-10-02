using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModMaker.Lua.Parser.Items;
using ModMaker.Lua.Parser;

namespace ModMaker.Lua.Compiler
{
    /// <summary>
    /// A helper class for GetInfoVisitor.  This is a tree structure that holds
    /// local variables and labels.
    /// </summary>
    sealed class GetInfoTree
    {
        /// <summary>
        /// A node in the tree.  This represents a block of code. It contains 
        /// local definitions and lables that need to be resolved.  When a 
        /// local variable is defined, it gets a new child because labels 
        /// cannot be accessed across local definitions.
        /// </summary>
        sealed class TreeNode
        {
            bool captures = false;

            /// <summary>
            /// Creates a new node with the given parrent node.
            /// </summary>
            public TreeNode(TreeNode parrent = null, bool passable = true)
            {
                if (parrent != null)
                    parrent.Children.Add(this);
                this.Parrent = parrent;
                this.Children = new List<TreeNode>();
                this.Passable = passable;
                this.Labels = new List<LabelItem>();
                this.GotoItems = new List<GotoItem>();
                this.TrueLocals = new Dictionary<string, NameItem>();
                this.CapturedLocals = new Dictionary<string, NameItem>();
                this.IsFunction = false;
            }

            /// <summary>
            /// Contains the parrent node or null if it is the root of the tree.
            /// </summary>
            public TreeNode Parrent;
            /// <summary>
            /// Contains the children of this block.
            /// </summary>
            public List<TreeNode> Children;

            /// <summary>
            /// True if labels are visible across this node. It is true for all
            /// Lua blocks like 'if' and 'while' and is false for functions and
            /// local definitions.
            /// </summary>
            public bool Passable;
            /// <summary>
            /// Contains the labels that are defined in this block.
            /// </summary>
            public List<LabelItem> Labels;
            /// <summary>
            /// Contains the Goto items that are defined in this block.
            /// </summary>
            public List<GotoItem> GotoItems;

            /// <summary>
            /// True if this node is a nested function.
            /// </summary>
            public bool IsFunction;
            /// <summary>
            /// Contains the local definitions for this block.  These are not 
            /// accessed by nested functions.
            /// </summary>
            public Dictionary<string, NameItem> TrueLocals;
            /// <summary>
            /// Contains the locals that are captured by nested functions.
            /// </summary>
            public Dictionary<string, NameItem> CapturedLocals;
            /// <summary>
            /// True if this block contains nested functions.
            /// </summary>
            public bool HasNested
            {
                get
                {
                    foreach (var item in Children)
                        if (item.IsFunction || item.HasNested)
                            return true;

                    return false;
                }
            }
            /// <summary>
            /// True if this nest captures variables from a parrent nested 
            /// function.  This does not apply to parrent nodes but to nested
            /// functions.
            /// </summary>
            public bool CapturesParrent { get { return captures; } set { captures = value; } }
        }

        TreeNode cur, root;

        /// <summary>
        /// Creates a new empty tree.
        /// </summary>
        public GetInfoTree()
        {
            this.root = this.cur = new TreeNode(null, false);
        }

        /// <summary>
        /// Resolves any GotoItems with their correct LabelItem's. This should 
        /// be called after the whole tree has been generated.
        /// </summary>
        public void Resolve()
        {
            Resolve(root);
        }

        /// <summary>
        /// Starts a new local block and returns an object that ends the block 
        /// when Dispose is called.
        /// </summary>
        /// <param name="passable">True if labels from parrent nodes are 
        /// visible in this node.</param>
        /// <returns>An object that will end the block when Dispose is called.</returns>
        public IDisposable Block(bool passable)
        {
            var temp = cur;
            cur = new TreeNode(cur, passable);
            return Helpers.Disposable(() => { cur = temp; });
        }
        /// <summary>
        /// Called when a local variable is defined.
        /// </summary>
        /// <param name="names">The name(s) of the local variables.</param>
        public void DefineLocal(IEnumerable<NameItem>/*!*/ names)
        {
            cur = new TreeNode(cur, false);
            foreach (var item in names)
                cur.TrueLocals.Add(item.Name, item);
        }

        /// <summary>
        /// Defines a new label in the current block.
        /// </summary>
        /// <param name="label">The label to define.</param>
        public void DefineLabel(LabelItem/*!*/ label)
        {
            foreach (var item in cur.Labels)
            {
                if (item.Name == label.Name)
                    throw new ArgumentException(string.Format(Resources.LabelAlreadyDefined, item.Name));
            }

            cur.Labels.Add(label);
        }
        /// <summary>
        /// Defines a new GotoItem in the current block.
        /// </summary>
        /// <param name="item">The item to define.</param>
        public void DefineGoto(GotoItem/*!*/ item)
        {
            cur.GotoItems.Add(item);
        }

        /// <summary>
        /// Called when get/set the value of a variable, determines whether the
        /// given variable is a capture from a parrent nested function.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        public void GetName(NameItem/*!*/ name)
        {
            bool inFunc = true; // true if cur is in the current function.
            TreeNode node = cur;
            while (node != null)
            {
                if (node.CapturedLocals.ContainsKey(name.Name) ||
                    node.TrueLocals.ContainsKey(name.Name))
                {
                    // ignore the local if it is the current function
                    if (!inFunc)
                    {
                        // if it is in TrueLocals, move it to CapturedLocals.
                        NameItem boundItem;
                        if (node.TrueLocals.TryGetValue(name.Name, out boundItem))
                        {
                            node.TrueLocals.Remove(name.Name);
                            node.CapturedLocals.Add(name.Name, boundItem);
                        }

                        // update all the CapturesParrent for any nodes between
                        //   the current node and the node that defines the local.
                        TreeNode cur2 = cur;
                        while (cur2 != node)
                        {
                            cur2.CapturesParrent = true;
                            cur2 = cur2.Parrent;
                        }
                    }
                    return;
                }
                if (node.IsFunction)
                    inFunc = false;
                node = node.Parrent;
            }
        }
        /// <summary>
        /// Defines a nested function and returns an object that restores the 
        /// tree when Dispose is called.  After the end of the function,
        /// EndFunc should be called.
        /// </summary>
        public IDisposable DefineFunc()
        {
            var temp = cur = new TreeNode(cur, false) { IsFunction = true };
            return Helpers.Disposable(() => { cur = temp; });
        }
        /// <summary>
        /// Ends a function definition and returns the FunctionInfo for the 
        /// function.  This should be called after the call to Dispose on the
        /// object returned from DefineFunc.
        /// </summary>
        /// <returns>The FunctionInfo for the function.</returns>
        public FuncDefItem.FunctionInfo EndFunc()
        {
            var info = new FuncDefItem.FunctionInfo();
            var names = new List<NameItem>();
            GetNames(cur, names);

            info.CapturedLocals = names.ToArray();
            info.CapturesParrent = cur.CapturesParrent;
            info.HasNested = cur.HasNested;

            cur = cur.Parrent;
            return info;
        }

        /// <summary>
        /// Recursively gets the names of the captured locals defined in the 
        /// given root node.
        /// </summary>
        /// <param name="root">The node to start the search.</param>
        /// <param name="names">Where to put the names.</param>
        static void GetNames(TreeNode root, List<NameItem>/*!*/ names)
        {
            if (root != null)
            {
                names.AddRange(root.CapturedLocals.Select(k => k.Value));
                foreach (var item in root.Children)
                {
                    // ignore locals defined in nested functions.
                    if (!item.IsFunction)
                        GetNames(item, names);
                }
            }
        }
        /// <summary>
        /// Recursively resolves any GotoItems to their correct LabelItem or 
        /// throws an exception.
        /// </summary>
        /// <param name="root">The current root node.</param>
        /// <exception cref="ModMaker.Lua.Parser.SyntaxException">If a label 
        /// cound not be resolved.</exception>
        static void Resolve(TreeNode root)
        {
            if (root != null)
            {
                foreach (var item in root.GotoItems)
                    ResolveGoto(root, item);

                foreach (var item in root.Children)
                    Resolve(item);
            }
        }
        /// <summary>
        /// Resolves a single GotoItem by traversing up the tree from the given 
        /// root.  Throws an exception if the label cannot be found.
        /// </summary>
        /// <param name="root">The node to start the search.</param>
        /// <param name="item">The item to resolve.</param>
        static void ResolveGoto(TreeNode/*!*/ root, GotoItem/*!*/ item)
        {
            do
            {
                foreach (var label in root.Labels)
                {
                    if (label.Name == item.Name)
                    {
                        item.Target = label;
                        return;
                    }
                }
            }
            // break statements can pass through local definitions
            while ((item.Name != "<break>" || !root.IsFunction) &&
                   (item.Name == "<break>" || root.Passable) &&
                   (root = root.Parrent) != null);

            throw new SyntaxException(string.Format(Resources.LabelNotFound, item.Name), item.Debug);
        }
    }
}
