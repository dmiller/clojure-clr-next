namespace Clojure.Collections

open System
open System.Collections
open Clojure.Numerics
open System.Linq


[<AbstractClass; AllowNullLiteral>]
type ASeq(m) =
    inherit Obj(m)

    [<NonSerialized>]
    let mutable hasheq: int option = None

    new() = ASeq(null)

    static member doCount(s: ISeq) =
        let rec step (s: ISeq) cnt =
            match s with
            | null -> cnt
            | :? Counted as c -> cnt + c.count ()
            | _ -> step (s.next ()) (cnt + 1)

        step s 0

    override this.ToString() = RTPrint.printString (this)

    override this.Equals(o) =
        if obj.ReferenceEquals(this, o) then
            true
        else
            match o with
            | :? Sequential
            | :? IList ->
                let rec step (s1: ISeq) (s2: ISeq) =
                    match s1, s2 with
                    | null, null -> true
                    | _, null -> false
                    | null, _ -> false
                    | _ -> Util.equals (s1.first (), s2.first ()) && step (s1.next ()) (s2.next ())

                step this (RT0.seq (o))
            | _ -> false

    override this.GetHashCode() = (this :> IHashEq).hasheq ()

    interface IHashEq with
        member this.hasheq() =
            match hasheq with
            | None ->
                let h = Hashing.hashOrdered (this)
                hasheq <- Some h
                h
            | Some h -> h

    interface ISeq with
        member _.first() =
            raise
            <| NotImplementedException("Subclasses of ASeq must implement ISeq.first()")

        member _.next() =
            raise
            <| NotImplementedException("Subclasses of ASeq must implement ISeq.next()")

        member this.more() =
            let s = (this :> ISeq).next ()

            if s = null then EmptyList.Empty :> ISeq else s

        member this.cons(o) = Cons(o, this) :> ISeq


    static member  countsMismatch(o1:obj, o2:obj) = 
        (o1 :? Counted) && (o2 :? Counted) && (o1:?>Counted).count() <> (o2:?>Counted).count()

 
    interface IPersistentCollection with
        member this.cons(o) =
            (this :> ISeq).cons (o) :> IPersistentCollection

        member this.count() =
            1 + ASeq.doCount ((this :> ISeq).next ())

        member _.empty() =
            EmptyList.Empty :> IPersistentCollection

        member this.equiv(o) =
            match o with
            | :? Sequential
            | :? IList ->
                let rec step (s1: ISeq) (s2: ISeq) =
                    match s1, s2 with
                    | null, null -> true
                    | _, null -> false
                    | null, _ -> false
                    | _ -> Util.equiv (s1.first (), s2.first ()) && step (s1.next ()) (s2.next ())
                
                if ASeq.countsMismatch(this,o) then
                    false
                else 
                    step this (RT0.seq (o))
            | _ -> false

    interface Seqable with
        member this.seq() = this :> ISeq

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
            //Java has this: return RT.nth(this, index);
            // THis causes an infinite loop in my code.    TODO:  SEE IF THIS IS STILL TRUE, OR FIND A WORKAROUND?
            // When this was introduces, a change was made in RT.nth that changed the List test in its type dispatch to RandomAccess.
            // CLR does not have the equivalent notion, so I just left it at IList.  BOOM!
            // So, I have to do a sequential search, duplicating some of the code in RT.nth.
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

    interface IEnumerable with
        member x.GetEnumerator() = new SeqEnumerator(x) :> IEnumerator

    interface ICollection with
        member x.Count = (x :> IPersistentCollection).count ()
        member x.IsSynchronized = true
        member x.SyncRoot = upcast x

        member x.CopyTo(arr: Array, idx) =
            if isNull arr then
                raise <| ArgumentNullException("array")

            if arr.Rank <> 1 then
                raise <| ArgumentException("Array must be 1-dimensional")

            if idx < 0 then
                raise <| ArgumentOutOfRangeException("arrayIndex", "must be non-negative")

            if arr.Length - idx < (x :> IPersistentCollection).count () then
                raise
                <| InvalidOperationException(
                    "The number of elements in source is greater than the available space in the array."
                )

            let rec step (i: int) (s: ISeq) =
                if i < arr.Length && s <> null then
                    arr.SetValue(s.first (), i)
                    step (i + 1) (s.next ())

            step idx (x :> ISeq)

and [<Sealed>] Cons(meta, f: obj, m: ISeq) =
    inherit ASeq(meta)

    let first: obj = f
    let more: ISeq = m

    new(f: obj, m: ISeq) = Cons(null, f, m)

    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(m, meta) then
                (this :> IObj)
            else
                Cons(m, first, more) :> IObj

    interface ISeq with
        override _.first() = first
        override this.next() = (this :> ISeq).more().seq ()

        override _.more() =
            match more with
            | null -> upcast EmptyList.Empty
            | _ -> more

    interface IPersistentCollection with
        override _.count() = 1 + RT0.count (more)

and [<Sealed>] EmptyList(m) =
    inherit Obj(m)

    new() = EmptyList(null)

    static member hasheq = Hashing.hashOrdered (Enumerable.Empty<Object>())

    static member Empty: EmptyList = EmptyList()

    override _.GetHashCode() = 1

    override _.Equals(o) =
        match o with
        | :? Sequential
        | :? IList -> RT0.seq (o) |> isNull
        | _ -> false

    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(m, (this :> IMeta).meta ()) then
                this :> IObj
            else
                EmptyList(m) :> IObj

    interface ISeq with
        override _.first() = null
        override _.next() = null
        override this.more() = this :> ISeq

        override this.cons(o) =
            PersistentList((this :> IMeta).meta (), o, null, 1) :> ISeq

    interface IPersistentCollection with
        override _.count() = 0

        override this.cons(o) =
            (this :> ISeq).cons (o) :> IPersistentCollection

        override this.empty() = this :> IPersistentCollection
        override this.equiv(o) = this.Equals(o)

    interface Seqable with
        override _.seq() = null

    interface IPersistentStack with
        override _.peek() = null

        override _.pop() =
            raise <| InvalidOperationException("Attempt to pop an empty list")

    interface Sequential

    interface IPersistentList

    interface IHashEq with
        override _.hasheq() = EmptyList.hasheq

    interface ICollection with
        override _.CopyTo(a: Array, idx: int) = () // no-op
        override _.Count = 0
        override _.IsSynchronized = true
        override this.SyncRoot = upcast this

    static member emptyEnumerator: IEnumerator = Seq.empty<obj>.GetEnumerator () :> IEnumerator

    interface IEnumerable with
        override x.GetEnumerator() = EmptyList.emptyEnumerator

    interface IList with
        override _.Add(_) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        override _.Clear() =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        override _.Insert(i, v) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        override _.Remove(v) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        override _.RemoveAt(i) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        override _.IsFixedSize = true
        override _.IsReadOnly = true

        override _.Item
            with get index = raise <| ArgumentOutOfRangeException("index")
            and set _ _ = raise <| InvalidOperationException("Cannot modify an immutable sequence")

        override _.IndexOf(v) = -1
        override _.Contains(v) = false

and [<AllowNullLiteral>] PersistentList(m1, f1, r1, c1) =
    inherit ASeq(m1)
    let first: obj = f1
    let rest: IPersistentList = r1
    let count = c1
    new(first: obj) = PersistentList(null, first, null, 1)

    // for backwards compatability
    static member Empty = EmptyList.Empty

    static member create(init: IList) =
        let mutable r = EmptyList.Empty :> IPersistentList

        for i = init.Count - 1 downto 0 do
            r <- downcast r.cons (init.[i])

        r

    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(m, (this :> IMeta).meta ()) then
                this :> IObj
            else
                PersistentList(m, first, rest, count) :> IObj

    interface ISeq with
        override _.first() = first
        override _.next() = if count = 1 then null else rest.seq ()

        override this.cons(o) =
            PersistentList((this :> IObj).meta (), o, (this :> IPersistentList), count + 1) :> ISeq

    interface IPersistentCollection with
        override _.count() = count

        override this.empty() =
            (EmptyList.Empty :> IObj).withMeta ((this :> IMeta).meta ()) :?> IPersistentCollection

    interface IPersistentStack with
        override _.peek() = first

        override this.pop() =
            match rest with
            | null -> (EmptyList.Empty :> IObj).withMeta ((this :> IMeta).meta ()) :?> IPersistentStack
            | _ -> rest :> IPersistentStack

    interface IPersistentList

    interface IReduceInit with
        member this.reduce(fn, start) =
            let rec step (s: ISeq) (value: obj) =
                match s with
                | null -> value
                | _ ->
                    match value with
                    | :? Reduced as r -> (r :> IDeref).deref ()
                    | _ -> step (s.next ()) (fn.invoke (value, s.first ()))

            let init = fn.invoke (start, (this :> ISeq).first ())

            let ret = step ((this :> ISeq).next ()) init

            match ret with
            | :? Reduced as r -> (r :> IDeref).deref ()
            | _ -> ret

    interface IReduce with
        member this.reduce(fn) =
            let rec step (s: ISeq) (value: obj) =
                match s with
                | null -> value
                | _ ->
                    let nextVal = (fn.invoke (value, s.first ()))

                    match nextVal with
                    | :? Reduced as r -> (r :> IDeref).deref ()
                    | _ -> step (s.next ()) nextVal

            step ((this :> ISeq).next ()) ((this :> ISeq).first ())

// I'm not sure this is the final resting place for this
module RT2 =

    let cons (x: obj, coll: obj) : ISeq =
        match coll with
        | null -> upcast PersistentList(x)
        | :? ISeq as s -> upcast Cons(x, s)
        | _ -> upcast Cons(x, RT0.seq (coll))
