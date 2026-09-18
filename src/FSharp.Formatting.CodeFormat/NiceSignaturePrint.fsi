/// Lays a tool tip signature out over several lines when it is too wide to read on one.
///
/// A tool tip is flowing text, so an over-long signature wraps wherever the line happens to run
/// out, which lands in the middle of a parameter as often as not. This picks the break points
/// instead: after the name, between the parameters, and before the result.
///
/// The compiler decides where those points are. Every line a tip shows has the shape
/// `&lt;prefix&gt; &lt;name&gt;: &lt;type&gt;`, whether it is a `val`, a `member` or a `union case`, and while
/// none of the three parse as they stand, the type on its own always does. Feeding just that to
/// the parser as a `val` gives a <see cref="T:FSharp.Compiler.Syntax.SynType"/> whose ranges point
/// straight back into the text, so the breaks land on real syntax rather than on a guess about
/// where the brackets balance.
module internal FSharp.Formatting.CodeFormat.NiceSignaturePrint

/// Lays `runs` out over several lines when the text they carry is wider than `width`.
///
/// Returns the runs as one line when they already fit, and also when the compiler cannot read a
/// type out of them, which is the case for the lines of a tip that are not signatures at all.
val layout: width: int -> runs: (TokenKind * string) list -> (TokenKind * string) list list
