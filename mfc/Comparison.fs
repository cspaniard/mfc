module Comparison

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Helpers

type BlockCompareStatus =
    | BlockEqual
    | BlockDifferent
    | BlockCancelled
    | BlockExceptionError of Exception

type FileCompareStatus =
    | FileEqual
    | FileDifferent
    | FileCancelled
    | FileExceptionError of Exception


// ---------------------------------------------------------------------------------------------------------------------
let compareBlockAsync (filePath1: string) (filePath2: string) (blockSize: int64) (ct: CancellationToken)
                      (arrayPool: ArrayPoolLight) (semaphore: SemaphoreSlim)
                      (blockNumber: int) : Task<BlockCompareStatus> =

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

                let stream1 = new FileStream (filePath1, FileMode.Open, FileAccess.Read, FileShare.Read)
                let stream2 = new FileStream (filePath2, FileMode.Open, FileAccess.Read, FileShare.Read)
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
                then return BlockEqual
                else return BlockDifferent
            finally
                arrayPool.ReturnArray buffer1
                arrayPool.ReturnArray buffer2

                semaphore.Release() |> ignore
        with
        | :? OperationCanceledException -> return BlockCancelled
        | ex -> return BlockExceptionError ex
    }
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
let compareAllBlocksAsync (tasks: Task<BlockCompareStatus> array) (cts: CancellationTokenSource) =

    let rec processTasks (remainingTasks: Task<BlockCompareStatus> array) (tasksCompleted: int) =
        task {
            if Array.isEmpty remainingTasks || tasksCompleted >= tasks.Length then
                return FileEqual
            else
                try
                    let! completedTask = Task.WhenAny remainingTasks
                    let updatedRemainingTasks = remainingTasks |> Array.filter ((<>) completedTask)

                    match completedTask.Result with
                    | BlockEqual ->
                        return! processTasks updatedRemainingTasks (tasksCompleted + 1)
                    | BlockDifferent
                    | BlockCancelled ->
                        cts.Cancel()
                        return FileDifferent
                    | BlockExceptionError ex ->
                        cts.Cancel()
                        return FileExceptionError ex
                with
                | ex ->
                    cts.Cancel()
                    return FileExceptionError ex
        }

    task {
        return! processTasks tasks 0
    }
// ---------------------------------------------------------------------------------------------------------------------

// ---------------------------------------------------------------------------------------------------------------------
// Guardamos como posible referencia.
// ---------------------------------------------------------------------------------------------------------------------
// let compareBlocksAsync (tasks : Task<BlockCompareStatus> array) (cts : CancellationTokenSource) =
//     let mutable tasksCompleted = 0
//     let mutable exitLoop = false
//     let mutable blockCompareStatus = Unchecked.defaultof<BlockCompareStatus>
//     let mutable remainingTasks = tasks |> List.ofArray
//
//     task {
//         while not exitLoop && tasksCompleted < tasks.Length do
//             try
//                 let! completedTask = Task.WhenAny remainingTasks
//                 remainingTasks <- remainingTasks |> List.filter ((<>) completedTask)
//                 tasksCompleted <- tasksCompleted + 1
//
//                 blockCompareStatus <- completedTask.Result
//
//                 match blockCompareStatus with
//                 | BlockEqual -> ()
//                 | _ ->
//                     cts.Cancel()
//                     exitLoop <- true
//             with
//             | ex ->
//                 cts.Cancel()
//                 blockCompareStatus <- BlockExceptionError ex
//                 exitLoop <- true
//
//         return
//             match blockCompareStatus with
//             | BlockEqual -> FileEqual
//             | BlockDifferent -> FileDifferent
//             | BlockCancelled -> FileCancelled
//             | BlockExceptionError ex -> FileExceptionError ex
//     }
//
// let compareBlocksAsync3 (tasks : Task<BlockCompareStatus> array) (cts : CancellationTokenSource) =
//     let rec processRemainingTasks (remainingTasks : Task<BlockCompareStatus> array) tasksCompleted lastBlockStatus =
//         task {
//             if tasksCompleted = tasks.Length then
//                 return
//                     match lastBlockStatus with
//                     | BlockEqual -> FileEqual
//                     | BlockDifferent -> FileDifferent
//                     | BlockCancelled -> FileCancelled
//                     | BlockExceptionError ex -> FileExceptionError ex
//             else
//                 try
//                     let! completedTask = Task.WhenAny remainingTasks
//                     let newRemainingTasks = remainingTasks |> Array.filter ((<>) completedTask)
//                     let blockStatus = completedTask.Result
//
//                     match blockStatus with
//                     | BlockEqual ->
//                         return! processRemainingTasks newRemainingTasks (tasksCompleted + 1) blockStatus
//                     | _ ->
//                         cts.Cancel()
//                         return
//                             match blockStatus with
//                             | BlockDifferent -> FileDifferent
//                             | BlockCancelled -> FileCancelled
//                             | BlockExceptionError ex -> FileExceptionError ex
//                             | _ -> FileEqual // Este caso nunca debería ocurrir
//                 with
//                 | ex ->
//                     cts.Cancel()
//                     return FileExceptionError ex
//         }
//
//     processRemainingTasks tasks 0 BlockEqual
