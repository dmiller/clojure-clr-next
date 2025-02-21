namespace Clojure.IO

open System.IO
open System.Text

/// A TextReader that allows pushing back characters and tracks line and column numbers.
[<AllowNullLiteral>]
type LineNumberingTextReader(_baseReader: TextReader, _capacity: int) =
    inherit PushbackTextReader(_baseReader, _capacity)

    let mutable _lineNumber = 1
    let mutable _columnNumber = 1
    let mutable _prevColumnNumber = 1
    let mutable _prevLineStart = true
    let mutable _atLineStart = true

    let mutable _index = 0


    let mutable _sb: StringBuilder = null

    let mutable _disposed = false

    /// Create a LineNumberingTextReader with pushback capacity of one character.
    new(baseReader) = new LineNumberingTextReader(baseReader, 1)

    member _.LineNumber
        with get () = _lineNumber
        and set (value) = _lineNumber <- value

    member _.ColumnNumber = _columnNumber
    member _.AtLineStart = _atLineStart
    member _.Index = _index


    override this.Read() : int =

        let mutable ch = base.Read()

        _prevLineStart <- _atLineStart

        if ch = -1 then
            _atLineStart <- true
        else
            _index <- _index + 1
            _atLineStart <- false
            _columnNumber <- _columnNumber + 1

            if ch = ('\r' |> int) then
                if this.Peek() = ('\n' |> int) then
                    ch <- base.Read()
                    _index <- _index + 1
                else
                    this.NoteLineAdvance()

            if ch = ('\n' |> int) then
                this.NoteLineAdvance()

            if not <| isNull _sb && ch <> -1 then
                _sb.Append(char ch) |> ignore

        ch

    member _.NoteLineAdvance() =
        _atLineStart <- true
        _lineNumber <- _lineNumber + 1
        _prevColumnNumber <- _columnNumber - 1
        _columnNumber <- 1


    override this.Unread(ch: int) : unit =
        base.Unread(ch: int)
        _index <- _index - 1
        _columnNumber <- _columnNumber - 1

        if ch = ('\n' |> int) then
            _lineNumber <- _lineNumber - 1
            _columnNumber <- _prevColumnNumber
            _atLineStart <- _prevLineStart

        if not <| isNull _sb then
            _sb.Remove(_sb.Length - 1, 1) |> ignore

    member _.CaptureString() = _sb <- StringBuilder()

    member _.GetString() =
        if isNull _sb then
            null
        else
            let ret = _sb.ToString()
            _sb <- null
            ret

    override _.Dispose(disposing: bool) =
        if not _disposed then
            if disposing then
                if not (isNull _baseReader) then
                    _baseReader.Dispose()

            _disposed <- true
            base.Dispose(disposing)
