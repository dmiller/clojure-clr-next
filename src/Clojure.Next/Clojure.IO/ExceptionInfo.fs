namespace Clojure.IO

open Clojure.Collections
open System

// THis type is needed in the reader and the compiler.
// Reader comes first so we put it here

/// An interface for exceptions that provide additional data in an IPersistentMap
[<Interface>]
type IExceptionInfo =
    abstract getData: unit -> IPersistentMap

/// An Exception implementing IExceptionInfo (getData).
type ExceptionInfo(msg: string, data: IPersistentMap, innerException: Exception) =
    inherit Exception(msg, innerException)

    let _data: IPersistentMap = if isNull data then PersistentArrayMap.Empty else data

    /// Create an ExceptionInfo with no data or inner exception.
    new(msg: string) = ExceptionInfo(msg, null, null)

    /// Create an ExceptionInfo with data but no inner exception.
    new(msg: string, data: IPersistentMap) = ExceptionInfo(msg, data, null)

    interface IExceptionInfo with
        member this.getData() = _data
