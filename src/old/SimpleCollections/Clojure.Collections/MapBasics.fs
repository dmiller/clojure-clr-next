namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Reflection


[<Sealed>]
type MapEnumerator(m: IPersistentMap) =
    let seqenum = new SeqEnumerator(m.seq ())
    let mutable disposed = false

    let ienum = seqenum :> IEnumerator

    member private _.currentKey = (ienum.Current :?> IMapEntry).key()
    member private _.currentVal = (ienum.Current :?> IMapEntry).value()

    interface IDictionaryEnumerator with
        member this.Entry =
            DictionaryEntry(this.currentKey, this.currentVal)

        member this.Key = this.currentKey
        member this.Value = this.currentVal

    interface IEnumerator with
        member _.Current = ienum.Current
        member _.MoveNext() = ienum.MoveNext()
        member _.Reset() = ienum.Reset()

    member _.Dispose disposing =
        if not disposed then
            if disposing && not (isNull ienum) then (seqenum :> IDisposable).Dispose()

            disposed <- true

    interface IDisposable with
        member this.Dispose() =
            this.Dispose(true)
            GC.SuppressFinalize(this)


[<Sealed>]
[<AllowNullLiteral>]
type KeySeq(meta, iseq, ienum) =
    inherit ASeq(meta)

    new(s, e) = KeySeq(null, s, e)

    static member create(s: ISeq): KeySeq =
        match s with
        | null -> null
        | _ -> KeySeq(s, null)

    static member createFromMap(m: IPersistentMap): KeySeq =
        if isNull m then
            null
        else
            let s = m.seq ()
            if isNull s then null else KeySeq(s, m)

    interface ISeq with
        override _.first() =
            match iseq.first () with
            | :? IMapEntry as me -> me.key ()
            | :? DictionaryEntry as de -> de.Key
            | _ ->
                raise
                <| InvalidCastException("Cannot convert hashtable entry to IMapEntry or DictionaryEntry")

        override _.next() = upcast KeySeq.create (iseq.next ())

    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(m, meta) then upcast this else upcast KeySeq(m, iseq, ienum)

    static member private keyIterator(e: IEnumerable): IEnumerator =
        let s =
            seq {
                for item in e do
                    yield (item :?> IMapEntry).key()
            }
        upcast s.GetEnumerator()


    interface IEnumerable with
        override _.GetEnumerator() =
            match ienum with
            | null -> base.GetMyEnumerator()
            | :? IMapEnumerable as imi -> imi.keyEnumerator ()
            | _ -> KeySeq.keyIterator (ienum)

[<Sealed>]
[<AllowNullLiteral>]
type ValSeq(meta, iseq: ISeq, ienum: IEnumerable) =
    inherit ASeq(meta)

    new(s, e) = ValSeq(null, s, e)

    static member create(s: ISeq): ValSeq =
        match s with
        | null -> null
        | _ -> ValSeq(s, null)

    static member createFromMap(m: IPersistentMap): ValSeq =
        if isNull m then
            null
        else
            let s = m.seq ()
            if isNull s then null else ValSeq(s, m)

    interface ISeq with
        override _.first() =
            match iseq.first () with
            | :? IMapEntry as me -> me.value ()
            | :? DictionaryEntry as de -> de.Value
            | _ ->
                raise
                <| InvalidCastException("Cannot convert hashtable entry to IMapEntry or DictionaryEntry")

        override _.next() = upcast ValSeq.create (iseq.next ())

    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(m, meta) then upcast this else upcast ValSeq(m, iseq, ienum)

    static member keyIterator(e: IEnumerable): IEnumerator =
        let s =
            seq {
                for item in e do
                    yield (item :?> IMapEntry).value()
            }
        upcast s.GetEnumerator()


    interface IEnumerable with
        override _.GetEnumerator() =
            match ienum with
            | null -> base.GetMyEnumerator()
            | :? IMapEnumerable as imi -> imi.keyEnumerator ()
            | _ -> ValSeq.keyIterator (ienum)


// TODO: Need to finish this.
// should be MapEntry -> AMapEntry -> APersistetntVector, so we need to get that in here ahead  APersistentVector is 1140+ SLOC.  AMapEntry is over 250.
// We don't need all this just to get APersistentMap going

[<AllowNullLiteral>]
type MapEntry(key: obj, value: obj) =

    static member create(k, v) = MapEntry(k, v) // not sure why we need this, but here it is

    interface IMapEntry with
        member _.key() = key
        member _.value() = value



[<AbstractClass>]
[<AllowNullLiteral>]
type APersistentMap() =
    inherit AFn()

    let mutable hash: int option = None
    let mutable hasheq: int option = None

    // Some static methods
    // Could be put into their own module?

    static member mapEquals(m: IPersistentMap, o: obj): bool =

        let rec step (s: ISeq) (d: IDictionary) =
            if isNull s then
                true
            else
                let me: IMapEntry = downcast s.first ()

                if d.Contains(me.key)
                   && Util.equals (me.value (), d.[me.key ()]) then
                    step (s.next ()) d
                else
                    false

        let mapDictEquals (m: IPersistentMap) (d: IDictionary): bool =
            if d.Count <> m.count () then false else step (m.seq ()) d


        if Object.ReferenceEquals(m, o) then
            true
        else
            match o with
            | :? IDictionary as d -> mapDictEquals m d
            | _ -> false

    static member mapHash(m: IPersistentMap): int =
        let rec step (s: ISeq) h =
            if isNull s then
                h
            else
                let me: IMapEntry = downcast s.first ()

                let hk =
                    if me.key () |> isNull then 0 else me.key().GetHashCode()

                let hv =
                    if me.value () |> isNull then 0 else me.value().GetHashCode()

                step (s.next ()) (h + hk ^^^ hv)

        step (m.seq ()) 0

    // do we still need this?  Still in this code, but not used here

    static member mapHasheq (m: IPersistentMap) int = Util.hashUnordered (m)


    // Object overrides

    override this.ToString() =
        // complete and total hack until I get RTEnv intiailized figured out:  TODO: FIX THIS!
        if not RTEnv.isInitialized then RTEnvInitialization.initialize ()

        RT.printString (this)

    override this.Equals(o) = APersistentMap.mapEquals (this, o)

    override this.GetHashCode() =
        match hash with
        | None ->
            let h = APersistentMap.mapHash (this)
            hash <- Some h
            h
        | Some h -> h

    interface IMeta with
        member _.meta() =
            raise
            <| NotImplementedException("You must implement meta in derived classes")

    interface IObj with
        member _.withMeta(m) =
            raise
            <| NotImplementedException("You must implement withMeta in derived classes")

    interface Counted with
        member _.count() =
            raise
            <| NotImplementedException("You must implement count in derived classes")



    interface Seqable with
        member _.seq() =
            raise
            <| NotImplementedException("You must implement seq in derived classes")

    interface IPersistentCollection with
        member this.count() = (this :> Counted).count()

        member _.empty() =
            raise
            <| NotImplementedException("You must implement empty in derived classes")

        member this.cons(o) = upcast (this :> IPersistentMap).cons(o)

        member this.equiv(o) =
            match o with
            | :? IDictionary as d ->
                if o :? IPersistentMap && not (o :? MapEquivalence) then
                    false
                elif d.Count <> (this :> IPersistentCollection).count() then
                    false
                else
                    let rec step (s: ISeq) =
                        if isNull s then
                            true
                        else
                            let me: IMapEntry = downcast s.first ()

                            if d.Contains(me.key ())
                               && Util.equiv (me.value (), d.[me.key ()]) then
                                step (s.next ())
                            else
                                false

                    step ((this :> Seqable).seq())
            | _ -> false


    interface ILookup with
        member _.valAt(k) =
            raise
            <| NotImplementedException("You must implement valAt in derived classes")

        member _.valAt(k, nf) =
            raise
            <| NotImplementedException("You must implement valAt in derived classes")


    interface Associative with
        member this.assoc(k, v) =
            upcast (this :> IPersistentMap).assoc(k, v)

        member _.containsKey(k) =
            raise
            <| NotImplementedException("You must implement containsKey in derived classes")

        member _.entryAt(k) =
            raise
            <| NotImplementedException("You must implement entryAt in derived classes")


    // TODO: conversion to an IMapEntry could be a protocol. Would simplify code in a lot of places
    interface IPersistentMap with
        member _.assoc(k, v) =
            raise
            <| NotImplementedException("You must implement entryAt in derived classes")

        member _.assocEx(k, v) =
            raise
            <| NotImplementedException("You must implement entryAt in derived classes")

        member _.without(k) =
            raise
            <| NotImplementedException("You must implement entryAt in derived classes")

        member this.count() = (this :> Counted).count()

        member this.cons(o) =
            match o with
            | null -> upcast this
            | :? IMapEntry as e ->
                (this :> IPersistentMap)
                    .assoc(e.key (), e.value ())
            | :? DictionaryEntry as e -> (this :> IPersistentMap).assoc(e.Key, e.Value)
            | _ when o.GetType().IsGenericType
                     && o.GetType().Name = "KeyValuePair`2" ->
                let t = o.GetType()

                let k =
                    t.InvokeMember("Key", BindingFlags.GetProperty, null, o, null)

                let v =
                    t.InvokeMember("Value", BindingFlags.GetProperty, null, o, null)

                (this :> IPersistentMap).assoc(k, v)
            | :? IPersistentVector as v ->
                if v.count () = 2 then
                    (this :> IPersistentMap)
                        .assoc(v.nth (0), v.nth (1))
                else
                    raise
                    <| ArgumentException("o", "Vector arg to map cons must be a pair")
            | _ ->
                let rec step (s: ISeq) (m: IPersistentMap) =
                    if isNull s then
                        m
                    else
                        let me = s.first () :?> IMapEntry
                        step (s.next ()) (m.assoc (me.key (), me.value ()))

                step (RT.seq (o)) this

    interface IFn with
        override this.invoke(arg1) = (this :> ILookup).valAt(arg1)
        override this.invoke(arg1, arg2) = (this :> ILookup).valAt(arg1, arg2)

    interface IHashEq with
        member x.hasheq() =
            match hasheq with
            | None ->
                let h = Util.hashUnordered (x :> IEnumerable)
                hasheq <- Some h
                h
            | Some h -> h

    //interface IDictionary<obj,obj> with
    //    member x.Add(k,v) = raise <| InvalidOperationException("Cannot modify an immutable map")
    //    member x.Keys = KeySeq.create((x:>Seqable).seq())
    //    member x.Values = ValSeq.create((x:>Sequable).seq())
    //    member x.Item
    //        with get key = (x:>ILookup).valAt(key)
    //        and set _ _ = raise <| InvalidOperationException("Cannot modify an immutable map")

    //interface ICollection<KeyValuePair<obj,obj>> with
    //    member x.Add(kv) = raise <| InvalidOperationException("Cannot modify an immutable map")
    //    member x.Clear() = raise <| InvalidOperationException("Cannot modify an immutable map")
    //    member x.Remove(kv) = raise <| InvalidOperationException("Cannot modify an immutable map")
    //    member x.IsReadOnly = true
    //    member x.Count = (x:>IPersistentMap).count()
    //    member x.CopyTo(arr, index) = () // IPMLEMENT
    //    member x.Contains(kv) =
    //        let ok, value = (x:>IDictionary<obj,obj>).TryGetValue(kv.Key)
    //        if not ok then false
    //        elif isNull value then isNull kv.Value
    //        else value.Equals(kv.Value)
    //    member x.GetEnumerator() =
    //        let mySeq =
    //            seq {
    //                    // can't use my usual recursive step function for iteration here because yield needs to be at top level
    //                    let mutable s = (x:>Seqable).seq()
    //                    while not (isNull x) do
    //                        let me : IMapEntry = downcast s.first()
    //                        yield KeyValuePair<obj,obj>(me.key(),me.value())
    //                        s <- s.next()
    //                }
    //        mySeq.GetEnumerator()


    interface IEnumerable with
        member this.GetEnumerator(): IEnumerator =
            new SeqEnumerator((this :> Seqable).seq()) :> IEnumerator

    interface IEnumerable<IMapEntry> with
        member this.GetEnumerator() =
            let s =
                seq {
                    let mutable s = (this :> Seqable).seq()

                    while not (isNull s) do
                        yield s.first () :?> IMapEntry
                        s <- s.next ()
                }

            s.GetEnumerator()


    //static member private keyIterator (e:IEnumerable) : IEnumerator =
    //    let s =
    //        seq {
    //                for item in e do
    //                    yield (item :?> IMapEntry).key()
    //            }
    //    upcast s.GetEnumerator()


    interface ICollection with
        member _.IsSynchronized = true
        member this.SyncRoot = upcast this
        member this.Count = (this :> IPersistentMap).count()

        member this.CopyTo(arr, idx) =
            if isNull arr then raise <| ArgumentNullException("array")

            if idx < 0 then
                raise
                <| ArgumentOutOfRangeException("arrayIndex", "must be non-negative")

            if arr.Rank <> 1 then
                raise
                <| ArgumentException("Array must be 1-dimensional")

            if idx >= arr.Length then
                raise
                <| ArgumentException("index", "must be less than the length")

            if (this :> IPersistentCollection).count() > arr.Length - idx then
                raise
                <| InvalidOperationException("Not enough available space from index to end of the array.")

            let rec step (i: int) (s: ISeq) =
                if not (isNull s) then
                    arr.SetValue(s.first (), i)
                    step (i + 1) (s.next ())

            step idx ((this :> Seqable).seq())

    interface IDictionary with
        member _.IsFixedSize = true
        member _.IsReadOnly = true

        member _.Add(k, v) =
            raise
            <| InvalidOperationException("Cannot modify an immutable map")

        member _.Clear() =
            raise
            <| InvalidOperationException("Cannot modify an immutable map")

        member _.Remove(k) =
            raise
            <| InvalidOperationException("Cannot modify an immutable map")

        member this.Keys =
            upcast KeySeq.create ((this :> Seqable).seq())

        member this.Values =
            upcast ValSeq.create ((this :> Seqable).seq())

        member this.Contains(k) = (this :> Associative).containsKey(k)

        member this.Item
            with get key = (this :> ILookup).valAt(key)
            and set _ _ =
                raise
                <| InvalidOperationException("Cannot modify an immutable map")

        member this.GetEnumerator() = upcast new MapEnumerator(this)

[<AbstractClass>]
type ATransientMap() =
    inherit AFn()

    abstract ensureEditable: unit -> unit
    abstract doAssoc: obj * obj -> ITransientMap
    abstract doWithout: key:obj -> ITransientMap
    abstract doValAt: obj * obj -> obj
    abstract doCount: unit -> int
    abstract doPersistent: unit -> IPersistentMap

    interface ITransientCollection with
        member this.persistent() =
            upcast (this :> ITransientMap).persistent()

        member this.conj(o) = upcast this.conj (o)

    member this.conj(value: obj): ITransientMap =
        this.ensureEditable ()

        // TODO: add KeyValuePair?  (also not in C# version)
        // TODO: find general method for handling heys
        match value with
        | :? IMapEntry as e ->
            downcast (this :> ITransientAssociative)
                .assoc(e.key (), e.value ())
        | :? DictionaryEntry as e ->
            downcast (this :> ITransientAssociative)
                .assoc(e.Key, e.Value)
        | :? IPersistentVector as v ->
            if v.count () <> 2 then
                raise
                <| ArgumentException("value", "vector arg to map conj must be a pair")
            downcast (this :> ITransientAssociative)
                .assoc(v.nth (0), v.nth (1))
        | _ ->
            let mutable ret: ITransientMap = upcast this
            let mutable es: ISeq = RT.seq (value)

            while not (isNull es) do
                let e: IMapEntry = downcast es.first ()
                ret <- ret.assoc (e.key (), e.value ())

            ret

    static member private NotFound: obj = obj ()

    interface ILookup with
        member this.valAt(key: obj) = (this :> ILookup).valAt(key, null)

        member this.valAt(key: obj, notFound: obj) =
            this.ensureEditable ()
            this.doValAt (key, notFound)

    interface ITransientAssociative with
        member this.assoc(k, v) =
            upcast (this :> ITransientMap).assoc(k, v)

    interface ITransientAssociative2 with
        member this.containsKey(key: obj) =
            (this :> ILookup)
                .valAt(key, ATransientMap.NotFound)
            <> ATransientMap.NotFound

        member this.entryAt(key: obj) =
            let v =
                (this :> ILookup)
                    .valAt(key, ATransientMap.NotFound)

            if v = ATransientMap.NotFound then null else upcast MapEntry.create (key, v)

    interface ITransientMap with
        member this.assoc(key, value) =
            this.ensureEditable ()
            this.doAssoc (key, value)

        member this.without(key) =
            this.ensureEditable ()
            this.doWithout (key)

        member this.persistent() =
            this.ensureEditable ()
            this.doPersistent ()

    interface Counted with
        member this.count() =
            this.ensureEditable ()
            this.doCount ()

    interface IFn with
        override this.invoke(arg1) = (this :> ILookup).valAt(arg1)
        override this.invoke(arg1, arg2) = (this :> ILookup).valAt(arg1, arg2)
