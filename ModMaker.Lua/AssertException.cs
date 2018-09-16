// Copyright 2012 Jacob Trimble
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

namespace ModMaker.Lua
{
    /// <summary>
    /// Thrown when an assertion fails.
    /// </summary>
    public sealed class AssertException : Exception
    {
        /// <summary>
        /// Creates a new AssertException object.
        /// </summary>
        public AssertException() : base("Assertion failed.") { }
        /// <summary>
        /// Creates a new AssertException object with the given message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public AssertException(string message) : base(message) { }
        /// <summary>
        /// Creates a new AssertException object with the given message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference
        ///     (Nothing in Visual Basic) if no inner exception is specified.</param>
        public AssertException(string message, Exception inner) : base(message, inner) { }
    }
}
