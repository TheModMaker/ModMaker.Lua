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
using System.Linq;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Compiler {
  /// <summary>
  /// A visitor that resolves gotos and breaks and also get capture information about function
  /// definitions.  This information is stored in the given item. This is used by the default
  /// compiler.
  /// </summary>
  public sealed class GetInfoVisitor : IParseItemVisitor {
    GetInfoTree _tree;

    internal NameItem[] _globalCaptures;
    internal bool _globalNested;

    /// <summary>
    /// Creates a new instance of GetInfoVisitor.
    /// </summary>
    public GetInfoVisitor() {
      _tree = new GetInfoTree();
    }

    /// <summary>
    /// Resolves all the labels and updates the information in the given IParseItem tree.
    /// </summary>
    /// <param name="target">The IParseItem tree to traverse.</param>
    /// <exception cref="System.ArgumentNullException">If target is null.</exception>
    public void Resolve(IParseItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      _tree = new GetInfoTree();
      target.Accept(this);
      _tree.Resolve();

      var info = _tree.EndFunc();
      _globalCaptures = info.CapturedLocals;
      _globalNested = info.HasNested;
    }

    public IParseItem Visit(BinOpItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      target.Lhs.Accept(this);
      target.Rhs.Accept(this);
      return target;
    }
    public IParseItem Visit(BlockItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      using (_tree.Block(true)) {
        foreach (var item in target.Children) {
          item.Accept(this);
        }

        if (target.Return != null) {
          target.Return.Accept(this);
        }
      }
      return target;
    }
    public IParseItem Visit(ClassDefItem target) {
      // Do nothing.
      return target;
    }
    public IParseItem Visit(ForGenItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      using (_tree.Block(true)) {
        _tree.DefineLocal(target.Names);
        _tree.DefineLabel(target.Break);

        target.Block.Accept(this);
        foreach (var item in target.Expressions) {
          item.Accept(this);
        }
      }

      return target;
    }
    public IParseItem Visit(ForNumItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      using (_tree.Block(true)) {
        _tree.DefineLocal(new[] { target.Name });
        _tree.DefineLabel(target.Break);

        target.Block.Accept(this);
        if (target.Start != null) {
          target.Start.Accept(this);
        }

        if (target.Limit != null) {
          target.Limit.Accept(this);
        }

        if (target.Step != null) {
          target.Step.Accept(this);
        }
      }
      return target;
    }
    public IParseItem Visit(FuncCallItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      foreach (var item in target.Arguments) {
        item.Expression.Accept(this);
      }

      target.Prefix.Accept(this);

      return target;
    }
    public IParseItem Visit(FuncDefItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      if (target.Local) {
        _tree.DefineLocal(new[] { target.Prefix as NameItem });
      }

      using (_tree.DefineFunc()) {
        _tree.DefineLocal(target.Arguments);
        target.Block.Accept(this);
      }
      target.FunctionInformation = _tree.EndFunc();

      return target;
    }
    public IParseItem Visit(GotoItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      _tree.DefineGoto(target);
      return target;
    }
    public IParseItem Visit(IfItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      target.Expression.Accept(this);

      using (_tree.Block(true)) {
        target.Block.Accept(this);
      }

      for (int i = 0; i < target.Elses.Count; i++) {
        using (_tree.Block(true)) {
          target.Elses[i].Expression.Accept(this);
          target.Elses[i].Block.Accept(this);
        }
      }

      if (target.ElseBlock != null) {
        using (_tree.Block(true)) {
          target.ElseBlock.Accept(this);
        }
      }

      return target;
    }
    public IParseItem Visit(IndexerItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      target.Prefix.Accept(this);
      target.Expression.Accept(this);

      return target;
    }
    public IParseItem Visit(LabelItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      _tree.DefineLabel(target);

      return target;
    }
    public IParseItem Visit(LiteralItem target) {
      // Do nothing.
      return target;
    }
    public IParseItem Visit(NameItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      _tree.GetName(target);

      return target;
    }
    public IParseItem Visit(RepeatItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      target.Expression.Accept(this);

      using (_tree.Block(true)) {
        _tree.DefineLabel(target.Break);
        target.Block.Accept(this);
      }

      return target;
    }
    public IParseItem Visit(ReturnItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      foreach (var item in target.Expressions) {
        item.Accept(this);
      }

      return target;
    }
    public IParseItem Visit(TableItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      foreach (var item in target.Fields) {
        item.Key.Accept(this);
        item.Value.Accept(this);
      }

      return target;
    }
    public IParseItem Visit(UnOpItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      target.Target.Accept(this);

      return target;
    }
    public IParseItem Visit(AssignmentItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      if (target.Local) {
        _tree.DefineLocal(target.Names.Select(i => i as NameItem));
      } else {
        foreach (var item in target.Names) {
          item.Accept(this);
        }
      }

      foreach (var item in target.Expressions) {
        item.Accept(this);
      }

      return target;
    }
    public IParseItem Visit(WhileItem target) {
      if (target == null) {
        throw new ArgumentNullException(nameof(target));
      }

      target.Expression.Accept(this);

      using (_tree.Block(true)) {
        _tree.DefineLabel(target.Break);
        target.Block.Accept(this);
      }

      return target;
    }
  }
}
