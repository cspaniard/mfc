module Helpers

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Reflection
open CommandLine
open Options

// ---------------------------------------------------------------------------------------------------------------------
type ArrayPoolLight(elementSize: int) =

    let customPool = ConcurrentQueue<byte[]>()

    member this.RentArray () =
        match customPool.TryDequeue() with
        | true, array -> array
        | false, _ -> Array.zeroCreate elementSize

    member this.ReturnArray (array: byte[]) =
        customPool.Enqueue(array)
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let (|Parsed|NotParsed|) (parserResult : ParserResult<_>) =

    match parserResult with
    | :? Parsed<_> -> Parsed
    | :? NotParsed<_> -> NotParsed
    | _ -> failwith "No debiéramos llegar aquí."
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let showInfo(errors: Error seq) =

    let processErrors (errors: Error seq) =

        for error in errors do
            match error with
            | :? MissingRequiredOptionError ->
                Console.WriteLine "    Falta un parámetro obligatorio."
            | :? BadFormatConversionError as e ->
                Console.WriteLine $"    {e.NameInfo.LongName}: Error de conversión de valores."
            | :? UnknownOptionError as e -> Console.WriteLine $"    Opción desconocida: {e.Token}"
            | _ -> Console.WriteLine $"    Error desconocido. %A{error}"

        Console.WriteLine ""

    let showVersionHeader () =
        let version = Assembly.GetExecutingAssembly().GetName().Version

        Console.WriteLine ""
        Console.Write $"mfc version {version.Major}.{version.Minor}.{version.Build}"
        Console.WriteLine " - (c) Motsoft 2025 by David Sanromá"
        Console.WriteLine ""

    let showArgumentsHelp () =
        Console.WriteLine "OPCIONES:"
        Console.WriteLine ""
        Console.WriteLine ("    {0,-30}Separador de campos. (def: \\t)\n","-s --separador")
        Console.WriteLine ("    {0,-30}Tamaño en bytes de cada bloque de lectura. (def: 512000)\n","-b --bloque-tamaño")
        Console.WriteLine ("    {0,-30}Tareas máximas de lectura en paralelo. (def: 10)\n","-t --tareas")
        Console.WriteLine ("    {0,-30}Modo de depuración. (def: false)\n","-d --debug")
        Console.WriteLine ("    {0,-30}Senda del directorio principal/origen. (Obligatorio)\n","   master-path")
        Console.WriteLine ("    {0,-30}Senda del directorio de backup. (Obligatorio)\n","   backup-path")

    showVersionHeader ()

    match Seq.head errors with
    | :? HelpRequestedError -> showArgumentsHelp ()
    | :? VersionRequestedError -> ()
    | _ ->
        Console.WriteLine "ERRORES:"
        processErrors errors
        showArgumentsHelp ()
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let showDebugInfo (options: ArgumentOptions) (processedFiles: int) (processedFolders: int) (stopwatch: Stopwatch) =

    Console.WriteLine ""
    Console.WriteLine $"Tamaño de bloque: {options.BlockSize:N0} Bytes"
    Console.WriteLine $"Tareas: {options.SemaphoreSize}"
    Console.WriteLine $"Archivos procesados: {processedFiles:N0}"
    Console.WriteLine $"Carpetas procesadas: {processedFolders:N0}"
    Console.WriteLine $"Tiempo transcurrido: {stopwatch.ElapsedMilliseconds:N0} ms"
    Console.WriteLine ""
// ---------------------------------------------------------------------------------------------------------------------
