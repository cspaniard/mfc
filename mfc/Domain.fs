module Domain

open System

type BlocksCompareStatus =
    | BlocksAreEqual
    | BlocksAreDifferent
    | BlocksWereCancelled
    | BlocksCompareException of Exception

type FilesCompareStatus =
    | FilesAreEqual
    | FilesAreDifferent
    | FilesWereCancelled
    | FilesCompareException of Exception

type ExitCode =
    | NoErrorsFound = 0
    | ErrorsFound = 1
    | DiferencesNotFound = 10
    | DiferencesFound = 11
