namespace Clojure.Collections.Alternate

open Clojure.Collections
open System
open System.Collections
open System.Collections.Generic

type INode2 =

    abstract assoc: shift: int * hash: int * key: obj * value: obj * addedLeaf: BoolBox -> INode2
    abstract without: shift: int * hash: int * key: obj -> INode2 option
    abstract find: shift: int * hash: int * key: obj -> IMapEntry option
    abstract find: shift: int * hash: int * key: obj * notFound: obj -> obj
    abstract getNodeSeq: unit -> ISeq

    abstract assoc: edit: AtomicBoolean * shift: int * hash: int * key: obj * value: obj * addedLeaf: BoolBox -> INode2

    abstract without: edit: AtomicBoolean * shift: int * hash: int * key: obj * removedLeaf: BoolBox -> INode2 option
    abstract kvReduce: fn: IFn * init: obj -> obj
    abstract fold: combinef: IFn * reducef: IFn * fjtask: IFn * fjfork: IFn * fjjoin: IFn -> obj
    abstract iterator: d: KVMangleFn<obj> -> IEnumerator
    abstract iteratorT: d: KVMangleFn<'T> -> IEnumerator<'T>


[<Sealed>]
type PHashMap(meta: IPersistentMap, count: int, root: INode2 option) =
    inherit APersistentMap()

    member public _.Count = count
    member public _.Root = root
    member public _.Meta = meta

    static member val internal notFoundValue = obj ()

    static member val Empty = PHashMap(null, 0, None)


    static member createD2(other: IDictionary<'TKey, 'TValue>) : IPersistentMap =
        let mutable ret =
            (PHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        for de in Seq.cast<KeyValuePair<'TKey, 'TValue>> other do
            ret <- ret.assoc (de.Key, de.Value)

        ret.persistent ()


    static member create(other: IDictionary) : IPersistentMap =
        let mutable ret =
            (PHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        for de in Seq.cast<DictionaryEntry> other do
            ret <- ret.assoc (de.Key, de.Value)

        ret.persistent ()


    static member createWithCheck([<ParamArray>] init: obj[]) : PersistentHashMap =
        let mutable ret =
            (PHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        for i in 0..2 .. init.Length - 1 do
            ret <- ret.assoc (init[i], init[i + 1])

            if ret.count () <> i / 2 + 1 then
                raise <| ArgumentException("init", "Duplicate key: " + init[ i ].ToString())

        downcast ret.persistent ()


    static member create([<ParamArray>] init: obj[]) : PersistentHashMap =
        let mutable ret =
            (PHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        for i in 0..2 .. init.Length - 1 do
            ret <- ret.assoc (init[i], init[i + 1])

        downcast ret.persistent ()


    static member create1(init: IList) : PersistentHashMap =
        let mutable ret =
            (PHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        let ie = init.GetEnumerator()

        while ie.MoveNext() do
            let key = ie.Current

            if not (ie.MoveNext()) then
                raise <| ArgumentException("init", "No value supplied for " + key.ToString())

            let value = ie.Current
            ret <- ret.assoc (key, value)

        downcast ret.persistent ()


    static member createWithCheck(items: ISeq) : PersistentHashMap =
        let mutable ret =
            (PHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        let rec loop (i: int) (s: ISeq) =
            if not (isNull s) then
                if isNull (s.next ()) then
                    raise
                    <| ArgumentException("items", "No value supplied for key: " + items.first().ToString())

                ret <- ret.assoc (items.first (), RTSeq.second (items))

                if ret.count () <> i + 1 then
                    raise
                    <| ArgumentException("items", "Duplicate key: " + items.first().ToString())

                loop (i + 1) (s.next().next ())

        loop 0 items
        downcast ret.persistent ()


    static member create(items: ISeq) : PersistentHashMap =
        let mutable ret =
            (PHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        let rec loop (s: ISeq) =
            if not (isNull s) then
                if isNull (s.next ()) then
                    raise
                    <| ArgumentException("items", "No value supplied for key: " + items.first().ToString())

                ret <- ret.assoc (s.first (), RTSeq.second (s))
                loop (s.next().next ())

        loop items
        downcast ret.persistent ()

    static member create(meta: IPersistentMap, [<ParamArray>] init: obj[]) : PersistentHashMap =
        (PHashMap.create (init) :> IObj).withMeta (meta) :?> PersistentHashMap


    interface IMeta with
        override this.meta() = meta


    interface IObj with
        override this.withMeta(newMeta) =
            if obj.ReferenceEquals(newMeta, meta) then
                this
            else
                PHashMap(newMeta, 0, None)


    interface ILookup with
        override this.valAt(k) = (this :> ILookup).valAt (k, null)

        override this.valAt(k, nf) =
            match root with
            | None -> nf
            | Some n -> n.find (0, NodeOps.hash (k), k, nf)

    interface Associative with
        override this.containsKey(k) =
            match root with
            | None -> false
            | Some n ->
                n.find (0, NodeOps.hash (k), k, PHashMap.notFoundValue)
                <> PHashMap.notFoundValue

    interface Seqable with
        override this.seq() =
            match root with
            | None -> null
            | Some n -> n.getNodeSeq ()

    interface IPersistentCollection with
        override this.count() = count

        override this.empty() =
            match root with
            | None -> this
            | Some _ -> PHashMap(meta, 0, None)

    interface Counted with
        override _.count() = count

    interface IPersistentMap with
        override _.count() = count

        override this.assoc(k, v) =
            let addedLeaf = BoolBox()

            let rootToUse =
                match root with
                | None -> BNode.Empty
                | Some n -> n

            let newRoot = rootToUse.assoc (0, NodeOps.hash (k), k, v, addedLeaf)

            if obj.ReferenceEquals(newRoot, rootToUse) then
                this
            else
                let newCount = if addedLeaf.isSet then count + 1 else count
                PHashMap(meta, newCount, Some newRoot)

        override this.assocEx(k, v) =
            if (this :> Associative).containsKey (k) then
                raise <| InvalidOperationException("Key already present")
            else
                (this :> IPersistentMap).assoc (k, v)

        override this.without(k) =
            match root with
            | None -> this
            | Some n ->
                match n.without (0, NodeOps.hash (k), k) with
                | None -> PHashMap.Empty
                | Some newRoot ->
                    if obj.ReferenceEquals(newRoot, n) then this
                    elif count = 1 then PHashMap.Empty
                    else PHashMap(meta, count - 1, Some newRoot)

    interface IEditableCollection with
        member this.asTransient() = THashMap(this)

    member this.MakeEnumerator(d: KVMangleFn<Object>) : IEnumerator =
        match root with
        | None -> Seq.empty.GetEnumerator()
        | Some n -> n.iteratorT (d)

    member this.MakeEnumeratorT<'T>(d: KVMangleFn<'T>) : IEnumerator<'T> =
        match root with
        | None -> Seq.empty.GetEnumerator()
        | Some n -> n.iteratorT (d)

    interface IEnumerable<IMapEntry> with
        member this.GetEnumerator() =
            this.MakeEnumeratorT<IMapEntry>(fun (k, v) -> upcast MapEntry.create (k, v))

    interface IEnumerable with
        member this.GetEnumerator() =
            this.MakeEnumerator(fun (k, v) -> upcast MapEntry.create (k, v))

    interface IMapEnumerable with
        member this.keyEnumerator() = this.MakeEnumerator(fun (k, v) -> k)
        member this.valEnumerator() = this.MakeEnumerator(fun (k, v) -> v)

    interface IKVReduce with
        member _.kvreduce(f, init) =
            match init with
            | :? Reduced as r -> (r :> IDeref).deref ()
            | _ ->
                match root with
                | None -> init
                | Some n ->
                    match n.kvReduce (f, init) with
                    | :? Reduced as r -> (r :> IDeref).deref ()
                    | r -> r

    member _.fold(n: int64, combinef: IFn, reducef: IFn, fjinvoke: IFn, fjtask: IFn, fjfork: IFn, fjjoin: IFn) : obj =
        // JVM: we are ignoreing n for now
        let top: Func<obj> =
            Func<obj>(
                (fun () ->
                    let init = combinef.invoke ()

                    match root with
                    | None -> init
                    | Some n -> combinef.invoke (init, n.fold (combinef, reducef, fjtask, fjfork, fjjoin)))
            )

        fjinvoke.invoke (top)


    static member createNode(shift: int, key1: obj, val1: obj, key2hash: int, key2: obj, val2: obj) : INode2 =
        let key1hash = NodeOps.hash (key1)

        if key1hash = key2hash then
            CNode(null, key1hash, 2, [| key1; val1; key2; val2 |])
        else
            let box = BoolBox()
            let edit = AtomicBoolean()

            (BNode.Empty :> INode2)
                .assoc(edit, shift, key1hash, key1, val1, box)
                .assoc (edit, shift, key2hash, key2, val2, box)


    static member internal createNode
        (
            edit: AtomicBoolean,
            shift: int,
            key1: obj,
            val1: obj,
            key2hash: int,
            key2: obj,
            val2: obj
        ) : INode2 =
        let key1hash = NodeOps.hash (key1)

        if key1hash = key2hash then
            CNode(null, key1hash, 2, [| key1; val1; key2; val2 |])
        else
            let box = BoolBox()

            (BNode.Empty :> INode2)
                .assoc(edit, shift, key1hash, key1, val1, box)
                .assoc (edit, shift, key2hash, key2, val2, box)

and [<Sealed>] THashMap(e, r, c) =
    inherit ATransientMap()

    [<NonSerialized>]
    let myEdit: AtomicBoolean = e

    [<VolatileField>]
    let mutable root: INode2 option = r

    [<VolatileField>]
    let mutable count: int = c

    let leafFlag: BoolBox = BoolBox()

    new(m: PHashMap) = THashMap(AtomicBoolean(true), m.Root, m.Count)

    override this.doAssoc(k, v) =
        leafFlag.reset ()

        let nodeToUse =
            match root with
            | None -> (BNode.Empty :> INode2)
            | Some n -> n

        let n = nodeToUse.assoc (myEdit, 0, NodeOps.hash (k), k, v, leafFlag)

        if not <| obj.ReferenceEquals(n, nodeToUse) then
            root <- Some n

        if leafFlag.isSet then
            count <- count + 1

        this


    override this.doWithout(k) =
        match root with
        | None -> this
        | Some n ->
            let newRoot = n.without (myEdit, 0, NodeOps.hash (k), k, leafFlag)

            if not <| obj.ReferenceEquals(newRoot, n) then
                root <- Some n

            if leafFlag.isSet then
                count <- count + 1

            this

    override _.doCount() = count

    override _.doPersistent() =
        myEdit.Set(false)
        PHashMap(null, count, root)

    override _.doValAt(k, nf) =
        match root with
        | None -> nf
        | Some n -> n.find (0, NodeOps.hash (k), k, nf)

    override _.ensureEditable() =
        if not <| myEdit.Get() then
            raise <| InvalidOperationException("Transient used after persistent! call")
