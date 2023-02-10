namespace Clojure.Collections

open Clojure.Numerics
open System.Collections
open System
open System.Collections.Generic




[<AbstractClass>]
type APersistentMap() =
    inherit AFn()

    let mutable hasheq: int option = None

    override this.ToString() = RTPrint.printString (this)
    override this.Equals(o) = APersistentMap.mapEquals (this, o)

    override this.GetHashCode() =
        match hasheq with
        | Some h -> h
        | None ->
            let h = Hashing.hashUnordered (this)
            hasheq <- Some h
            h


    static member mapEquals(m1: IPersistentMap, o: obj) =
        match o with
        | _ when obj.ReferenceEquals(m1, o) -> true
        | :? IDictionary as d ->
            if d.Count <> m1.count () then
                false
            else
                let rec loop (s: ISeq) =
                    if isNull s then
                        true
                    else
                        let me = s.first () :?> IMapEntry

                        if not <| d.Contains(me.key ()) || not <| Util.equals (me.value (), d[me.key ()]) then
                            false
                        else
                            loop (s.next ())

                loop (m1.seq ())
        | _ -> false

    interface IHashEq with
        member this.hasheq() = this.GetHashCode()

    interface MapEquivalence

    interface ILookup with
        member _.valAt(k) =
            raise
            <| NotImplementedException("Derived classes must implement ILookup.valAt(key)")

        member _.valAt(k, nf) =
            raise
            <| NotImplementedException("Derived classes must implement ILookup.valAt(key,notFound")

    interface Associative with
        member _.containsKey(k) =
            raise
            <| NotImplementedException("Derived classes must implement Associative.containsKey(key)")

        member _.entryAt(i) =
            raise
            <| NotImplementedException("Derived classes must implement Associative.entryAt(key)")

        member this.assoc(k, v) = (this :> IPersistentMap).assoc (k, v)

    interface Seqable with
        member _.seq() =
            raise <| NotImplementedException("Derived classes must implement Seqable.seq()")

    interface IPersistentCollection with
        member this.cons(o) = (this :> IPersistentMap).cons (o)
        member this.count() = (this :> IPersistentMap).count ()

        member _.empty() =
            raise
            <| NotImplementedException("Derived classes must implement IPersistentCollection.empty()")

        member this.equiv(o) =
            match o with
            | :? IPersistentMap when not (o :? MapEquivalence) -> false
            | :? IDictionary as d ->
                if d.Count <> (this :> IPersistentMap).count () then
                    false
                else
                    let rec loop (s: ISeq) =
                        if isNull s then
                            true
                        else
                            let me = s.first () :?> IMapEntry

                            if not <| d.Contains(me.key ()) || not <| Util.equiv (me.value (), d[me.key ()]) then
                                false
                            else
                                loop (s.next ())

                    loop ((this :> Seqable).seq ())
            | _ -> false

    interface IMeta with
        member _.meta() =
            raise
            <| NotImplementedException("Derived classes must implement IMeta.meta(meta)")

    interface IObj with
        member _.withMeta(meta) =
            raise
            <| NotImplementedException("Derived classes must implement IObj.withMeta(meta)")

    interface Counted with
        member this.count() = (this :> IPersistentMap).count ()

    interface IPersistentMap with
        member _.assoc(k, v) =
            raise
            <| NotImplementedException("Derived classes must implement IPersistentMap.assoc(key,value)")

        member _.assocEx(k, v) =
            raise
            <| NotImplementedException("Derived classes must implement IPersistentMap.assocEx(key,value)")

        member _.without(k) =
            raise
            <| NotImplementedException("Derived classes must implement IPersistentMap.without(key)")

        member _.count() =
            raise
            <| NotImplementedException("Derived classes must implement IPersistentMap.count()")

        member this.cons(o) =
            match o with
            | :? IMapEntry as me -> (this :> IPersistentMap).assoc (me.key (), me.value ())
            | :? DictionaryEntry as de -> (this :> IPersistentMap).assoc (de.Key, de.Value)
            | :? KeyValuePair<obj, obj> as kvp -> (this :> IPersistentMap).assoc (kvp.Key, kvp.Value)
            | :? IPersistentVector as v ->
                if v.count () = 2 then
                    (this :> IPersistentMap).assoc (v.nth (0), v.nth (1))
                else
                    raise <| ArgumentException("Vector arg to map cons must be a pair")
            | _ ->
                let rec loop (s: ISeq) (m: IPersistentMap) =
                    if isNull s then
                        m
                    else
                        let me = s.first () :?> IMapEntry
                        loop (s.next ()) (m.assoc (me.key (), me.value ()))

                loop (RT0.seq (o)) this

    interface IFn with
        override this.invoke(a1) = (this :> ILookup).valAt (a1)
        override this.invoke(a1, a2) = (this :> ILookup).valAt (a1, a2)

    static member val private missingValue = obj ()

    interface IDictionary<obj, obj> with
        member _.Add(_, _) =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member _.Remove(_) =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member this.Keys = KeySeq.create ((this :> Seqable).seq ())
        member this.Values = ValSeq.create ((this :> Seqable).seq ())

        member this.Item
            with get key = (this :> ILookup).valAt (key)
            and set _ _ = raise <| InvalidOperationException("Cannot modify an immutable map")

        member this.ContainsKey(k) = (this :> Associative).containsKey (k)

        member this.TryGetValue(k, v) =
            match (this :> ILookup).valAt (k, APersistentMap.missingValue) with
            | x when x = APersistentMap.missingValue ->
                v <- null
                false
            | found ->
                v <- found
                true


    interface ICollection<KeyValuePair<obj, obj>> with
        member _.Add(_) =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member _.Clear() =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member _.Remove(_) =
            raise <| InvalidOperationException("Cannot modify an immutable map")


        member _.IsReadOnly = true
        member this.Count = (this :> IPersistentMap).count ()

        member this.Contains(kv) =
            match (this :> IDictionary<obj, obj>).TryGetValue(kv.Key) with
            | false, _ -> false
            | true, null -> isNull kv.Value
            | true, v -> v.Equals(kv.Value)
        
        member this.CopyTo(arr, idx) =
            let s = (this :> Seqable).seq ()

            if not <| isNull s then
                (s :?> ICollection).CopyTo(arr, idx)


    interface IDictionary with
        member _.IsFixedSize = true
        member _.IsReadOnly = true
        member this.Contains(key) = (this:>Associative).containsKey(key)
        member this.Keys = KeySeq.create ((this :> Seqable).seq ())
        member this.Values = ValSeq.create ((this :> Seqable).seq ())
        member this.Item
            with get key = (this :> ILookup).valAt (key)
            and set _ _ = raise <| InvalidOperationException("Cannot modify an immutable map")
        member this.GetEnumerator() = new MapEnumerator(this)

        member _.Add(_, _) =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member _.Clear() =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member _.Remove(_) =
            raise <| InvalidOperationException("Cannot modify an immutable map")




    interface ICollection with
        member this.Count = (this :> IPersistentMap).count ()
        member this.IsSynchronized = true
        member this.SyncRoot = this

        member this.CopyTo(arr, idx) =
            let s = (this :> Seqable).seq ()

            if not <| isNull s then
                (s :?> ICollection).CopyTo(arr, idx)


    interface IEnumerable<KeyValuePair<obj, obj>> with
        member this.GetEnumerator() =
            let generator (s: ISeq) : (KeyValuePair<obj, obj> * ISeq) option =
                if isNull s then
                    None
                else
                    let me = s.first () :?> IMapEntry
                    let kvp = KeyValuePair<obj, obj>(me.key (), me.value ())
                    Some(kvp, s.next ())

            let s = Seq.unfold generator ((this :> Seqable).seq ())
            s.GetEnumerator()


    interface IEnumerable with
        member this.GetEnumerator() =
            let generator (s: ISeq) : (KeyValuePair<obj, obj> * ISeq) option =
                if isNull s then
                    None
                else
                    let me = s.first () :?> IMapEntry
                    let kvp = KeyValuePair<obj, obj>(me.key (), me.value ())
                    Some(kvp, s.next ())

            let s = Seq.unfold generator ((this :> Seqable).seq ())
            s.GetEnumerator()

    interface IEnumerable<IMapEntry> with
        member this.GetEnumerator() =
            let generator (s: ISeq) : (IMapEntry * ISeq) option =
                if isNull s then
                    None
                else
                    let me = s.first () :?> IMapEntry
                    Some(me, s.next ())

            let s = Seq.unfold generator ((this :> Seqable).seq ())
            s.GetEnumerator()
