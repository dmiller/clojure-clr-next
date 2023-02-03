namespace Clojure.Collections

open System

[<Sealed>]
type ArrayChunk(arr:obj array,offset:int ,iend:int) =
    
    new(arr,offset) = ArrayChunk(arr,offset,arr.Length)


    interface Counted with
        member _.count() = iend-offset


    interface Indexed with
        member _.nth(i) = arr[offset+i]
        member this.nth(i,nf) =
            if 0 <= i && i < (this:>Counted).count() then  
                (this:>Indexed).nth(i)
            else
                nf

    interface IChunk with
        member _.dropFirst() =
            if offset = iend then
                raise <| InvalidOperationException("dropFirst of empty chunk")
            else
                ArrayChunk(arr,offset+1,iend) 

        member _.reduce(f,start) =
            let ret = f.invoke(start,arr[offset])
            let rec step (ret:obj) idx =
                match ret with  
                | :? Reduced -> ret
                | _ when idx >= iend -> ret
                | _ -> step (f.invoke(ret,arr[idx])) (idx+1)
            step ret (offset+1)



[<Sealed>]
type ChunkBuffer(capacity:int) =

    let mutable buffer : obj array = Array.zeroCreate capacity
    let mutable cnt : int = 0

    interface Counted with
        member _.count() = cnt

    member _.add(o:obj) = 
        buffer[cnt] <- 0
        cnt <- cnt+1

    member _.chunk() : IChunk =
        let ret = ArrayChunk(buffer,0,cnt)
        buffer <- null
        ret

type ChunkedCons(meta:IPersistentMap, chunk:IChunk, more:ISeq) =
    inherit ASeq(meta)

    new(chunk,more) = ChunkedCons(null,chunk,more)

    interface IObj with 
        override this.withMeta(m) =
            if obj.ReferenceEquals(m,meta) then
                this
            else
                ChunkedCons(m,chunk,more)

    interface ISeq with
        override _.first() = chunk.nth(0)
        override this.next() =
            if chunk.count() > 1 then
                ChunkedCons(chunk.dropFirst(),more)
            else 
                (this:>IChunkedSeq).chunkedNext()
        override this.more() =
            if chunk.count() > 1 then
                ChunkedCons(chunk.dropFirst(),more)
            elif isNull more then
                PersistentList.Empty
            else
                more

    interface IChunkedSeq with
        member _.chunkedFirst() = chunk
        member this.chunkedNext() = (this:>IChunkedSeq).chunkedMore().seq()
        member _.chunkedMore() =
            if isNull more then
                PersistentList.Empty
            else 
                more
