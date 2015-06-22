#region Copyright © 2001-2003 Jean-Claude Manoli [jc@manoli.net]
/*
 * This software is provided 'as-is', without any express or implied warranty.
 * In no event will the author(s) be held liable for any damages arising from
 * the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 *   1. The origin of this software must not be misrepresented; you must not
 *      claim that you wrote the original software. If you use this software
 *      in a product, an acknowledgment in the product documentation would be
 *      appreciated but is not required.
 * 
 *   2. Altered source versions must be plainly marked as such, and must not
 *      be misrepresented as being the original software.
 * 
 *   3. This notice may not be removed or altered from any source distribution.
 */ 
#endregion

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Manoli.Utils.CSharpFormat
{
	/// <summary>
	/// Provides a base class for formatting most programming languages.
	/// </summary>
	public abstract class CodeFormat : SourceFormat
	{
		/// <summary>
		/// Must be overridden to provide a list of keywords defined in 
		/// each language.
		/// </summary>
		/// <remarks>
		/// Keywords must be separated with spaces.
		/// </remarks>
		protected abstract string Keywords 
		{
			get;
		}

		/// <summary>
		/// Must be overridden to provide a list of operators defined in 
		/// each language.
		/// </summary>
		/// <remarks>
		/// Operators must be separated with spaces.
		/// </remarks>
		protected virtual string Operators
		{
			get { return ""; }
		}

		/// <summary>
		/// Can be overridden to provide a list of preprocessors defined in 
		/// each language.
		/// </summary>
		/// <remarks>
		/// Preprocessors must be separated with spaces.
		/// </remarks>
		protected virtual string Preprocessors
		{
			get { return ""; }
		}


		/// <summary>
		/// Must be overridden to provide a regular expression string
		/// to match strings literals. 
		/// </summary>
		protected abstract string StringRegEx
		{
			get;
		}

		/// <summary>
		/// Must be overridden to provide a regular expression string
		/// to match comments. 
		/// </summary>
		protected abstract string CommentRegEx
		{
			get;
		}

		/// <summary>
		/// Determines if the language is case sensitive.
		/// </summary>
		/// <value><b>true</b> if the language is case sensitive, <b>false</b> 
		/// otherwise. The default is true.</value>
		/// <remarks>
		/// A case-insensitive language formatter must override this 
		/// property to return false.
		/// </remarks>
		public virtual bool CaseSensitive
		{
			get { return true; }
		}

		// Match group numbers for the Regex built in the constructor
		private const int COMMENT_GROUP = 1;
		private const int STRING_LITERAL_GROUP = 2;
		private const int PREPROCESSOR_KEYWORD_GROUP = 3;
		private const int KEYWORD_GROUP = 4;
		private const int OPERATOR_GROUP = 5;

		/// <summary>
		/// A regular expression that should never match anything.
		/// </summary>
		private const string IMPOSSIBLE_MATCH_REGEX = "(?!.*)_{37}(?<!.*)";

		/// <summary/>
		protected CodeFormat()
		{
			string regKeyword = BuildRegex(Keywords);
			string regPreproc = BuildRegex(Preprocessors);
			string regOps = BuildRegex(Operators);
			if (regOps.Length == 0) regOps = IMPOSSIBLE_MATCH_REGEX;
			if (regPreproc.Length == 0) regPreproc = IMPOSSIBLE_MATCH_REGEX;

			// Build a master regex with capturing groups.
			// Note that the group numbers must with the constants COMMENT_GROUP, OPERATOR_GROUP...!
			StringBuilder regAll = new StringBuilder();
			regAll.Append("(");
			regAll.Append(CommentRegEx);
			regAll.Append(")|(");
			regAll.Append(StringRegEx);
			regAll.Append(")|(");
			regAll.Append(regPreproc);
			regAll.Append(")|(");
			regAll.Append(regKeyword);
			regAll.Append(")|(");
			regAll.Append(regOps);
			regAll.Append(")");

			RegexOptions regexOptions = RegexOptions.Singleline;
			if (!CaseSensitive) regexOptions |= RegexOptions.IgnoreCase;
			CodeRegex = new Regex(regAll.ToString(), regexOptions);
		}

		private string BuildRegex(string separated)
		{
			if (separated.Length == 0) return "";
			var sb = new StringBuilder(separated);
			sb.Replace("&", "&amp;");
			sb.Replace("<", "&lt;");
			sb.Replace(">", "&gt;");
			foreach (char c in new char[] { '&', '?', '*', '.', '<', '>', '[', ']', '^', '|', '(', ')', '#', '+' }) {
				sb.Replace(c.ToString(), "\\" + c);
			}
			sb.Replace(" ", @"(?=\W|$)|(?<=^|\W)");
			return @"(?<=^|\W)" + sb.ToString() + @"(?=\W|$)";
		}

		/// <summary>
		/// Called to evaluate the HTML fragment corresponding to each 
		/// matching token in the code.
		/// </summary>
		/// <param name="match">The <see cref="Match"/> resulting from a 
		/// single regular expression match.</param>
		/// <returns>A string containing the HTML code fragment.</returns>
		protected override string MatchEval(Match match)
		{
			if(match.Groups[COMMENT_GROUP].Success)
			{
				StringReader reader = new StringReader(match.ToString());
				string line;
				StringBuilder sb = new StringBuilder();
				while ((line = reader.ReadLine()) != null)
				{
					if(sb.Length > 0)
					{
						sb.Append("\n");
					}
					sb.Append("<span class=\"c\">");
					sb.Append(line);
					sb.Append("</span>");
				}
				return sb.ToString();
			}
			if(match.Groups[STRING_LITERAL_GROUP].Success)
			{
				return "<span class=\"s\">" + match.ToString() + "</span>";
			}
			if(match.Groups[PREPROCESSOR_KEYWORD_GROUP].Success)
			{
				return "<span class=\"prep\">" + match.ToString() + "</span>";
			}
			if(match.Groups[KEYWORD_GROUP].Success)
			{
				return "<span class=\"k\">" + match.ToString() + "</span>";
			}
			if(match.Groups[OPERATOR_GROUP].Success)
			{
				return "<span class=\"o\">" + match.ToString() + "</span>";
			}
			System.Diagnostics.Debug.Assert(false, "None of the above!");
			return ""; //none of the above
		}
	}
}

