namespace Clojure.Collections

open System
open System.Collections
open System.Runtime.CompilerServices
open Clojure.Numerics



[<Sealed; AllowNullLiteral>]
type LazySeq private (m1, fn1, s1) =
    inherit Obj(m1)
    let mutable fn: IFn = fn1
    let mutable s: ISeq = s1
    let mutable sv: obj = null

    private new(m1: IPersistentMap, s1: ISeq) = LazySeq(m1, null, s1)
    new(fn: IFn) = LazySeq(null, fn, null)

    override this.GetHashCode() =
        match (this :> ISeq).seq () with
        | null -> 1
        | s -> Hashing.hash s


    override this.Equals(o: obj) =
        match (this :> ISeq).seq (), o with
        | null, :? Sequential
        | null, :? IList -> RT0.seq (o) = null
        | null, _ -> false
        | s, _ -> s.Equals(o)

    interface IObj with
        override this.withMeta(meta: IPersistentMap) =
            if obj.ReferenceEquals((this :> IMeta).meta (), meta) then
                this :> IObj
            else
                LazySeq(meta, (this :> ISeq).seq ()) :> IObj

    member _.sval() : obj =
        if not (isNull fn) then
            sv <- fn.invoke ()
            fn <- null

        match sv with
        | null -> upcast s
        | _ -> sv


    interface Seqable with

        [<MethodImpl(MethodImplOptions.Synchronized)>]
        override this.seq() =

            this.sval () |> ignore

            if not (isNull sv) then

                let rec getNext (x: obj) =
                    match x with
                    | :? LazySeq as ls -> getNext (ls.sval ())
                    | _ -> x

                let ls = sv
                sv <- null
                s <- RT0.seq (getNext ls)

            s


    interface IPersistentCollection with
        member _.count() =
            let rec countAux (s: ISeq) (acc: int) : int =
                match s with
                | null -> acc
                | _ -> countAux (s.next ()) (acc + 1)

            countAux s 0

        member this.cons(o) = upcast (this :> ISeq).cons (o)
        member _.empty() = upcast PersistentList.Empty

        member this.equiv(o) =
            match (this :> ISeq).seq () with
            | null ->
                match o with
                | :? IList
                | :? Sequential -> RT0.seq (o) = null
                | _ -> false
            | s -> s.equiv (o)

    interface ISeq with
        member this.first() =
            (this :> ISeq).seq () |> ignore
            if isNull s then null else s.first ()

        member this.next() =
            (this :> ISeq).seq () |> ignore
            if isNull s then null else s.next ()

        member this.more() =
            (this :> ISeq).seq () |> ignore

            if isNull s then upcast PersistentList.Empty else s.more ()

        member this.cons(o: obj) : ISeq = RT2.cons (o, (this :> ISeq).seq ())

    interface IPending with
        member _.isRealized() = isNull fn

    interface IHashEq with
        member this.hasheq() = Hashing.hashOrdered (this)

    interface IEnumerable with
        member this.GetEnumerator() = upcast new SeqEnumerator(this)

    interface IList with
        member _.Add(_) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Clear() =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Insert(i, v) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Remove(v) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.RemoveAt(i) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.IsFixedSize = true
        member _.IsReadOnly = true

        member this.Item
            with get index =
                if index < 0 then
                    raise <| ArgumentOutOfRangeException("index", "Index must be non-negative")

                let rec step i (s: ISeq) =
                    if i = index then
                        s.first ()
                    elif s = null then
                        raise <| ArgumentOutOfRangeException("index", "Index past end of list")
                    else
                        step (i + 1) (s.next ())

                step 0 this // TODO: See IndexOf. Should this be called on x or x.seq() ??  Check original Java code.
            and set _ _ = raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member this.IndexOf(v) =
            let rec step i (s: ISeq) =
                if isNull s then -1
                else if Util.equiv (s.first (), v) then i
                else step (i + 1) (s.next ())

            step 0 ((this :> ISeq).seq ())

        member this.Contains(v) =
            let rec step (s: ISeq) =
                if isNull s then false
                else if Util.equiv (s.first (), v) then true
                else step (s.next ())

            step ((this :> ISeq).seq ())

    interface ICollection with
        member this.Count = (this :> IPersistentCollection).count ()
        member _.IsSynchronized = true
        member this.SyncRoot = upcast this

        member this.CopyTo(arr: Array, idx) =
            if isNull arr then
                raise <| ArgumentNullException("array")

            if idx < 0 then
                raise <| ArgumentOutOfRangeException("arrayIndex", "must be non-negative")

            if arr.Rank <> 1 then
                raise <| ArgumentException("Array must be 1-dimensional")

            if idx >= arr.Length then
                raise <| ArgumentException("index", "must be less than the length")

            if (this :> IPersistentCollection).count () > arr.Length - idx then
                raise
                <| InvalidOperationException("Not enough available space from index to end of the array.")

            let rec step (i: int) (s: ISeq) =
                if not (isNull s) then
                    arr.SetValue(s.first (), i)
                    step (i + 1) (s.next ())

            step idx (this :> ISeq)
