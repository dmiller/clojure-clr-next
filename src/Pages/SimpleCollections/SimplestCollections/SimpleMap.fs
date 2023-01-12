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
type SimpleMap(keys: obj list, vals: obj list) =

    do
        if keys.Length <> vals.Length then
            raise (ArgumentException("keys and vals lists must have the same length"))


    new() = SimpleMap(List.Empty, List.Empty)

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
            (this :> IPersistentMap).cons (o) :> IPersistentCollection

        member _.empty() = SimpleMap() :> IPersistentCollection
        member this.equiv(o) = SimpleMap.mapCompare (this, o)

    interface Seqable with
        member _.seq() = upcast SimpleMapSeq(keys, vals)

    interface ILookup with
        member _.valAt(key) =
            match List.tryFindIndex (fun k -> k = key) keys with
            | Some idx -> vals.Item(idx)
            | None -> null

        member _.valAt(key, notFound) =
            match List.tryFindIndex (fun k -> k = key) keys with
            | Some idx -> vals.Item(idx)
            | None -> notFound

    interface Associative with
        member _.containsKey(key) = List.contains key keys

        member this.entryAt(key) =
            if (this :> Associative).containsKey key then
                { Key = key; Value = (this :> ILookup).valAt (key) } :> IMapEntry
            else
                null

        member this.assoc(k, v) =
            upcast (this :> IPersistentMap).assoc (k, v)

    interface Counted with
        member _.count() = keys.Length

    interface IEnumerable<IMapEntry> with
        member _.GetEnumerator() : IEnumerator<IMapEntry> =
            (seq {
                for i = 0 to keys.Length - 1 do
                    yield { Key = keys.Item(i); Value = vals.Item(i)} :> IMapEntry
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
                SimpleMap(k :: keys, v :: vals) :> IPersistentMap

        member this.assocEx(k, v) =
            if (this :> Associative).containsKey (k) then
                raise <| InvalidOperationException("Key already present.")
            else
                (this :> IPersistentMap).assoc (k, v)

        member this.without(key) =
            match List.tryFindIndex (fun k -> k = key) keys with
            | Some idx ->
                let keysHead, keysTail = List.splitAt idx keys
                let valsHead, valsTail = List.splitAt idx vals
                SimpleMap(keysHead @ keysTail.Tail, valsHead @ valsTail.Tail) :> IPersistentMap
            | None -> this :> IPersistentMap

        member this.cons(o) =
            match o with
            | :? IMapEntry as me -> (this :> IPersistentMap).assoc (me.key (), me.value ())
            | _ -> raise <| InvalidOperationException("Can only cons an IMapEntry to this map")

        member _.count() = keys.Length


and SimpleMapSeq(keys: obj list, vals: obj list) =

    interface Seqable with
        member this.seq() = upcast this

    interface IPersistentCollection with
        member _.count() = List.length keys
        member this.cons(o) = upcast SimpleCons(o, this)
        member _.empty() = upcast SimpleEmptySeq()

        member this.equiv(o) =
            match o with
            | :? Seqable as s -> Util.seqEquiv this (s.seq ())
            | _ -> false

    interface ISeq with
        member _.first() =
            upcast { Key = keys.Head; Value = vals.Head }

        member _.next() =
            if keys.Length <= 1 then
                null
            else
                upcast SimpleMapSeq(keys.Tail, vals.Tail)

        member this.more() =
            match (this :> ISeq).next () with
            | null -> upcast SimpleEmptySeq()
            | s -> s

        member this.cons(o) = upcast SimpleCons(o, this)
