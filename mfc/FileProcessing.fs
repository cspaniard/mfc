module FileProcessing

open System
open System.Threading
open System.IO
open Domain
open Comparison
open Helpers
open Options

// ---------------------------------------------------------------------------------------------------------------------
let processFilesTry (folderPath: NormalizedPath)
                    (processFun: string -> FilesCompareStatus) : FileCount * FolderCount * ExitCode =

    let rec processFilesRec (path : NormalizedPath)
                            (fileAcc: FileCount, folderAcc: FolderCount, exitCode: ExitCode) =

        let files = Directory.GetFiles path.Value

        let processResults =
            files
            |> Array.map processFun

        let newExitCode =
            if exitCode = ExitCode.DiferencesNotFound then
                if processResults |> Array.contains FilesAreDifferent
                then ExitCode.DiferencesFound
                else exitCode
            else
                exitCode

        let paths = Directory.GetDirectories path.Value

        paths
        |> Array.fold (fun (fileCount: FileCount, folderCount: FolderCount, exitCode) path ->
            processFilesRec (NormalizedPath.Create path) (fileCount, folderCount, exitCode))
            (fileAcc + files.Length, folderAcc + paths.Length, newExitCode)

    processFilesRec folderPath (0, 0, ExitCode.DiferencesNotFound)
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let processFile (masterPath: NormalizedPath) (lastBackupPath: NormalizedPath) (blockSize: int64)
                (arrayPool: ArrayPoolLight) (semaphore: SemaphoreSlim) (separator: string)
                (masterFileName: string) : FilesCompareStatus =

    // -----------------------------------------------------------------------------------------------------------------
    let writeFilesAreDifferent (file1: string) (file2: string) =
        Console.WriteLine $"{file1}{separator}{file2}{separator}DIFERENTES"

    let writeFilesAreEqual (file1: string) (file2: string) =
        Console.WriteLine $"{file1}{separator}{file2}{separator}IGUALES"

    let raiseExceptionError (file1: string) (file2: string) (ex : Exception) =
        failwith $"Error al comparar los archivos. {file1} - {file2} - {ex.Message} - {ex.StackTrace}"
    // -----------------------------------------------------------------------------------------------------------------

    let relativeFileName = masterFileName.Replace(masterPath.Value, "").Remove(0, 1)
    let backupFileName = Path.Combine (lastBackupPath.Value, relativeFileName)

    compareTwoFiles masterFileName backupFileName blockSize arrayPool semaphore
    |> fun fileCompareStatus ->
       match fileCompareStatus with
       | FilesAreEqual -> writeFilesAreEqual masterFileName backupFileName
       | FilesAreDifferent -> writeFilesAreDifferent masterFileName backupFileName
       | FilesWereCancelled -> ()
       | FilesCompareException ex -> raiseExceptionError masterFileName backupFileName ex

       fileCompareStatus
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let checkPathsExistTry (paths: NormalizedPath seq) =

    let exceptions =
        [|
            for path in paths do
                if not (Directory.Exists path.Value) then
                    Exception $"La senda no existe: {path.Value}"
        |]

    if exceptions |> Array.isEmpty = false then
        raise <| AggregateException exceptions
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let checkPathsRelationshipsTry (path1: NormalizedPath) (path2: NormalizedPath) =

    if path1.Value.StartsWith(path2.Value + Path.DirectorySeparatorChar.ToString()) ||
       path2.Value.StartsWith(path1.Value + Path.DirectorySeparatorChar.ToString())
    then
        Exception "Las sendas especificadas comparten raíz." |> AggregateException
        |> raise
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let checkPathsAreEqualTry (path1: NormalizedPath) (path2: NormalizedPath) =

    if path1.Value = path2.Value then
        Exception "Las sendas especificadas son iguales." |> AggregateException
        |> raise
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let validatePathsTry (masterPath: NormalizedPath) (backupPath: NormalizedPath) =

    checkPathsExistTry [| masterPath ; backupPath |]
    checkPathsAreEqualTry masterPath backupPath
    checkPathsRelationshipsTry masterPath backupPath
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let launchProcessing (options: ArgumentOptions) =

    let masterPath = NormalizedPath.Create options.MasterPath
    let backupPath = NormalizedPath.Create options.BackupPath

    validatePathsTry masterPath backupPath

    let blockSize = options.BlockSize
    let arrayPool = ArrayPoolLight (blockSize |> int)
    use semaphore = new SemaphoreSlim (options.SemaphoreSize)
    let separator = options.Separator

    let processFileFun = processFile masterPath backupPath blockSize arrayPool semaphore separator

    processFilesTry masterPath processFileFun
// ---------------------------------------------------------------------------------------------------------------------
