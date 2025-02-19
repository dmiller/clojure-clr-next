namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics
open System.Linq

/// Abstract base class for Clojure sequences.
[<AbstractClass; AllowNullLiteral>]
type ASeq(m) =
    inherit Obj(m)

    /// Cached hash code for the sequence.
    /// Lazily computed.
    [<NonSerialized>]
    let mutable _hasheq: int option = None

    new() = ASeq(null)

    /// Helper function to count the number of elements in a sequence.
    static member private doCount(s: ISeq) =
        let rec loop (s: ISeq) cnt =
            match s with
            | null -> cnt
            | :? Counted as c -> cnt + c.count ()
            | _ -> loop (s.next ()) (cnt + 1)

        loop s 0


    // Object overrides

    override this.ToString() = RTPrint.printString (this)

    override this.Equals(o) =
        if LanguagePrimitives.PhysicalEquality (this :> obj) o then
            true
        else
            match o with
            | :? Sequential
            | :? IList ->
                let rec loop (s1: ISeq) (s2: ISeq) =
                    match s1, s2 with
                    | null, null -> true
                    | _, null -> false
                    | null, _ -> false
                    | _ -> Util.equals (s1.first (), s2.first ()) && loop (s1.next ()) (s2.next ())

                loop this (RT0.seq (o))
            | _ -> false

    override this.GetHashCode() = (this :> IHashEq).hasheq ()

    interface IHashEq with
        member this.hasheq() =
            match _hasheq with
            | None ->
                let h = Hashing.hashOrdered (this)
                _hasheq <- Some h
                h
            | Some h -> h

    interface Sequential

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


    static member private countsMismatch(o1: obj, o2: obj) =
        match o1, o2 with
        | (:? Counted as c1), (:? Counted as c2) -> c1.count () <> c2.count ()
        | _ -> false

    /// Version of IPersistentCollection.count() accessible to derived classes.
    member this.DoCount() =
        (this :> IPersistentCollection).count ()

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
                let rec loop (s1: ISeq) (s2: ISeq) =
                    match s1, s2 with
                    | null, null -> true
                    | _, null -> false
                    | null, _ -> false
                    | _ -> Util.equiv (s1.first (), s2.first ()) && loop (s1.next ()) (s2.next ())

                if ASeq.countsMismatch (this, o) then
                    false
                else
                    loop this (RT0.seq (o))
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

                let rec loop i (s: ISeq) =
                    if i = index then
                        s.first ()
                    elif s = null then
                        raise <| ArgumentOutOfRangeException("index", "Index past end of list")
                    else
                        loop (i + 1) (s.next ())

                loop 0 this // TODO: See IndexOf. Should this be called on x or x.seq() ??  Check original Java code.
            and set _ _ = raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member this.IndexOf(v) =
            let rec loop i (s: ISeq) =
                if isNull s then -1
                else if Util.equiv (s.first (), v) then i
                else loop (i + 1) (s.next ())

            loop 0 ((this :> ISeq).seq ())

        member this.Contains(v) =
            let rec loop (s: ISeq) =
                if isNull s then false
                else if Util.equiv (s.first (), v) then true
                else loop (s.next ())

            loop ((this :> ISeq).seq ())

    // I don't know a workaround for getting the enumerator from a base class call in a derived class

    member this.GetMyEnumeratorT() =
        (this: IEnumerable<obj>).GetEnumerator()

    interface IEnumerable<obj> with
        member this.GetEnumerator() =
            new SeqEnumerator(this) :> IEnumerator<obj>

    interface IEnumerable with
        member this.GetEnumerator() = new SeqEnumerator(this) :> IEnumerator

    interface ICollection<obj> with
        member this.Count = (this :> IPersistentCollection).count ()
        member _.IsReadOnly = true

        member _.Add(_) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Clear() =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Remove(_) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member this.Contains(x) = (this :> IList).Contains(x)
        member this.CopyTo(arr, idx) = (this :> ICollection).CopyTo(arr, idx)

    interface ICollection with
        member this.Count = (this :> IPersistentCollection).count ()
        member _.IsSynchronized = true
        member this.SyncRoot = upcast this

        member this.CopyTo(arr: Array, idx) =
            if isNull arr then
                raise <| ArgumentNullException("array")

            if arr.Rank <> 1 then
                raise <| ArgumentException("Array must be 1-dimensional")

            if idx < 0 then
                raise <| ArgumentOutOfRangeException("arrayIndex", "must be non-negative")

            if arr.Length - idx < (this :> IPersistentCollection).count () then
                raise
                <| InvalidOperationException(
                    "The number of elements in source is greater than the available space in the array."
                )

            let rec loop (i: int) (s: ISeq) =
                if i < arr.Length && not (isNull s) then
                    arr.SetValue(s.first (), i)
                    loop (i + 1) (s.next ())

            loop idx (this :> ISeq)

/// Implementation of a cons cell.
/// Classic Lisp with a Clojure flavor.
and [<Sealed>] Cons(meta, _first: obj, _more: ISeq) =
    inherit ASeq(meta)

    /// Construct a Cons with null metadata
    new(f: obj, m: ISeq) = Cons(null, f, m)

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m meta then
                (this :> IObj)
            else
                Cons(m, _first, _more) :> IObj

    interface ISeq with
        override _.first() = _first
        override this.next() = (this :> ISeq).more().seq ()

        override _.more() =
            match _more with
            | null -> upcast EmptyList.Empty
            | _ -> _more

    interface IPersistentCollection with
        override _.count() = 1 + RT0.count (_more)

/// The empty list.
and [<Sealed>] EmptyList(m) =
    inherit Obj(m)

    new() = EmptyList(null)

    static member val _hasheq = Hashing.hashOrdered (Enumerable.Empty<Object>())

    static member val Empty: EmptyList = EmptyList()

    override _.GetHashCode() = 1

    override _.Equals(o) =
        match o with
        | :? Sequential
        | :? IList -> RT0.seq (o) |> isNull
        | _ -> false

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m ((this :> IMeta).meta ()) then
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
        override _.hasheq() = EmptyList._hasheq

    interface ICollection with
        override _.CopyTo(a: Array, idx: int) = () // no-op
        override _.Count = 0
        override _.IsSynchronized = true
        override this.SyncRoot = upcast this

    static member val emptyEnumerator: IEnumerator = Seq.empty<obj>.GetEnumerator () :> IEnumerator

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

/// Implementation of a persistent list.
and [<AllowNullLiteral>] PersistentList private (meta: IPersistentMap, _first: obj, _rest: IPersistentList, _count: int) =
    inherit ASeq(meta)

    /// Construct a PersistentList of one item with null metadata
    new(first: obj) = PersistentList(null, first, null, 1)

    // for backwards compatability
    static member val Empty = EmptyList.Empty

    /// Construct a PersistentList from an IList
    static member create(init: IList) =
        let mutable r = EmptyList.Empty :> IPersistentList

        for i = init.Count - 1 downto 0 do
            r <- downcast r.cons (init.[i])

        r

    /// Construct a PersistentList from a List<obj>
    static member create(init: ResizeArray<obj>) =
        let mutable r = EmptyList.Empty :> IPersistentList

        for i = init.Count - 1 downto 0 do
            r <- downcast r.cons (init.[i])

        r

    /// Construct a PersistentList from a List<obj>
    static member create(init: obj list) =
        let mutable r = EmptyList.Empty :> IPersistentList

        for i = init.Length - 1 downto 0 do
            r <- downcast r.cons (init.[i])

        r
    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m ((this :> IMeta).meta ()) then
                this :> IObj
            else
                PersistentList(m, _first, _rest, _count) :> IObj

    interface ISeq with
        override _.first() = _first

        override _.next() =
            if _count = 1 then null else _rest.seq ()

        override this.cons(o) =
            PersistentList((this :> IObj).meta (), o, (this :> IPersistentList), _count + 1) :> ISeq

    interface IPersistentCollection with
        override _.count() = _count

        override this.empty() =
            (EmptyList.Empty :> IObj).withMeta ((this :> IMeta).meta ()) :?> IPersistentCollection

    interface IPersistentStack with
        override _.peek() = _first

        override this.pop() =
            match _rest with
            | null -> (EmptyList.Empty :> IObj).withMeta ((this :> IMeta).meta ()) :?> IPersistentStack
            | _ -> _rest :> IPersistentStack

    interface IPersistentList

    interface IReduceInit with
        member this.reduce(fn, start) =
            let rec loop (s: ISeq) (value: obj) =
                match s with
                | null -> value
                | _ ->
                    match value with
                    | :? Reduced as r -> (r :> IDeref).deref ()
                    | _ -> loop (s.next ()) (fn.invoke (value, s.first ()))

            let init = fn.invoke (start, (this :> ISeq).first ())

            let ret = loop ((this :> ISeq).next ()) init

            match ret with
            | :? Reduced as r -> (r :> IDeref).deref ()
            | _ -> ret

    interface IReduce with
        member this.reduce(fn) =
            let rec loop (s: ISeq) (value: obj) =
                match s with
                | null -> value
                | _ ->
                    let nextVal = (fn.invoke (value, s.first ()))

                    match nextVal with
                    | :? Reduced as r -> (r :> IDeref).deref ()
                    | _ -> loop (s.next ()) nextVal

            loop ((this :> ISeq).next ()) ((this :> ISeq).first ())
