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

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines the type of a unary operation.
    /// </summary>
    public enum UnaryOperationType
    {
        /// <summary>
        /// The type of the operation is unknown.
        /// </summary>
        Unknown,
        /// <summary>
        /// A unary negation, (-A).
        /// </summary>
        Minus,
        /// <summary>
        /// A logical negation, (not A).
        /// </summary>
        Not,
        /// <summary>
        /// The length of a variable (#A).
        /// </summary>
        Length,
    }

    /// <summary>
    /// Defines a parse item that represents a unary operation expression.
    /// </summary>
    public sealed class UnOpItem : IParseExp
    {
        IParseExp target;

        /// <summary>
        /// Creates a new UnOpItem with the given state.
        /// </summary>
        /// <param name="target">The target expression.</param>
        /// <param name="type">The type of operation.</param>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public UnOpItem(IParseExp target, UnaryOperationType type)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            this.target = target;
            this.OperationType = type;
        }

        /// <summary>
        /// Gets or sets the target expression.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to null.</exception>
        public IParseExp Target
        {
            get { return target; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                target = value;
            }
        }
        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        public UnaryOperationType OperationType { get; set; }
        /// <summary>
        /// Gets or sets the debug info for this item.
        /// </summary>
        public Token Debug { get; set; }
        /// <summary>
        /// Gets or sets the user data for this object. This value is never
        /// modified by the default framework, but may be modified by other
        /// visitors.
        /// </summary>
        public object UserData { get; set; }

        /// <summary>
        /// Dispatches to the specific visit method for this item type.
        /// </summary>
        /// <param name="visitor">The visitor object.</param>
        /// <returns>The object returned from the specific IParseItemVisitor method.</returns>
        /// <exception cref="System.ArgumentNullException">If visitor is null.</exception>
        public IParseItem Accept(IParseItemVisitor visitor)
        {
            if (visitor == null)
                throw new ArgumentNullException(nameof(visitor));

            return visitor.Visit(this);
        }
    }
}
