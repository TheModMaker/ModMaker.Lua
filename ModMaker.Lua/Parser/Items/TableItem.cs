using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents a table definition expression.
    /// </summary>
    public sealed class TableItem : IParseExp
    {
        double i = 1;
        List<KeyValuePair<IParseExp, IParseExp>> fields;

        /// <summary>
        /// Creates a new instance of TableItem.
        /// </summary>
        public TableItem()
        {
            fields = new List<KeyValuePair<IParseExp, IParseExp>>();
        }

        /// <summary>
        /// Gets the fields of the table.
        /// </summary>
        public ReadOnlyCollection<KeyValuePair<IParseExp, IParseExp>> Fields
        { get { return new ReadOnlyCollection<KeyValuePair<IParseExp, IParseExp>>(fields); } }
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
        /// Adds a new item to the table definition.
        /// </summary>
        /// <param name="index">The index expression.</param>
        /// <param name="exp">The value expression.</param>
        /// <exception cref="System.ArgumentNullException">If exp is null.</exception>
        public void AddItem(IParseExp index, IParseExp exp)
        {
            if (exp == null)
                throw new ArgumentNullException("exp");

            if (index == null)
                index = new LiteralItem(i++);
            fields.Add(new KeyValuePair<IParseExp, IParseExp>(index, exp));
        }
    }
}
