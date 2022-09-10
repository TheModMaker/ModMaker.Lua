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

namespace System.Diagnostics.CodeAnalysis {

  // Add definitions for C# or compiler features not available in older .NET versions.  These are
  // only used by the compiler, so these don't need to do anything.  We just need them so we don't
  // get unresolved symbol errors.

#if NET48
  [AttributeUsage(AttributeTargets.Parameter)]
  internal class NotNullWhen : Attribute {
    public NotNullWhen(bool value) {
      ReturnValue = value;
    }

    public bool ReturnValue { get; private set; }
  }
#endif
}
