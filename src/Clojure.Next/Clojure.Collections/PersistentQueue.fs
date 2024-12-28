namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics


//  Seq type for PersistentQueue

type private PQSeq(meta: IPersistentMap, front: ISeq, rear: ISeq) =
    inherit ASeq(meta)

    new(f,r) = PQSeq(null, f, r)

    interface IObj with
        member this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality meta m then
                this
            else
                PQSeq(m, front, rear)

    interface IPersistentCollection with
        override this.count() = RT0.count(front) + RT0.count(rear)

    interface ISeq with
        override this.first() = front.first()
        override this.next() =
            match front.next() with
            | null -> if isNull rear then null else PQSeq(meta, RT0.seq(rear), null)
            | f1 -> PQSeq(meta, f1, rear)


// A persistent queue. (Conses onto rear, peeks/pops from front.)
// See Okasaki's Batched Queues.
// This differs in that it uses a PersistentVector as the rear, which is in-order,
// so no reversing or suspensions required for persistent use.</para>

type PersistentQueue(meta: IPersistentMap, cnt: int, front: ISeq, rear: IPersistentVector ) =
    inherit Obj(meta)

    let mutable hashCode = 0
    let mutable hasheq = 0

    static member Empty = PersistentQueue(null,0,null,null)

    override this.Equals(obj) =
        match obj with
        | :? Sequential as s ->
            let ms = RT0.seq(obj)
            let rec loop(s: ISeq, ms: ISeq) =
                match s with
                | null -> isNull ms
                | _ when isNull ms -> false
                | _ when not(Util.equals(s.first(), ms.first())) -> false
                | _ -> loop(s.next(), ms.next())
            loop((this :> Seqable).seq(), ms)
        | _ -> false

    override this.GetHashCode() =
        let mutable cached = hashCode
        if cached = 0 then
            let rec loop (s:ISeq) (code:int) =
                match s with
                | null -> code
                | _ -> 
                    let h1 = 
                        match s.first() with
                        | null -> 0
                        | f1 -> Util.hasheq(f1)

                    loop (s.next()) (31 * code + h1)
            cached <- loop ((this :> Seqable).seq()) 1
            hashCode <- cached
        cached

    interface IObj with
        member this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality meta m then
                this
            else
                PersistentQueue(m, cnt, front, rear)

    interface IPersistentStack with
        member this.peek() = RTSeq.first(front)
        member this.pop() =
            if isNull front then
                this
            else
                let f1 = RTSeq.next(front)
                if isNull f1 then
                    PersistentQueue(meta, cnt-1, RT0.seq(rear), null)
                else
                    PersistentQueue(meta, cnt - 1, f1, rear)
    
    interface Counted with
        member this.count() = cnt

    interface IPersistentCollection with
        member this.count() = cnt
        member this.cons(o) =
                if isNull front then
                    PersistentQueue(meta, cnt + 1, RTSeq.list(o), null)
                else
                    PersistentQueue(meta, cnt + 1, front, (if isNull rear then PersistentVector.EMPTY  :> IPersistentVector else rear).cons(o))
        member this.empty() = (PersistentQueue.Empty :> IObj).withMeta(meta) :?> IPersistentCollection
        member this.equiv(o) =
            match o with
            | :? Sequential as s ->
                let ms = RT0.seq(o)
                let rec loop(s: ISeq, ms: ISeq) =
                    match s with
                    | null -> isNull ms
                    | _ when isNull ms -> false
                    | _ when not(Util.equiv(s.first(), ms.first())) -> false
                    | _ -> loop(s.next(), ms.next())
                loop((this :> Seqable).seq(), ms)
            | _ -> false
        member this.seq() = 
            if isNull front then
                null
            else
                PQSeq(front, (rear:>Seqable).seq())

    static member private seqContains(seq:ISeq, o: obj) : bool =
        let rec loop (s:ISeq) =
            match s with
            | null -> false
            | _ when Util.equals(seq.first(), o) -> true
            | _ -> loop (seq.next())
        loop seq        

    interface ICollection with
        member this.CopyTo(array: Array, arrayIndex: int) =
            let mutable i = arrayIndex
            for v in (this :> ICollection) do
                array.SetValue(v, arrayIndex)
                i <- i + 1
        member this.Count = cnt
        member this.IsSynchronized = true
        member this.SyncRoot = this

    interface ICollection<Object> with
        member this.Count = cnt
        member this.IsReadOnly = true
        member this.Add(item) = raise <| InvalidOperationException("Cannot modify immutable queue")
        member this.Clear() = raise <| InvalidOperationException("Cannot modify immutable queue")
        member this.Contains(item) =
            PersistentQueue.seqContains(front,item) || PersistentQueue.seqContains((rear :> Seqable).seq(),item)
        member this.CopyTo(array, arrayIndex) =
            let mutable i = arrayIndex
            for v in (this :> ICollection<Object>) do
                array[i] <- v
                i <- i + 1
        member this.Remove(item) = raise <| InvalidOperationException("Cannot modify immutable queue")

    interface IEnumerable with
        member this.GetEnumerator() =
            let mutable s = front
            let items = 
                seq {
                    while not(isNull s) do
                        yield s.first()
                        s <- s.next()
                    s <- (rear :> Seqable).seq()
                    while not(isNull rear) do
                        yield s.first()
                        s <- s.next()   
                    }
            items.GetEnumerator()

    interface IEnumerable<Object> with
        member this.GetEnumerator() =
            let mutable s = front
            let items = 
                seq {
                    while not(isNull s) do
                        yield s.first()
                        s <- s.next()
                    s <- (rear :> Seqable).seq()
                    while not(isNull rear) do
                        yield s.first()
                        s <- s.next()   
                    }
            items.GetEnumerator()

    interface IHashEq with
        member this.hasheq() =
            let mutable cached = hasheq
            if cached = 0 then
                cached <- Hashing.hashOrdered(this)
                hasheq <- cached
            cached


