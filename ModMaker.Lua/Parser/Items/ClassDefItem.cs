using System;
using System.Collections.Generic;
using System.Linq;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents a class definition.
    /// </summary>
    public sealed class ClassDefItem : IParseStatement
    {
        /// <summary>
        /// Creates a new instance of ClassDefItem with the given state.
        /// </summary>
        /// <param name="name">The name of the class.</param>
        /// <param name="implements">The types that it implements.</param>
        public ClassDefItem(string name, string[] implements)
        {
            implements = implements ?? new string[0];
            this.Name = name;
            this.Implements = new List<string>(implements.Where(s => !string.IsNullOrEmpty(s)));
        }

        /// <summary>
        /// Gets or sets the name of the class.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Gets the types that this class implements.
        /// </summary>
        public List<string> Implements { get; private set; }
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
    }
}
