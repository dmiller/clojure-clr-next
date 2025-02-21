namespace Clojure.Collections

open Clojure.Numerics
open System
open System.Collections
open System.Collections.Generic


/// Abstract base class for persistent sets
[<AbstractClass; AllowNullLiteral>]
type APersistentSet(_impl: IPersistentMap) =
    inherit AFn()

    let mutable _hasheq = 0

    member internal _.Impl = _impl

    override this.ToString() : string = RTPrint.printString (this)
    override this.GetHashCode() : int = this.hasheq ()
    override this.Equals(obj: obj) : bool = APersistentSet.setEquals (this, obj)


    static member setEquals(s: IPersistentSet, obj: obj) : bool =

        let equalsIPersistentSet (s1: IPersistentSet, s2: IPersistentSet) =
            if (s1 :> Counted).count () <> (s2 :> Counted).count () then
                false
            else
                let rec loop (s2s: ISeq) =
                    match s2s with
                    | null -> true
                    | _ when not <| s1.contains (s2s.first ()) -> false
                    | _ -> loop (s2s.next ())

                loop (s2.seq ())


        let equalsISet (s: IPersistentSet, set: ISet<Object>) =
            if (s :> Counted).count () <> set.Count then
                false
            else
                (set :> IEnumerable<Object>) |> Seq.forall (fun o -> s.contains (o))

        match obj with
        | _ when Object.ReferenceEquals(s, obj) -> true
        | :? IPersistentSet as set -> equalsIPersistentSet (s, set)
        | :? ISet<Object> as set -> equalsISet (s, set)
        | _ -> false

    member this.hasheq() : int =
        if _hasheq = 0 then
            _hasheq <- Hashing.hashUnordered (this :> IEnumerable)

        _hasheq

    interface IPersistentSet with
        member _.disjoin(key: obj) : IPersistentSet =
            raise <| NotImplementedException("Concrete subclasses must define disjoin")

        member _.contains(key: obj) : bool = _impl.containsKey (key)
        member _.get(key: obj) : obj = _impl.valAt (key)
        member _.count() : int = _impl.count ()

    interface Counted with
        member _.count() : int = _impl.count ()

    interface IPersistentCollection with
        member _.count() : int = _impl.count ()
        member _.seq() : ISeq = KeySeq.create (_impl.seq ())
        member this.equiv(arg: obj) : bool = APersistentSet.setEquals (this, arg)

        member _.cons(o: obj) : IPersistentCollection =
            raise <| InvalidOperationException("Concrete subclasses must define cons")

        member _.empty() : IPersistentCollection =
            raise <| InvalidOperationException("Concrete subclasses must define empty")

    member this.doEquiv(arg: obj) : bool =
        (this :> IPersistentCollection).equiv (arg)

    interface IFn with
        member this.invoke(arg1: obj) : obj = (this :> IPersistentSet).get ()

    interface ICollection<Object> with
        member _.Add(item: obj) =
            raise <| InvalidOperationException("Cannot modify immutable set")

        member _.Clear() =
            raise <| InvalidOperationException("Cannot modify immutable set")

        member this.Contains(item: obj) : bool =
            (this :> IPersistentSet).contains (item)

        member this.CopyTo(array: obj array, arrayIndex: int) =
            let mutable i = arrayIndex

            for v in (this :> ICollection<Object>) do
                array.SetValue(v, arrayIndex)
                i <- i + 1

        member _.Remove(item: obj) : bool =
            raise <| InvalidOperationException("Cannot modify immutable set")

        member this.Count: int = (this :> IPersistentCollection).count ()
        member this.IsReadOnly: bool = true

    interface ICollection with
        member this.CopyTo(array: Array, arrayIndex: int) =
            let mutable i = arrayIndex

            for v in (this :> ICollection) do
                array.SetValue(v, arrayIndex)
                i <- i + 1

        member this.Count = (this :> IPersistentCollection).count ()
        member this.IsSynchronized: bool = true
        member this.SyncRoot: obj = this

    interface IEnumerable<Object> with
        member this.GetEnumerator() =
            let generator (s: ISeq) : (obj * ISeq) option =
                if isNull s then None else Some(s.first (), s.next ())

            let s = Seq.unfold generator ((this :> Seqable).seq ())
            s.GetEnumerator()

    interface IEnumerable with
        member this.GetEnumerator() =
            (this :> IEnumerable<Object>).GetEnumerator()

/// Abstract base class for transient sets
[<AbstractClass; AllowNullLiteral>]
type ATransientSet(impl: ITransientMap) =
    inherit AFn()

    let mutable _impl = impl

    interface Counted with
        member _.count() = _impl.count ()

    interface ITransientCollection with
        member this.conj(o: obj) : ITransientCollection =
            let m = _impl.assoc (o, o)

            if not <| Object.ReferenceEquals(m, _impl) then
                _impl <- m

            this

        member this.persistent() : IPersistentCollection =
            raise <| NotImplementedException("Concrete subclasses must define persistent")

    interface ITransientSet with
        member this.disjoin(key: obj) : ITransientSet =
            let m = _impl.without (key)

            if not <| Object.ReferenceEquals(m, _impl) then
                _impl <- m

            this

        member this.contains(key: obj) : bool =
            not <| Object.ReferenceEquals(_impl.valAt (key), this)

        member this.get(key: obj) : obj = _impl.valAt (key)

/// An immutable, persistent set
type PersistentHashSet(_meta: IPersistentMap, _impl: IPersistentMap) =
    inherit APersistentSet(_impl)

    /// An empty PersistentHashSet
    static member Empty = PersistentHashSet(null, PersistentHashMap.Empty)

    /// Create a PersistentHashSet from an array of objects.
    static member create([<ParamArray>] init: obj[]) =
        let mutable ret = (PersistentHashSet.Empty :> IEditableCollection).asTransient ()

        for o in init do
            ret <- ret.conj (o)

        ret.persistent () :?> PersistentHashSet

    /// Create a PersistentHashSet from an IList.
    static member create(init: IList) =
        let mutable ret = (PersistentHashSet.Empty :> IEditableCollection).asTransient ()

        for o in init do
            ret <- ret.conj (o)

        ret.persistent () :?> PersistentHashSet

    /// Create a PersistentHashSet from an ISeq.
    static member create(items: ISeq) =
        let rec loop (ret: ITransientCollection) (s: ISeq) =
            match s with
            | null -> ret
            | _ -> loop (ret.conj (s.first ())) (s.next ())

        let ret =
            loop ((PersistentHashSet.Empty :> IEditableCollection).asTransient ()) items

        ret.persistent () :?> PersistentHashSet

    /// Create a PersistentHashSet from an array of objects, checking for duplicates. (Throws if duplicate item found.)
    static member createWithCheck([<ParamArray>] init: obj[]) =
        let mutable ret =
            (PersistentHashSet.Empty :> IEditableCollection).asTransient () :?> ITransientSet

        for i = 0 to init.Length - 1 do
            ret <- ret.conj (init[i]) :?> ITransientSet

            if (ret.count () <> i + 1) then
                raise <| ArgumentException("Duplicate key: " + init[ i ].ToString())

        ret.persistent () :?> PersistentHashSet

    /// Create a PersistentHashSet from an IList, checking for duplicates. (Throws if duplicate item found.)
    static member createWithCheck(init: IList) =
        let mutable ret =
            (PersistentHashSet.Empty :> IEditableCollection).asTransient () :?> ITransientSet

        let mutable i = 0

        for o in init do
            ret <- ret.conj (o) :?> ITransientSet

            if (ret.count () <> i + 1) then
                raise <| ArgumentException("Duplicate key: " + init[ i ].ToString())

            i <- i + 1

        ret.persistent () :?> PersistentHashSet

    /// Create a PersistentHashSet from an ISeq, checking for duplicates. (Throws if duplicate item found.)
    static member createWithCheck(items: ISeq) =
        let rec loop (index: int) (ret: ITransientSet) (s: ISeq) =
            match s with
            | null -> ret
            | _ when (ret.count () <> index + 1) -> raise <| ArgumentException("Duplicate key: " + s.first().ToString())
            | _ -> loop (index + 1) (ret.conj (s.first ()) :?> ITransientSet) (s.next ())

        let ret =
            loop 0 ((PersistentHashSet.Empty :> IEditableCollection).asTransient () :?> ITransientSet) items

        ret.persistent () :?> PersistentHashSet

    interface IObj with
        member this.withMeta(m) : IObj =
            if Object.ReferenceEquals(_meta, m) then
                this
            else
                PersistentHashSet(m, _impl)

    interface IMeta with
        member this.meta() : IPersistentMap = _meta

    interface IPersistentSet with
        override this.disjoin(key: obj) : IPersistentSet =
            if (this :> IPersistentSet).contains (key) then
                new PersistentHashSet(_meta, _impl.without (key))
            else
                this

    interface IPersistentCollection with
        override this.cons(o: obj) =
            if (this :> IPersistentSet).contains (o) then
                this
            else
                PersistentHashSet(_meta, _impl.assoc (o, o))

    interface IEditableCollection with
        member _.asTransient() : ITransientCollection =
            TransientHashSet((_impl :?> PersistentHashMap :> IEditableCollection).asTransient () :?> ITransientMap)

/// A transient hash-set
and [<AllowNullLiteral>] TransientHashSet(_impl: ITransientMap) =
    inherit ATransientSet(_impl)

    interface ITransientCollection with
        override this.persistent() : IPersistentCollection =
            PersistentHashSet(null, _impl.persistent ())
