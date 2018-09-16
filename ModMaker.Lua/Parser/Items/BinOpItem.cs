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
    /// Defines the type of a binary expression.
    /// </summary>
    public enum BinaryOperationType
    {
        /// <summary>
        /// The type of operation is unknown.
        /// </summary>
        Unknown,
        /// <summary>
        /// An addition of two values (A + B).
        /// </summary>
        Add,
        /// <summary>
        /// A subtraction of two values (A - B).
        /// </summary>
        Subtract,
        /// <summary>
        /// A multiplication of two values (A * B).
        /// </summary>
        Multiply,
        /// <summary>
        /// A division of two values (A / B).
        /// </summary>
        Divide,
        /// <summary>
        /// A power of two values (A ^ B).
        /// </summary>
        Power,
        /// <summary>
        /// A modulo of two values (A % B).
        /// </summary>
        Modulo,
        /// <summary>
        /// Concatenation two values (A .. B).
        /// </summary>
        Concat,
        /// <summary>
        /// Comparison for greater than (A &gt; B).
        /// </summary>
        Gt,
        /// <summary>
        /// Comparison for less than (A &lt; B).
        /// </summary>
        Lt,
        /// <summary>
        /// Comparison for greater than or equal to (A &gt;= B).
        /// </summary>
        Gte,
        /// <summary>
        /// Comparison for less than or equal to (A &lt;= B).
        /// </summary>
        Lte,
        /// <summary>
        /// Comparison for equal to (A == B).
        /// </summary>
        Equals,
        /// <summary>
        /// Comparison for not equal to (A ~= B).
        /// </summary>
        NotEquals,
        /// <summary>
        /// Checks whether two values are both true and returns the last
        /// true value (A and B).
        /// </summary>
        And,
        /// <summary>
        /// Checks whether at least one value is true and returns the first
        /// true value (A or B).
        /// </summary>
        Or,
    }

    /// <summary>
    /// Defines a parse item that is a binary expression.
    /// e.g. a + b.
    /// </summary>
    public sealed class BinOpItem : IParseExp
    {
        IParseExp lhs, rhs;

        /// <summary>
        /// Creates a new instance with the given state.
        /// </summary>
        /// <param name="lhs">The left-hand-side of the epxression.</param>
        /// <param name="rhs">The right-hand-side of the epxression.</param>
        /// <param name="type">The type of the expression.</param>
        /// <exception cref="System.ArgumentNullException">If lhs is null.</exception>
        public BinOpItem(IParseExp lhs, BinaryOperationType type, IParseExp rhs)
        {
            if (lhs == null)
                throw new ArgumentNullException(nameof(lhs));
            if (rhs == null)
                throw new ArgumentNullException(nameof(rhs));

            this.lhs = lhs;
            this.rhs = rhs;
            this.OperationType = type;
        }

        /// <summary>
        /// Gets or sets the left-hand-side of the expression.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to null.</exception>
        public IParseExp Lhs
        {
            get { return lhs; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                this.lhs = value;
            }
        }
        /// <summary>
        /// Gets or sets the right-hand-side of the expression.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to null.</exception>
        public IParseExp Rhs
        {
            get { return rhs; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                this.rhs = value;
            }
        }
        /// <summary>
        /// Gets or sets the type of the operation, e.g. addition.
        /// </summary>
        public BinaryOperationType OperationType { get; set; }
        /// <summary>
        /// Gets or sets the user data for this object. This value is never
        /// modified by the default framework, but may be modified by other
        /// visitors.
        /// </summary>
        public object UserData { get; set; }
        /// <summary>
        /// Gets or sets the debug info for this item.
        /// </summary>
        public Token Debug { get; set; }

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
