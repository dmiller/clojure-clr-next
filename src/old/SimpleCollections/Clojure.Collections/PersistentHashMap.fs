namespace Clojure.Collections

open System
open System.Threading
open Clojure.Collections
open System.Collections
open System.Collections.Generic

// A PersistentHashMap consists of a head node representing the map that has a points to a tree of nodes containing the key/value pairs.
// The head node indicated if null is a key and holds the associated value.
// Thus the tree is guaranteed not to contain a null key, allowing null to be used as an 'empty field' indicator.
// The tree contains three kinds of nodes:
//     ArrayNode
//     BitmapIndexedNode
//     HashCollisionNode
//
// This arrangement seems ideal for a discriminated union,
//  but the need for mutable fields (required to implement IEditableCollection and the ITransientXxx interfaces in-place)
//  made the code unwieldy.  Perhaps some smarter than me can do this someday.


type KVMangleFn<'T> = obj * obj -> 'T


[<AllowNullLiteral>]
type INode =
    abstract assoc: shift:int * hash:int * key:obj * value:obj * addedLeaf:SillyBox -> INode
    abstract without: shift:int * hash:int * key:obj -> INode
    abstract find: shift:int * hash:int * key:obj -> IMapEntry
    abstract find: shift:int * hash:int * key:obj * notFound:obj -> obj
    abstract getNodeSeq: unit -> ISeq

    abstract assoc: edit:AtomicReference<Thread>
                    * shift:int
                    * hash:int
                    * key:obj
                    * value:obj
                    * addedLeaf:SillyBox
                    -> INode

    abstract without: edit:AtomicReference<Thread> * shift:int * hash:int * key:obj * removedLeaf:SillyBox -> INode
    abstract kvReduce: fn:IFn * init:obj -> obj
    abstract fold: combinef:IFn * reducef:IFn * fjtask:IFn * fjfork:IFn * fjjoin:IFn -> obj
    abstract iterator: d:KVMangleFn<obj> -> IEnumerator
    abstract iteratorT: d:KVMangleFn<'T> -> IEnumerator<'T>

module private INodeOps =

    // INode[] manipulation

    let cloneAndSet (arr: 'T [], i: int, a: 'T): 'T [] =
        let clone: 'T [] = downcast arr.Clone()
        clone.[i] <- a
        clone


    let cloneAndSet2 (arr: 'T [], i: int, a: 'T, j: int, b: 'T): 'T [] =
        let clone: 'T [] = downcast arr.Clone()
        clone.[i] <- a
        clone.[j] <- b
        clone

    let removePair (arr: 'T [], i: int): 'T [] =
        let newArr: 'T [] = Array.zeroCreate <| arr.Length - 2
        Array.Copy(arr, 0, newArr, 0, 2 * i)
        Array.Copy(arr, 2 * (i + 1), newArr, 2 * i, newArr.Length - 2 * i)
        newArr

    // Random goodness

    let hash (k) = Util.hasheq (k)
    let bitPos (hash, shift) = 1 <<< Util.mask (hash, shift)
    let bitIndex (bitmap, bit) = Util.bitCount (bitmap &&& (bit - 1)) // Not used?

    let findIndex (key: obj, items: obj [], count: int): int =
        seq { 0 .. 2 .. 2 * count - 1 }
        |> Seq.tryFindIndex (fun i -> Util.equiv (key, items.[i]))
        |> Option.defaultValue -1


open INodeOps

module private NodeIter =

    let getEnumerator (array: obj [], d: KVMangleFn<obj>): IEnumerator =
        let s =
            seq {
                for i in 0 .. 2 .. array.Length - 1 do
                    let key = array.[i]
                    let nodeOrVal = array.[i + 1]

                    if not (isNull key) then
                        yield d (key, nodeOrVal)
                    elif not (isNull nodeOrVal) then
                        let ie = (nodeOrVal :?> INode).iterator(d)

                        while ie.MoveNext() do
                            yield ie.Current

            }
        s.GetEnumerator() :> IEnumerator

    let getEnumeratorT (array: obj [], d: KVMangleFn<'T>): IEnumerator<'T> =
        let s =
            seq {
                for i in 0 .. 2 .. array.Length - 1 do
                    let key = array.[i]
                    let nodeOrVal = array.[i + 1]

                    if not (isNull key) then
                        yield d (key, nodeOrVal)
                    elif not (isNull nodeOrVal) then
                        let ie = (nodeOrVal :?> INode).iteratorT(d)

                        while ie.MoveNext() do
                            yield ie.Current

            }

        s.GetEnumerator()

// TODO: figure out why passing a simple object does not work for comparison via either <> or Object.ReferenceEquals
type private NotFoundSentinel = { Name: string }

[<AllowNullLiteral>]
type PersistentHashMap(meta: IPersistentMap, count: int, root: INode, hasNull: bool, nullValue: obj) =
    inherit APersistentMap()

    new(count, root, hasNull, nullValue) = PersistentHashMap(null, count, root, hasNull, nullValue)

    member internal _.Meta = meta
    member internal _.Count = count
    member internal _.Root = root
    member internal _.HasNull = hasNull
    member internal _.NullValue = nullValue

    static member Empty =
        PersistentHashMap(null, 0, null, false, null)

    static member private notFoundValue = { NotFoundSentinel.Name = "abc" }

    // factories

    static member create(other: IDictionary): IPersistentMap =
        let mutable ret =
            (PersistentHashMap.Empty :> IEditableCollection)
                .asTransient() :?> ITransientMap

        for o in other do
            let de = o :?> DictionaryEntry
            ret <- ret.assoc (de.Key, de.Value)

        ret.persistent ()

    static member create([<ParamArray>] init: obj []): PersistentHashMap =
        let mutable ret =
            (PersistentHashMap.Empty :> IEditableCollection)
                .asTransient() :?> ITransientMap

        for i in 0 .. 2 .. init.Length - 1 do
            ret <- ret.assoc (init.[i], init.[i + 1])
        downcast ret.persistent ()

    static member createWithCheck([<ParamArray>] init: obj []): PersistentHashMap =
        let mutable ret =
            (PersistentHashMap.Empty :> IEditableCollection)
                .asTransient() :?> ITransientMap

        for i in 0 .. 2 .. init.Length - 1 do
            ret <- ret.assoc (init.[i], init.[i + 1])

            if ret.count () <> i / 2 + 1 then
                raise
                <| ArgumentException("init", "Duplicate key: " + init.[i].ToString())
        downcast ret.persistent ()

    static member create1(init: IList): PersistentHashMap =
        let mutable ret =
            (PersistentHashMap.Empty :> IEditableCollection)
                .asTransient() :?> ITransientMap

        let ie = init.GetEnumerator()

        while ie.MoveNext() do
            let key = ie.Current

            if not (ie.MoveNext()) then
                raise
                <| ArgumentException("init", "No value supplied for " + key.ToString())

            let value = ie.Current
            ret <- ret.assoc (key, value)
        downcast ret.persistent ()

    static member createWithCheck(items: ISeq): PersistentHashMap =
        let mutable ret =
            (PersistentHashMap.Empty :> IEditableCollection)
                .asTransient() :?> ITransientMap

        let rec step (i: int) (s: ISeq) =
            if not (isNull s) then
                if isNull (s.next ()) then
                    raise
                    <| ArgumentException
                        ("items",
                         "No value supplied for key: "
                         + items.first().ToString())

                ret <- ret.assoc (items.first (), RT.second (items))

                if ret.count () <> i + 1 then
                    raise
                    <| ArgumentException("items", "Duplicate key: " + items.first().ToString())

                step (i + 1) (s.next().next())

        step 0 items
        downcast ret.persistent ()

    static member create(items: ISeq): PersistentHashMap =
        let mutable ret =
            (PersistentHashMap.Empty :> IEditableCollection)
                .asTransient() :?> ITransientMap

        let rec step (s: ISeq) =
            if not (isNull s) then
                if isNull (s.next ()) then
                    raise
                    <| ArgumentException
                        ("items",
                         "No value supplied for key: "
                         + items.first().ToString())

                ret <- ret.assoc (s.first (), RT.second (s))
                step (s.next().next())

        step items
        downcast ret.persistent ()


    static member create(meta: IPersistentMap, [<ParamArray>] init: obj []): PersistentHashMap =
        (PersistentHashMap.create (init) :> IObj)
            .withMeta(meta) :?> PersistentHashMap


    interface IMeta with
        override _.meta() = meta

    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(m, meta)
            then upcast this
            else upcast PersistentHashMap(m, count, root, hasNull, nullValue)

    interface Counted with
        override _.count() = count

    interface ILookup with
        override this.valAt(k) = (this :> ILookup).valAt(k, null)

        override _.valAt(k, nf) =
            if isNull k then if hasNull then nullValue else nf
            elif isNull root then null
            else root.find (0, hash (k), k, nf)

    interface Associative with
        override _.containsKey(k) =
            if isNull k then
                hasNull
            else
                (not (isNull root))
                && root.find (0, hash (k), k, PersistentHashMap.notFoundValue)
                   <> (upcast PersistentHashMap.notFoundValue)

        override _.entryAt(k) =
            if isNull k
            then if hasNull then upcast MapEntry.create (null, nullValue) else null
            elif isNull root
            then null
            else root.find (0, hash (k), k)


    interface Seqable with
        override _.seq() =
            let s =
                if isNull root then null else root.getNodeSeq ()

            if hasNull
            then upcast Cons(MapEntry.create (null, nullValue), s)
            else s


    interface IPersistentCollection with
        override _.count() = count

        override _.empty() =
            (PersistentHashMap.Empty :> IObj).withMeta(meta) :?> IPersistentCollection


    interface IPersistentMap with
        override this.assoc(k, v) =
            if isNull k then
                if hasNull && v = nullValue
                then upcast this
                else upcast PersistentHashMap(meta, (if hasNull then count else count + 1), root, true, v)
            else
                let addedLeaf = SillyBox()

                let rootToUse: INode =
                    if isNull root then upcast BitmapIndexedNode.Empty else root

                let newRoot =
                    rootToUse.assoc (0, hash (k), k, v, addedLeaf)

                if newRoot = root then
                    upcast this
                else
                    upcast PersistentHashMap
                               (meta, (if addedLeaf.isSet then count + 1 else count), newRoot, hasNull, nullValue)

        override this.assocEx(k, v) =
            if (this :> Associative).containsKey(k) then
                raise
                <| InvalidOperationException("Key already present")

            (this :> IPersistentMap).assoc(k, v)

        override this.without(k) =
            if isNull k then
                if hasNull
                then upcast PersistentHashMap(meta, count - 1, root, false, null)
                else upcast this
            elif isNull root then
                upcast this
            else
                let newRoot = root.without (0, hash (k), k)

                if newRoot = root
                then upcast this
                else upcast PersistentHashMap(meta, count - 1, newRoot, hasNull, nullValue)


    interface IEditableCollection with
        member this.asTransient() = upcast TransientHashMap(this)

    static member emptyEnumerator() = Seq.empty.GetEnumerator()

    static member nullEnumerator(d: KVMangleFn<obj>, nullValue: obj, root: IEnumerator) =
        let s =
            seq {
                yield d (null, nullValue)

                while root.MoveNext() do
                    yield root.Current
            }

        s.GetEnumerator()

    static member nullEnumeratorT<'T>(d: KVMangleFn<'T>, nullValue: obj, root: IEnumerator<'T>) =
        let s =
            seq {
                yield d (null, nullValue)

                while root.MoveNext() do
                    yield root.Current
            }

        s.GetEnumerator()

    member _.MakeEnumerator(d: KVMangleFn<Object>): IEnumerator =
        let rootIter =
            if isNull root then PersistentHashMap.emptyEnumerator () else root.iteratorT (d)

        if hasNull
        then upcast PersistentHashMap.nullEnumerator (d, nullValue, rootIter)
        else upcast rootIter

    member _.MakeEnumeratorT<'T>(d: KVMangleFn<'T>) =
        let rootIter =
            if isNull root then PersistentHashMap.emptyEnumerator () else root.iteratorT (d)

        if hasNull
        then PersistentHashMap.nullEnumeratorT (d, nullValue, rootIter)
        else rootIter

    interface IMapEnumerable with
        member this.keyEnumerator() = this.MakeEnumerator(fun (k, v) -> k)
        member this.valEnumerator() = this.MakeEnumerator(fun (k, v) -> v)


    interface IEnumerable<IMapEntry> with
        member this.GetEnumerator() =
            this.MakeEnumeratorT<IMapEntry>(fun (k, v) -> upcast MapEntry.create (k, v))

    interface IEnumerable with
        member this.GetEnumerator() =
            this.MakeEnumerator(fun (k, v) -> upcast MapEntry.create (k, v))

    interface IKVReduce with
        member _.kvreduce(f, init) =
            let init =
                if hasNull then f.invoke (init, null, nullValue) else init

            match init with // in original, call to RT.isReduced
            | :? Reduced as r -> (r :> IDeref).deref()
            | _ ->
                if not (isNull root) then
                    match root.kvReduce (f, init) with // in original, call to RT.isReduced
                    | :? Reduced as r -> (r :> IDeref).deref()
                    | r -> r
                else
                    init

    member _.fold(n: int64, combinef: IFn, reducef: IFn, fjinvoke: IFn, fjtask: IFn, fjfork: IFn, fjjoin: IFn): obj =
        // JVM: we are ignoreing n for now
        let top: Func<obj> =
            Func<obj>
                ((fun () ->
                    let mutable ret = combinef.invoke ()

                    if not (isNull root)
                    then ret <- combinef.invoke (ret, root.fold (combinef, reducef, fjtask, fjfork, fjjoin))

                    if hasNull
                    then combinef.invoke (ret, reducef.invoke (combinef.invoke (), null, nullValue))
                    else ret))

        fjinvoke.invoke (top)

    static member internal createNode(shift: int, key1: obj, val1: obj, key2hash: int, key2: obj, val2: obj): INode =
        let key1hash = hash (key1)

        if key1hash = key2hash then
            upcast HashCollisionNode(null, key1hash, 2, [| key1; val1; key2; val2 |])
        else
            let box = SillyBox()
            let edit = AtomicReference<Thread>()

            (BitmapIndexedNode.Empty :> INode)
                .assoc(edit, shift, key1hash, key1, val1, box)
                .assoc(edit, shift, key2hash, key2, val2, box)

    static member internal createNode(edit: AtomicReference<Thread>,
                                      shift: int,
                                      key1: obj,
                                      val1: obj,
                                      key2hash: int,
                                      key2: obj,
                                      val2: obj)
                                      : INode =
        let key1hash = hash (key1)

        if key1hash = key2hash then
            upcast HashCollisionNode(null, key1hash, 2, [| key1; val1; key2; val2 |])
        else
            let box = SillyBox()

            (BitmapIndexedNode.Empty :> INode)
                .assoc(edit, shift, key1hash, key1, val1, box)
                .assoc(edit, shift, key2hash, key2, val2, box)



and private TransientHashMap(e, r, c, hn, nv) =
    inherit ATransientMap()

    [<NonSerialized>]
    let edit: AtomicReference<Thread> = e

    [<VolatileField>]
    let mutable root: INode = r

    [<VolatileField>]
    let mutable count: int = c

    [<VolatileField>]
    let mutable hasNull: bool = hn

    [<VolatileField>]
    let mutable nullValue: obj = nv

    let leafFlag: SillyBox = SillyBox()

    new(m: PersistentHashMap) =
        TransientHashMap(AtomicReference(Thread.CurrentThread), m.Root, m.Count, m.HasNull, m.NullValue)

    override this.doAssoc(k, v) =
        if isNull k then
            if nullValue <> v then nullValue <- v

            if not hasNull then
                count <- count + 1
                hasNull <- true
        else
            leafFlag.reset ()

            let n =
                (if isNull root then (BitmapIndexedNode.Empty :> INode) else root)
                    .assoc(edit, 0, hash (k), k, v, leafFlag)

            if n <> root then root <- n

            if leafFlag.isSet then count <- count + 1
        upcast this

    override this.doWithout(k) =
        if isNull k then
            if hasNull then
                hasNull <- false
                nullValue <- null
                count <- count - 1
        elif not (isNull root) then
            leafFlag.reset ()

            let n =
                root.without (edit, 0, hash (k), k, leafFlag)

            if n <> root then root <- n

            if leafFlag.isSet then count <- count - 1

        upcast this

    override _.doCount() = count

    override _.doPersistent() =
        edit.Set(null)
        upcast PersistentHashMap(count, root, hasNull, nullValue)

    override _.doValAt(k, nf) =
        if isNull k then if hasNull then nullValue else nf
        elif isNull root then nf
        else root.find (0, hash (k), k, nf)

    override _.ensureEditable() =
        if edit.Get() |> isNull then
            raise
            <| InvalidOperationException("Transient used after persistent! call")



and [<Sealed>] private ArrayNode(e, c, a) =
    let mutable count: int = c
    let array: INode [] = a

    [<NonSerialized>]
    let edit: AtomicReference<Thread> = e

    member private _.setNode(i, n) = array.[i] <- n
    member private _.incrementCount() = count <- count + 1
    member private _.decrementCount() = count <- count - 1

    // TODO: Do this with some sequence functions?
    member _.pack(edit: AtomicReference<Thread>, idx): INode =
        let newArray: obj [] = Array.zeroCreate <| 2 * (count - 1)
        let mutable j = 1
        let mutable bitmap = 0
        for i = 0 to idx - 1 do
            if not (isNull array.[i]) then
                newArray.[j] <- upcast array.[i]
                bitmap <- bitmap ||| 1 <<< i
                j <- j + 2
        for i = idx + 1 to array.Length - 1 do
            if not (isNull array.[i]) then
                newArray.[j] <- upcast array.[i]
                bitmap <- bitmap ||| 1 <<< i
                j <- j + 2
        upcast BitmapIndexedNode(edit, bitmap, newArray)


    member this.ensureEditable(e) =
        if edit = e
        then this
        else ArrayNode(e, count, array.Clone() :?> INode [])

    member this.editAndSet(e, i, n) =
        let editable = this.ensureEditable (e)
        editable.setNode (i, n)
        editable


    interface INode with
        member this.assoc(shift, hash, key, value, addedLeaf) =
            let idx = Util.mask (hash, shift)
            let node = array.[idx]

            if isNull node then
                upcast ArrayNode
                           (null,
                            count + 1,
                            cloneAndSet
                                (array,
                                 idx,
                                 (BitmapIndexedNode.Empty :> INode)
                                     .assoc(shift + 5, hash, key, value, addedLeaf)))
            else
                let n =
                    node.assoc (shift + 5, hash, key, value, addedLeaf)

                if n = node
                then upcast this
                else upcast ArrayNode(null, count, cloneAndSet (array, idx, n))

        member this.without(shift, hash, key) =
            let idx = Util.mask (hash, shift)
            let node = array.[idx]

            if isNull node then
                upcast this
            else
                let n = node.without (shift + 5, hash, key)

                if n = node then
                    upcast this
                elif isNull n then
                    if count <= 8 then // shrink
                        this.pack (null, idx)
                    else
                        upcast ArrayNode(null, count - 1, cloneAndSet (array, idx, n))
                else
                    upcast ArrayNode(null, count, cloneAndSet (array, idx, n))

        member _.find(shift, hash, key) =
            let idx = Util.mask (hash, shift)
            let node = array.[idx]

            match node with
            | null -> null
            | _ -> node.find (shift + 5, hash, key)

        member _.find(shift, hash, key, nf) =
            let idx = Util.mask (hash, shift)
            let node = array.[idx]

            match node with
            | null -> nf
            | _ -> node.find (shift + 5, hash, key, nf)

        member _.getNodeSeq() = ArrayNodeSeq.create (array)

        member this.assoc(e, shift, hash, key, value, addedLeaf) =
            let idx = Util.mask (hash, shift)
            let node = array.[idx]

            if isNull node then
                let editable =
                    this.editAndSet
                        (edit,
                         idx,
                         (BitmapIndexedNode.Empty :> INode)
                             .assoc(e, shift + 5, hash, key, value, addedLeaf))

                editable.incrementCount ()
                upcast editable
            else
                let n =
                    node.assoc (e, shift + 5, hash, key, value, addedLeaf)

                if n = node then upcast this else upcast this.editAndSet (e, idx, n)

        member this.without(e, shift, hash, key, removedLeaf) =
            let idx = Util.mask (hash, shift)
            let node = array.[idx]

            if isNull node then
                upcast this
            else
                let n =
                    node.without (e, shift + 5, hash, key, removedLeaf)

                if n = node then
                    upcast this
                elif isNull n then
                    if count <= 8 then // shrink
                        this.pack (e, idx)
                    else
                        let editable = this.editAndSet (e, idx, n)
                        editable.decrementCount ()
                        upcast editable
                else
                    upcast this.editAndSet (e, idx, n)

        member _.kvReduce(f, init) =
            let rec step (i: int) (v: obj) =
                if i >= array.Length then
                    v
                else
                    let n = array.[i]
                    let nextv = n.kvReduce (f, v)

                    if RT.isReduced (nextv) then nextv else step (i + 1) nextv

            step 0 init

        member _.fold(combinef, reducef, fjtask, fjfork, fjjoin) =
            let tasks =
                array
                |> Array.map (fun node -> Func<obj>((fun () -> node.fold (combinef, reducef, fjtask, fjfork, fjjoin))))

            ArrayNode.foldTasks (tasks, combinef, fjtask, fjfork, fjjoin)

        member _.iterator(d) =
            let s =
                seq {
                    for node in array do
                        if not (isNull node) then
                            let ie = node.iterator (d)

                            while ie.MoveNext() do
                                yield ie.Current
                }
            s.GetEnumerator() :> IEnumerator

        member _.iteratorT(d) =
            let s =
                seq {
                    for node in array do
                        if not (isNull node) then
                            let ie = node.iteratorT (d)

                            while ie.MoveNext() do
                                yield ie.Current
                }

            s.GetEnumerator()


    static member foldTasks(tasks: Func<obj> [], combinef: IFn, fjtask: IFn, fjfork: IFn, fjjoin: IFn) =
        match tasks.Length with
        | 0 -> combinef.invoke ()
        | 1 -> tasks.[0].Invoke()
        | _ ->
            let halves = tasks |> Array.splitInto 2

            let fn =
                Func<obj>(fun () -> ArrayNode.foldTasks (halves.[1], combinef, fjtask, fjfork, fjjoin))

            let forked = fjfork.invoke (fjtask.invoke (fn))
            combinef.invoke (ArrayNode.foldTasks (halves.[0], combinef, fjtask, fjfork, fjjoin), fjjoin.invoke (forked))


and private ArrayNodeSeq(meta, nodes: INode [], i: int, s: ISeq) =
    inherit ASeq(meta)

    static member create(meta: IPersistentMap, nodes: INode [], i: int, s: ISeq): ISeq =
        match s with
        | null ->
            let result =
                nodes
                |> Seq.indexed
                |> Seq.skip i
                |> Seq.filter (fun (j, node) -> not (isNull node))
                |> Seq.tryPick (fun (j, node) ->
                    let ns = node.getNodeSeq ()

                    if (isNull ns)
                    then None
                    else ArrayNodeSeq(meta, nodes, j + 1, ns) |> Some)

            match result with
            | Some s -> upcast s
            | None -> null
        | _ -> upcast ArrayNodeSeq(meta, nodes, i, s)

    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(m, (this :> IMeta).meta())
            then upcast this
            else upcast ArrayNodeSeq(m, nodes, i, s)

    interface ISeq with
        member _.first() = s.first ()

        member _.next() =
            ArrayNodeSeq.create (null, nodes, i, s.next ())

    static member create(nodes: INode []) =
        ArrayNodeSeq.create (null, nodes, 0, null)


and [<Sealed; AllowNullLiteral>] internal BitmapIndexedNode(e, b, a) =

    [<NonSerialized>]
    let edit: AtomicReference<Thread> = e

    let mutable bitmap: int = b
    let mutable array: obj [] = a

    static member Empty: BitmapIndexedNode =
        BitmapIndexedNode(null, 0, Array.empty<obj>)

    member _.index(bit: int): int = Util.bitCount (bitmap &&& (bit - 1))

    member x.Bitmap
        with private get () = bitmap
        and private set (v) = bitmap <- v

    member private _.setArrayVal(i, v) = array.[i] <- v

    member _.Array
        with private get () = array
        and private set (v) = array <- v


    interface INode with
        member this.assoc(shift, hash, key, value, addedLeaf) =
            let bit = bitPos (hash, shift)
            let idx = this.index (bit)

            if bitmap &&& bit = 0 then
                let n = Util.bitCount (bitmap)

                if n >= 16 then
                    let nodes: INode [] = Array.zeroCreate 32
                    let jdx = Util.mask (hash, shift)
                    nodes.[jdx] <- (BitmapIndexedNode.Empty :> INode)
                        .assoc(shift + 5, hash, key, value, addedLeaf)

                    let mutable j = 0
                    for i = 0 to 31 do
                        if ((bitmap >>> i) &&& 1) <> 0 then
                            nodes.[i] <- if isNull array.[j] then
                                             array.[j + 1] :?> INode
                                         else
                                             (BitmapIndexedNode.Empty :> INode)
                                                 .assoc(shift + 5,
                                                        INodeOps.hash (array.[j]),
                                                        array.[j],
                                                        array.[j + 1],
                                                        addedLeaf)

                            j <- j + 2
                    upcast ArrayNode(null, n + 1, nodes)
                else
                    let newArray: obj [] = 2 * (n + 1) |> Array.zeroCreate
                    Array.Copy(array, 0, newArray, 0, 2 * idx)
                    newArray.[2 * idx] <- key
                    addedLeaf.set ()
                    newArray.[2 * idx + 1] <- value
                    Array.Copy(array, 2 * idx, newArray, 2 * (idx + 1), 2 * (n - idx))
                    upcast BitmapIndexedNode(null, (bitmap ||| bit), newArray)
            else
                let keyOrNull = array.[2 * idx]
                let valOrNode = array.[2 * idx + 1]

                if isNull keyOrNull then
                    let n =
                        (valOrNode :?> INode)
                            .assoc(shift + 5, hash, key, value, addedLeaf)

                    if n = (valOrNode :?> INode)
                    then upcast this
                    else upcast BitmapIndexedNode(null, bitmap, cloneAndSet (array, 2 * idx + 1, upcast n))
                elif Util.equiv (key, keyOrNull) then
                    if value = valOrNode
                    then upcast this
                    else upcast BitmapIndexedNode(null, bitmap, cloneAndSet (array, 2 * idx + 1, value))
                else
                    addedLeaf.set ()
                    upcast BitmapIndexedNode
                               (null,
                                bitmap,
                                cloneAndSet2
                                    (array,
                                     2 * idx,
                                     null,
                                     2 * idx + 1,
                                     upcast PersistentHashMap.createNode
                                                (shift + 5, keyOrNull, valOrNode, hash, key, value)))

        member this.without(shift, hash, key) =
            let bit = bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                upcast this
            else
                let idx = this.index (bit)
                let keyOrNull = array.[2 * idx]
                let valOrNode = array.[2 * idx + 1]

                if isNull keyOrNull then
                    let n =
                        (valOrNode :?> INode)
                            .without(shift + 5, hash, key)

                    if n = (valOrNode :?> INode) then
                        upcast this
                    elif not (isNull n) then
                        upcast BitmapIndexedNode(null, bitmap, cloneAndSet (array, 2 * idx + 1, upcast n))
                    elif bitmap = bit then
                        null
                    else
                        upcast BitmapIndexedNode(null, bitmap ^^^ bit, removePair (array, idx))
                elif Util.equiv (key, keyOrNull) then
                    if bitmap = bit
                    then null
                    else upcast BitmapIndexedNode(null, bitmap ^^^ bit, removePair (array, idx))
                else
                    upcast this

        member this.find(shift, hash, key) =
            let bit = bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                null
            else
                let idx = this.index (bit)
                let keyOrNull = array.[2 * idx]
                let valOrNode = array.[2 * idx + 1]

                if isNull keyOrNull
                then (valOrNode :?> INode).find(shift + 5, hash, key)
                elif Util.equiv (key, keyOrNull)
                then upcast MapEntry.create (keyOrNull, valOrNode)
                else null

        member this.find(shift, hash, key, nf) =
            let bit = bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                nf
            else
                let idx = this.index (bit)
                let keyOrNull = array.[2 * idx]
                let valOrNode = array.[2 * idx + 1]

                if isNull keyOrNull then
                    (valOrNode :?> INode)
                        .find(shift + 5, hash, key, nf)
                elif Util.equiv (key, keyOrNull) then
                    valOrNode
                else
                    nf

        member _.getNodeSeq() = NodeSeq.create (array)

        member this.assoc(edit, shift, hash, key, value, addedLeaf) =
            let bit = bitPos (hash, shift)
            let idx = this.index (bit)

            if (bitmap &&& bit) <> 0 then
                let keyOrNull = array.[2 * idx]
                let valOrNode = array.[2 * idx + 1]

                if isNull keyOrNull then
                    let n =
                        (valOrNode :?> INode)
                            .assoc(edit, shift + 5, hash, key, value, addedLeaf)

                    if n = (valOrNode :?> INode)
                    then upcast this
                    else upcast this.editAndSet (edit, 2 * idx + 1, n)
                elif Util.equiv (key, keyOrNull) then
                    if value = valOrNode
                    then upcast this
                    else upcast this.editAndSet (edit, 2 * idx + 1, value)
                else
                    addedLeaf.set ()
                    upcast this.editAndSet
                               (edit,
                                2 * idx,
                                null,
                                2 * idx + 1,
                                PersistentHashMap.createNode (edit, shift + 5, keyOrNull, valOrNode, hash, key, value))
            else
                let n = Util.bitCount bitmap

                if n * 2 < array.Length then
                    addedLeaf.set ()
                    let editable = this.ensureEditable (edit)
                    Array.Copy(editable.Array, 2 * idx, editable.Array, 2 * (idx + 1), 2 * (n - idx))
                    editable.setArrayVal (2 * idx, key)
                    editable.setArrayVal (2 * idx + 1, value)
                    editable.Bitmap <- editable.Bitmap ||| bit
                    upcast editable
                elif n >= 16 then
                    let nodes: INode [] = Array.zeroCreate 32
                    let jdx = Util.mask (hash, shift)
                    nodes.[jdx] <- (BitmapIndexedNode.Empty :> INode)
                        .assoc(edit, shift + 5, hash, key, value, addedLeaf)

                    let mutable j = 0
                    for i = 0 to 31 do
                        if ((bitmap >>> i) &&& 1) <> 0 then
                            if isNull array.[j] then
                                nodes.[i] <- array.[j + 1] :?> INode
                            else
                                nodes.[i] <- (BitmapIndexedNode.Empty :> INode)
                                    .assoc(edit,
                                           shift + 5,
                                           INodeOps.hash (array.[j]),
                                           array.[j],
                                           array.[j + 1],
                                           addedLeaf)

                            j <- j + 2
                    upcast ArrayNode(edit, n + 1, nodes)
                else
                    let newArray: obj [] = 2 * (n + 4) |> Array.zeroCreate
                    Array.Copy(array, 0, newArray, 0, 2 * idx)
                    newArray.[2 * idx] <- key
                    newArray.[2 * idx + 1] <- value
                    addedLeaf.set ()
                    Array.Copy(array, 2 * idx, newArray, 2 * (idx + 1), 2 * (n - idx))
                    let editable = this.ensureEditable (edit)
                    editable.Array <- newArray
                    editable.Bitmap <- editable.Bitmap ||| bit
                    upcast editable

        member this.without(e, shift, hash, key, removedLeaf) =
            let bit = bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                upcast this
            else
                let idx = this.index (bit)
                let keyOrNull = array.[2 * idx]
                let valOrNode = array.[2 * idx + 1]

                if isNull keyOrNull then
                    let n =
                        (valOrNode :?> INode)
                            .without(e, shift + 5, hash, key, removedLeaf)

                    if n = (valOrNode :?> INode)
                    then upcast this
                    elif not (isNull n)
                    then upcast this.editAndSet (e, 2 * idx + 1, n)
                    elif bitmap = bit
                    then null
                    else upcast this.editAndRemovePair (e, bit, idx)
                elif Util.equiv (key, keyOrNull) then
                    removedLeaf.set ()
                    upcast this.editAndRemovePair (e, bit, idx)
                else
                    upcast this

        member _.kvReduce(f, init) = NodeSeq.kvReduce (array, f, init)

        member _.fold(combinef, reducef, fjtask, fjfork, fjjoin) =
            NodeSeq.kvReduce (array, reducef, combinef.invoke ())

        member _.iterator(d) = NodeIter.getEnumerator (array, d)

        member _.iteratorT(d) = NodeIter.getEnumeratorT (array, d)

    member this.ensureEditable(e: AtomicReference<Thread>): BitmapIndexedNode =
        if edit = e then
            this
        else
            let n = Util.bitCount (bitmap)

            let newArray: obj [] =
                Array.zeroCreate (if n >= 0 then 2 * (n + 1) else 4) // make room for next assoc

            Array.Copy(array, newArray, 2 * n)
            BitmapIndexedNode(e, bitmap, newArray)

    member private this.editAndSet(e: AtomicReference<Thread>, i: int, a: obj): BitmapIndexedNode =
        let editable = this.ensureEditable (e)
        editable.setArrayVal (i, a)
        editable


    member private this.editAndSet(e: AtomicReference<Thread>, i: int, a: obj, j: int, b: obj): BitmapIndexedNode =
        let editable = this.ensureEditable (e)
        editable.setArrayVal (i, a)
        editable.setArrayVal (j, b)
        editable

    member private this.editAndRemovePair(e: AtomicReference<Thread>, bit: int, i: int): BitmapIndexedNode =
        if bitmap = bit then
            null
        else
            let editable = this.ensureEditable (e)
            editable.Bitmap <- editable.Bitmap ^^^ bit
            Array.Copy(editable.Array, 2 * (i + 1), editable.Array, 2 * i, editable.Array.Length - 2 * (i + 1))
            editable.setArrayVal (editable.Array.Length - 2, null)
            editable.setArrayVal (editable.Array.Length - 1, null)
            editable


and HashCollisionNode(edit: AtomicReference<Thread>, hash: int, c, a) =

    let mutable count: int = c
    let mutable array: obj [] = a

    member _.Array
        with private get () = array
        and private set (a) = array <- a

    member _.Count
        with private get () = count
        and private set (c) = count <- c

    member _.tryFindIndex(key: obj): int option =
        let rec step (i: int) =
            if i >= 2 * count then None
            elif Util.equiv (key, array.[i]) then Some i
            else i + 2 |> step

        step 0


    interface INode with
        member this.assoc(shift, h, key, value, addedLeaf) =
            if h = hash then
                match this.tryFindIndex (key) with
                | Some idx ->
                    if array.[idx + 1] = value
                    then upcast this
                    else upcast HashCollisionNode(null, h, count, cloneAndSet (array, idx + 1, value))
                | None ->
                    let newArray: obj [] = 2 * (count + 1) |> Array.zeroCreate
                    Array.Copy(array, 0, newArray, 0, 2 * count)
                    newArray.[2 * count] <- key
                    newArray.[2 * count + 1] <- value
                    addedLeaf.set ()
                    upcast HashCollisionNode(edit, h, count + 1, newArray)
            else
                (BitmapIndexedNode(null, bitPos (hash, shift), [| null; this |]) :> INode)
                    .assoc(shift, h, key, value, addedLeaf)

        member this.without(shift, h, key) =
            match this.tryFindIndex (key) with
            | None -> upcast this
            | Some idx ->
                if count = 1
                then null
                else upcast HashCollisionNode(null, h, count - 1, removePair (array, idx / 2))

        member this.find(shift, h, key) =
            match this.tryFindIndex (key) with
            | None -> null
            | Some idx -> upcast MapEntry.create (array.[idx], array.[idx + 1])

        member this.find(shift, h, key, nf) =
            match this.tryFindIndex (key) with
            | None -> nf
            | Some idx -> array.[idx + 1]

        member _.getNodeSeq() = NodeSeq.create (array)

        member this.assoc(e, shift, h, key, value, addedLeaf) =
            if h = hash then
                match this.tryFindIndex (key) with
                | Some idx ->
                    if array.[idx + 1] = value
                    then upcast this
                    else upcast this.editAndSet (e, idx + 1, value)
                | None ->
                    if array.Length > 2 * count then
                        addedLeaf.set ()

                        let editable =
                            this.editAndSet (e, 2 * count, key, 2 * count + 1, value)

                        editable.Count <- editable.Count + 1
                        upcast editable
                    else
                        let newArray: obj [] = array.Length + 2 |> Array.zeroCreate
                        Array.Copy(array, 0, newArray, 0, array.Length)
                        newArray.[array.Length] <- key
                        newArray.[array.Length + 1] <- value
                        addedLeaf.set ()
                        upcast this.ensureEditable (e, count + 1, newArray)
            else
                (BitmapIndexedNode(null, bitPos (hash, shift), [| null; this; null; null |]) :> INode)
                    .assoc(e, shift, h, key, value, addedLeaf)

        member this.without(e, shift, h, key, removedLeaf) =
            match this.tryFindIndex (key) with
            | None -> upcast this
            | Some idx ->
                removedLeaf.set ()

                if count = 1 then
                    null
                else
                    let editable = this.ensureEditable (e)
                    editable.Array.[idx] <- editable.Array.[2 * count - 2]
                    editable.Array.[idx + 1] <- editable.Array.[2 * count - 1]
                    editable.Array.[2 * count - 2] <- null
                    editable.Array.[2 * count - 1] <- null
                    editable.Count <- editable.Count - 1
                    upcast editable

        member _.kvReduce(f, init) = NodeSeq.kvReduce (array, f, init)

        member _.fold(combinef, reducef, fjtask, fjfork, fjjoin) =
            NodeSeq.kvReduce (array, reducef, combinef.invoke ())

        member _.iterator(d) = NodeIter.getEnumerator (array, d)

        member _.iteratorT(d) = NodeIter.getEnumeratorT (array, d)


    member this.ensureEditable(e) =
        if e = edit then
            this
        else
            let newArray: obj [] = 2 * (count + 1) |> Array.zeroCreate
            Array.Copy(array, 0, newArray, 0, 2 * count)
            HashCollisionNode(e, hash, count, newArray)

    member this.ensureEditable(e, c, a) =
        if e = edit then
            array <- a
            count <- c
            this
        else
            HashCollisionNode(e, hash, c, a)

    member this.editAndSet(e, i, a) =
        let editable = this.ensureEditable (e)
        editable.Array.[i] <- a
        editable

    member this.editAndSet(e, i, a, j, b) =
        let editable = this.ensureEditable (e)
        editable.Array.[i] <- a
        editable.Array.[j] <- b
        editable




and NodeSeq(meta, array: obj [], idx: int, seq: ISeq) =
    inherit ASeq(meta)

    new(i, a, s) = NodeSeq(null, a, i, s)

    static member private create(array: obj [], i: int, s: ISeq): ISeq =
        if not (isNull s) then
            upcast NodeSeq(null, array, i, s)
        else
            let result =
                array
                |> Seq.indexed
                |> Seq.skip i
                |> Seq.tryPick (fun (j, node) ->

                    if j % 2 = 0 then // even => key entry
                        if not (isNull array.[j]) then NodeSeq(null, array, j, null) |> Some else None
                    else // odd => value entry

                    if isNull array.[j - 1] then
                        let node: INode = array.[j] :?> INode

                        if not (isNull node) then
                            let nodeSeq = node.getNodeSeq ()

                            if not (isNull nodeSeq)
                            then NodeSeq(null, array, j + 1, nodeSeq) |> Some
                            else None
                        else
                            None
                    else
                        None)

            match result with
            | Some ns -> upcast ns
            | None -> null


    static member create(array: obj []): ISeq = NodeSeq.create (array, 0, null)

    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(m, (this :> IMeta).meta())
            then upcast this
            else upcast NodeSeq(m, array, idx, seq)

    interface ISeq with
        member _.first() =
            match seq with
            | null -> upcast MapEntry.create (array.[idx], array.[idx + 1])
            | _ -> seq.first ()

        member _.next() =
            match seq with
            | null -> NodeSeq.create (array, idx + 2, null)
            | _ -> NodeSeq.create (array, idx, seq.next ())

    static member kvReduce(a: obj [], f: IFn, init: obj): obj =
        let rec step (result: obj) (i: int) =
            if i >= a.Length then
                result
            else
                let nextResult =
                    if not (isNull a.[i]) then
                        f.invoke (result, a.[i], a.[i + 1])
                    else
                        let node = a.[i + 1] :?> INode

                        if not (isNull node) then node.kvReduce (f, result) else result

                if RT.isReduced (nextResult) then nextResult else step nextResult (i + 2)

        step init 0
