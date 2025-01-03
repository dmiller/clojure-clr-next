namespace Clojure.IO

open System.IO
open System.Collections.Generic
open System

[<AllowNullLiteral>]
type PushbackTextReader(baseReader : TextReader, capacity: int) =
    inherit TextReader()

    let buffer = new Stack<int>(capacity)
    let mutable disposed = false

    new(baseReader) = new PushbackTextReader(baseReader, 1)

    member _.BaseReader  = baseReader
    member _.Capacity = capacity

    override _.Peek() : int = if buffer.Count > 0 then buffer.Peek()  else baseReader.Peek()

    abstract member Unread : ch : int -> unit
    default this.Unread(ch: int) = 
        if buffer.Count >= capacity then
            raise <| IOException("Attempt to unread, buffer full")
        buffer.Push(ch)

    override _.Read() : int = 
        if buffer.Count > 0 then
            buffer.Pop()
        else
            baseReader.Read()

    interface IDisposable with
        member this.Dispose (): unit = 
            this.Dispose(true)
            GC.SuppressFinalize(this)

    override this.Dispose (disposing: bool): unit = 
        if not disposed then
            if disposing then
                if not (isNull baseReader) then baseReader.Dispose()
            disposed <- true
            base.Dispose(disposing)




