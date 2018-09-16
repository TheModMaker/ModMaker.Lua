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
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a variable assignment statement.
    /// e.g. i, x = foo();
    /// </summary>
    public sealed class AssignmentItem : IParseStatement
    {
        List<IParseExp> exps;
        List<IParseVariable> names;

        /// <summary>
        /// Creates a new instance of AssignmentItem.
        /// </summary>
        /// <param name="local">True if this is a local definition, otherwise false.</param>
        public AssignmentItem(bool local)
        {
            this.names = new List<IParseVariable>();
            this.exps = new List<IParseExp>();
            this.Local = local;
        }

        /// <summary>
        /// Gets the value expression of this item.
        /// </summary>
        public ReadOnlyCollection<IParseExp> Expressions
        { get { return new ReadOnlyCollection<IParseExp>(exps); } }
        /// <summary>
        /// Gets the name expression of this item.
        /// </summary>
        public ReadOnlyCollection<IParseVariable> Names
        { get { return new ReadOnlyCollection<IParseVariable>(names); } }
        /// <summary>
        /// Gets or sets whether this is a local definition.
        /// </summary>
        public bool Local { get; set; }
        /// <summary>
        /// Gets or sets whether the last expression is single.  Namely whether the last expression
        /// is wrapped in parentheses, e.g. i = (foo()).
        /// </summary>
        public bool IsLastExpressionSingle { get; set; }
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

        /// <summary>
        /// Adds a name to the item.  This defines the left-hand-side of the
        /// statement.
        /// </summary>
        /// <param name="name">The parse item that defines the name.</param>
        /// <exception cref="System.ArgumentNullException">If name is null.</exception>
        public void AddName(IParseVariable name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            names.Add(name);
        }
        /// <summary>
        /// Adds an expression to the item.  This defines the right-hand-side
        /// of the statement.
        /// </summary>
        /// <param name="item">The item that defines the expression.</param>
        /// <exception cref="System.ArgumentNullException">If item is null.</exception>
        public void AddItem(IParseExp item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            exps.Add(item);
        }
    }
}
