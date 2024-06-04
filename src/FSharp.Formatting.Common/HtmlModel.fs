namespace FSharp.Formatting.HtmlModel


type internal HtmlProperties =
    | DefaultChecked of bool
    | DefaultValue of string
    | Accept of string
    | AcceptCharset of string
    | AccessKey of string
    | Action of string
    | AllowFullScreen of bool
    | AllowTransparency of bool
    | Alt of string
    | Async of bool
    | AutoComplete of string
    | AutoFocus of bool
    | AutoPlay of bool
    | Capture of bool
    | CellPadding of string
    | CellSpacing of string
    | CharSet of string
    | Challenge of string
    | Checked of bool
    | ClassID of string
    | Class of string
    | Cols of float
    | ColSpan of float
    | Content of string
    | ContentEditable of bool
    | ContextMenu of string
    | Controls of bool
    | Coords of string
    | CrossOrigin of string
    | Ddata of string
    | DateTime of string
    | Default of bool
    | Defer of bool
    | Dir of string
    | Disabled of bool
    | Download of string
    | Draggable of bool
    | EncType of string
    | Form of string
    | FormAction of string
    | FormEncType of string
    | FormMethod of string
    | FormNoValidate of bool
    | FormTarget of string
    | FrameBorder of string
    | Headers of string
    | Height of string
    | Hidden of bool
    | High of float
    | Href of string
    | HrefLang of string
    | HtmlFor of string
    | HttpEquiv of string
    | Icon of string
    | Id of string
    | InputMode of string
    | Integrity of string
    | Is of string
    | KeyParams of string
    | KeyType of string
    | Kind of string
    | Label of string
    | Lang of string
    | Language of string
    | List of string
    | Loop of bool
    | Low of float
    | Manifest of string
    | MarginHeight of float
    | MarginWidth of float
    | Max of string
    | MaxLength of float
    | Media of string
    | MediaGroup of string
    | Method of string
    | Min of string
    | MinLength of float
    | Multiple of bool
    | Muted of bool
    | Name of string
    | NoValidate of bool
    | OnClick of string
    | OnMouseOut of string
    | OnMouseOver of string
    | Open of bool
    | Optimum of float
    | Pattern of string
    | Placeholder of string
    | Poster of string
    | Preload of string
    | RadioGroup of string
    | ReadOnly of bool
    | Rel of string
    | Required of bool
    | Role of string
    | Rows of float
    | RowSpan of float
    | Sandbox of string
    | Scope of string
    | Scoped of bool
    | Scrolling of string
    | Seamless of bool
    | Selected of bool
    | Shape of string
    | Size of float
    | Sizes of string
    | Span of float
    | SpellCheck of bool
    | Src of string
    | SrcDoc of string
    | SrcLang of string
    | SrcSet of string
    | Start of float
    | Step of string
    //| Style of CSSProperties list
    | Summary of string
    | TabIndex of float
    | Target of string
    | Title of string
    | Type of string
    | UseMap of string
    | Value of string
    | Width of string
    | Wmode of string
    | Wrap of string
    | About of string
    | Datatype of string
    | Inlist of string
    | Prefix of string
    | Property of string
    | Resource of string
    | Typeof of string
    | Vocab of string
    | AutoCapitalize of string
    | AutoCorrect of string
    | AutoSave of string
    | Color of string
    | ItemProp of string
    | ItemScope of bool
    | ItemType of string
    | ItemID of string
    | ItemRef of string
    | Results of float
    | Security of string
    | Unselectable of bool
    | Custom of name: string * value: string

    override s.ToString() =
        match s with
        | DefaultChecked s -> sprintf "defaultChecked=\"%s\"" (if s then "true" else "false")
        | DefaultValue s -> sprintf "defaultValue=\"%s\"" s
        | Accept s -> sprintf "accept=\"%s\"" s
        | AcceptCharset s -> sprintf "acceptCharset=\"%s\"" s
        | AccessKey s -> sprintf "accessKey=\"%s\"" s
        | Action s -> sprintf "action=\"%s\"" s
        | AllowFullScreen s -> sprintf "allowFullScreen=\"%s\"" (if s then "true" else "false")
        | AllowTransparency s -> sprintf "allowTransparency=\"%s\"" (if s then "true" else "false")
        | Alt s -> sprintf "alt=\"%s\"" s
        | Async s -> sprintf "async=\"%s\"" (if s then "true" else "false")
        | AutoComplete s -> sprintf "autoComplete=\"%s\"" s
        | AutoFocus s -> sprintf "autoFocus=\"%s\"" (if s then "true" else "false")
        | AutoPlay s -> sprintf "autoPlay=\"%s\"" (if s then "true" else "false")
        | Capture s -> sprintf "capture=\"%s\"" (if s then "true" else "false")
        | CellPadding s -> sprintf "cellPadding=\"%s\"" s
        | CellSpacing s -> sprintf "cellSpacing=\"%s\"" s
        | CharSet s -> sprintf "charSet=\"%s\"" s
        | Challenge s -> sprintf "challenge=\"%s\"" s
        | Checked s -> sprintf "checked=\"%s\"" (if s then "true" else "false")
        | ClassID s -> sprintf "classID=\"%s\"" s
        | Class s -> sprintf "class=\"%s\"" s
        | Cols s -> sprintf "cols=\"%g\"" s
        | ColSpan s -> sprintf "colSpan=\"%g\"" s
        | Content s -> sprintf "content=\"%s\"" s
        | ContentEditable s -> sprintf "contentEditable=\"%s\"" (if s then "true" else "false")
        | ContextMenu s -> sprintf "contextMenu=\"%s\"" s
        | Controls s -> sprintf "controls=\"%s\"" (if s then "true" else "false")
        | Coords s -> sprintf "coords=\"%s\"" s
        | CrossOrigin s -> sprintf "crossOrigin=\"%s\"" s
        | Ddata s -> sprintf "ddata=\"%s\"" s
        | DateTime s -> sprintf "dateTime=\"%s\"" s
        | Default s -> sprintf "default=\"%s\"" (if s then "true" else "false")
        | Defer s -> sprintf "defer=\"%s\"" (if s then "true" else "false")
        | Dir s -> sprintf "dir=\"%s\"" s
        | Disabled s -> sprintf "disabled=\"%s\"" (if s then "true" else "false")
        | Download s -> sprintf "download=\"%s\"" s
        | Draggable s -> sprintf "draggable=\"%s\"" (if s then "true" else "false")
        | EncType s -> sprintf "encType=\"%s\"" s
        | Form s -> sprintf "form=\"%s\"" s
        | FormAction s -> sprintf "formAction=\"%s\"" s
        | FormEncType s -> sprintf "formEncType=\"%s\"" s
        | FormMethod s -> sprintf "formMethod=\"%s\"" s
        | FormNoValidate s -> sprintf "formNoValidate=\"%s\"" (if s then "true" else "false")
        | FormTarget s -> sprintf "formTarget=\"%s\"" s
        | FrameBorder s -> sprintf "frameBorder=\"%s\"" s
        | Headers s -> sprintf "headers=\"%s\"" s
        | Height s -> sprintf "height=\"%s\"" s
        | Hidden s -> sprintf "hidden=\"%s\"" (if s then "true" else "false")
        | High s -> sprintf "high=\"%g\"" s
        | Href s -> sprintf "href=\"%s\"" s
        | HrefLang s -> sprintf "hrefLang=\"%s\"" s
        | HtmlFor s -> sprintf "htmlFor=\"%s\"" s
        | HttpEquiv s -> sprintf "httpEquiv=\"%s\"" s
        | Icon s -> sprintf "icon=\"%s\"" s
        | Id s -> sprintf "id=\"%s\"" s
        | InputMode s -> sprintf "inputMode=\"%s\"" s
        | Integrity s -> sprintf "integrity=\"%s\"" s
        | Is s -> sprintf "is=\"%s\"" s
        | KeyParams s -> sprintf "keyParams=\"%s\"" s
        | KeyType s -> sprintf "keyType=\"%s\"" s
        | Kind s -> sprintf "kind=\"%s\"" s
        | Label s -> sprintf "label=\"%s\"" s
        | Lang s -> sprintf "lang=\"%s\"" s
        | Language s -> sprintf "language=\"%s\"" s
        | List s -> sprintf "list=\"%s\"" s
        | Loop s -> sprintf "loop=\"%s\"" (if s then "true" else "false")
        | Low s -> sprintf "low=\"%g\"" s
        | Manifest s -> sprintf "manifest=\"%s\"" s
        | MarginHeight s -> sprintf "marginHeight=\"%g\"" s
        | MarginWidth s -> sprintf "marginWidth=\"%g\"" s
        | Max s -> sprintf "max=\"%s\"" s
        | MaxLength s -> sprintf "maxLength=\"%g\"" s
        | Media s -> sprintf "media=\"%s\"" s
        | MediaGroup s -> sprintf "mediaGroup=\"%s\"" s
        | Method s -> sprintf "method=\"%s\"" s
        | Min s -> sprintf "min=\"%s\"" s
        | MinLength s -> sprintf "minLength=\"%g\"" s
        | Multiple s -> sprintf "multiple=\"%s\"" (if s then "true" else "false")
        | Muted s -> sprintf "muted=\"%s\"" (if s then "true" else "false")
        | Name s -> sprintf "name=\"%s\"" s
        | NoValidate s -> sprintf "noValidate=\"%s\"" (if s then "true" else "false")
        | OnClick s -> sprintf "onclick=\"%s\"" s
        | OnMouseOut s -> sprintf "onmouseout=\"%s\"" s
        | OnMouseOver s -> sprintf "onmouseover=\"%s\"" s
        | Open s -> sprintf "open=\"%s\"" (if s then "true" else "false")
        | Optimum s -> sprintf "optimum=\"%g\"" s
        | Pattern s -> sprintf "pattern=\"%s\"" s
        | Placeholder s -> sprintf "placeholder=\"%s\"" s
        | Poster s -> sprintf "poster=\"%s\"" s
        | Preload s -> sprintf "preload=\"%s\"" s
        | RadioGroup s -> sprintf "radioGroup=\"%s\"" s
        | ReadOnly s -> sprintf "readOnly=\"%s\"" (if s then "true" else "false")
        | Rel s -> sprintf "rel=\"%s\"" s
        | Required s -> sprintf "required=\"%s\"" (if s then "true" else "false")
        | Role s -> sprintf "role=\"%s\"" s
        | Rows s -> sprintf "rows=\"%g\"" s
        | RowSpan s -> sprintf "rowSpan=\"%g\"" s
        | Sandbox s -> sprintf "sandbox=\"%s\"" s
        | Scope s -> sprintf "scope=\"%s\"" s
        | Scoped s -> sprintf "scoped=\"%s\"" (if s then "true" else "false")
        | Scrolling s -> sprintf "scrolling=\"%s\"" s
        | Seamless s -> sprintf "seamless=\"%s\"" (if s then "true" else "false")
        | Selected s -> sprintf "selected=\"%s\"" (if s then "true" else "false")
        | Shape s -> sprintf "shape=\"%s\"" s
        | Size s -> sprintf "size=\"%g\"" s
        | Sizes s -> sprintf "sizes=\"%s\"" s
        | Span s -> sprintf "span=\"%g\"" s
        | SpellCheck s -> sprintf "spellCheck=\"%s\"" (if s then "true" else "false")
        | Src s -> sprintf "src=\"%s\"" s
        | SrcDoc s -> sprintf "srcDoc=\"%s\"" s
        | SrcLang s -> sprintf "srcLang=\"%s\"" s
        | SrcSet s -> sprintf "srcSet=\"%s\"" s
        | Start s -> sprintf "start=\"%g\"" s
        | Step s -> sprintf "step=\"%s\"" s
        //| Style s -> sprintf "style=\"%s\"" (s |> List.map(string) |> String.concat " ")
        | Summary s -> sprintf "summary=\"%s\"" s
        | TabIndex s -> sprintf "tabIndex=\"%g\"" s
        | Target s -> sprintf "target=\"%s\"" s
        | Title s -> sprintf "title=\"%s\"" s
        | Type s -> sprintf "type=\"%s\"" s
        | UseMap s -> sprintf "useMap=\"%s\"" s
        | Value s -> sprintf "value=\"%s\"" s
        | Width s -> sprintf "width=\"%s\"" s
        | Wmode s -> sprintf "wmode=\"%s\"" s
        | Wrap s -> sprintf "wrap=\"%s\"" s
        | About s -> sprintf "about=\"%s\"" s
        | Datatype s -> sprintf "datatype=\"%s\"" s
        | Inlist s -> sprintf "inlist=\"%s\"" s
        | Prefix s -> sprintf "prefix=\"%s\"" s
        | Property s -> sprintf "property=\"%s\"" s
        | Resource s -> sprintf "resource=\"%s\"" s
        | Typeof s -> sprintf "typeof=\"%s\"" s
        | Vocab s -> sprintf "vocab=\"%s\"" s
        | AutoCapitalize s -> sprintf "autoCapitalize=\"%s\"" s
        | AutoCorrect s -> sprintf "autoCorrect=\"%s\"" s
        | AutoSave s -> sprintf "autoSave=\"%s\"" s
        | Color s -> sprintf "color=\"%s\"" s
        | ItemProp s -> sprintf "itemProp=\"%s\"" s
        | ItemScope s -> sprintf "itemScope=\"%s\"" (if s then "true" else "false")
        | ItemType s -> sprintf "itemType=\"%s\"" s
        | ItemID s -> sprintf "itemID=\"%s\"" s
        | ItemRef s -> sprintf "itemRef=\"%s\"" s
        | Results s -> sprintf "results=\"%g\"" s
        | Security s -> sprintf "security=\"%s\"" s
        | Unselectable s -> sprintf "unselectable=\"%s\"" (if s then "true" else "false")
        | Custom(k, v) -> sprintf "%s=\"%s\"" k v

type internal HtmlElement =
    private
    | A of props: HtmlProperties list * children: HtmlElement list
    | Abbr of props: HtmlProperties list * children: HtmlElement list
    | Address of props: HtmlProperties list * children: HtmlElement list
    | Area of props: HtmlProperties list
    | Article of props: HtmlProperties list * children: HtmlElement list
    | Aside of props: HtmlProperties list * children: HtmlElement list
    | Audio of props: HtmlProperties list * children: HtmlElement list
    | B of props: HtmlProperties list * children: HtmlElement list
    | Base of props: HtmlProperties list
    | Bdi of props: HtmlProperties list * children: HtmlElement list
    | Bdo of props: HtmlProperties list * children: HtmlElement list
    | Big of props: HtmlProperties list * children: HtmlElement list
    | Blockquote of props: HtmlProperties list * children: HtmlElement list
    | Body of props: HtmlProperties list * children: HtmlElement list
    | Br of props: HtmlProperties list
    | Button of props: HtmlProperties list * children: HtmlElement list
    | Canvas of props: HtmlProperties list * children: HtmlElement list
    | Caption of props: HtmlProperties list * children: HtmlElement list
    | Cite of props: HtmlProperties list * children: HtmlElement list
    | Code of props: HtmlProperties list * children: HtmlElement list
    | Col of props: HtmlProperties list
    | Colgroup of props: HtmlProperties list * children: HtmlElement list
    | Data of props: HtmlProperties list * children: HtmlElement list
    | Datalist of props: HtmlProperties list * children: HtmlElement list
    | Dd of props: HtmlProperties list * children: HtmlElement list
    | Del of props: HtmlProperties list * children: HtmlElement list
    | Details of props: HtmlProperties list * children: HtmlElement list
    | Dfn of props: HtmlProperties list * children: HtmlElement list
    | Dialog of props: HtmlProperties list * children: HtmlElement list
    | Div of props: HtmlProperties list * children: HtmlElement list
    | Dl of props: HtmlProperties list * children: HtmlElement list
    | Dt of props: HtmlProperties list * children: HtmlElement list
    | Em of props: HtmlProperties list * children: HtmlElement list
    | Embed of props: HtmlProperties list
    | Fieldset of props: HtmlProperties list * children: HtmlElement list
    | Figcaption of props: HtmlProperties list * children: HtmlElement list
    | Figure of props: HtmlProperties list * children: HtmlElement list
    | Footer of props: HtmlProperties list * children: HtmlElement list
    | Form of props: HtmlProperties list * children: HtmlElement list
    | H1 of props: HtmlProperties list * children: HtmlElement list
    | H2 of props: HtmlProperties list * children: HtmlElement list
    | H3 of props: HtmlProperties list * children: HtmlElement list
    | H4 of props: HtmlProperties list * children: HtmlElement list
    | H5 of props: HtmlProperties list * children: HtmlElement list
    | H6 of props: HtmlProperties list * children: HtmlElement list
    | Head of props: HtmlProperties list * children: HtmlElement list
    | Header of props: HtmlProperties list * children: HtmlElement list
    | Hgroup of props: HtmlProperties list * children: HtmlElement list
    | Hr of props: HtmlProperties list
    | Html of props: HtmlProperties list * children: HtmlElement list
    | I of props: HtmlProperties list * children: HtmlElement list
    | Iframe of props: HtmlProperties list * children: HtmlElement list
    | Img of props: HtmlProperties list
    | Input of props: HtmlProperties list
    | Ins of props: HtmlProperties list * children: HtmlElement list
    | Kbd of props: HtmlProperties list * children: HtmlElement list
    | Keygen of props: HtmlProperties list
    | Label of props: HtmlProperties list * children: HtmlElement list
    | Legend of props: HtmlProperties list * children: HtmlElement list
    | Li of props: HtmlProperties list * children: HtmlElement list
    | Link of props: HtmlProperties list
    | Main of props: HtmlProperties list * children: HtmlElement list
    | Map of props: HtmlProperties list * children: HtmlElement list
    | Mark of props: HtmlProperties list * children: HtmlElement list
    | Menu of props: HtmlProperties list * children: HtmlElement list
    | Menuitem of props: HtmlProperties list
    | Meta of props: HtmlProperties list
    | Meter of props: HtmlProperties list * children: HtmlElement list
    | Nav of props: HtmlProperties list * children: HtmlElement list
    | Noscript of props: HtmlProperties list * children: HtmlElement list
    | Object of props: HtmlProperties list * children: HtmlElement list
    | Ol of props: HtmlProperties list * children: HtmlElement list
    | Optgroup of props: HtmlProperties list * children: HtmlElement list
    | Option of props: HtmlProperties list * children: HtmlElement list
    | Output of props: HtmlProperties list * children: HtmlElement list
    | P of props: HtmlProperties list * children: HtmlElement list
    | Param of props: HtmlProperties list
    | Picture of props: HtmlProperties list * children: HtmlElement list
    | Pre of props: HtmlProperties list * children: HtmlElement list
    | Progress of props: HtmlProperties list * children: HtmlElement list
    | Q of props: HtmlProperties list * children: HtmlElement list
    | Rp of props: HtmlProperties list * children: HtmlElement list
    | Rt of props: HtmlProperties list * children: HtmlElement list
    | Ruby of props: HtmlProperties list * children: HtmlElement list
    | S of props: HtmlProperties list * children: HtmlElement list
    | Samp of props: HtmlProperties list * children: HtmlElement list
    | Script of props: HtmlProperties list * children: HtmlElement list
    | Section of props: HtmlProperties list * children: HtmlElement list
    | Select of props: HtmlProperties list * children: HtmlElement list
    | Small of props: HtmlProperties list * children: HtmlElement list
    | Source of props: HtmlProperties list
    | Span of props: HtmlProperties list * children: HtmlElement list
    | Strong of props: HtmlProperties list * children: HtmlElement list
    | Style of props: HtmlProperties list * children: HtmlElement list
    | Sub of props: HtmlProperties list * children: HtmlElement list
    | Summary of props: HtmlProperties list * children: HtmlElement list
    | Sup of props: HtmlProperties list * children: HtmlElement list
    | Table of props: HtmlProperties list * children: HtmlElement list
    | Tbody of props: HtmlProperties list * children: HtmlElement list
    | Td of props: HtmlProperties list * children: HtmlElement list
    | Textarea of props: HtmlProperties list * children: HtmlElement list
    | Tfoot of props: HtmlProperties list * children: HtmlElement list
    | Th of props: HtmlProperties list * children: HtmlElement list
    | Thead of props: HtmlProperties list * children: HtmlElement list
    | Time of props: HtmlProperties list * children: HtmlElement list
    | Title of props: HtmlProperties list * children: HtmlElement list
    | Tr of props: HtmlProperties list * children: HtmlElement list
    | Track of props: HtmlProperties list
    | U of props: HtmlProperties list * children: HtmlElement list
    | Ul of props: HtmlProperties list * children: HtmlElement list
    | Var of props: HtmlProperties list * children: HtmlElement list
    | Video of props: HtmlProperties list * children: HtmlElement list
    | Wbr of props: HtmlProperties list
    | Svg of props: HtmlProperties list * children: HtmlElement list
    | Circle of props: HtmlProperties list * children: HtmlElement list
    | Defs of props: HtmlProperties list * children: HtmlElement list
    | Ellipse of props: HtmlProperties list * children: HtmlElement list
    | G of props: HtmlProperties list * children: HtmlElement list
    | Image of props: HtmlProperties list * children: HtmlElement list
    | Line of props: HtmlProperties list * children: HtmlElement list
    | LinearGradient of props: HtmlProperties list * children: HtmlElement list
    | Mask of props: HtmlProperties list * children: HtmlElement list
    | Path of props: HtmlProperties list * children: HtmlElement list
    | Pattern of props: HtmlProperties list * children: HtmlElement list
    | Polygon of props: HtmlProperties list * children: HtmlElement list
    | Polyline of props: HtmlProperties list * children: HtmlElement list
    | RadialGradient of props: HtmlProperties list * children: HtmlElement list
    | Rect of props: HtmlProperties list * children: HtmlElement list
    | Stop of props: HtmlProperties list * children: HtmlElement list
    | Text of props: HtmlProperties list * children: HtmlElement list
    | Tspan of props: HtmlProperties list * children: HtmlElement list
    | String of string
    | EncodeString of string
    | CustomElement of element: string * props: HtmlProperties list * children: HtmlElement list

    override tag.ToString() =
        let rec format tag (props: HtmlProperties list) (children: HtmlElement list) level =
            let cnt =
                if children.Length > 0 then
                    "\n"
                    + (children
                       |> List.map (fun n -> (String.replicate level "  ") + helper (level + 1) n)
                       |> String.concat "\n")
                    + "\n"
                    + (String.replicate (level - 1) "  ")
                else
                    ""

            let attrs =
                if props.Length > 0 then
                    " " + (props |> List.map string<HtmlProperties> |> String.concat " ")
                else
                    ""

            sprintf "<%s%s>%s</%s>" tag attrs cnt tag


        and formatVoid tag (props: HtmlProperties list) _level =
            let attrs =
                if props.Length > 0 then
                    " " + (props |> List.map string<HtmlProperties> |> String.concat " ")
                else
                    ""

            sprintf "<%s%s/>" tag attrs

        and helper level tag =
            match tag with
            | A(props, children) -> format "a" props children level
            | Abbr(props, children) -> format "abbr" props children level
            | Address(props, children) -> format "address" props children level
            | Area(props) -> formatVoid "area" props level
            | Article(props, children) -> format "article" props children level
            | Aside(props, children) -> format "aside" props children level
            | Audio(props, children) -> format "audio" props children level
            | B(props, children) -> format "b" props children level
            | Base(props) -> formatVoid "base" props level
            | Bdi(props, children) -> format "bdi" props children level
            | Bdo(props, children) -> format "bdo" props children level
            | Big(props, children) -> format "big" props children level
            | Blockquote(props, children) -> format "blockquote" props children level
            | Body(props, children) -> format "body" props children level
            | Br(props) -> formatVoid "br" props level
            | Button(props, children) -> format "button" props children level
            | Canvas(props, children) -> format "canvas" props children level
            | Caption(props, children) -> format "caption" props children level
            | Cite(props, children) -> format "cite" props children level
            | Code(props, children) -> format "code" props children level
            | Col(props) -> formatVoid "col" props level
            | Colgroup(props, children) -> format "colgroup" props children level
            | Data(props, children) -> format "data" props children level
            | Datalist(props, children) -> format "datalist" props children level
            | Dd(props, children) -> format "dd" props children level
            | Del(props, children) -> format "del" props children level
            | Details(props, children) -> format "details" props children level
            | Dfn(props, children) -> format "dfn" props children level
            | Dialog(props, children) -> format "dialog" props children level
            | Div(props, children) -> format "div" props children level
            | Dl(props, children) -> format "dl" props children level
            | Dt(props, children) -> format "dt" props children level
            | Em(props, children) -> format "em" props children level
            | Embed(props) -> formatVoid "embed" props level
            | Fieldset(props, children) -> format "fieldset" props children level
            | Figcaption(props, children) -> format "figcaption" props children level
            | Figure(props, children) -> format "figure" props children level
            | Footer(props, children) -> format "footer" props children level
            | Form(props, children) -> format "form" props children level
            | H1(props, children) -> format "h1" props children level
            | H2(props, children) -> format "h2" props children level
            | H3(props, children) -> format "h3" props children level
            | H4(props, children) -> format "h4" props children level
            | H5(props, children) -> format "h5" props children level
            | H6(props, children) -> format "h6" props children level
            | Head(props, children) -> format "head" props children level
            | Header(props, children) -> format "header" props children level
            | Hgroup(props, children) -> format "hgroup" props children level
            | Hr(props) -> formatVoid "hr" props level
            | Html(props, children) -> format "html" props children level
            | I(props, children) -> format "i" props children level
            | Iframe(props, children) -> format "iframe" props children level
            | Img(props) -> formatVoid "img" props level
            | Input(props) -> formatVoid "input" props level
            | Ins(props, children) -> format "ins" props children level
            | Kbd(props, children) -> format "kbd" props children level
            | Keygen(props) -> formatVoid "keygen" props level
            | Label(props, children) -> format "label" props children level
            | Legend(props, children) -> format "legend" props children level
            | Li(props, children) -> format "li" props children level
            | Link(props) -> formatVoid "link" props level
            | Main(props, children) -> format "main" props children level
            | Map(props, children) -> format "map" props children level
            | Mark(props, children) -> format "mark" props children level
            | Menu(props, children) -> format "menu" props children level
            | Menuitem(props) -> formatVoid "menuitem" props level
            | Meta(props) -> formatVoid "meta" props level
            | Meter(props, children) -> format "meter" props children level
            | Nav(props, children) -> format "nav" props children level
            | Noscript(props, children) -> format "noscript" props children level
            | Object(props, children) -> format "object" props children level
            | Ol(props, children) -> format "ol" props children level
            | Optgroup(props, children) -> format "optgroup" props children level
            | Option(props, children) -> format "option" props children level
            | Output(props, children) -> format "output" props children level
            | P(props, children) -> format "p" props children level
            | Param(props) -> formatVoid "param" props level
            | Picture(props, children) -> format "picture" props children level
            | Pre(props, children) -> format "pre" props children level
            | Progress(props, children) -> format "progress" props children level
            | Q(props, children) -> format "q" props children level
            | Rp(props, children) -> format "rp" props children level
            | Rt(props, children) -> format "rt" props children level
            | Ruby(props, children) -> format "ruby" props children level
            | S(props, children) -> format "s" props children level
            | Samp(props, children) -> format "samp" props children level
            | Script(props, children) -> format "script" props children level
            | Section(props, children) -> format "section" props children level
            | Select(props, children) -> format "select" props children level
            | Small(props, children) -> format "small" props children level
            | Source(props) -> formatVoid "source" props level
            | Span(props, children) -> format "span" props children level
            | Strong(props, children) -> format "strong" props children level
            | Style(props, children) -> format "style" props children level
            | Sub(props, children) -> format "sub" props children level
            | Summary(props, children) -> format "summary" props children level
            | Sup(props, children) -> format "sup" props children level
            | Table(props, children) -> format "table" props children level
            | Tbody(props, children) -> format "tbody" props children level
            | Td(props, children) -> format "td" props children level
            | Textarea(props, children) -> format "textarea" props children level
            | Tfoot(props, children) -> format "tfoot" props children level
            | Th(props, children) -> format "th" props children level
            | Thead(props, children) -> format "thead" props children level
            | Time(props, children) -> format "time" props children level
            | Title(props, children) -> format "title" props children level
            | Tr(props, children) -> format "tr" props children level
            | Track(props) -> formatVoid "track" props level
            | U(props, children) -> format "u" props children level
            | Ul(props, children) -> format "ul" props children level
            | Var(props, children) -> format "var" props children level
            | Video(props, children) -> format "video" props children level
            | Wbr(props) -> formatVoid "wbr" props level
            | Svg(props, children) -> format "svg" props children level
            | Circle(props, children) -> format "circle" props children level
            | Defs(props, children) -> format "defs" props children level
            | Ellipse(props, children) -> format "ellipse" props children level
            | G(props, children) -> format "g" props children level
            | Image(props, children) -> format "image" props children level
            | Line(props, children) -> format "line" props children level
            | LinearGradient(props, children) -> format "radient" props children level
            | Mask(props, children) -> format "mask" props children level
            | Path(props, children) -> format "path" props children level
            | Pattern(props, children) -> format "pattern" props children level
            | Polygon(props, children) -> format "polygon" props children level
            | Polyline(props, children) -> format "polyline" props children level
            | RadialGradient(props, children) -> format "radient" props children level
            | Rect(props, children) -> format "rect" props children level
            | Stop(props, children) -> format "stop" props children level
            | Text(props, children) -> format "text" props children level
            | Tspan(props, children) -> format "tspan" props children level
            | String str -> str
            | EncodeString str -> System.Web.HttpUtility.HtmlEncode str
            | CustomElement(element, props, children) -> format element props children level

        helper 1 tag

module internal Html =
    let a (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.A(props, children)
    let abbr (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Abbr(props, children)
    let address (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Address(props, children)
    let area (props: HtmlProperties list) = HtmlElement.Area(props)
    let article (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Article(props, children)
    let aside (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Aside(props, children)
    let audio (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Audio(props, children)
    let b (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.B(props, children)
    let ``base`` (props: HtmlProperties list) = HtmlElement.Base(props)
    let bdi (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Bdi(props, children)
    let bdo (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Bdo(props, children)
    let big (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Big(props, children)
    let blockquote (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Blockquote(props, children)
    let body (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Body(props, children)
    let br (props: HtmlProperties list) = HtmlElement.Br(props)
    let button (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Button(props, children)
    let canvas (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Canvas(props, children)
    let caption (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Caption(props, children)
    let cite (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Cite(props, children)
    let code (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Code(props, children)
    let col (props: HtmlProperties list) = HtmlElement.Col(props)
    let colgroup (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Colgroup(props, children)
    let data (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Data(props, children)
    let datalist (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Datalist(props, children)
    let dd (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Dd(props, children)
    let del (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Del(props, children)
    let details (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Details(props, children)
    let dfn (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Dfn(props, children)
    let dialog (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Dialog(props, children)
    let div (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Div(props, children)
    let dl (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Dl(props, children)
    let dt (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Dt(props, children)
    let em (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Em(props, children)
    let embed (props: HtmlProperties list) = HtmlElement.Embed(props)
    let fieldset (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Fieldset(props, children)
    let figcaption (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Figcaption(props, children)
    let figure (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Figure(props, children)
    let footer (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Footer(props, children)
    let form (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Form(props, children)
    let h1 (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.H1(props, children)
    let h2 (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.H2(props, children)
    let h3 (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.H3(props, children)
    let h4 (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.H4(props, children)
    let h5 (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.H5(props, children)
    let h6 (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.H6(props, children)
    let head (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Head(props, children)
    let header (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Header(props, children)
    let hgroup (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Hgroup(props, children)
    let hr (props: HtmlProperties list) = HtmlElement.Hr(props)
    let html (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Html(props, children)
    let i (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.I(props, children)
    let iframe (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Iframe(props, children)
    let img (props: HtmlProperties list) = HtmlElement.Img(props)
    let input (props: HtmlProperties list) = HtmlElement.Input(props)
    let ins (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Ins(props, children)
    let kbd (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Kbd(props, children)
    let keygen (props: HtmlProperties list) = HtmlElement.Keygen(props)
    let label (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Label(props, children)
    let legend (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Legend(props, children)
    let li (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Li(props, children)
    let link (props: HtmlProperties list) = HtmlElement.Link(props)
    let main (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Main(props, children)
    let map (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Map(props, children)
    let mark (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Mark(props, children)
    let menu (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Menu(props, children)
    let menuitem (props: HtmlProperties list) = HtmlElement.Menuitem(props)
    let meta (props: HtmlProperties list) = HtmlElement.Meta(props)
    let meter (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Meter(props, children)
    let nav (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Nav(props, children)
    let noscript (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Noscript(props, children)
    let ``object`` (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Object(props, children)
    let ol (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Ol(props, children)
    let optgroup (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Optgroup(props, children)
    let option (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Option(props, children)
    let output (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Output(props, children)
    let p (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.P(props, children)
    let param (props: HtmlProperties list) = HtmlElement.Param(props)
    let picture (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Picture(props, children)
    let pre (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Pre(props, children)
    let progress (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Progress(props, children)
    let q (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Q(props, children)
    let rp (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Rp(props, children)
    let rt (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Rt(props, children)
    let ruby (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Ruby(props, children)
    let s (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.S(props, children)
    let samp (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Samp(props, children)
    let script (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Script(props, children)
    let section (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Section(props, children)
    let select (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Select(props, children)
    let small (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Small(props, children)
    let source (props: HtmlProperties list) = HtmlElement.Source(props)
    let span (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Span(props, children)
    let strong (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Strong(props, children)
    let style (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Style(props, children)
    let sub (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Sub(props, children)
    let summary (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Summary(props, children)
    let sup (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Sup(props, children)
    let table (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Table(props, children)
    let tbody (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Tbody(props, children)
    let td (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Td(props, children)
    let textarea (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Textarea(props, children)
    let tfoot (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Tfoot(props, children)
    let th (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Th(props, children)
    let thead (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Thead(props, children)
    let time (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Time(props, children)
    let title (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Title(props, children)
    let tr (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Tr(props, children)
    let track (props: HtmlProperties list) = HtmlElement.Track(props)
    let u (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.U(props, children)
    let ul (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Ul(props, children)
    let var (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Var(props, children)
    let video (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Video(props, children)
    let wbr (props: HtmlProperties list) = HtmlElement.Wbr(props)
    let svg (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Svg(props, children)
    let circle (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Circle(props, children)
    let defs (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Defs(props, children)
    let ellipse (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Ellipse(props, children)
    let g (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.G(props, children)
    let image (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Image(props, children)
    let line (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Line(props, children)

    let linearGradient (props: HtmlProperties list) (children: HtmlElement list) =
        HtmlElement.LinearGradient(props, children)

    let mask (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Mask(props, children)
    let path (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Path(props, children)
    let pattern (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Pattern(props, children)
    let polygon (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Polygon(props, children)
    let polyline (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Polyline(props, children)

    let radialGradient (props: HtmlProperties list) (children: HtmlElement list) =
        HtmlElement.RadialGradient(props, children)

    let rect (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Rect(props, children)
    let stop (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Stop(props, children)
    let text (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Text(props, children)
    let tspan (props: HtmlProperties list) (children: HtmlElement list) = HtmlElement.Tspan(props, children)
    //let string str = HtmlElement.String str
    let (!!) str = HtmlElement.String str
    let encode str = HtmlElement.EncodeString str

    /// Web component from https://iconify.design/docs/
    let iconifyIcon (props: HtmlProperties list) =
        HtmlElement.CustomElement("iconify-icon", props, [])

    let fsdocsDetailsToggle (props: HtmlProperties list) =
        HtmlElement.CustomElement("fsdocs-details-toggle", props, [])
