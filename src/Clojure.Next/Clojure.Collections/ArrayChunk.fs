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

