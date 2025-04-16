module FileProcessing

open System
open System.Threading
open System.IO
open Comparison
open Helpers
open Options

// ---------------------------------------------------------------------------------------------------------------------
let compareFilesSameSize (file1: string) (file2: string) (fileSize: int64) (blockSize: int64)
                         (arrayPool: ArrayPoolLight) (semaphore: SemaphoreSlim) : FileCompareStatus =

    let cts = new CancellationTokenSource()

    let blockMaxIndex =
        if fileSize % blockSize = 0L then
            (fileSize / blockSize) - 1L
        else
            fileSize / blockSize
        |> int

    let blockCompareTasks =
        [| for blockIndex in 0..blockMaxIndex do
               compareBlockAsync file1 file2 blockSize cts.Token arrayPool semaphore blockIndex |]

    (compareAllBlocksAsync blockCompareTasks cts).GetAwaiter().GetResult()
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let compareTwoFiles (file1: string) (file2: string) (blockSize: int64)
                    (arrayPool: ArrayPoolLight) (semaphore: SemaphoreSlim) (separator: string) =

    let fileSize1 = (FileInfo file1).Length

    if not (File.Exists file2) then
        Console.WriteLine $"{file1}{separator}{file2}{separator}DIFERENTES"
    elif fileSize1 <> (FileInfo file2).Length then
        Console.WriteLine $"{file1}{separator}{file2}{separator}DIFERENTES"
    else
        compareFilesSameSize file1 file2 fileSize1 blockSize arrayPool semaphore
        |> function
            | FileEqual -> Console.WriteLine $"{file1}{separator}{file2}{separator}IGUALES"
            | FileDifferent -> Console.WriteLine $"{file1}{separator}{file2}{separator}DIFERENTES"
            | FileCancelled -> ()
            | FileExceptionError ex ->
                Console.Error.WriteLine $"Error al comparar los archivos. {file1} - {file2} - {ex.Message} - {ex.StackTrace}"
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let processFilesTry (folderPath: string) (processFun: string -> unit) : int * int =

    let rec processFilesRec folder (fileAcc, folderAcc) =

        let files = Directory.GetFiles folder
        files |> Array.iter processFun

        let folders = Directory.GetDirectories folder

        folders
        |> Array.fold (fun (fileAcc, folderAcc) dir ->
            processFilesRec dir (fileAcc, folderAcc)) (fileAcc + files.Length, folderAcc + folders.Length)

    processFilesRec folderPath (0, 0)
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let processFile (masterPath: string) (lastBackupPath: string) (blockSize: int64)
                (arrayPool: ArrayPoolLight) (semaphore: SemaphoreSlim) (separator: string)
                (masterFileName: string) =

    let relativeFileName = masterFileName.Replace(masterPath, "").Remove(0, 1)
    let backupFileName = Path.Combine(lastBackupPath, relativeFileName)

    compareTwoFiles masterFileName backupFileName blockSize arrayPool semaphore separator
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let checkPathsExistTry (paths: string seq) =

    let exceptions =
        [|
            for path in paths do
                if not (Directory.Exists path) then
                    Exception $"La senda no existe: {path}"
        |]

    if Array.isEmpty exceptions = false then
        raise (AggregateException(exceptions))
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let checkPathsRelationsshipsTry (path1: string) (path2: string) =
    let normalizedPath1 = Path.GetFullPath(path1).TrimEnd(Path.DirectorySeparatorChar)
    let normalizedPath2 = Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar)

    if normalizedPath1.StartsWith(normalizedPath2 + Path.DirectorySeparatorChar.ToString()) ||
       normalizedPath2.StartsWith(normalizedPath1 + Path.DirectorySeparatorChar.ToString())
    then
        failwith "Las sendas especificadas comparten raíz."
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let checkPathsAreEqualTry (path1: string) (path2: string) =
    let normalizedPath1 = Path.GetFullPath(path1).TrimEnd(Path.DirectorySeparatorChar)
    let normalizedPath2 = Path.GetFullPath(path2).TrimEnd(Path.DirectorySeparatorChar)

    if normalizedPath1 = normalizedPath2 then
        failwith "Las sendas especificadas son iguales."
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
