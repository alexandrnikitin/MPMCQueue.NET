#r @"../packages/FAKE.4.39.0/tools/FakeLib.dll"
open Fake

let product = "MPMCQueue.NET"
let description = "Bounded multiple producers multiple consumers queue for .NET"
let id = "52db9f2d-3411-47cf-b467-801c0376d313"
let copyright = "Copyright 2016"
let authors = [ "Alexandr Nikitin" ]
let tags = ["queue"]
let version = "0.1.0"

let buildDir = ".output"
let packagingRoot = "./.packaging/"
let packagingDir = packagingRoot @@  product
let packagingSourceDir = packagingRoot @@  product + ".Source"
let nugetPath = "../.nuget/nuget.exe"

Target "Default" (fun _ ->
    trace "The default target"
)

Target "Clean" (fun _ ->
    CleanDirs [buildDir; packagingRoot]
)

Target "RestorePackages" (fun _ -> 
     !! "./**/packages.config"
     |> Seq.iter (RestorePackage (fun p -> { p with ToolPath = nugetPath } ))
 )

Target "Build" (fun _ ->
    !! "../src/**/*.csproj"
      |> MSBuildRelease buildDir "Build"
      |> Log "Build-Output: "
)

Target "BuildTest" (fun _ ->
    !! "../tests/**/*.csproj"
      |> MSBuildDebug buildDir "Build"
      |> Log "TestBuild-Output: "
)

open Fake.AssemblyInfoFile

Target "AssemblyInfo" (fun _ ->
    let assemblyInfoVersion = version + ".0"
    CreateCSharpAssemblyInfo "../src/MPMCQueue.NET/Properties/AssemblyInfo.cs"
        [Attribute.Title product
         Attribute.Description description
         Attribute.Copyright copyright
         Attribute.Guid id
         Attribute.Product product
         Attribute.Version assemblyInfoVersion
         Attribute.FileVersion assemblyInfoVersion]
)

"Clean"
  ==> "RestorePackages"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "BuildTest"
  ==> "Default"

RunTargetOrDefault "Default"