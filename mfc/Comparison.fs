module Comparison

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Domain
open Helpers

// ---------------------------------------------------------------------------------------------------------------------
let compareBlockAsync (filePath1: string) (filePath2: string) (blockSize: int64) (ct: CancellationToken)
                      (arrayPool: ArrayPoolLight) (semaphore: SemaphoreSlim)
                      (blockNumber: int) : Task<BlocksCompareStatus> =

    task {
        let mutable buffer1 = Unchecked.defaultof<byte[]>
        let mutable buffer2 = Unchecked.defaultof<byte[]>

        try
            try
                do! semaphore.WaitAsync(ct)

                buffer1 <- arrayPool.RentArray ()

                while buffer1 = null do
                    Console.Error.WriteLine $"Pedido buffer 1 extra {filePath1} - {blockNumber}"
                    buffer1 <- arrayPool.RentArray ()

                buffer2 <- arrayPool.RentArray ()

                while buffer2 = null do
                    Console.Error.WriteLine $"Pedido buffer 2 extra {filePath1} - {blockNumber}"
                    buffer2 <- arrayPool.RentArray ()

                use stream1 = new FileStream (filePath1, FileMode.Open, FileAccess.Read, FileShare.Read)
                use stream2 = new FileStream (filePath2, FileMode.Open, FileAccess.Read, FileShare.Read)

                let offset = blockSize * (blockNumber |> int64)

                ct.ThrowIfCancellationRequested()
                stream1.Seek (offset, SeekOrigin.Begin) |> ignore
                stream2.Seek (offset, SeekOrigin.Begin) |> ignore

                let! bytesRead1 = stream1.ReadAsync (buffer1, 0, (int blockSize), ct)
                let! bytesRead2 = stream2.ReadAsync (buffer2, 0, (int blockSize), ct)

                if bytesRead1 <> bytesRead2 then
                    failwith $"Error de lectura en {filePath1} y {filePath2} - {blockNumber}: longitudes diferentes."

                let span1 = ReadOnlySpan<byte>(buffer1, 0, bytesRead1)
                let span2 = ReadOnlySpan<byte>(buffer2, 0, bytesRead2)

                ct.ThrowIfCancellationRequested()
                if span1.SequenceEqual(span2)
                then return BlocksAreEqual
                else return BlocksAreDifferent
            with
            | :? OperationCanceledException -> return BlocksWereCancelled
            | ex -> return BlocksCompareException ex
        finally
            arrayPool.ReturnArray buffer1
            arrayPool.ReturnArray buffer2
            semaphore.Release() |> ignore
    }
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let compareAllBlocksAsync (tasks: Task<BlocksCompareStatus> array) (cts: CancellationTokenSource) =

    let rec processTasks (remainingTasks: Task<BlocksCompareStatus> array) (tasksCompleted: int) =
        task {
            if Array.isEmpty remainingTasks || tasksCompleted >= tasks.Length then
                return FilesAreEqual
            else
                try
                    let! completedTask = Task.WhenAny remainingTasks
                    let updatedRemainingTasks = remainingTasks |> Array.filter ((<>) completedTask)

                    match completedTask.Result with
                    | BlocksAreEqual ->
                        return! processTasks updatedRemainingTasks (tasksCompleted + 1)
                    | BlocksAreDifferent
                    | BlocksWereCancelled ->
                        cts.Cancel()
                        return FilesAreDifferent
                    | BlocksCompareException ex ->
                        cts.Cancel()
                        return FilesCompareException ex
                with
                | ex ->
                    cts.Cancel()
                    return FilesCompareException ex
        }

    task {
        return! processTasks tasks 0
    }
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let compareFilesSameSize (file1: string) (file2: string) (fileSize: int64) (blockSize: int64)
                         (arrayPool: ArrayPoolLight) (semaphore: SemaphoreSlim) : FilesCompareStatus =

    use cts = new CancellationTokenSource()

    let blockCount = Math.Ceiling((decimal fileSize) / (decimal blockSize)) |> int

    let blockCompareTasks =
        [|
            for blockIndex in 0..blockCount - 1 do
                compareBlockAsync file1 file2 blockSize cts.Token arrayPool semaphore blockIndex
        |]

    (compareAllBlocksAsync blockCompareTasks cts).GetAwaiter().GetResult()
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let compareTwoFiles (file1: string) (file2: string) (blockSize: int64)
                    (arrayPool: ArrayPoolLight) (semaphore: SemaphoreSlim) : FilesCompareStatus =

    let fileSize1 = (FileInfo file1).Length

    if file2 |> File.Exists = false then
        FilesAreDifferent
    elif fileSize1 <> (FileInfo file2).Length then
        FilesAreDifferent
    else
        compareFilesSameSize file1 file2 fileSize1 blockSize arrayPool semaphore
// ---------------------------------------------------------------------------------------------------------------------
