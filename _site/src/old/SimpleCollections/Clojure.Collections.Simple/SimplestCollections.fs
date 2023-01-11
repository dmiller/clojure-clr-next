namespace Clojure.Collections.Simple

open Clojure.Collections
open System.Collections
open System.Collections.Generic
open System

// I went to this degree of elaboration so I could use this as a general example of implementation of a sequence for tutorial purposes

module Util =

    let checkEquals o1 o2 =
        obj.ReferenceEquals(o1, o2)
        || not (isNull o1) && o1.Equals(o2)

    let rec seqEquals (s1: ISeq) (s2: ISeq) =
        match s1, s2 with
        | null, null -> true
        | null, _ -> false
        | _, null -> false
        | _ ->
            checkEquals (s1.first ()) (s2.first ())
            && seqEquals (s1.next ()) (s2.next ())

    let seqEquiv s1 s2 = seqEquals s1 s2

    let seqCount (s: ISeq) =
        let rec step (s: ISeq) cnt =
            if isNull s then cnt else step (s.next ()) (cnt + 1)

        step s 0

    let getHashCode (s: ISeq) =
        let combine hc x =
            31 * hc + if isNull x then 0 else x.GetHashCode()

        let rec step (s: ISeq) hc =
            if isNull s
            then hc
            else step (s.next ()) (combine hc (s.first ()))

        step s 1

    let rec seqToString (s: ISeq) =
        let itemToString (o: obj) =
            match o with
            | :? Seqable as s -> seqToString (s.seq ())
            | _ -> o.ToString()

        let rec itemsToString (s: ISeq) =
            if isNull s then
                ""
            else
                (itemToString (s.first ()))
                + (itemsToString (s.next ()))

        if isNull s then "nil" else "(" + (itemsToString s) + ")"

type SimpleCons(head: obj, tail: ISeq) =
    // I had to restrain myself from calling these car & cdr

    interface ISeq with
        member _.first() = head
        member this.next() = (this :> ISeq).more().seq()

        member _.more() =
            if isNull tail then upcast SimpleEmptySeq() else tail

        member this.cons(o) = upcast SimpleCons(o, this)

    interface IPersistentCollection with
        member _.count() = 1 + Util.seqCount tail
        member this.cons(o) = upcast (this :> ISeq).cons(o)
        member _.empty() = upcast SimpleEmptySeq()

        member this.equiv(o) =
            match o with
            | :? Seqable as s -> Util.seqEquiv (this :> ISeq) (s.seq ())
            | _ -> false

    interface Seqable with
        member this.seq() = upcast this

    override this.Equals(o) =
        match o with
        | :? Seqable as s -> Util.seqEquals (this :> ISeq) (s.seq ())
        | _ -> false

    override this.GetHashCode() = Util.getHashCode this

    override this.ToString() = Util.seqToString this

    static member makeConsSeq(n: int) =
        let mutable (c: ISeq) = upcast SimpleEmptySeq()
        for i = n - 1 downto 0 do
            c <- c.cons (i)

        c



and SimpleEmptySeq() =

    interface ISeq with
        member _.first() = null
        member _.next() = null
        member this.more() = upcast this
        member this.cons(o) = upcast SimpleCons(o, this)

    interface IPersistentCollection with
        member _.count() = 0
        member this.cons(o) = upcast (this :> ISeq).cons(o)
        member this.empty() = upcast this
        member this.equiv(o) = this.Equals(o)

    interface Seqable with
        member x.seq() = null

    override x.Equals(o) =
        match o with
        | :? Seqable as s -> s.seq () |> isNull
        | _ -> false

    override x.GetHashCode() = 1

    override x.ToString() = "()"

// Make a super-simple Range sequence to implement ISeq
[<AllowNullLiteral>]
type SimpleRange(startVal: int, endVal: int) =

    interface ISeq with
        member _.first() = upcast startVal
        member this.next() = (this :> ISeq).more().seq()

        member this.more() =
            if startVal = endVal then upcast SimpleEmptySeq() else upcast SimpleRange(startVal + 1, endVal)

        member this.cons(o) = SimpleCons(o, (this :> ISeq)) :> ISeq

    interface IPersistentCollection with
        member _.count() = endVal - startVal + 1
        member this.cons(o) = upcast (this :> ISeq).cons(o)
        member _.empty() = upcast SimpleEmptySeq()

        member this.equiv(o) =
            match o with
            | :? Seqable as s -> Util.seqEquiv (this :> ISeq) (s.seq ())
            | _ -> false

    interface Seqable with
        member this.seq() = (this :> ISeq)

    override this.Equals(o) =
        match o with
        | :? Seqable as s -> Util.seqEquals (this :> ISeq) (s.seq ())
        | _ -> false

    override this.GetHashCode() = Util.getHashCode this

    override this.ToString() = Util.seqToString this

[<AllowNullLiteral>]
type SimpleMapEntry(key: obj, value: obj) =

    interface IMapEntry with
        member _.key() = key
        member _.value() = value


[<AllowNullLiteral>]
type SimpleMap(keys: obj list, vals: obj list) =

    new() = SimpleMap(List.Empty, List.Empty)

    static member mapCompare(m1: IPersistentMap, o: obj): bool =
        if obj.ReferenceEquals(m1, o) then
            true
        else
            match o with
            | :? IPersistentMap as m2 ->
                if m1.count () <> m2.count () then
                    false
                else
                    let rec step (s: ISeq) =
                        if isNull s then
                            true
                        else
                            let me: IMapEntry = downcast s.first ()

                            if m2.containsKey (me.key ())
                               && m2.valAt(me.key ()).Equals(me.value ()) then
                                step (s.next ())
                            else
                                false

                    step (m1.seq ())
            | _ -> false


    interface IPersistentCollection with
        member this.count() = (this :> IPersistentMap).count()

        member this.cons(o) =
            (this :> IPersistentMap).cons(o) :> IPersistentCollection

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
            if (this :> Associative).containsKey key
            then SimpleMapEntry(key, (this :> ILookup).valAt(key)) :> IMapEntry
            else null

        member this.assoc(k, v) =
            upcast (this :> IPersistentMap).assoc(k, v)

    interface Counted with
        member _.count() = keys.Length

    interface IEnumerable<IMapEntry> with
        member _.GetEnumerator(): IEnumerator<IMapEntry> =
            (seq {
                for i = 0 to keys.Length - 1 do
                    yield SimpleMapEntry(keys.Item(i), vals.Item(i)) :> IMapEntry
             })
                .GetEnumerator()

    interface IEnumerable with
        member this.GetEnumerator(): IEnumerator =
            upcast (this :> IEnumerable<IMapEntry>).GetEnumerator()

    interface IPersistentMap with
        member this.assoc(k, v) =
            if (this :> Associative).containsKey k
            then (this :> IPersistentMap).without(k).assoc(k, v) // not the most efficient way, but who cares?
            else SimpleMap(k :: keys, v :: vals) :> IPersistentMap

        member this.assocEx(k, v) =
            if (this :> Associative).containsKey(k) then
                raise
                <| InvalidOperationException("Key already present.")
            else
                (this :> IPersistentMap).assoc(k, v)

        member this.without(key) =
            match List.tryFindIndex (fun k -> k = key) keys with
            | Some idx ->
                let keysHead, keysTail = List.splitAt idx keys
                let valsHead, valsTail = List.splitAt idx vals
                SimpleMap(keysHead @ keysTail.Tail, valsHead @ valsTail.Tail) :> IPersistentMap
            | None -> this :> IPersistentMap

        member this.cons(o) =
            match o with
            | :? IMapEntry as me ->
                (this :> IPersistentMap)
                    .assoc(me.key (), me.value ())
            | _ ->
                raise
                <| InvalidOperationException("Can only cons an IMapEntry to this map")

        member _.count() = keys.Length

    static member makeSimpleMap(n: int) =
        let keys =
            seq { for c in 'a' .. 'z' -> box c }
            |> Seq.take n
            |> Seq.toList

        let vals =
            seq { for c in 'A' .. 'Z' -> box c }
            |> Seq.take n
            |> Seq.toList

        SimpleMap(keys, vals)

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
            upcast SimpleMapEntry(keys.Head, vals.Head)

        member _.next() =
            if keys.Length <= 1
            then null
            else upcast SimpleMapSeq(keys.Tail, vals.Tail)

        member this.more() =
            match (this :> ISeq).next() with
            | null -> upcast SimpleEmptySeq()
            | s -> s

        member this.cons(o) = upcast SimpleCons(o, this)

    interface Sequential
