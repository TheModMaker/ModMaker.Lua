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

using System.IO;
using System.Text;

namespace ModMaker.Lua.Parser {
  /// <summary>
  /// This object is in charge of parsing input into an object tree.  This will take a Stream object
  /// input and will parse it into an IParseItem tree.
  /// </summary>
  public interface IParser {
    /// <summary>
    /// Parses the given Lua code into a IParseItem tree.
    /// </summary>
    /// <param name="input">The Lua code to parse.</param>
    /// <param name="encoding">The encoding that the stream uses.</param>
    /// <param name="name">The name of the chunk, used for exceptions.</param>
    /// <returns>The code as an IParseItem tree.</returns>
    IParseItem Parse(Stream input, Encoding encoding, string name);
    /// <summary>
    /// Parses the given Lua code into a IParseItem tree.
    /// </summary>
    /// <param name="input">The Lua code to parse.</param>
    /// <param name="name">The name of the chunk, used for exceptions.</param>
    /// <returns>The code as an IParseItem tree.</returns>
    IParseItem Parse(string input, string name);
  }
}
