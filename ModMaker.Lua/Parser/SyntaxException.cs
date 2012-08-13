using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModMaker.Lua.Parser
{
    /// <summary>
    /// When there is an error in the syntax of a Lua chunk.
    /// </summary>
    [Serializable]
    public sealed class SyntaxException : Exception
    {
        internal SyntaxException(string message, long line, long col) 
            : base("Error in the syntax of the file.\nMessage: " + 
                message + "\nLine: " + line + "  Col: " + col) { }
        internal SyntaxException(string message, long line, long col, Exception inner)
            : base("Error in the syntax of the file.\nMessage: " + 
                message + "\nLine: " + line + "  Col: " + col, inner) { }
        internal SyntaxException(string message, long line, long col, string file)
            : base("Error in the syntax of the file.\nMessage: " + 
                message + "\nLine: " + line + "  Col: " + col + (string.IsNullOrWhiteSpace(file) ? "" : "  File: " + file)) { }
        internal SyntaxException(string message, long line, long col, string file, Exception inner)
            : base("Error in the syntax of the file.\nMessage: " + 
                message + "\nLine: " + line + "  Col: " + col + (string.IsNullOrWhiteSpace(file) ? "" : "  File: " + file), inner) { }
    }
}
