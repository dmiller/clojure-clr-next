namespace Clojure.IO

open System.IO
open System.Collections.Generic
open System

/// A TextReader that allows pushing back characters.
[<AllowNullLiteral>]
type PushbackTextReader(_baseReader: TextReader, _capacity: int) =
    inherit TextReader()

    /// The character pushback buffer.
    let _buffer = new Stack<int>(_capacity)

    /// Flag indicating whether this reader has been disposed.
    let mutable _disposed = false

    /// Create a PushbackTextReader with capacity of one character.
    new(baseReader) = new PushbackTextReader(baseReader, 1)

    member _.BaseReader = _baseReader
    member _.Capacity = _capacity

    override _.Peek() : int =
        if _buffer.Count > 0 then
            _buffer.Peek()
        else
            _baseReader.Peek()

    /// Push a character into the buffer.
    abstract member Unread: ch: int -> unit

    default this.Unread(ch: int) =
        if _buffer.Count >= _capacity then
            raise <| IOException("Attempt to unread, buffer full")

        _buffer.Push(ch)

    override _.Read() : int =
        if _buffer.Count > 0 then
            _buffer.Pop()
        else
            _baseReader.Read()

    interface IDisposable with
        member this.Dispose() : unit =
            this.Dispose(true)
            GC.SuppressFinalize(this)

    override this.Dispose(disposing: bool) : unit =
        if not _disposed then
            if disposing then
                if not (isNull _baseReader) then
                    _baseReader.Dispose()

            _disposed <- true
            base.Dispose(disposing)
