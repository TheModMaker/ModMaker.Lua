using System;
using System.Diagnostics;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents a name in an expression.
    /// </summary>
    [DebuggerDisplay("NameItem {Name}")]
    public sealed class NameItem : IParseVariable
    {
        /// <summary>
        /// Creates a new instance of NameItem with the given name.
        /// </summary>
        /// <param name="name">The name of the item.</param>
        public NameItem(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Gets or sets the name of this instance.
        /// </summary>
        public string Name { get; set; }
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
        /// Determines whether the specified System.Object is equal to the current System.Object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            return obj is NameItem && object.Equals(((NameItem)obj).Name, this.Name);
        }
        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>A hash code for the current System.Object.</returns>
        public override int GetHashCode()
        {
            return (this.Name ?? "").GetHashCode();
        }
    }
}
