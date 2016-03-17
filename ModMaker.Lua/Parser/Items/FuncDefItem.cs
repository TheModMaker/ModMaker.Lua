using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents a function definition.
    /// </summary>
    public sealed class FuncDefItem : IParseStatement, IParseExp
    {
        List<NameItem> args;

        /// <summary>
        /// Creates a new FuncDefItem with the given name.
        /// </summary>
        /// <param name="name">The name of the method, must be a NameItem or 
        /// IndexerItem.</param>
        public FuncDefItem(IParseVariable name)
            : this(name, false) { }
        /// <summary>
        /// Creates a new FuncDefItem with the given name.
        /// </summary>
        /// <param name="name">The name of the method, must be a NameItem
        /// or IndexerItem.</param>
        /// <param name="local">True if this is a local definition, otherwise false.</param>
        public FuncDefItem(IParseVariable name, bool local)
        {
            this.Prefix = name;
            this.args = new List<NameItem>();
            this.Local = local;
        }

        /// <summary>
        /// Gets the name of the arguments defined for this function definition.
        /// </summary>
        public ReadOnlyCollection<NameItem> Arguments
        { get { return new ReadOnlyCollection<NameItem>(args); } }
        /// <summary>
        /// Gets or sets the prefix expression for this function definition, 
        /// must be a NameItem or an IndexerItem.
        /// </summary>
        public IParseVariable Prefix { get; set; }
        /// <summary>
        /// Gets or sets whether this is a local function definition.
        /// </summary>
        public bool Local { get; set; }
        /// <summary>
        /// Gets or sets the name if the instance method or null if this isn't 
        /// an instance method.
        /// </summary>
        public string InstanceName { get; set; }
        /// <summary>
        /// Gets or sets the block of the function.
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
        /// Gets or sets information about the function. To get this information,
        /// use GetInfoVisitor.
        /// </summary>
        public FunctionInfo FunctionInformation { get; set; }

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
        /// Adds a new argument to the definition.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <exception cref="System.ArgumentNullException">If item is null.</exception>
        public void AddArgument(NameItem item)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            args.Add(item);
        }

        /// <summary>
        /// Defines information about a function definition.  This is used by
        /// GetInfoVisitor and the compiler.  This manages captured variables
        /// and allows for smaller generated code.
        /// </summary>
        public sealed class FunctionInfo
        {
            /// <summary>
            /// Creates a new instance of FunctionInfo.
            /// </summary>
            public FunctionInfo()
            {
                this.HasNested = false;
                this.CapturesParrent = false;
                this.CapturedLocals = new NameItem[0];
            }

            /// <summary>
            /// Gets or sets whether this function has nested functions.
            /// </summary>
            public bool HasNested { get; set; }
            /// <summary>
            /// Gets or sets whether this function captures local variables
            /// from the parrent function.
            /// </summary>
            public bool CapturesParrent { get; set; }
            /// <summary>
            /// Gets or sets an array of the local variables defined in
            /// this function that are captured by nested  functions.
            /// </summary>
            public NameItem[] CapturedLocals { get; set; }
        }
    }
}
