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

module private NodeOps2 =

    // things that are not in NodeOps

    let removeEntry (arr: 'T[], i: int) : 'T[] =
        let newArr: 'T[] = Array.zeroCreate <| arr.Length - 1
        Array.Copy(arr, 0, newArr, 0, i)
        Array.Copy(arr, (i + 1), newArr, i, newArr.Length - i)
        newArr

[<Sealed>]
type PHashMap(meta: IPersistentMap, count: int, root: INode2 option) =
    inherit APersistentMap()

    member public _.Count = count
    member public _.Root = root
    member public _.Meta = meta

    static member val internal notFoundValue = obj ()

    static member val Empty = PHashMap(null, 0, None)

    new(c, r) = PHashMap(null, c, r)

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


    static member createWithCheck([<ParamArray>] init: obj[]) : PHashMap =
        let mutable ret =
            (PHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        for i in 0..2 .. init.Length - 1 do
            ret <- ret.assoc (init[i], init[i + 1])

            if ret.count () <> i / 2 + 1 then
                raise <| ArgumentException("init", "Duplicate key: " + init[ i ].ToString())

        downcast ret.persistent ()


    static member create([<ParamArray>] init: obj[]) : PHashMap =
        let mutable ret =
            (PHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        for i in 0..2 .. init.Length - 1 do
            ret <- ret.assoc (init[i], init[i + 1])

        downcast ret.persistent ()


    static member create1(init: IList) : PHashMap =
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


    static member createWithCheck(items: ISeq) : PHashMap =
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


    static member create(items: ISeq) : PHashMap =
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

    static member create(meta: IPersistentMap, [<ParamArray>] init: obj[]) : PHashMap =
        (PHashMap.create (init) :> IObj).withMeta (meta) :?> PHashMap


    interface IMeta with
        override this.meta() = meta


    interface IObj with
        override this.withMeta(newMeta) =
            if obj.ReferenceEquals(newMeta, meta) then
                this
            else
                PHashMap(newMeta, count, root)

    interface ILookup with
        override this.valAt(k) = (this :> ILookup).valAt (k, null)

        override this.valAt(k, nf) =
            match root with
            | None -> nf
            | Some n -> n.find (0, NodeOps.hash (k), k, nf)

    interface Associative with
        override _.containsKey(k) =
            match root with
            | None -> false
            | Some n ->
                n.find (0, NodeOps.hash (k), k, PHashMap.notFoundValue)
                <> PHashMap.notFoundValue

        override _.entryAt(k) =
            match root with
            | None -> null
            | Some n ->
                match n.find (0, NodeOps.hash (k), k) with
                | None -> null
                | Some v -> v

    interface Seqable with
        override _.seq() =
            match root with
            | None -> null
            | Some n -> n.getNodeSeq ()

    interface IPersistentCollection with
        override _.count() = count

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
                    elif count = 1 then PHashMap.Empty //  TODO -- is this possible?  Shouldn't n.without return None in this case?  and shouldn't we preserrve the meta?
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
            CNode(null, key1hash, 2, [| MapEntry(key1, val1); MapEntry(key2, val2) |])
        else
            let box = BoolBox()

            (BNode.Empty :> INode2)
                .assoc(shift, key1hash, key1, val1, box)
                .assoc (shift, key2hash, key2, val2, box)


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
            CNode(edit, key1hash, 2, [| MapEntry(key1, val1); MapEntry(key2, val2) |])
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
            leafFlag.reset ()
            let newRoot = n.without (myEdit, 0, NodeOps.hash (k), k, leafFlag)

            if not <| obj.ReferenceEquals(newRoot, n) then
                root <- Some n

            if leafFlag.isSet then
                count <- count - 1

            this

    override _.doCount() = count

    override _.doPersistent() =
        myEdit.Set(false)
        PHashMap(count, root)

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

    do
        let mutable cnt = 0
        for i = 0 to nodes.Length-1 do
            if nodes[i].IsSome then cnt <- cnt+1
        if cnt <> count then
            Console.WriteLine("Mismatch!!!")



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
            match nodes[i] with
            | None -> ()
            | Some n ->
                newArray[j] <- Node n
                bitmap <- bitmap ||| (1 <<< i)
                j <- j + 1

        for i = idx + 1 to nodes.Length - 1 do
            match nodes[i] with
            | None -> ()
            | Some n ->
                newArray[j] <- Node n
                bitmap <- bitmap ||| (1 <<< i)
                j <- j + 1

        BNode(edit, bitmap, newArray)


    member this.ensureEditable(e) =
        if obj.ReferenceEquals(e, myEdit) then
            this
        else
            ANode(e, count, downcast nodes.Clone())

    member this.editAndSet(e, i, n) =
        let editable = this.ensureEditable (e)
        nodes[i] <- n
        editable


    interface INode2 with

        member this.assoc(shift, hash, key, value, addedLeaf) =
            let idx = NodeOps.mask (hash, shift)

            match nodes[idx] with
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

            match nodes[idx] with
            | None -> Some this
            | Some node ->
                match node.without (shift + 5, hash, key) with
                | None -> // this branch got deleted
                    if count <= 8 then
                        this.pack (null, idx) |> Some // shrink
                    else
                        ANode(null, count - 1, NodeOps.cloneAndSet (nodes, idx, None)) :> INode2 |> Some // zero out this entry
                | Some newNode ->
                    if obj.ReferenceEquals(newNode, node) then
                        this :> INode2 |> Some
                    else
                        ANode(null, count, NodeOps.cloneAndSet (nodes, idx, Some newNode)) :> INode2
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

        member _.getNodeSeq() = ANodeSeq.create (nodes, 0)

        member this.assoc(e, shift, hash, key, value, addedLeaf) =
            let idx = NodeOps.mask (hash, shift)
            // Console.Write($"A.a {shift} {hash} {key} {value} {idx}: ")

            match nodes[idx] with
            | None ->
                // Console.WriteLine("--empty, assoc to BNode.Empty")
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
                // Console.WriteLine($"--assoc to {node}");
                let newNode = node.assoc (e, shift + 5, hash, key, value, addedLeaf)

                if obj.ReferenceEquals(newNode, node) then
                    this
                else
                    this.editAndSet (e, idx, newNode |> Some)

        member this.without(e, shift, hash, key, removedLeaf) =
            let idx = NodeOps.mask (hash, shift)

            match nodes[idx] with
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
                    if obj.ReferenceEquals(newNode, node) then
                        this :> INode2 |> Some
                    else
                        this.editAndSet (e, idx, newNode |> Some) :> INode2 |> Some

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

    member this.editAndSet(e: AtomicBoolean, idx: int, entry: BNodeEntry) : BNode =
        if obj.ReferenceEquals(e, myEdit) then
            entries[idx] <- entry
            this
        else
            let newEntries = entries.Clone() :?> BNodeEntry array
            newEntries[idx] <- entry
            BNode(e, bitmap, entries)

    member this.editAndRemove(e: AtomicBoolean, bit: int, idx: int) : INode2 option =
        if bitmap = bit then
            None
        elif obj.ReferenceEquals(myEdit, e) then
            Array.Copy(entries, idx + 1, entries, idx, entries.Length - idx - 1)
            entries[idx] <- EmptyEntry
            this :> INode2 |> Some
        else
            let newEntries: BNodeEntry array = Array.zeroCreate (entries.Length - 1)
            Array.Copy(entries, 0, newEntries, 0, idx - 1)
            Array.Copy(entries, idx + 1, newEntries, idx, entries.Length - idx - 1)
            BNode(e, bitmap ^^^ bit, newEntries) :> INode2 |> Some


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
                    nodes[jdx] <- (BNode.Empty :> INode2).assoc (shift + 5, hash, key, value, addedLeaf) |> Some

                    let mutable j = 0

                    for i = 0 to 31 do
                        if ((bitmap >>> i) &&& 1) <> 0 then
                            nodes[i] <-
                                match entries[j] with
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
                    newArray[idx] <- KeyValue(key, value)
                    Array.Copy(entries, idx, newArray, idx + 1, n - idx)
                    addedLeaf.set ()
                    BNode(null, bitmap ||| bit, newArray)

            else
                let entry = entries[idx]

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

                    if obj.ReferenceEquals(newNode, node) then
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
                            BNode(null, bitmap ^^^ bit, NodeOps2.removeEntry (entries, idx)) :> INode2 |> Some
                    else
                        this :> INode2 |> Some
                | Node(Node = node) ->
                    match node.without (shift + 5, hash, key) with
                    | None ->
                        if bitmap = bit then // only one entry
                            None
                        else    
                            BNode(null,bitmap^^^bit,NodeOps2.removeEntry(entries,idx)) :> INode2 |> Some                    
                    | Some n ->
                        if obj.ReferenceEquals(n, node) then
                            this :> INode2 |> Some
                        else
                            BNode(null, bitmap, NodeOps.cloneAndSet (entries, idx, Node(n))) :> INode2
                            |> Some
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise

        member this.find(shift, hash, key) =
            let bit = NodeOps.bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                None
            else
                let idx = this.index (bit)

                match entries[idx] with
                | KeyValue(Key = k; Value = v) ->
                    if Util.equiv (key, k) then
                        MapEntry(k, v) :> IMapEntry |> Some
                    else
                        None
                | Node(Node = node) -> node.find (shift + 5, hash, key)
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise

        member this.find(shift, hash, key, nf) =
            let bit = NodeOps.bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                nf
            else
                let idx = this.index (bit)

                match entries[idx] with
                | KeyValue(Key = k; Value = v) -> if Util.equiv (key, k) then v else nf
                | Node(Node = node) -> node.find (shift + 5, hash, key, nf)
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise

        member this.getNodeSeq() = BNodeSeq.create (entries, 0)

        member this.assoc(e, shift, hash, key, value, addedLeaf) =
            let bit = NodeOps.bitPos (hash, shift)
            let idx = this.index (bit)

            // Console.Write($"B.a {shift} {hash} {key} {value} {bit} {idx}: ")

            if (bitmap &&& bit) <> 0 then
                match entries[idx] with
                | KeyValue(Key = k; Value = v) ->
                    if Util.equiv (k, key) then
                        if value = v then
                            // Console.WriteLine("-- found key, value matches");
                            this
                        else
                            // Console.WriteLine("--found key, new value");
                            this.editAndSet (e, idx, KeyValue(key, value))
                    else
                        //// Console.WriteLine("--found kv, wrong key, createNode")
                        addedLeaf.set ()
                        let newNode = PHashMap.createNode (e, shift + 5, k, v, hash, key, value)
                        this.editAndSet (e, idx, Node newNode)
                | Node(Node = node) ->
                    let newNode = node.assoc (e, shift + 5, hash, key, value, addedLeaf)

                    if obj.ReferenceEquals(node, newNode) then
                        this
                    else
                        this.editAndSet (e, idx, Node newNode)
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise
            else
                let n = NodeOps.bitCount (bitmap)

                if obj.ReferenceEquals(myEdit, e) && n < entries.Length then
                    // we have space in the array and we are already editing this node transiently, so we can just move things areound.
                    // if we have space in the array but we are not already editing this node, then the space is not helpful -- we just fall through to the next case.
                    addedLeaf.set ()
                    Array.Copy(entries, idx, entries, idx + 1, n - idx)
                    entries[idx] <- KeyValue(key, value)
                    bitmap <- bitmap ||| idx
                    this
                elif n >= 16 then
                    let nodes: INode2 option array = Array.zeroCreate 32
                    let jdx = NodeOps.mask (hash, shift)

                    nodes[jdx] <-
                        (BNode.Empty :> INode2).assoc (e, shift + 5, hash, key, value, addedLeaf)
                        |> Some

                    let mutable j = 0

                    for i = 0 to 31 do
                        if ((bitmap >>> i) &&& 1) <> 0 then
                            nodes[i] <-
                                match entries[j] with
                                | KeyValue(Key = k; Value = v) ->
                                    (BNode.Empty :> INode2).assoc (e, shift + 5, NodeOps.hash(k), k, v, addedLeaf) |> Some
                                | Node(Node = node) -> node |> Some
                                | EmptyEntry ->
                                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                                    |> raise

                            j <- j + 1

                    ANode(e, n + 1, nodes)
                else
                    let newArray: BNodeEntry[] = Array.zeroCreate (n + 1)
                    Array.Copy(entries, 0, newArray, 0, idx)
                    newArray[idx] <- KeyValue(key, value)
                    Array.Copy(entries, idx, newArray, idx + 1, n - idx)
                    addedLeaf.set ()
                    this.modifyOrCreateBNode e (bitmap ||| bit) newArray

        member this.without(e, shift, hash, key, removedLeaf) =
            let bit = NodeOps.bitPos (hash, shift)

            if bitmap &&& bit = 0 then
                this :> INode2 |> Some
            else
                let idx = this.index (bit)

                match entries[idx] with
                | KeyValue(Key = k) ->
                    if Util.equiv (key, k) then
                        removedLeaf.set ()
                        this.editAndRemove (e, bit, idx)
                    else
                        this :> INode2 |> Some
                | Node(Node = n) ->
                    match n.without (e, shift + 5, hash, key, removedLeaf) with
                    | None -> this :> INode2 |> Some // TODO:  Is this right?
                    | Some newNode ->
                        if obj.ReferenceEquals(newNode, n) then
                            this :> INode2 |> Some
                        elif bitmap = bit then
                            None
                        else
                            this.editAndRemove (e, bit, idx)
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise

        member _.kvReduce(f, init) = BNodeSeq.kvReduce (entries, f, init)

        member _.fold(combinef, reducef, fjtask, fjfork, fjjoin) =
            BNodeSeq.kvReduce (entries, reducef, combinef.invoke ())

        member this.iterator d =
            let s =
                seq {
                    for entry in entries do
                        match entry with
                        | KeyValue(Key = k; Value = v) -> yield d (k, v)
                        | Node(Node = node) ->
                            let ie = node.iterator (d)

                            while ie.MoveNext() do
                                yield ie.Current
                        | EmptyEntry -> ()

                }

            s.GetEnumerator()


        member this.iteratorT d =
            let s =
                seq {
                    for entry in entries do
                        match entry with
                        | KeyValue(Key = k; Value = v) -> yield d (k, v)
                        | Node(Node = node) ->
                            let ie = node.iteratorT (d)

                            while ie.MoveNext() do
                                yield ie.Current
                        | EmptyEntry -> ()

                }

            s.GetEnumerator()

and [<Sealed>] internal CNode(e: AtomicBoolean, h: int, c: int, a: MapEntry[]) =

    [<NonSerialized>]
    let myEdit: AtomicBoolean = e

    let mutable count: int = c
    let mutable kvs: MapEntry[] = a
    let nodeHash: int = h

    member this.tryFindNodeIndex key =
        kvs
        |> Array.tryFindIndex (fun kv -> (not (isNull kv)) && Util.equiv ((kv :> IMapEntry).key (), key))

    interface INode2 with
        member this.assoc(shift, hash, key, value, addedLeaf) =
            if hash = nodeHash then
                match this.tryFindNodeIndex key with
                | Some idx ->
                    let kv = kvs.[idx] :> IMapEntry

                    if kv.value () = value then
                        this
                    else
                        CNode(null, hash, count, NodeOps.cloneAndSet (kvs, idx, MapEntry(key, value)))
                | None ->
                    let newArray: MapEntry[] = count + 1 |> Array.zeroCreate
                    Array.Copy(kvs, 0, newArray, 0, count)
                    newArray.[count] <- MapEntry(key, value)
                    addedLeaf.set ()
                    CNode(null, hash, count + 1, newArray)
            else
                (BNode(null, NodeOps.bitPos (hash, shift), [| Node(this) |]) :> INode2)
                    .assoc (shift, h, key, value, addedLeaf)

        member this.without(shift, hash, key) =
            match this.tryFindNodeIndex key with
            | None -> this :> INode2 |> Some
            | Some idx ->
                if count = 1 then
                    None
                else
                    CNode(null, h, count-1, NodeOps2.removeEntry (kvs, idx)) :> INode2 |> Some

        member this.find(shift, hash, key) =
            match this.tryFindNodeIndex key with
            | None -> None
            | Some idx -> Some(upcast kvs.[idx])


        member this.find(shift, hash, key, notFound) =
            match this.tryFindNodeIndex key with
            | None -> notFound
            | Some idx -> (kvs.[idx] :> IMapEntry).value ()

        member this.getNodeSeq() = CNodeSeq.create (kvs, 0)

        member this.assoc(e, shift, hash, key, value, addedLeaf) =
            if hash = nodeHash then
                match this.tryFindNodeIndex key with
                | Some idx ->
                    let kv = kvs.[idx] :> IMapEntry

                    if kv.value () = value then
                        this
                    elif obj.ReferenceEquals(myEdit, e) then
                        kvs.[idx] <- MapEntry(key, value)
                        this
                    else
                        // we have an entry with a different value, but we are not editable.
                        // create a new node with the new k/v entry
                        CNode(e, hash, count, NodeOps.cloneAndSet (kvs, idx, MapEntry(key, value)))
                | None ->
                    // no entry for this key, so we will be adding.
                    addedLeaf.set ()

                    if obj.ReferenceEquals(myEdit, e) then
                        // we are editable.
                        // Either we have existing space or we need to create a new array.
                        // Either way, we can update in place.
                        if kvs.Length > count then
                            kvs.[count] <- MapEntry(key, value)
                            count <- count + 1
                            this
                        else
                            // no space, create a new array.
                            let currLength = kvs.Length
                            let newArray: MapEntry[] = currLength + 1 |> Array.zeroCreate
                            Array.Copy(kvs, 0, newArray, 0, currLength)
                            newArray.[currLength] <- MapEntry(key, value)
                            kvs <- newArray
                            count <- count + 1
                            this
                    else
                        // we are not editable, so we need to create a new node

                        let newArray: MapEntry[] = count + 1 |> Array.zeroCreate
                        Array.Copy(kvs, 0, newArray, 0, count)
                        newArray.[count] <- MapEntry(key, value)

                        CNode(e, hash, count + 1, newArray)
            else
                // we got to this collision node, but our key has different hash.
                // Need to create a bitmap node here holding our collision node and add our new key/value to it.
                (BNode(e, NodeOps.bitPos (hash, shift), [| Node(this) |]) :> INode2)
                    .assoc (e, shift, h, key, value, addedLeaf)

        member this.without(e, shift, hash, key, removedLeaf) =
            match this.tryFindNodeIndex key with
            | None -> this :> INode2 |> Some
            | Some idx ->
                removedLeaf.set ()

                if count = 1 then
                    None
                else if obj.ReferenceEquals(myEdit, e) then
                    // we are editable, edit in place
                    count <- count - 1
                    kvs.[idx] <- kvs.[count]
                    kvs.[count] <- null
                    this :> INode2 |> Some
                else
                    CNode(e, h, count - 1, NodeOps2.removeEntry (kvs, idx)) :> INode2 |> Some

        member _.kvReduce(f, init) = CNodeSeq.kvReduce (kvs, f, init)

        member _.fold(combinef, reducef, fjtask, fjfork, fjjoin) =
            CNodeSeq.kvReduce (kvs, reducef, combinef.invoke ())


        member this.iterator d =
            let s =
                seq {
                    for kv in kvs do
                        let me = kv :> IMapEntry
                        yield d (me.key (), me.value ())
                }

            s.GetEnumerator()

        member this.iteratorT d =
            let s =
                seq {
                    for kv in kvs do
                        let me = kv :> IMapEntry
                        yield d (me.key (), me.value ())
                }

            s.GetEnumerator()

and ANodeSeq(nodes: INode2 option array, idx: int, s: ISeq) =
    inherit ASeq()

    static member create(nodes: (INode2 option)[], idx: int) : ISeq =
        if idx >= nodes.Length then
            null
        else
            match nodes.[idx] with
            | Some(node) ->
                match node.getNodeSeq () with
                | null -> ANodeSeq.create (nodes, idx + 1)
                | s -> ANodeSeq(nodes, idx, s)
            | None -> ANodeSeq.create (nodes, idx + 1)

    interface ISeq with
        member _.first() = s.first ()

        member _.next() =
            match s.next () with
            | null -> ANodeSeq.create (nodes, idx + 1)
            | s1 -> ANodeSeq(nodes, idx, s1)


and BNodeSeq(entries: BNodeEntry[], idx: int, seq: ISeq) =
    inherit ASeq()

    static member create(entries: BNodeEntry[], idx: int) : ISeq =
        if idx >= entries.Length then
            null
        else
            match entries.[idx] with
            | KeyValue(_, _) -> BNodeSeq(entries, idx, null)
            | Node(Node = node) ->
                match node.getNodeSeq () with
                | null -> BNodeSeq.create (entries, idx + 1)
                | s -> BNodeSeq(entries, idx, s)
            | EmptyEntry -> null

    interface ISeq with
        member _.first() =
            match entries.[idx] with
            | KeyValue(Key = k; Value = v) -> MapEntry(k, v)
            | Node(Node = _) -> seq.first ()
            | EmptyEntry ->
                InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                |> raise


        member _.next() =
            match entries.[idx] with
            | KeyValue(_, _) -> BNodeSeq.create (entries, idx + 1)
            | Node(_) ->
                match seq.next () with
                | null -> BNodeSeq.create (entries, idx + 1)
                | s -> BNodeSeq(entries, idx, s)
            | EmptyEntry ->
                InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                |> raise

    static member kvReduce(entries: BNodeEntry array, f: IFn, init: obj) : obj =
        let rec loop (idx: int) (acc: obj) =
            if idx >= entries.Length then
                acc
            else
                let nextAcc =
                    match entries[idx] with
                    | KeyValue(Key = k; Value = v) -> f.invoke (acc, k, v)
                    | Node(Node = n) -> n.kvReduce (f, acc)
                    | EmptyEntry -> acc

                match nextAcc with
                | :? Reduced -> nextAcc
                | _ -> loop (idx + 1) nextAcc

        loop 0 init


and CNodeSeq(kvs: MapEntry[], idx: int) =
    inherit ASeq()

    static member create(kvs: MapEntry[], idx: int) : ISeq =
        if idx >= kvs.Length then null else CNodeSeq(kvs, idx)

    interface ISeq with
        member _.first() = kvs.[idx]
        member _.next() = CNodeSeq.create (kvs, idx + 1)

    static member kvReduce(kvs: MapEntry array, f:IFn, init: obj) : obj =
        let rec loop (idx: int) (acc: obj) =
            if idx >= kvs.Length then
                acc
            else
                let nextAcc =
                    match kvs[idx] with
                    | null -> acc
                    | entry -> f.invoke(acc,(entry:>IMapEntry).key,(entry:>IMapEntry).value)

                match nextAcc with
                | :? Reduced -> nextAcc
                | _ -> loop (idx + 1) nextAcc

        loop 0 init
