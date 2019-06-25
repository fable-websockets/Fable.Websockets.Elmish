#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.JavaScript

open Globbing.Operators
open Fake.Core.TargetOperators

// Directories
let buildDir  = "./build/"
let deployDir = "./deploy/"

let projectFolders  =  [
                         (DirectoryInfo.getMatchingFilesRecursive "*.fsproj" (DirectoryInfo.ofPath "./src"))  
                         (DirectoryInfo.getMatchingFilesRecursive "*.fsproj" (DirectoryInfo.ofPath "./samples/"))
                       ]
                       |> Array.concat
                       |> Seq.filter(fun path -> not <| path.FullName.Contains(".fable"))
                       |> Seq.map (fun m -> m.Directory.FullName)



// Targets
Target.create "Clean" (fun _ ->
    Shell.cleanDirs [buildDir; deployDir]
)

Target.create "YarnRestore" (fun _->        
   ["./";"./samples/FileBrowser/Client/"; "./src/Fable.Websockets.Elmish/"]
   |> Seq.iter (fun dir -> Yarn.install (fun p ->{ p with WorkingDirectory = dir}))
   |> ignore   
)

Target.create "Restore" (fun _->    
    projectFolders
    |> Seq.map (DotNet.restore id)
    |> Seq.toArray
    |> ignore
)

Target.create "Build" (fun _ ->
    projectFolders
    |> Seq.map (DotNet.build id)
    |> Seq.toArray    
    |> ignore        
)

Target.create "RunElmishSample" (fun _ ->
    // Start client
    [  async { return (DotNet.exec (fun p -> {p with WorkingDirectory = "./samples/FileBrowser/Server/"}) "watch" "run" |> ignore) }
       async { return (Yarn.exec "start-sample" (fun p -> {p with WorkingDirectory = "./samples/FileBrowser/Client/"})) }
    ] |> Async.Parallel |> Async.RunSynchronously |> ignore
)

let release =  File.read "RELEASE_NOTES.md" |> ReleaseNotes.parse

Target.create "Meta" (fun _ ->
    [ "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
      "<PropertyGroup>"
      "<Description>Library for strongly typed websocket use in Fable</Description>"
      "<PackageProjectUrl>https://github.com/ncthbrt/Fable.Websockets.Elmish</PackageProjectUrl>"
      "<PackageLicenseUrl>https://github.com/ncthbrt/Fable.Websockets/blob/master/LICENSE.md</PackageLicenseUrl>"
      "<PackageIconUrl></PackageIconUrl>"
      "<RepositoryUrl>https://github.com/ncthbrt/Fable.Websockets.Elmish</RepositoryUrl>"
      "<PackageTags>fable;fsharp;elmish;websockets;observables</PackageTags>"
      "<Authors>Nick Cuthbert;OlegZee</Authors>"
      sprintf "<Version>%s</Version>" (string release.SemVer)
      "</PropertyGroup>"
      "</Project>"]
    |> File.write false "src/Meta.props"    
)

Target.create "Package" (fun _ ->            
    !! @"./src/**/*.fsproj"
    |> Seq.iter (DotNet.pack (fun p -> { p with OutputPath = Some <| System.Environment.CurrentDirectory + "/build" }))
)

// Build order
"Meta" ==> "Clean" ==> "Restore" ==> "YarnRestore" ==> "Build" ==> "Package"

"Build" ==> "RunElmishSample"

// start build
Target.runOrDefaultWithArguments "Build"
