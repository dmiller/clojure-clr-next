namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic


type TypedSeqEnumerator<'T when 'T: not struct>(o: obj) =
    let mutable orig = o
    let mutable seqOption : ISeq option = None
    
    interface IEnumerator<'T> with
        member _.Current =
            match seqOption with
            | Some s when s <> null -> s.first() :?> 'T
            | None -> raise <| InvalidOperationException("No current value")
            
    interface IEnumerator with

        // Allowing Reset means we hold on to the head of the seq.
        // This might be bad. So we do not implement it.
        member _.Reset() = raise <| NotSupportedException("Reset not supported on EnumeratorSeq")

        member _.MoveNext() =
            match seqOption with
            | Some s when (s = null) -> false
            | Some s -> 
                let next = s.next()
                seqOption <- Some next
                next <> null
            | None -> 
                let next = RT0.seq(o)
                orig <- null
                seqOption <- Some next
                next <> null

            
        member this.Current = (this :> IEnumerator<'T>).Current :> obj

    member _.Dispose disposing =
        if disposing then
            orig <- null
            seqOption <- None

    interface IDisposable with
        member this.Dispose() =
            this.Dispose(true)
            GC.SuppressFinalize(this)


type SeqEnumerator(s: ISeq) =
    inherit TypedSeqEnumerator<obj>(s)

type IMapEntrySeqEnumerator(s: ISeq) =
    inherit TypedSeqEnumerator<IMapEntry>(s)