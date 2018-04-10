// Learn more about F# at http://fsharp.org
module SiteMaint.Main

open System
open Argu
open Arguments
open Commands

[<EntryPoint>]
let main argv =
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<SiteMaintArguments>(programName = "sitemaint", errorHandler = errorHandler)

    let results = parser.Parse()

    printfn "Got parse results: %A" <| results

    match results with
    | Site siteArgs -> backupSite siteArgs
    | Log logArgs -> trimLogFiles logArgs
    | _ -> failwith "unrecognized commands"

    0 // return an integer exit code
