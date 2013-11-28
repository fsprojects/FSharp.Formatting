// (c) Microsoft Corporation. Apache 2.0 License
//----------------------------------------------------------------------------
// Some general F# utilities for mangling / unmangling / manipulating names.
//--------------------------------------------------------------------------


module internal Microsoft.FSharp.Metadata.Reader.Internal.PrettyNaming
#nowarn "62"  // Using ^ for string concatenation 

open Microsoft.FSharp.Metadata.Reader.Internal.Prelude
    
open System.Globalization
open System.Collections.Generic

let private opNameTable = 
  [ ("[]", "op_Nil");
    ("::", "op_ColonColon");
    ("+", "op_Addition");
    ("~%", "op_Splice");
    ("~%%", "op_SpliceUntyped");
    ("~++", "op_Increment");
    ("~--", "op_Decrement");
    ("-", "op_Subtraction");
    ("*", "op_Multiply");
    ("**", "op_Exponentiation");
    ("/", "op_Division");
    ("@", "op_Append");
    ("^", "op_Concatenate");
    ("%", "op_Modulus");
    ("&&&", "op_BitwiseAnd");
    ("|||", "op_BitwiseOr");
    ("^^^", "op_ExclusiveOr");
    ("<<<", "op_LeftShift");
    ("~~~", "op_LogicalNot");
    (">>>", "op_RightShift");
    ("~+", "op_UnaryPlus");
    ("~-", "op_UnaryNegation");
    ("~&", "op_AddressOf");
    ("~&&", "op_IntegerAddressOf");
    ("&&", "op_BooleanAnd");
    ("||", "op_BooleanOr");
    ("<=", "op_LessThanOrEqual");
    ("=","op_Equality");
    ("<>","op_Inequality");
    (">=", "op_GreaterThanOrEqual");
    ("<", "op_LessThan");
    (">", "op_GreaterThan");
    ("|>", "op_PipeRight");
    ("||>", "op_PipeRight2");
    ("|||>", "op_PipeRight3");
    ("<|", "op_PipeLeft");
    ("<||", "op_PipeLeft2");
    ("<|||", "op_PipeLeft3");
    ("!", "op_Dereference");
    (">>", "op_ComposeRight");
    ("<<", "op_ComposeLeft");
    ("<< >>", "op_TypedQuotationUnicode");
    ("<<| |>>", "op_ChevronsBar");
    ("<@ @>", "op_Quotation");
    ("<@@ @@>", "op_QuotationUntyped");
    ("+=", "op_AdditionAssignment");
    ("-=", "op_SubtractionAssignment");
    ("*=", "op_MultiplyAssignment");
    ("/=", "op_DivisionAssignment");
    ("..", "op_Range");
    (".. ..", "op_RangeStep"); 
    ("?", "op_Dynamic");
    ("?<-", "op_DynamicAssignment");
    (".()", "op_ArrayLookup");
    (".()<-", "op_ArrayAssign");
    ]

let opCharTranslateTable =
  [ ( '>', "Greater");
    ( '<', "Less"); 
    ( '+', "Plus");
    ( '-', "Minus");
    ( '*', "Multiply");
    ( '=', "Equals");
    ( '~', "Twiddle");
    ( '%', "Percent");
    ( '.', "Dot");
    ( '$', "Dollar");
    ( '&', "Amp");
    ( '|', "Bar");
    ( '@', "At");
    ( '#', "Hash");
    ( '^', "Hat");
    ( '!', "Bang");
    ( '?', "Qmark");
    ( '/', "Divide");
    ( ':', "Colon");
    ( '(', "LParen");
    ( ',', "Comma");
    ( ')', "RParen");
    ( ' ', "Space");
    ( '[', "LBrack");
    ( ']', "RBrack"); ]

let opCharDict = 
    let t = new Dictionary<_,_>()
    for (c,_) in opCharTranslateTable do 
        t.Add(c,1)
    t
        
let isOpName (n:string) =
    let rec loop i = (i < n.Length && (opCharDict.ContainsKey(n.[i]) || loop (i+1)))
    loop 0

let decompileOpName = 
  let t = new Dictionary<string,string>()
  for (x,y) in opNameTable do
      t.Add(y,x)
  fun n -> 
      let mutable res = Unchecked.defaultof<_>
      if t.TryGetValue(n,&res) then 
          res
      else
          if n.StartsWith("op_",System.StringComparison.Ordinal) then 
            let rec loop (remaining:string) = 
                let l = remaining.Length
                if l = 0 then Some(remaining) else
                let choice = 
                  opCharTranslateTable |> List.tryPick (fun (a,b) -> 
                      let bl = b.Length
                      if bl <= l && remaining.Substring(0,bl) = b then 
                        Some(string a, remaining.Substring(bl,l - bl)) 
                      else None) 
                        
                match choice with 
                | Some (a,remaining2) -> 
                    match loop remaining2 with 
                    | None -> None
                    | Some a2 -> Some(a^a2)
                | None -> None (* giveup *)
            match loop (n.Substring(3,n.Length - 3)) with
            | Some res -> res
            | None -> n
          else n
                  
//-------------------------------------------------------------------------
// Handle mangled .NET generic type names
//------------------------------------------------------------------------- 
     
let private mangledGenericTypeNameSym = '`'

let isMangledGenericName (n:string) = 
    n.IndexOf mangledGenericTypeNameSym <> -1 &&
    // check what comes after the symbol is a number 
    let m = n.LastIndexOf mangledGenericTypeNameSym
    let mutable res = m < n.Length - 1
    for i = m + 1 to n.Length - 1 do
        res <- res && n.[i] >= '0' && n.[i] <= '9';
    res

type NameArityPair = NameArityPair of string*int

let demangleGenericTypeName n = 
    if  isMangledGenericName n then 
        let pos = n.LastIndexOf mangledGenericTypeNameSym
        n.Substring(0,pos)
    else n

//-------------------------------------------------------------------------
// Property name mangling.
// Expecting s to be in the form (as returned by qualified_mangled_name_of_tcref) of:
//    get_P                         or  set_P
//    Names/Space/Class/NLPath-get_P  or  Names/Space/Class/NLPath.set_P
// Required to return "P"
//-------------------------------------------------------------------------

let private chopStringTo (s:string) (c:char) =
    (* chopStringTo "abcdef" 'c' --> "def" *)
    if s.IndexOf c <> -1 then
        let i =  s.IndexOf c + 1
        s.Substring(i, s.Length - i)
    else
        s

/// Try to chop "get_" or "set_" from a string
let tryChopPropertyName (s: string) =
    // extract the logical name from any mangled name produced by MakeMemberDataAndMangledNameForMemberVal 
    let s = 
        if s.StartsWith("get_", System.StringComparison.Ordinal) || 
            s.StartsWith("set_", System.StringComparison.Ordinal) 
        then s 
        else chopStringTo s '.'

    if s.Length <= 4 || (let s = s.Substring(0,4) in s <> "get_" && s <> "set_") then
        None
    else 
        Some(s.Substring(4,s.Length - 4) )


let chopPropertyName s =
    match tryChopPropertyName s with 
    | None -> failwith("Invalid internal property name: '"^s^"'");
    | Some res -> res
        

let demangleOperatorName nm = 
    let nm = decompileOpName nm
    if isOpName nm then "( "^nm^" )" else nm 

let fsharpModuleSuffix = "Module"

let isActivePatternName (nm:string) =
    (nm.IndexOf '|' = 0) &&
    nm.Length >= 3 &&
    (nm.LastIndexOf '|' = nm.Length - 1) &&
    ( let core = nm.Substring(1,nm.Length - 2) 
      // no operator characters except '|'
      core |> String.forall (fun c -> c = '|' || not (opCharDict.ContainsKey c)) &&
      // at least one non-operator character
      core |> String.exists (fun c -> not (opCharDict.ContainsKey c)))

//isActivePatternName "|+|" = false
//isActivePatternName "|ABC|" = true
//isActivePatternName "|ABC|DEF|" = true
//isActivePatternName "|||" = false
//isActivePatternName "||S|" = true

type ActivePatternInfo = 
    | APInfo of bool * string list 
    member x.IsTotal = let (APInfo(p,_)) = x in p
    member x.ActiveTags = let (APInfo(_,tags)) = x in tags

let activePatternInfoOfValName nm = 
    let rec loop (nm:string) = 
        let n = nm.IndexOf '|'
        if n > 0 then 
            nm.[0..n-1] :: loop nm.[n+1..]
        else
            [nm]
    let nm = decompileOpName nm
    if isActivePatternName nm then 
        let res = loop nm.[1..nm.Length-2]
        let resH,resT = List.frontAndBack res
        Some(if resT = "_" then APInfo(false,resH) else APInfo(true,res))
    else 
        None
    
