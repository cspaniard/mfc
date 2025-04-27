open System
open System.Diagnostics
open CommandLine
open Domain
open FileProcessing
open Helpers
open Options

let parser = new Parser ()

let parserResult =
    Environment.GetCommandLineArgs ()
    |> Array.tail
    |> parser.ParseArguments<ArgumentOptions>

match parserResult with
| Parsed as parsed ->
    try
        let stopwatch = Stopwatch.StartNew ()
        try
            let processedFiles, processedFolders, exitCode = launchProcessing parsed.Value
            stopwatch.Stop ()

            if parsed.Value.Debug then
                showDebugInfo parsed.Value processedFiles processedFolders stopwatch exitCode

            exit (int exitCode)
        finally
            stopwatch.Stop ()
    with
    | :? AggregateException as aex ->
        Console.Error.WriteLine ""

        aex.InnerExceptions
        |> Seq.iter (fun ex -> Console.Error.WriteLine $"Error: {ex.Message}")

        Console.Error.WriteLine ""
    | ex ->
        Console.Error.WriteLine $"Error: {ex.Message} - {ex.StackTrace}"

    if parsed.Value.Debug then
        showExitCode ExitCode.ErrorsFound
        Console.WriteLine ""

    exit (int ExitCode.ErrorsFound)

| NotParsed as notParsed -> processParsingErrors notParsed.Errors |> int |> exit
