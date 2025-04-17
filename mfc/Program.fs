open System
open System.Diagnostics
open CommandLine
open FileProcessing
open Helpers
open Options

let parser = new Parser()

let parserResult =
    Environment.GetCommandLineArgs()
    |> Array.tail
    |> parser.ParseArguments<ArgumentOptions>

try
    match parserResult with
    | Parsed as parsed ->
        let stopwatch = Stopwatch.StartNew()
        let processedFiles, processedFolders = launchProcessing parsed.Value
        stopwatch.Stop()

        if parsed.Value.Debug then
            showDebugInfo parsed.Value processedFiles processedFolders stopwatch

    | NotParsed as notParsed -> showInfo notParsed.Errors
with
| :? AggregateException as aex ->
    Console.WriteLine ""

    aex.InnerExceptions
    |> Seq.iter (fun ex -> Console.Error.WriteLine $"Error: {ex.Message}")

    Console.WriteLine ""
| ex ->
    Console.Error.WriteLine $"Error: {ex.Message} - {ex.StackTrace}"
