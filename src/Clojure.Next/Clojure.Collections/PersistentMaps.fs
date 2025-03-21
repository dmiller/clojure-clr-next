﻿namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics
open System.Text


////////////////////////////////////
//
//  APersistentMap
//
////////////////////////////////////

/// Abstract base class for immutable, persistent maps.
[<AbstractClass; AllowNullLiteral>]
type APersistentMap() =
    inherit AFn()

    let mutable _hasheq: int option = None

    override this.ToString() = RTPrint.printString (this)
    override this.Equals(o) = APersistentMap.mapEquals (this, o)

    override this.GetHashCode() =
        match _hasheq with
        | Some h -> h
        | None ->
            let h = Hashing.hashUnordered (this :> IEnumerable)
            _hasheq <- Some h
            h

    /// Helper method for comparing an IPersistentMap to another object.
    static member mapEquals(m1: IPersistentMap, o: obj) =
        match o with
        | _ when LanguagePrimitives.PhysicalEquality (m1 :> obj) o -> true
        | :? IDictionary as d ->
            if d.Count <> m1.count () then
                false
            else
                let rec loop (s: ISeq) =
                    if isNull s then
                        true
                    else
                        let me = s.first () :?> IMapEntry

                        if not <| d.Contains(me.key ()) || not <| Util.equals (me.value (), d[me.key ()]) then
                            false
                        else
                            loop (s.next ())

                loop (m1.seq ())
        | _ -> false

    interface IHashEq with
        member this.hasheq() = this.GetHashCode()

    interface MapEquivalence

    interface ILookup with
        member _.valAt(k) =
            raise
            <| NotImplementedException("Derived classes must implement ILookup.valAt(key)")

        member _.valAt(k, nf) =
            raise
            <| NotImplementedException("Derived classes must implement ILookup.valAt(key,notFound")

    interface Associative with
        member _.containsKey(k) =
            raise
            <| NotImplementedException("Derived classes must implement Associative.containsKey(key)")

        member _.entryAt(i) =
            raise
            <| NotImplementedException("Derived classes must implement Associative.entryAt(key)")

        member this.assoc(k, v) = (this :> IPersistentMap).assoc (k, v)

    interface Seqable with
        member _.seq() =
            raise <| NotImplementedException("Derived classes must implement Seqable.seq()")

    interface IPersistentCollection with
        member this.cons(o) = (this :> IPersistentMap).cons (o)
        member this.count() = (this :> IPersistentMap).count ()

        member _.empty() =
            raise
            <| NotImplementedException("Derived classes must implement IPersistentCollection.empty()")

        member this.equiv(o) =
            match o with
            | :? IPersistentMap when not (o :? MapEquivalence) -> false
            | :? IDictionary as d ->
                if d.Count <> (this :> IPersistentMap).count () then
                    false
                else
                    let rec loop (s: ISeq) =
                        if isNull s then
                            true
                        else
                            let me = s.first () :?> IMapEntry

                            if not <| d.Contains(me.key ()) || not <| Util.equiv (me.value (), d[me.key ()]) then
                                false
                            else
                                loop (s.next ())

                    loop ((this :> Seqable).seq ())
            | _ -> false

    member this.doEquiv(o: obj) : bool =
        (this :> IPersistentCollection).equiv (o)


    interface IMeta with
        member _.meta() =
            raise
            <| NotImplementedException("Derived classes must implement IMeta.meta(meta)")

    interface IObj with
        member _.withMeta(meta) =
            raise
            <| NotImplementedException("Derived classes must implement IObj.withMeta(meta)")

    interface Counted with
        member this.count() = (this :> IPersistentMap).count ()

    interface IPersistentMap with
        member _.assoc(k, v) =
            raise
            <| NotImplementedException("Derived classes must implement IPersistentMap.assoc(key,value)")

        member _.assocEx(k, v) =
            raise
            <| NotImplementedException("Derived classes must implement IPersistentMap.assocEx(key,value)")

        member _.without(k) =
            raise
            <| NotImplementedException("Derived classes must implement IPersistentMap.without(key)")

        member _.count() =
            raise
            <| NotImplementedException("Derived classes must implement IPersistentMap.count()")

        member this.cons(o) =
            match o with
            | :? IMapEntry as me -> (this :> IPersistentMap).assoc (me.key (), me.value ())
            | :? DictionaryEntry as de -> (this :> IPersistentMap).assoc (de.Key, de.Value)
            | :? KeyValuePair<obj, obj> as kvp -> (this :> IPersistentMap).assoc (kvp.Key, kvp.Value)
            | :? IPersistentVector as v ->
                if v.count () = 2 then
                    (this :> IPersistentMap).assoc (v.nth (0), v.nth (1))
                else
                    raise <| ArgumentException("Vector arg to map cons must be a pair")
            | _ ->
                let rec loop (s: ISeq) (m: IPersistentMap) =
                    if isNull s then
                        m
                    else
                        let me = s.first () :?> IMapEntry
                        loop (s.next ()) (m.assoc (me.key (), me.value ()))

                loop (RTSeq.seq (o)) this

    interface IFn with
        override this.invoke(a1) = (this :> ILookup).valAt (a1)
        override this.invoke(a1, a2) = (this :> ILookup).valAt (a1, a2)

    static member val private missingValue = obj ()

    interface IDictionary<obj, obj> with
        member _.Add(_, _) =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member _.Remove(_) =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member this.Keys = KeySeq.create ((this :> Seqable).seq ())
        member this.Values = ValSeq.create ((this :> Seqable).seq ())

        member this.Item
            with get key = (this :> ILookup).valAt (key)
            and set _ _ = raise <| InvalidOperationException("Cannot modify an immutable map")

        member this.ContainsKey(k) = (this :> Associative).containsKey (k)

        member this.TryGetValue(k, v) =
            match (this :> ILookup).valAt (k, APersistentMap.missingValue) with
            | x when x = APersistentMap.missingValue ->
                v <- null
                false
            | found ->
                v <- found
                true


    interface ICollection<KeyValuePair<obj, obj>> with
        member _.Add(_) =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member _.Clear() =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member _.Remove(_) =
            raise <| InvalidOperationException("Cannot modify an immutable map")


        member _.IsReadOnly = true
        member this.Count = (this :> IPersistentMap).count ()

        member this.Contains(kv) =
            match (this :> IDictionary<obj, obj>).TryGetValue(kv.Key) with
            | false, _ -> false
            | true, null -> isNull kv.Value
            | true, v -> v.Equals(kv.Value)

        member this.CopyTo(arr, idx) =
            let s = (this :> Seqable).seq ()

            if not <| isNull s then // TODO -- this won't work -- check out APersistentSet for a replacement -- the C# code is worng.
                (s :?> ICollection).CopyTo(arr, idx)


    interface IDictionary with
        member _.IsFixedSize = true
        member _.IsReadOnly = true
        member this.Contains(key) = (this :> Associative).containsKey (key)
        member this.Keys = KeySeq.create ((this :> Seqable).seq ())
        member this.Values = ValSeq.create ((this :> Seqable).seq ())

        member this.Item
            with get key = (this :> ILookup).valAt (key)
            and set _ _ = raise <| InvalidOperationException("Cannot modify an immutable map")

        member this.GetEnumerator() = new MapEnumerator(this)

        member _.Add(_, _) =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member _.Clear() =
            raise <| InvalidOperationException("Cannot modify an immutable map")

        member _.Remove(_) =
            raise <| InvalidOperationException("Cannot modify an immutable map")


    interface ICollection with
        member this.Count = (this :> IPersistentMap).count ()
        member this.IsSynchronized = true
        member this.SyncRoot = this

        member this.CopyTo(arr, idx) =
            let s = (this :> Seqable).seq ()

            if not <| isNull s then
                (s :?> ICollection).CopyTo(arr, idx)


    interface IEnumerable<KeyValuePair<obj, obj>> with
        member this.GetEnumerator() =
            let generator (s: ISeq) : (KeyValuePair<obj, obj> * ISeq) option =
                if isNull s then
                    None
                else
                    let me = s.first () :?> IMapEntry
                    let kvp = KeyValuePair<obj, obj>(me.key (), me.value ())
                    Some(kvp, s.next ())

            let s = Seq.unfold generator ((this :> Seqable).seq ())
            s.GetEnumerator()


    interface IEnumerable with
        member this.GetEnumerator() =
            let generator (s: ISeq) : (KeyValuePair<obj, obj> * ISeq) option =
                if isNull s then
                    None
                else
                    let me = s.first () :?> IMapEntry
                    let kvp = KeyValuePair<obj, obj>(me.key (), me.value ())
                    Some(kvp, s.next ())

            let s = Seq.unfold generator ((this :> Seqable).seq ())
            s.GetEnumerator()

    interface IEnumerable<IMapEntry> with
        member this.GetEnumerator() =
            let generator (s: ISeq) : (IMapEntry * ISeq) option =
                if isNull s then
                    None
                else
                    let me = s.first () :?> IMapEntry
                    Some(me, s.next ())

            let s = Seq.unfold generator ((this :> Seqable).seq ())
            s.GetEnumerator()

////////////////////////////////////
//
//  ATransientMap
//
////////////////////////////////////

/// Abstract base class for transient maps.
[<AbstractClass; AllowNullLiteral>]
type ATransientMap() =
    inherit AFn()

    // methods to be supplied by derived classes
    abstract ensureEditable: unit -> unit
    abstract doAssoc: key: obj * value: obj -> ITransientMap
    abstract doWithout: key: obj -> ITransientMap
    abstract doValAt: key: obj * notFound: obj -> obj
    abstract doCount: unit -> int
    abstract doPersistent: unit -> IPersistentMap


    //ITransientMap, ITransientAssociative2

    interface ITransientCollection with
        member this.persistent() = (this :> ITransientMap).persistent ()
        member this.conj(o) = this.conj (o)

    interface ITransientAssociative with
        member this.assoc(k, v) = (this :> ITransientMap).assoc (k, v)

    interface ILookup with
        member this.valAt(k) = (this :> ILookup).valAt (k, null)

        member this.valAt(k, nf) =
            this.ensureEditable ()
            this.doValAt (k, nf)

    interface IFn with
        member this.invoke(arg1) = (this :> ILookup).valAt (arg1)
        member this.invoke(arg1, arg2) = (this :> ILookup).valAt (arg1, arg2)

    interface ITransientMap with
        member this.assoc(k, v) =
            this.ensureEditable ()
            this.doAssoc (k, v)

        member this.without(k) =
            this.ensureEditable ()
            this.doWithout (k)

        member this.persistent() =
            this.ensureEditable ()
            this.doPersistent ()

    interface Counted with
        member this.count() =
            this.ensureEditable ()
            this.doCount ()

    static member val _notFound = obj ()

    interface ITransientAssociative2 with
        member this.containsKey(k) =
            (this :> ILookup).valAt (k, ATransientMap._notFound) <> ATransientMap._notFound

        member this.entryAt(k) =
            let v = (this :> ILookup).valAt (k, ATransientMap._notFound)

            if v <> ATransientMap._notFound then
                MapEntry.create (k, v)
            else
                null

    member this.conj(o: obj) =
        this.ensureEditable ()
        let tm = this :> ITransientMap

        match o with
        | :? IMapEntry as me -> tm.assoc (me.key (), me.value ())
        | :? DictionaryEntry as de -> tm.assoc (de.Key, de.Value)
        | :? IPersistentVector as pv ->
            if pv.count () <> 2 then
                raise <| ArgumentException("Vector arg to map conj must be a pair")
            else
                tm.assoc (pv.nth (0), pv.nth (1))
        | :? KeyValuePair<obj, obj> as p -> tm.assoc (p.Key, p.Value)
        | _ ->
            let rec loop (ret: ITransientMap) (s: ISeq) =
                if isNull s then
                    ret
                else
                    let me = s.first () :?> IMapEntry
                    let nextRet = ret.assoc (me.key (), me.value ())
                    loop nextRet (s.next ())

            loop this (RTSeq.seq (o))


////////////////////////////////////
//
//  PersistentArrayMap
//
////////////////////////////////////

/// An immutable, persistent map implemented as an array of alternating keys and values.  Efficient for small mpas only.
[<Sealed; AllowNullLiteral>]
type PersistentArrayMap private (_meta: IPersistentMap, _arr: obj array) =
    inherit APersistentMap()

    /// Create a PersistentArrayMap from an array of alternating keys and values, with null metadata.
    new(arr) = PersistentArrayMap(null, arr)

    /// Create an empty array map with null metadata.
    new() = PersistentArrayMap(null, Array.zeroCreate 0)
    
    /// Size at which to switch from PersistentArrayMap to PersistentHashMap.
    static member val HashtableThreshold: int = 16

    /// An empty PersistentArrayMap with null metadata.
    static member val public Empty = PersistentArrayMap()

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m _meta then
                this
            else
                PersistentArrayMap(m, _arr)

    interface IMeta with
        override _.meta() = _meta

    interface ILookup with
        override this.valAt(key) = (this :> ILookup).valAt (key, null)

        override this.valAt(key, nf) =
            let idx = this.indexOfKey (key)
            if idx >= 0 then _arr[idx + 1] else nf

    member this.indexOfObject(key: obj) =
        let ep = Util.getEquivPred (key)

        let rec loop (i: int) =
            if i >= _arr.Length then -1
            elif ep (key, _arr[i]) then i
            else loop (i + 2)

        loop 0

    member this.indexOfKey(key: obj) =
        match key with
        | :? Keyword ->
            let rec loop (i: int) =
                if i >= _arr.Length then -1
                elif LanguagePrimitives.PhysicalEquality key _arr[i] then i
                else loop (i + 2)

            loop 0
        | _ -> this.indexOfObject (key)

    static member internal equalKey(k1: obj, k2: obj) =
        match k1 with
        | :? Keyword -> LanguagePrimitives.PhysicalEquality k1 k2
        | _ -> Util.equiv (k1, k2)

    interface Associative with
        override this.containsKey(k) = this.indexOfKey (k) >= 0

        override this.entryAt(k) =
            let i = this.indexOfKey (k)

            if i >= 0 then
                MapEntry.create (_arr[i], _arr[i + 1]) :> IMapEntry
            else
                null

    interface Seqable with
        override this.seq() =
            if _arr.Length > 0 then PAMSeq(_arr, 0) else null

    interface IPersistentCollection with
        override this.empty() =
            (PersistentArrayMap.Empty :> IObj).withMeta (_meta) :?> IPersistentCollection

    interface Counted with
        override this.count() = _arr.Length / 2

    interface IPersistentMap with
        override this.count() = _arr.Length / 2

        override this.assoc(k, v) =
            let i = this.indexOfKey (k)

            if i >= 0 then
                if _arr[i + 1] = v then
                    this
                else
                    let newArray = _arr.Clone() :?> obj array
                    newArray[i + 1] <- v
                    this.create (newArray)
            elif _arr.Length >= PersistentArrayMap.HashtableThreshold then
                (this.createHashTree (_arr) :> IPersistentMap).assoc (k, v)
            else
                let newArray: obj array = Array.zeroCreate (_arr.Length + 2)

                if _arr.Length > 0 then
                    Array.Copy(_arr, 0, newArray, 0, _arr.Length)

                newArray[newArray.Length - 2] <- k
                newArray[newArray.Length - 1] <- v
                this.create (newArray)

        override this.assocEx(k, v) =
            let i = this.indexOfKey (k)

            if i >= 0 then
                raise <| InvalidOperationException("Key already present")
            else
                (this :> IPersistentMap).assoc (k, v)

        override this.without(k) =
            let i = this.indexOfKey (k)

            if i < 0 then
                this
            else
                // key exists, remove
                let newLen = _arr.Length - 2

                if newLen = 0 then
                    (this :> IPersistentCollection).empty () :?> IPersistentMap
                else
                    let newArray: obj array = Array.zeroCreate newLen
                    Array.Copy(_arr, 0, newArray, 0, i)
                    Array.Copy(_arr, i + 2, newArray, i, newLen - i)
                    this.create (newArray)


    member this.createHashTree(init: obj array) = PersistentHashMap.create (_meta, init)

    interface IEditableCollection with
        member _.asTransient() = TransientArrayMap(_arr)

    interface IKVReduce with
        member this.kvreduce(f, init) =
            let rec loop (acc: obj) (i: int) =
                if i >= _arr.Length then
                    acc
                else
                    match f.invoke (acc, _arr[i], _arr[i + 1]) with
                    | :? Reduced as red -> (red :> IDeref).deref ()
                    | newAcc -> loop newAcc (i + 2)

            loop init 0

    interface IDrop with
        member this.drop(n) =
            if _arr.Length > 0 then
                ((this :> Seqable).seq () :?> IDrop).drop (n)
            else
                null


    interface IMapEnumerable with
        member this.keyEnumerator() =
            (this :> IMapEnumerableTyped<obj, obj>).tkeyEnumerator ()

        member this.valEnumerator() =
            (this :> IMapEnumerableTyped<obj, obj>).tvalEnumerator ()

    interface IMapEnumerableTyped<obj, obj> with
        member this.tkeyEnumerator() =
            (seq { for i in 0..2 .. _arr.Length - 1 -> _arr[i] }).GetEnumerator()

        member this.tvalEnumerator() =
            (seq { for i in 0..2 .. _arr.Length - 1 -> _arr[i + 1] }).GetEnumerator()

    interface IEnumerable with
        member this.GetEnumerator() =
            (this :> IEnumerable<IMapEntry>).GetEnumerator()

    interface IEnumerable<IMapEntry> with
        member this.GetEnumerator() =
            (seq { for i in 0..2 .. _arr.Length - 1 -> (MapEntry.create (_arr[i], _arr[i + 1]) :> IMapEntry) })
                .GetEnumerator()

    interface IEnumerable<KeyValuePair<obj, obj>> with
        member this.GetEnumerator() =
            (seq { for i in 0..2 .. _arr.Length - 1 -> KeyValuePair(_arr[i], _arr[i + 1]) })
                .GetEnumerator()

    static member createD2(other: IDictionary<'TKey, 'TValue>) : IPersistentMap =
        let mutable ret =
            (PersistentArrayMap.Empty :> IEditableCollection).asTransient () :?> ITransientAssociative

        for de in Seq.cast<KeyValuePair<'TKey, 'TValue>> other do
            ret <- ret.assoc (de.Key, de.Value)

        ret.persistent () :?> IPersistentMap

    static member create(other: IDictionary) : IPersistentMap =
        let mutable ret =
            (PersistentArrayMap.Empty :> IEditableCollection).asTransient () :?> ITransientAssociative

        for de in Seq.cast<DictionaryEntry> other do
            ret <- ret.assoc (de.Key, de.Value)

        ret.persistent () :?> IPersistentMap

    member this.create([<ParamArray>] init: obj array) = PersistentArrayMap(_meta, init)

    static member createWithCheck(init: obj array) =

        let mutable i = 0

        while i < init.Length do
            let mutable j = i + 2

            while j < init.Length do
                if PersistentArrayMap.equalKey (init.[i], init.[j]) then
                    raise <| ArgumentException($"Duplicate key: {init.[i]}")

                j <- j + 2

            i <- i + 2

        PersistentArrayMap(init)


    // This method attempts to find reuse the given array as the basis for an array map as quickly as possible.
    // If a trailing element exists in the array or it contains duplicate keys then it delegates to the complex path.

    static member createAsIfByAssoc(init: obj array) : PersistentArrayMap =
        let hasTrailing = (init.Length &&& 1) = 1

        let rec keyIsDuplicated (i: int) (j: int) =
            if j >= init.Length then false
            elif init[i] = init[j] then true
            else keyIsDuplicated i (j + 2)

        let rec hasDuplicateKey (i: int) =
            if i >= init.Length then false
            elif keyIsDuplicated i (i + 2) then true
            else hasDuplicateKey (i + 2)

        let complexPath = hasTrailing || hasDuplicateKey 0

        if complexPath then
            PersistentArrayMap.createAsIfByAssocComplexPath (init, hasTrailing)
        else
            PersistentArrayMap(init)

    static member growSeedArray(seed: obj array, trailing: IPersistentCollection) : obj array =
        let seed = seed
        let seedCount = seed.Length - 1
        Array.Resize(ref seed, seedCount + trailing.count () * 2)

        let rec loop (i: int) (s: ISeq) =
            if not <| isNull s then
                let me = s.first () :?> IMapEntry
                seed[i] <- me.key ()
                seed[i + 1] <- me.value ()
                loop (i + 2) (s.next ())

        loop seedCount (trailing.seq ())
        seed


    // This method handles the default case of an array containing alternating key/value pairs.
    // It will reallocate a smaller init array if duplicate keys are found.
    //
    // If a trailing element is found then will attempt to add it to the resulting map as if by conj.
    // NO guarantees about the order of the keys in the trailing element are made.

    static member createAsIfByAssocComplexPath(init: obj array, hasTrailing: bool) : PersistentArrayMap =
        let init =
            if hasTrailing then
                let trailing =
                    (PersistentArrayMap.Empty :> IPersistentMap).cons (init[init.Length - 1])

                PersistentArrayMap.growSeedArray (init, trailing)
            else
                init

        // ClojureJVM says: If this looks like it is doing busy-work, it is because it
        // is achieving these goals: O(n^2) run time like
        // createWithCheck(), never modify init arg, and only
        // allocate memory if there are duplicate keys.
        //
        // the original code used doubly-nested for-loops with breaks.  Needed a bit of reworking for F#.

        let rec appearsFirstTime (kIndex: int) (j: int) =
            if j >= kIndex then
                true
            elif PersistentArrayMap.equalKey (init[j], init[kIndex]) then
                false
            else
                appearsFirstTime kIndex (j + 2)

        let rec uniqueKeyCount (cnt: int) (i: int) =
            if i >= init.Length then
                cnt
            else
                uniqueKeyCount (if appearsFirstTime i 0 then cnt + 1 else cnt) (i + 2)

        let keyCount = uniqueKeyCount 0 0

        if keyCount * 2 = init.Length then
            PersistentArrayMap(init)
        else
            // Create a new shorter array with unique keys, and
            // the last value associated with each key.  To behave
            // like assoc, the first occurrence of each key must
            // be used, since its metadata may be different than
            // later equal keys.

            let noDups: obj array = Array.zeroCreate (keyCount * 2)

            let rec findValIndex (kIndex: int) (j: int) =
                if j < kIndex then
                    j + 1
                elif PersistentArrayMap.equalKey (init[kIndex], init[j]) then
                    j + 1
                else
                    findValIndex kIndex (j - 2)

            let rec doKeys (nodupLen: int) (kIndex: int) : int =
                if kIndex >= init.Length then
                    nodupLen
                elif appearsFirstTime kIndex 0 then
                    let vIndex = findValIndex kIndex (init.Length - 2)
                    noDups[nodupLen] <- init[kIndex]
                    noDups[nodupLen + 1] <- init[vIndex]
                    doKeys (nodupLen + 2) (kIndex + 1)
                else
                    doKeys nodupLen (kIndex + 1)

            let dupLen = doKeys 0 0

            if dupLen <> keyCount * 2 then
                raise
                <| ArgumentException(
                    $"Internal error: createAsIfByAssocComplexPath keyCount = {keyCount}, duplen = {dupLen}"
                )

            PersistentArrayMap(init)

////////////////////////////////////
//
//  TransientArrayMap
//
////////////////////////////////////

/// A transient map implemented as an array of alternating keys and values.
and TransientArrayMap(a: obj array) =
    inherit ATransientMap()

    let _arr: obj array =
        Array.zeroCreate (Math.Max(PersistentArrayMap.HashtableThreshold, a.Length))

    [<NonSerialized; VolatileField>]
    let mutable _isTransient = true

    [<VolatileField>]
    let mutable _len = a.Length

    do Array.Copy(a, _arr, a.Length)

    member this.indexOfKey(key: obj) =
        let rec loop (i: int) =
            if i >= _arr.Length then -1
            elif PersistentArrayMap.equalKey (_arr[i], key) then i
            else loop (i + 2)

        loop 0

    override this.ensureEditable() =
        if not _isTransient then
            raise <| InvalidOperationException("Transient used after persistent! call")

    override this.doAssoc(k, v) =
        let i = this.indexOfKey (k)

        if i >= 0 then
            if _arr[i + 1] <> v then // TODO: Should this be PhysicalEquality?
                _arr[i + 1] <- v

            this
        elif _len >= _arr.Length then
            ((PersistentHashMap.create (_arr) :> IEditableCollection).asTransient () :?> ITransientMap)
                .assoc (k, v)
        else
            _arr[_len] <- k
            _arr[_len + 1] <- v
            _len <- _len + 2
            this

    override this.doWithout(k) =
        let i = this.indexOfKey (k)

        if i >= 0 then
            if _len >= 2 then
                _arr[i] <- _arr[_len - 2]
                _arr[i + 1] <- _arr[_len - 1]

            _len <- _len - 2

        this

    override this.doValAt(k, nf) =
        let i = this.indexOfKey (k)
        if i >= 0 then _arr[i + 1] else nf

    override this.doCount() = _len / 2

    override this.doPersistent() =
        this.ensureEditable ()
        _isTransient <- false
        let a: obj array = Array.zeroCreate _len
        Array.Copy(_arr, a, _len)
        PersistentArrayMap(a)


////////////////////////////////////
//
//  PAMSeq
//
////////////////////////////////////

/// Implements an ISeq for a PersistentArrayMap.
and [<Sealed>] internal PAMSeq(meta: IPersistentMap, _arr: obj array, _index: int) =
    inherit ASeq(meta)

    new(a, i) = PAMSeq(null, a, i)

    interface ISeq with
        override _.first() =
            MapEntry.create (_arr[_index], _arr[_index + 1])

        override _.next() =
            if _index + 2 < _arr.Length then
                PAMSeq(_arr, _index + 2)
            else
                null

    interface IPersistentCollection with
        override _.count() = (_arr.Length - _index) / 2

    interface IObj with
        override this.withMeta(m) =
            if meta = m then this else PAMSeq(m, _arr, _index)


    interface IReduce with
        override _.reduce(f) =
            if _index < _arr.Length then
                let rec loop (acc: obj) (i: int) =
                    if i >= _arr.Length then
                        acc
                    else
                        match f.invoke (acc, MapEntry.create (_arr[i], _arr[i + 1])) with
                        | :? Reduced as red -> (red :> IDeref).deref ()
                        | nextAcc -> loop nextAcc (i + 2)

                loop (MapEntry.create (_arr[_index], _arr[_index + 1])) (_index + 2)
            else
                f.invoke ()

    interface IReduceInit with
        override _.reduce(f, start) =
            let rec loop (acc: obj) (i: int) =
                if i >= _arr.Length then
                    acc
                else
                    match f.invoke (acc, MapEntry.create (_arr[i], _arr[i + 1])) with
                    | :? Reduced as red -> (red :> IDeref).deref ()
                    | nextAcc -> loop nextAcc (i + 2)

            loop start _index


    interface IDrop with
        member this.drop(n) =
            if n < (this :> IPersistentCollection).count () then
                PAMSeq(_arr, _index + 2 * n) :> Sequential
            else
                null

    interface IEnumerable<obj> with
        override _.GetEnumerator() =
            let s =
                seq { for i in _index..2 .. _arr.Length - 1 -> MapEntry.create (_arr[i], _arr[i + 1]) :> obj }

            s.GetEnumerator()

    interface IEnumerable with
        override this.GetEnumerator() =
            (this :> IEnumerable<obj>).GetEnumerator()


and KVMangleFn<'T> = obj * obj -> 'T

/// The interface for nodes in a PersistentHashMap
and [<AllowNullLiteral>] INode =

    abstract assoc: shift: int * hash: int * key: obj * value: obj * addedLeaf: BoolBox -> INode
    abstract without: shift: int * hash: int * key: obj -> INode
    abstract find: shift: int * hash: int * key: obj -> IMapEntry
    abstract find: shift: int * hash: int * key: obj * notFound: obj -> obj
    abstract getNodeSeq: unit -> ISeq

    abstract assoc: edit: AtomicBoolean * shift: int * hash: int * key: obj * value: obj * addedLeaf: BoolBox -> INode

    abstract without: edit: AtomicBoolean * shift: int * hash: int * key: obj * removedLeaf: BoolBox -> INode
    abstract kvReduce: fn: IFn * init: obj -> obj
    abstract fold: combinef: IFn * reducef: IFn * fjtask: IFn * fjfork: IFn * fjjoin: IFn -> obj
    abstract iterator: d: KVMangleFn<obj> -> IEnumerator
    abstract iteratorT: d: KVMangleFn<'T> -> IEnumerator<'T>

    abstract printContents: prefix: string -> unit (* debug helper *)

/// Helper method for implementing INodes in an PersistentHashMap
and [<AbstractClass; Sealed>] private NodeOps() =

    static member cloneAndSet(arr: 'T array, i: int, a: 'T) : 'T array =
        let clone: 'T array = downcast arr.Clone()
        clone[i] <- a
        clone


    static member cloneAndSet2(arr: 'T array, i: int, a: 'T, j: int, b: 'T) : 'T array =
        let clone: 'T array = downcast arr.Clone()
        clone[i] <- a
        clone[j] <- b
        clone

    static member removePair(arr: 'T array, i: int) : 'T array =
        let newArr: 'T array = Array.zeroCreate <| arr.Length - 2
        Array.Copy(arr, 0, newArr, 0, 2 * i)
        Array.Copy(arr, 2 * (i + 1), newArr, 2 * i, newArr.Length - 2 * i)
        newArr

    // Random goodness

    static member hash(k: obj) = Hashing.hasheq k
    static member mask(hash: int, shift: int) = (hash >>> shift) &&& 0x01f
    static member bitPos(hash, shift) = 1 <<< NodeOps.mask (hash, shift)

    static member bitCount(x: int) =
        let x = x - ((x >>> 1) &&& 0x55555555)
        let x = (((x >>> 2) &&& 0x33333333) + (x &&& 0x33333333))
        let x = (((x >>> 4) + x) &&& 0x0f0f0f0f)
        (x * 0x01010101) >>> 24

    static member bitIndex(bitmap, bit) = NodeOps.bitCount (bitmap &&& (bit - 1)) // Not used?

    static member findIndex(key: obj, items: obj[], count: int) : int =
        seq { 0..2 .. 2 * count - 1 }
        |> Seq.tryFindIndex (fun i -> Util.equiv (key, items[i]))
        |> Option.defaultValue -1

    // Debug helper
    static member nodeTypeDesignator(node: INode) =
        match node with
        | :? ArrayNode -> "A"
        | :? BitmapIndexedNode -> "B"
        | :? HashCollisionNode -> "C"
        | _ -> "?"

/// Implements iteration over the nodes in a PersistentHashMap
and [<AbstractClass; Sealed>] private NodeIter() =
    static member getEnumerator(array: obj[], d: KVMangleFn<obj>) : IEnumerator =
        let s =
            seq {
                for i in 0..2 .. array.Length - 1 do
                    let key = array[i]
                    let nodeOrVal = array[i + 1]

                    if not (isNull key) then
                        yield d (key, nodeOrVal)
                    elif not (isNull nodeOrVal) then
                        let ie = (nodeOrVal :?> INode).iterator (d)

                        while ie.MoveNext() do
                            yield ie.Current
            }

        s.GetEnumerator() :> IEnumerator

    static member getEnumeratorT(array: obj[], d: KVMangleFn<'T>) : IEnumerator<'T> =
        let s =
            seq {
                for i in 0..2 .. array.Length - 1 do
                    let key = array[i]
                    let nodeOrVal = array[i + 1]

                    if not (isNull key) then
                        yield d (key, nodeOrVal)
                    elif not (isNull nodeOrVal) then
                        let ie = (nodeOrVal :?> INode).iteratorT (d)

                        while ie.MoveNext() do
                            yield ie.Current
            }

        s.GetEnumerator()


/// An immutable, persistent map implemented as a hash array mapped trie.
and PersistentHashMap private (_meta: IPersistentMap, _count: int, _root: INode, _hasNull: bool, _nullValue: obj) =
    inherit APersistentMap()

    // A persistent rendition of Phil Bagwell's Hash Array Mapped Trie
    //
    // Uses path copying for persistence.
    // HashCollision leaves vs extended hashing
    // Node polymorphism vs conditionals
    // No sub-tree pools or root-resizing
    // Any errors are Rich Hickey's (so he says), except those that I introduced

    // A PersistentHashMap consists of a head node representing the map that has a points to a tree of nodes containing the key/value pairs.
    // The head node indicated if null is a key and holds the associated value.
    // Thus the tree is guaranteed not to contain a null key, allowing null to be used as an 'empty field' indicator.
    // The tree contains three kinds of nodes:
    //     ArrayNode
    //     BitmapIndexedNode
    //     HashCollisionNode
    //
    // This arrangement seems ideal for a discriminated union, but we need mutable fields
    // (required to implement IEditableCollection and the ITransientXxx interfaces in-place)
    // and DUs don't support that.  Perhaps some smarter than me can do this someday.

    new(count, root, hasNull, nullValue) = PersistentHashMap(null, count, root, hasNull, nullValue)

    member internal _.Meta = _meta
    member internal _.Count = _count
    member internal _.Root = _root
    member internal _.HasNull = _hasNull
    member internal _.NullValue = _nullValue

    static member val Empty = PersistentHashMap(null, 0, null, false, null)

    static member val internal notFoundValue = obj ()

    // factories

    static member createD2(other: IDictionary<'TKey, 'TValue>) : IPersistentMap =
        let mutable ret =
            (PersistentHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        for de in Seq.cast<KeyValuePair<'TKey, 'TValue>> other do
            ret <- ret.assoc (de.Key, de.Value)

        ret.persistent ()

    static member create(other: IDictionary) : IPersistentMap =
        let mutable ret =
            (PersistentHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        for de in Seq.cast<DictionaryEntry> other do
            ret <- ret.assoc (de.Key, de.Value)

        ret.persistent ()

    static member create([<ParamArray>] init: obj[]) : PersistentHashMap =
        let mutable ret =
            (PersistentHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        for i in 0..2 .. init.Length - 1 do
            ret <- ret.assoc (init[i], init[i + 1])

        downcast ret.persistent ()

    static member createWithCheck([<ParamArray>] init: obj[]) : PersistentHashMap =
        let mutable ret =
            (PersistentHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

        let mutable i = 0

        while i < init.Length - 1 do
            ret <- ret.assoc (init[i], init[i + 1])
            i <- i + 2

            if ret.count () <> i / 2 + 1 then
                raise <| ArgumentException("init", "Duplicate key: " + init[ i ].ToString())

        downcast ret.persistent ()

    static member create1(init: IList) : PersistentHashMap =
        let mutable ret =
            (PersistentHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

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
            (PersistentHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

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
            (PersistentHashMap.Empty :> IEditableCollection).asTransient () :?> ITransientMap

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
        (PersistentHashMap.create (init) :> IObj).withMeta (meta) :?> PersistentHashMap


    interface IMeta with
        override _.meta() = _meta

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m _meta then
                this
            else
                PersistentHashMap(m, _count, _root, _hasNull, _nullValue)

    interface ILookup with
        override this.valAt(k) = (this :> ILookup).valAt (k, null)

        override _.valAt(k, nf) =
            if isNull k then
                if _hasNull then _nullValue else nf
            elif isNull _root then
                null
            else
                _root.find (0, NodeOps.hash (k), k, nf)

    interface Associative with
        override _.containsKey(k) =
            if isNull k then
                _hasNull
            else
                (not (isNull _root))
                && not
                   <| LanguagePrimitives.PhysicalEquality
                       (_root.find (0, NodeOps.hash (k), k, PersistentHashMap.notFoundValue))
                       PersistentHashMap.notFoundValue

        override _.entryAt(k) =
            if isNull k then
                if _hasNull then
                    upcast MapEntry.create (null, _nullValue)
                else
                    null
            elif isNull _root then
                null
            else
                _root.find (0, NodeOps.hash (k), k)


    interface Seqable with
        override _.seq() =
            let s = if isNull _root then null else _root.getNodeSeq ()

            if _hasNull then
                upcast Cons(MapEntry.create (null, _nullValue), s)
            else
                s


    interface IPersistentCollection with
        override _.count() = _count

        override _.empty() =
            (PersistentHashMap.Empty :> IObj).withMeta (_meta) :?> IPersistentCollection

    interface Counted with
        override _.count() = _count

    interface IPersistentMap with
        override this.assoc(k, v) =
            if isNull k then
                if _hasNull && LanguagePrimitives.PhysicalEquality v _nullValue then
                    upcast this
                else
                    upcast PersistentHashMap(_meta, (if _hasNull then _count else _count + 1), _root, true, v)
            else
                let addedLeaf = BoolBox()

                let rootToUse: INode =
                    if isNull _root then
                        upcast BitmapIndexedNode.Empty
                    else
                        _root

                let newRoot = rootToUse.assoc (0, NodeOps.hash (k), k, v, addedLeaf)

                if LanguagePrimitives.PhysicalEquality newRoot _root then
                    upcast this
                else
                    upcast
                        PersistentHashMap(
                            _meta,
                            (if addedLeaf.isSet then _count + 1 else _count),
                            newRoot,
                            _hasNull,
                            _nullValue
                        )

        override this.assocEx(k, v) =
            if (this :> Associative).containsKey (k) then
                raise <| InvalidOperationException("Key already present")

            (this :> IPersistentMap).assoc (k, v)

        override this.without(k) =
            if isNull k then
                if _hasNull then
                    upcast PersistentHashMap(_meta, _count - 1, _root, false, null)
                else
                    upcast this
            elif isNull _root then
                upcast this
            else
                let newRoot = _root.without (0, NodeOps.hash (k), k)

                if LanguagePrimitives.PhysicalEquality newRoot _root then
                    upcast this
                else
                    upcast PersistentHashMap(_meta, _count - 1, newRoot, _hasNull, _nullValue)

        override _.count() = _count

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

    member _.MakeEnumerator(d: KVMangleFn<Object>) : IEnumerator =
        let rootIter =
            if isNull _root then
                PersistentHashMap.emptyEnumerator ()
            else
                _root.iteratorT (d)

        if _hasNull then
            upcast PersistentHashMap.nullEnumerator (d, _nullValue, rootIter)
        else
            upcast rootIter

    member _.MakeEnumeratorT<'T>(d: KVMangleFn<'T>) =
        let rootIter =
            if isNull _root then
                PersistentHashMap.emptyEnumerator ()
            else
                _root.iteratorT (d)

        if _hasNull then
            PersistentHashMap.nullEnumeratorT (d, _nullValue, rootIter)
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
            let init = if _hasNull then f.invoke (init, null, _nullValue) else init

            match init with
            | :? Reduced as r -> (r :> IDeref).deref ()
            | _ ->
                if not (isNull _root) then
                    match _root.kvReduce (f, init) with
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

                    if not (isNull _root) then
                        ret <- combinef.invoke (ret, _root.fold (combinef, reducef, fjtask, fjfork, fjjoin))

                    if _hasNull then
                        combinef.invoke (ret, reducef.invoke (combinef.invoke (), null, _nullValue))
                    else
                        ret)
            )

        fjinvoke.invoke (top)

    static member internal createNode(shift: int, key1: obj, val1: obj, key2hash: int, key2: obj, val2: obj) : INode =
        let key1hash = NodeOps.hash (key1)

        if key1hash = key2hash then
            upcast HashCollisionNode(null, key1hash, 2, [| key1; val1; key2; val2 |])
        else
            let box = BoolBox()
            let edit = AtomicBoolean()

            (BitmapIndexedNode.Empty :> INode)
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
            upcast HashCollisionNode(null, key1hash, 2, [| key1; val1; key2; val2 |])
        else
            let box = BoolBox()

            (BitmapIndexedNode.Empty :> INode)
                .assoc(edit, shift, key1hash, key1, val1, box)
                .assoc (edit, shift, key2hash, key2, val2, box)

    member _.printContents() =
        printfn "PersistentHashMap: count=%d, hasNull=%b" _count _hasNull

        if not (isNull _root) then
            _root.printContents ("*" + NodeOps.nodeTypeDesignator (_root) + ":")


/// Implements a transient hash map.
and private TransientHashMap(e, r, c, hn, nv) =
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

    new(m: PersistentHashMap) = TransientHashMap(AtomicBoolean(true), m.Root, m.Count, m.HasNull, m.NullValue)

    override this.doAssoc(k, v) =
        if isNull k then
            if not <| LanguagePrimitives.PhysicalEquality nullValue v then
                nullValue <- v

            if not hasNull then
                count <- count + 1
                hasNull <- true
        else
            leafFlag.reset ()

            let n =
                (if isNull root then
                     (BitmapIndexedNode.Empty :> INode)
                 else
                     root)
                    .assoc (edit, 0, NodeOps.hash (k), k, v, leafFlag)

            if not <| LanguagePrimitives.PhysicalEquality n root then
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

            if not <| LanguagePrimitives.PhysicalEquality n root then
                root <- n

            if leafFlag.isSet then
                count <- count - 1

        this

    override _.doCount() = count

    override _.doPersistent() =
        edit.Set(false)
        upcast PersistentHashMap(count, root, hasNull, nullValue)

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

/// A node in a PersistentHashMap that contains an array of values.
and [<Sealed>] private ArrayNode(_edit: AtomicBoolean, count: int, _array: INode array) =

    let mutable _count: int = count

    member private _.setNode(i, n) = _array[i] <- n
    member private _.incrementCount() = _count <- _count + 1
    member private _.decrementCount() = _count <- _count - 1

    // TODO: Do this with some sequence functions?
    member _.pack(edit: AtomicBoolean, idx) : INode =
        let newArray: obj[] = Array.zeroCreate <| 2 * (_count - 1)
        let mutable j = 1
        let mutable bitmap = 0

        for i = 0 to idx - 1 do
            if not (isNull _array[i]) then
                newArray[j] <- upcast _array[i]
                bitmap <- bitmap ||| (1 <<< i)
                j <- j + 2

        for i = idx + 1 to _array.Length - 1 do
            if not (isNull _array[i]) then
                newArray[j] <- upcast _array[i]
                bitmap <- bitmap ||| (1 <<< i)
                j <- j + 2

        upcast BitmapIndexedNode(edit, bitmap, newArray)


    member this.ensureEditable(e) =
        if LanguagePrimitives.PhysicalEquality _edit e then
            this
        else
            ArrayNode(e, _count, _array.Clone() :?> INode[])

    member this.editAndSet(e, i, n) =
        let editable = this.ensureEditable (e)
        editable.setNode (i, n)
        editable


    interface INode with
        member this.assoc(shift, hash, key, value, addedLeaf) =
            let idx = NodeOps.mask (hash, shift)
            let node = _array[idx]

            if isNull node then
                upcast
                    ArrayNode(
                        null,
                        _count + 1,
                        NodeOps.cloneAndSet (
                            _array,
                            idx,
                            (BitmapIndexedNode.Empty :> INode)
                                .assoc (shift + 5, hash, key, value, addedLeaf)
                        )
                    )
            else
                let n = node.assoc (shift + 5, hash, key, value, addedLeaf)

                if LanguagePrimitives.PhysicalEquality n node then
                    upcast this
                else
                    upcast ArrayNode(null, _count, NodeOps.cloneAndSet (_array, idx, n))

        member this.without(shift, hash, key) =
            let idx = NodeOps.mask (hash, shift)
            let node = _array[idx]

            if isNull node then
                upcast this
            else
                let n = node.without (shift + 5, hash, key)

                if LanguagePrimitives.PhysicalEquality n node then
                    upcast this
                elif isNull n then
                    if _count <= 8 then // shrink
                        this.pack (null, idx)
                    else
                        upcast ArrayNode(null, _count - 1, NodeOps.cloneAndSet (_array, idx, n))
                else
                    upcast ArrayNode(null, _count, NodeOps.cloneAndSet (_array, idx, n))

        member _.find(shift, hash, key) =
            let idx = NodeOps.mask (hash, shift)
            let node = _array[idx]

            match node with
            | null -> null
            | _ -> node.find (shift + 5, hash, key)

        member _.find(shift, hash, key, nf) =
            let idx = NodeOps.mask (hash, shift)
            let node = _array[idx]

            match node with
            | null -> nf
            | _ -> node.find (shift + 5, hash, key, nf)

        member _.getNodeSeq() = ArrayNodeSeq.create (_array)

        member this.assoc(e, shift, hash, key, value, addedLeaf) =
            let idx = NodeOps.mask (hash, shift)
            let node = _array[idx]

            if isNull node then
                let editable =
                    this.editAndSet (
                        e,
                        idx,
                        (BitmapIndexedNode.Empty :> INode)
                            .assoc (e, shift + 5, hash, key, value, addedLeaf)
                    )

                editable.incrementCount ()
                upcast editable
            else
                let n = node.assoc (e, shift + 5, hash, key, value, addedLeaf)

                if LanguagePrimitives.PhysicalEquality n node then
                    upcast this
                else
                    upcast this.editAndSet (e, idx, n)

        member this.without(e, shift, hash, key, removedLeaf) =
            let idx = NodeOps.mask (hash, shift)
            let node = _array[idx]

            if isNull node then
                upcast this
            else
                let n = node.without (e, shift + 5, hash, key, removedLeaf)

                if LanguagePrimitives.PhysicalEquality n node then
                    upcast this
                elif isNull n then
                    if _count <= 8 then // shrink
                        this.pack (e, idx)
                    else
                        let editable = this.editAndSet (e, idx, n)
                        editable.decrementCount ()
                        upcast editable
                else
                    upcast this.editAndSet (e, idx, n)

        member _.kvReduce(f, init) =
            let rec loop (i: int) (v: obj) =
                if i >= _array.Length then
                    v
                else
                    match _array[i] with
                    | null -> loop (i + 1) v
                    | n ->
                        let nextv = n.kvReduce (f, v)
                        if nextv :? Reduced then nextv else loop (i + 1) nextv

            loop 0 init

        member _.fold(combinef, reducef, fjtask, fjfork, fjjoin) =
            let tasks =
                _array
                |> Array.filter (fun node -> not <| isNull node)
                |> Array.map (fun node -> Func<obj>((fun () -> node.fold (combinef, reducef, fjtask, fjfork, fjjoin))))

            ArrayNode.foldTasks (tasks, combinef, fjtask, fjfork, fjjoin)

        member _.iterator(d) =
            let s =
                seq {
                    for node in _array do
                        if not (isNull node) then
                            let ie = node.iterator (d)

                            while ie.MoveNext() do
                                yield ie.Current
                }

            s.GetEnumerator() :> IEnumerator

        member _.iteratorT(d) =
            let s =
                seq {
                    for node in _array do
                        if not (isNull node) then
                            let ie = node.iteratorT (d)

                            while ie.MoveNext() do
                                yield ie.Current
                }

            s.GetEnumerator()

        member this.printContents(prefix) : unit =
            let sb = StringBuilder(prefix)

            for i = 0 to _array.Length - 1 do
                let node = _array.[i]

                if not (isNull node) then
                    sb.AppendFormat(" {0}:{1}", i, NodeOps.nodeTypeDesignator (node)) |> ignore

            printfn "%s" (sb.ToString())


            for i = 0 to _array.Length - 1 do
                let node = _array.[i]

                if not (isNull node) then
                    let newPrefix =
                        prefix + "-" + i.ToString() + ":" + NodeOps.nodeTypeDesignator (node) + ":"

                    node.printContents (newPrefix)


    static member foldTasks(tasks: Func<obj>[], combinef: IFn, fjtask: IFn, fjfork: IFn, fjjoin: IFn) =
        match tasks.Length with
        | 0 -> combinef.invoke ()
        | 1 -> tasks[ 0 ].Invoke()
        | _ ->
            let halves = tasks |> Array.splitInto 2

            let fn =
                Func<obj>(fun () -> ArrayNode.foldTasks (halves[1], combinef, fjtask, fjfork, fjjoin))

            let forked = fjfork.invoke (fjtask.invoke (fn))
            combinef.invoke (ArrayNode.foldTasks (halves[0], combinef, fjtask, fjfork, fjjoin), fjjoin.invoke (forked))

/// ISeq for an array of INodes.
and private ArrayNodeSeq(meta, nodes: INode[], i: int, s: ISeq) =
    inherit ASeq(meta)

    static member create(meta: IPersistentMap, nodes: INode[], i: int, s: ISeq) : ISeq =
        match s with
        | null ->
            let result =
                nodes
                |> Seq.indexed
                |> Seq.skip i
                |> Seq.filter (fun (j, node) -> not (isNull node))
                |> Seq.tryPick (fun (j, node) ->
                    let ns = node.getNodeSeq ()

                    if (isNull ns) then
                        None
                    else
                        ArrayNodeSeq(meta, nodes, j + 1, ns) |> Some)

            match result with
            | Some s -> upcast s
            | None -> null
        | _ -> upcast ArrayNodeSeq(meta, nodes, i, s)

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m ((this :> IMeta).meta ()) then
                upcast this
            else
                upcast ArrayNodeSeq(m, nodes, i, s)

    interface ISeq with
        member _.first() = s.first ()

        member _.next() =
            ArrayNodeSeq.create (null, nodes, i, s.next ())

    static member create(nodes: INode[]) =
        ArrayNodeSeq.create (null, nodes, 0, null)

/// A node in a PersistentHashMap that allows efficient packing
and [<Sealed; AllowNullLiteral>] internal BitmapIndexedNode(_edit: AtomicBoolean, bitmap: int, array: obj array) =

    let mutable _bitmap: int = bitmap
    let mutable _array: obj array = array

    static member val Empty: BitmapIndexedNode = BitmapIndexedNode(null, 0, Array.empty<obj>)

    member _.index(bit: int) : int =
        NodeOps.bitCount (_bitmap &&& (bit - 1))

    member x.Bitmap
        with private get () = _bitmap
        and private set (v) = _bitmap <- v

    member private _.setArrayVal(i, v) = _array[i] <- v

    member _.Array
        with private get () = _array
        and private set (v) = _array <- v


    interface INode with
        member this.assoc(shift, hash, key, value, addedLeaf) =
            let bit = NodeOps.bitPos (hash, shift)
            let idx = this.index (bit)

            if _bitmap &&& bit = 0 then
                let n = NodeOps.bitCount (_bitmap)

                if n >= 16 then
                    let nodes: INode[] = Array.zeroCreate 32
                    let jdx = NodeOps.mask (hash, shift)

                    nodes[jdx] <-
                        (BitmapIndexedNode.Empty :> INode)
                            .assoc (shift + 5, hash, key, value, addedLeaf)

                    let mutable j = 0

                    for i = 0 to 31 do
                        if ((_bitmap >>> i) &&& 1) <> 0 then
                            nodes[i] <-
                                if isNull _array[j] then
                                    _array[j + 1] :?> INode
                                else
                                    (BitmapIndexedNode.Empty :> INode)
                                        .assoc (
                                            shift + 5,
                                            NodeOps.hash (_array[j]),
                                            _array[j],
                                            _array[j + 1],
                                            addedLeaf
                                        )

                            j <- j + 2

                    upcast ArrayNode(null, n + 1, nodes)
                else
                    let newArray: obj[] = 2 * (n + 1) |> Array.zeroCreate
                    Array.Copy(_array, 0, newArray, 0, 2 * idx)
                    newArray[2 * idx] <- key
                    addedLeaf.set ()
                    newArray[2 * idx + 1] <- value
                    Array.Copy(_array, 2 * idx, newArray, 2 * (idx + 1), 2 * (n - idx))
                    upcast BitmapIndexedNode(null, (_bitmap ||| bit), newArray)
            else
                let keyOrNull = _array[2 * idx]
                let valOrNode = _array[2 * idx + 1]

                if isNull keyOrNull then
                    let existingNode = (valOrNode :?> INode)
                    let n = existingNode.assoc (shift + 5, hash, key, value, addedLeaf)

                    if LanguagePrimitives.PhysicalEquality n existingNode then
                        upcast this
                    else
                        upcast BitmapIndexedNode(null, _bitmap, NodeOps.cloneAndSet (_array, 2 * idx + 1, upcast n))
                elif Util.equiv (key, keyOrNull) then
                    if LanguagePrimitives.PhysicalEquality value valOrNode then
                        upcast this
                    else
                        upcast BitmapIndexedNode(null, _bitmap, NodeOps.cloneAndSet (_array, 2 * idx + 1, value))
                else
                    addedLeaf.set ()

                    upcast
                        BitmapIndexedNode(
                            null,
                            _bitmap,
                            NodeOps.cloneAndSet2 (
                                _array,
                                2 * idx,
                                null,
                                2 * idx + 1,
                                upcast PersistentHashMap.createNode (shift + 5, keyOrNull, valOrNode, hash, key, value)
                            )
                        )

        member this.without(shift, hash, key) =
            let bit = NodeOps.bitPos (hash, shift)

            if (_bitmap &&& bit) = 0 then
                upcast this
            else
                let idx = this.index (bit)
                let keyOrNull = _array[2 * idx]
                let valOrNode = _array[2 * idx + 1]

                if isNull keyOrNull then
                    let existingNode = (valOrNode :?> INode)
                    let n = existingNode.without (shift + 5, hash, key)

                    if LanguagePrimitives.PhysicalEquality n existingNode then
                        upcast this
                    elif not (isNull n) then
                        upcast BitmapIndexedNode(null, _bitmap, NodeOps.cloneAndSet (_array, 2 * idx + 1, upcast n))
                    elif _bitmap = bit then
                        null
                    else
                        upcast BitmapIndexedNode(null, _bitmap ^^^ bit, NodeOps.removePair (_array, idx))
                elif Util.equiv (key, keyOrNull) then
                    if _bitmap = bit then
                        null
                    else
                        upcast BitmapIndexedNode(null, _bitmap ^^^ bit, NodeOps.removePair (_array, idx))
                else
                    upcast this

        member this.find(shift, hash, key) =
            let bit = NodeOps.bitPos (hash, shift)

            if (_bitmap &&& bit) = 0 then
                null
            else
                let idx = this.index (bit)
                let keyOrNull = _array[2 * idx]
                let valOrNode = _array[2 * idx + 1]

                if isNull keyOrNull then
                    (valOrNode :?> INode).find (shift + 5, hash, key)
                elif Util.equiv (key, keyOrNull) then
                    upcast MapEntry.create (keyOrNull, valOrNode)
                else
                    null

        member this.find(shift, hash, key, nf) =
            let bit = NodeOps.bitPos (hash, shift)

            if (_bitmap &&& bit) = 0 then
                nf
            else
                let idx = this.index (bit)
                let keyOrNull = _array[2 * idx]
                let valOrNode = _array[2 * idx + 1]

                if isNull keyOrNull then
                    (valOrNode :?> INode).find (shift + 5, hash, key, nf)
                elif Util.equiv (key, keyOrNull) then
                    valOrNode
                else
                    nf

        member _.getNodeSeq() = NodeSeq.create (_array)

        member this.assoc(e, shift, hash, key, value, addedLeaf) =
            let bit = NodeOps.bitPos (hash, shift)
            let idx = this.index (bit)

            if (_bitmap &&& bit) <> 0 then
                let keyOrNull = _array[2 * idx]
                let valOrNode = _array[2 * idx + 1]

                if isNull keyOrNull then
                    let existingNode = valOrNode :?> INode
                    let n = existingNode.assoc (e, shift + 5, hash, key, value, addedLeaf)

                    if LanguagePrimitives.PhysicalEquality n existingNode then
                        this
                    else
                        this.editAndSet (e, 2 * idx + 1, n)

                elif Util.equiv (key, keyOrNull) then
                    if LanguagePrimitives.PhysicalEquality value valOrNode then
                        this
                    else
                        this.editAndSet (e, 2 * idx + 1, value)
                else
                    addedLeaf.set ()

                    upcast
                        this.editAndSet (
                            e,
                            2 * idx,
                            null,
                            2 * idx + 1,
                            PersistentHashMap.createNode (e, shift + 5, keyOrNull, valOrNode, hash, key, value)
                        )
            else
                let n = NodeOps.bitCount _bitmap

                if n * 2 < _array.Length then
                    addedLeaf.set ()
                    let editable = this.ensureEditable (e)
                    Array.Copy(editable.Array, 2 * idx, editable.Array, 2 * (idx + 1), 2 * (n - idx))
                    editable.setArrayVal (2 * idx, key)
                    editable.setArrayVal (2 * idx + 1, value)
                    editable.Bitmap <- editable.Bitmap ||| bit
                    upcast editable
                elif n >= 16 then
                    let nodes: INode[] = Array.zeroCreate 32
                    let jdx = NodeOps.mask (hash, shift)

                    nodes[jdx] <-
                        (BitmapIndexedNode.Empty :> INode)
                            .assoc (e, shift + 5, hash, key, value, addedLeaf)

                    let mutable j = 0

                    for i = 0 to 31 do
                        if ((_bitmap >>> i) &&& 1) <> 0 then
                            if isNull _array[j] then
                                nodes[i] <- _array[j + 1] :?> INode
                            else
                                nodes[i] <-
                                    (BitmapIndexedNode.Empty :> INode)
                                        .assoc (
                                            e,
                                            shift + 5,
                                            NodeOps.hash (_array[j]),
                                            _array[j],
                                            _array[j + 1],
                                            addedLeaf
                                        )

                            j <- j + 2

                    upcast ArrayNode(e, n + 1, nodes)
                else
                    let newArray: obj[] = 2 * (n + 4) |> Array.zeroCreate
                    Array.Copy(_array, 0, newArray, 0, 2 * idx)
                    newArray[2 * idx] <- key
                    newArray[2 * idx + 1] <- value
                    addedLeaf.set ()
                    Array.Copy(_array, 2 * idx, newArray, 2 * (idx + 1), 2 * (n - idx))
                    let editable = this.ensureEditable (e)
                    editable.Array <- newArray
                    editable.Bitmap <- editable.Bitmap ||| bit
                    upcast editable

        member this.without(e, shift, hash, key, removedLeaf) =
            let bit = NodeOps.bitPos (hash, shift)

            if (_bitmap &&& bit) = 0 then
                upcast this
            else
                let idx = this.index (bit)
                let keyOrNull = _array[2 * idx]
                let valOrNode = _array[2 * idx + 1]

                if isNull keyOrNull then
                    let existingNode = (valOrNode :?> INode)
                    let n = existingNode.without (e, shift + 5, hash, key, removedLeaf)

                    if LanguagePrimitives.PhysicalEquality n existingNode then
                        upcast this
                    elif not (isNull n) then
                        upcast this.editAndSet (e, 2 * idx + 1, n)
                    elif _bitmap = bit then
                        null
                    else
                        upcast this.editAndRemovePair (e, bit, idx)
                elif Util.equiv (key, keyOrNull) then
                    removedLeaf.set ()
                    upcast this.editAndRemovePair (e, bit, idx)
                else
                    upcast this

        member _.kvReduce(f, init) = NodeSeq.kvReduce (_array, f, init)

        member _.fold(combinef, reducef, fjtask, fjfork, fjjoin) =
            NodeSeq.kvReduce (_array, reducef, combinef.invoke ())

        member _.iterator(d) = NodeIter.getEnumerator (_array, d)

        member _.iteratorT(d) = NodeIter.getEnumeratorT (_array, d)

        member this.printContents(prefix) : unit =
            let sb = StringBuilder(prefix)

            let mutable j = 0

            for i = 0 to 31 do
                if ((_bitmap >>> i) &&& 1) <> 0 then
                    if isNull _array[j] then
                        // we have a node
                        let child = _array.[j + 1] :?> INode
                        sb.AppendFormat(" {0}:{1}", i, NodeOps.nodeTypeDesignator (child)) |> ignore
                    else
                        // we have a key-value pair
                        sb.AppendFormat(" {0} = Key {1}, ", i, _array[j]) |> ignore

                    j <- j + 2

            printfn "%s" (sb.ToString())

            j <- 0

            for i = 0 to 31 do
                if ((_bitmap >>> i) &&& 1) <> 0 then
                    if isNull _array[j] then
                        // we have a node
                        let child = _array.[j + 1] :?> INode

                        let newPrefix =
                            prefix + "-" + i.ToString() + ":" + NodeOps.nodeTypeDesignator (child) + ":"

                        child.printContents (newPrefix)

                    j <- j + 2


    member this.ensureEditable(e: AtomicBoolean) : BitmapIndexedNode =
        if LanguagePrimitives.PhysicalEquality _edit e then
            this
        else
            let n = NodeOps.bitCount (_bitmap)

            let newArray: obj[] = Array.zeroCreate (if n >= 0 then 2 * (n + 1) else 4) // make room for next assoc

            Array.Copy(_array, newArray, 2 * n)
            BitmapIndexedNode(e, _bitmap, newArray)

    member private this.editAndSet(e: AtomicBoolean, i: int, a: obj) : BitmapIndexedNode =
        let editable = this.ensureEditable (e)
        editable.setArrayVal (i, a)
        editable


    member private this.editAndSet(e: AtomicBoolean, i: int, a: obj, j: int, b: obj) : BitmapIndexedNode =
        let editable = this.ensureEditable (e)
        editable.setArrayVal (i, a)
        editable.setArrayVal (j, b)
        editable

    member private this.editAndRemovePair(e: AtomicBoolean, bit: int, i: int) : BitmapIndexedNode =
        if _bitmap = bit then
            null
        else
            let editable = this.ensureEditable (e)
            editable.Bitmap <- editable.Bitmap ^^^ bit
            Array.Copy(editable.Array, 2 * (i + 1), editable.Array, 2 * i, editable.Array.Length - 2 * (i + 1))
            editable.setArrayVal (editable.Array.Length - 2, null)
            editable.setArrayVal (editable.Array.Length - 1, null)
            editable

/// A node in a PersistentHashMap for keys that hash to the same value.
and HashCollisionNode(_edit: AtomicBoolean, _hash: int, count: int, array: obj array) =

    let mutable _count: int = count
    let mutable _array: obj array = array

    member _.Array
        with private get () = _array
        and private set (a) = _array <- a

    member _.Count
        with private get () = _count
        and private set (c) = _count <- c

    member _.tryFindIndex(key: obj) : int option =
        let rec loop (i: int) =
            if i >= 2 * _count then None
            elif Util.equiv (key, _array[i]) then Some i
            else i + 2 |> loop

        loop 0


    interface INode with
        member this.assoc(shift, h, key, value, addedLeaf) =
            if h = _hash then
                match this.tryFindIndex (key) with
                | Some idx ->
                    if LanguagePrimitives.PhysicalEquality _array[idx + 1] value then
                        upcast this
                    else
                        upcast HashCollisionNode(null, h, _count, NodeOps.cloneAndSet (_array, idx + 1, value))
                | None ->
                    let newArray: obj[] = 2 * (_count + 1) |> Array.zeroCreate
                    Array.Copy(_array, 0, newArray, 0, 2 * _count)
                    newArray[2 * _count] <- key
                    newArray[2 * _count + 1] <- value
                    addedLeaf.set ()
                    upcast HashCollisionNode(_edit, h, _count + 1, newArray)
            else
                (BitmapIndexedNode(null, NodeOps.bitPos (_hash, shift), [| null; this |]) :> INode)
                    .assoc (shift, h, key, value, addedLeaf)

        member this.without(shift, h, key) =
            match this.tryFindIndex (key) with
            | None -> upcast this
            | Some idx ->
                if _count = 1 then
                    null
                else
                    upcast HashCollisionNode(null, h, _count - 1, NodeOps.removePair (_array, idx / 2))

        member this.find(shift, h, key) =
            match this.tryFindIndex (key) with
            | None -> null
            | Some idx -> upcast MapEntry.create (_array[idx], _array[idx + 1])

        member this.find(shift, h, key, nf) =
            match this.tryFindIndex (key) with
            | None -> nf
            | Some idx -> _array[idx + 1]

        member _.getNodeSeq() = NodeSeq.create (_array)

        member this.assoc(e, shift, h, key, value, addedLeaf) =
            if h = _hash then
                match this.tryFindIndex (key) with
                | Some idx ->
                    if LanguagePrimitives.PhysicalEquality _array[idx + 1] value then
                        upcast this
                    else
                        upcast this.editAndSet (e, idx + 1, value)
                | None ->
                    if _array.Length > 2 * _count then
                        addedLeaf.set ()

                        let editable = this.editAndSet (e, 2 * _count, key, 2 * _count + 1, value)

                        editable.Count <- editable.Count + 1
                        upcast editable
                    else
                        let newArray: obj[] = _array.Length + 2 |> Array.zeroCreate
                        Array.Copy(_array, 0, newArray, 0, _array.Length)
                        newArray[_array.Length] <- key
                        newArray[_array.Length + 1] <- value
                        addedLeaf.set ()
                        upcast this.ensureEditable (e, _count + 1, newArray)
            else
                (BitmapIndexedNode(null, NodeOps.bitPos (_hash, shift), [| null; this; null; null |]) :> INode)
                    .assoc (e, shift, h, key, value, addedLeaf)

        member this.without(e, shift, h, key, removedLeaf) =
            match this.tryFindIndex (key) with
            | None -> upcast this
            | Some idx ->
                removedLeaf.set ()

                if _count = 1 then
                    null
                else
                    let editable = this.ensureEditable (e)
                    editable.Array[idx] <- editable.Array[2 * _count - 2]
                    editable.Array[idx + 1] <- editable.Array[2 * _count - 1]
                    editable.Array[2 * _count - 2] <- null
                    editable.Array[2 * _count - 1] <- null
                    editable.Count <- editable.Count - 1
                    upcast editable

        member _.kvReduce(f, init) = NodeSeq.kvReduce (_array, f, init)

        member _.fold(combinef, reducef, fjtask, fjfork, fjjoin) =
            NodeSeq.kvReduce (_array, reducef, combinef.invoke ())

        member _.iterator(d) = NodeIter.getEnumerator (_array, d)

        member _.iteratorT(d) = NodeIter.getEnumeratorT (_array, d)

        member _.printContents(prefix) : unit =
            let sb = StringBuilder(prefix)

            for i = 0 to _array.Length - 1 do
                if i % 2 = 0 then
                    sb.AppendFormat(" {0}:Key {1}, ", i, _array[i]) |> ignore

            printfn "%s" (sb.ToString())


    member this.ensureEditable(e) =
        if LanguagePrimitives.PhysicalEquality e _edit then
            this
        else
            let newArray: obj[] = 2 * (_count + 1) |> Array.zeroCreate
            Array.Copy(_array, 0, newArray, 0, 2 * _count)
            HashCollisionNode(e, _hash, _count, newArray)

    member this.ensureEditable(e, c, a) =
        if LanguagePrimitives.PhysicalEquality e _edit then
            _array <- a
            _count <- c
            this
        else
            HashCollisionNode(e, _hash, c, a)

    member this.editAndSet(e, i, a) =
        let editable = this.ensureEditable (e)
        editable.Array[i] <- a
        editable

    member this.editAndSet(e, i, a, j, b) =
        let editable = this.ensureEditable (e)
        editable.Array[i] <- a
        editable.Array[j] <- b
        editable




and NodeSeq(meta: IPersistentMap, _array: obj array, _idx: int, _seq: ISeq) =
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
            if LanguagePrimitives.PhysicalEquality m ((this :> IMeta).meta ()) then
                upcast this
            else
                upcast NodeSeq(m, _array, _idx, _seq)

    interface ISeq with
        member _.first() =
            match _seq with
            | null -> upcast MapEntry.create (_array[_idx], _array[_idx + 1])
            | _ -> _seq.first ()

        member _.next() =
            match _seq with
            | null -> NodeSeq.create (_array, _idx + 2, null)
            | _ -> NodeSeq.create (_array, _idx, _seq.next ())

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
