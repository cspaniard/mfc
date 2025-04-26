module FileProcessing

open System
open System.Threading
open System.IO
open Domain
open Comparison
open Helpers
open Options

// ---------------------------------------------------------------------------------------------------------------------
let processFilesTry (folderPath: string)
                    (processFun: string -> FilesCompareStatus) : FileCount * FolderCount * ExitCode =

    let rec processFilesRec folder (fileAcc, folderAcc, exitCode) =

        let files = Directory.GetFiles folder

        let processResults = files |> Array.map processFun

        let newExitCode =
            if exitCode = ExitCode.DiferencesNotFound then
                if processResults |> Array.contains FilesAreDifferent
                then ExitCode.DiferencesFound
                else exitCode
            else
                exitCode

        let folders = Directory.GetDirectories folder

        folders
        |> Array.fold (fun (fileAcc, folderAcc, exitCode) folder ->
            processFilesRec folder (fileAcc, folderAcc, exitCode))
            (fileAcc + files.Length, folderAcc + folders.Length, newExitCode)

    processFilesRec folderPath (0, 0, ExitCode.DiferencesNotFound)
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let processFile (masterPath: string) (lastBackupPath: string) (blockSize: int64)
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

    let relativeFileName = masterFileName.Replace(masterPath, "").Remove(0, 1)
    let backupFileName = Path.Combine(lastBackupPath, relativeFileName)

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
let checkPathsExistTry (paths: string seq) =

    let exceptions =
        [|
            for path in paths do
                if not (Directory.Exists path) then
                    Exception $"La senda no existe: {path}"
        |]

    if exceptions |> Array.isEmpty = false then
        raise (AggregateException(exceptions))
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let checkPathsRelationsshipsTry (path1: string) (path2: string) =

    let normalizedPath1 = Path.GetFullPath(path1).TrimEnd(Path.DirectorySeparatorChar)
    let normalizedPath2 = Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar)

    if normalizedPath1.StartsWith(normalizedPath2 + Path.DirectorySeparatorChar.ToString()) ||
       normalizedPath2.StartsWith(normalizedPath1 + Path.DirectorySeparatorChar.ToString())
    then
        raise (AggregateException(Exception("Las sendas especificadas comparten raíz.")))
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let checkPathsAreEqualTry (path1: string) (path2: string) =

    let normalizedPath1 = Path.GetFullPath(path1).TrimEnd(Path.DirectorySeparatorChar)
    let normalizedPath2 = Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar)

    if normalizedPath1 = normalizedPath2 then
        raise (AggregateException(Exception("Las sendas especificadas son iguales.")))
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let launchProcessing (options: ArgumentOptions) =

        let masterPath = options.MasterPath
        let backupPath = options.BackupPath

        checkPathsExistTry [| masterPath; backupPath |]

        checkPathsAreEqualTry masterPath backupPath
        checkPathsRelationsshipsTry masterPath backupPath

        let blockSize = options.BlockSize
        let arrayPool = ArrayPoolLight(blockSize |> int)
        let semaphore = new SemaphoreSlim(options.SemaphoreSize)
        let separator = options.Separator

        let processFileFun = processFile masterPath backupPath blockSize arrayPool semaphore separator

        processFilesTry masterPath processFileFun
// ---------------------------------------------------------------------------------------------------------------------
