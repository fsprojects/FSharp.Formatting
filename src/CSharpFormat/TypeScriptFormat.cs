
namespace Manoli.Utils.CSharpFormat {

    /// <summary>
    /// Generates color-coded HTML 4.01 from TypeScript source code.
    /// </summary>
    public class TypeScriptFormat : JavaScriptFormat {

        /// <summary>
        /// The list of TypeScript keywords extends the ones from JavaScript.
        /// </summary>
        protected override string Keywords
        {
            get
            {
                return base.Keywords + " module export import declare extends implements"
                + " constructor let async await from";
            }
        }
    }
}

