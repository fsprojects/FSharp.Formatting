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
	/// Generates color-coded HTML 4.01 from PHP source code.
	/// </summary>
	public class PhpFormat : CLikeFormat
	{
		/// <summary>
		/// Regular expression string to match string and character literals. 
		/// </summary>
		protected override string StringRegEx
		{
			get { return @"@?""""|@?"".*?(?!\\).""|''|'.*?(?!\\).'"; }
		}

		/// <summary>
		/// The list of PHP keywords.
		/// </summary>
		protected override string Keywords 
		{
			get 
			{ 
				return "import namespace and or xor __FILE__ __LINE__ array as break case class const continue default" +
				"die do echo else elseif empty endfor endforeach endif endswitch endwhile eval exit" +
				"extends for foreach function global if include include_once isset list new print require" +
				"require_once return static switch unset var while __FUNCTION__ __CLASS__ __METHOD__ final" +
				"interface implements extends public private protected abstract clone try catch throw";
			}
		}

		/// <summary>
		/// The list of PHP preprocessors.
		/// </summary>
		protected override string Preprocessors
		{
			get 
			{ 
				return @"<? <?php ?>";
			}
		}
	}
}

