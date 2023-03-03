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
                | None -> BNode.Empty :> INode2
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

and [<Sealed>] ANode(e, c, a) =

    let mutable count: int = c
    let mutable nodes: INode2 option array = a

    [<NonSerialized>]
    let myEdit: AtomicBoolean = e

    member private _.setNode(i, n) = nodes[i] <- n
    member _.incrementCount() = count <- count + 1
    member _.decrementCount() = count <- count - 1


    member _.pack(edit: AtomicBoolean, idx: int) : INode2 =
        let newArray: BNodeEntry[] = count - 1 |> Array.zeroCreate
        let mutable j = 0
        let mutable bitmap = 0

        for i = 0 to idx - 1 do
            match nodes.[i] with
            | None -> ()
            | Some n ->
                newArray.[j] <- Node n
                bitmap <- bitmap ||| 1 <<< i
                j <- j + 1

        for i = idx + 1 to nodes.Length - 1 do
            match nodes.[i] with
            | None -> ()
            | Some n ->
                newArray.[j] <- Node n
                bitmap <- bitmap ||| 1 <<< i
                j <- j + 1

        BNode(edit, bitmap, newArray)

    member this.ensureEditable(e) =
        if obj.ReferenceEquals(e, myEdit) then
            this
        else
            ANode(e, count, downcast nodes.Clone())

    member this.editAndSet(e, i, n) =
        let editable = this.ensureEditable (e)
        nodes.[i] <- n
        editable


    interface INode2 with

        member this.assoc(shift, hash, key, value, addedLeaf) =
            let idx = NodeOps.mask (hash, shift)

            match nodes.[idx] with
            | None ->
                let newNode = (BNode.Empty :> INode2).assoc (shift + 5, hash, key, value, addedLeaf)

                ANode(null, count + 1, NodeOps.cloneAndSet (nodes, idx, Some newNode))
            | Some node ->
                let newNode = node.assoc (shift + 5, hash, key, value, addedLeaf)

                if obj.ReferenceEquals(newNode, node) then
                    this
                else
                    ANode(null, count, NodeOps.cloneAndSet (nodes, idx, Some newNode))

        member this.without(shift, hash, key) =
            let idx = NodeOps.mask (hash, shift)

            match nodes.[idx] with
            | None -> Some this
            | Some node ->
                match node.without (shift + 5, hash, key) with
                | None -> // this branch got deleted
                    if count <= 8 then
                        this.pack (null, idx) |> Some // shrink
                    else
                        ANode(null, count - 1, NodeOps.cloneAndSet (nodes, idx, None)) :> INode2 |> Some // zero out this entry
                | Some newNode ->
                    if newNode = node then
                        this :> INode2 |> Some
                    else
                        ANode(null, count - 1, NodeOps.cloneAndSet (nodes, idx, Some newNode)) :> INode2
                        |> Some

        member _.find(shift, hash, key) =
            let idx = NodeOps.mask (hash, shift)

            match nodes[idx] with
            | None -> None
            | Some n -> n.find (shift + 5, hash, key)

        member _.find(shift, hash, key, nf) =
            let idx = NodeOps.mask (hash, shift)

            match nodes[idx] with
            | None -> nf
            | Some n -> n.find (shift + 5, hash, key, nf)

        member _.getNodeSeq() = ANodeSeq.create (nodes)

        member this.assoc(e, shift, hash, key, value, addedLeaf) =
            let idx = NodeOps.mask (hash, shift)

            match nodes.[idx] with
            | None ->
                let editable =
                    this.editAndSet (
                        e,
                        idx,
                        (BNode.Empty :> INode2).assoc (e, shift + 5, hash, key, value, addedLeaf)
                        |> Some
                    )

                editable.incrementCount ()
                editable

            | Some node ->
                let newNode = node.assoc (e, shift + 5, hash, key, value, addedLeaf)

                if obj.ReferenceEquals(newNode, node) then
                    this
                else
                    this.editAndSet (e, idx, newNode |> Some)

        member this.without(e, shift, hash, key, removedLeaf) =
            let idx = NodeOps.mask (hash, shift)

            match nodes.[idx] with
            | None -> this :> INode2 |> Some
            | Some node ->
                match node.without (e, shift + 5, hash, key, removedLeaf) with
                | None -> // this branch got deleted
                    if count <= 8 then
                        this.pack (e, idx) |> Some // shrink
                    else
                        let editable = this.editAndSet (e, idx, None)
                        editable.decrementCount ()
                        editable :> INode2 |> Some

                | Some newNode ->
                    if newNode = node then
                        this :> INode2 |> Some
                    else
                        let editable = this.editAndSet (e, idx, newNode |> Some)

                        editable.decrementCount ()
                        editable :> INode2 |> Some

        member _.kvReduce(f, init) =
            let rec loop (i: int) (v: obj) =
                if i >= nodes.Length then
                    v
                else
                    match nodes[i] with
                    | None -> v
                    | Some n ->
                        let nextV = n.kvReduce (f, v)

                        match nextV with
                        | :? Reduced -> nextV
                        | _ -> loop (i + 1) nextV

            loop 0 init


        member _.fold(combinef, reducef, fjtask, fjfork, fjjoin) =
            let tasks =
                nodes
                |> Array.filter (fun node -> node.IsSome)
                |> Array.map (fun node ->
                    Func<obj>((fun () -> node.Value.fold (combinef, reducef, fjtask, fjfork, fjjoin))))

            ANode.foldTasks (tasks, combinef, fjtask, fjfork, fjjoin)

        member _.iterator(d) =
            let s =
                seq {
                    for node in nodes do
                        match node with
                        | None -> ()
                        | Some n ->
                            let ie = n.iterator (d)

                            while ie.MoveNext() do
                                yield ie.Current
                }

            s.GetEnumerator() :> IEnumerator

        member _.iteratorT(d) =
            let s =
                seq {
                    for node in nodes do
                        match node with
                        | None -> ()
                        | Some n ->
                            let ie = n.iteratorT (d)

                            while ie.MoveNext() do
                                yield ie.Current
                }

            s.GetEnumerator()


    static member foldTasks(tasks: Func<obj>[], combinef: IFn, fjtask: IFn, fjfork: IFn, fjjoin: IFn) =
        match tasks.Length with
        | 0 -> combinef.invoke ()
        | 1 -> tasks[ 0 ].Invoke()
        | _ ->
            let halves = tasks |> Array.splitInto 2

            let fn =
                Func<obj>(fun () -> ANode.foldTasks (halves[1], combinef, fjtask, fjfork, fjjoin))

            let forked = fjfork.invoke (fjtask.invoke (fn))
            combinef.invoke (ArrayNode.foldTasks (halves[0], combinef, fjtask, fjfork, fjjoin), fjjoin.invoke (forked))


and BNodeEntry =
    | KeyValue of Key: obj * Value: obj
    | Node of Node: INode2
    | EmptyEntry

and [<Sealed>] BNode(e, b, a) =

    [<NonSerialized>]
    let myEdit: AtomicBoolean = e

    let mutable bitmap: int = b
    let mutable entries: BNodeEntry array = a

    static member Empty: BNode = BNode(null, 0, Array.empty<BNodeEntry>)

    member this.modifyOrCreateBNode (newEdit: AtomicBoolean) (newBitmap: int) (newEntries: BNodeEntry[]) =
        if obj.ReferenceEquals(myEdit, newEdit) then
            // Current node is editable -- modify in-place
            bitmap <- newBitmap
            entries <- newEntries
            this
        else
            // create new editable node with correct data
            BNode(newEdit, newBitmap, newEntries)

    member _.index(bit: int) : int = NodeOps.bitCount (bitmap &&& (bit - 1))

    member x.Bitmap
        with private get () = bitmap
        and private set (v) = bitmap <- v

    member private _.setEntriesVal(i, v) = entries[i] <- v

    member _.Entries
        with private get () = entries
        and private set (v) = entries <- v

    interface INode2 with
        member this.assoc(shift, hash, key, value, addedLeaf) =
            let bit = NodeOps.bitPos (hash, shift)
            let idx = this.index (bit)

            if bitmap &&& bit = 0 then
                let n = NodeOps.bitCount (bitmap)

                if n >= 16 then
                    let nodes: INode2 option[] = Array.zeroCreate 32
                    let jdx = NodeOps.mask (hash, shift)
                    nodes.[jdx] <- (BNode.Empty :> INode2).assoc (shift + 5, hash, key, value, addedLeaf) |> Some

                    let mutable j = 0

                    for i = 0 to 31 do
                        if ((bitmap >>> i) &&& 1) <> 0 then
                            nodes.[i] <-
                                match entries.[j] with
                                | KeyValue(Key = k; Value = v) ->
                                    (BNode.Empty :> INode2).assoc (shift + 5, NodeOps.hash (k), k, v, addedLeaf)
                                    |> Some
                                | Node(Node = node) -> node |> Some
                                | EmptyEntry ->
                                    InvalidOperationException("Found Empty cell in BitmapEntry -- algorithm bug")
                                    |> raise

                            j <- j + 1

                    ANode(null, n + 1, nodes)

                else
                    let newArray: BNodeEntry[] = Array.zeroCreate (n + 1)
                    Array.Copy(entries, 0, newArray, 0, idx)
                    newArray.[idx] <- KeyValue(key, value)
                    Array.Copy(entries, idx, newArray, idx + 1, n - idx)
                    addedLeaf.set ()
                    BNode(null, bitmap ||| bit, newArray)

            else
                let entry = entries.[idx]

                match entry with
                | KeyValue(Key = k; Value = v) ->
                    if Util.equiv (key, k) then
                        if value = v then
                            this
                        else
                            BNode(null, bitmap, NodeOps.cloneAndSet (entries, idx, KeyValue(key, value)))
                    else
                        addedLeaf.set ()

                        let newNode = PHashMap.createNode (shift + 5, k, v, hash, key, value)

                        BNode(null, bitmap, NodeOps.cloneAndSet (entries, idx, Node(newNode)))
                | Node(Node = node) ->
                    let newNode = node.assoc (shift + 5, hash, key, value, addedLeaf)

                    if newNode = node then
                        this
                    else
                        BNode(null, bitmap, NodeOps.cloneAndSet (entries, idx, Node(newNode)))
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise

        member this.without(shift, hash, key) =
            let bit = NodeOps.bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                this :> INode2 |> Some
            else
                let idx = this.index (bit)

                match entries[idx] with
                | KeyValue(Key = k; Value = v) ->
                    if Util.equiv (key, k) then
                        if bitmap = bit then // only one entry
                            None
                        else
                            BNode(null, bitmap ^^^ bit, NodeOps.removePair (entries, idx)) :> INode2 |> Some
                    else
                        this :> INode2 |> Some
                | Node(Node = node) ->
                    match node.without (shift + 5, hash, key) with
                    | None -> this :> INode2 |> Some
                    | Some n ->
                        if obj.ReferenceEquals(n, node) then
                            this :> INode2 |> Some
                        else
                            BNode(null, bitmap, NodeOps.cloneAndSet (entries, idx, Node(n))) :> INode2
                            |> Some
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise

