using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents a return statement.
    /// </summary>
    public sealed class ReturnItem : IParseItem
    {
        List<IParseExp> exps;

        /// <summary>
        /// Creates a new instance of ReturnItem.
        /// </summary>
        public ReturnItem()
        {
            exps = new List<IParseExp>();
        }

        /// <summary>
        /// Gets the expressions of the return statement.
        /// </summary>
        public ReadOnlyCollection<IParseExp> Expressions
        { get { return new ReadOnlyCollection<IParseExp>(exps); } }
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
                throw new ArgumentNullException("visitor");

            return visitor.Visit(this);
        }
        /// <summary>
        /// Adds an expression to the statement.
        /// </summary>
        /// <param name="expression">The expression to add.</param>
        /// <exception cref="System.ArgumentNullException">If expression is null.</exception>
        public void AddExpression(IParseExp expression)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");

            exps.Add(expression);
        }
    }
}
