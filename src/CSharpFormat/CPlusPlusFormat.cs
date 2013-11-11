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
	/// Generates color-coded HTML 4.01 from C++ source code.
	/// </summary>
	public class CPlusPlusFormat : CLikeFormat
	{
		/// <summary>
		/// The list of C++ keywords.
		/// </summary>
		protected override string Keywords 
		{
			get 
			{
				return "__abstract abstract __alignof array __asm __assume __based bool __box break case catch __cdecl char class const "+
					"const_cast continue __declspec default __delegate delegate delete deprecated dllexport dllimport do double dynamic_cast"+
					" else enum struct event __event __except explicit extern false __fastcall __finally finally float for each in __forceinline"+
					" friend friend_as __gc gcnew generic goto __hook __identifier if __if_exists __if_not_exists initonly __inline inline int"+
					" __int8 __int16 __int32 __int64 __interface interface interior_ptr __leave literal long __m64 __m128 __m128d __m128i __multiple_inheritance"+
					" mutable naked namespace new __nogc noinline __noop noreturn nothrow novtable nullptr operator __pin private __property property"+
					" protected public __raise ref register reinterpret_cast return safecast __sealed sealed selectany short signed __single_inheritance sizeof"+
					" static static_cast __stdcall struct __super switch template this thread throw true try __try __except __finally __try_cast"+
					" typedef typeid typeid typename __unaligned __unhook union unsigned using uuid __uuidof __value virtual value __virtual_inheritance void"+
					" volatile __w64 __wchar_t wchar_t while";
			}
		}

		/// <summary>
		/// The list of C++ preprocessors.
		/// </summary>
		protected override string Preprocessors
		{
			get 
			{ 
				return "#if #else #elif #endif #define #undef #warning "
					+ "#error #line #region #endregion #pragma #using #include";
			}
		}
	}
}

