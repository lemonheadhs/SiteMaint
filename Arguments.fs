namespace SiteMaint

open Argu

module Arguments =

    type CLIArg = CustomCommandLineAttribute
    type CLIAlt = AltCommandLineAttribute

    type SiteMaintArguments =
        | [<CLIArg "backup-site">] [<CliPrefix(CliPrefix.None)>] [<First>] Site     
        | [<CLIArg "truncate-log">] [<CliPrefix(CliPrefix.None)>] Log   
        | [<CLIArg "-src">] [<Mandatory>] SourceDir of string
        | [<CLIArg "-dst">] [<Mandatory>] DestinationDir of string
        | [<CLIAlt "-n">] Archive_Name of string
        | [<CLIArg "-ldir">] LogDir of string
        | [<CLIArg "-lp">] [<Mandatory>] Log_File_Name_Patterns of string list
        | [<CLIAlt "-pd">] Preserve_Days of int
    with
        interface IArgParserTemplate with
            member arg.Usage =
                match arg with
                | Site _ -> "perform site backup"
                | Log -> "trim log files"
                | SourceDir _ -> "specify the folder path of the site"
                | DestinationDir _ -> "specify the folder path for backup archive files"
                | Archive_Name _ -> "archive file name"
                | LogDir _ -> "specify the log file folder, the path relative to site folder"
                | Log_File_Name_Patterns _ -> "naming patterns to recognize log files"
                | Preserve_Days _ -> "preserve log files within latest * days"

module Commands =
    open Arguments
    open System
    open System.IO
    open System.IO.Compression
    open System.Text.RegularExpressions

    let private defaultArchiveName () =
        let dateStr = DateTime.Now.Date.ToString("yyyy-MM-dd")
        "Site" + dateStr + ".zip"

    let (|Site|_|) (args: ParseResults<SiteMaintArguments>) =
        args.TryGetResult <@ SiteMaintArguments.Site @>
        |> Option.map (fun _ ->
            let siteDir = args.GetResult( <@ SourceDir @>, defaultValue = "")
            let dstDir = args.GetResult( <@ DestinationDir @>, defaultValue = "" )
            let fileName = args.GetResult( <@ Archive_Name @>, defaultValue = defaultArchiveName() )
            siteDir, dstDir, fileName
        )

    let ensureDirectory dir =
        if not <| Directory.Exists dir then
            Directory.CreateDirectory dir |> ignore

    let backupSite (siteDir, dstDir, fileName) =
        ensureDirectory dstDir
        ZipFile.CreateFromDirectory(siteDir, Path.Combine(dstDir, fileName))

    let private optionLift3 f a b c =
        Option.bind (fun a' ->
            Option.bind (fun b' ->
                Option.bind (fun c' -> 
                    Some (f a' b' c')
                ) c
            ) b
        ) a

    let (|Log|_|) (args: ParseResults<SiteMaintArguments>) =
        let gather a b c = a, b, c
        args.TryGetResult <@ SiteMaintArguments.Log @>
        |> Option.bind (fun l ->
            let logDir = 
                args.TryGetResult <@ LogDir @>
                |> Option.map (fun ld -> 
                    Path.Combine(args.GetResult (<@ SourceDir @>, defaultValue = ""), ld)
                )
            let filePatterns = 
                args.TryGetResult <@ Log_File_Name_Patterns @>
                |> Option.map (Seq.map (fun str -> Regex(str)))
            let preserveDays = 
                args.GetResult(<@ Preserve_Days @>, defaultValue = 30)
                |> Convert.ToDouble
                |> Some
            optionLift3 gather logDir filePatterns preserveDays
        )

    let siteEssentialFile = 
        Regex(".(dll|css|js|html|cshtml|asax|aspx|ascx|asmx|ashx|xslt|config|Config|xml|dtd|json|jpg|png|gif|eot|svg|ttf|woff)$")
    let trimLogFiles (logDir, filePatterns: Regex seq, preserveDays) =
        let logDirInfo = DirectoryInfo(logDir)
        let isLogFile (fi: FileInfo) =
            let folder acc (rgx: Regex) = 
                acc || rgx.IsMatch(fi.Name)
            if (siteEssentialFile.IsMatch(fi.Name)) then
                false
            else
                Seq.fold folder false filePatterns

        let today = DateTime.Now.Date
        let exceedPreserveDays (fi: FileInfo) =
            fi.CreationTime < today.AddDays(-preserveDays)

        logDirInfo.GetFiles()
        |> Seq.filter (fun f -> 
            (f |> isLogFile) && (f |> exceedPreserveDays)
        )
        |> Seq.iter (fun f -> f.Delete())

