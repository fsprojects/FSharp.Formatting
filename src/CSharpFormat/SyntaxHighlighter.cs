using System;
using System.Collections.Generic;
using System.Data;
using System.Configuration;
using System.Web;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using Manoli.Utils.CSharpFormat;

namespace CSharpFormat
{
    /// <summary>
    /// Summary description for SyntaxHighlighter
    /// </summary>
    public class SyntaxHighlighter
    {
        static Regex regLang = new Regex(@"\<pre(?<attrs1>[^\>]*)lang=""(?<lang>[^""]*)""(?<attrs2>[^\>]*)\>(?<content>.*?)\</pre\>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public static string FormatHtml(string input)
        {
            Match m = regLang.Match(input);
            if (m.Success)
            {
                StringBuilder result = new StringBuilder(input.Length + 1000);
                int pos = 0;
                while (m.Success)
                {
                    var attrs1 = m.Groups["attrs1"].Value.Trim();
                    var attrs2 = m.Groups["attrs2"].Value.Trim();
                    var attrs = attrs1 + ((attrs1.Length > 0 || attrs2.Length > 0) ? " " : "") + attrs2;

                    result.Append(input.Substring(pos, m.Index - pos));
                    result.Append("<pre" + attrs + ">");
                    result.Append(FormatCode(m.Groups["lang"].Value, m.Groups["content"].Value).Item2);
                    result.Append("</pre>");
                    pos = m.Index + m.Length;
                    m = m.NextMatch();
                }
                result.Append(input.Substring(pos, input.Length - pos));
                return result.ToString();
            }
            else
            {
                return input;
            }
        }

        public static Tuple<bool, string> FormatCode(string lang, string code)
        {
            SourceFormat sf = null;
            switch (lang)
            {
                case "csharp":
                case "cs":
                    sf = new Manoli.Utils.CSharpFormat.CSharpFormat();
                    break;
                case "c++":
                case "cpp":
                    sf = new CPlusPlusFormat();
                    break;
                case "js":
                case "javascript":
                    sf = new JavaScriptFormat();
                    break;
                case "vb":
                case "basic":
                    sf = new VisualBasicFormat();
                    break;
                case "sql":
                    sf = new TsqlFormat();
                    break;
                case "msh":
                    sf = new MshFormat();
                    break;
                case "haskell":
                    sf = new HaskellFormat();
                    break;
                case "php":
                    sf = new PhpFormat();
                    break;
                case "fsharp":
                case "fs":
                    sf = new FSharpFormat();
                    break;
                case "html":
                case "xml":
                case "aspx":
                    sf = new HtmlFormat();
                    break;
            }
            if (sf == null)
            {
                return Tuple.Create(false, SourceFormat.EscapeHtml(code, tabSpaces: 2));
            }
            else
            {
                sf.TabSpaces = 2;
                return Tuple.Create(true, sf.FormatCode(code));
            }
        }
    }
}
