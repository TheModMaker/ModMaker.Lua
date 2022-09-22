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

namespace ModMaker.Lua.Runtime {
  /// <summary>
  /// This represents a Lua object that is bound to a specific environment.  These values cannot be
  /// used in another environment as it could violate the sandbox.  For security, values should be
  /// cloned when passed between environments.
  /// </summary>
  public interface ILuaBoundValue {
    /// <summary>
    /// Gets the environment that this value is bound to.
    /// </summary>
    public ILuaEnvironment Environment { get; }

    /// <summary>
    /// Creates a deep copy of this object that is bound to the given environment.  This may throw
    /// a NotSupportedException if this isn't supported.
    /// </summary>
    /// <param name="environment">The new environment to bind to.</param>
    /// <returns>A deep clone of the object.</returns>
    public object CloneIntoEnvironment(ILuaEnvironment environment);
  }
}
