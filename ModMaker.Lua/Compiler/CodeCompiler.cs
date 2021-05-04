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
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using ModMaker.Lua.Parser;
using ModMaker.Lua.Runtime;
using ModMaker.Lua.Runtime.LuaValues;

namespace ModMaker.Lua.Compiler {
  /// <summary>
  /// Defines an ICodeCompiler to compile the IParseItem tree into IL code.
  /// </summary>
  [LuaIgnore]
  public sealed class CodeCompiler : ICodeCompiler {
    readonly List<string> _types = new List<string>();
    readonly AssemblyBuilder _ab;
    readonly ModuleBuilder _mb;
    readonly LuaSettings _settings;
    int _tid = 1;

    /// <summary>
    /// Creates a new CodeCompiler object.
    /// </summary>
    public CodeCompiler(LuaSettings settings) {
      _settings = settings;
      _ab = AssemblyBuilder.DefineDynamicAssembly(
          new AssemblyName("DynamicAssembly"), AssemblyBuilderAccess.Run);

#if NETFRAMEWORK
      if (_settings.AddNativeDebugSymbols) {
        ConstructorInfo debugConstructor = typeof(DebuggableAttribute).GetConstructor(
            new[] { typeof(DebuggableAttribute.DebuggingModes) });
        var debugBuilder = new CustomAttributeBuilder(
            debugConstructor, new object[] {
                DebuggableAttribute.DebuggingModes.DisableOptimizations |
                DebuggableAttribute.DebuggingModes.Default,
            });
        _ab.SetCustomAttribute(debugBuilder);
      }

      _mb = _ab.DefineDynamicModule("DynamicAssembly.dll", true);
#else
      _mb = _ab.DefineDynamicModule("DynamicAssembly.dll");
#endif
    }

    public ILuaValue Compile(ILuaEnvironment env, IParseItem item, string name) {
      if (env == null) {
        throw new ArgumentNullException(nameof(env));
      }

      if (item == null) {
        throw new ArgumentNullException(nameof(item));
      }

      name ??= "<>_func_" + (_tid++);
      if (_types.Contains(name)) {
        int i = 0;
        while (_types.Contains(name + i)) {
          i++;
        }

        name += i;
      }

      GetInfoVisitor lVisitor = new GetInfoVisitor();
      lVisitor.Resolve(item);

      TypeBuilder tb = _mb.DefineType(
          name, TypeAttributes.Public | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed,
          typeof(LuaValueBase), Type.EmptyTypes);
      ChunkBuilder cb = new ChunkBuilder(_settings, _mb, tb, lVisitor._globalCaptures,
                                         lVisitor._globalNested);

      CompilerVisitor cVisitor = new CompilerVisitor(cb);
      item.Accept(cVisitor);
      var ret = cb.CreateChunk(env);
      return ret;
    }
  }
}
