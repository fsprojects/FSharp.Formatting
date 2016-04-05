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
            get { return @"\(\*.*?\*\)|(?<!\:|\:/)//.*?(?:\r|\n|$)"; }
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
            get { return "source nuget\\s github\\s gist\\s git\\s http\\s group framework version_in_path content " 
              + "copy_local redirects import_targets references cache strategy lowest_matching NUGET GITHUB GROUP GIT HTTP specs remote File"; }
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
          
            Sanitize(sb);
            
            sb.Replace(" ", @"\b|\b");
            return @"\b" + sb.ToString() + @"\b";
        }
    }
}
