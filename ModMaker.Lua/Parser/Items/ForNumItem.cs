using System;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents a numerical for loop. 
    /// e.g. for i = 2, 4 do ... end.
    /// </summary>
    public sealed class ForNumItem : IParseStatement
    {
        IParseExp start, limit;

        /// <summary>
        /// Creates a new ForNumItem with the given name.
        /// </summary>
        /// <param name="name">The name of the variable defined.</param>
        /// <param name="limit">The item that defines the limit of the loop.</param>
        /// <param name="start">The item that defines the start of the loop.</param>
        /// <param name="step">The item that defines the step of the loop.</param>
        /// <exception cref="System.ArgumentNullException">If name, start,
        /// or limit is null.</exception>
        public ForNumItem(NameItem name, IParseExp start, IParseExp limit, IParseExp step)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            if (start == null)
                throw new ArgumentNullException("start");
            if (limit == null)
                throw new ArgumentNullException("limit");

            this.start = start;
            this.limit = limit;
            this.Step = step;
            this.Name = name;
            this.Break = new LabelItem("<break>");
        }

        /// <summary>
        /// Gets the label that represents a break from the loop.
        /// </summary>
        public LabelItem Break { get; private set; }
        /// <summary>
        /// Gets or sets the name of the variable in the loop.
        /// </summary>
        public NameItem Name { get; set; }
        /// <summary>
        /// Gets or sets the expression that determines the start of the loop.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to null.</exception>
        public IParseExp Start
        {
            get { return start; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                start = value;
            }
        }
        /// <summary>
        /// Gets or sets the expression that determines the limit of the loop.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">If setting to null.</exception>
        public IParseExp Limit
        {
            get { return limit; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                limit = value;
            }
        }
        /// <summary>
        /// Gets or sets the expression that determines the step of the loop.
        /// </summary>
        public IParseExp Step { get; set; }
        /// <summary>
        /// Gets or sets the block of the for loop.
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
    }
}
