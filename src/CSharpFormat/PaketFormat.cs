using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Manoli.Utils.CSharpFormat
{
    /// <summary>
    /// Generates color-coded Paket source code.
    /// </summary>
    public class PaketFormat : FSharpFormat
	{
        /// <summary>
        /// Regular expression string to match single line and multi-line 
        /// comments (// and (* *)). Single line comments should not have 
        /// a : before them to avoid color as comments URLs. For example
        /// (source https://nuget.org/api/v2)
        /// </summary>
        protected override string CommentRegEx
        {
            get { return @"\(\*.*?\*\)|(?<!\:)//.*?(?=\r|\n)"; }
        }
        
        /// <summary>
        /// Packet operators
        /// </summary>
        protected override string Operators
        {
            get { return "= == > < >= <= ~>"; }
        }
        
        /// <summary>
        /// Matches version numbers
        /// </summary>
        protected override string NumberRegEx
        {
          get { return @"\b\d+(\.\d+)*\b"; }
        }
    }
}
