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

using System.Linq;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;

#nullable enable

namespace ModMaker.Lua.Compiler {
  /// <summary>
  /// A visitor that resolves gotos and breaks and also get capture information about function
  /// definitions.  This information is stored in the given item. This is used by the default
  /// compiler.
  /// </summary>
  public sealed class GetInfoVisitor : IParseItemVisitor {
    readonly GetInfoTree _tree;
    readonly FuncDefItem.FunctionInfo _info;

    GetInfoVisitor(IParseItem target) {
      _tree = new GetInfoTree();
      target.Accept(this);
      _tree.Resolve();
      _info = _tree.EndFunc();
    }

    /// <summary>
    /// This traverses the tree, resolves the lables, and updates the info in the item.
    /// </summary>
    /// <param name="target">The IParseItem tree to traverse.</param>
    /// <returns>The function info describing the item.</returns>
    public static FuncDefItem.FunctionInfo Resolve(IParseItem target) {
      var visitor = new GetInfoVisitor(target);
      return visitor._info;
    }

    public IParseItem Visit(BinOpItem target) {
      target.Lhs.Accept(this);
      target.Rhs.Accept(this);
      return target;
    }
    public IParseItem Visit(BlockItem target) {
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
      foreach (var item in target.Arguments) {
        item.Expression.Accept(this);
      }

      target.Prefix.Accept(this);

      return target;
    }
    public IParseItem Visit(FuncDefItem target) {
      if (target.Local) {
        _tree.DefineLocal(new[] { (NameItem)target.Prefix! });
      }

      using (_tree.DefineFunc()) {
        _tree.DefineLocal(target.Arguments);
        target.Block.Accept(this);
      }
      target.FunctionInformation = _tree.EndFunc();

      return target;
    }
    public IParseItem Visit(GotoItem target) {
      _tree.DefineGoto(target);
      return target;
    }
    public IParseItem Visit(IfItem target) {
      target.Expression.Accept(this);

      using (_tree.Block(true)) {
        target.Block.Accept(this);
      }

      foreach (IfItem.ElseInfo info in target.Elses) {
        using (_tree.Block(true)) {
          info.Expression.Accept(this);
          info.Block.Accept(this);
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
      target.Prefix.Accept(this);
      target.Expression.Accept(this);

      return target;
    }
    public IParseItem Visit(LabelItem target) {
      _tree.DefineLabel(target);

      return target;
    }
    public IParseItem Visit(LiteralItem target) {
      // Do nothing.
      return target;
    }
    public IParseItem Visit(NameItem target) {
      _tree.GetName(target);

      return target;
    }
    public IParseItem Visit(RepeatItem target) {
      target.Expression.Accept(this);

      using (_tree.Block(true)) {
        _tree.DefineLabel(target.Break);
        target.Block.Accept(this);
      }

      return target;
    }
    public IParseItem Visit(ReturnItem target) {
      foreach (var item in target.Expressions) {
        item.Accept(this);
      }

      return target;
    }
    public IParseItem Visit(TableItem target) {
      foreach (var item in target.Fields) {
        item.Key.Accept(this);
        item.Value.Accept(this);
      }

      return target;
    }
    public IParseItem Visit(UnOpItem target) {
      target.Target.Accept(this);

      return target;
    }
    public IParseItem Visit(AssignmentItem target) {
      if (target.Local) {
        _tree.DefineLocal(target.Names.Cast<NameItem>());
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
      target.Expression.Accept(this);

      using (_tree.Block(true)) {
        _tree.DefineLabel(target.Break);
        target.Block.Accept(this);
      }

      return target;
    }
  }
}
