/// Internal module that generates HTML documentation output from an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocModel"/>.
/// Produces one HTML file per namespace and per entity, plus an index file.
module internal FSharp.Formatting.ApiDocs.GenerateHtml

open System
open System.Collections.Generic
open System.IO
open System.Web
open FSharp.Formatting.Common
open FSharp.Compiler.Symbols
open FSharp.Formatting.Templating
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html

/// Embed some HTML generated in GenerateModel
val embed: x: ApiDocHtml -> HtmlElement
/// Wraps the HTML text of an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocHtml"/> in a summary paragraph,
/// unless the content starts with a <c>&lt;pre&gt;</c> tag (which cannot be nested inside <c>&lt;p&gt;</c>).
val fsdocsSummary: x: ApiDocHtml -> HtmlElement
/// The part every namespace of a collection shares, up to a dot: "FSharp.Formatting." for the
/// namespaces of this repository, "Microsoft.FSharp." for FSharp.Core. Empty when they share nothing
/// or when there is only one. The menu drops it from the entries it shows, which otherwise read as a
/// column of identically truncated names.
val commonNamespacePrefix: names: string list -> string

/// The namespace whose menu entry stands for this one: itself, or the outermost documented namespace
/// it is nested in. "Fantomas.FCS.Syntax" folds into "Fantomas.FCS", so the menu that every page
/// carries stays a list of top-level namespaces rather than one line per nested namespace. The API
/// reference index is unaffected and still lists every namespace.
val menuNamespaceOf: documented: Set<string> -> name: string -> string

/// Collects the sections of one API page while its content is rendered, so the "On this page" menu
/// can be built afterwards from the very same ids. Content and menu therefore cannot drift apart.
///
/// A section is a heading that the reader can jump to. A section that lists types or modules also
/// gets an entry per row, because those lead to another page; the member tables of a type or a
/// module do not, since repeating them would only restate the page the reader is already on.
///
/// Only the headings take part in the scroll-driven highlight of the menu (`data-fsdocs-heading`):
/// each tracked element costs three generated CSS rules, so the namespaces with many types stay
/// cheap.
type internal SectionCollector =
    internal new: unit -> SectionCollector
    /// Registers a section and returns its heading element, annotated for the scroll-driven highlight.
    member internal Heading: level: int * id: string * title: string -> HtmlElement
    /// Lists an entry under the section most recently registered. The link goes wherever the row it
    /// stands for goes, which for a type or a module is its own page: jumping to the row instead
    /// would leave the reader on the page they are already reading.
    member internal Entry: href: string * text: string -> unit
    /// The menu HTML, or the empty placeholder when the page has nothing worth listing.
    member internal Menu: string

/// Renders HTML API documentation for all namespaces and entities in an
/// <see cref="T:FSharp.Formatting.ApiDocs.ApiDocModel"/>. Writes one file per namespace
/// and per entity to the output directory using the supplied template.
type HtmlRender =
    internal new: model: ApiDocModel * ?menuTemplateFolder: string -> HtmlRender
    /// The substitutions relevant to all pages, with the namespace links built for the given root
    /// (a content page deeper in the site needs a different relative root than the API pages).
    member internal GlobalSubstitutionsFor: root: string -> Substitutions
    /// Get the substitutions relevant to all
    member internal GlobalSubstitutions: Substitutions
    /// The pages of the API documentation: the output file relative to the output folder
    /// (forward slashes) and a function rendering the page for a template and global substitutions.
    /// Nothing is rendered until the function is called.
    member internal Pages: collectionName: string -> (string * (string option -> Substitutions -> string)) list

    /// Writes all API documentation HTML files (index, one per namespace, one per entity)
    /// to <paramref name="outDir"/>, applying <paramref name="templateOpt"/> to each page.
    member internal Generate:
        outDir: string * templateOpt: string option * collectionName: string * globalParameters: Substitutions -> unit
