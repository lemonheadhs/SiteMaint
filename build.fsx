// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r "./packages/FAKE/tools/FakeLib.dll"

open Fake
open System

// --------------------------------------------------------------------------------------
// Build variables
// --------------------------------------------------------------------------------------

let buildDir  = "./build/"
let appReferences = !! "/**/*.fsproj"
let dotnetcliVersion = "2.0.3"
let mutable dotnetExePath = "dotnet"

// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------

let run' timeout cmd args dir =
    if execProcess (fun info ->
        info.FileName <- cmd
        if not (String.IsNullOrWhiteSpace dir) then
            info.WorkingDirectory <- dir
        info.Arguments <- args
    ) timeout |> not then
        failwithf "Error while running '%s' with args: %s" cmd args

let run = run' System.TimeSpan.MaxValue

let runDotnet workingDir args =
    let result =
        ExecProcess (fun info ->
            info.FileName <- dotnetExePath
            info.WorkingDirectory <- workingDir
            info.Arguments <- args) TimeSpan.MaxValue
    if result <> 0 then failwithf "dotnet %s failed" args

// --------------------------------------------------------------------------------------
// Targets
// --------------------------------------------------------------------------------------

Target "Clean" (fun _ ->
    CleanDirs [buildDir]
)

Target "InstallDotNetCLI" (fun _ ->
    if not <| DotNetCli.isInstalled() then
        dotnetExePath <- DotNetCli.InstallDotNetSDK dotnetcliVersion
)

Target "Restore" (fun _ ->
    appReferences
    |> Seq.iter (fun p ->
        let dir = System.IO.Path.GetDirectoryName p
        runDotnet dir "restore"
    )
)

Target "Build" (fun _ ->
    appReferences
    |> Seq.iter (fun p ->
        let dir = System.IO.Path.GetDirectoryName p
        runDotnet dir "build"
    )
)

let trialDir = "./trial/"
let appendWith pLast pFirst =
    System.IO.Path.Combine(pFirst, pLast)

Target "Copy" (fun _ ->
    appReferences
    |> Seq.iter (fun p ->
        let binDir = 
            System.IO.Path.GetDirectoryName p
            |> appendWith "bin/Debug/net45/"
        let dirName = System.IO.Path.GetFileNameWithoutExtension p
        let toolDir = System.IO.Path.Combine(trialDir, dirName)
        ensureDirectory toolDir
        CopyRecursive binDir toolDir false |> ignore
    )
)

Target "CleanTrialSpace" (fun _ ->
    let excepts = [| "Site" |]
    ensureDirectory trialDir
    let trialDirInfo = 
        System.IO.DirectoryInfo(
            System.IO.Path.Combine(__SOURCE_DIRECTORY__, trialDir)
        )
        
    trialDirInfo.GetDirectories()
    |> Seq.filter (fun d ->
        excepts |> (not << Array.contains d.Name)
    )
    |> Seq.map (fun d -> d.FullName)
    |> DeleteDirs
)

// --------------------------------------------------------------------------------------
// Build order
// --------------------------------------------------------------------------------------

"Clean"
  ==> "InstallDotNetCLI"
  ==> "Restore"
  ==> "Build"
  ==> "Copy"
"CleanTrialSpace"
  ==> "Copy"

RunTargetOrDefault "Build"
