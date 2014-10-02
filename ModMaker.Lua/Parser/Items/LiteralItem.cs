using System;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents a literal.
    /// </summary>
    public sealed class LiteralItem : IParseExp
    {
        /// <summary>
        /// Creates a new LiteralItem with the given value.
        /// </summary>
        /// <param name="item">The value of this literal.</param>
        public LiteralItem(object item)
        {
            if (!(item is bool || item is double || item is string || item == null))
                throw new InvalidOperationException(Resources.InvalidLiteralType);
            this.Value = item;
        }

        /// <summary>
        /// Gets or sets the value of this literal.
        /// </summary>
        public object Value { get; set; }
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
    }
}