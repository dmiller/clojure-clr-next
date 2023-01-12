namespace Clojure.Collections.Simple

open System
open System.Collections
open System.Collections.Generic
open Clojure.Collections


type SimpleMapEntry =
    {Key: obj 
     Value: obj} 
    
    interface IMapEntry with
        member this.key() = this.Key
        member this.value() = this.Value


[<AllowNullLiteral>]
type SimpleMap(kvs : IMapEntry list) =

    static member EmptyMap = SimpleMap(List.Empty)

    static member mapCompare(m1: IPersistentMap, o: obj) : bool =
        if obj.ReferenceEquals(m1, o) then
            true
        else
            match o with
            | :? IPersistentMap as m2 ->
                if m1.count () <> m2.count () then
                    false
                else
                    let rec step (s: ISeq) =
                        match s with
                        | null -> true
                        | _ ->
                            let me: IMapEntry = downcast s.first ()

                            if m2.containsKey (me.key ()) && m2.valAt(me.key ()).Equals(me.value ()) then
                                step (s.next ())
                            else
                                false

                    step (m1.seq ())
            | _ -> false


    interface IPersistentCollection with
        member this.count() = (this :> IPersistentMap).count ()

        member this.cons(o) =
            (this :> IPersistentMap).cons (o)

        member _.empty() = SimpleMap.EmptyMap
        member this.equiv(o) = SimpleMap.mapCompare (this, o)

    interface Seqable with
        member _.seq() = upcast SimpleMapSeq(kvs)

    static member isEntryForKey key (me : IMapEntry) = me.key() = key

    interface ILookup with
        member _.valAt(key) =
            match List.tryFind (SimpleMap.isEntryForKey key) kvs with
            | Some me -> me.value()
            | None -> null

        member _.valAt(key, notFound) =
            match List.tryFind (SimpleMap.isEntryForKey key) kvs with
            | Some me -> me.value()
            | None -> notFound

    interface Associative with
        member this.containsKey(key) = 
            (List.tryFind (SimpleMap.isEntryForKey key) kvs).IsSome 

        member this.entryAt(key) =
            match List.tryFind (SimpleMap.isEntryForKey key) kvs with
            | Some me -> me
            | None -> null

        member this.assoc(k, v) =
            upcast (this :> IPersistentMap).assoc (k, v)

    interface Counted with
        member _.count() = kvs.Length

    interface IEnumerable<IMapEntry> with
        member _.GetEnumerator() : IEnumerator<IMapEntry> =
            (seq {
                for i = 0 to kvs.Length - 1 do
                    yield kvs.Item(i)
            })
                .GetEnumerator()

    interface IEnumerable with
        member this.GetEnumerator() : IEnumerator =
            upcast (this :> IEnumerable<IMapEntry>).GetEnumerator()

    interface IPersistentMap with
        member this.assoc(k, v) =
            if (this :> Associative).containsKey k then
                (this :> IPersistentMap).without(k).assoc (k, v) // not the most efficient way, but who cares?
            else
                SimpleMap({Key = k; Value = v} :: kvs) :> IPersistentMap

        member this.assocEx(k, v) =
            if (this :> Associative).containsKey (k) then
                raise <| InvalidOperationException("Key already present.")
            else
                (this :> IPersistentMap).assoc (k, v)

        member this.without(key) =
            match List.tryFindIndex (SimpleMap.isEntryForKey key) kvs with
            | Some idx ->
                let kvsHead, kvsTail = List.splitAt idx kvs
                SimpleMap(kvsHead @ kvsTail.Tail)
            | None -> this

        member this.cons(o) =
            match o with
            | :? IMapEntry as me -> (this :> IPersistentMap).assoc (me.key (), me.value ())
            | _ -> raise <| InvalidOperationException("Can only cons an IMapEntry to this map")

        member _.count() = kvs.Length


and SimpleMapSeq(kvs : IMapEntry list) =

    interface Seqable with
        member this.seq() = upcast this

    interface IPersistentCollection with
        member _.count() = List.length kvs
        member this.cons(o) = upcast SimpleCons(o, this)
        member _.empty() = upcast SimpleEmptySeq()

        member this.equiv(o) =
            match o with
            | :? Seqable as s -> Util.seqEquiv this (s.seq ())
            | _ -> false

    interface ISeq with
        member _.first() = kvs.Head

        member _.next() =
            if kvs.Length <= 1 then
                null
            else
                upcast SimpleMapSeq(kvs.Tail)

        member this.more() =
            match (this :> ISeq).next () with
            | null -> upcast SimpleEmptySeq()
            | s -> s

        member this.cons(o) = upcast SimpleCons(o, this)
