module FSharp.Formatting.Menu
type MenuItem =
    {
        Link: string
        Content: string
    }
let createMenu (header : string) (items : MenuItem list) =  ""
let isTemplatingAvailable (input:string) : bool = false