// Copyright 2022 Jacob Trimble
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
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Parser {
  /// <summary>
  /// A helper to keep track of locals, blocks, and labels.  This is used during parsing and
  /// execution to resolve names.  If this is gtiven a CompilerMessageCollection, errors are given
  /// to that object and otherwise ignored; if null is passed then exceptions are thrown.
  /// </summary>
  public sealed class LocalsResolver {
    readonly CompilerMessageCollection? _errors;
    readonly Stack<Function> _functions = new Stack<Function>();

    sealed class Block {
      public readonly HashSet<GotoItem> LocalGoto = new HashSet<GotoItem>();
      public readonly Dictionary<string, LabelItem> VisibleLabels =
          new Dictionary<string, LabelItem>();
      public readonly Dictionary<string, NameItem> Locals = new Dictionary<string, NameItem>();
    }

    sealed class Function {
      public Function() {
        Blocks.Push(new Block());
      }

      public readonly Stack<Block> Blocks = new Stack<Block>();

      public readonly List<NameItem> CapturedParents = new List<NameItem>();
      public readonly HashSet<NameItem> CapturedLocals =
          new HashSet<NameItem>(new ReferenceEqualsComparer<NameItem>());
      public bool HasNested = false;

      public readonly HashSet<GotoItem> PendingGoto = new HashSet<GotoItem>();
    }

    public LocalsResolver(CompilerMessageCollection? errors = null) {
      _errors = errors;
    }

    /// <summary>
    /// Finds the NameItem (by reference) that resolves the given name according to the current
    /// block.  For function info, this also marks the variable as used.
    /// </summary>
    /// <param name="name">The name to search for.</param>
    /// <returns>The instance that matches the given name, or null if not found.</returns>
    public NameItem? ResolveName(string name) {
      // Traverse upward to find the function that defines the variable as a local.
      foreach (var func in _functions) {
        foreach (var block in func.Blocks) {
          if (block.Locals.TryGetValue(name, out NameItem? ret)) {
            if (func != _functions.Peek())
              func.CapturedLocals.Add(ret);

            // Traverse again from the start to mark the variable as captured.
            foreach (var func2 in _functions) {
              if (func2 == func)
                break;
              if (!func2.CapturedParents.Contains(ret, new ReferenceEqualsComparer<NameItem>()))
                func2.CapturedParents.Add(ret);
            }

            return ret;
          }
        }
      }
      return null;
    }
    /// <summary>
    /// Returns information about the current variable usage for the function.
    /// </summary>
    public FuncDefItem.FunctionInfo GetFunctionInfo() {
      var func = _functions.Peek();
      return new FuncDefItem.FunctionInfo() {
        CapturedLocals = func.CapturedLocals.ToArray(),
        CapturedParents = func.CapturedParents.ToArray(),
        HasNested = func.HasNested,
      };
    }

    /// <summary>
    /// Defines a new block for a function definition.  This MUST be used within a "using" statement
    /// so Dispose is called at the end of the block.
    /// </summary>
    /// <returns>An instance that is used to mark the end of the function.</returns>
    public IDisposable DefineFunction() {
      var func = new Function();
      if (_functions.Count > 0)
        _functions.Peek().HasNested = true;
      _functions.Push(func);
      return Helpers.Disposable(() => _endFunction(func));
    }
    /// <summary>
    /// Defines a new generic block (e.g. for an "if").  This MUST be used within a "using"
    /// statement so Dispose is called at the end of the block.
    /// </summary>
    /// <returns>An instance that is used to mark the end of the function.</returns>
    public IDisposable DefineBlock() {
      var func = _functions.Peek();
      var block = new Block();
      func.Blocks.Push(block);
      return Helpers.Disposable(() => _endBlock(func, block));
    }
    
    /// <summary>
    /// Defines a collection of local variables within the current block.
    /// </summary>
    /// <param name="names">The names of the local variables.</param>
    public void DefineLocals(IEnumerable<NameItem> names) {
      var block = _functions.Peek().Blocks.Peek();
      block.LocalGoto.Clear();
      foreach (var name in names) {
        // Replace any existing name.
        block.Locals.Remove(name.Name);
        block.Locals.Add(name.Name, name);
      }
    }
    /// <summary>
    /// Defines a new label in the current block.
    /// </summary>
    /// <param name="label">The label to define.</param>
    public void DefineLabel(LabelItem label) {
      var func = _functions.Peek();
      var block = func.Blocks.Peek();
      block.VisibleLabels.Add(label.Name, label);
      // Make a copy of the set so we can modify while traversing.
      foreach (var @goto in block.LocalGoto.ToArray()) {
        if (@goto.Name == label.Name) {
          @goto.Target = label;
          block.LocalGoto.Remove(@goto);
          func.PendingGoto.Remove(@goto);
        }
      }
    }
    /// <summary>
    /// Defines a new GotoItem in the current block.
    /// </summary>
    /// <param name="item">The item to define.</param>
    public void DefineGoto(GotoItem @goto) {
      var target = _resolveLabel(@goto.Name);
      if (target == null) {
        var func = _functions.Peek();
        func.PendingGoto.Add(@goto);
        func.Blocks.Peek().LocalGoto.Add(@goto);
      } else {
        @goto.Target = target;
      }
    }

    LabelItem? _resolveLabel(string name) {
      foreach (var block in _functions.Peek().Blocks) {
        if (block.VisibleLabels.TryGetValue(name, out LabelItem? item))
          return item;
      }
      return null;
    }

    void _endBlock(Function func, Block block) {
      if (_functions.Peek() != func)
        throw new InvalidOperationException("Block Dispose called after Function Dispose");
      var top = func.Blocks.Pop();
      if (top != block)
        throw new InvalidOperationException("Block Dispose called out of order");
    }

    void _endFunction(Function func) {
      var top = _functions.Pop();
      if (top != func)
        throw new InvalidOperationException("Function Dispose called out of order");

      if (func.PendingGoto.Count > 0) {
        var errors = _errors ?? new CompilerMessageCollection(MessageLevel.Error);
        foreach (var @goto in func.PendingGoto) {
          errors.Add(new CompilerMessage(
              MessageLevel.Error, MessageId.LabelNotFound, @goto.Debug,
              $"Label '{@goto.Name}' not found or not visible"));
        }
        if (_errors == null)
          throw errors.MakeException();
      }
    }
  }
}
