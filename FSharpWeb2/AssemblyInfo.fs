namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharpWeb2")>]
[<assembly: AssemblyProductAttribute("FSharpWeb2")>]
[<assembly: AssemblyDescriptionAttribute("A functional web application DSL for ASP.NET Web API.")>]
[<assembly: AssemblyVersionAttribute("1.0.1")>]
[<assembly: AssemblyFileVersionAttribute("1.0.1")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0.1"
