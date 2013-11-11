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

namespace Manoli.Utils.CSharpFormat
{
	using System;
	using System.IO;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Generates color-coded HTML 4.01 from F# source code.
	/// </summary>
	public class FSharpFormat : CLikeFormat
	{
		protected override string Operators
		{
			get
			{
				return "§ [< >]|> | _ -> ->> <- [< >] [| |] [ ] <@@ @@> <@| |@> <@. .@> <@ @>";
			}
		}

		/// <summary>
		/// Regular expression string to match single line and multi-line 
		/// comments (// and (* *)). 
		/// </summary>
		protected override string CommentRegEx
		{
			get { return @"\(\*.*?\*\)|//.*?(?=\r|\n)"; }
		}


		/// <summary>
		/// The list of F# keywords.
		/// </summary>
		protected override string Keywords 
		{
			get 
			{ 
				return "abstract and as assert asr begin class default delegate do! do done downcast downto else "
					+ "end enum exception extern false finally for fun function if in inherit interface land lazy "
					+ "use! use let! let lor lsl lsr lxor match member mod module mutable namespace new null of open or override "
					+ "rec return! return sig static struct then to true try type val when inline upcast while with void yield! yield";
			}
		}

		/// <summary>
		/// Regular expression string to match string and character literals. 
		/// </summary>
		protected override string StringRegEx
		{
			get { return @"@?""""|@?"".*?(?!\\).""|''|'[^\s]*?(?!\\)'"; }
		}

		protected override string Preprocessors
		{
			get
			{
				return "#light";
			}
		}
	}
}

