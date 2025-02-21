namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics


///  Seq type for PersistentQueue
type private PQSeq(meta: IPersistentMap, _front: ISeq, _rear: ISeq) =
    inherit ASeq(meta)

    /// Create a PQSeq with null metadata
    new(f, r) = PQSeq(null, f, r)

    interface IObj with
        member this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality meta m then
                this
            else
                PQSeq(m, _front, _rear)

    interface IPersistentCollection with
        override this.count() = RT0.count (_front) + RT0.count (_rear)

    interface ISeq with
        override this.first() = _front.first ()

        override this.next() =
            match _front.next () with
            | null ->
                if isNull _rear then
                    null
                else
                    PQSeq(meta, RTSeq.seq (_rear), null)
            | f1 -> PQSeq(meta, f1, _rear)


// An immutable, persistent queue
type PersistentQueue(meta: IPersistentMap, _cnt: int, _front: ISeq, _rear: IPersistentVector) =
    inherit Obj(meta)


    // A persistent queue. (Conses onto rear, peeks/pops from front.)
    // See Okasaki's Batched Queues.
    // This differs in that it uses a PersistentVector as the rear, which is in-order,
    // so no reversing or suspensions required for persistent use.</para>

    /// Cached hash code
    let mutable _hashCode = 0
    // TODO: Do we still need separate hash codes for GetHashCode and hasheq?

    /// Cached hasheq code
    let mutable _hasheq = 0

    /// An empty PersistentQueue
    static member val Empty = PersistentQueue(null, 0, null, null)

    // Object overrides

    override this.Equals(obj) =
        match obj with
        | :? Sequential as s ->
            let ms = RTSeq.seq (obj)

            let rec loop (s: ISeq, ms: ISeq) =
                match s with
                | null -> isNull ms
                | _ when isNull ms -> false
                | _ when not (Util.equals (s.first (), ms.first ())) -> false
                | _ -> loop (s.next (), ms.next ())

            loop ((this :> Seqable).seq (), ms)
        | _ -> false

    override this.GetHashCode() =
        let mutable cached = _hashCode

        if cached = 0 then
            let rec loop (s: ISeq) (code: int) =
                match s with
                | null -> code
                | _ ->
                    let h1 =
                        match s.first () with
                        | null -> 0
                        | f1 -> Util.hasheq (f1)

                    loop (s.next ()) (31 * code + h1)

            cached <- loop ((this :> Seqable).seq ()) 1
            _hashCode <- cached

        cached

    interface IObj with
        member this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality meta m then
                this
            else
                PersistentQueue(m, _cnt, _front, _rear)

    interface IPersistentStack with
        member this.peek() = RTSeq.first (_front)

        member this.pop() =
            if isNull _front then
                this
            else
                let f1 = RTSeq.next (_front)

                if isNull f1 then
                    PersistentQueue(meta, _cnt - 1, RTSeq.seq (_rear), null)
                else
                    PersistentQueue(meta, _cnt - 1, f1, _rear)

    interface Counted with
        member this.count() = _cnt

    interface IPersistentCollection with
        member this.count() = _cnt

        member this.cons(o) =
            if isNull _front then
                PersistentQueue(meta, _cnt + 1, RTSeq.list (o), null)
            else
                PersistentQueue(
                    meta,
                    _cnt + 1,
                    _front,
                    (if isNull _rear then
                         PersistentVector.Empty :> IPersistentVector
                     else
                         _rear)
                        .cons (o)
                )

        member this.empty() =
            (PersistentQueue.Empty :> IObj).withMeta (meta) :?> IPersistentCollection

        member this.equiv(o) =
            match o with
            | :? Sequential as s ->
                let ms = RTSeq.seq (o)

                let rec loop (s: ISeq, ms: ISeq) =
                    match s with
                    | null -> isNull ms
                    | _ when isNull ms -> false
                    | _ when not (Util.equiv (s.first (), ms.first ())) -> false
                    | _ -> loop (s.next (), ms.next ())

                loop ((this :> Seqable).seq (), ms)
            | _ -> false

        member this.seq() =
            if isNull _front then
                null
            else
                PQSeq(_front, (_rear :> Seqable).seq ())

    static member private seqContains(seq: ISeq, o: obj) : bool =
        let rec loop (s: ISeq) =
            match s with
            | null -> false
            | _ when Util.equals (seq.first (), o) -> true
            | _ -> loop (seq.next ())

        loop seq

    interface ICollection with
        member this.CopyTo(array: Array, arrayIndex: int) =
            let mutable i = arrayIndex

            for v in (this :> ICollection) do
                array.SetValue(v, arrayIndex)
                i <- i + 1

        member this.Count = _cnt
        member this.IsSynchronized = true
        member this.SyncRoot = this

    interface ICollection<Object> with
        member this.Count = _cnt
        member this.IsReadOnly = true

        member this.Add(item) =
            raise <| InvalidOperationException("Cannot modify immutable queue")

        member this.Clear() =
            raise <| InvalidOperationException("Cannot modify immutable queue")

        member this.Contains(item) =
            PersistentQueue.seqContains (_front, item)
            || PersistentQueue.seqContains ((_rear :> Seqable).seq (), item)

        member this.CopyTo(array, arrayIndex) =
            let mutable i = arrayIndex

            for v in (this :> ICollection<Object>) do
                array[i] <- v
                i <- i + 1

        member this.Remove(item) =
            raise <| InvalidOperationException("Cannot modify immutable queue")

    interface IEnumerable with
        member this.GetEnumerator() =
            let mutable s = _front

            let items =
                seq {
                    while not (isNull s) do
                        yield s.first ()
                        s <- s.next ()

                    s <- (_rear :> Seqable).seq ()

                    while not (isNull _rear) do
                        yield s.first ()
                        s <- s.next ()
                }

            items.GetEnumerator()

    interface IEnumerable<Object> with
        member this.GetEnumerator() =
            let mutable s = _front

            let items =
                seq {
                    while not (isNull s) do
                        yield s.first ()
                        s <- s.next ()

                    s <- (_rear :> Seqable).seq ()

                    while not (isNull _rear) do
                        yield s.first ()
                        s <- s.next ()
                }

            items.GetEnumerator()

    interface IHashEq with
        member this.hasheq() =
            if _hasheq = 0 then
                _hasheq <- Hashing.hashOrdered (this)

            _hasheq
