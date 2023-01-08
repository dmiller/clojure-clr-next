namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Linq


type TypedSeqEnumerator<'T when 'T: not struct>(s: ISeq) =
    let mutable orig = s
    let mutable next = s
    let mutable isRealized = false
    let mutable curr: 'T option = None

    interface IEnumerator<'T> with
        member _.Current =
            if next = null then
                raise
                <| InvalidOperationException("No current value.")

            match curr with
            | None ->
                let v = RT.first (next) :?> 'T
                curr <- Some v
                v
            | Some v -> v


    interface IEnumerator with
        member _.Reset() =
            isRealized <- false // TODO - first this -- already realized!  (Note from original code)
            curr <- None
            next <- orig

        member _.MoveNext() =
            if next = null then
                false
            else
                curr <- None

                if not isRealized then
                    isRealized <- true
                    next <- RT.seq (next)
                else
                    next <- RT.next (next)

                next <> null

        member this.Current = (this :> IEnumerator<'T>).Current :> obj

    member _.Dispose disposing =
        if disposing then
            orig <- null
            curr <- None
            next <- null

    interface IDisposable with
        member this.Dispose() =
            this.Dispose(true)
            GC.SuppressFinalize(this)


type SeqEnumerator(s: ISeq) =
    inherit TypedSeqEnumerator<obj>(s)

type IMapEntrySeqEnumerator(s: ISeq) =
    inherit TypedSeqEnumerator<IMapEntry>(s)


[<AbstractClass>]
[<AllowNullLiteral>]
type ASeq(m) =
    inherit Obj(m)

    [<NonSerialized>]
    let mutable hash: int option = None

    [<NonSerialized>]
    let mutable hasheq: int option = None

    new() = ASeq(null)

    override this.ToString() =
        // complete and total hack until I get RTEnv intiailized figured out:  TODO: FIX THIS!
        if not RTEnv.isInitialized then RTEnvInitialization.initialize ()

        RT.printString (this)

    override x.Equals(o) =
        if Object.ReferenceEquals(x, o) then
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
                    | _ ->
                        Util.equals (s1.first (), s2.first ())
                        && step (s1.next ()) (s2.next ())

                step x (RT.seq (o))
            | _ -> false

    override this.GetHashCode() =
        let rec step (xs: ISeq) (h: int) =
            match xs with
            | null -> h
            | _ ->
                let f = xs.first ()
                let fh = Util.hash f
                step (xs.next ()) (31 * h + fh)

        match hash with
        | None ->
            let h = step ((this :> ISeq).seq()) 1
            hash <- Some h
            h
        | Some h -> h


    static member doCount(s: ISeq) =
        let rec step (s: ISeq) cnt =
            match s with
            | null -> cnt
            | :? Counted as c -> cnt + c.count ()
            | _ -> step (s.next ()) (cnt + 1)

        step s 0

    // This is a little bit of stupidity so we can access GetEnumerator through base in derived classes
    // I've been unable to find a better was

    member x.GetMyEnumerator() = (x :> IEnumerable).GetEnumerator()

    interface ISeq with
        member _.first() =
            raise
            <| NotImplementedException("Subclasses of ASeq must implement ISeq.first()")

        member _.next() =
            raise
            <| NotImplementedException("Subclasses of ASeq must implement ISeq.next()")

        member this.more() =
            let s = (this :> ISeq).next()

            if s = null then EmptyList.Empty :> ISeq else s

        member this.cons(o) = Cons(o, this) :> ISeq

    interface IPersistentCollection with
        member this.cons(o) =
            (this :> ISeq).cons(o) :> IPersistentCollection

        member this.count() = 1 + ASeq.doCount ((this :> ISeq).next())

        member _.empty() = EmptyList.Empty :> IPersistentCollection

        member this.equiv(o) =
            match o with
            | :? Sequential
            | :? IList ->
                let rec step (s1: ISeq) (s2: ISeq) =
                    match s1, s2 with
                    | null, null -> true
                    | _, null -> false
                    | null, _ -> false
                    | _ ->
                        Util.equiv (s1.first (), s2.first ())
                        && step (s1.next ()) (s2.next ())

                step this (RT.seq (o))
            | _ -> false

    interface Seqable with
        member this.seq() = this :> ISeq

    // In the original, we also did IList<obj>  We goenthing special form that, I think.


    interface IList with
        member _.Add(_) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Clear() =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Insert(i, v) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Remove(v) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.RemoveAt(i) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

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
                    raise
                    <| ArgumentOutOfRangeException("index", "Index must be non-negative")

                let rec step i (s: ISeq) =
                    if i = index then
                        s.first ()
                    elif s = null then
                        raise
                        <| ArgumentOutOfRangeException("index", "Index past end of list")
                    else
                        step (i + 1) (s.next ())

                step 0 this // TODO: See IndexOf. Should this be called on x or x.seq() ??  Check original Java code.
            and set _ _ =
                raise
                <| InvalidOperationException("Cannot modify an immutable sequence")

        member this.IndexOf(v) =
            let rec step i (s: ISeq) =
                if isNull s then -1
                else if Util.equiv (s.first (), v) then i
                else step (i + 1) (s.next ())

            step 0 ((this :> ISeq).seq())

        member this.Contains(v) =
            let rec step (s: ISeq) =
                if isNull s then false
                else if Util.equiv (s.first (), v) then true
                else step (s.next ())

            step ((this :> ISeq).seq())

    interface IEnumerable with
        member x.GetEnumerator() = new SeqEnumerator(x) :> IEnumerator

    interface ICollection with
        member x.Count = (x :> IPersistentCollection).count()
        member x.IsSynchronized = true
        member x.SyncRoot = upcast x

        member x.CopyTo(arr: Array, idx) =
            if isNull arr then raise <| ArgumentNullException("array")

            if arr.Rank <> 1 then
                raise
                <| ArgumentException("Array must be 1-dimensional")

            if idx < 0 then
                raise
                <| ArgumentOutOfRangeException("arrayIndex", "must be non-negative")

            if arr.Length - idx < (x :> IPersistentCollection).count() then
                raise
                <| InvalidOperationException
                    ("The number of elements in source is greater than the available space in the array.")

            let rec step (i: int) (s: ISeq) =
                if i < arr.Length && s <> null then
                    arr.SetValue(s.first (), i)
                    step (i + 1) (s.next ())

            step idx (x :> ISeq)

    interface IHashEq with
        member this.hasheq() =
            match hasheq with
            | None ->
                let h = Util.hashOrdered (this)
                hasheq <- Some h
                h
            | Some h -> h

and [<Sealed>] Cons(meta, f: obj, m: ISeq) =
    inherit ASeq(meta)

    let first: obj = f
    let more: ISeq = m

    new(f: obj, m: ISeq) = Cons(null, f, m)

    interface IObj with
        member this.withMeta(m) =
            if Object.ReferenceEquals(m, meta) then (this :> IObj) else Cons(m, first, more) :> IObj

    interface ISeq with
        member _.first() = first
        member this.next() = (this :> ISeq).more().seq()

        member _.more() =
            match more with
            | null -> upcast EmptyList.Empty
            | _ -> more

    interface IPersistentCollection with
        member _.count() = 1 + RT.count (more)


and [<Sealed>] EmptyList(m) =
    inherit Obj(m)

    new() = EmptyList(null)

    static member hasheq =
        Util.hashOrdered (Enumerable.Empty<Object>())

    static member Empty: EmptyList = EmptyList()

    override _.GetHashCode() = 1

    override _.Equals(o) =
        match o with
        | :? Sequential
        | :? IList -> RT.seq (o) |> isNull
        | _ -> false

    interface IObj with
        member this.withMeta(m) =
            if Object.ReferenceEquals(m, (this :> IMeta).meta())
            then this :> IObj
            else EmptyList(m) :> IObj

    interface ISeq with
        member _.first() = null
        member _.next() = null
        member this.more() = this :> ISeq

        member this.cons(o) =
            PersistentList((this :> IMeta).meta(), o, null, 1) :> ISeq

    interface IPersistentCollection with
        member _.count() = 0

        member this.cons(o) =
            (this :> ISeq).cons(o) :> IPersistentCollection

        member this.empty() = this :> IPersistentCollection
        member this.equiv(o) = this.Equals(o)

    interface Seqable with
        member _.seq() = null

    interface IPersistentStack with
        member _.peek() = null

        member _.pop() =
            raise
            <| InvalidOperationException("Attempt to pop an empty list")

    interface Sequential

    interface IPersistentList

    interface IHashEq with
        member _.hasheq() = EmptyList.hasheq

    interface ICollection with
        member _.CopyTo(a: Array, idx: int) = () // no-op
        member _.Count = 0
        member _.IsSynchronized = true
        member this.SyncRoot = upcast this

    static member emptyEnumerator: IEnumerator =
        Seq.empty<obj>.GetEnumerator() :> IEnumerator

    interface IEnumerable with
        member x.GetEnumerator() = EmptyList.emptyEnumerator

    interface IList with
        member _.Add(_) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Clear() =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Insert(i, v) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Remove(v) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.RemoveAt(i) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.IsFixedSize = true
        member _.IsReadOnly = true

        member _.Item
            with get index =
                raise <| ArgumentOutOfRangeException("index")
            and set _ _ =
                raise
                <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.IndexOf(v) = -1
        member _.Contains(v) = false

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
        member this.withMeta(m) =
            if Object.ReferenceEquals(m, (this :> IMeta).meta())
            then this :> IObj
            else PersistentList(m, first, rest, count) :> IObj

    interface ISeq with
        member _.first() = first
        member _.next() = if count = 1 then null else rest.seq ()

        member this.cons(o) =
            PersistentList((this :> IObj).meta(), o, (this :> IPersistentList), count + 1) :> ISeq

    interface IPersistentCollection with
        member _.count() = count

        member this.empty() =
            (EmptyList.Empty :> IObj)
                .withMeta((this :> IMeta).meta()) :?> IPersistentCollection

    interface IPersistentStack with
        member _.peek() = first

        member this.pop() =
            match rest with
            | null ->
                (EmptyList.Empty :> IObj)
                    .withMeta((this :> IMeta).meta()) :?> IPersistentStack
            | _ -> rest :> IPersistentStack

    interface IPersistentList

    interface IReduceInit with
        member this.reduce(fn, start) =
            let rec step (s: ISeq) (value: obj) =
                match s with
                | null -> value
                | _ ->
                    match value with
                    | :? Reduced as r -> (r :> IDeref).deref()
                    | _ -> step (s.next ()) (fn.invoke (value, s.first ()))

            let init =
                fn.invoke (start, (this :> ISeq).first())

            let ret = step ((this :> ISeq).next()) init

            match ret with
            | :? Reduced as r -> (r :> IDeref).deref()
            | _ -> ret

    interface IReduce with
        member this.reduce(fn) =
            let rec step (s: ISeq) (value: obj) =
                match s with
                | null -> value
                | _ ->
                    let nextVal = (fn.invoke (value, s.first ()))

                    match nextVal with
                    | :? Reduced as r -> (r :> IDeref).deref()
                    | _ -> step (s.next ()) nextVal

            step ((this :> ISeq).next()) ((this :> ISeq).first())

// We had to defer this definition until now because we needed PersistentList and Cons
// Eventually we will have to consolidate via an export module.

module RT2 =

    let cons (x: obj, coll: obj): ISeq =
        match coll with
        | null -> upcast PersistentList(x)
        | :? ISeq as s -> upcast Cons(x, s)
        | _ -> upcast Cons(x, RT.seq (coll))

// LazySeq comes up surprisingly early in the food (procedure) chain.
// It relies on very few things external to it, so let's go next.


[<Sealed>]
[<AllowNullLiteral>]
type LazySeq(m1, fn1, s1) =
    inherit Obj(m1)
    let mutable fn: IFn = fn1
    let mutable s: ISeq = s1
    let mutable sv: obj = null
    new(fn: IFn) = LazySeq(null, fn, null)
    new(m1: IPersistentMap, s1: ISeq) = LazySeq(m1, null, s1)

    override this.GetHashCode() =
        match (this :> ISeq).seq() with
        | null -> 1
        | s -> Util.hash s


    override this.Equals(o: obj) =
        match (this :> ISeq).seq(), o with
        | null, :? Sequential
        | null, :? IList -> RT.seq (o) = null
        | null, _ -> false
        | s, _ -> s.Equals(o)

    interface IObj with
        member this.withMeta(meta: IPersistentMap) =
            if Object.ReferenceEquals((this :> IMeta).meta(), meta)
            then this :> IObj
            else LazySeq(meta, (this :> ISeq).seq()) :> IObj

    member _.sval(): obj =
        if not (isNull fn) then
            sv <- fn.invoke ()
            fn <- null

        match sv with
        | null -> upcast s
        | _ -> sv


    interface Seqable with

        [<MethodImpl(MethodImplOptions.Synchronized)>]
        override this.seq() =
            let rec getNext (x: obj) =
                match x with
                | :? LazySeq as ls -> getNext (ls.sval ())
                | _ -> this

            this.sval () |> ignore

            if not (isNull sv) then
                let ls = sv
                sv <- null
                s <- RT.seq (getNext (ls))

            s


    interface IPersistentCollection with
        member _.count() =
            let rec countAux (s: ISeq) (acc: int): int =
                match s with
                | null -> acc
                | _ -> countAux (s.next ()) (acc + 1)

            countAux s 0

        member this.cons(o) = upcast (this :> ISeq).cons(o)
        member _.empty() = upcast PersistentList.Empty

        member this.equiv(o) =
            match (this :> ISeq).seq() with
            | null ->
                match o with
                | :? IList
                | :? Sequential -> RT.seq (o) = null
                | _ -> false
            | s -> s.equiv (o)

    interface ISeq with
        member this.first() =
            (this :> ISeq).seq() |> ignore
            if isNull s then null else s.first ()

        member this.next() =
            (this :> ISeq).seq() |> ignore
            if isNull s then null else s.next ()

        member this.more() =
            (this :> ISeq).seq() |> ignore

            if isNull s then upcast PersistentList.Empty else s.more ()

        member this.cons(o: obj): ISeq = RT2.cons (o, (this :> ISeq).seq())

    interface IPending with
        member _.isRealized() = isNull fn

    interface IHashEq with
        member this.hasheq() = Util.hashOrdered (this)

    interface IEnumerable with
        member this.GetEnumerator() = upcast new SeqEnumerator(this)

    interface IList with
        member _.Add(_) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Clear() =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Insert(i, v) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Remove(v) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.RemoveAt(i) =
            raise
            <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.IsFixedSize = true
        member _.IsReadOnly = true

        member this.Item
            with get index =
                if index < 0 then
                    raise
                    <| ArgumentOutOfRangeException("index", "Index must be non-negative")

                let rec step i (s: ISeq) =
                    if i = index then
                        s.first ()
                    elif s = null then
                        raise
                        <| ArgumentOutOfRangeException("index", "Index past end of list")
                    else
                        step (i + 1) (s.next ())

                step 0 this // TODO: See IndexOf. Should this be called on x or x.seq() ??  Check original Java code.
            and set _ _ =
                raise
                <| InvalidOperationException("Cannot modify an immutable sequence")

        member this.IndexOf(v) =
            let rec step i (s: ISeq) =
                if isNull s then -1
                else if Util.equiv (s.first (), v) then i
                else step (i + 1) (s.next ())

            step 0 ((this :> ISeq).seq())

        member this.Contains(v) =
            let rec step (s: ISeq) =
                if isNull s then false
                else if Util.equiv (s.first (), v) then true
                else step (s.next ())

            step ((this :> ISeq).seq())

    interface ICollection with
        member this.Count = (this :> IPersistentCollection).count()
        member _.IsSynchronized = true
        member this.SyncRoot = upcast this

        member this.CopyTo(arr: Array, idx) =
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

            step idx (this :> ISeq)
