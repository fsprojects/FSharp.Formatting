using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Manoli.Utils.CSharpFormat
{
	/// <summary>
	/// Generates color-coded HTML 4.01 from MSH (code name Monad) source code.
	/// </summary>
	public class HaskellFormat : CodeFormat
	{
		/// <summary>
		/// Regular expression string to match single line comments (#).
		/// </summary>
		protected override string CommentRegEx
		{
			get { return @"--.*?(?=\r|\n)"; }
		}

		/// <summary>
		/// Regular expression string to match string and character literals. 
		/// </summary>
		protected override string StringRegEx
		{
			get { return @"@?""""|@?"".*?(?!\\).""|''"; }
		}

		/// <summary>
		/// The list of MSH keywords.
		/// </summary>
		protected override string Keywords 
		{
			get 
			{ 
				return "class instance let where do data docase case newtype";
			}
		}

		/// <summary>
		/// Use preprocessors property to hilight operators.
		/// </summary>
		protected override string Preprocessors
		{
			get
			{
				return "\\$ - + = - < > :: && ||";
			}
		}

	}
}
