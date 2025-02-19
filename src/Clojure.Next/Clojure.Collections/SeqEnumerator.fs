namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic

// Implements enumerations over ISeqs (anything RT0.seq works on).
type TypedSeqEnumerator<'T when 'T: not struct>(o: obj) =

    let mutable _orig = o
    let mutable _seqOption: ISeq option = None

    interface IEnumerator<'T> with
        member _.Current =
            match _seqOption with
            | Some s when not <| isNull s -> s.first () :?> 'T
            | _ -> raise <| InvalidOperationException("No current value")

    interface IEnumerator with

        // Allowing Reset means we hold on to the head of the seq.
        // This might be bad. So we do not implement it.
        // (IEnumerator docs say Reset is for COM compatibility.)
        member _.Reset() =
            raise <| NotSupportedException("Reset not supported on EnumeratorSeq")

        member _.MoveNext() =
            match _seqOption with
            | Some s when isNull s -> false
            | Some s ->
                let next = s.next ()
                _seqOption <- Some next
                not <| isNull next
            | None ->
                let next = RT0.seq (o)
                _orig <- null
                _seqOption <- Some next
                not <| isNull next

        member this.Current = (this :> IEnumerator<'T>).Current :> obj

    member _.Dispose disposing =
        if disposing then
            _orig <- null
            _seqOption <- None

    interface IDisposable with
        member this.Dispose() =
            this.Dispose(true)
            GC.SuppressFinalize(this)

/// Enumerator for ISeqs of objects.
type SeqEnumerator(s: ISeq) =
    inherit TypedSeqEnumerator<obj>(s)

/// Enumerator for ISeqs of IMapEntry objects.
type IMapEntrySeqEnumerator(s: ISeq) =
    inherit TypedSeqEnumerator<IMapEntry>(s)
