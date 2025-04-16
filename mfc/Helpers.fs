module Helpers

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Reflection
open System.Runtime.InteropServices
open CommandLine
open Options

// ---------------------------------------------------------------------------------------------------------------------
type ArrayPoolLight(elementSize: int) =

    let customPool = ConcurrentQueue<byte[]>()

    member this.RentArray () =
        match customPool.TryDequeue() with
        | true, array -> array                                        // Toma un array disponible del pool
        | false, _ -> Array.zeroCreate elementSize                    // Crea un array nuevo si no hay disponible

    member this.ReturnArray (array: byte[]) =
        customPool.Enqueue(array)                                     // Devuelve el array al pool
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let (|Parsed|NotParsed|) (parserResult : ParserResult<_>) =

    match parserResult with
    | :? Parsed<_> -> Parsed
    | :? NotParsed<_> -> NotParsed
    | _ -> failwith "No debiéramos llegar aquí."
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let (|LinuxOs|WindowsOs|MacOs|OtherOs|) _ =

    let knownOsList =
        [ (OSPlatform.Linux, LinuxOs)
          (OSPlatform.Windows, WindowsOs)
          (OSPlatform.OSX, MacOs) ]

    match knownOsList |> List.tryFind (fst >> RuntimeInformation.IsOSPlatform) with
    | Some (_, os) -> os
    | None -> OtherOs
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

    // Mostrar información sobre el uso de memoria
    // let currentProcess = Process.GetCurrentProcess();
    // Console.WriteLine($"Memoria física utilizada: {currentProcess.WorkingSet64 / 1024L / 1024L:N0} MiB");
    // Console.WriteLine($"Memoria privada utilizada: {currentProcess.PrivateMemorySize64 / 1024L / 1024L:N0} MiB");
    // Console.WriteLine($"Memoria virtual utilizada: {currentProcess.VirtualMemorySize64 / 1024L / 1024L:N0} MiB");
// ---------------------------------------------------------------------------------------------------------------------
