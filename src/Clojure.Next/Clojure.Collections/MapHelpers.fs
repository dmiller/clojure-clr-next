namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics

/// An ISeq over the keys of an IPersistentMap.
[<Sealed; AllowNullLiteral>]
type KeySeq private (meta: IPersistentMap, _seq: ISeq, _enumerable: IEnumerable) =
    inherit ASeq(meta)

    /// Create a KeySeq from an ISeq with null metadata.
    private new(s, e) = KeySeq(null, s, e)

    /// Create a KeySeq from an ISeq with null metadata.
    static member create(s: ISeq) : KeySeq =
        match s with
        | null -> null
        | _ -> KeySeq(s, null)

    /// Create a KeySeq from an IPersistentMap.
    static member createFromMap(map: IPersistentMap) =
        if isNull map then
            null
        else
            let seq = map.seq ()
            if isNull seq then null else KeySeq(seq, map)


    interface ISeq with
        override _.first() =
            match _seq.first () with
            | :? IMapEntry as me -> me.key ()
            | :? DictionaryEntry as de -> de.Key
            | _ ->
                raise
                <| InvalidCastException("Cannot convert hashtable entry to IMapEntry or DictionaryEntry")

        override _.next() = KeySeq.create (_seq.next ())

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m ((this :> IMeta).meta ()) then
                this
            else
                KeySeq(m, _seq, _enumerable)

    interface IEnumerable<obj> with
        override this.GetEnumerator() =
            match _enumerable with
            | null -> base.GetMyEnumeratorT()
            | :? IMapEnumerableTyped<obj, obj> as imet -> imet.tkeyEnumerator ()
            | :? IMapEnumerable as ime -> ime.keyEnumerator () :?> IEnumerator<obj>
            | _ -> KeySeq.KeyEnumeratorT(_enumerable)

    interface IEnumerable with
        override this.GetEnumerator() =
            (this :> IEnumerable<obj>).GetEnumerator()

    static member KeyEnumeratorT(enumerable: IEnumerable) =
        let s = Seq.cast<IMapEntry> enumerable |> Seq.map (fun me -> me.key ())
        s.GetEnumerator()

/// An ISeq over the values of an IPersistentMap.
[<Sealed; AllowNullLiteral>]
type ValSeq private (meta: IPersistentMap, seq: ISeq, enumerable: IEnumerable) =
    inherit ASeq(meta)

    /// Create a ValSeq from an ISeq with null metadata.
    private new(s, e) = ValSeq(null, s, e)

    /// Create a ValSeq from an ISeq with null metadata.
    static member create(s: ISeq) : ValSeq =
        match s with
        | null -> null
        | _ -> ValSeq(s, null)

    /// Create a ValSeq from an IPersistentMap.
    static member createFromMap(map: IPersistentMap) =
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

        override _.next() = ValSeq.create (seq.next ())

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m ((this :> IMeta).meta ()) then
                this
            else
                ValSeq(m, seq, enumerable)

    interface IEnumerable<obj> with
        override this.GetEnumerator() =
            match enumerable with
            | null -> base.GetMyEnumeratorT()
            | :? IMapEnumerableTyped<obj, obj> as imet -> imet.tvalEnumerator ()
            | :? IMapEnumerable as ime -> ime.valEnumerator () :?> IEnumerator<obj>
            | _ -> ValSeq.ValEnumeratorT(enumerable)

    interface IEnumerable with
        override this.GetEnumerator() =
            (this :> IEnumerable<obj>).GetEnumerator()

    static member ValEnumeratorT(enumerable: IEnumerable) =
        let s = Seq.cast<IMapEntry> enumerable |> Seq.map (fun me -> me.value ())
        s.GetEnumerator()


/// An IEnumerator over an IPersistentMap.
type MapEnumerator(_map: IPersistentMap) =

    let _seqEnum = new SeqEnumerator(_map.seq ()) :> IEnumerator
    let mutable _isDisposed = false

    member private _.currentKey = (_seqEnum.Current :?> IMapEntry).key ()
    member private _.currentVal = (_seqEnum.Current :?> IMapEntry).value ()


    interface IDictionaryEnumerator with
        member this.Entry = DictionaryEntry(this.currentKey, this.currentVal)
        member this.Key = this.currentKey
        member this.Value = this.currentVal

    interface IEnumerator with
        member this.Current = _seqEnum.Current
        member this.MoveNext() = _seqEnum.MoveNext()
        member this.Reset() = _seqEnum.Reset()

    interface IDisposable with
        member this.Dispose() : unit =
            this.Dispose(true)
            GC.SuppressFinalize(this)

    member this.Dispose(disposing: bool) =
        if not _isDisposed then
            if disposing && not (isNull _seqEnum) then
                (_seqEnum :?> IDisposable).Dispose()

            _isDisposed <- true


// ClojureJVM has a class named Box.
// It contains a Val (type Ojbect) that can be get/set.
// THe only use of Box is in PersistentHashMap and PersistentHashTree.
// It is passed around in Assoc and Without calls to track whether a node gets added somewhere below in the tree.
// It is used in very restricted manner.
//     Box addedLeaf = new Box(null)
//     addedLeaf.Val = addedLeaf
//     if (addedLeaf.Val != null ) ...
// In other words, you are set or not, and setting is sticky.
// 
// I decided to cut this down to the minimum.  Just a true/false value.
// So the code snippets above become:
//     let addedLeaf = BoolBox()
//     addedLeaf.set()
//     if addedLeaf.isSet  ...

/// A boxed boolean value
[<Sealed>]
type BoolBox(init) =

    let mutable _value: bool = init

    new() = BoolBox(false)

    member _.set() = _value <- true
    member _.reset() = _value <- false
    member _.isSet = _value
    member _.isNotSet = not _value

// Of course, then I found that PersistentTreeMap actually used Box to return a value.
// So here is that version.

/// A boxed value (nullable)
[<Sealed>]
type ValueBox<'T when 'T : null >(init) =

    let mutable _value: 'T = init

    new() = ValueBox(null)

    member _.Value 
        with get() = _value
        and set(v) = _value <- v