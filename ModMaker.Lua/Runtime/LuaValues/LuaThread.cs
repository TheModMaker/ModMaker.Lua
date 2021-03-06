// Copyright 2016 Jacob Trimble
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
using ModMaker.Lua.Parser.Items;

namespace ModMaker.Lua.Runtime.LuaValues {
  /// <summary>
  /// Defines a thread in Lua.  Cannot be created in C#, use the coroutine library in Lua to create
  /// a thread.  Threads in Lua execute synchronously.
  /// </summary>
  public abstract class LuaThread : LuaValueBase, ILuaThread, IDisposable {
    volatile LuaThreadStatus _flag = LuaThreadStatus.Suspended;
    bool _disposed = false;

    /// <summary>
    /// Creates a new thread.
    /// </summary>
    protected LuaThread() { }
    ~LuaThread() {
      if (!_disposed) {
        _dispose(false);
      }
    }

    public override LuaValueType ValueType { get { return LuaValueType.Thread; } }

    public event EventHandler<YieldEventArgs> OnYield;
    public LuaThreadStatus Status {
      get { return _flag; }
      protected set { _flag = value; }
    }
    public bool IsLua { get; protected set; }

    public abstract ILuaMultiValue Resume(ILuaMultiValue args);
    public abstract ILuaMultiValue Yield(ILuaMultiValue args);

    public override ILuaValue Arithmetic(BinaryOperationType type, ILuaValue other) {
      return _arithmeticBase(type, other) ?? ((ILuaValueVisitor)other).Arithmetic(type, this);
    }
    public override ILuaValue Arithmetic<T>(BinaryOperationType type, LuaUserData<T> self) {
      return self.ArithmeticFrom(type, this);
    }

    public override bool Equals(ILuaValue other) {
      return ReferenceEquals(this, other);
    }
    public override bool Equals(object obj) {
      return ReferenceEquals(this, obj);
    }
    public override int GetHashCode() {
      return base.GetHashCode();
    }

    public void Dispose() {
      if (!_disposed) {
        _disposed = true;
        GC.SuppressFinalize(this);
        _dispose(true);
      }
    }
    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting
    /// unmanaged resources.
    /// </summary>
    protected virtual void _dispose(bool disposing) { }

    /// <summary>
    /// Calls the OnYield event handler.
    /// </summary>
    /// <param name="e">The arguments to the event.</param>
    protected void _callOnYield(YieldEventArgs e) {
      OnYield?.Invoke(this, e);
    }
  }
}
