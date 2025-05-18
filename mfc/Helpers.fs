module Helpers

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Reflection
open CommandLine
open Domain
open Options

// ---------------------------------------------------------------------------------------------------------------------
type ArrayPoolLight (elementSize: int) =

    let customPool = ConcurrentQueue<byte[]> ()

    member this.RentArray () =
        match customPool.TryDequeue () with
        | true, array -> array
        | false, _ -> Array.zeroCreate elementSize

    member this.ReturnArray (array: byte[]) =
        customPool.Enqueue array
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let (|Parsed|NotParsed|) (parserResult : ParserResult<_>) =

    match parserResult with
    | :? Parsed<_> -> Parsed
    | :? NotParsed<_> -> NotParsed
    | _ -> failwith "No debiéramos llegar aquí."
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let processParsingErrors (errors: Error seq) =

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

    let showHelp () =
        Console.WriteLine "OPCIONES:"
        Console.WriteLine ""
        Console.WriteLine ("    {0,-30}Separador de campos. (def: \\t)\n","-s --separador")
        Console.WriteLine ("    {0,-30}Tamaño en bytes de cada bloque de lectura. (def: 512000)\n","-b --bloque-tamaño")
        Console.WriteLine ("    {0,-30}Tareas máximas de lectura en paralelo. (def: 10)\n","-t --tareas")
        Console.WriteLine ("    {0,-30}Modo de depuración. (def: false)\n","-d --debug")
        Console.WriteLine ("    {0,-30}Usar la codificación indicada.\n","-e --encoding")
        Console.WriteLine ("    {0,-30}Muestra esta ayuda.\n","   --help")
        Console.WriteLine ("    {0,-30}Muestra la version.\n","   --version")
        Console.WriteLine ("    {0,-30}Senda del directorio principal/origen. (Obligatorio)\n","   master-path")
        Console.WriteLine ("    {0,-30}Senda del directorio de backup. (Obligatorio)\n","   backup-path")

        Console.WriteLine ""
        Console.WriteLine "NOTAS:"
        Console.WriteLine "    Se devuelven los siguientes códigos de salida:"
        Console.WriteLine ("        {0, 2}: Se encontraron errores en el procesado.", int ExitCode.ErrorsFound)
        Console.WriteLine ("        {0, 2}: No se encontraron diferencias.", int ExitCode.DiferencesNotFound)
        Console.WriteLine ("        {0, 2}: Se encontraron diferencias.", int ExitCode.DiferencesFound)
        Console.WriteLine ""

        Console.WriteLine "Encodings Disponibles:"
        System.Text.Encoding.GetEncodings()
        |> Array.iter (fun ei -> Console.WriteLine $"    {ei.Name,-12} {ei.DisplayName} ({ei.CodePage})")
        Console.WriteLine ""


    showVersionHeader ()

    match Seq.head errors with
    | :? HelpRequestedError ->
        showHelp ()
        ExitCode.NoErrorsFound
    | :? VersionRequestedError -> ExitCode.NoErrorsFound
    | _ ->
        Console.WriteLine "ERRORES:"
        processErrors errors
        showHelp ()
        ExitCode.ErrorsFound
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let showExitCode (exitCode: ExitCode) =
    Console.WriteLine $"Código de salida: {int exitCode}"
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let showDebugInfo (options: ArgumentOptions) (processedFiles: int) (processedFolders: int)
                  (stopwatch: Stopwatch) (exitCode : ExitCode) =

    Console.WriteLine ""
    Console.WriteLine $"Tamaño de bloque: {options.BlockSize:N0} Bytes"
    Console.WriteLine $"Tareas: {options.SemaphoreSize}"
    Console.WriteLine $"Archivos procesados: {processedFiles:N0}"
    Console.WriteLine $"Carpetas procesadas: {processedFolders:N0}"
    Console.WriteLine $"Tiempo transcurrido: {stopwatch.ElapsedMilliseconds:N0} ms"
    showExitCode exitCode
    Console.WriteLine ""
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let setEnconding (options: ArgumentOptions) =

    try
        if options.Encoding <> "" then
            Console.OutputEncoding <- System.Text.Encoding.GetEncoding options.Encoding
    with
    | _ -> Exception $"La codificación especificada no está soportada en esta plataforma: {options.Encoding}."
           |> AggregateException |> raise
// ---------------------------------------------------------------------------------------------------------------------
