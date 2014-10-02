using System;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents a goto statement.
    /// </summary>
    public sealed class GotoItem : IParseStatement
    {
        /// <summary>
        /// Creates a new GotoItem with the given name.
        /// </summary>
        /// <param name="label">The name of the target label.</param>
        /// <exception cref="System.ArgumentNullException">If label is null.</exception>
        public GotoItem(string label)
        {
            if (label == null)
                throw new ArgumentNullException("label");

            this.Name = label;
        }

        /// <summary>
        /// Gets or sets the target name of the goto.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Gets or sets the debug info for this item.
        /// </summary>
        public Token Debug { get; set; }
        /// <summary>
        /// Gets or sets the destination of the goto.
        /// </summary>
        public LabelItem Target { get; set; }
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
