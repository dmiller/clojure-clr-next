namespace Clojure.IO

open System
open Clojure.Collections

type ReaderException(_msg: string, _line: int, _column: int, _inner: exn) =
    inherit Exception(_msg, _inner)

    static let ErrNS = "clojure.error"
    static let ErrLine = Keyword.intern (ErrNS, "line")
    static let ErrColumn = Keyword.intern (ErrNS, "column")

    let data = 
        if _line > 0 then
            RTMap.map (ErrLine, _line, ErrColumn, _column)
        else
            null

    interface IExceptionInfo with
        member this.getData() = data

    new() = ReaderException(null, -1, -1, null)
    new(msg: string) = ReaderException(msg, -1, -1, null)
    new(msg: string, inner: exn) = ReaderException(msg, -1, -1, inner)
