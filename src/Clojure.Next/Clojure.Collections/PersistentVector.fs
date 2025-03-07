﻿namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics



////////////////////////////////
//
// IPVecSeq
//
////////////////////////////////

/// Implements an ISeq over an IPersistentVector
[<Sealed>]
type IPVecSeq(meta: IPersistentMap, _vec: IPersistentVector, _index: int) =
    inherit ASeq(meta)

    /// Create an IPVecSeq with null metadata
    new(vec, index) = IPVecSeq(null, vec, index)

    // TODO: something more efficient  (todo = from Java)

    interface ISeq with
        override _.first() = _vec.nth (_index)

        override _.next() =
            if _index + 1 < _vec.count () then
                IPVecSeq(_vec, _index + 1)
            else
                null

    interface IPersistentCollection with
        override _.count() = _vec.count () - _index

    interface Counted with
        override _.count() = _vec.count () - _index

    interface IndexedSeq with
        member this.index() = _index

    interface IObj with
        override this.withMeta(meta: IPersistentMap) =
            if (this :> IMeta).meta () = meta then
                this
            else
                IPVecSeq(meta, _vec, _index)

    member this.reducer(f: IFn, acc: obj, idx: int) =
        if idx >= _vec.count () then
            acc
        else
            match f.invoke (acc, _vec.nth (idx)) with
            | :? Reduced as red -> (red :> IDeref).deref ()
            | nextAcc -> this.reducer (f, nextAcc, idx + 1)

    interface IReduceInit with
        member this.reduce(f, init) =
            match this.reducer (f, f.invoke (init, _vec.nth (_index)), _index + 1) with
            | :? Reduced as red -> (red :> IDeref).deref ()
            | finalAcc -> finalAcc


    interface IReduce with
        member this.reduce(f) =
            this.reducer (f, _vec.nth (_index), _index + 1)



////////////////////////////////
//
//  IPVecRSeq
//
////////////////////////////////

/// Implements an ISeq over an IPersistentVector in reverse order
[<Sealed>]
type IPVecRSeq(meta: IPersistentMap, _vec: IPersistentVector, _index: int) =
    inherit ASeq(meta)

    // Create an IPVecRSeq with null metadata
    new(vec, index) = IPVecRSeq(null, vec, index)

    interface ISeq with
        override _.first() = _vec.nth (_index)

        override _.next() =
            if _index > 0 then IPVecRSeq(_vec, _index - 1) else null

    interface IPersistentCollection with
        override _.count() = _index + 1

    interface Counted with
        override _.count() = _index + 1

    interface IndexedSeq with
        member this.index() = _index

    interface IObj with
        override this.withMeta(meta: IPersistentMap) =
            if (this :> IMeta).meta () = meta then
                this
            else
                IPVecRSeq(meta, _vec, _index)

    // IReduce not in Java original

    member this.reducer(f: IFn, acc: obj, idx: int) =
        if idx < 0 then
            acc
        else
            match f.invoke (acc, _vec.nth (idx)) with
            | :? Reduced as red -> (red :> IDeref).deref ()
            | nextAcc -> this.reducer (f, nextAcc, idx - 1)

    interface IReduceInit with
        member this.reduce(f, init) =
            match this.reducer (f, f.invoke (init, _vec.nth (_index)), _index + 1) with
            | :? Reduced as red -> (red :> IDeref).deref ()
            | finalAcc -> finalAcc

    interface IReduce with
        member this.reduce(f) =
            this.reducer (f, _vec.nth (_index), _index + 1)


////////////////////////////////
//
// APersistentVector
//
////////////////////////////////

/// Abstract base class for persistent vectors.
[<AbstractClass>]
[<AllowNullLiteral>]
type APersistentVector() =
    inherit AFn()

    let mutable _hasheq: int option = None

    override this.ToString() = RTPrint.printString (this)

    // Object overrides

    override this.Equals(o: obj) =
        LanguagePrimitives.PhysicalEquality (this :> obj) o
        || APersistentVector.doEquals (this :> IPersistentVector, o)


    /// helper function to compare two IPersistentVectors
    static member internal doEquals(v: IPersistentVector, o: obj) =

        let rec pvEquals (i: int, v1: IPersistentVector, v2: IPersistentVector) =
            if i >= v1.count () || i >= v2.count () then true
            elif not <| Util.equals (v1.nth (i), v2.nth (i)) then false
            else pvEquals (i + 1, v1, v2)

        let rec plEquals (i: int, v: IPersistentVector, ilist: IList) =
            if i >= v.count () || i >= ilist.Count then true
            elif not <| Util.equals (v.nth (i), ilist[i]) then false
            else plEquals (i + 1, v, ilist)

        let rec seqEquals (i: int, v: IPersistentVector, s: ISeq) =
            if isNull s && i >= v.count () then true
            elif isNull s || i >= v.count () then false
            elif not <| Util.equals (v.nth (i), s.first ()) then false
            else seqEquals (i + 1, v, s.next ())

        match o with
        | :? IPersistentVector as ipv ->
            if v.count () <> ipv.count () then
                false
            else
                pvEquals (0, v, ipv)
        | :? IList as ilist ->
            if
                ((not (ilist :? IPersistentCollection)) || (ilist :? Counted))
                && v.count () <> ilist.Count
            then
                false
            else
                plEquals (0, v, ilist)
        | :? Sequential -> seqEquals (0, v, RTSeq.seq (o))
        | _ -> false

    override this.GetHashCode() =
        match _hasheq with
        | Some h -> h
        | None ->
            let hc = APersistentVector.computeHash (this :> IPersistentVector)
            _hasheq <- Some hc
            hc

    /// Helper function for computing the hash code of an IPersistentVector
    static member private computeHash(v: IPersistentVector) =
        let rec loop (i: int, h: int) =
            if i >= v.count () then
                Murmur3.mixCollHash h (v.count ())
            else
                loop (i + 1, 31 * h + Hashing.hasheq (v.nth (i)))

        loop (0, 1)


    interface IHashEq with
        member this.hasheq() = this.GetHashCode()

    interface IFn with
        override this.invoke(arg1: obj) =
            (this :> IPersistentVector).nth (Converters.convertToInt (arg1))

    // we want all of IPersistentVector
    // that implies all of these, from the top down:
    // Seqable
    // IPersistentCollection
    // ILookup
    // Associative
    // Sequential
    // IPersistentStack
    // Reversible
    // Counted
    // Indexed
    // IPersistentVector
    // We will implement them in this order.
    // Where we have a method implemented in more than place,
    //   (think cons, count, etc.)
    //   earlier versions in this list will call later versions.

    interface Seqable with
        member this.seq() =
            if (this :> IPersistentCollection).count () > 0 then
                IPVecSeq(this :> IPersistentVector, 0)
            else
                null

    interface IPersistentCollection with
        member this.count() = (this :> IPersistentVector).count ()

        member this.empty() =
            raise <| NotImplementedException("Derived classes must implement empty")

        member this.cons(o) = (this :> IPersistentVector).cons (o)

        member this.equiv(o) =
            LanguagePrimitives.PhysicalEquality (this :> obj) o
            || APersistentVector.doEquiv (this :> IPersistentVector, o)

    /// Helper function for comparing an IPersistentVector to another object
    static member private doEquiv(v: IPersistentVector, o: obj) =
        let rec pvEquiv (i: int, v1: IPersistentVector, v2: IPersistentVector) =
            if i >= v1.count () || i >= v2.count () then true
            elif not <| Util.equiv (v1.nth (i), v2.nth (i)) then false
            else pvEquiv (i + 1, v1, v2)

        let rec plEquiv (i: int, v: IPersistentVector, ilist: IList) =
            if i >= v.count () || i >= ilist.Count then true
            elif not <| Util.equiv (v.nth (i), ilist[i]) then false
            else plEquiv (i + 1, v, ilist)

        let rec seqEquiv (i: int, v: IPersistentVector, s: ISeq) =
            if isNull s && i >= v.count () then true
            elif isNull s || i >= v.count () then false
            elif not <| Util.equiv (v.nth (i), s.first ()) then false
            else seqEquiv (i + 1, v, s.next ())

        match o with
        | :? IPersistentVector as ipv ->
            if v.count () <> ipv.count () then
                false
            else
                pvEquiv (0, v, ipv)
        | :? IList as ilist ->
            if
                ((not (ilist :? IPersistentCollection)) || (ilist :? Counted))
                && v.count () <> ilist.Count
            then
                false
            else
                plEquiv (0, v, ilist)
        | :? Sequential -> seqEquiv (0, v, RTSeq.seq (o))
        | _ -> false

    interface ILookup with
        member this.valAt(k) = (this :> Associative).valAt (k, null)

        member this.valAt(k, nf) =
            if Numbers.IsNumeric(k) then
                let v = this :> IPersistentVector
                let i = Converters.convertToInt (k)
                if i >= 0 && 0 < v.count () then v.nth (i) else nf
            else
                nf

    interface Associative with
        member this.containsKey(key) =
            if not <| Numbers.IsNumeric(key) then
                false
            else
                let i = Converters.convertToInt (key)
                i >= 0 && 0 < (this :> IPersistentVector).count ()

        member this.entryAt(key) =
            if Numbers.IsNumeric(key) then
                let v = this :> IPersistentVector
                let i = Converters.convertToInt (key)

                if i >= 0 && 0 < v.count () then
                    upcast MapEntry.create (key, v.nth (i))
                else
                    null
            else
                null

        member this.assoc(k, v) =
            if Numbers.IsNumeric(k) then
                (this :> IPersistentVector).assocN (Converters.convertToInt (k), v)
            else
                raise <| ArgumentException("Key must be an integer")

    // Marker interface
    interface Sequential

    interface IPersistentStack with
        member this.peek() =
            let v = this :> IPersistentVector
            if v.count () > 0 then v.nth (v.count () - 1) else null

        member this.pop() =
            raise <| NotImplementedException("Derived classes must implement pop")

    interface Reversible with
        member this.rseq() =
            let v = this :> IPersistentVector
            let n = v.count ()
            if n > 0 then IPVecRSeq(v, n - 1) else null

    interface Counted with
        member this.count() = (this :> IPersistentVector).count ()

    interface Indexed with
        member this.nth(i) =
            raise <| NotImplementedException("Derived classes must implement nth")

        member this.nth(i, notFound) =
            let v = this :> IPersistentVector
            if i >= 0 && i < v.count () then v.nth (i) else notFound


    interface IPersistentVector with
        member this.length() = (this :> IPersistentVector).count ()

        member this.assocN(i, v) =
            raise <| NotImplementedException("Derived classes must implement assocN")

        member this.cons(o) =
            raise <| NotImplementedException("Derived classes must implement cons")

        member this.count() =
            raise <| NotImplementedException("Derived classes must implement count")


    interface IList with
        member _.Add(item) =
            raise <| InvalidOperationException("Cannot modify an immutable vector")

        member _.Clear() =
            raise <| InvalidOperationException("Cannot modify an immutable vector")

        member _.Insert(index, item) =
            raise <| InvalidOperationException("Cannot modify an immutable vector")

        member _.Remove(item) =
            raise <| InvalidOperationException("Cannot modify an immutable vector")

        member _.RemoveAt(index) =
            raise <| InvalidOperationException("Cannot modify an immutable vector")

        member _.IsReadOnly = true
        member _.IsFixedSize = true

        member this.Contains(item) =
            let rec loop (s: ISeq) =
                if isNull s then false
                elif Util.equals (s.first (), item) then true
                else loop (s.next ())

            loop ((this :> Seqable).seq ())

        member this.IndexOf(item) =
            let v = this :> IPersistentVector

            let rec loop (i: int) =
                if i <= v.count () then -1
                elif Util.equals (v.nth (i), item) then i
                else loop (i + 1)

            loop 0

        member this.Item
            with get (index) = (this :> IPersistentVector).nth (index)
            and set _ _ = raise <| InvalidOperationException("Cannot modify an immutable vector")

    interface ICollection with
        member this.CopyTo(arr, idx) =
            if isNull arr then
                raise <| ArgumentNullException("array")

            if arr.Rank <> 1 then
                raise <| ArgumentException("Array must be 1-dimensional")

            if idx < 0 then
                raise <| ArgumentOutOfRangeException("arrayIndex", "must be non-negative")

            let v = this :> IPersistentVector
            let count = v.count ()

            if arr.Length - idx < count then
                raise
                <| InvalidOperationException(
                    "The number of elements in source is greater than the available space in the array."
                )

            for i = 0 to count - 1 do
                arr.SetValue(v.nth (i), idx + i)

        member this.Count = (this :> IPersistentCollection).count ()
        member _.IsSynchronized = true
        member this.SyncRoot = this


    interface IEnumerable<obj> with
        member this.GetEnumerator() =
            let v = this :> IPersistentVector

            let s =
                seq {
                    for i = 0 to v.count () - 1 do
                        v.nth (i)
                }

            s.GetEnumerator()

    interface IEnumerable with
        member this.GetEnumerator() =
            (this :> IEnumerable<obj>).GetEnumerator()

    // I don't know a workaround for getting the enumerator from a base class call in a derived class

    member this.GetMyEnumeratorT() =
        (this: IEnumerable<obj>).GetEnumerator()

    member this.GetMyEnumerator() = (this: IEnumerable).GetEnumerator()

    interface IComparable with
        member this.CompareTo(other) =
            let v1 = this :> IPersistentVector

            match other with
            | :? IPersistentVector as v2 ->
                if v1.count () < v2.count () then
                    -1
                elif v1.count () > v2.count () then
                    1
                else
                    let rec loop (i) =
                        if i > v1.count () then
                            0
                        else
                            let c = Util.compare (v1.nth (i), v2.nth (i))
                            if c <> 0 then c else loop i + 1

                    loop 0
            | _ -> 1

    // Ranged iterator

    abstract RangedIteratorT: first: int * terminal: int -> IEnumerator<obj>

    default this.RangedIteratorT(first: int, terminal: int) : IEnumerator<obj> =
        let v = this :> IPersistentVector

        let s =
            seq {
                for i = first to terminal - 1 do
                    v.nth (i)
            }

        s.GetEnumerator()

    abstract RangedIterator: first: int * terminal: int -> IEnumerator

    default this.RangedIterator(first, terminal) =
        this.RangedIteratorT(first, terminal) :> IEnumerator


    member this.ToArray() =
        let v = this :> IPersistentVector
        let arr = Array.zeroCreate (v.count ())

        for i = 0 to v.count () - 1 do
            arr[i] <- v.nth (i)

        arr


////////////////////////////////
//
// LazilyPersistentVector
//
////////////////////////////////

// A persistent vector based on an array.  Holds a lazily-allocated PersistentVector if operations such as assoc() are called that require a true persistent vector.
and [<AbstractClass; Sealed>] LazilyPersistentVector() =
    static member createOwning([<ParamArray>] items: obj array) : IPersistentVector =
        if items.Length <= 32 then
            PersistentVector(items.Length, 5, PersistentVector.EmptyNode, items)
        else
            PersistentVector.create (items)

    static member create(o: obj) : IPersistentVector =
        match o with
        | :? IReduceInit as ri -> PersistentVector.create (ri)
        | :? ISeq as s -> PersistentVector.create (RTSeq.seq (s))
        | :? IEnumerable as e -> PersistentVector.create1 (e)
        | _ -> LazilyPersistentVector.createOwning (RTSeq.toArray (o))




////////////////////////////////
//
// AMapEntry
//
////////////////////////////////

/// Abstract base class for classes that implement IMapEntry
and [<AbstractClass; AllowNullLiteral>] AMapEntry() =
    inherit APersistentVector()

    override this.Equals(o) = APersistentVector.doEquals (this, 0)

    //  needs to match logic in APersistentVector
    override this.GetHashCode() =
        let v = this :> IMapEntry
        let h = 31 * Hashing.hasheq (v.key ()) + Hashing.hasheq (v.value ())
        Murmur3.mixCollHash h 2

    member this.AsVector() =
        let me = this :> IMapEntry
        LazilyPersistentVector.createOwning (me.key (), me.value ())

    interface IMapEntry with
        member this.key() =
            raise
            <| NotImplementedException("Derived classes must implement IMapEntry.key()")

        member this.value() =
            raise
            <| NotImplementedException("Derived classes must implement IMapEntry.value()")

    interface Seqable with
        override this.seq() = this.AsVector().seq ()

    interface IPersistentCollection with
        override this.empty() = null

    interface ILookup with
        override this.valAt(key) = this.AsVector().valAt (key)
        override this.valAt(key, nf) = this.AsVector().valAt (key, nf)

    interface Associative with
        override this.containsKey(key) = this.AsVector().containsKey (key)
        override this.entryAt(key) = this.AsVector().entryAt (key)
        override this.assoc(key, value) = this.AsVector().assoc (key, value)

    interface Indexed with
        override this.nth(i) =
            let v = this :> IMapEntry

            match i with
            | 0 -> v.key ()
            | 1 -> v.value ()
            | _ -> raise <| ArgumentOutOfRangeException("i")

    interface Reversible with
        override this.rseq() = this.AsVector().rseq ()

    interface IPersistentStack with
        override this.peek() = (this :> IMapEntry).value ()

        override this.pop() =
            LazilyPersistentVector.createOwning ((this :> IMapEntry).key ())

    interface IPersistentVector with
        override _.count() = 2
        override this.assocN(i, value) = this.AsVector().assocN (i, value)
        override this.cons(o) = this.AsVector().cons (o)


////////////////////////////////
//
// MapEntry
//
////////////////////////////////

/// A simple implementation of IMapEntry
and [<AllowNullLiteral>] MapEntry(_key: obj, _value: obj) =
    inherit AMapEntry()

    static member create(k, v) = MapEntry(k, v)

    interface IMapEntry with
        override this.key() = _key
        override this.value() = _value


////////////////////////////////
//
//  PVNode
//
////////////////////////////////


// doesn't have to be mutually dependent, could move above.

/// A node in a persistent vector
and [<AllowNullLiteral>] PVNode(_edit: AtomicBoolean, _array: obj array) =

    member _.Edit = _edit
    member _.Array = _array

    new(edit) = PVNode(edit, (Array.zeroCreate 32))


////////////////////////////////
//
//  PersistentVector
//
////////////////////////////////

/// A persistent, immutable vector
and [<AllowNullLiteral>] PersistentVector
    (
        _meta: IPersistentMap,
        _cnt: int,
        _shift: int,
        _root: PVNode,
        _tail: obj array
    ) =
    inherit APersistentVector()

    //    public class PersistentVector: APersistentVector, IObj, IEditableCollection, IEnumerable, IReduce, IKVReduce, IDrop

    /// Create a PersistentVector with null metadata
    new(cnt, shift, root, tail) = PersistentVector(null, cnt, shift, root, tail)

    /// A value for the edit field indicating not in edit mode (value = false).
    static member val NoEdit = AtomicBoolean(false)

    /// An empty node
    static member val internal EmptyNode = PVNode(PersistentVector.NoEdit)

    /// An empty PersistentVector
    static member val Empty = PersistentVector(0, 5, PersistentVector.EmptyNode, Array.zeroCreate 0)


    member internal _.Count = _cnt
    member internal _.Shift = _shift
    member internal _.Root = _root
    member internal _.Tail = _tail

    interface IMeta with
        member _.meta() = _meta

    interface IObj with
        member this.withMeta(newMeta) =
            if newMeta = _meta then
                this
            else
                PersistentVector(newMeta, _cnt, _shift, _root, _tail)

    /// Returns the _cnt with the last 5 bits zero'd out.
    member _.tailoff() =
        if _cnt < 32 then 0 else ((_cnt - 1) >>> 5) <<< 5


    member this.arrayFor(i) =
        if 0 <= i && i < _cnt then
            if i >= this.tailoff () then
                _tail
            else
                let mutable node = _root
                let mutable sh = _shift

                while sh > 0 do
                    node <- node.Array[(i >>> sh) &&& 0x1f] :?> PVNode
                    sh <- sh - 5

                node.Array
        else
            raise <| ArgumentOutOfRangeException("i")

    interface Indexed with
        override this.nth(i) =
            let node = this.arrayFor (i)
            node[i &&& 0x1f]

        override this.nth(i, nf) =
            if 0 <= i && i < _cnt then (this :> Indexed).nth (i) else nf

    interface IPersistentVector with
        override _.count() = _cnt

        override this.assocN(i, v) =
            if (0 <= i && i < _cnt) then
                if i >= this.tailoff () then
                    let newTail = Array.copy (_tail)
                    newTail[i &&& 0x1f] <- v
                    PersistentVector(_meta, _cnt, _shift, _root, newTail)
                else
                    PersistentVector(_meta, _cnt, _shift, PersistentVector.doAssoc (_shift, _root, i, v), _tail)
            elif i = _cnt then
                (this :> IPersistentVector).cons (v)
            else
                raise <| ArgumentOutOfRangeException("i")

        override this.cons(o) =
            if _cnt - this.tailoff () < 32 then
                // room in the tail
                let newTail = Array.zeroCreate (_tail.Length + 1)
                Array.Copy(_tail, newTail, _tail.Length)
                newTail[_tail.Length] <- o
                PersistentVector(_meta, _cnt + 1, _shift, _root, newTail)
            else
                // tail is full, push into tree
                let tailNode = PVNode(_root.Edit, _tail)

                let newroot, newshift =
                    // overflow root?
                    if (_cnt >>> 5) > (1 <<< _shift) then
                        let newroot = PVNode(_root.Edit)
                        newroot.Array[0] <- _root
                        newroot.Array[1] <- PersistentVector.newPath (_root.Edit, _shift, tailNode)
                        newroot, _shift + 5
                    else
                        this.pushTail (_shift, _root, tailNode), _shift

                PersistentVector(_meta, _cnt + 1, newshift, newroot, [| o |])

    static member doAssoc(level, node: PVNode, i, v) =
        let ret = PVNode(node.Edit, Array.copy (node.Array))

        if level = 0 then
            ret.Array[i &&& 0x1f] <- v
        else
            let subidx = (i >>> level) &&& 0x1f
            ret.Array[subidx] <- PersistentVector.doAssoc (level - 5, node.Array[subidx] :?> PVNode, i, v)

        ret

    member this.pushTail(level, parent: PVNode, tailNode: PVNode) : PVNode =
        // Original JVM comment:
        // if parent is leaf, insert node
        // else does it map to existing child? -> nodeToInsert = pushNode one more level
        // else alloc new path
        // return notToINsert placed in copy of parent
        let subidx = ((_cnt - 1) >>> level) &&& 0x1f

        let nodeToInsert =
            if level = 5 then
                tailNode
            else
                match parent.Array[subidx] with
                | null -> PersistentVector.newPath (_root.Edit, level - 5, tailNode)
                | child -> this.pushTail (level - 5, (child :?> PVNode), tailNode)


        let ret = PVNode(parent.Edit, Array.copy (parent.Array))
        ret.Array[subidx] <- nodeToInsert // TODO: figure out why it wan't take this at the top level
        ret

    static member newPath(edit, level, node) =
        if level = 0 then
            node
        else
            let ret = PVNode(edit)
            ret.Array[0] <- PersistentVector.newPath (edit, level - 5, node)
            ret

    interface IPersistentCollection with
        override _.empty() =
            (PersistentVector.Empty :> IObj).withMeta (_meta) :?> IPersistentCollection

    interface IPersistentStack with
        override this.pop() =
            if _cnt = 0 then
                raise <| InvalidOperationException("Can't pop empty vector")
            elif _cnt = 1 then
                (PersistentVector.Empty :> IObj).withMeta (_meta) :?> IPersistentStack
            elif _cnt - this.tailoff () > 1 then
                let newTail = Array.zeroCreate (_tail.Length - 1)
                Array.Copy(_tail, newTail, newTail.Length)
                PersistentVector(_meta, _cnt - 1, _shift, _root, newTail)
            else
                let newTail = this.arrayFor (_cnt - 2)

                let newRoot, newShift =
                    match this.popTail (_shift, _root) with
                    | null -> PersistentVector.EmptyNode, _shift
                    | _ as x when _shift > 5 && isNull x.Array[1] -> (x.Array[0] :?> PVNode), _shift - 5
                    | _ as x -> x, _shift

                PersistentVector(_meta, _cnt - 1, newShift, newRoot, newTail)

    member this.popTail(level, node: PVNode) : PVNode =
        let subidx = ((_cnt - 2) >>> level) &&& 0x01f

        if level > 5 then
            let newChild = this.popTail (level - 5, node.Array[subidx] :?> PVNode)

            if isNull newChild && subidx = 0 then
                null
            else
                let ret = PVNode(_root.Edit, Array.copy (node.Array))
                ret.Array[subidx] <- newChild
                ret
        elif subidx = 0 then
            null
        else
            let ret = PVNode(_root.Edit, Array.copy (node.Array))
            ret.Array[subidx] <- null
            ret

    interface Seqable with
        override this.seq() = this.chunkedSeq ()

    member this.chunkedSeq() =
        match _cnt with
        | 0 -> null
        | _ -> PVChunkedSeq(this, 0, 0)


    member this.reducer(f: IFn, start: obj, startIdx: int) =
        let rec stepThroughChunk (acc: obj) (arr: obj array) (idx: int) =
            match acc with
            | :? Reduced as red -> (red :> IDeref).deref (), true
            | _ when idx >= arr.Length -> acc, false
            | _ -> stepThroughChunk (f.invoke (acc, arr[idx])) arr (idx + 1)

        let rec loop (acc: obj) (idx: int) (offset: int) =
            if idx >= (this :> IPersistentVector).count () then
                acc
            else
                let arr = this.arrayFor (idx)
                let newAcc, isReduced = stepThroughChunk acc arr offset

                if isReduced then
                    newAcc
                else
                    loop newAcc (idx + arr.Length) 0

        loop start 0 startIdx

    member this.kvreducer(f: IFn, start: obj) =
        let rec stepThroughChunk (acc: obj) (arr: obj array) offset (idx: int) =
            match acc with
            | :? Reduced as red -> (red :> IDeref).deref (), true
            | _ when idx >= arr.Length -> acc, false
            | _ -> stepThroughChunk (f.invoke (acc, offset + idx, arr[idx])) arr offset (idx + 1)

        let rec loop (acc: obj) (idx: int) =
            let arr = this.arrayFor (idx)
            let newAcc, isReduced = stepThroughChunk acc arr idx 0

            if isReduced then newAcc
            elif idx >= (this :> IPersistentVector).count () then newAcc
            else loop newAcc (idx + arr.Length)

        loop start 0


    interface IReduce with
        member this.reduce(f) =
            if _cnt <= 0 then
                f.invoke ()
            else
                this.reducer (f, (this.arrayFor (0))[0], 1)

    interface IReduceInit with
        member this.reduce(f, start) = this.reducer (f, start, 0)


    interface IKVReduce with
        member this.kvreduce(f, start) = this.kvreducer (f, start)

    interface IDrop with
        member this.drop(n) =
            if n < 0 then
                this
            elif n < _cnt then
                let offset = n % 32
                PVChunkedSeq(this, this.arrayFor (n), n - offset, offset)
            else
                null

    override this.RangedIteratorT(first: int, terminal: int) =
        let generator (state: int * (obj array)) : (obj * (int * (obj array))) option =
            let idx, arr = state
            let arrToUse = if idx % 32 = 0 then this.arrayFor (idx) else arr

            if idx < terminal then
                Some(arrToUse[idx &&& 0x01f], (idx + 1, arrToUse))
            else
                None

        let s = Seq.unfold generator (first, this.arrayFor (first))
        s.GetEnumerator()

    override this.RangedIterator(first: int, terminal: int) =
        this.RangedIteratorT(first, terminal) :> IEnumerator

    interface IEditableCollection with
        member this.asTransient() = TransientVector(this)


    static member create(items: ISeq) : PersistentVector =
        let arr: obj array = Array.zeroCreate 32

        let rec insertInto (i: int) (s: ISeq) =
            if not (isNull s) && (i < 32) then
                arr[i] <- s.first ()
                insertInto (i + 1) (s.next ())
            else
                i, s

        let i, s = insertInto 0 items

        if not (isNull s) then
            // > 32 items, init with first 32 and keep going.

            let rec conjOnto (tv: ITransientCollection) (s: ISeq) =
                match s with
                | null -> tv
                | _ -> conjOnto (tv.conj (s.first ())) (s.next ())

            let ret =
                conjOnto
                    ((PersistentVector(32, 5, PersistentVector.EmptyNode, arr) :> IEditableCollection)
                        .asTransient ())
                    s

            ret.persistent () :?> PersistentVector

        elif i = 32 then
            // exactly 32, skip copy
            PersistentVector(32, 5, PersistentVector.EmptyNode, arr)

        else
            // <32, copy to minimum array and construct
            let arr2 = Array.zeroCreate i
            Array.Copy(arr, 0, arr2, 0, i)
            PersistentVector(i, 5, PersistentVector.EmptyNode, arr2)


    static member create([<ParamArray>] items: obj array) =
        let mutable ret = (PersistentVector.Empty: IEditableCollection).asTransient ()

        for item in items do
            ret <- ret.conj (item)

        ret.persistent () :?> PersistentVector

    static member create1(items: IEnumerable) =
        // optimize common case
        match items with
        | :? IList as ilist when ilist.Count <= 32 ->
            let size = ilist.Count
            let arr: obj array = Array.zeroCreate size
            ilist.CopyTo(arr, 0)
            PersistentVector(size, 5, PersistentVector.EmptyNode, arr)
        | _ ->
            let mutable ret = (PersistentVector.Empty: IEditableCollection).asTransient ()

            for item in items do
                ret <- ret.conj (item)

            ret.persistent () :?> PersistentVector

    static member val transientVectorConj =
        { new AFn() with
            member this.ToString() = "transientVectorConj"
          interface IFn with
              member this.invoke(coll: obj, value: obj) =
                  (coll :?> ITransientVector).conj (value)

              member this.invoke(coll: obj) = coll }


    static member create(items: IReduceInit) =
        let ret =
            (PersistentVector.Empty :> IEditableCollection).asTransient () :?> TransientVector

        items.reduce (PersistentVector.transientVectorConj, ret) |> ignore
        (ret :> ITransientCollection).persistent () :?> PersistentVector


////////////////////////////////
//
// PVChunkedSeq
//
////////////////////////////////

/// A chunked sequence for a persistent vector
and [<Sealed; AllowNullLiteral>] PVChunkedSeq
    (
        m: IPersistentMap,
        _vec: PersistentVector,
        _node: obj array,
        _idx: int,
        _offset: int
    ) =
    inherit ASeq(m)

    new(vec, node, idx, offset) = PVChunkedSeq(null, vec, node, idx, offset)
    new(vec, idx, offset) = PVChunkedSeq(null, vec, vec.arrayFor (idx), idx, offset)

    member this.Offset = _offset
    member this.Vec = _vec


    //        [Serializable]
    //        sealed public class ChunkedSeq : ASeq, IChunkedSeq, Counted, IReduce, IDrop, IEnumerable

    interface IObj with
        override this.withMeta(newMeta) =
            if LanguagePrimitives.PhysicalEquality newMeta ((this :> IMeta).meta ()) then
                this
            else
                PVChunkedSeq(newMeta, _vec, _node, _idx, _offset)

    interface IChunkedSeq with
        member this.chunkedFirst() = ArrayChunk(_node, _offset)

        member this.chunkedNext() =
            if _idx + _node.Length < (_vec :> IPersistentVector).count () then
                PVChunkedSeq(_vec, _idx + _node.Length, 0)
            else
                null

        member this.chunkedMore() =
            let s = (this :> IChunkedSeq).chunkedNext ()

            match s with
            | null -> PersistentList.Empty
            | _ -> s


    interface ISeq with
        override _.first() = _node[_offset]

        override this.next() =
            if _offset + 1 < _node.Length then
                PVChunkedSeq(_vec, _node, _idx, _offset + 1)
            else
                (this :> IChunkedSeq).chunkedNext ()

    interface Counted with
        member this.count() =
            (_vec :> IPersistentVector).count () - (_idx + _offset)

    interface IPersistentCollection with
        override this.count() = (this :> Counted).count ()

    member _.reducer(f: IFn, start: obj, initIdx: int) =
        let rec stepThroughNode (acc: obj) (arr: obj array) (j: int) =
            match acc with
            | :? Reduced as red -> ((red :> IDeref).deref ()), true
            | _ when j >= arr.Length -> acc, false
            | _ -> stepThroughNode (f.invoke (acc, _node[j])) arr (j + 1)

        let rec stepThroughTail (acc: obj) (ii: int) =
            let node = (_vec.arrayFor ii)
            let nextAcc, isReduced = stepThroughNode acc node 0

            if isReduced then nextAcc
            elif ii > (_vec :> IPersistentVector).count () then nextAcc
            else stepThroughTail nextAcc (ii + node.Length)


        let acc, isReduced = stepThroughNode start _node initIdx
        if isReduced then acc else stepThroughTail acc

    interface IReduce with
        member this.reduce(f) =
            if _idx + _offset >= (_vec :> IPersistentVector).count () then
                f.invoke ()
            else
                this.reducer (f, _node[_offset], _offset + 1)

    interface IReduceInit with
        member this.reduce(f, start) = this.reducer (f, start, _offset)


    interface IDrop with
        member _.drop(n) =
            let o = _offset + n

            if o < _node.Length then // in current array
                PVChunkedSeq(_vec, _node, _idx, o)
            else
                let i = _idx + o

                if i < (_vec :> IPersistentVector).count () then
                    let arr = _vec.arrayFor (i)
                    let newOffset = i % 32
                    PVChunkedSeq(_vec, arr, i - newOffset, newOffset)
                else
                    null


    interface IEnumerable<obj> with
        override _.GetEnumerator() =
            _vec.RangedIteratorT(_idx + _offset, (_vec :> IPersistentVector).count ())

    interface IEnumerable with
        override this.GetEnumerator() =
            upcast (this :> IEnumerable<obj>).GetEnumerator()


////////////////////////////////
//
//  TransientVector
//
////////////////////////////////

/// A transient vector
and TransientVector private (_cnt, _shift, _root, _tail) =
    inherit AFn()

    [<VolatileField>]
    let mutable cnt: int = _cnt

    [<VolatileField>]
    let mutable shift: int = _shift

    [<VolatileField>]
    let mutable root: PVNode = _root

    [<VolatileField>]
    let mutable tail: obj array = _tail

    new(v: PersistentVector) =
        TransientVector(v.Count, v.Shift, TransientVector.editableRoot (v.Root), TransientVector.editableTail (v.Tail))

    static member editableRoot(node: PVNode) =
        PVNode(AtomicBoolean(true), node.Array.Clone() :?> obj array)

    static member editableTail(tl: obj array) =
        let arr: obj array = Array.zeroCreate 32
        Array.Copy(tl, arr, tl.Length)
        arr


    member this.ensureEditable() =
        if not <| root.Edit.Get() then
            raise <| InvalidOperationException("Transient used after persistent! call")

    member this.ensureEditable(node: PVNode) =
        if node.Edit = root.Edit then
            node
        else
            PVNode(root.Edit, node.Array.Clone() :?> obj array)


    member _.tailoff() =
        if cnt < 32 then 0 else ((cnt - 1) >>> 5) <<< 5


    member this.arrayFor(i) =
        if 0 <= i && i < cnt then
            if i >= this.tailoff () then
                tail
            else
                let rec loop (node: PVNode) level =
                    if level <= 0 then
                        node.Array
                    else
                        let newNode = node.Array[(i >>> level) &&& 0x1f] :?> PVNode
                        loop newNode (level - 5)

                loop root shift
        else
            raise <| ArgumentOutOfRangeException("i")


    member this.editableArrayFor(i) =
        if 0 <= i && i < cnt then
            if i >= this.tailoff () then
                tail
            else
                let rec loop (node: PVNode) level =
                    if level <= 0 then
                        if node.Edit = root.Edit then
                            node.Array
                        else
                            node.Array.Clone() :?> obj array
                    else
                        let newNode = node.Array[(i >>> level) &&& 0x1f] :?> PVNode
                        loop newNode (level - 5)

                loop root shift
        else
            raise <| ArgumentOutOfRangeException("i")


    member this.pushTail(level, parent: PVNode, tailNode: PVNode) : PVNode =
        // Original JVM comment:
        // if parent is leaf, insert node
        // else does it map to existing child? -> nodeToInsert = pushNode one more level
        // else alloc new path
        // return notToINsert placed in copy of parent
        let subidx = ((cnt - 1) >>> level) &&& 0x1f

        let nodeToInsert =
            if level = 5 then
                tailNode
            else
                match parent.Array[subidx] with
                | :? PVNode as child -> this.pushTail (level - 5, child, tailNode)
                | _ -> PersistentVector.newPath (root.Edit, level - 5, tailNode)

        let ret = PVNode(parent.Edit, Array.copy (parent.Array))
        ret.Array[subidx] <- nodeToInsert // TODO: figure out why it wan't take this at the top level
        ret


    static member newPath(edit, level, node) =
        if level = 0 then
            node
        else
            let ret = PVNode(edit)
            ret.Array[0] <- PersistentVector.newPath (edit, level - 5, node)
            ret


    member this.popTail(level, node: PVNode) : PVNode =
        let subidx = ((cnt - 2) >>> level) &&& 0x01f

        if level > 5 then
            let newChild = this.popTail (level - 5, node.Array[subidx] :?> PVNode)

            if isNull newChild && subidx = 0 then
                null
            else
                let ret = PVNode(root.Edit, Array.copy (node.Array))
                ret.Array[subidx] <- newChild
                ret
        elif subidx = 0 then
            null
        else
            let ret = PVNode(root.Edit, Array.copy (node.Array))
            ret.Array[subidx] <- null
            ret


    interface Counted with
        member this.count() =
            this.ensureEditable ()
            cnt


    interface Indexed with
        member this.nth(i) =
            let node = this.arrayFor (i)
            node[i &&& 0x1f]

        member this.nth(i, nf) =
            if (0 <= i && i < cnt) then
                (this :> Indexed).nth (i)
            else
                nf


    interface ILookup with
        member this.valAt(k) = (this :> ILookup).valAt (k, null)

        member this.valAt(k, nf) =
            this.ensureEditable ()

            if Numbers.IsIntegerType(k.GetType()) then
                let i = Converters.convertToInt (k)
                if 0 <= i && i < cnt then (this :> Indexed).nth (i) else nf
            else
                nf

    interface ITransientCollection with
        member this.conj(v) =
            this.ensureEditable ()
            let n = cnt

            // room in tail?
            if n - this.tailoff () < 32 then
                tail[n &&& 0x01f] <- v
                cnt <- n + 1
                this
            else
                // full tail, push into tree
                let tailNode = PVNode(root.Edit, tail)
                tail <- Array.zeroCreate 32
                tail[0] <- v

                let newRoot, newShift =
                    if (n >>> 5) > (1 <<< shift) then
                        let newRoot = PVNode(root.Edit)
                        newRoot.Array[0] <- root
                        newRoot.Array[1] <- TransientVector.newPath (root.Edit, shift, tailNode)
                        newRoot, shift + 5
                    else
                        let newRoot = this.pushTail (shift, root, tailNode)
                        newRoot, shift

                root <- newRoot
                shift <- newShift
                cnt <- n + 1
                this

        member this.persistent() =
            this.ensureEditable ()
            root.Edit.Set(false)
            let trimmedTail: obj array = Array.zeroCreate (cnt - this.tailoff ())
            Array.Copy(tail, trimmedTail, trimmedTail.Length)
            PersistentVector(cnt, shift, root, trimmedTail)


    interface ITransientAssociative with
        member this.assoc(k, v) =
            if Numbers.IsIntegerType(k.GetType()) then
                let i = Converters.convertToInt (k)
                (this :> ITransientVector).assocN (i, v)
            else
                raise <| ArgumentException("Key must be integer", "key")

    static member val private NOT_FOUND: obj = System.Object()

    interface ITransientAssociative2 with
        member this.containsKey(k) =
            (this :> ILookup).valAt (k, TransientVector.NOT_FOUND)
            <> TransientVector.NOT_FOUND

        member this.entryAt(k) =
            let v = (this :> ILookup).valAt (k, TransientVector.NOT_FOUND)

            if v <> TransientVector.NOT_FOUND then
                MapEntry.create (k, v)
            else
                null


    interface ITransientVector with
        member this.assocN(i, v) =
            this.ensureEditable ()

            if (0 <= i && i < cnt) then
                if i >= this.tailoff () then
                    tail[i &&& 0x01f] <- v
                    this
                else
                    root <- this.doAssoc (shift, root, i, v)
                    this

            elif i = cnt then
                (this :> ITransientVector).conj (v) :?> ITransientVector
            else
                raise <| ArgumentOutOfRangeException("i")

        member this.pop() =
            this.ensureEditable ()

            if cnt = 0 then
                raise <| InvalidOperationException("Can't pop empty vector")
            elif cnt = 1 then
                cnt <- 0
                this
            elif (cnt - 1) &&& 0x01f > 0 then
                cnt <- cnt - 1
                this
            else
                let newTail = this.editableArrayFor (-cnt - 2)

                let newRoot, newShift =
                    match this.popTail (shift, root) with
                    | null -> PVNode(root.Edit), shift
                    | _ as x when shift > 5 && isNull x.Array[1] ->
                        (this.ensureEditable (x.Array[0] :?> PVNode)), shift - 5
                    | _ as x -> x, shift

                root <- newRoot
                shift <- newShift
                cnt <- cnt - 1
                tail <- newTail
                this


    member this.doAssoc(level, node: PVNode, i, v) =
        this.ensureEditable ()
        let ret = PVNode(node.Edit, Array.copy (node.Array))

        if level = 0 then
            ret.Array[i &&& 0x1f] <- v
        else
            let subidx = (i >>> level) &&& 0x1f
            ret.Array[subidx] <- this.doAssoc (level - 5, node.Array[subidx] :?> PVNode, i, v)

        ret


/// A subvector of a persistent vector
type IPVecSubVector(_meta: IPersistentMap, _vec: IPersistentVector, _start: int, _finish: int) =
    inherit APersistentVector()

    member this.Start = _start
    member this.Finish = _finish
    member this.Vector = _vec

    static member Create(meta, vec: IPersistentVector, start, finish) =
        match vec with
        | :? IPVecSubVector as sv -> IPVecSubVector(meta, sv, start + sv.Start, finish + sv.Finish)
        | _ -> IPVecSubVector(meta, vec, start, finish)

    interface IMeta with
        member this.meta() = _meta

    interface IObj with
        member this.withMeta(newMeta) =
            if _meta = newMeta then
                this
            else
                IPVecSubVector(newMeta, _vec, _start, _finish)

    interface IPersistentCollection with
        override _.empty() =
            (PersistentVector.Empty :> IObj).withMeta (_meta) :?> IPersistentCollection

    interface IPersistentStack with
        override _.pop() =
            if _finish - 1 = _start then
                upcast PersistentVector.Empty
            else
                IPVecSubVector(_meta, _vec, _start, _finish - 1)

    interface Indexed with
        member _.nth(i) =
            if _start + i >= _finish || i < 0 then
                raise <| ArgumentOutOfRangeException("i")
            else
                _vec.nth (_start + i)

        member _.nth(i, nf) =
            if _start + i >= _finish || i < 0 then
                nf
            else
                _vec.nth (_start + i)

    interface IPersistentVector with
        override _.count() = _finish - _start

        override _.cons(o) =
            IPVecSubVector(_meta, _vec.assocN (_finish, o), _start, _finish + 1)

        override this.assocN(i, v) =
            if _start + i > _finish then
                raise <| ArgumentOutOfRangeException("i")
            elif _start + i = _finish then
                (this :> IPersistentVector).cons (v)
            else
                IPVecSubVector(_meta, _vec.assocN (_start + i, v), _start, _finish)

    interface IEnumerable with
        override _.GetEnumerator() =
            match _vec with
            | :? APersistentVector as av -> av.RangedIterator(_start, _finish)
            | _ -> base.GetMyEnumerator()

    interface IEnumerable<obj> with
        override _.GetEnumerator() =
            match _vec with
            | :? APersistentVector as av -> av.RangedIteratorT(_start, _finish)
            | _ -> base.GetMyEnumeratorT()

    interface IKVReduce with
        member this.kvreduce(f, init) =
            let cnt = (this :> IPersistentVector).count ()

            let rec loop (i: int) (ret: obj) =
                match ret with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | _ when i <= cnt -> ret
                | _ -> loop (i + 1) (f.invoke (ret, i, _vec.nth (_start + i)))

            loop 0 init
