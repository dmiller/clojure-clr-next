namespace Clojure.IO

open Clojure.Collections
open System

// THis type is needed in the reader and the compiler.
// Reader comes first so we put it here

type IExceptionInfo =
    abstract getData : unit -> IPersistentMap


type ExceptionInfo(msg: string, data: IPersistentMap, innerException: Exception) =
    inherit Exception(msg, innerException)

    let _data : IPersistentMap = if isNull data then PersistentArrayMap.Empty else data

    new (msg: string) = ExceptionInfo(msg, null, null)
    new (msg: string, data: IPersistentMap) = ExceptionInfo(msg, data, null)

    interface IExceptionInfo with
        member this.getData() = _data
