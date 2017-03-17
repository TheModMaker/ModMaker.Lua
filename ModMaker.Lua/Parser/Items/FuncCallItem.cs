using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ModMaker.Lua.Parser.Items
{
    /// <summary>
    /// Defines a parse item that represents a function call expression or statement.
    /// </summary>
    public sealed class FuncCallItem : IParseStatement, IParsePrefixExp
    {
        List<ArgumentInfo> args;
        IParseExp prefix;

        /// <summary>
        /// Contains information about an argument passed to the function.
        /// </summary>
        public struct ArgumentInfo
        {
            /// <summary>
            /// Creates a new instance of ArgumentInfo.
            /// </summary>
            /// <param name="exp">The expression for the argument.</param>
            /// <param name="byRef">Whether the argument is passed by-ref.</param>
            public ArgumentInfo(IParseExp exp, bool byRef)
            {
                Expression = exp;
                IsByRef = byRef;
            }

            /// <summary>
            /// Contains the expression for the argument.
            /// </summary>
            public readonly IParseExp Expression;
            /// <summary>
            /// Contains whether the argument is passed by-ref.
            /// </summary>
            public readonly bool IsByRef;
        }

        /// <summary>
        /// Creates a new instance of FuncCallItem with the given state.
        /// </summary>
        /// <param name="prefix">The prefix expression that defines the call.</param>
        /// <exception cref="System.ArgumentException">If prefix is not an 
        /// expression or prefix-expression.</exception>
        /// <exception cref="System.ArgumentNullException">If prefix is null.</exception>
        public FuncCallItem(IParseExp prefix)
            : this(prefix, null, -1) { }
        /// <summary>
        /// Creates a new instance of FuncCallItem with the given state.
        /// </summary>
        /// <param name="prefix">The prefix expression that defines the call.</param>
        /// <param name="instance">The string instance call name or null if
        /// not an instance call.</param>
        /// <param name="overload">The zero-based index of the overload to call,
        /// or negative to use overload resolution.</param>
        /// <exception cref="System.ArgumentException">If prefix is not an 
        /// expression or prefix-expression.</exception>
        /// <exception cref="System.ArgumentNullException">If prefix is null.</exception>
        public FuncCallItem(IParseExp prefix, string instance, int overload)
        {
            if (prefix == null)
                throw new ArgumentNullException("prefix");

            this.prefix = prefix;
            this.args = new List<ArgumentInfo>();
            this.InstanceName = instance;
            this.IsTailCall = false;
            this.IsLastArgSingle = false;
            this.Overload = overload;
        }

        /// <summary>
        /// Gets the arguments that are passed to the call.  The first item
        /// is the expression that defines the value, the second item is
        /// whether the argument is passed by-reference.
        /// </summary>
        public ReadOnlyCollection<ArgumentInfo> Arguments
        { get { return new ReadOnlyCollection<ArgumentInfo>(args); } }
        /// <summary>
        /// Gets or sets the prefix expression that defines what object to call.
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
        /// Gets or sets whether this is a tail call. This value is not checked
        /// for validity during compilation and may cause errors if changed.
        /// </summary>
        public bool IsTailCall { get; set; }
        /// <summary>
        /// Gets or sets whether this represents a statement.  This value is
        /// not checked for validity during compilation and may cause errors
        /// if changed.
        /// </summary>
        public bool Statement { get; set; }
        /// <summary>
        /// Gets or sets whether the last argument in this call should be single.  Namely that the
        /// last argument is wrapped in parentheses, e.g. foo(2, (call())).
        /// </summary>
        public bool IsLastArgSingle { get; set; }
        /// <summary>
        /// Gets or sets the instance name of the call or null if not an 
        /// instance call.
        /// </summary>
        public string InstanceName { get; set; }
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
        /// Gets or sets the overlaod of the function, use a negative number
        /// to use overload resolution.
        /// </summary>
        public int Overload { get; set; }

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
        /// Adds an argument expression to the function call.
        /// </summary>
        /// <param name="item">The expression item to add.</param>
        /// <param name="byRef">Whether the argument is passed by reference.</param>
        /// <exception cref="System.ArgumentNullException">If item is null.</exception>
        public void AddItem(IParseExp item, bool byRef)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            args.Add(new ArgumentInfo(item, byRef));
        }
    }
}
