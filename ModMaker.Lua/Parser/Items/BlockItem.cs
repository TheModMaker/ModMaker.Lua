using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents a block of code.
    /// </summary>
    public sealed class BlockItem : IParseStatement
    {
        List<IParseStatement> children;

        /// <summary>
        /// Creates a new empty BlockItem.
        /// </summary>
        public BlockItem()
        {
            this.children = new List<IParseStatement>();
        }

        /// <summary>
        /// Gets or sets the return statement of the block, can be null.
        /// </summary>
        public ReturnItem Return { get; set; }
        /// <summary>
        /// Gets the children of the block item.
        /// </summary>
        public ReadOnlyCollection<IParseStatement> Children
        { get { return new ReadOnlyCollection<IParseStatement>(children); } }
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
                throw new ArgumentNullException("visitor");

            return visitor.Visit(this);
        }
        /// <summary>
        /// Adds a child element to the block.
        /// </summary>
        /// <param name="child">The child statement to add.</param>
        /// <exception cref="System.ArgumentNullException">If child is null.</exception>
        public void AddItem(IParseStatement child)
        {
            if (child == null)
                throw new ArgumentNullException("child");

            this.children.Add(child);
        }
        /// <summary>
        /// Adds a range of child elements to the block.
        /// </summary>
        /// <param name="children">The children to add.</param>
        /// <exception cref="System.ArgumentException">If children contains
        /// a null item -or- if one of the children is not a statement.</exception>
        /// <exception cref="System.ArgumentNullException">If children is null.</exception>
        public void AddRange(IEnumerable<IParseStatement> children)
        {
            if (children == null)
                throw new ArgumentNullException("children");
            if (children.Contains(null))
                throw new ArgumentException(string.Format(Resources.CannotContainNull, "Children"));

            this.children.AddRange(children);
        }
    }
}