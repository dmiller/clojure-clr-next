namespace Clojure.Collections.Simple

open Clojure.Collections
open System.Collections
open System.Collections.Generic
open System
open System.Reflection
open System.Threading

type INode3 =
    abstract assoc: shift:int -> hash:int -> key:obj -> value:obj -> addedLeaf:Box -> INode3
    abstract without: shift:int -> hash:int -> key:obj -> INode3 option
    abstract find: shift:int -> hash:int -> key:obj -> IMapEntry option
    abstract find2: shift:int -> hash:int -> key:obj -> notFound:obj -> obj
    abstract getNodeSeq: unit -> ISeq

    abstract assocTransient: edit:AtomicReference<Thread>
     -> shift:int -> hash:int -> key:obj -> value:obj -> addedLeaf:Box -> INode3

    abstract withoutTransient: edit:AtomicReference<Thread>
     -> shift:int -> hash:int -> key:obj -> removedLeaf:Box -> INode3 option

    abstract iterator: d:KVMangleFn<obj> -> IEnumerator
    abstract iteratorT: d:KVMangleFn<'T> -> IEnumerator<'T>

module private INodeOps3 =

    let cloneAndSet (arr: 'T [], i: int, a: 'T): 'T [] =
        let clone: 'T [] = downcast arr.Clone()
        clone.[i] <- a
        clone

    let removeEntry (arr: 'T [], i: int): 'T [] =
        let newArr: 'T [] = Array.zeroCreate <| arr.Length - 1
        Array.Copy(arr, 0, newArr, 0, i)
        Array.Copy(arr, (i + 1), newArr, i, newArr.Length - i)
        newArr

    let getHash (o: obj): int =
        match o with
        | null -> 0
        | _ -> o.GetHashCode()

    let mask (hash, shift) = (hash >>> shift) &&& 0x01f

    let bitPos (hash, shift) = 1 <<< mask (hash, shift)

    let bitCount (x) =
        let x = x - ((x >>> 1) &&& 0x55555555)

        let x =
            (((x >>> 2) &&& 0x33333333) + (x &&& 0x33333333))

        let x = (((x >>> 4) + x) &&& 0x0f0f0f0f)
        (x * 0x01010101) >>> 24

    let bitIndex (bitmap, bit) = bitCount (bitmap &&& (bit - 1))

    let hashToIndex (hash: int) (shift: int) (bitmap: int): int option =
        let bit = bitPos (hash, shift)

        if bit &&& bitmap = 0 then None else bitIndex (bitmap, bit) |> Some

    let pcequiv (k1: obj, k2: obj) =
        match k1, k2 with
        | :? IPersistentCollection as pc1, _ -> pc1.equiv (k2)
        | _, (:? IPersistentCollection as pc2) -> pc2.equiv (k1)
        | _ -> k1.Equals(k2)

    let equiv (k1: obj, k2: obj) =
        if Object.ReferenceEquals(k1, k2) then true
        elif isNull k1 then false
        else pcequiv (k1, k2)

open INodeOps3


// pick these up from original SimpleHashMap

//type Box(init) =
//    let mutable value: bool = init
//    new() = Box(false)

//    member _.set() = value <- true
//    member _.reset() = value <- false
//    member _.isSet = value
//    member _.isNotSet = not value

//type KVTranformFn<'T> = obj * obj -> 'T


type NotFoundSentinel3 = | NFS3

type SimpleHashMap3 =
    | EmptyMap
    | Rooted of Count: int * Node: INode3

    static member notFoundValue = NFS3

    static member createNode (shift: int) (key1: obj) (val1: obj) (key2hash: int) (key2: obj) (val2: obj): INode3 =
        let key1hash = hash (key1)

        if key1hash = key2hash then
            CollisionNode3
                (null,
                 key1hash,
                 2,
                 [| MapEntry(key1, val1)
                    MapEntry(key2, val2) |])
        else
            let box = Box()

            let n1 =
                (BitmapNode3.Empty :> INode3).assoc shift key1hash key1 val1 box

            n1.assoc shift key2hash key2 val2 box

    interface Counted with
        member this.count() =
            match this with
            | EmptyMap -> 0
            | Rooted(Count = c) -> c

    interface Seqable with
        member this.seq() =
            match this with
            | EmptyMap -> null
            | Rooted(Node = n) -> n.getNodeSeq ()

    interface IPersistentCollection with
        member this.count() = (this :> Counted).count()

        member this.cons(o) = upcast (this :> IPersistentMap).cons(o)

        member _.empty() = upcast EmptyMap

        member this.equiv(o) =
            match o with
            | :? IDictionary as d ->
                if d.Count <> (this :> IPersistentCollection).count() then
                    false
                else
                    let rec step (s: ISeq) =
                        if isNull s then
                            true
                        else
                            let me: IMapEntry = downcast s.first ()

                            if d.Contains(me.key ())
                               && Util.equiv (me.value (), d.[me.key ()]) then
                                step (s.next ())
                            else
                                false

                    step ((this :> Seqable).seq())
            | _ -> false

    interface ILookup with
        member this.valAt(k) = (this :> ILookup).valAt(k, null)

        member this.valAt(k, nf) =
            match this with
            | EmptyMap -> nf
            | Rooted(Node = n) -> n.find2 0 (hash k) k nf

    interface Associative with
        member this.containsKey(k) =
            match this with
            | EmptyMap -> false
            | Rooted(Node = n) ->
                (n.find2 0 (hash k) k SimpleHashMap3.notFoundValue)
                <> (upcast SimpleHashMap3.notFoundValue)

        member this.entryAt(k) =
            match this with
            | EmptyMap -> null
            | Rooted(Node = n) ->
                match n.find 0 (hash k) k with
                | None -> null
                | Some me -> me

        member this.assoc(k, v) =
            upcast (this :> IPersistentMap).assoc(k, v)

    interface IPersistentMap with
        member this.count() = (this :> Counted).count()

        member this.assocEx(k, v) =
            if (this :> Associative).containsKey(k) then
                raise
                <| InvalidOperationException("Key already present")

            (this :> IPersistentMap).assoc(k, v)

        member this.cons(o) =
            match o with
            | null -> upcast this
            | :? IMapEntry as e ->
                (this :> IPersistentMap)
                    .assoc(e.key (), e.value ())
            | :? DictionaryEntry as e -> (this :> IPersistentMap).assoc(e.Key, e.Value)
            | _ when o.GetType().IsGenericType
                     && o.GetType().Name = "KeyValuePair`2" ->
                let t = o.GetType()

                let k =
                    t.InvokeMember("Key", BindingFlags.GetProperty, null, o, null)

                let v =
                    t.InvokeMember("Value", BindingFlags.GetProperty, null, o, null)

                (this :> IPersistentMap).assoc(k, v)
            | :? IPersistentVector as v ->
                if v.count () = 2 then
                    (this :> IPersistentMap)
                        .assoc(v.nth (0), v.nth (1))
                else
                    raise
                    <| ArgumentException("o", "Vector arg to map cons must be a pair")
            | _ ->
                let rec step (s: ISeq) (m: IPersistentMap) =
                    if isNull s then
                        m
                    else
                        let me = s.first () :?> IMapEntry
                        step (s.next ()) (m.assoc (me.key (), me.value ()))

                step (RT.seq (o)) this

        member this.assoc(k, v) =
            let addedLeaf = Box()

            let rootToUse: INode3 =
                match this with
                | EmptyMap -> BitmapNode3.Empty
                | Rooted(Node = n) -> n

            let newRoot = rootToUse.assoc 0 (hash k) k v addedLeaf

            if newRoot = rootToUse then
                upcast this
            else
                let count = (this :> Counted).count()

                let updatedCount =
                    if addedLeaf.isSet then count + 1 else count
                upcast Rooted(updatedCount, newRoot)

        member this.without(k) =
            match this with
            | EmptyMap -> upcast this
            | Rooted (Count = c; Node = n) ->
                match n.without 0 (hash k) k with
                | None -> EmptyMap
                | Some newRoot ->
                    if newRoot = n then upcast this
                    elif c = 1 then upcast EmptyMap
                    else upcast Rooted(c - 1, newRoot)

    member this.MakeEnumerator(d: KVTranformFn<Object>): IEnumerator =
        match this with
        | EmptyMap -> upcast Seq.empty.GetEnumerator()
        | Rooted(Node = n) -> upcast n.iteratorT (d)

    member this.MakeEnumeratorT<'T>(d: KVTranformFn<'T>): IEnumerator<'T> =
        match this with
        | EmptyMap -> Seq.empty.GetEnumerator()
        | Rooted(Node = n) -> n.iteratorT (d)

    interface IEnumerable<IMapEntry> with
        member this.GetEnumerator() =
            this.MakeEnumeratorT<IMapEntry>(fun (k, v) -> upcast MapEntry.create (k, v))

    interface IEnumerable with
        member this.GetEnumerator() =
            this.MakeEnumerator(fun (k, v) -> upcast MapEntry.create (k, v))

    interface IMapEnumerable with
        member this.keyEnumerator() = this.MakeEnumerator(fun (k, v) -> k)
        member this.valEnumerator() = this.MakeEnumerator(fun (k, v) -> v)

    //interface IFn with
    //    override this.invoke(arg1) = (this:>ILookup).valAt(arg1)
    //    override this.invoke(arg1,arg2) = (this:>ILookup).valAt(arg1,arg2)

    interface IEditableCollection with
        member this.asTransient() =
            upcast TransientSimpleHashMap3.create (this)

    static member create(other: IDictionary): IPersistentMap =
        let mutable ret =
            (SimpleHashMap3.EmptyMap :> IEditableCollection)
                .asTransient() :?> ITransientMap

        for o in other do
            let de = o :?> DictionaryEntry
            ret <- ret.assoc (de.Key, de.Value)

        ret.persistent ()

    static member create1(init: IList): IPersistentMap =
        let mutable ret =
            (SimpleHashMap3.EmptyMap :> IEditableCollection)
                .asTransient() :?> ITransientMap

        let ie = init.GetEnumerator()

        while ie.MoveNext() do
            let key = ie.Current

            if not (ie.MoveNext()) then
                raise
                <| ArgumentException("init", "No value supplied for " + key.ToString())

            let value = ie.Current
            ret <- ret.assoc (key, value)

        ret.persistent ()

and [<Sealed>] private TransientSimpleHashMap3(e, r, c) =
    inherit ATransientMap()

    [<NonSerialized>]
    let edit: AtomicReference<Thread> = e

    [<VolatileField>]
    let mutable root: INode3 option = r

    [<VolatileField>]
    let mutable count: int = c

    let leafFlag = Box()

    static member create(m: SimpleHashMap3) =
        match m with
        | EmptyMap -> TransientSimpleHashMap3(AtomicReference(Thread.CurrentThread), None, 0)
        | Rooted (Count = c; Node = n) -> TransientSimpleHashMap3(AtomicReference(Thread.CurrentThread), Some n, c)

    override this.doAssoc(k, v) =
        leafFlag.reset ()

        match root with
        | None -> root <- Some((BitmapNode3.Empty :> INode3).assocTransient edit 0 (hash k) k v leafFlag)
        | Some currNode ->
            let newNode =
                currNode.assocTransient edit 0 (hash k) k v leafFlag

            if newNode <> currNode then root <- Some newNode

        if leafFlag.isSet then count <- count + 1
        upcast this

    override this.doWithout(k) =
        leafFlag.reset ()

        match root with
        | None -> ()
        | Some currNode ->
            let newNode =
                currNode.withoutTransient edit 0 (hash k) k leafFlag

            match newNode with
            | None -> root <- None
            | Some node -> if node <> currNode then root <- newNode

            if leafFlag.isSet then count <- count - 1

        this

    override _.doCount() = count

    override _.doPersistent() =
        edit.Set(null)

        match root with
        | None -> EmptyMap
        | Some currNode -> Rooted(count, currNode)

    override _.doValAt(k, nf) =
        match root with
        | None -> nf
        | Some currNode -> currNode.find2 0 (hash k) k nf

    override _.ensureEditable() =
        if edit.Get() |> isNull then
            raise
            <| InvalidOperationException("Transient used after persistent! call")



and [<Sealed>] ArrayNode3(e, c, a) =
    let mutable count: int = c
    let nodes: INode3 option [] = a

    [<NonSerialized>]
    let edit: AtomicReference<Thread> = e

    member this.ensureEditable(e) =
        if edit = e
        then this
        else ArrayNode3(e, count, downcast nodes.Clone() )

    member this.editAndSet(e, i, n) =
        let editable = this.ensureEditable (e)
        nodes.[i] <- n
        editable

    member _.incrementCount() = count <- count + 1
    member _.decrementCount() = count <- count - 1

    static member pack (edit: AtomicReference<Thread>) (count: int) (nodes: INode3 option []) (idx: int): INode3 =
        let newArray: BNodeEntry3 [] = count - 1 |> Array.zeroCreate
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

        BitmapNode3(edit, bitmap, newArray)

    interface INode3 with

        member this.find shift hash key =
            let idx = mask (hash, shift)

            match nodes.[idx] with
            | None -> None
            | Some node -> node.find (shift + 5) hash key

        member this.find2 shift hash key notFound =
            let idx = mask (hash, shift)

            match nodes.[idx] with
            | None -> notFound
            | Some node -> node.find2 (shift + 5) hash key notFound


        member this.assoc shift hash key value addedLeaf =
            let idx = mask (hash, shift)

            match nodes.[idx] with
            | None ->
                let newNode =
                    (BitmapNode3.Empty :> INode3).assoc (shift + 5) hash key value addedLeaf

                ArrayNode3(null, count + 1, cloneAndSet (nodes, idx, Some newNode))
            | Some node ->
                let newNode =
                    node.assoc (shift + 5) hash key value addedLeaf

                if newNode = node
                then this
                else ArrayNode3(null, count, cloneAndSet (nodes, idx, Some newNode))

        member this.without shift hash key =
            let idx = mask (hash, shift)

            match nodes.[idx] with
            | None -> (this :> INode3) |> Some
            | Some node ->
                match node.without (shift + 5) hash key with
                | None -> // this branch got deleted
                    if count <= 8 then
                        ArrayNode3.pack null count nodes idx |> Some // shrink
                    else
                        ArrayNode3(null, count - 1, cloneAndSet (nodes, idx, None)) :> INode3
                        |> Some // zero out this entry
                | Some newNode ->
                    if newNode = node then
                        this :> INode3 |> Some
                    else
                        ArrayNode3(null, count - 1, cloneAndSet (nodes, idx, Some newNode)) :> INode3
                        |> Some

        member this.assocTransient e shift hash key value addedLeaf =
            let idx = mask (hash, shift)

            match nodes.[idx] with
            | None ->
                let editable =
                    this.editAndSet
                        (e,
                         idx,
                         (BitmapNode3.Empty :> INode3).assocTransient e (shift + 5) hash key value addedLeaf
                         |> Some)

                editable.incrementCount ()
                editable

            | Some node ->
                let newNode =
                    node.assocTransient e (shift + 5) hash key value addedLeaf

                if newNode = node
                then this
                else this.editAndSet (e, idx, newNode |> Some)


        member this.withoutTransient e shift hash key removedLeaf =
            let idx = mask (hash, shift)

            match nodes.[idx] with
            | None -> this :> INode3 |> Some
            | Some node ->
                match node.withoutTransient e (shift + 5) hash key removedLeaf with
                | None -> // this branch got deleted
                    if count <= 8 then
                        ArrayNode3.pack e count nodes idx |> Some // shrink
                    else
                        let editable = this.editAndSet (e, idx, None)
                        editable.decrementCount ()
                        editable :> INode3 |> Some

                | Some newNode ->
                    if newNode = node then
                        this :> INode3 |> Some
                    else
                        let editable =
                            this.editAndSet (e, idx, newNode |> Some)

                        editable.decrementCount ()
                        editable :> INode3 |> Some


        member this.getNodeSeq() = ArrayNode3Seq.create (nodes, 0)

        member this.iterator d =
            let s =
                seq {
                    for onode in nodes do
                        match onode with
                        | None -> ()
                        | Some node ->
                            let ie = node.iteratorT (d)

                            while ie.MoveNext() do
                                yield ie.Current
                }

            s.GetEnumerator()

        member this.iteratorT d =
            let s =
                seq {
                    for onode in nodes do
                        match onode with
                        | None -> ()
                        | Some node ->
                            let ie = node.iteratorT (d)

                            while ie.MoveNext() do
                                yield ie.Current
                }

            s.GetEnumerator()

and BNodeEntry3 =
    | KeyValue of Key: obj * Value: obj
    | Node of Node: INode3
    | EmptyEntry

and [<Sealed>] internal BitmapNode3(e, b, a) =

    [<NonSerialized>]
    let edit: AtomicReference<Thread> = e

    let mutable bitmap: int = b
    let mutable entries: BNodeEntry3 [] = a

    static member Empty: BitmapNode3 =
        BitmapNode3(null, 0, Array.empty<BNodeEntry3>)

    member this.modifyOrCreateBNode (newEdit: AtomicReference<Thread>) (newBitmap: int) (newEntries: BNodeEntry3 []) =
        if edit = newEdit then
            // Current node is editable -- modify in-place
            bitmap <- newBitmap
            entries <- newEntries
            this
        else
            // create new editable node with correct data
            BitmapNode3(newEdit, newBitmap, newEntries)


    interface INode3 with

        member this.assoc shift hash key value addedLeaf =
            match hashToIndex hash shift bitmap with
            | None ->
                let n = bitCount (bitmap)

                if n >= 16 then
                    let nodes: INode3 option [] = Array.zeroCreate 32
                    let jdx = mask (hash, shift)
                    nodes.[jdx] <- (BitmapNode3.Empty :> INode3).assoc (shift + 5) hash key value addedLeaf
                                   |> Some

                    let mutable j = 0
                    for i = 0 to 31 do
                        if ((bitmap >>> i) &&& 1) <> 0 then
                            nodes.[i] <- match entries.[j] with
                                         | KeyValue (Key = k; Value = v) ->
                                             (BitmapNode3.Empty :> INode3).assoc (shift + 5) (getHash k) k v addedLeaf
                                             |> Some
                                         | Node(Node = node) -> node |> Some
                                         | EmptyEntry ->
                                             InvalidOperationException
                                                 ("Found Empty cell in BitmapNode3 -- algorithm bug")
                                             |> raise

                            j <- j + 1

                    ArrayNode3(null, n + 1, nodes)

                else
                    let bit = bitPos (hash, shift)
                    let idx = bitIndex (bitmap, bit)
                    let newArray: BNodeEntry3 [] = Array.zeroCreate (n + 1)
                    Array.Copy(entries, 0, newArray, 0, idx)
                    newArray.[idx] <- KeyValue(key, value)
                    Array.Copy(entries, idx, newArray, idx + 1, n - idx)
                    addedLeaf.set ()
                    BitmapNode3(null, bitmap ||| bit, newArray)

            | Some idx ->
                let entry = entries.[idx]

                match entry with
                | KeyValue (Key = k; Value = v) ->
                    if equiv (key, k) then
                        if value = v
                        then this
                        else BitmapNode3(null, bitmap, cloneAndSet (entries, idx, KeyValue(key, value)))
                    else
                        addedLeaf.set ()

                        let newNode =
                            SimpleHashMap3.createNode (shift + 5) k v hash key value

                        BitmapNode3(null, bitmap, cloneAndSet (entries, idx, Node(newNode)))
                | Node(Node = node) ->
                    let newNode =
                        node.assoc (shift + 5) hash key value addedLeaf

                    if newNode = node
                    then this
                    else BitmapNode3(null, bitmap, cloneAndSet (entries, idx, Node(newNode)))
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise


        member this.without shift hash key =
            match hashToIndex hash shift bitmap with
            | None -> this :> INode3 |> Some
            | Some idx ->
                let entry = entries.[idx]

                match entry with
                | KeyValue (Key = k; Value = v) ->
                    if equiv (k, key) then
                        let bit = bitPos (hash, shift)

                        if bitmap = bit then // only one entry
                            None
                        else
                            BitmapNode3(null, bitmap ^^^ bit, removeEntry (entries, idx)) :> INode3
                            |> Some
                    else
                        this :> INode3 |> Some
                | Node(Node = node) ->
                    match node.without (shift + 5) hash key with
                    | None -> this :> INode3 |> Some
                    | Some n ->
                        if n = node then
                            this :> INode3 |> Some
                        else
                            BitmapNode3(null, bitmap, cloneAndSet (entries, idx, Node(n))) :> INode3
                            |> Some
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise

        member this.find shift hash key =
            match hashToIndex hash shift bitmap with
            | None -> None
            | Some idx ->
                match entries.[idx] with
                | KeyValue (Key = k; Value = v) ->
                    if equiv (key, k) then (MapEntry(k, v) :> IMapEntry) |> Some else None
                | Node(Node = node) -> node.find (shift + 5) hash key
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise

        member this.find2 shift hash key notFound =
            match hashToIndex hash shift bitmap with
            | None -> notFound
            | Some idx ->
                match entries.[idx] with
                | KeyValue (Key = k; Value = v) -> if equiv (key, k) then v else notFound
                | Node(Node = node) -> node.find2 (shift + 5) hash key notFound
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise

        member this.getNodeSeq() = BitmapNode3Seq.create (entries, 0)

        member this.assocTransient e shift hash key value addedLeaf =
            match hashToIndex hash shift bitmap with
            | None ->
                let n = bitCount (bitmap)

                if edit = e && n < entries.Length then
                    // we have space in the array and we are already editing this node transiently so we can just move things around here.
                    // If we have space in the array but we are not already editing this node, then the space is not helpful -- we just fall through to the subsequent cases
                    addedLeaf.set ()
                    let bit = bitPos (hash, shift)
                    let idx = bitIndex (bitmap, bit)
                    let array = entries
                    Array.Copy(array, idx, array, idx + 1, n - idx)
                    array.[idx] <- KeyValue(key, value)
                    bitmap <- bitmap ||| idx
                    this

                elif n >= 16 then
                    let nodes: INode3 option [] = Array.zeroCreate 32
                    let jdx = mask (hash, shift)
                    nodes.[jdx] <- (BitmapNode3.Empty :> INode3).assocTransient e (shift + 5) hash key value addedLeaf
                                   |> Some

                    let mutable j = 0
                    for i = 0 to 31 do
                        if ((bitmap >>> i) &&& 1) <> 0 then
                            nodes.[i] <- match entries.[j] with
                                         | KeyValue (Key = k; Value = v) ->
                                             (BitmapNode3.Empty :> INode3).assocTransient
                                                 e
                                                 (shift + 5)
                                                 (getHash k)
                                                 k
                                                 v
                                                 addedLeaf
                                             |> Some
                                         | Node(Node = node) -> node |> Some
                                         | EmptyEntry ->
                                             InvalidOperationException
                                                 ("Found Empty cell in BitmapNode3 -- algorithm bug")
                                             |> raise

                            j <- j + 1

                    ArrayNode3(e, n + 1, nodes)

                else
                    let bit = bitPos (hash, shift)
                    let idx = bitIndex (bitmap, bit)
                    let newArray: BNodeEntry3 [] = Array.zeroCreate (n + 1)
                    Array.Copy(entries, 0, newArray, 0, idx)
                    newArray.[idx] <- KeyValue(key, value)
                    Array.Copy(entries, idx, newArray, idx + 1, n - idx)
                    addedLeaf.set ()
                    this.modifyOrCreateBNode e (bitmap ||| bit) newArray

            | Some idx ->
                let entry = entries.[idx]

                match entry with
                | KeyValue (Key = k; Value = v) ->
                    if equiv (key, k) then
                        if value = v
                        then this
                        else BitmapNode3(null, bitmap, cloneAndSet (entries, idx, KeyValue(key, value)))
                    else
                        addedLeaf.set ()

                        let newNode =
                            SimpleHashMap3.createNode (shift + 5) k v hash key value

                        BitmapNode3(null, bitmap, cloneAndSet (entries, idx, Node(newNode)))
                | Node(Node = node) ->
                    let newNode =
                        node.assoc (shift + 5) hash key value addedLeaf

                    if newNode = node
                    then this
                    else BitmapNode3(null, bitmap, cloneAndSet (entries, idx, Node(newNode)))
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise

        member this.withoutTransient e shift hash key removedLeaf =
            match hashToIndex hash shift bitmap with
            | None -> Some(upcast this)
            | Some idx ->
                let entry = entries.[idx]

                match entry with
                | KeyValue (Key = k; Value = v) ->
                    if equiv (k, key) then
                        let bit = bitPos (hash, shift)

                        if bitmap = bit then // only one entry
                            None
                        elif edit = e then
                            // we are editable, edit in place
                            bitmap <- bitmap ^^^ bit
                            entries.[idx] <- EmptyEntry
                            (this :> INode3) |> Some
                        else
                            BitmapNode3(e, bitmap ^^^ bit, removeEntry (entries, idx)) :> INode3
                            |> Some
                    else
                        this :> INode3 |> Some
                | Node(Node = node) ->
                    match node.withoutTransient e (shift + 5) hash key removedLeaf with
                    | None -> this :> INode3 |> Some
                    | Some n ->
                        if n = node then
                            this :> INode3 |> Some
                        else
                            BitmapNode3(null, bitmap, cloneAndSet (entries, idx, Node(n))) :> INode3
                            |> Some
                | EmptyEntry ->
                    InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                    |> raise

        member this.iterator d =
            let s =
                seq {
                    for entry in entries do
                        match entry with
                        | KeyValue (Key = k; Value = v) -> yield d (k, v)
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
                        | KeyValue (Key = k; Value = v) -> yield d (k, v)
                        | Node(Node = node) ->
                            let ie = node.iteratorT (d)

                            while ie.MoveNext() do
                                yield ie.Current
                        | EmptyEntry -> ()

                }

            s.GetEnumerator()



and [<Sealed>] internal CollisionNode3(e: AtomicReference<Thread>, h: int, c: int, a: MapEntry []) =

    let edit: AtomicReference<Thread> = e
    let mutable count: int = c
    let mutable kvs: MapEntry [] = a
    let nodeHash: int = h

    member this.tryFindNodeIndex key =
        kvs
        |> Array.tryFindIndex (fun kv ->
            (not (isNull kv))
            && equiv ((kv :> IMapEntry).key(), key))

    interface INode3 with

        member this.assoc shift hash key value addedLeaf =
            if hash = nodeHash then
                match this.tryFindNodeIndex key with
                | Some idx ->
                    let kv = kvs.[idx] :> IMapEntry

                    if kv.value () = value
                    then this
                    else CollisionNode3(null, hash, count, cloneAndSet (kvs, idx, MapEntry(key, value)))
                | None ->
                    let newArray: MapEntry [] = count + 1 |> Array.zeroCreate
                    Array.Copy(kvs, 0, newArray, 0, count)
                    newArray.[count] <- MapEntry(key, value)
                    addedLeaf.set ()
                    CollisionNode3(null, hash, count + 1, newArray)
            else
                (BitmapNode3(null, bitPos (hash, shift), [| Node(this) |]) :> INode3)
                    .assoc
                    shift
                    h
                    key
                    value
                    addedLeaf

        member this.without shift hash key =
            match this.tryFindNodeIndex key with
            | None -> this :> INode3 |> Some
            | Some idx ->
                if count = 1 then
                    None
                else
                    CollisionNode3(null, h, count, removeEntry (kvs, idx)) :> INode3
                    |> Some

        member this.find shift hash key =
            match this.tryFindNodeIndex key with
            | None -> None
            | Some idx -> Some(upcast kvs.[idx])


        member this.find2 shift hash key notFound =
            match this.tryFindNodeIndex key with
            | None -> notFound
            | Some idx -> (kvs.[idx] :> IMapEntry).value()

        member this.getNodeSeq() = CollisionNode3Seq.create (kvs, 0)

        member this.assocTransient e shift hash key value addedLeaf =
            if hash = nodeHash then
                match this.tryFindNodeIndex key with
                | Some idx ->
                    let kv = kvs.[idx] :> IMapEntry

                    if kv.value () = value then
                        this
                    elif edit = e then
                        kvs.[idx] <- MapEntry(key, value)
                        this
                    else
                        // we have an entry with a different value, but we are not editable.
                        // create a new node with the new k/v entry
                        CollisionNode3(e, hash, count, cloneAndSet (kvs, idx, MapEntry(key, value)))
                | None ->
                    // no entry for this key, so we will be adding.
                    addedLeaf.set ()

                    if edit = e then
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
                            let newArray: MapEntry [] = currLength + 1 |> Array.zeroCreate
                            Array.Copy(kvs, 0, newArray, 0, currLength)
                            newArray.[currLength] <- MapEntry(key, value)
                            kvs <- newArray
                            count <- count + 1
                            this
                    else
                        // we are not editable, so we need to create a new node

                        let newArray: MapEntry [] = count + 1 |> Array.zeroCreate
                        Array.Copy(kvs, 0, newArray, 0, count)
                        newArray.[count] <- MapEntry(key, value)

                        CollisionNode3(e, hash, count + 1, newArray)
            else
                // we got to this collision node, but our key has different hash.
                // Need to create a bitmap node here holding our collision node and add our new key/value to it.
                (BitmapNode3(e, bitPos (hash, shift), [| Node(this) |]) :> INode3)
                    .assocTransient
                    e
                    shift
                    h
                    key
                    value
                    addedLeaf

        member this.withoutTransient e shift hash key removedLeaf =
            match this.tryFindNodeIndex key with
            | None -> this :> INode3 |> Some
            | Some idx ->
                removedLeaf.set ()

                if count = 1 then
                    None
                else if edit = e then
                    // we are editable, edit in place
                    count <- count - 1
                    kvs.[idx] <- kvs.[count]
                    kvs.[count] <- null
                    this :> INode3 |> Some
                else
                    CollisionNode3(e, h, count - 1, removeEntry (kvs, idx)) :> INode3
                    |> Some


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


and ArrayNode3Seq(nodes: INode3 option [], idx: int, s: ISeq) =
    inherit ASeq()


    static member create(nodes: (INode3 option) [], idx: int): ISeq =
        if idx >= nodes.Length then
            null
        else
            match nodes.[idx] with
            | Some (node) ->
                match node.getNodeSeq () with
                | null -> ArrayNode3Seq.create (nodes, idx + 1)
                | s -> ArrayNode3Seq(nodes, idx, s)
            | None -> ArrayNode3Seq.create (nodes, idx + 1)

    interface ISeq with
        member _.first() = s.first ()

        member _.next() =
            match s.next () with
            | null -> ArrayNode3Seq.create (nodes, idx + 1)
            | s1 -> ArrayNode3Seq(nodes, idx, s1)

and BitmapNode3Seq(entries: BNodeEntry3 [], idx: int, seq: ISeq) =
    inherit ASeq()

    static member create(entries: BNodeEntry3 [], idx: int): ISeq =
        if idx >= entries.Length then
            null
        else
            match entries.[idx] with
            | KeyValue (_, _) -> BitmapNode3Seq(entries, idx, null)
            | Node(Node = node) ->
                match node.getNodeSeq () with
                | null -> BitmapNode3Seq.create (entries, idx + 1)
                | s -> BitmapNode3Seq(entries, idx, s)
            | EmptyEntry -> null

    interface ISeq with
        member _.first() =
            match entries.[idx] with
            | KeyValue (Key = k; Value = v) -> MapEntry(k, v)
            | Node(Node = _) -> seq.first ()
            | EmptyEntry ->
                InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                |> raise


        member _.next() =
            match entries.[idx] with
            | KeyValue (_, _) -> BitmapNode3Seq.create (entries, idx + 1)
            | Node (_) ->
                match seq.next () with
                | null -> BitmapNode3Seq.create (entries, idx + 1)
                | s -> BitmapNode3Seq(entries, idx, s)
            | EmptyEntry ->
                InvalidOperationException("Found Empty cell in BitmapNode3 -- algorithm bug")
                |> raise

and CollisionNode3Seq(kvs: MapEntry [], idx: int) =
    inherit ASeq()

    static member create(kvs: MapEntry [], idx: int): ISeq =
        if idx >= kvs.Length then null else CollisionNode3Seq(kvs, idx)

    interface ISeq with
        member _.first() = kvs.[idx]
        member _.next() = CollisionNode3Seq.create (kvs, idx + 1)
