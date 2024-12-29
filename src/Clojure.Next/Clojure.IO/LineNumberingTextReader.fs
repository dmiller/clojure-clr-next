namespace Clojure.IO

open System.IO
open System.Text

type LineNumberingTextReader(baseReader : TextReader, capacity: int) =
    inherit PushbackTextReader(baseReader, capacity)

    let mutable lineNumber = 1
    let mutable columnNumber = 1
    let mutable prevColumnNumber = 1
    let mutable prevLineStart = true
    let mutable atLineStart = true

    let mutable index = 0


    let mutable sb : StringBuilder = null

    let mutable disposed = false

    new (baseReader) = new LineNumberingTextReader(baseReader, 1)

    member _.LineNumber 
        with get() = lineNumber
        and set(value) = lineNumber <- value

    member _.ColumnNumber = columnNumber
    member _.AtLineStart = atLineStart
    member _.Index = index


    override this.Read() : int = 

        let mutable ch = baseReader.Read()

        prevLineStart <- atLineStart
       
        if ch = -1 then
            atLineStart <- true
        else
            index <- index + 1
            atLineStart <- false
            columnNumber <- columnNumber + 1
            if ch = ('\r' |> int) then 
                if this.Peek() = ('\n' |> int) then
                    ch <- baseReader.Read() 
                    index <- index + 1
                else 
                    this.NoteLineAdvance()
            if ch = ('\n' |> int) then
                this.NoteLineAdvance()
            if not <| isNull sb && ch <> -1 then
                sb.Append(char ch) |> ignore
        ch

    member _.NoteLineAdvance() =
        atLineStart <- true
        lineNumber <- lineNumber + 1
        prevColumnNumber <- columnNumber - 1
        columnNumber <- 1


    override this.Unread (ch: int): unit = 
        base.Unread(ch: int)
        index <- index - 1
        columnNumber <- columnNumber - 1
        if ch = ('\n' |> int) then
            lineNumber <- lineNumber - 1
            columnNumber <- prevColumnNumber
            atLineStart <- prevLineStart
        if not <| isNull sb then
            sb.Remove(sb.Length - 1,1) |> ignore

    member _.CaptureString() = sb <- StringBuilder()

    member _.GetString() = 
        if isNull sb then
            null
        else
            let ret = sb.ToString()
            sb <- null
            ret

    override _.Dispose(disposing: bool) = 
        if not disposed then
            if disposing then
                if not (isNull baseReader) then baseReader.Dispose()            
            disposed <- true
            base.Dispose(disposing)