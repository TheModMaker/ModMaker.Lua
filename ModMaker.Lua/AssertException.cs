using System;

namespace ModMaker.Lua
{
    /// <summary>
    /// Thrown when an assertion fails.
    /// </summary>
    public sealed class AssertException : Exception
    {
        /// <summary>
        /// Creates a new AssertException object.
        /// </summary>
        public AssertException() : base("Assertion failed.") { }
        /// <summary>
        /// Creates a new AssertException object with the given message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public AssertException(string message) : base(message) { }
        /// <summary>
        /// Creates a new AssertException object with the given message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference
        ///     (Nothing in Visual Basic) if no inner exception is specified.</param>
        public AssertException(string message, Exception inner) : base(message, inner) { }
    }
}