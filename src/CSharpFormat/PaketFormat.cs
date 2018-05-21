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
		/// comments (// and (* *)). Single line comments should have to have 
		/// a space after them to avoid color as comments URLs and paths. For example
		/// ```
		///     source https://nuget.org/api/v2
		///      cache //hive/dependencies
		/// ```
		/// </summary>
		protected override string CommentRegEx
		{
			get { return @"\(\*.*?\*\)|//\s.*?(?:\r|\n|$)"; }
		}
		
		/// <summary>
		/// Paket operators
		/// </summary>
		protected override string Operators
		{
			get { return "== >= <= ~> ! @ ~ : < > ="; }
		}
		
		/// <summary>
		/// Paket Keywords
		/// </summary>
		protected override string Keywords
		{
			get 
			{ 
				return "source nuget github gist git http group framework version_in_path content"
					+ " copy_local redirects import_targets references cache strategy lowest_matching NUGET"
					+ " specs remote File username password copy_content_to_output_dir GITHUB GROUP GIT HTTP"
					+ " CopyToOutputDirectory storage";
			}
		}

		/// <summary>
		/// Matches version numbers
		/// </summary>
		protected override string NumberRegEx
		{
		  get { return @"\b\d+(\.\d+)*\b"; }
		}
		
		public PaketFormat()
		{
			var regKeyword = BuildKeywordsRegex(Keywords);
			var regPreproc = BuildRegex(Preprocessors);
			var regOps = BuildRegex(Operators);
			
			if (regOps.Length == 0) regOps = IMPOSSIBLE_MATCH_REGEX;
			if (regPreproc.Length == 0) regPreproc = IMPOSSIBLE_MATCH_REGEX;

			var regAll = ConcatenateRegex(CommentRegEx, StringRegEx, regPreproc, regKeyword, regOps, NumberRegEx);

			RegexOptions regexOptions = RegexOptions.Singleline;
			if (!CaseSensitive) regexOptions |= RegexOptions.IgnoreCase;
			CodeRegex = new Regex(regAll.ToString(), regexOptions);
		}
	  
		protected string BuildKeywordsRegex(string separated)
		{
			if (separated.Length == 0) return "";
			var sb = new StringBuilder(separated);
			
			sb.Replace(" ", @"(?=\:?(?:\s|$))|(?<=\s|^)");
			return @"(?<=\s|^)" + sb.ToString() + @"(?=\:?(?:\s|$))";
		}
	}
}
