/*
 * Markdown  -  A text-to-HTML conversion tool for web writers
 * Copyright (c) 2004 John Gruber
 * http://daringfireball.net/projects/markdown/
 * 
 * Copyright (c) 2004 Michel Fortin - Translation to PHP
 * http://www.michelf.com/projects/php-markdown/
 * 
 * Copyright (c) 2004-2005 Milan Negovan - C# translation to .NET
 * http://www.aspnetresources.com
 * 
 */

#region Copyright and license

/*
Copyright (c) 2003-2004 John Gruber   
<http://daringfireball.net/>   
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

* Redistributions of source code must retain the above copyright notice,
  this list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright
  notice, this list of conditions and the following disclaimer in the
  documentation and/or other materials provided with the distribution.

* Neither the name "Markdown" nor the names of its contributors may
  be used to endorse or promote products derived from this software
  without specific prior written permission.

This software is provided by the copyright holders and contributors "as
is" and any express or implied warranties, including, but not limited
to, the implied warranties of merchantability and fitness for a
particular purpose are disclaimed. In no event shall the copyright owner
or contributors be liable for any direct, indirect, incidental, special,
exemplary, or consequential damages (including, but not limited to,
procurement of substitute goods or services; loss of use, data, or
profits; or business interruption) however caused and on any theory of
liability, whether in contract, strict liability, or tort (including
negligence or otherwise) arising in any way out of the use of this
software, even if advised of the possibility of such damage.
*/

#endregion

using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownSharp
{
    [Obsolete("This old version is included only for historical comparison purposes; use at your own risk!")]
    public class MarkdownOld
    {
        public class Pair
        {
            public Object First;
            public Object Second;
        }

        #region Class members

        private const int nestedBracketDepth = 6;
        private const string emptyElementSuffix = " />"; // Change to ">" for HTML output
        private const int tabWidth = 4;

        private static readonly string markerUL;
        private static readonly string markerOL;
        private static readonly string markerAny;

        private static readonly string nestedBrackets;
        private static readonly Hashtable escapeTable;
        private static readonly Hashtable backslashEscapeTable;

        private Hashtable urls;
        private Hashtable titles;
        private Hashtable htmlBlocks;

        private int listLevel = 0;

        #endregion

        /// <summary>
        /// Static constructor
        /// </summary>
        /// <remarks>
        /// In the static constuctor we'll initialize what stays the same across all transforms.
        /// </remarks>
        static MarkdownOld()
        {
            nestedBrackets += RepeatString(@"(?>[^\[\]]+|\[", nestedBracketDepth);
            nestedBrackets += RepeatString(@"\])*", nestedBracketDepth);

            markerUL = @"[*+-]";
            markerOL = @"\d+[.]";
            markerAny = string.Format("(?:{0}|{1})", markerUL, markerOL);

            // Table of hash values for escaped characters:
            escapeTable = new Hashtable();

            escapeTable[@"\"] = ComputeMD5(@"\");
            escapeTable["`"] = ComputeMD5("`");
            escapeTable["*"] = ComputeMD5("*");
            escapeTable["_"] = ComputeMD5("_");
            escapeTable["{"] = ComputeMD5("{");
            escapeTable["}"] = ComputeMD5("}");
            escapeTable["["] = ComputeMD5("[");
            escapeTable["]"] = ComputeMD5("]");
            escapeTable["("] = ComputeMD5("(");
            escapeTable[")"] = ComputeMD5(")");
            escapeTable[">"] = ComputeMD5(">");
            escapeTable["#"] = ComputeMD5("#");
            escapeTable["+"] = ComputeMD5("+");
            escapeTable["-"] = ComputeMD5("-");
            escapeTable["."] = ComputeMD5(".");
            escapeTable["!"] = ComputeMD5("!");

            // Create an identical table but for escaped characters.
            backslashEscapeTable = new Hashtable();

            foreach (string key in escapeTable.Keys)
                backslashEscapeTable[@"\" + key] = escapeTable[key];
        }

        public MarkdownOld()
        {
            urls = new Hashtable();
            titles = new Hashtable();
            htmlBlocks = new Hashtable();
        }

        /// <summary>
        /// Main function. The order in which other subs are called here is
        /// essential. Link and image substitutions need to happen before
        /// EscapeSpecialChars(), so that any *'s or _'s in the <a>
        /// and <img> tags get encoded.
        /// </summary>
        public string Transform(string text)
        {
            // Standardize line endings:
            // DOS to Unix and Mac to Unix
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // Make sure $text ends with a couple of newlines:
            text += "\n\n";

            // Convert all tabs to spaces.
            text = Detab(text);

            // Strip any lines consisting only of spaces and tabs.
            // This makes subsequent regexen easier to write, because we can
            // match consecutive blank lines with /\n+/ instead of something
            // contorted like /[ \t]*\n+/ .
            text = Regex.Replace(text, @"^[ \t]+$", string.Empty, RegexOptions.Multiline);

            // Turn block-level HTML blocks into hash entries
            text = HashHTMLBlocks(text);

            // Strip link definitions, store in hashes.
            text = StripLinkDefinitions(text);

            text = RunBlockGamut(text);

            text = UnescapeSpecialChars(text);

            return text + "\n";
        }

        #region Process link definitions

        /// <summary>
        /// Strips link definitions from text, stores the URLs and titles in hash references.
        /// </summary>
        /// <remarks>Link defs are in the form: ^[id]: url "optional title"</remarks>
        private string StripLinkDefinitions(string text)
        {
            string pattern = string.Format(@"
						^[ ]{{0,{0}}}\[(.+)\]:	# id = $1
						  [ \t]*
						  \n?				# maybe *one* newline
						  [ \t]*
						<?(\S+?)>?			# url = $2
						  [ \t]*
						  \n?				# maybe one newline
						  [ \t]*
						(?:
							(?<=\s)			# lookbehind for whitespace
							[\x22(]
							(.+?)			# title = $3
							[\x22)]
							[ \t]*
						)?	# title is optional
						(?:\n+|\Z)", tabWidth - 1);

            text = Regex.Replace(text, pattern, new MatchEvaluator(LinkEvaluator), RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
            return text;
        }

        private string LinkEvaluator(Match match)
        {
            string linkID = match.Groups[1].Value.ToLower();
            urls[linkID] = EncodeAmpsAndAngles(match.Groups[2].Value);

            if (match.Groups[3] != null && match.Groups[3].Length > 0)
                titles[linkID] = match.Groups[3].Value.Replace("\"", "&quot;");

            return string.Empty;
        }

        #endregion

        #region Hashify HTML blocks

        /// <summary>
        /// Hashify HTML blocks
        /// </summary>
        private string HashHTMLBlocks(string text)
        {
            /*
             We only want to do this for block-level HTML tags, such as headers,
             lists, and tables. That's because we still want to wrap <p>s around
             "paragraphs" that are wrapped in non-block-level tags, such as anchors,
             phrase emphasis, and spans. The list of tags we're looking for is
             hard-coded:
            */
            string blockTags1 = "p|div|h[1-6]|blockquote|pre|table|dl|ol|ul|script|noscript|form|fieldset|iframe|math|ins|del";
            string blockTags2 = "p|div|h[1-6]|blockquote|pre|table|dl|ol|ul|script|noscript|form|fieldset|iframe|math";

            /*
             First, look for nested blocks, e.g.:
            <div>
                <div>
                tags for inner block must be indented.
                </div>
            </div>
	        
             The outermost tags must start at the left margin for this to match, and
             the inner nested divs must be indented.
             We need to do this before the next, more liberal match, because the next
             match will start at the first `<div>` and stop at the first `</div>`.
            */
            string pattern = string.Format(@"
                (						# save in $1
					^					# start of line  (with /m)
					<({0})	            # start tag = $2
					\b					# word break
					(.*\n)*?			# any number of lines, minimally matching
					</\2>				# the matching end tag
					[ \t]*				# trailing spaces/tabs
					(?=\n+|\Z)	        # followed by a newline or end of document
				)", blockTags1);

            text = Regex.Replace(text, pattern, new MatchEvaluator(HtmlEvaluator), RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

            // Now match more liberally, simply from `\n<tag>` to `</tag>\n`
            pattern = string.Format(@"
               (						# save in $1
					^					# start of line  (with /m)
					<({0})	            # start tag = $2
					\b					# word break
					(.*\n)*?			# any number of lines, minimally matching
					.*</\2>				# the matching end tag
					[ \t]*				# trailing spaces/tabs
					(?=\n+|\Z)	        # followed by a newline or end of document
				)", blockTags2);

            text = Regex.Replace(text, pattern, new MatchEvaluator(HtmlEvaluator), RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

            // Special case just for <hr />. It was easier to make a special case than
            // to make the other regex more complicated.
            pattern = string.Format(@"
                (?:
					(?<=\n\n)		    # Starting after a blank line
					|				    # or
					\A\n?			    # the beginning of the doc
				)
				(						# save in $1
					[ ]{{0, {0}}}
					<(hr)				# start tag = $2
					\b					# word break
					([^<>])*?			#
					/?>					# the matching end tag
					[ \t]*
					(?=\n{{2,}}|\Z)		# followed by a blank line or end of document
				)", tabWidth - 1);
            text = Regex.Replace(text, pattern, new MatchEvaluator(HtmlEvaluator), RegexOptions.IgnorePatternWhitespace);

            // Special case for standalone HTML comments:
            pattern = string.Format(@"
				(?:
					(?<=\n\n)		# Starting after a blank line
					|				# or
					\A\n?			# the beginning of the doc
				)
				(						# save in $1
					[ ]{{0,{0}}}
					(?s:
						<!
						(--.*?--\s*)+
						>
					)
					[ \t]*
					(?=\n{{2,}}|\Z)		# followed by a blank line or end of document
				)", tabWidth - 1);
            text = Regex.Replace(text, pattern, new MatchEvaluator(HtmlEvaluator), RegexOptions.IgnorePatternWhitespace);

            return text;
        }

        private string HtmlEvaluator(Match match)
        {
            string text = match.Groups[1].Value;
            string key = ComputeMD5(text);
            htmlBlocks[key] = text;

            // # String that will replace the block
            return string.Concat("\n\n", key, "\n\n");
        }

        #endregion

        #region Run transformations that form block-level elements (RunBlockGamut)

        /// <summary>
        /// These are all the transformations that form block-level 
        /// tags like paragraphs, headers, and list items.
        /// </summary>
        private string RunBlockGamut(string text)
        {
            text = DoHeaders(text);

            // Do Horizontal Rules:
            text = Regex.Replace(text, @"^[ ]{0,2}([ ]?\*[ ]?){3,}[ \t]*$", "<hr" + emptyElementSuffix + "\n", RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
            text = Regex.Replace(text, @"^[ ]{0,2}([ ]? -[ ]?){3,}[ \t]*$", "<hr" + emptyElementSuffix + "\n", RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
            text = Regex.Replace(text, @"^[ ]{0,2}([ ]? _[ ]?){3,}[ \t]*$", "<hr" + emptyElementSuffix + "\n", RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);


            text = DoLists(text);
            text = DoCodeBlocks(text);
            text = DoBlockQuotes(text);

            /*
                We already ran _HashHTMLBlocks() before, in Markdown(), but that
                was to escape raw HTML in the original Markdown source. This time,
                we're escaping the markup we've just created, so that we don't wrap
                <p> tags around block-level tags.
            */
            text = HashHTMLBlocks(text);

            text = FormParagraphs(text);

            return text;
        }

        #endregion

        #region Run transformations within block-level elements (RunSpanGamut)

        /// <summary>
        /// These are all the transformations that occur *within* block-level 
        /// tags like paragraphs, headers, and list items.
        /// </summary>
        private string RunSpanGamut(string text)
        {
            text = DoCodeSpans(text);

            text = EscapeSpecialChars(text);

            // Process anchor and image tags. Images must come first,
            // because ![foo][f] looks like an anchor.
            text = DoImages(text);
            text = DoAnchors(text);

            // Make links out of things like `<http://example.com/>`
            // Must come after DoAnchors(), because you can use < and >
            // delimiters in inline links like [this](<url>).
            text = DoAutoLinks(text);

            // Fix unencoded ampersands and <'s:
            text = EncodeAmpsAndAngles(text);

            text = DoItalicsAndBold(text);

            // Do hard breaks:  
            text = Regex.Replace(text, @" {2,}\n", string.Format("<br{0}\n", emptyElementSuffix));

            return text;
        }

        #endregion

        #region Parse HTML into tokens

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text">String containing HTML markup.</param>
        /// <returns>An array of the tokens comprising the input string. Each token is 
        /// either a tag (possibly with nested, tags contained therein, such 
        /// as &lt;a href="<MTFoo>"&gt;, or a run of text between tags. Each element of the 
        /// array is a two-element array; the first is either 'tag' or 'text'; the second is 
        /// the actual value.
        /// </returns>
        private ArrayList TokenizeHTML(string text)
        {
            // Regular expression derived from the _tokenize() subroutine in 
            // Brad Choate's MTRegex plugin.
            // http://www.bradchoate.com/past/mtregex.php
            int pos = 0;
            int depth = 6;
            ArrayList tokens = new ArrayList();


            string nestedTags = string.Concat(RepeatString(@"(?:<[a-z\/!$](?:[^<>]|", depth),
                RepeatString(@")*>)", depth));
            string pattern = string.Concat(@"(?s:<!(?:--.*?--\s*)+>)|(?s:<\?.*?\?>)|", nestedTags);

            MatchCollection mc = Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (Match m in mc)
            {
                string wholeTag = m.Value;
                int tagStart = m.Index;
                Pair token = null;

                if (pos < tagStart)
                {
                    token = new Pair();
                    token.First = "text";
                    token.Second = text.Substring(pos, tagStart - pos);
                    tokens.Add(token);
                }

                token = new Pair();
                token.First = "tag";
                token.Second = wholeTag;
                tokens.Add(token);

                pos = m.Index + m.Length;
            }

            if (pos < text.Length)
            {
                Pair token = new Pair();
                token.First = "text";
                token.Second = text.Substring(pos, text.Length - pos);
                tokens.Add(token);
            }

            return tokens;
        }

        #endregion

        #region Escape special characters

        private string EscapeSpecialChars(string text)
        {
            ArrayList tokens = TokenizeHTML(text);

            // Rebuild text from the tokens
            text = string.Empty;

            foreach (Pair token in tokens)
            {
                string value = token.Second.ToString();

                if (token.First.Equals("tag"))
                    /*
                        Within tags, encode * and _ so they don't conflict with their use 
                        in Markdown for italics and strong. We're replacing each 
                        such character with its corresponding MD5 checksum value; 
                        this is likely overkill, but it should prevent us from colliding
                        with the escape values by accident.
                    */
                    value = value.Replace("*", escapeTable["*"].ToString()).Replace("_", escapeTable["_"].ToString());
                else
                    value = EncodeBackslashEscapes(value);

                text += value;
            }

            return text;
        }

        #endregion

        #region Process referenced and inline anchors

        /// <summary>
        /// Turn Markdown link shortcuts into XHTML <a> tags. 
        /// </summary>
        private string DoAnchors(string text)
        {
            //
            // First, handle reference-style links: [link text] [id]
            //
            string pattern = string.Format(@"
            (                               # wrap whole match in $1
		        \[
			        ({0})                   # link text = $2
		        \]

		        [ ]?                        # one optional space
		        (?:\n[ ]*)?                 # one optional newline followed by spaces

		        \[
			        (.*?)                   # id = $3
		        \]
		    )", nestedBrackets);

            text = Regex.Replace(text, pattern, new MatchEvaluator(AnchorReferenceEvaluator), RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);

            //
            // Next, inline-style links: [link text](url "optional title")
            //
            pattern = string.Format(@"
                (                          # wrap whole match in $1
		            \[
			            ({0})              # link text = $2
		            \]
		            \(                     # literal paren
			            [ \t]*
			            <?(.*?)>?          # href = $3
			            [ \t]*
			            (                  # $4
			            (['\x22])          # quote char = $5
			            (.*?)              # Title = $6
			            \5                 # matching quote
			            )?                 # title is optional
		            \)
        		)", nestedBrackets);

            text = Regex.Replace(text, pattern, new MatchEvaluator(AnchorInlineEvaluator), RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);

            return text;
        }

        private string AnchorReferenceEvaluator(Match match)
        {
            string wholeMatch = match.Groups[1].Value;
            string linkText = match.Groups[2].Value;
            string linkID = match.Groups[3].Value.ToLower();
            string url = null;
            string res = null;
            string title = null;

            // for shortcut links like [this][].
            if (linkID.Equals(string.Empty))
                linkID = linkText.ToLower();

            if (urls[linkID] != null)
            {
                url = urls[linkID].ToString();

                //We've got to encode these to avoid conflicting with italics/bold.
                url = url.Replace("*", escapeTable["*"].ToString()).Replace("_", escapeTable["_"].ToString());
                res = string.Format("<a href=\"{0}\"", url);

                if (titles[linkID] != null)
                {
                    title = titles[linkID].ToString();
                    title = title.Replace("*", escapeTable["*"].ToString()).Replace("_", escapeTable["_"].ToString());
                    res += string.Format(" title=\"{0}\"", title);
                }

                res += string.Format(">{0}</a>", linkText);
            }
            else
                res = wholeMatch;

            return res;
        }

        private string AnchorInlineEvaluator(Match match)
        {
            string linkText = match.Groups[2].Value;
            string url = match.Groups[3].Value;
            string title = match.Groups[6].Value;
            string res = null;

            // We've got to encode these to avoid conflicting with italics/bold.
            url = url.Replace("*", escapeTable["*"].ToString()).Replace("_", escapeTable["_"].ToString());
            res = string.Format("<a href=\"{0}\"", url);

            if (title != null && title.Length > 0)
            {
                title = title.Replace("\"", "&quot;").Replace("*", escapeTable["*"].ToString()).Replace("_", escapeTable["_"].ToString());
                res += string.Format(" title=\"{0}\"", title);
            }

            res += string.Format(">{0}</a>", linkText);
            return res;
        }

        #endregion

        #region Process inline and referenced images

        /// <summary>
        /// Turn Markdown image shortcuts into <img> tags. 
        /// </summary>
        private string DoImages(string text)
        {
            // First, handle reference-style labeled images: ![alt text][id]
            string pattern = @"
                    (               # wrap whole match in $1
		            !\[
			            (.*?)	    # alt text = $2
		            \]

		            [ ]?            # one optional space
		            (?:\n[ ]*)?		# one optional newline followed by spaces

		            \[
			            (.*?)       # id = $3
		            \]

		            )";

            text = Regex.Replace(text, pattern, new MatchEvaluator(ImageReferenceEvaluator), RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

            // Next, handle inline images:  ![alt text](url "optional title")
            // Don't forget: encode * and _
            pattern = @"
                (				# wrap whole match in $1
		        !\[
			        (.*?)		# alt text = $2
		        \]
		        \(			    # literal paren
			        [ \t]*
			        <?(\S+?)>?	# src url = $3
			        [ \t]*
			        (			# $4
			        (['\x22])	# quote char = $5
			        (.*?)		# title = $6
			        \5		    # matching quote
			        [ \t]*
			        )?			# title is optional
		        \)
		        )";

            text = Regex.Replace(text, pattern, new MatchEvaluator(ImageInlineEvaluator), RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

            return text;
        }

        private string ImageReferenceEvaluator(Match match)
        {
            string wholeMatch = match.Groups[1].Value;
            string altText = match.Groups[2].Value;
            string linkID = match.Groups[3].Value.ToLower();
            string url = null;
            string res = null;
            string title = null;

            // for shortcut links like ![this][].
            if (linkID.Equals(string.Empty))
                linkID = altText.ToLower();

            altText = altText.Replace("\"", "&quot;");

            if (urls[linkID] != null)
            {
                url = urls[linkID].ToString();

                // We've got to encode these to avoid conflicting with italics/bold.
                url = url.Replace("*", escapeTable["*"].ToString()).Replace("_", escapeTable["_"].ToString());
                res = string.Format("<img src=\"{0}\" alt=\"{1}\"", url, altText);

                if (titles[linkID] != null)
                {
                    title = titles[linkID].ToString();
                    title = title.Replace("*", escapeTable["*"].ToString()).Replace("_", escapeTable["_"].ToString());

                    res += string.Format(" title=\"{0}\"", title);
                }

                res += emptyElementSuffix;
            }
            else
            {
                // If there's no such link ID, leave intact:
                res = wholeMatch;
            }

            return res;
        }

        private string ImageInlineEvaluator(Match match)
        {
            string altText = match.Groups[2].Value;
            string url = match.Groups[3].Value;
            string title = match.Groups[6].Value;
            string res = null;


            altText = altText.Replace("\"", "&quot;");
            title = title.Replace("\"", "&quot;");

            // We've got to encode these to avoid conflicting with italics/bold.
            url = url.Replace("*", escapeTable["*"].ToString()).Replace("_", escapeTable["_"].ToString());
            res = string.Format("<img src=\"{0}\" alt=\"{1}\"", url, altText);

            title = title.Replace("*", escapeTable["*"].ToString()).Replace("_", escapeTable["_"].ToString());
            res += string.Format(" title=\"{0}\"", title);

            res += emptyElementSuffix;
            return res;
        }

        #endregion

        #region Process headers

        private string DoHeaders(string text)
        {
            /*
            Setext-style headers:
            
            Header 1
            ========
	          
            Header 2
            --------
            */

            text = Regex.Replace(text, @"^(.+)[ \t]*\n=+[ \t]*\n+", new MatchEvaluator(SetextHeader1Evaluator), RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
            text = Regex.Replace(text, @"^(.+)[ \t]*\n-+[ \t]*\n+", new MatchEvaluator(SetextHeader2Evaluator), RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

            /*
             atx-style headers:
                # Header 1
                ## Header 2
                ## Header 2 with closing hashes ##
                ...
                ###### Header 6
            */
            string pattern = @"
                ^(\#{1,6})	# $1 = string of #'s
			    [ \t]*
			    (.+?)		# $2 = Header text
			    [ \t]*
			    \#*			# optional closing #'s (not counted)
			    \n+";

            text = Regex.Replace(text, pattern, new MatchEvaluator(AtxHeaderEvaluator), RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

            return text;
        }

        private string SetextHeader1Evaluator(Match match)
        {
            string header = match.Groups[1].Value;
            return string.Concat("<h1>", RunSpanGamut(header), "</h1>\n\n");
        }

        private string SetextHeader2Evaluator(Match match)
        {
            string header = match.Groups[1].Value;
            return string.Concat("<h2>", RunSpanGamut(header), "</h2>\n\n");
        }

        private string AtxHeaderEvaluator(Match match)
        {
            string headerSig = match.Groups[1].Value;
            string headerText = match.Groups[2].Value;

            return string.Concat("<h", headerSig.Length, ">", RunSpanGamut(headerText), "</h", headerSig.Length, ">\n\n");
        }

        #endregion

        #region Process ordered and unordered lists

        private string DoLists(string text)
        {
            // Re-usable pattern to match any entirel ul or ol list:
            string pattern = null;

            string wholeList = string.Format(@"
			(                               # $1 = whole list
			  (                             # $2
				[ ]{{0,{1}}}
			    ({0})                       # $3 = first list item marker
				[ \t]+
			  )
			  (?s:.+?)
			  (                             # $4
				  \z
				|
				  \n{{2,}}
				  (?=\S)
				  (?!                       # Negative lookahead for another list item marker
				  	[ \t]*
				  	{0}[ \t]+
				  )
			  )
			)", markerAny, tabWidth - 1);

            // We use a different prefix before nested lists than top-level lists.
            // See extended comment in _ProcessListItems().
            if (listLevel > 0)
            {
                pattern = "^" + wholeList;
                text = Regex.Replace(text, pattern, new MatchEvaluator(ListEvaluator), RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
            }
            else
            {
                pattern = @"(?:(?<=\n\n)|\A\n?)" + wholeList;
                text = Regex.Replace(text, pattern, new MatchEvaluator(ListEvaluator), RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
            }

            return text;
        }

        private string ListEvaluator(Match match)
        {
            string list = match.Groups[1].Value;
            string listType = Regex.IsMatch(match.Groups[3].Value, markerUL) ? "ul" : "ol";
            string result = null;

            // Turn double returns into triple returns, so that we can make a
            // paragraph for the last item in a list, if necessary:
            list = Regex.Replace(list, @"\n{2,}", "\n\n\n");
            result = ProcessListItems(list, markerAny);
            result = string.Format("<{0}>\n{1}</{0}>\n", listType, result);

            return result;
        }

        /// <summary>
        /// Process the contents of a single ordered or unordered list, splitting it
        /// into individual list items.
        /// </summary>
        private string ProcessListItems(string list, string marker)
        {
            /*
	            The listLevel global keeps track of when we're inside a list.
	            Each time we enter a list, we increment it; when we leave a list,
	            we decrement. If it's zero, we're not in a list anymore.
	        
	            We do this because when we're not inside a list, we want to treat
	            something like this:
	        
	            	I recommend upgrading to version
	            	8. Oops, now this line is treated
	            	as a sub-list.
	        
	            As a single paragraph, despite the fact that the second line starts
	            with a digit-period-space sequence.
	        
	            Whereas when we're inside a list (or sub-list), that line will be
	            treated as the start of a sub-list. What a kludge, huh? This is
	            an aspect of Markdown's syntax that's hard to parse perfectly
	            without resorting to mind-reading. Perhaps the solution is to
	            change the syntax rules such that sub-lists must start with a
	            starting cardinal number; e.g. "1." or "a.".
            */

            listLevel++;

            // Trim trailing blank lines:
            list = Regex.Replace(list, @"\n{2,}\z", "\n");

            string pattern = string.Format(
              @"(\n)?                      # leading line = $1
                (^[ \t]*)                  # leading whitespace = $2
                ({0}) [ \t]+               # list marker = $3
		        ((?s:.+?)                  # list item text = $4
                (\n{{1,2}}))      
		        (?= \n* (\z | \2 ({0}) [ \t]+))", marker);

            list = Regex.Replace(list, pattern, new MatchEvaluator(ListEvaluator2),
                                  RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
            listLevel--;
            return list;
        }

        private string ListEvaluator2(Match match)
        {
            string item = match.Groups[4].Value;
            string leadingLine = match.Groups[1].Value;


            if ((leadingLine != null && leadingLine != string.Empty) || Regex.IsMatch(item, @"\n{2,}"))
                item = RunBlockGamut(Outdent(item));
            else
            {
                // Recursion for sub-lists:
                item = DoLists(Outdent(item));
                item = item.TrimEnd('\n');
                item = RunSpanGamut(item);
            }

            return string.Format("<li>{0}</li>\n", item);
        }

        #endregion

        #region Process code blocks

        private string DoCodeBlocks(string text)
        {
            // TODO: Should we allow 2 empty lines here or only one?
            string pattern = string.Format(@"
                    (?:\n\n|\A)
			        (	                     # $1 = the code block -- one or more lines, starting with a space/tab
			        (?:
				        (?:[ ]{{{0}}} | \t)  # Lines must start with a tab or a tab-width of spaces
				        .*\n+
			        )+
			        )
			        ((?=^[ ]{{0,{0}}}\S)|\Z) # Lookahead for non-space at line-start, or end of doc",
                                            tabWidth);

            text = Regex.Replace(text, pattern,
                                  new MatchEvaluator(CodeBlockEvaluator),
                                  RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

            return text;
        }

        private string CodeBlockEvaluator(Match match)
        {
            string codeBlock = match.Groups[1].Value;
            codeBlock = EncodeCode(Outdent(codeBlock));

            // Trim leading newlines and trailing whitespace
            codeBlock = Regex.Replace(codeBlock, @"^\n+", string.Empty);
            codeBlock = Regex.Replace(codeBlock, @"\s+\z", string.Empty);

            return string.Concat("\n\n<pre><code>", codeBlock, "\n</code></pre>\n\n");
        }

        #endregion

        #region Process code spans

        private string DoCodeSpans(string text)
        {
            /*
                *	Backtick quotes are used for <code></code> spans.
                *	You can use multiple backticks as the delimiters if you want to
                    include literal backticks in the code span. So, this input:

                    Just type ``foo `bar` baz`` at the prompt.
        
                    Will translate to:
        
                      <p>Just type <code>foo `bar` baz</code> at the prompt.</p>
        
                    There's no arbitrary limit to the number of backticks you
                    can use as delimters. If you need three consecutive backticks
                    in your code, use four for delimiters, etc.
        
                *	You can use spaces to get literal backticks at the edges:
        
                      ... type `` `bar` `` ...
        
                    Turns to:
        
                      ... type <code>`bar`</code> ...	        
            */

            string pattern = @"
                    (`+)		# $1 = Opening run of `
			        (.+?)		# $2 = The code block
			        (?<!`)
			        \1
			        (?!`)";
            text = Regex.Replace(text, pattern,
                                  new MatchEvaluator(CodeSpanEvaluator),
                                  RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

            return text;
        }

        private string CodeSpanEvaluator(Match match)
        {
            string s = match.Groups[2].Value;
            s = s.Replace(@"^[ \t]*", string.Empty).Replace(@"[ \t]*$", string.Empty);
            s = EncodeCode(s);

            return string.Concat("<code>", s, "</code>");
        }

        #endregion

        #region Encode/escape certain characters inside Markdown code runs

        /// <summary>
        /// Encode/escape certain characters inside Markdown code runs.
        /// </summary>
        /// <remarks>
        /// The point is that in code, these characters are literals, and lose their 
        /// special Markdown meanings.
        /// </remarks>
        private string EncodeCode(string code)
        {
            code = code.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

            foreach (string key in escapeTable.Keys)
                code = code.Replace(key, escapeTable[key].ToString());

            return code;
        }

        #endregion

        #region Process bold and italics

        private string DoItalicsAndBold(string text)
        {
            // <strong> must go first:
            text = Regex.Replace(text, @"(\*\*|__) (?=\S) (.+?[*_]*) (?<=\S) \1",
                                  new MatchEvaluator(BoldEvaluator),
                                  RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

            // Then <em>:
            text = Regex.Replace(text, @"(\*|_) (?=\S) (.+?) (?<=\S) \1",
                                  new MatchEvaluator(ItalicsEvaluator),
                                  RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);
            return text;
        }

        private string ItalicsEvaluator(Match match)
        {
            return string.Format("<em>{0}</em>", match.Groups[2].Value);
        }

        private string BoldEvaluator(Match match)
        {
            return string.Format("<strong>{0}</strong>", match.Groups[2].Value);
        }

        #endregion

        #region Process blockquotes

        private string DoBlockQuotes(string text)
        {
            string pattern =
                @"(				        # Wrap whole match in $1
			    (
			    ^[ \t]*>[ \t]?			# '>' at the start of a line
				    .+\n				# rest of the first line
			    (.+\n)*					# subsequent consecutive lines
			    \n*						# blanks
			    )+
		    )";

            text = Regex.Replace(text, pattern, new MatchEvaluator(BlockQuoteEvaluator), RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline);
            return text;
        }

        private string BlockQuoteEvaluator(Match match)
        {
            string bq = match.Groups[1].Value;

            // Trim one level of quoting - trim whitespace-only lines
            bq = Regex.Replace(bq, @"^[ \t]*>[ \t]?", string.Empty, RegexOptions.Multiline);
            bq = Regex.Replace(bq, @"^[ \t]+$", string.Empty, RegexOptions.Multiline);

            bq = RunBlockGamut(bq);
            bq = Regex.Replace(bq, @"^", "  ", RegexOptions.Multiline);

            // These leading spaces screw with <pre> content, so we need to fix that:
            bq = Regex.Replace(bq, @"(\s*<pre>.+?</pre>)", new MatchEvaluator(BlockQuoteEvaluator2), RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

            return string.Format("<blockquote>\n{0}\n</blockquote>\n\n", bq);
        }

        private string BlockQuoteEvaluator2(Match match)
        {
            string pre = match.Groups[1].Value;
            pre = Regex.Replace(pre, @"^  ", string.Empty, RegexOptions.Multiline);

            return pre;
        }

        #endregion

        #region Create paragraph tags

        private string FormParagraphs(string text)
        {
            // Strip leading and trailing lines:
            text = Regex.Replace(text, @"^\n+", string.Empty);
            text = Regex.Replace(text, @"\n+\z", string.Empty);

            string[] grafs = Regex.Split(text, @"\n{2,}");

            // Wrap <p> tags.
            for (int i = 0; i < grafs.Length; i++)
            {
                // Milan Negovan: I'm adding an additional check for an empty block of code.
                // Otherwise an empty <p></p> is created.
                if (htmlBlocks[grafs[i]] == null && grafs[i].Length > 0)
                {
                    string block = grafs[i];

                    block = RunSpanGamut(block);
                    block = Regex.Replace(block, @"^([ \t]*)", "<p>");
                    block += "</p>";

                    grafs[i] = block;
                }
            }

            // Unhashify HTML blocks
            for (int i = 0; i < grafs.Length; i++)
            {
                string block = (string)htmlBlocks[grafs[i]];

                if (block != null)
                    grafs[i] = block;
            }

            return string.Join("\n\n", grafs);

        }

        #endregion

        #region Process emails and links

        private string DoAutoLinks(string text)
        {
            text = Regex.Replace(text, "<((https?|ftp):[^'\">\\s]+)>", new MatchEvaluator(HyperlinkEvaluator));

            // Email addresses: <address@domain.foo>
            string pattern =
                @"<
                (?:mailto:)?
		        (
			        [-.\w]+
			        \@
			        [-a-z0-9]+(\.[-a-z0-9]+)*\.[a-z]+
		        )
		        >";

            text = Regex.Replace(text, pattern, new MatchEvaluator(EmailEvaluator), RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

            return text;
        }

        private string HyperlinkEvaluator(Match match)
        {
            string link = match.Groups[1].Value;
            return string.Format("<a href=\"{0}\">{0}</a>", link);
        }

        private string EmailEvaluator(Match match)
        {
            string email = UnescapeSpecialChars(match.Groups[1].Value);

            /*
                Input: an email address, e.g. "foo@example.com"
            
                Output: the email address as a mailto link, with each character
                        of the address encoded as either a decimal or hex entity, in
                        the hopes of foiling most address harvesting spam bots. E.g.:
            
                  <a href="&#x6D;&#97;&#105;&#108;&#x74;&#111;:&#102;&#111;&#111;&#64;&#101;
                    x&#x61;&#109;&#x70;&#108;&#x65;&#x2E;&#99;&#111;&#109;">&#102;&#111;&#111;
                    &#64;&#101;x&#x61;&#109;&#x70;&#108;&#x65;&#x2E;&#99;&#111;&#109;</a>
            
                Based by a filter by Matthew Wickline, posted to the BBEdit-Talk
                mailing list: <http://tinyurl.com/yu7ue>
            
             */
            email = "mailto:" + email;

            // leave ':' alone (to spot mailto: later) 
            email = Regex.Replace(email, @"([^\:])", new MatchEvaluator(EncodeEmailEvaluator));

            email = string.Format("<a href=\"{0}\">{0}</a>", email);

            // strip the mailto: from the visible part
            email = Regex.Replace(email, "\">.+?:", "\">");
            return email;
        }

        private string EncodeEmailEvaluator(Match match)
        {
            char c = Convert.ToChar(match.Groups[1].Value);

            Random rnd = new Random();
            int r = rnd.Next(0, 100);

            // Original author note:
            // Roughly 10% raw, 45% hex, 45% dec 
            // '@' *must* be encoded. I insist.
            if (r > 90 && c != '@') return c.ToString();
            if (r < 45) return string.Format("&#x{0:x};", (int)c);

            return string.Format("&#x{0:x};", (int)c);
        }

        #endregion

        #region EncodeAmpsAndAngles, EncodeBackslashEscapes, UnescapeSpecialChars, Outdent, UnslashQuotes

        /// <summary>
        /// Smart processing for ampersands and angle brackets that need to be encoded.
        /// </summary>
        private string EncodeAmpsAndAngles(string text)
        {
            // Ampersand-encoding based entirely on Nat Irons's Amputator MT plugin:
            // http://bumppo.net/projects/amputator/

            text = Regex.Replace(text, @"&(?!#?[xX]?(?:[0-9a-fA-F]+|\w+);)", "&amp;");

            // Encode naked <'s
            text = Regex.Replace(text, @"<(?![a-z/?\$!])", "&lt;", RegexOptions.IgnoreCase);

            return text;
        }

        private string EncodeBackslashEscapes(string value)
        {
            // Must process escaped backslashes first.
            foreach (string key in backslashEscapeTable.Keys)
                value = value.Replace(key, backslashEscapeTable[key].ToString());

            return value;
        }

        /// <summary>
        /// Swap back in all the special characters we've hidden. 
        /// </summary>
        private string UnescapeSpecialChars(string text)
        {
            foreach (string key in escapeTable.Keys)
                text = text.Replace(escapeTable[key].ToString(), key);

            return text;
        }

        /// <summary>
        /// Remove one level of line-leading tabs or spaces
        /// </summary>
        private string Outdent(string block)
        {
            return Regex.Replace(block, @"^(\t|[ ]{1," + tabWidth.ToString() + @"})", string.Empty, RegexOptions.Multiline);
        }
        #endregion

        #region Replace tabs with spaces and pad them to tab width

        private string Detab(string text)
        {
            // Inspired from a post by Bart Lateur: 
            // http://www.nntp.perl.org/group/perl.macperl.anyperl/154
            return Regex.Replace(text, @"^(.*?)\t", new MatchEvaluator(TabEvaluator), RegexOptions.Multiline);
        }

        private string TabEvaluator(Match match)
        {
            string leading = match.Groups[1].Value;
            return string.Concat(leading, RepeatString(" ", tabWidth - leading.Length % tabWidth));
        }

        #endregion

        #region Helper methods (RepeatString & ComputeMD5)

        /// <summary>
        /// This is to emulate what's evailable in PHP
        /// </summary>
        /// <param name="text"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private static string RepeatString(string text, int count)
        {
            string res = null;

            for (int i = 0; i < count; i++)
                res += text;

            return res;
        }

        /// <summary>
        /// Calculate an MD5 hash of an arbitrary string
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static string ComputeMD5(string text)
        {
            MD5 algo = MD5.Create();
            byte[] plainText = Encoding.UTF8.GetBytes(text);
            byte[] hashedText = algo.ComputeHash(plainText);
            string res = null;

            foreach (byte b in hashedText)
                res += b.ToString("x2");

            return res;
        }
        #endregion
    }
}