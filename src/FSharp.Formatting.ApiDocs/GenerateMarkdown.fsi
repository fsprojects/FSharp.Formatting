/// Internal module that generates Markdown documentation output from an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocModel"/>.
/// Produces one Markdown file per namespace and per entity, plus an index file.
module internal FSharp.Formatting.ApiDocs.GenerateMarkdown

open System
open System.IO
open System.Web
open FSharp.Formatting.Common
open FSharp.Formatting.Markdown
open FSharp.Formatting.Markdown.Dsl
open FSharp.Formatting.Templating

/// HTML-encodes a string and additionally escapes pipe characters for safe use in Markdown tables.
val encode: x: string -> string
/// URL-encodes a string (for use in anchor hrefs).
val urlEncode: x: string -> string
/// Returns the trimmed HTML text of an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocHtml"/> value.
val htmlString: x: ApiDocHtml -> string
/// Returns the trimmed HTML text of an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocHtml"/> value,
/// with newlines replaced by <c>&lt;br /&gt;</c> and pipe characters escaped for Markdown tables.
val htmlStringSafe: x: ApiDocHtml -> string
/// Wraps an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocHtml"/> value as a Markdown DSL node.
val embed: x: ApiDocHtml -> MarkdownSpan
/// Wraps an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocHtml"/> value as a Markdown DSL node,
/// escaping characters that would break Markdown table cells.
val embedSafe: x: ApiDocHtml -> MarkdownSpan
/// A Markdown DSL node representing an HTML line break.
val br: MarkdownSpan

/// Renders Markdown API documentation for all namespaces and entities in an
/// <see cref="T:FSharp.Formatting.ApiDocs.ApiDocModel"/>. Writes one file per namespace
/// and per entity to <paramref name="outDir"/> using the supplied template.
type MarkdownRender =
    internal new: model: ApiDocModel * ?menuTemplateFolder: string -> MarkdownRender
    /// The substitutions relevant to all pages, with the namespace links built for the given root
    /// (a content page deeper in the site needs a different relative root than the API pages).
    member internal GlobalSubstitutionsFor: root: string -> Substitutions
    /// Get the substitutions relevant to all
    member internal GlobalSubstitutions: Substitutions
    /// The pages of the API documentation: the output file relative to the output folder
    /// (forward slashes) and a function rendering the page for a template and global substitutions.
    /// Nothing is rendered until the function is called.
    member internal Pages: collectionName: string -> (string * (string option -> Substitutions -> string)) list

    /// Writes all API documentation Markdown files (index, one per namespace, one per entity)
    /// to <paramref name="outDir"/>, applying <paramref name="templateOpt"/> to each page.
    member internal Generate:
        outDir: string * templateOpt: string option * collectionName: string * globalParameters: Substitutions -> unit
