using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents a for generic statement.  
    /// e.g. for k, v in dict do ... end.
    /// </summary>
    public sealed class ForGenItem : IParseStatement
    {
        NameItem[] names;
        List<IParseExp> exps;

        /// <summary>
        /// Creates a new instance of ForGenItem with the given names definiton.
        /// </summary>
        /// <param name="names">The names that are defined by this statement.</param>
        /// <exception cref="System.ArgumentException">If names does not contain
        /// any names or contains a null item.</exception>
        /// <exception cref="System.ArgumentNullException">If names is null.</exception>
        public ForGenItem(IEnumerable<NameItem> names)
        {
            if (names == null)
                throw new ArgumentNullException("names");
            if (names.Contains(null))
                throw new ArgumentException(string.Format(Resources.CannotContainNull, "Names"));

            this.names = names.ToArray();
            this.exps = new List<IParseExp>();
            this.Break = new LabelItem("<break>");

            if (this.names.Length == 0)
                throw new ArgumentException(Resources.DefineAtLeastOneName);
        }

        /// <summary>
        /// Gets the expressions for the for generic statement.
        /// </summary>
        public ReadOnlyCollection<IParseExp> Expressions { get { return new ReadOnlyCollection<IParseExp>(exps); } }
        /// <summary>
        /// Gets the name definitions for the for generic statement.
        /// </summary>
        public ReadOnlyCollection<NameItem> Names { get { return new ReadOnlyCollection<NameItem>(names); } }
        /// <summary>
        /// Gets the label that represents a break from the loop.
        /// </summary>
        public LabelItem Break { get; private set; }
        /// <summary>
        /// Gets or sets the block of the for generic statement.
        /// </summary>
        public BlockItem Block { get; set; }
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
        /// Adds the given expression to the object.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <exception cref="System.ArgumentException">If item is not an expression.</exception>
        /// <exception cref="System.ArgumentNullException">If item is null.</exception>
        public void AddExpression(IParseExp item)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            exps.Add(item);
        }
    }
}
