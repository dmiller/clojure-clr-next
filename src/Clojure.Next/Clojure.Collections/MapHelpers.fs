namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics

[<Sealed;AllowNullLiteral>]
type KeySeq private (meta: IPersistentMap, seq: ISeq, enumerable: IEnumerable) =
    inherit ASeq(meta)

    private new(s, e) = KeySeq(null, s, e)

    static member create(s: ISeq) : KeySeq =
        match s with
        | null -> null
        | _ -> KeySeq(s, null)

    static member create(map: IPersistentMap) =
        if isNull map then
            null
        else
            let seq = map.seq ()
            if isNull seq then null else KeySeq(seq, map)


    interface ISeq with
        override _.first() =
            match seq.first () with
            | :? IMapEntry as me -> me.key ()
            | :? DictionaryEntry as de -> de.Key
            | _ ->
                raise
                <| InvalidCastException("Cannot convert hashtable entry to IMapEntry or DictionaryEntry")
        override _.next() = KeySeq.create(seq.next())

    interface IObj with
        override this.withMeta(m) =
            if obj.ReferenceEquals(m, (this :> IMeta).meta ()) then
                this
            else KeySeq(m,seq,enumerable)

    interface IEnumerable<obj> with
        override this.GetEnumerator() =
            match enumerable with
            | null -> base.GetMyEnumeratorT()
            | :? IMapEnumerableTyped<obj,obj> as imet -> imet.tkeyEnumerator()
            | :? IMapEnumerable as ime -> ime.keyEnumerator() :?> IEnumerator<obj>
            | _ -> KeySeq.KeyEnumeratorT(enumerable)

    interface IEnumerable with
        override this.GetEnumerator() = (this:>IEnumerable<obj>).GetEnumerator()

    static member KeyEnumeratorT(enumerable:IEnumerable) = 
        let s =
            Seq.cast<IMapEntry> enumerable
            |>  Seq.map (fun me -> me.key())
        s.GetEnumerator()

[<Sealed;AllowNullLiteral>]
type ValSeq private (meta: IPersistentMap, seq: ISeq, enumerable: IEnumerable) =
    inherit ASeq(meta)

    private new(s, e) = ValSeq(null, s, e)

    static member create(s: ISeq) : ValSeq =
        match s with
        | null -> null
        | _ -> ValSeq(s, null)

    static member create(map: IPersistentMap) =
        if isNull map then
            null
        else
            let seq = map.seq ()
            if isNull seq then null else ValSeq(seq, map)


    interface ISeq with
        override _.first() =
            match seq.first () with
            | :? IMapEntry as me -> me.value ()
            | :? DictionaryEntry as de -> de.Value
            | _ ->
                raise
                <| InvalidCastException("Cannot convert hashtable entry to IMapEntry or DictionaryEntry")
        override _.next() = KeySeq.create(seq.next())

    interface IObj with
        override this.withMeta(m) =
            if obj.ReferenceEquals(m, (this :> IMeta).meta ()) then
                this
            else ValSeq(m,seq,enumerable)

    interface IEnumerable<obj> with
        override this.GetEnumerator() =
            match enumerable with
            | null -> base.GetMyEnumeratorT()
            | :? IMapEnumerableTyped<obj,obj> as imet -> imet.tvalEnumerator()
            | :? IMapEnumerable as ime -> ime.valEnumerator() :?> IEnumerator<obj>
            | _ -> ValSeq.ValEnumeratorT(enumerable)

    interface IEnumerable with
        override this.GetEnumerator() = (this:>IEnumerable<obj>).GetEnumerator()

    static member ValEnumeratorT(enumerable:IEnumerable) = 
        let s =
            Seq.cast<IMapEntry> enumerable
            |>  Seq.map (fun me -> me.value())
        s.GetEnumerator()


type MapEnumerator(map:IPersistentMap) =
    
    let seqEnum = new SeqEnumerator(map.seq()) :> IEnumerator
    let mutable isDisposed = false

    member private _.currentKey = (seqEnum.Current :?> IMapEntry).key()
    member private _.currentVal = (seqEnum.Current :?> IMapEntry).value() 


    interface IDictionaryEnumerator with
        member this.Entry  = DictionaryEntry(this.currentKey,this.currentVal)
        member this.Key = this.currentKey
        member this.Value = this.currentVal
        
    interface IEnumerator with
        member this.Current = seqEnum.Current
        member this.MoveNext() = seqEnum.MoveNext()
        member this.Reset() = seqEnum.Reset()

    interface IDisposable with
        member this.Dispose(): unit = 
            this.Dispose(true)
            GC.SuppressFinalize(this)

    member this.Dispose(disposing:bool) =
        if not isDisposed then
            if disposing && not (isNull seqEnum) then (seqEnum:?>IDisposable).Dispose()
            isDisposed <- true    
        