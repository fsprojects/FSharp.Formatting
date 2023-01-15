/// <summary>
/// DOM low-level helper functions
/// </summary>
module Test.SeeAlso

/// <summary>
/// Dispose all given items when the parent <c>SutilElement</c> is unmounted. Each item should implement <c>System.IDisposable</c>.
///
/// See also: <seealso cref="M:Test.SeeAlso.unsubscribeOnUnmount"/>
/// </summary>
let disposeOnUnmount (ds : System.IDisposable list) =
    ignore ds

/// <summary>
/// Call each function of type `(unit -> unit)` when the element is unmounted
///
/// See also: <seealso cref="M:Test.SeeAlso.disposeOnUnmount"/>
/// </summary>
let unsubscribeOnUnmount (ds : (unit->unit) list) =
    ignore ds
