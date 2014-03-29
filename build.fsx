// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#r "packages/FAKE/tools/FakeLib.dll"
#r "packages/FAKE/tools/NuGet.Core.dll"
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
let release = parseReleaseNotes (IO.File.ReadAllLines "RELEASE_NOTES.md")

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

Target "BuildNumber" (fun _ ->
    use tb = getTfsBuild()
    tb.Build.BuildNumber <- sprintf "FSharpWeb2.%s.%s" release.AssemblyVersion tb.Build.BuildNumber
    tb.Build.Save()
)

Target "Build" (fun _ ->
    !! ("*/**/" + projectFile + "*.*proj")
    |> MSBuildRelease "bin" "Rebuild"
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
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "CopyLicense"
  ==> "RestorePackages"

"RestorePackages"
  =?> ("BuildNumber", isTfsBuild)
  ==> "AssemblyInfo"
  ==> "Build"

"Build"
  ==> "BuildTests"
  ==> "RunTests"
  ==> "All"

RunTargetOrDefault "All"

