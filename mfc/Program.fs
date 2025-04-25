open System
open System.Diagnostics
open CommandLine
open Domain
open FileProcessing
open Helpers
open Options

let parser = new Parser()

let parserResult =
    Environment.GetCommandLineArgs()
    |> Array.tail
    |> parser.ParseArguments<ArgumentOptions>

try
    try
        match parserResult with
        | Parsed as parsed ->
            let stopwatch = Stopwatch.StartNew()
            let processedFiles, processedFolders, exitCode = launchProcessing parsed.Value
            stopwatch.Stop()

            if parsed.Value.Debug then
                showDebugInfo parsed.Value processedFiles processedFolders stopwatch exitCode

            exit (int exitCode)

        | NotParsed as notParsed -> showInfo notParsed.Errors
    with
    | :? AggregateException as aex ->
        Console.Error.WriteLine ""

        aex.InnerExceptions
        |> Seq.iter (fun ex -> Console.Error.WriteLine $"Error: {ex.Message}")

        Console.Error.WriteLine ""
    | ex ->
        Console.Error.WriteLine $"Error: {ex.Message} - {ex.StackTrace}"
finally
    exit (int ExitCode.ErrorsFound)
