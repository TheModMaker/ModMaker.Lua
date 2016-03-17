using System;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents an indexer expression.
    /// </summary>
    public sealed class IndexerItem : IParseVariable
    {
        IParseExp prefix;
        IParseExp exp;

        /// <summary>
        /// Creates a new IndexerItem.
        /// </summary>
        /// <param name="prefix">The prefix expression.</param>
        /// <param name="exp">The expression of the accessed member.</param>
        /// <exception cref="System.ArgumentNullException">If prefix
        /// or exp is null.</exception>
        public IndexerItem(IParseExp prefix, IParseExp exp)
        {
            this.Prefix = prefix;
            this.Expression = exp;
        }
        /// <summary>
        /// Creates a new IndexerItem.
        /// </summary>
        /// <param name="prefix">The prefix expression.</param>
        /// <param name="name">The name of the accessed member.</param>
        /// <exception cref="System.ArgumentNullException">If prefix
        /// or name is null.</exception>
        public IndexerItem(IParseExp prefix, string name)
            : this(prefix, new LiteralItem(name)) { }

        /// <summary>
        /// Gets or sets the prefix expression.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to null.</exception>
        public IParseExp Prefix
        {
            get { return prefix; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                prefix = value;
            }
        }
        /// <summary>
        /// Gets or sets the indexing expression.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to null.</exception>
        public IParseExp Expression
        {
            get { return exp; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                exp = value;
            }
        }
        /// <summary>
        /// Gets or sets the debug info for this item.
        /// </summary>
        public Token Debug { get; set; }
        /// <summary>
        /// Gets or sets the user data for this object.  This
        /// is not modified inbetween calls to Accept but may
        /// be altered by other visitor objects.
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
