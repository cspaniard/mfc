namespace Options

open CommandLine

type ArgumentOptions = {
    [<Option ('s', "separador", Default = "\t")>]
    Separator : string

    [<Option ('b', "bloque-tamaño", Default = 512000L)>]
    BlockSize : int64

    [<Option ('t', "tareas", Default = 10)>]
    SemaphoreSize : int

    [<Option ('d', "debug", Default = false)>]
    Debug : bool

    [<Value (0, MetaName="master-path", Required = true)>]
    MasterPath : string

    [<Value (1, MetaName="backup-path", Required = true)>]
    BackupPath : string
}
