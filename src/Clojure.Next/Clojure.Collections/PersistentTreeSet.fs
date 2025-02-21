namespace Clojure.Collections

open System
open System.Collections

/// An immutable, persistent sorted set.
[<Serializable; AllowNullLiteral>]
type PersistentTreeSet private (_meta: IPersistentMap, _impl: IPersistentMap) =
    inherit APersistentSet(_impl)

    static member Empty = PersistentTreeSet(null, PersistentTreeMap.Empty)

    static member Create(init: ISeq) =
        let rec loop (s: ISeq) (ret: PersistentTreeSet) =
            match s with
            | null -> ret
            | _ -> loop (s.next ()) ((ret :> IPersistentCollection).cons (s.first) :?> PersistentTreeSet)

        loop init PersistentTreeSet.Empty


    static member Create(comp: IComparer, init: ISeq) =
        let rec loop (s: ISeq) (ret: PersistentTreeSet) =
            match s with
            | null -> ret
            | _ -> loop (s.next ()) ((ret :> IPersistentCollection).cons (s.first) :?> PersistentTreeSet)

        loop init (PersistentTreeSet(null, PersistentTreeMap(null, comp)))


    override _.Equals(obj: obj) : bool =
        try
            base.Equals(obj: obj)
        with :? InvalidCastException ->
            false

    override _.GetHashCode() : int = base.GetHashCode()

    interface IObj with
        override this.withMeta(meta: IPersistentMap) : IObj =
            if Object.ReferenceEquals(meta, _meta) then
                this
            else
                PersistentTreeSet(meta, _impl)

    interface IMeta with
        override _.meta() : IPersistentMap = _meta

    interface IPersistentCollection with
        override _.equiv(arg: obj) : bool =
            try
                base.doEquiv (arg)
            with :? InvalidCastException ->
                false

        override this.cons(o: obj) : IPersistentCollection =
            if (this :> IPersistentSet).contains (o) then
                this
            else
                PersistentTreeSet(_meta, _impl.assoc (o, o))

        override this.empty() : IPersistentCollection =
            PersistentTreeSet(_meta, _impl.empty () :?> PersistentTreeMap) :> IPersistentCollection

    interface IPersistentSet with
        override this.disjoin(key: obj) : IPersistentSet =
            if (this :> IPersistentSet).contains (key) then
                PersistentTreeSet(_meta, _impl.without (key))
            else
                this

    interface Reversible with
        member _.rseq() : ISeq =
            KeySeq.create ((_impl :?> Reversible).rseq ())

    interface Sorted with
        member _.entryKey(entry: obj) : obj = entry
        member _.comparator() : IComparer = (_impl :?> Sorted).comparator ()

        member _.seq(ascending: bool) : ISeq =
            let m = _impl :?> Sorted
            KeySeq.create (m.seq (ascending))

        member _.seqFrom(key: obj, ascending: bool) : ISeq =
            let m = _impl :?> Sorted
            KeySeq.create (m.seqFrom (key, ascending))
