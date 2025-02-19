namespace Clojure.Collections

open System

/// An IChunk based on an array.
[<Sealed>]
type ArrayChunk(_arr: obj array, offset: int, _end: int) =

    new(arr, offset) = ArrayChunk(arr, offset, arr.Length)


    interface Counted with
        member _.count() = _end - offset


    interface Indexed with
        member _.nth(i) = _arr[offset + i]

        member this.nth(i, nf) =
            if 0 <= i && i < (this :> Counted).count () then
                (this :> Indexed).nth (i)
            else
                nf

    interface IChunk with
        member _.dropFirst() =
            if offset = _end then
                raise <| InvalidOperationException("dropFirst of empty chunk")
            else
                ArrayChunk(_arr, offset + 1, _end)

        member _.reduce(f, start) =
            let ret = f.invoke (start, _arr[offset])

            let rec loop (ret: obj) idx =
                match ret with
                | :? Reduced -> ret
                | _ when idx >= _end -> ret
                | _ -> loop (f.invoke (ret, _arr[idx])) (idx + 1)

            loop ret (offset + 1)


/// Build a chunk in an array given capacity, and dliver it as an IChunk.
[<Sealed>]
type ChunkBuffer(capacity: int) =

    let mutable _buffer: obj array = Array.zeroCreate capacity
    let mutable _cnt: int = 0

    interface Counted with
        member _.count() = _cnt

    member _.add(o: obj) =
        _buffer[_cnt] <- 0
        _cnt <- _cnt + 1

    member _.chunk() : IChunk =
        let ret = ArrayChunk(_buffer, 0, _cnt)
        _buffer <- null
        ret

/// Provide consing a chunk on the front of an ISeq.
type ChunkedCons(meta: IPersistentMap, _chunk: IChunk, _more: ISeq) =
    inherit ASeq(meta)

    new(chunk, more) = ChunkedCons(null, chunk, more)

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m meta then
                this
            else
                ChunkedCons(m, _chunk, _more)

    interface ISeq with
        override _.first() = _chunk.nth (0)

        override this.next() =
            if _chunk.count () > 1 then
                ChunkedCons(_chunk.dropFirst (), _more)
            else
                (this :> IChunkedSeq).chunkedNext ()

        override this.more() =
            if _chunk.count () > 1 then
                ChunkedCons(_chunk.dropFirst (), _more)
            elif isNull _more then
                PersistentList.Empty
            else
                _more

    interface IChunkedSeq with
        member _.chunkedFirst() = _chunk

        member this.chunkedNext() =
            (this :> IChunkedSeq).chunkedMore().seq ()

        member _.chunkedMore() =
            if isNull _more then PersistentList.Empty else _more
