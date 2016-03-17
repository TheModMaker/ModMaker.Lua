
namespace ModMaker.Lua.Parser
{
    /// <summary>
    /// A code item that has been parsed into a tree.  This represents
    /// a single item such as an if statement or a binary expression.
    /// </summary>
    public interface IParseItem
    {
        /// <summary>
        /// Gets or sets the debug info for this item.
        /// </summary>
        Token Debug { get; set; }
        /// <summary>
        /// Gets or sets the user data for this object. This value is never
        /// modified by the default framework, but may be modified by other
        /// visitors.
        /// </summary>
        object UserData { get; set; }

        /// <summary>
        /// Dispatches to the specific visit method for this item type.
        /// </summary>
        /// <param name="visitor">The visitor object.</param>
        /// <returns>The object returned from the specific IParseItemVisitor method.</returns>
        IParseItem Accept(IParseItemVisitor visitor);
    }

    /// <summary>
    /// A parse item that is an expression, such as a binary expression or
    /// a function call.
    /// </summary>
    public interface IParseExp : IParseItem { }
    /// <summary>
    /// A parse item that is a prefix expression, these are also expressions,
    /// but can be used as prefixes for indesers and function calls.
    /// </summary>
    public interface IParsePrefixExp : IParseExp { }
    /// <summary>
    /// A parse item that is a variable, these store values such as a NameItem.
    /// </summary>
    public interface IParseVariable : IParsePrefixExp { }
    /// <summary>
    /// A parse item that is a statement, such as an assignment.
    /// </summary>
    public interface IParseStatement : IParseItem { }
}