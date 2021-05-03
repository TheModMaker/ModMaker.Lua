// Copyright 2021 Jacob Trimble
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

#nullable enable

namespace ModMaker.Lua.Parser {
  public struct DebugInfo {
    public DebugInfo(string path, long startPos, long startLine, long endPos, long endLine) {
      Path = path;
      StartPos = startPos;
      StartLine = startLine;
      EndPos = endPos;
      EndLine = endLine;
    }

    /// <summary>
    /// The path to the file this is from.
    /// </summary>
    public string Path;
    /// <summary>
    /// The starting position of the item.
    /// </summary>
    public long StartPos;
    /// <summary>
    /// The starting line of the item.
    /// </summary>
    public long StartLine;
    /// <summary>
    /// The ending position of the item.
    /// </summary>
    public long EndPos;
    /// <summary>
    /// The ending line of the item.
    /// </summary>
    public long EndLine;

    public static bool operator ==(DebugInfo lhs, DebugInfo rhs) {
      return lhs.Path == rhs.Path && lhs.StartPos == rhs.StartPos &&
             lhs.StartLine == rhs.StartLine && lhs.EndPos == rhs.EndPos &&
             lhs.EndLine == rhs.EndLine;
    }
    public static bool operator !=(DebugInfo lhs, DebugInfo rhs) {
      return !(lhs == rhs);
    }

    public override bool Equals(object? obj) {
      return obj is DebugInfo info && info == this;
    }
    public override int GetHashCode() {
#if NETFRAMEWORK
      return Path.GetHashCode() ^ StartPos.GetHashCode() ^ StartLine.GetHashCode() ^
             EndPos.GetHashCode() ^ EndLine.GetHashCode();
#else
      return HashCode.Combine(Path, StartPos, StartLine, EndPos, EndLine);
#endif
    }
    public override string ToString() {
      string name = System.IO.Path.GetFileNameWithoutExtension(Path);
      return $"DebugInfo(Path={name}, Line={StartLine}, Pos={StartPos})";
    }
  }
}
