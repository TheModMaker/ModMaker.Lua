// Copyright 2014 Jacob Trimble
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Compiler {
  /// <summary>
  /// A helper class for GetInfoVisitor.  This is a tree structure that holds local variables and
  /// labels.
  /// </summary>
  sealed class GetInfoTree {
    /// <summary>
    /// A node in the tree.  This represents a block of code. It contains local definitions and
    /// labels that need to be resolved.  When a local variable is defined, it gets a new child
    /// because labels cannot be accessed across local definitions.
    /// </summary>
    sealed class TreeNode {
      /// <summary>
      /// Creates a new node with the given parent node.
      /// </summary>
      public TreeNode(TreeNode parent = null, bool passable = true) {
        if (parent != null) {
          parent.Children.Add(this);
        }

        Parent = parent;
        Passable = passable;
        IsFunction = false;
      }

      /// <summary>
      /// Contains the parent node or null if it is the root of the tree.
      /// </summary>
      public readonly TreeNode Parent;
      /// <summary>
      /// Contains the children of this block.
      /// </summary>
      public readonly IList<TreeNode> Children = new List<TreeNode>();

      /// <summary>
      /// True if labels are visible across this node. It is true for all Lua blocks like 'if' and
      /// 'while' and is false for functions and local definitions.
      /// </summary>
      public readonly bool Passable;
      /// <summary>
      /// Contains the labels that are defined in this block.
      /// </summary>
      public readonly IList<LabelItem> Labels = new List<LabelItem>();
      /// <summary>
      /// Contains the Goto items that are defined in this block.
      /// </summary>
      public readonly IList<GotoItem> GotoItems = new List<GotoItem>();

      /// <summary>
      /// True if this node is a nested function.
      /// </summary>
      public bool IsFunction = false;
      /// <summary>
      /// Contains the local definitions for this block.  These are not accessed by nested
      /// functions.
      /// </summary>
      public readonly IDictionary<string, NameItem> TrueLocals = new Dictionary<string, NameItem>();
      /// <summary>
      /// Contains the locals that are captured by nested functions.
      /// </summary>
      public readonly IDictionary<string, NameItem> CapturedLocals =
          new Dictionary<string, NameItem>();
      /// <summary>
      /// True if this block contains nested functions.
      /// </summary>
      public bool HasNested {
        get {
          foreach (var item in Children) {
            if (item.IsFunction || item.HasNested) {
              return true;
            }
          }

          return false;
        }
      }
      /// <summary>
      /// True if this nest captures variables from a parent nested function.  This does not apply
      /// to parent nodes but to nested functions.
      /// </summary>
      public bool CapturesParent;
    }

    TreeNode _cur;
    readonly TreeNode _root;

    /// <summary>
    /// Creates a new empty tree.
    /// </summary>
    public GetInfoTree() {
      _root = _cur = new TreeNode(null, false);
    }

    /// <summary>
    /// Resolves any GotoItems with their correct LabelItem's. This should be called after the whole
    /// tree has been generated.
    /// </summary>
    public void Resolve() {
      _resolve(_root);
    }

    /// <summary>
    /// Starts a new local block and returns an object that ends the block when Dispose is called.
    /// </summary>
    /// <param name="passable">True if labels from parent nodes are visible in this node.</param>
    /// <returns>An object that will end the block when Dispose is called.</returns>
    public IDisposable Block(bool passable) {
      var temp = _cur;
      _cur = new TreeNode(_cur, passable);
      return Helpers.Disposable(() => { _cur = temp; });
    }
    /// <summary>
    /// Called when a local variable is defined.
    /// </summary>
    /// <param name="names">The name(s) of the local variables.</param>
    public void DefineLocal(IEnumerable<NameItem> names) {
      _cur = new TreeNode(_cur, false);
      foreach (var item in names) {
        _cur.TrueLocals[item.Name] = item;
      }
    }

    /// <summary>
    /// Defines a new label in the current block.
    /// </summary>
    /// <param name="label">The label to define.</param>
    public void DefineLabel(LabelItem label) {
      foreach (var item in _cur.Labels) {
        if (item.Name == label.Name) {
          throw new ArgumentException(string.Format(Resources.LabelAlreadyDefined, item.Name));
        }
      }

      _cur.Labels.Add(label);
    }
    /// <summary>
    /// Defines a new GotoItem in the current block.
    /// </summary>
    /// <param name="item">The item to define.</param>
    public void DefineGoto(GotoItem item) {
      _cur.GotoItems.Add(item);
    }

    /// <summary>
    /// Called when get/set the value of a variable, determines whether the given variable is a
    /// capture from a parent nested function.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    public void GetName(NameItem name) {
      bool inFunc = true;
      TreeNode node = _cur;
      while (node != null) {
        if (node.CapturedLocals.ContainsKey(name.Name) || node.TrueLocals.ContainsKey(name.Name)) {
          // Ignore the local if it is the current function
          if (!inFunc) {
            // If it is in TrueLocals, move it to CapturedLocals.
            if (node.TrueLocals.TryGetValue(name.Name, out NameItem boundItem)) {
              node.TrueLocals.Remove(name.Name);
              node.CapturedLocals.Add(name.Name, boundItem);
            }

            // Update all the CapturesParent for any nodes between the current node and the node
            // that defines the local.
            TreeNode cur2 = _cur;
            while (cur2 != node) {
              cur2.CapturesParent = true;
              cur2 = cur2.Parent;
            }
          }
          return;
        }
        if (node.IsFunction) {
          inFunc = false;
        }

        node = node.Parent;
      }
    }
    /// <summary>
    /// Defines a nested function and returns an object that restores the tree when Dispose is
    /// called.  After the end of the function, EndFunc should be called.
    /// </summary>
    public IDisposable DefineFunc() {
      var temp = _cur = new TreeNode(_cur, false) { IsFunction = true };
      return Helpers.Disposable(() => { _cur = temp; });
    }
    /// <summary>
    /// Ends a function definition and returns the FunctionInfo for the function.  This should be
    /// called after the call to Dispose on the object returned from DefineFunc.
    /// </summary>
    /// <returns>The FunctionInfo for the function.</returns>
    public FuncDefItem.FunctionInfo EndFunc() {
      var info = new FuncDefItem.FunctionInfo();
      var names = new List<NameItem>();
      _getNames(_cur, names);

      info.CapturedLocals = names.ToArray();
      info.CapturesParent = _cur.CapturesParent;
      info.HasNested = _cur.HasNested;

      _cur = _cur.Parent;
      return info;
    }

    /// <summary>
    /// Recursively gets the names of the captured locals defined in the given root node.
    /// </summary>
    /// <param name="root">The node to start the search.</param>
    /// <param name="names">Where to put the names.</param>
    static void _getNames(TreeNode root, List<NameItem> names) {
      if (root != null) {
        names.AddRange(root.CapturedLocals.Select(k => k.Value));
        foreach (var item in root.Children) {
          // Ignore locals defined in nested functions.
          if (!item.IsFunction) {
            _getNames(item, names);
          }
        }
      }
    }
    /// <summary>
    /// Recursively resolves any GotoItems to their correct LabelItem or throws an exception.
    /// </summary>
    /// <param name="root">The current root node.</param>
    /// <exception cref="ModMaker.Lua.Parser.SyntaxException">
    /// If a label could not be resolved.
    /// </exception>
    static void _resolve(TreeNode root) {
      if (root != null) {
        foreach (var item in root.GotoItems) {
          _resolveGoto(root, item);
        }

        foreach (var item in root.Children) {
          _resolve(item);
        }
      }
    }
    /// <summary>
    /// Resolves a single GotoItem by traversing up the tree from the given root.  Throws an
    /// exception if the label cannot be found.
    /// </summary>
    /// <param name="root">The node to start the search.</param>
    /// <param name="item">The item to resolve.</param>
    static void _resolveGoto(TreeNode root, GotoItem item) {
      do {
        foreach (var label in root.Labels) {
          if (label.Name == item.Name) {
            item.Target = label;
            return;
          }
        }
        // Break statements can pass through local definitions
      } while ((item.Name != "<break>" || !root.IsFunction) &&
               (item.Name == "<break>" || root.Passable) &&
               (root = root.Parent) != null);

      throw new SyntaxException(string.Format(Resources.LabelNotFound, item.Name), item.Debug);
    }
  }
}
