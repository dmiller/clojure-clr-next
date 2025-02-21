namespace Clojure.IO

open System
open Clojure.Collections

/// An exception thrown by the reader.
type ReaderException(_msg: string, _line: int, _column: int, _inner: exn) =
    inherit Exception(_msg, _inner)

    static let ErrNS = "clojure.error"
    static let ErrLine = Keyword.intern (ErrNS, "line")
    static let ErrColumn = Keyword.intern (ErrNS, "column")

    let _data =
        if _line > 0 then
            RTMap.map (ErrLine, _line, ErrColumn, _column)
        else
            null

    interface IExceptionInfo with
        member this.getData() = _data

    /// Create a ReaderException with no source data or inner exception.
    new() = ReaderException(null, -1, -1, null)

    /// Create a ReaderException with a message but no source data or inner exception.
    new(msg: string) = ReaderException(msg, -1, -1, null)

    /// Create a ReaderException with a message and inner exception, but no source data
    new(msg: string, inner: exn) = ReaderException(msg, -1, -1, inner)
