// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#I "packages/FAKE/tools"
#r "FakeLib.dll"
#load "packages/SourceLink.Fake/tools/SourceLink.Tfs.fsx"
open System
open System.IO
open Fake 
open Fake.AssemblyInfoFile
open Fake.Git
open Fake.ReleaseNotesHelper
open SourceLink

// --------------------------------------------------------------------------------------
// Provide project-specific details below
// --------------------------------------------------------------------------------------

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package 
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docs/tools/generate.fsx"

// The name of the project 
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "FSharpWeb2"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "A functional web application DSL for ASP.NET Web API."

// File system information 
// (<projectFile>.*proj is built during the building process)
let projectFile = "FSharpWeb2"

// Pattern specifying assemblies to be tested using NUnit
let testFile = "FSharpTest1"

// --------------------------------------------------------------------------------------
// The rest of the file includes standard build steps 
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let (!!) includes = (!! includes).SetBaseDirectory __SOURCE_DIRECTORY__

let release = parseReleaseNotes (IO.File.ReadAllLines "RELEASE_NOTES.md")
let isAppVeyorBuild = environVar "APPVEYOR" <> null
let releaseVersion =
    if isAppVeyorBuild then sprintf "%s-a%s" release.AssemblyVersion (DateTime.UtcNow.ToString "yyMMddHHmm")
    else release.AssemblyVersion

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
  let fileName = project + "/AssemblyInfo.fs"
  CreateFSharpAssemblyInfo fileName
      [ Attribute.Title project
        Attribute.Product project
        Attribute.Description summary
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion ] )

// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages

Target "RestorePackages" RestorePackages

Target "Clean" (fun _ ->
    CleanDirs ["bin"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

Target "BuildVersion" (fun _ ->
    Shell.Exec("appveyor", sprintf "UpdateBuild -Version \"%s\"" releaseVersion) |> ignore
)

Target "BuildNumber" (fun _ ->
    #if MONO
    ()
    #else
    use tb = getTfsBuild()
    tb.Build.BuildNumber <- sprintf "FSharpWeb2.%s.%s" releaseVersion tb.Build.BuildNumber
    tb.Build.Save()
    #endif
)

Target "Build" (fun _ ->
    !! ("*/**/" + projectFile + "*.*proj")
    |> MSBuildReleaseExt "bin" ["PackageAsSingleFile","True"] "Package"
    |> ignore
)

Target "CopyLicense" (fun _ ->
    [ "LICENSE.txt" ] |> CopyTo "bin"
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target "BuildTests" (fun _ ->
    !! ("*/**/*Test*.*proj")
    |> MSBuildDebug "bin" "Rebuild"
    |> ignore
)

Target "RunTests" (fun _ ->
    !! ("bin/*Test*.dll")
    |> NUnit (fun p ->
        { p with
            DisableShadowCopy = true
            TimeOut = TimeSpan.FromMinutes 20.
            OutputFile = "TestResults.xml" })
)

// --------------------------------------------------------------------------------------
// Establish stage targets and set their respective Git repo

let password = let p = getBuildParam "password" in if String.IsNullOrEmpty p then p else ":" + p

let tempDevDir = "temp/a"
let gitUrlDev = sprintf "https://panesofglass%s@panesofglassdev.scm.azurewebsites.net:443/panesofglassdev.git" password

let tempQADir = "temp/b"
let gitUrlQA = sprintf "https://panesofglass%s@panesofglassqa.scm.azurewebsites.net:443/panesofglassqa.git" password

let tempProdDir = "temp/c"
let gitUrlProd = sprintf "https://panesofglass%s@panesofglass.scm.azurewebsites.net:443/panesofglass.git" password

Target "DeployDev" (fun _ ->
    CleanDir tempDevDir
    Repository.clone "" gitUrlDev tempDevDir

    fullclean tempDevDir
    CopyRecursive "bin/_PublishedWebsites/FSharpWeb2" tempDevDir true |> tracefn "%A"
    StageAll tempDevDir
    Commit tempDevDir (sprintf "Update Dev environment to version %s" release.NugetVersion)
    Branches.push tempDevDir
)

Target "PromoteQA" (fun _ ->
    CleanDir tempDevDir
    Repository.clone "" gitUrlDev tempDevDir
    DeleteDir (tempDevDir + "/.git")

    CleanDir tempQADir
    Repository.clone "" gitUrlQA tempQADir

    fullclean tempQADir
    CopyRecursive tempDevDir tempQADir true |> tracefn "%A"
    StageAll tempQADir
    Commit tempQADir (sprintf "Update QA environment to version %s" release.NugetVersion)
    Branches.push tempQADir
)

Target "PromoteProd" (fun _ ->
    CleanDir tempQADir
    Repository.clone "" gitUrlQA tempQADir
    DeleteDir (tempQADir + "/.git")

    CleanDir tempProdDir
    Repository.clone "" gitUrlProd tempProdDir

    fullclean tempProdDir
    CopyRecursive tempQADir tempProdDir true |> tracefn "%A"
    StageAll tempProdDir
    Commit tempProdDir (sprintf "Update Prod environment to version %s" release.NugetVersion)
    Branches.push tempProdDir
)

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "CopyLicense"
  =?> ("BuildVersion", isAppVeyorBuild)
  =?> ("BuildNumber", isTfsBuild)
  ==> "RestorePackages"
  ==> "AssemblyInfo"
  ==> "Build"

"Build"
  ==> "BuildTests"
  ==> "RunTests"
  =?> ("DeployDev", not (String.IsNullOrEmpty password))
  ==> "All"

RunTargetOrDefault "All"

