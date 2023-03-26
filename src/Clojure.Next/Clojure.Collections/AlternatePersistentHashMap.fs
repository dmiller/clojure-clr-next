namespace Clojure.Collections.Alternate

open Clojure.Collections
open System.Collections.Generic
open System.Collections
open System


type [<AbstractClass; Sealed>] private NodeOps2() =

    static member remove(arr: 'T array, i: int) : 'T array =
        let newArr: 'T array = Array.zeroCreate <| arr.Length - 1
        Array.Copy(arr, 0, newArr, 0, i)
        Array.Copy(arr, i + 1, newArr, i, newArr.Length - i)
        newArr


type PHashMap private (meta: IPersistentMap, count: int, root: INode, hasNull: bool, nullValue: obj) =
    inherit APersistentMap()


    // A persistent rendition of Phil Bagwell's Hash Array Mapped Trie
    //
    // Uses path copying for persistence.
    // HashCollision leaves vs extended hashing
    // Node polymorphism vs conditionals
    // No sub-tree pools or root-resizing
    // Any errors are Rich Hickey's (so he says), except those that I introduced

    // A PHashMap consists of a head node representing the map that has a points to a tree of nodes containing the key/value pairs.
    // The head node indicated if null is a key and holds the associated value.
    // Thus the tree is guaranteed not to contain a null key, allowing null to be used as an 'empty field' indicator.
    // The tree contains three kinds of nodes:
    //     ANode
    //     BNode
    //     CNode
    //
    // This arrangement seems ideal for a discriminated union, but we need mutable fields
    // (required to implement IEditableCollection and the ITransientXxx interfaces in-place)
    // and DUs don't support that.  Perhaps some smarter than me can do this someday.

    new(count, root, hasNull, nullValue) = PHashMap(null, count, root, hasNull, nullValue)

    member internal _.Meta = meta
    member internal _.Count = count
    member internal _.Root = root
    member internal _.HasNull = hasNull
    member internal _.NullValue = nullValue

    static member val Empty = PHashMap(null, 0, null, false, null)

    static member val internal notFoundValue = obj ()

    // factories

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

    static member create([<ParamArray>] init: obj[]) : PHashMap =
        let mutable ret =
            (PHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        for i in 0..2 .. init.Length - 1 do
            ret <- ret.assoc (init[i], init[i + 1])

        downcast ret.persistent ()

    static member createWithCheck([<ParamArray>] init: obj[]) : PHashMap =
        let mutable ret =
            (PHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        for i in 0..2 .. init.Length - 1 do
            ret <- ret.assoc (init[i], init[i + 1])

            if ret.count () <> i / 2 + 1 then
                raise <| ArgumentException("init", "Duplicate key: " + init[ i ].ToString())

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
        override _.meta() = meta

    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(m, meta) then
                this
            else
                PHashMap(m, count, root, hasNull, nullValue)

    interface ILookup with
        override this.valAt(k) = (this :> ILookup).valAt (k, null)

        override _.valAt(k, nf) =
            if isNull k then
                if hasNull then nullValue else nf
            elif isNull root then
                null
            else
                root.find (0, NodeOps.hash (k), k, nf)

    interface Associative with
        override _.containsKey(k) =
            if isNull k then
                hasNull
            else
                (not (isNull root))
                && root.find (0, NodeOps.hash (k), k, PHashMap.notFoundValue)
                   <> PHashMap.notFoundValue

        override _.entryAt(k) =
            if isNull k then
                if hasNull then
                    upcast MapEntry.create (null, nullValue)
                else
                    null
            elif isNull root then
                null
            else
                root.find (0, NodeOps.hash (k), k)


    interface Seqable with
        override _.seq() =
            let s = if isNull root then null else root.getNodeSeq ()

            if hasNull then
                upcast Cons(MapEntry.create (null, nullValue), s)
            else
                s


    interface IPersistentCollection with
        override _.count() = count

        override _.empty() =
            (PHashMap.Empty :> IObj).withMeta (meta) :?> IPersistentCollection

    interface Counted with
        override _.count() = count

    interface IPersistentMap with
        override this.assoc(k, v) =
            if isNull k then
                if hasNull && v = nullValue then
                    upcast this
                else
                    upcast PHashMap(meta, (if hasNull then count else count + 1), root, true, v)
            else
                let addedLeaf = BoolBox()

                let rootToUse: INode = if isNull root then upcast BNode.Empty else root

                let newRoot = rootToUse.assoc (0, NodeOps.hash (k), k, v, addedLeaf)

                if obj.ReferenceEquals(newRoot, root) then
                    upcast this
                else
                    upcast
                        PHashMap(
                            meta,
                            (if addedLeaf.isSet then count + 1 else count),
                            newRoot,
                            hasNull,
                            nullValue
                        )

        override this.assocEx(k, v) =
            if (this :> Associative).containsKey (k) then
                raise <| InvalidOperationException("Key already present")

            (this :> IPersistentMap).assoc (k, v)

        override this.without(k) =
            if isNull k then
                if hasNull then
                    upcast PHashMap(meta, count - 1, root, false, null)
                else
                    upcast this
            elif isNull root then
                upcast this
            else
                let newRoot = root.without (0, NodeOps.hash (k), k)

                if obj.ReferenceEquals(newRoot, root) then
                    upcast this
                else
                    upcast PHashMap(meta, count - 1, newRoot, hasNull, nullValue)

        override _.count() = count

    interface IEditableCollection with
        member this.asTransient() = upcast THashMap(this)

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

    member _.MakeEnumerator(d: KVMangleFn<Object>) : IEnumerator =
        let rootIter =
            if isNull root then
                PHashMap.emptyEnumerator ()
            else
                root.iteratorT (d)

        if hasNull then
            upcast PHashMap.nullEnumerator (d, nullValue, rootIter)
        else
            upcast rootIter

    member _.MakeEnumeratorT<'T>(d: KVMangleFn<'T>) =
        let rootIter =
            if isNull root then
                PHashMap.emptyEnumerator ()
            else
                root.iteratorT (d)

        if hasNull then
            PHashMap.nullEnumeratorT (d, nullValue, rootIter)
        else
            rootIter

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
            let init = if hasNull then f.invoke (init, null, nullValue) else init

            match init with
            | :? Reduced as r -> (r :> IDeref).deref ()
            | _ ->
                if not (isNull root) then
                    match root.kvReduce (f, init) with
                    | :? Reduced as r -> (r :> IDeref).deref ()
                    | r -> r
                else
                    init

    member _.fold(n: int64, combinef: IFn, reducef: IFn, fjinvoke: IFn, fjtask: IFn, fjfork: IFn, fjjoin: IFn) : obj =
        // JVM: we are ignoreing n for now
        let top: Func<obj> =
            Func<obj>(
                (fun () ->
                    let mutable ret = combinef.invoke ()

                    if not (isNull root) then
                        ret <- combinef.invoke (ret, root.fold (combinef, reducef, fjtask, fjfork, fjjoin))

                    if hasNull then
                        combinef.invoke (ret, reducef.invoke (combinef.invoke (), null, nullValue))
                    else
                        ret)
            )

        fjinvoke.invoke (top)

    static member internal createNode(shift: int, key1: obj, val1: obj, key2hash: int, key2: obj, val2: obj) : INode =
        let key1hash = NodeOps.hash (key1)

        if key1hash = key2hash then
            upcast CNode(null, key1hash, 2, [| key1; val1; key2; val2 |])
        else
            let box = BoolBox()
            let edit = AtomicBoolean()

            (BNode.Empty :> INode)
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
        ) : INode =
        let key1hash = NodeOps.hash (key1)

        if key1hash = key2hash then
            upcast CNode(null, key1hash, 2, [| key1; val1; key2; val2 |])
        else
            let box = BoolBox()

            (BNode.Empty :> INode)
                .assoc(edit, shift, key1hash, key1, val1, box)
                .assoc (edit, shift, key2hash, key2, val2, box)



and private THashMap(e, r, c, hn, nv) =
    inherit ATransientMap()

    [<NonSerialized>]
    let edit: AtomicBoolean = e

    [<VolatileField>]
    let mutable root: INode = r

    [<VolatileField>]
    let mutable count: int = c

    [<VolatileField>]
    let mutable hasNull: bool = hn

    [<VolatileField>]
    let mutable nullValue: obj = nv

    let leafFlag: BoolBox = BoolBox()

    new(m: PHashMap) = THashMap(AtomicBoolean(true), m.Root, m.Count, m.HasNull, m.NullValue)

    override this.doAssoc(k, v) =
        if isNull k then
            if nullValue <> v then
                nullValue <- v

            if not hasNull then
                count <- count + 1
                hasNull <- true
        else
            leafFlag.reset ()

            let n =
                (if isNull root then
                     (BNode.Empty :> INode)
                 else
                     root)
                    .assoc (edit, 0, NodeOps.hash (k), k, v, leafFlag)

            if not <| obj.ReferenceEquals(n, root) then
                root <- n

            if leafFlag.isSet then
                count <- count + 1

        this

    override this.doWithout(k) =
        if isNull k then
            if hasNull then
                hasNull <- false
                nullValue <- null
                count <- count - 1
        elif not (isNull root) then
            leafFlag.reset ()

            let n = root.without (edit, 0, NodeOps.hash (k), k, leafFlag)

            if not <| obj.ReferenceEquals(n, root) then
                root <- n

            if leafFlag.isSet then
                count <- count - 1

        this

    override _.doCount() = count

    override _.doPersistent() =
        edit.Set(false)
        upcast PHashMap(count, root, hasNull, nullValue)

    override _.doValAt(k, nf) =
        if isNull k then
            if hasNull then nullValue else nf
        elif isNull root then
            nf
        else
            root.find (0, NodeOps.hash (k), k, nf)

    override _.ensureEditable() =
        if not <| edit.Get() then
            raise <| InvalidOperationException("Transient used after persistent! call")

and BNodeEntry =
    | BKey of Key:obj
    | BSubNode of Node:INode

and [<Sealed; AllowNullLiteral>] internal BNode(e, b, ks, vs) =

    [<NonSerialized>]
    let myedit: AtomicBoolean = e

    let mutable bitmap: int = b
    let mutable kns: BNodeEntry array = ks
    let mutable vals: obj array = vs

    static member val Empty: BNode = BNode(null, 0, Array.empty<BNodeEntry>, Array.empty<obj>)

    member _.index(bit: int) : int = NodeOps.bitCount (bitmap &&& (bit - 1))

    member x.Bitmap
        with private get () = bitmap
        and private set (v) = bitmap <- v

    member private _.setKnArray(i, v) = kns[i] <- v
    member private _.setValArray(i,v) = vals[i] <- v

    member _.Kns
        with private get () = kns 
        and private set (v) = kns <- v

    member _.Vals
        with private get () = vals
        and private set (v) = vals <- v


    interface INode with
        member this.assoc(shift, hash, key, value, addedLeaf) =
            let bit = NodeOps.bitPos (hash, shift)
            let idx = this.index (bit)

            if bitmap &&& bit = 0 then
                let n = NodeOps.bitCount (bitmap)

                let newKns: BNodeEntry array = (n + 1) |> Array.zeroCreate
                Array.Copy(kns,0,newKns,0,idx)
                newKns[idx] <- BKey(key)
                Array.Copy(kns,idx,newKns,idx+1,n-idx)

                let newVals: obj array = (n + 1) |> Array.zeroCreate
                Array.Copy(vals,0,newVals,0,idx)
                newVals[idx] <- value
                Array.Copy(vals,idx,newVals,idx+1,n-idx)
                addedLeaf.set ()
                upcast BNode(null, (bitmap ||| bit), newKns, newVals)
            else
                match kns[idx] with
                | BSubNode(Node=node) ->
                    let resultNode = node.assoc(shift+5,hash,key,value,addedLeaf)
                    if obj.ReferenceEquals(resultNode, node) then
                        this
                    else
                        BNode(null,bitmap,NodeOps.cloneAndSet(kns,idx,BSubNode(resultNode)),vals)
                | BKey(Key=k) ->
                    if Util.equiv (key, k) then

                        if value = vals[idx] then
                            this
                        else
                            BNode(null, bitmap, kns,  NodeOps.cloneAndSet (vals, idx, value))
                    else
                        addedLeaf.set ()
                        BNode(null,bitmap,NodeOps.cloneAndSet(kns,idx,BSubNode(PHashMap.createNode(shift+5,k,vals[idx],hash,key,value))),vals)

        member this.without(shift, hash, key) =
            let bit = NodeOps.bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                upcast this
            else
                let idx = this.index (bit)

                match kns[idx] with
                | BSubNode(Node=n) -> 
                    let returnNode = n.without(shift+5,hash,key)
                    if obj.ReferenceEquals(returnNode, n) then
                        this
                    elif not (isNull returnNode) then
                        BNode(null, bitmap, NodeOps.cloneAndSet(kns,idx,BSubNode(returnNode)), vals)
                    elif bitmap = bit then
                        null
                    else 
                        BNode(null,bitmap^^^bit,NodeOps2.remove(kns,idx),NodeOps2.remove(vals,idx))

                | BKey(Key=k) -> 
                    if Util.equiv (key, k) then
                        if bitmap = bit then
                            null
                        else
                            BNode(null,bitmap^^^bit,NodeOps2.remove(kns,idx),NodeOps2.remove(vals,idx))
                    else
                        this

        member this.find(shift, hash, key) =
            let bit = NodeOps.bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                null
            else
                let idx = this.index (bit)
                match kns[idx] with
                | BSubNode(Node=n) ->
                    n.find(shift+5,hash,key)
                | BKey(Key=k) ->
                    if Util.equiv (key, k) then
                        MapEntry.create (k, vals[idx])
                    else
                        null

        member this.find(shift, hash, key, nf) =
            let bit = NodeOps.bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                nf
            else
                let idx = this.index (bit)
                match kns[idx] with
                | BSubNode(Node=n) ->
                    n.find(shift+5,hash,key,nf)
                | BKey(Key=k) ->
                    if Util.equiv (key, k) then
                        MapEntry.create (k, vals[idx])
                    else
                        nf

        member _.getNodeSeq() = BNodeSeq.create (kns,vals)

        member this.assoc(e, shift, hash, key, value, addedLeaf) =
            let bit = NodeOps.bitPos (hash, shift)
            let idx = this.index (bit)

            if (bitmap &&& bit) <> 0 then
                match kns[idx] with
                | BSubNode(Node = n) ->
                    let returnNode = n.assoc(e,shift+5,hash,key,value,addedLeaf)
                    if obj.ReferenceEquals(returnNode, n) then
                        this
                    else
                        this.editAndSetNode(e,idx,returnNode)
                | BKey(Key=k) ->
                    if Util.equiv(k,key) then
                        if vals[idx] = value then   
                            this
                        else
                            this.editAndSetValue(e,idx,value)
                    else
                        addedLeaf.editAndSet2(e,idx,PHashMap.createNode(e,shift+5,k,vals[idx],hash,key,value),null)
            else
                let n = NodeOps.bitCount bitmap

                if n * 2 < array.Length then
                    addedLeaf.set ()
                    let editable = this.ensureEditable (e)
                    Array.Copy(editable.Array, 2 * idx, editable.Array, 2 * (idx + 1), 2 * (n - idx))
                    editable.setArrayVal (2 * idx, key)
                    editable.setArrayVal (2 * idx + 1, value)
                    editable.Bitmap <- editable.Bitmap ||| bit
                    upcast editable
                else
                    let newArray: obj[] = 2 * (n + 4) |> Array.zeroCreate
                    Array.Copy(array, 0, newArray, 0, 2 * idx)
                    newArray[2 * idx] <- key
                    newArray[2 * idx + 1] <- value
                    addedLeaf.set ()
                    Array.Copy(array, 2 * idx, newArray, 2 * (idx + 1), 2 * (n - idx))
                    let editable = this.ensureEditable (e)
                    editable.Array <- newArray
                    editable.Bitmap <- editable.Bitmap ||| bit
                    upcast editable

        member this.without(e, shift, hash, key, removedLeaf) =
            let bit = NodeOps.bitPos (hash, shift)

            if (bitmap &&& bit) = 0 then
                upcast this
            else
                let idx = this.index (bit)
                let keyOrNull = array[2 * idx]
                let valOrNode = array[2 * idx + 1]

                if isNull keyOrNull then
                    let existingNode = (valOrNode :?> INode)
                    let n = existingNode.without (e, shift + 5, hash, key, removedLeaf)

                    if obj.ReferenceEquals(n, existingNode) then
                        upcast this
                    elif not (isNull n) then
                        upcast this.editAndSet (e, 2 * idx + 1, n)
                    elif bitmap = bit then
                        null
                    else
                        upcast this.editAndRemovePair (e, bit, idx)
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

    member this.ensureEditable(e: AtomicBoolean) : BNode =
        if Object.ReferenceEquals(myedit, e) then
            this
        else
            let n = NodeOps.bitCount (bitmap)

            let newArray: obj[] = Array.zeroCreate (if n >= 0 then 2 * (n + 1) else 4) // make room for next assoc

            Array.Copy(array, newArray, 2 * n)
            BNode(e, bitmap, newArray)

    member private this.editAndSet(e: AtomicBoolean, i: int, a: obj) : BNode =
        let editable = this.ensureEditable (e)
        editable.setArrayVal (i, a)
        editable


    member private this.editAndSet(e: AtomicBoolean, i: int, a: obj, j: int, b: obj) : BNode =
        let editable = this.ensureEditable (e)
        editable.setArrayVal (i, a)
        editable.setArrayVal (j, b)
        editable

    member private this.editAndRemovePair(e: AtomicBoolean, bit: int, i: int) : BNode =
        if bitmap = bit then
            null
        else
            let editable = this.ensureEditable (e)
            editable.Bitmap <- editable.Bitmap ^^^ bit
            Array.Copy(editable.Array, 2 * (i + 1), editable.Array, 2 * i, editable.Array.Length - 2 * (i + 1))
            editable.setArrayVal (editable.Array.Length - 2, null)
            editable.setArrayVal (editable.Array.Length - 1, null)
            editable


and CNode(edit: AtomicBoolean, hash: int, c, a) =

    let mutable count: int = c
    let mutable array: obj[] = a

    member _.Array
        with private get () = array
        and private set (a) = array <- a

    member _.Count
        with private get () = count
        and private set (c) = count <- c

    member _.tryFindIndex(key: obj) : int option =
        let rec loop (i: int) =
            if i >= 2 * count then None
            elif Util.equiv (key, array[i]) then Some i
            else i + 2 |> loop

        loop 0


    interface INode with
        member this.assoc(shift, h, key, value, addedLeaf) =
            if h = hash then
                match this.tryFindIndex (key) with
                | Some idx ->
                    if array[idx + 1] = value then
                        upcast this
                    else
                        upcast CNode(null, h, count, NodeOps.cloneAndSet (array, idx + 1, value))
                | None ->
                    let newArray: obj[] = 2 * (count + 1) |> Array.zeroCreate
                    Array.Copy(array, 0, newArray, 0, 2 * count)
                    newArray[2 * count] <- key
                    newArray[2 * count + 1] <- value
                    addedLeaf.set ()
                    upcast CNode(edit, h, count + 1, newArray)
            else
                (BNode(null, NodeOps.bitPos (hash, shift), [| null; this |]) :> INode)
                    .assoc (shift, h, key, value, addedLeaf)

        member this.without(shift, h, key) =
            match this.tryFindIndex (key) with
            | None -> upcast this
            | Some idx ->
                if count = 1 then
                    null
                else
                    upcast CNode(null, h, count - 1, NodeOps.removePair (array, idx / 2))

        member this.find(shift, h, key) =
            match this.tryFindIndex (key) with
            | None -> null
            | Some idx -> upcast MapEntry.create (array[idx], array[idx + 1])

        member this.find(shift, h, key, nf) =
            match this.tryFindIndex (key) with
            | None -> nf
            | Some idx -> array[idx + 1]

        member _.getNodeSeq() = NodeSeq.create (array)

        member this.assoc(e, shift, h, key, value, addedLeaf) =
            if h = hash then
                match this.tryFindIndex (key) with
                | Some idx ->
                    if array[idx + 1] = value then
                        upcast this
                    else
                        upcast this.editAndSet (e, idx + 1, value)
                | None ->
                    if array.Length > 2 * count then
                        addedLeaf.set ()

                        let editable = this.editAndSet (e, 2 * count, key, 2 * count + 1, value)

                        editable.Count <- editable.Count + 1
                        upcast editable
                    else
                        let newArray: obj[] = array.Length + 2 |> Array.zeroCreate
                        Array.Copy(array, 0, newArray, 0, array.Length)
                        newArray[array.Length] <- key
                        newArray[array.Length + 1] <- value
                        addedLeaf.set ()
                        upcast this.ensureEditable (e, count + 1, newArray)
            else
                (BNode(null, NodeOps.bitPos (hash, shift), [| null; this; null; null |]) :> INode)
                    .assoc (e, shift, h, key, value, addedLeaf)

        member this.without(e, shift, h, key, removedLeaf) =
            match this.tryFindIndex (key) with
            | None -> upcast this
            | Some idx ->
                removedLeaf.set ()

                if count = 1 then
                    null
                else
                    let editable = this.ensureEditable (e)
                    editable.Array[idx] <- editable.Array[2 * count - 2]
                    editable.Array[idx + 1] <- editable.Array[2 * count - 1]
                    editable.Array[2 * count - 2] <- null
                    editable.Array[2 * count - 1] <- null
                    editable.Count <- editable.Count - 1
                    upcast editable

        member _.kvReduce(f, init) = NodeSeq.kvReduce (array, f, init)

        member _.fold(combinef, reducef, fjtask, fjfork, fjjoin) =
            NodeSeq.kvReduce (array, reducef, combinef.invoke ())

        member _.iterator(d) = NodeIter.getEnumerator (array, d)

        member _.iteratorT(d) = NodeIter.getEnumeratorT (array, d)


    member this.ensureEditable(e) =
        if obj.ReferenceEquals(e, edit) then
            this
        else
            let newArray: obj[] = 2 * (count + 1) |> Array.zeroCreate
            Array.Copy(array, 0, newArray, 0, 2 * count)
            CNode(e, hash, count, newArray)

    member this.ensureEditable(e, c, a) =
        if obj.ReferenceEquals(e, edit) then
            array <- a
            count <- c
            this
        else
            CNode(e, hash, c, a)

    member this.editAndSet(e, i, a) =
        let editable = this.ensureEditable (e)
        editable.Array[i] <- a
        editable

    member this.editAndSet(e, i, a, j, b) =
        let editable = this.ensureEditable (e)
        editable.Array[i] <- a
        editable.Array[j] <- b
        editable




and NodeSeq(meta, array: obj[], idx: int, seq: ISeq) =
    inherit ASeq(meta)

    new(i, a, s) = NodeSeq(null, a, i, s)

    static member private create(array: obj[], i: int, s: ISeq) : ISeq =
        if not (isNull s) then
            upcast NodeSeq(null, array, i, s)
        else
            let result =
                array
                |> Seq.indexed
                |> Seq.skip i
                |> Seq.tryPick (fun (j, node) ->

                    if j % 2 = 0 then // even => key entry
                        if not (isNull array[j]) then
                            NodeSeq(null, array, j, null) |> Some
                        else
                            None
                    else // odd => value entry

                    if
                        isNull array[j - 1]
                    then
                        let node: INode = array[j] :?> INode

                        if not (isNull node) then
                            let nodeSeq = node.getNodeSeq ()

                            if not (isNull nodeSeq) then
                                NodeSeq(null, array, j + 1, nodeSeq) |> Some
                            else
                                None
                        else
                            None
                    else
                        None)

            match result with
            | Some ns -> upcast ns
            | None -> null


    static member create(array: obj[]) : ISeq = NodeSeq.create (array, 0, null)

    interface IObj with
        override this.withMeta(m) =
            if Object.ReferenceEquals(m, (this :> IMeta).meta ()) then
                upcast this
            else
                upcast NodeSeq(m, array, idx, seq)

    interface ISeq with
        member _.first() =
            match seq with
            | null -> upcast MapEntry.create (array[idx], array[idx + 1])
            | _ -> seq.first ()

        member _.next() =
            match seq with
            | null -> NodeSeq.create (array, idx + 2, null)
            | _ -> NodeSeq.create (array, idx, seq.next ())

    static member kvReduce(a: obj[], f: IFn, init: obj) : obj =
        let rec loop (result: obj) (i: int) =
            if i >= a.Length then
                result
            else
                let nextResult =
                    if not (isNull a[i]) then
                        f.invoke (result, a[i], a[i + 1])
                    else
                        let node = a[i + 1] :?> INode

                        if not (isNull node) then
                            node.kvReduce (f, result)
                        else
                            result

                if nextResult :? Reduced then
                    nextResult
                else
                    loop nextResult (i + 2)

        loop init 0
