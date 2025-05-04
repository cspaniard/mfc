module Domain

open System
open System.IO

type BlocksCompareStatus =
    | BlocksAreEqual
    | BlocksAreDifferent
    | BlocksWereCancelled
    | BlocksCompareException of Exception

type FileCompareStatus =
    | FilesAreEqual
    | FilesAreDifferent
    | FilesWereCancelled
    | FilesCompareException of Exception

type ExitCode =
    | NoErrorsFound = 0
    | ErrorsFound = 1
    | DiferencesNotFound = 10
    | DiferencesFound = 11

type FileCount = int
type FolderCount = int

type NormalizedPath =
    private NormalizedPath of string
        static member Create (path: string) =
            NormalizedPath (Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar))

        member this.Value = let (NormalizedPath path) = this in path
