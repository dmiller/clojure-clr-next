namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics

[<Sealed>]
type IPVecSeq(meta: IPersistentMap, vec: IPersistentVector, index: int) =
    inherit ASeq(meta)

    new(vec, index) = IPVecSeq(null, vec, index)

    // TODO: something more efficient  (todo = from Java)

    //        public sealed class Seq : ASeq, IndexedSeq, IReduce, Counted  // Counted left out of Java version

    interface ISeq with
        override _.first() = vec.nth (index)

        override _.next() =
            if index + 1 < vec.count () then
                IPVecSeq(vec, index + 1)
            else
                null

    interface IPersistentCollection with
        override _.count() = vec.count () - index

    interface Counted with
        override _.count() = vec.count () - index

    interface IndexedSeq with
        member this.index() = index

    interface IObj with
        override this.withMeta(meta: IPersistentMap) =
            if (this :> IMeta).meta () = meta then
                this
            else
                IPVecSeq(meta, vec, index)


    // IReduce not in Java original

    interface IReduceInit with
        member _.reduce(f, init) =
            let rec step (i: int) (ret: obj) =
                match ret with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | _ when i >= vec.count () -> ret
                | _ -> step (i + 1) (f.invoke (ret, vec.nth (i)))

            step (index + 1) (f.invoke (init, vec.nth (index)))

    interface IReduce with
        member _.reduce(f) =
            let rec step (i: int) (ret: obj) =
                match ret with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | _ when i >= vec.count () -> ret
                | _ -> step (i + 1) (f.invoke (ret, vec.nth (i)))

            step (index + 1) (f.invoke (vec.nth (index)))


[<Sealed>]
type IPVecRSeq(meta: IPersistentMap, vec: IPersistentVector, index: int) =
    inherit ASeq(meta)

    new(vec, index) = IPVecRSeq(null, vec, index)


    //        public sealed class Seq : ASeq, IndexedSeq, IReduce, Counted  // Counted left out of Java version

    interface ISeq with
        override _.first() = vec.nth (index)

        override _.next() =
            if index > 0 then IPVecRSeq(vec, index - 1) else null

    interface IPersistentCollection with
        override _.count() = index + 1

    interface Counted with
        override _.count() = index + 1

    interface IndexedSeq with
        member this.index() = index

    interface IObj with
        override this.withMeta(meta: IPersistentMap) =
            if (this :> IMeta).meta () = meta then
                this
            else
                IPVecRSeq(meta, vec, index)

    // IReduce not in Java original

    interface IReduceInit with
        member _.reduce(f, init) =
            let rec step (i: int) (ret: obj) =
                match ret with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | _ when i < 0 -> ret
                | _ -> step (i - 1) (f.invoke (ret, vec.nth (i)))

            step (index + 1) (f.invoke (init, vec.nth (index)))

    interface IReduce with
        member _.reduce(f) =
            let rec step (i: int) (ret: obj) =
                match ret with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | _ when i < 0 -> ret
                | _ -> step (i - 1) (f.invoke (ret, vec.nth (i)))

            step (index + 1) (f.invoke (vec.nth (index)))


[<AbstractClass>]
type APersistentVector() =
    inherit AFn()

    let mutable hasheq: int option = None

    override this.ToString() = RTPrint.printString (this)

    override this.Equals(o: obj) =
        obj.ReferenceEquals(this, o)
        || APersistentVector.doEquals (this :> IPersistentVector, o)

    static member doEquals(v: IPersistentVector, o: obj) =
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
            if v.count () <> ilist.Count then
                false
            else
                plEquals (0, v, ilist)
        | :? Sequential -> seqEquals (0, v, RT0.seq (o))
        | _ -> false

    override this.GetHashCode() =
        match hasheq with
        | Some h -> h
        | None ->
            let hc = APersistentVector.computeHash (this :> IPersistentVector)
            hasheq <- Some hc
            hc

    static member private computeHash(v: IPersistentVector) =
        let rec step (i: int, h: int) =
            if i >= v.count () then
                Murmur3.mixCollHash h (v.count ())
            else
                step (i + 1, 31 * h + Hashing.hasheq (v.nth (i)))

        step (0, 1)

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
            obj.ReferenceEquals(this, o)
            || APersistentVector.doEquiv (this :> IPersistentVector, o)

    static member doEquiv(v: IPersistentVector, o: obj) =
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
            if v.count () <> ilist.Count then
                false
            else
                plEquiv (0, v, ilist)
        | :? Sequential -> seqEquiv (0, v, RT0.seq (o))
        | _ -> false

    // why is the IList case coded so diffently here?

    //            if (obj is IList ilist)
    //            {
    //                if ((!(ilist is IPersistentCollection) || (ilist is Counted)) && (ilist.Count != v.count()))
    //                    return false;

    //                var i2 = ilist.GetEnumerator();

    //                for (var i1 = ((IList)v).GetEnumerator(); i1.MoveNext();)
    //                {
    //                    if (!i2.MoveNext() || !Util.equiv(i1.Current,i2.Current))
    //                        return false;
    //                }

    //                return !i2.MoveNext();
    //            }

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
            let rec step (s: ISeq) =
                if isNull s then false
                elif Util.equals (s.first (), item) then true
                else step (s.next ())

            step ((this :> Seqable).seq ())

        member this.IndexOf(item) =
            let v = this :> IPersistentVector

            let rec step (i: int) =
                if i <= v.count () then -1
                elif Util.equals (v.nth (i), item) then i
                else step (i + 1)

            step 0

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
                    let rec step (i) =
                        if i > v1.count () then
                            0
                        else
                            let c = Util.compare (v1.nth (i), v2.nth (i))
                            if c <> 0 then c else step i + 1

                    step 0
            | _ -> 1

    // Ranged iterator

    member this.RangedIteratorT(first: int, terminal: int) =
        let v = this :> IPersistentVector

        let s =
            seq {
                for i = first to terminal - 1 do
                    v.nth (i)
            }

        s.GetEnumerator()

    member this.RangedIterator(first, terminal) =
        this.RangedIteratorT(first, terminal) :> IEnumerator


    member this.ToArray() =
        let v = this :> IPersistentVector
        let arr = Array.zeroCreate (v.count ())

        for i = 0 to v.count () - 1 do
            arr[i] <- v.nth (i)

        arr


and [<AbstractClass; Sealed>] LazilyPersistentVector() =
    static member createOwning([<ParamArray>] items: obj array) : IPersistentVector = null

and [<AbstractClass>] AMapEntry() =
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




///**
// *   Copyright (c) Rich Hickey. All rights reserved.
// *   The use and distribution terms for this software are covered by the
// *   Eclipse Public License 1.0 (http://opensource.org/licenses/eclipse-1.0.php)
// *   which can be found in the file epl-v10.html at the root of this distribution.
// *   By using this software in any fashion, you are agreeing to be bound by
// * 	 the terms of this license.
// *   You must not remove this notice, or any other, from this software.
// **/

///**
// *   Author: David Miller
// **/

//using System;
//using System.Collections;
//using System.Threading;
//using System.Collections.Generic;

//namespace clojure.lang
//{
//    /// <summary>
//    /// Implements a persistent vector using a specialized form of array-mapped hash trie.
//    /// </summary>
//    [Serializable]
//    public class PersistentVector: APersistentVector, IObj, IEditableCollection, IEnumerable, IReduce, IKVReduce, IDrop
//    {
//        #region Node class

//        [Serializable]
//        public sealed class Node
//        {
//            #region Data

//            [NonSerialized]
//            readonly AtomicReference<Thread> _edit;

//            public AtomicReference<Thread> Edit
//            {
//                get { return _edit; }
//            } 

//            readonly object[] _array;

//            public object[] Array
//            {
//                get { return _array; }
//            } 

            
//            #endregion

//            #region C-tors

//            public Node(AtomicReference<Thread> edit, object[] array)
//            {
//                _edit = edit;
//                _array = array;
//            }

//            public Node(AtomicReference<Thread> edit)
//            {
//                _edit = edit;
//                _array = new object[32];
//            }
        
//            #endregion
//        }

//        #endregion

//        #region Data

//        static readonly AtomicReference<Thread> NoEdit = new AtomicReference<Thread>();
//        internal static readonly Node EmptyNode = new Node(NoEdit, new object[32]);

//        readonly int _cnt;
//        readonly int _shift;
//        readonly Node _root;
//        readonly object[] _tail;

//        public int Shift { get { return _shift; } }
//        public Node Root { get { return _root; } }
//        public object[] Tail() { return _tail; } 

//        readonly IPersistentMap _meta;

//        /// <summary>
//        /// An empty <see cref="PersistentVector">PersistentVector</see>.
//        /// </summary>
//        static public readonly PersistentVector EMPTY = new PersistentVector(0,5,EmptyNode, new object[0]);

//        #endregion

//        #region Transient vector conj

//        private sealed class TransientVectorConjer : AFn
//        {
//            public override object invoke(object coll, object val)
//            {
//                return ((ITransientVector)coll).conj(val);
//            }

//            public override object invoke(object coll)
//            {
//                return coll;
//            }
//        }

//        static readonly IFn _transientVectorConj = new TransientVectorConjer();

//        #endregion

//        #region C-tors and factory methods

//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        public static PersistentVector adopt(Object[] items)
//        {
//            return new PersistentVector(items.Length, 5, EmptyNode, items);
//        }

//        /// <summary>
//        /// Create a <see cref="PersistentVector">PersistentVector</see> from an <see cref="ISeq">IReduceInit</see>.
//        /// </summary>
//        /// <param name="items"></param>
//        /// <returns></returns>
//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        static public PersistentVector create(IReduceInit items)
//        {
//            TransientVector ret = (TransientVector)EMPTY.asTransient();
//            items.reduce(_transientVectorConj, ret);
//            return (PersistentVector)ret.persistent();
//        }

//        /// <summary>
//        /// Create a <see cref="PersistentVector">PersistentVector</see> from an <see cref="ISeq">ISeq</see>.
//        /// </summary>
//        /// <param name="items">A sequence of items.</param>
//        /// <returns>An initialized vector.</returns>
//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        static public PersistentVector create(ISeq items)
//        {
//            Object[] arr = new Object[32];
//            int i = 0;
//            for (; items != null && i < 32; items = items.next())
//                arr[i++] = items.first();

//            if (items != null)
//            {
//                // >32, construct with array directly
//                PersistentVector start = new PersistentVector(32, 5, EmptyNode, arr);
//                TransientVector ret = (TransientVector)start.asTransient();
//                for (; items != null; items = items.next())
//                    ret = (TransientVector)ret.conj(items.first());
//                return (PersistentVector)ret.persistent();
//            }
//            else if (i == 32)
//            {
//                // exactly 32, skip copy
//                return new PersistentVector(32, 5, EmptyNode, arr);
//            }
//            else
//            {
//                // <32, copy to minimum array and construct
//                Object[] arr2 = new Object[i];
//                Array.Copy(arr, 0, arr2, 0, i);

//                return new PersistentVector(i, 5, EmptyNode, arr2);
//            }
//        }

//        /// <summary>
//        /// Create a <see cref="PersistentVector">PersistentVector</see> from an array of items.
//        /// </summary>
//        /// <param name="items"></param>
//        /// <returns></returns>
//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        static public PersistentVector create(params object[] items)
//        {
//            ITransientCollection ret = EMPTY.asTransient();
//            foreach (object item in items)
//                ret = ret.conj(item);
//            return (PersistentVector)ret.persistent();
//        }

//        /// <summary>
//        /// Create a <see cref="PersistentVector">PersistentVector</see> from an IEnumerable.
//        /// </summary>
//        /// <param name="items"></param>
//        /// <returns></returns>
//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        static public PersistentVector create1(IEnumerable items)
//        {
//            // optimize common case
//            if (items is IList ilist)
//            {
//                int size = ilist.Count;
//                if (size <= 32)
//                {
//                    Object[] arr = new Object[size];
//                    ilist.CopyTo(arr, 0);

//                    return new PersistentVector(size, 5, PersistentVector.EmptyNode, arr);
//                }
//            }

//            ITransientCollection ret = EMPTY.asTransient();
//            foreach (object item in items)
//            {
//                ret = ret.conj(item);
//            }
//            return (PersistentVector)ret.persistent();
//        }


//        /// <summary>
//        /// Initialize a <see cref="PersistentVector">PersistentVector</see> from basic components.
//        /// </summary>
//        /// <param name="cnt"></param>
//        /// <param name="shift"></param>
//        /// <param name="root"></param>
//        /// <param name="tail"></param>
//        public PersistentVector(int cnt, int shift, Node root, object[] tail)
//        {
//            _meta = null;
//            _cnt = cnt;
//            _shift = shift;
//            _root = root;
//            _tail = tail;
//        }


//        /// <summary>
//        /// Initialize a <see cref="PersistentVector">PersistentVector</see> from given metadata and basic components.
//        /// </summary>
//        /// <param name="meta"></param>
//        /// <param name="cnt"></param>
//        /// <param name="shift"></param>
//        /// <param name="root"></param>
//        /// <param name="tail"></param>
//        PersistentVector(IPersistentMap meta, int cnt, int shift, Node root, object[] tail)
//        {
//            _meta = meta;
//            _cnt = cnt;
//            _shift = shift;
//            _root = root;
//            _tail = tail;
//        }

//        #endregion

//        #region IObj members

//        public IObj withMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new PersistentVector(meta, _cnt, _shift, _root, _tail);
//        }

//        #endregion

//        #region IMeta Members

//        public IPersistentMap meta()
//        {
//            return _meta;
//        }

//        #endregion

//        #region IPersistentVector members

//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        int tailoff()
//        {
//            if (_cnt < 32)
//                return 0;
//            return ((_cnt - 1) >> 5) << 5;
//        }


//        /// <summary>
//        /// Get the i-th item in the vector.
//        /// </summary>
//        /// <param name="i">The index of the item to retrieve/</param>
//        /// <returns>The i-th item</returns>
//        /// <remarks>Throws an exception if the index <c>i</c> is not in the range of the vector's elements.</remarks>
//        public override object nth(int i)
//        {
//            object[] node = ArrayFor(i);
//            return node[i & 0x01f];
//        }

//        public override Object nth(int i, Object notFound)
//        {
//            if (i >= 0 && i < _cnt)
//                return nth(i);
//            return notFound;
//        }

//        object[] ArrayFor(int i) 
//        {
//            if (i >= 0 && i < _cnt)
//            {
//                if (i >= tailoff())
//                    return _tail;
//                Node node = _root;
//                for (int level = _shift; level > 0; level -= 5)
//                    node = (Node)node.Array[(i >> level) & 0x01f];
//                return node.Array;
//            }
//            throw new ArgumentOutOfRangeException("i");
//        }


//        /// <summary>
//        /// Return a new vector with the i-th value set to <c>val</c>.
//        /// </summary>
//        /// <param name="i">The index of the item to set.</param>
//        /// <param name="val">The new value</param>
//        /// <returns>A new (immutable) vector v with v[i] == val.</returns>
//        public override IPersistentVector assocN(int i, Object val)
//        {
//            if (i >= 0 && i < _cnt)
//            {
//                if (i >= tailoff())
//                {
//                    object[] newTail = new object[_tail.Length];
//                    Array.Copy(_tail, newTail, _tail.Length);
//                    newTail[i & 0x01f] = val;

//                    return new PersistentVector(meta(), _cnt, _shift, _root, newTail);
//                }

//                return new PersistentVector(meta(), _cnt, _shift, doAssoc(_shift, _root, i, val), _tail);
//            }
//            if (i == _cnt)
//                return cons(val);
//            throw new ArgumentOutOfRangeException("i");
//        }

//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        static private Node doAssoc(int level, Node node, int i, object val)
//        {
//            Node ret = new Node(node.Edit, (object[])node.Array.Clone());
//            if (level == 0)
//                ret.Array[i & 0x01f] = val;
//            else
//            {
//                int subidx = ( i >> level ) & 0x01f;
//                ret.Array[subidx] = doAssoc(level-5,(Node) node.Array[subidx], i, val);
//            }
//            return ret;
//        }

//        /// <summary>
//        /// Creates a new vector with a new item at the end.
//        /// </summary>
//        /// <param name="o">The item to add to the vector.</param>
//        /// <returns>A new (immutable) vector with the objected added at the end.</returns>
//        /// <remarks>Overrides <c>cons</c> in <see cref="IPersistentCollection">IPersistentCollection</see> to specialize the return value.</remarks>

//        public override IPersistentVector cons(object o)
//        {
//            //if (_tail.Length < 32)
//            if ( _cnt - tailoff() < 32 )
//            {
//                object[] newTail = new object[_tail.Length + 1];
//                Array.Copy(_tail, newTail, _tail.Length);
//                newTail[_tail.Length] = o;
//                return new PersistentVector(meta(), _cnt + 1, _shift, _root, newTail);
//            }

//            // full tail, push into tree
//            Node newroot;
//            Node tailnode = new Node(_root.Edit, _tail);
//            int newshift = _shift;
            
//            // overflow root?
//            if ((_cnt >> 5) > (1 << _shift))
//            {
//                newroot = new Node(_root.Edit);
//                newroot.Array[0] = _root;
//                newroot.Array[1] = newPath(_root.Edit, _shift, tailnode);
//                newshift += 5;
//            }
//            else
//                newroot = pushTail(_shift, _root, tailnode);


//            return new PersistentVector(meta(), _cnt + 1, newshift, newroot, new object[] { o });
//        }

//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        private Node pushTail(int level, Node parent, Node tailnode)
//        {
//            // if parent is leaf, insert node,
//            // else does it map to existing child?  -> nodeToInsert = pushNode one more level
//            // else alloc new path
//            // return nodeToInsert placed in copy of parent
//            int subidx = ((_cnt - 1) >> level) & 0x01f;
//            Node ret = new Node(parent.Edit, (object[])parent.Array.Clone());
//            Node nodeToInsert;

//            if (level == 5)
//                nodeToInsert = tailnode;
//            else
//            {
//                Node child = (Node)parent.Array[subidx];
//                nodeToInsert = (child != null
//                                 ? pushTail(level - 5, child, tailnode)
//                                 : newPath(_root.Edit, level - 5, tailnode));
//            }
//            ret.Array[subidx] = nodeToInsert;
//            return ret;
//        }

//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        static Node newPath(AtomicReference<Thread> edit, int level, Node node)
//        {
//            if (level == 0)
//                return node;

//            Node ret = new Node(edit);
//            ret.Array[0] = newPath(edit, level - 5, node);
//            return ret;
//        }

//        #endregion

//        #region IPersistentCollection members

//        /// <summary>
//        /// Gets the number of items in the collection.
//        /// </summary>
//        /// <returns>The number of items in the collection.</returns>
//        public override int count()
//        {
//            return _cnt;
//        }

//        /// <summary>
//        /// Gets an empty collection of the same type.
//        /// </summary>
//        /// <returns>An emtpy collection.</returns>
//        public override IPersistentCollection empty()
//        {
//            return (IPersistentCollection)EMPTY.withMeta(meta());
//        }
        
//        #endregion

//        #region IPersistentStack members

//        /// <summary>
//        /// Returns a new stack with the top element popped.
//        /// </summary>
//        /// <returns>The new stack.</returns>
//        public override IPersistentStack pop()
//        {
//            if ( _cnt == 0 )
//                throw new InvalidOperationException("Can't pop empty vector");
//            if ( _cnt == 1)
//                return (IPersistentStack)EMPTY.withMeta(meta());
//            //if ( _tail.Length > 1 )
//            if (_cnt - tailoff() > 1)
//            {
//                object[] newTail = new object[_tail.Length-1];
//                Array.Copy(_tail,newTail,newTail.Length);
//                return new PersistentVector(meta(),_cnt-1,_shift,_root,newTail);
//            }
//            object[] newtail = ArrayFor(_cnt - 2);

//            Node newroot = popTail(_shift,_root);
//            int newshift = _shift;
//            if ( newroot == null )
//                newroot = EmptyNode;
//            if ( _shift > 5 && newroot.Array[1] == null )
//            {
//                newroot = (Node)newroot.Array[0];
//                newshift -= 5;
//            }
//            return new PersistentVector(meta(), _cnt - 1, newshift, newroot, newtail);
//        }

//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        private Node popTail(int level, Node node)
//        {
//            int subidx = ((_cnt - 2) >> level) & 0x01f;
//            if (level > 5)
//            {
//                Node newchild = popTail(level - 5, (Node)node.Array[subidx]);
//                if (newchild == null && subidx == 0)
//                    return null;
//                else
//                {
//                    Node ret = new Node(_root.Edit, (object[])node.Array.Clone());
//                    ret.Array[subidx] = newchild;
//                    return ret;
//                }
//            }
//            else if (subidx == 0)
//                return null;
//            else
//            {
//                Node ret = new Node(_root.Edit, (object[])node.Array.Clone());
//                ret.Array[subidx] = null;
//                return ret;
//            }
//        }


//        #endregion

//        #region IFn members



//        #endregion

//        #region Seqable members

//        public override ISeq seq()
//        {
//            return chunkedSeq();
//        }

//        #endregion

//        #region ChunkedSeq

//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        public IChunkedSeq chunkedSeq()
//        {
//            if (count() == 0)
//                return null;
//            return new ChunkedSeq(this, 0, 0);
//        }

//        [Serializable]
//        sealed public class ChunkedSeq : ASeq, IChunkedSeq, Counted, IReduce, IDrop, IEnumerable
//        {
//            #region Data

//            readonly PersistentVector _vec;
//            readonly object[] _node;
//            readonly int _i;
//            readonly int _offset;

//            public int Offset
//            {
//                get { return _offset; }
//            }

//            public PersistentVector Vec
//            {
//                get { return _vec; }
//            }

//            #endregion

//            #region C-tors

//            public ChunkedSeq(PersistentVector vec, int i, int offset)
//            {
//                _vec = vec;
//                _i = i;
//                _offset = offset;
//                _node = vec.ArrayFor(i);
//            }

//            ChunkedSeq(IPersistentMap meta, PersistentVector vec, object[] node, int i, int offset)
//                : base(meta)
//            {
//                _vec = vec;
//                _node = node;
//                _i = i;
//                _offset = offset;
//            }

//            public ChunkedSeq(PersistentVector vec, object[] node, int i, int offset)
//            {
//                _vec = vec;
//                _node = node;
//                _i = i;
//                _offset = offset;
//            }

//            #endregion

//            #region IObj members


//            public override IObj withMeta(IPersistentMap meta)
//            {
//                return (meta == _meta)
//                    ? this
//                    : new ChunkedSeq(meta, _vec, _node, _i, _offset);
//            }

//            #endregion

//            #region IChunkedSeq Members

//            public IChunk chunkedFirst()
//            {
//                return new ArrayChunk(_node, _offset);
//            }

//            public ISeq chunkedNext()
//            {
//                if (_i + _node.Length < _vec._cnt)
//                    return new ChunkedSeq(_vec, _i + _node.Length, 0);
//                return null;
//            }

//            public ISeq chunkedMore()
//            {
//                ISeq s = chunkedNext();
//                if (s == null)
//                    return PersistentList.EMPTY;
//                return s;
//            }

//            #endregion

//            #region IPersistentCollection Members


//            //public new IPersistentCollection cons(object o)
//            //{
//            //    throw new NotImplementedException();
//            //}

//            #endregion

//            #region ISeq members

//            public override object first()
//            {
//                return _node[_offset];
//            }

//            public override ISeq next()
//            {
//                if (_offset + 1 < _node.Length)
//                    return new ChunkedSeq(_vec, _node, _i, _offset + 1);
//                return chunkedNext();
//            }

//            #endregion

//            #region Counted members

//            public override int count()
//            {
//                return _vec._cnt - (_i + _offset);
//            }

//            #endregion

//            #region IReduce members

//            public object reduce(IFn f)
//            {
//                object acc;
//                if (_i + _offset < _vec._cnt)
//                    acc = _node[_offset];
//                else
//                    return f.invoke();

//                for (int j = _offset + 1; j < _node.Length; j++)
//                {
//                    acc = f.invoke(acc, _node[j]);
//                    if (RT.isReduced(acc))
//                        return ((IDeref)acc).deref();
//                }

//                int step = 0;
//                for (int ii = _i + _node.Length; ii < _vec._cnt; ii += step)
//                {
//                    object[] array = _vec.ArrayFor(ii);
//                    for (int j = 0; j < array.Length; j++)
//                    {
//                        acc = f.invoke(acc, array[j]);
//                        if (RT.isReduced(acc))
//                            return ((IDeref)acc).deref();
//                    }
//                    step = array.Length;
//                }

//                return acc;
//            }
       

//            public object reduce(IFn f, object start)
//            {
//                object acc = start;

//                for (int j = _offset; j < _node.Length; j++)
//                {
//                    acc = f.invoke(acc, _node[j]);
//                    if (RT.isReduced(acc))
//                        return ((IDeref)acc).deref();
//                }

//                int step = 0;
//                for (int ii = _i + _node.Length; ii < _vec._cnt; ii += step)
//                {
//                    object[] array = _vec.ArrayFor(ii);
//                    for (int j = 0; j < array.Length; j++)
//                    {
//                        acc = f.invoke(acc, array[j]);
//                        if (RT.isReduced(acc))
//                            return ((IDeref)acc).deref();
//                    }
//                    step = array.Length;
//                }

//                return acc;
//            }

//            #endregion

//            #region IDrop members

//            public Sequential drop(int n)
//            {
//                int o = _offset + n;
//                if (o < _node.Length)   // in current array
//                    return new ChunkedSeq(_vec, _node, _i, o);
//                else
//                {
//                    int i = _i + o;
//                    if (i < _vec._cnt) // in vec
//                    {
//                        var array = _vec.ArrayFor(i);
//                        int newOffset = i % 32;
//                        return new ChunkedSeq(_vec, array, i - newOffset, newOffset);
//                    }
//                    else
//                        return null;
//                }
//            }

//            #endregion

//            #region IEnumerable

//            public override IEnumerator<object> GetEnumerator()
//            {
//                return _vec.RangedIteratorT(_i+_offset,_vec._cnt );
//            }

//            IEnumerator IEnumerable.GetEnumerator()
//            {
//                return GetEnumerator();
//            }

//            #endregion


//        }

//        #endregion

//        #region IEditableCollection Members

//        public ITransientCollection asTransient()
//        {
//            return new TransientVector(this);
//        }

//        #endregion

//        #region TransientVector class

//        class TransientVector : AFn, ITransientVector, ITransientAssociative2, Counted
//        {
//            #region Data

//            volatile int _cnt;
//            volatile int _shift;
//            volatile Node _root;
//            volatile object[] _tail;

//            #endregion

//            #region Ctors

//            TransientVector(int cnt, int shift, Node root, Object[] tail)
//            {
//                _cnt = cnt;
//                _shift = shift;
//                _root = root;
//                _tail = tail;
//            }

//            public TransientVector(PersistentVector v)
//                : this(v._cnt, v._shift, EditableRoot(v._root), EditableTail(v._tail))
//            {
//            }

//            #endregion

//            #region Counted Members

//            public int count()
//            {
//                EnsureEditable();
//                return _cnt;
//            }

//            #endregion

//            #region Implementation

//            void EnsureEditable()
//            {
//                Thread owner = _root.Edit.Get();
//                if (owner == null)
//                    throw new InvalidOperationException("Transient used after persistent! call");
//            }


//            Node EnsureEditable(Node node)
//            {
//                if (node.Edit == _root.Edit)
//                    return node;
//                return new Node(_root.Edit, (object[])node.Array.Clone());
//            }

//            static Node EditableRoot(Node node)
//            {
//                return new Node(new AtomicReference<Thread>(Thread.CurrentThread), (object[])node.Array.Clone());
//            }

//            static object[] EditableTail(object[] tl)
//            {
//                object[] ret = new object[32];
//                Array.Copy(tl, ret, tl.Length);
//                return ret;
//            }

//            Node PushTail(int level, Node parent, Node tailnode)
//            {
//                //if parent is leaf, insert node,
//                // else does it map to an existing child? -> nodeToInsert = pushNode one more level
//                // else alloc new path
//                //return  nodeToInsert placed in copy of parent
//                int subidx = ((_cnt - 1) >> level) & 0x01f;
//                Node ret = new Node(parent.Edit, (object[])parent.Array.Clone());
//                Node nodeToInsert;
//                if (level == 5)
//                {
//                    nodeToInsert = tailnode;
//                }
//                else
//                {
//                    Node child = (Node)parent.Array[subidx];
//                    nodeToInsert = (child != null)
//                        ? PushTail(level - 5, child, tailnode)
//                                                   : newPath(_root.Edit, level - 5, tailnode);
//                }
//                ret.Array[subidx] = nodeToInsert;
//                return ret;
//            }

//            int Tailoff()
//            {
//                if (_cnt < 32)
//                    return 0;
//                return ((_cnt - 1) >> 5) << 5;
//            }

//            object[] ArrayFor(int i)
//            {
//                if (i >= 0 && i < _cnt)
//                {
//                    if (i >= Tailoff())
//                        return _tail;
//                    Node node = _root;
//                    for (int level = _shift; level > 0; level -= 5)
//                        node = (Node)node.Array[(i >> level) & 0x01f];
//                    return node.Array;
//                }
//                throw new ArgumentOutOfRangeException("i");
//            }

//            object[] EditableArrayFor(int i)
//            {
//                if (i >= 0 && i < _cnt)
//                {
//                    if (i >= Tailoff())
//                        return _tail;
//                    Node node = _root;
//                    for (int level = _shift; level > 0; level -= 5)
//                        node = EnsureEditable((Node)node.Array[(i >> level) & 0x01f]);
//                    return node.Array;
//                }
//                throw new ArgumentOutOfRangeException("i");
//            }

//            #endregion

//            #region ITransientVector Members

//            public object nth(int i)
//            {
//                object[] node = ArrayFor(i);
//                return node[i & 0x01f];
//            }


//            public object nth(int i, object notFound)
//            {
//                if (i >= 0 && i < count())
//                    return nth(i);
//                return notFound;
//            }

//            public ITransientVector assocN(int i, object val)
//            {
//                EnsureEditable();
//                if (i >= 0 && i < _cnt)
//                {
//                    if (i >= Tailoff())
//                    {
//                        _tail[i & 0x01f] = val;
//                        return this;
//                    }

//                    _root = doAssoc(_shift, _root, i, val);
//                    return this;
//                }
//                if (i == _cnt)
//                    return (ITransientVector)conj(val);
//                throw new ArgumentOutOfRangeException("i");
//            }

//            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//            Node doAssoc(int level, Node node, int i, Object val)
//            {
//                node = EnsureEditable(node);
//                Node ret = new Node(node.Edit, (object[])node.Array.Clone());
//                if (level == 0)
//                {
//                    ret.Array[i & 0x01f] = val;
//                }
//                else
//                {
//                    int subidx = (i >> level) & 0x01f;
//                    ret.Array[subidx] = doAssoc(level - 5, (Node)node.Array[subidx], i, val);
//                }
//                return ret;
//            }

//            public ITransientVector pop()
//            {
//                EnsureEditable();
//                if (_cnt == 0)
//                    throw new InvalidOperationException("Can't pop empty vector");
//                if (_cnt == 1)
//                {
//                    _cnt = 0;
//                    return this;
//                }
//                int i = _cnt - 1;
//                // pop in tail?
//                if ((i & 0x01f) > 0)
//                {
//                    --_cnt;
//                    return this;
//                }
//                object[] newtail = EditableArrayFor(_cnt - 2);

//                Node newroot = PopTail(_shift, _root);
//                int newshift = _shift;
//                if (newroot == null)
//                {
//                    newroot = new Node(_root.Edit);
//                }
//                if (_shift > 5 && newroot.Array[1] == null)
//                {
//                    newroot = EnsureEditable((Node)newroot.Array[0]);
//                    newshift -= 5;
//                }
//                _root = newroot;
//                _shift = newshift;
//                --_cnt;
//                _tail = newtail;
//                return this;
//            }


//            private Node PopTail(int level, Node node)
//            {
//                node = EnsureEditable(node);
//                int subidx = ((_cnt - 2) >> level) & 0x01f;
//                if (level > 5)
//                {
//                    Node newchild = PopTail(level - 5, (Node)node.Array[subidx]);
//                    if (newchild == null && subidx == 0)
//                        return null;
//                    else
//                    {
//                        Node ret = node;
//                        ret.Array[subidx] = newchild;
//                        return ret;
//                    }
//                }
//                else if (subidx == 0)
//                    return null;
//                else
//                {
//                    Node ret = node;
//                    ret.Array[subidx] = null;
//                    return ret;
//                }
//            }

//            #endregion

//            #region ITransientAssociative Members

//            public ITransientAssociative assoc(object key, object val)
//            {
//                if (Util.IsIntegerType(key.GetType()))
//                {
//                    int i = Util.ConvertToInt(key);
//                    return assocN(i, val);
//                }
//                throw new ArgumentException("Key must be integer");
//            }

//            #endregion

//            #region ITransientCollection Members

//            public ITransientCollection conj(object val)
//            {

//                EnsureEditable();
//                int i = _cnt;
//                //room in tail?
//                if (i - Tailoff() < 32)
//                {
//                    _tail[i & 0x01f] = val;
//                    ++_cnt;
//                    return this;
//                }
//                //full tail, push into tree
//                Node newroot;
//                Node tailnode = new Node(_root.Edit, _tail);
//                _tail = new object[32];
//                _tail[0] = val;
//                int newshift = _shift;
//                //overflow root?
//                if ((_cnt >> 5) > (1 << _shift))
//                {
//                    newroot = new Node(_root.Edit);
//                    newroot.Array[0] = _root;
//                    newroot.Array[1] = newPath(_root.Edit, _shift, tailnode);
//                    newshift += 5;
//                }
//                else
//                    newroot = PushTail(_shift, _root, tailnode);
//                _root = newroot;
//                _shift = newshift;
//                ++_cnt;
//                return this;
//            }

//            public IPersistentCollection persistent()
//            {
//                EnsureEditable();
//                _root.Edit.Set(null);
//                object[] trimmedTail = new object[_cnt-Tailoff()];
//                Array.Copy(_tail,trimmedTail,trimmedTail.Length);
//                return new PersistentVector(_cnt, _shift, _root, trimmedTail);
//            }

//            #endregion

//            #region ITransientAssociative2 methods

//            private static readonly Object NOT_FOUND = new object();

//            public bool containsKey(object key)
//            {
//                return valAt(key, NOT_FOUND) != NOT_FOUND;
//            }

//            public IMapEntry entryAt(object key)
//            {
//                Object v = valAt(key, NOT_FOUND);
//                if (v != NOT_FOUND)
//                    return MapEntry.create(key, v);
//                return null;
//            }

//            #endregion


//            #region ILookup Members


//            public object valAt(object key)
//            {
//                // note - relies on EnsureEditable in 2-arg valAt
//                return valAt(key, null);
//            }

//            public object valAt(object key, object notFound)
//            {
//                EnsureEditable();
//                if (Util.IsIntegerType(key.GetType()))
//                {
//                    int i = Util.ConvertToInt(key);
//                    if (i >= 0 && i < count())
//                        return nth(i);
//                }
//                return notFound;
//            }

//            #endregion
//        }
 
//        #endregion

//        #region IReduce members and kvreduce

//        public object reduce(IFn f)
//        {
//            Object init;
//            if (_cnt > 0)
//                init = ArrayFor(0)[0];
//            else
//                return f.invoke();
//            int step;
//            for (int i = 0; i < _cnt; i += step)
//            {
//                Object[] array = ArrayFor(i);
//                for (int j = (i == 0) ? 1 : 0; j < array.Length; ++j)
//                {
//                    init = f.invoke(init, array[j]);
//                    if (RT.isReduced(init))
//                        return ((IDeref)init).deref();
//                }
//                step = array.Length;
//            }
//            return init;
//        }

//        public object reduce(IFn f, object start)
//        {
//            int step;
//            for (int i = 0; i < _cnt; i += step)
//            {
//                Object[] array = ArrayFor(i);
//                for (int j = 0; j < array.Length; ++j)
//                {
//                    start = f.invoke(start, array[j]);
//                    if (RT.isReduced(start))
//                        return ((IDeref)start).deref();
//                }
//                step = array.Length;
//            }
//            return start;
//        }

//        public object kvreduce(IFn f, object init)
//        {
//            int step;
//            for (int i = 0; i < _cnt; i += step)
//            {
//                object[] array = ArrayFor(i);
//                for (int j = 0; j < array.Length; j++)
//                {
//                    init = f.invoke(init, j + i, array[j]);
//                    if (RT.isReduced(init))
//                        return ((IDeref)init).deref();
//                }
//                step = array.Length;
//            }
//            return init;
//        }

//        #endregion

//        #region IDrop members

//        public Sequential drop(int n)
//        {
//            if (n < 0)
//                return this;
//            else if (n < _cnt)
//            {
//                int offset = n % 32;
//                return new ChunkedSeq(this, this.ArrayFor(n), n - offset, offset);
//            }
//            else
//                return null;
//        }

//        #endregion

//        #region Ranged iterator

//        public override IEnumerator RangedIterator(int start, int end)
//        {
//            int i = start;
//            int b = i - (i%32);
//            object[] arr = (start < count()) ? ArrayFor(i) : null;

//            while (i < end)
//            {
//                if (i - b == 32)
//                {
//                    arr = ArrayFor(i);
//                    b += 32;
//                }
//                yield return arr[i++ & 0x01f];
//            }
//        }

//        public override IEnumerator<object> RangedIteratorT(int start, int end)
//        {
//            int i = start;
//            int b = i - (i % 32);
//            object[] arr = (start < count()) ? ArrayFor(i) : null;

//            while (i < end)
//            {
//                if (i - b == 32)
//                {
//                    arr = ArrayFor(i);
//                    b += 32;
//                }
//                yield return arr[i++ & 0x01f];
//            }
//        }

//        IEnumerator IEnumerable.GetEnumerator()
//        {
//            return RangedIterator(0, count());
//        }
        
//        public override IEnumerator<object> GetEnumerator()
//        {
//            return RangedIteratorT(0, count());

//        }

//        #endregion
//    }
//}



// Move this after PersistentVector

//type IPVecSubVector(meta: IPersistentMap, vec:IPersistentVector, start:int, finish:int) =
//    inherit APersistentVector()

//    member this.Start = start
//    member this.Finish = finish
//    member this.Vector = vec


//    static member Create(meta,vec:IPersistentVector,start,finish) =
//        match vec with
//        | :? IPVecSubVector as sv ->
//            IPVecSubVector(meta,sv,start+sv.Start,finish+sv.Finish)
//        | _ -> IPVecSubVector(meta,vec,start,finish)

//    interface IMeta with
//        member this.meta() = meta

//    interface IObj with
//        member this.withMeta(newMeta) =
//            if meta =  newMeta then
//                this
//            else
//                IPVecSubVector(newMeta,vec,start,finish)

//    interface IPersistentCollection with
//        override _.empty() = upcast (PersistentVector.Empty:>IObj).withMeta(meta)

//    interface IPersistentStack with
//        override _.pop() =
//            if finish-1 = start then
//                upcast PersistentVector.Empty
//            else
//                IPVecSubVector(meta,vec,start,finish-1)

//    interface Indexed with
//        member _.nth(i) =
//            if start+i >= finish || i < 0 then
//                raise <| ArgumentOutOfRangeException("i")
//            else
//                vec.nth(start+i)

//    interface IPersistentVector with
//        override _.count() = finish-start
//        override _.cons(o) = IPVecSubVector(meta,vec.assocN(finish,o),start,finish+1)
//        override this.assocN(i,v) =
//            if start+i > finish then
//                 raise <| ArgumentOutOfRangeException("i")
//            elif start+i = finish then
//                (this:>IPersistentVector).cons(v)
//            else
//                IPVecSubVector(meta,vec.assocN(start+i,v),start,finish)

//     interface IEnumerable with
//        override _.GetEnumerator() =
//            match vec with
//            | :? APersistentVector as av ->
//                av.RangedIterator(start,finish)
//            | _ -> base.GetMyEnumerator()

//     interface IEnumerable<obj> with
//        override _.GetEnumerator() =
//            match vec with
//            | :? APersistentVector as av ->
//                av.RangedIteratorT(start,finish)
//            | _ -> base.GetMyEnumeratorT()

//    interface IKVReduce with
//        member this.kvreduce(f,init) =
//            let cnt = (this:>IPersistentVector).count()
//            let rec step (i:int) ret =
//                match ret with
//                | :? Reduced as red -> (red:>IDeref).deref()
//                | _ when i <= cnt -> ret
//                | _ -> step (i+1) (f.invoke(ret,i,vec.nth(start+i)))
//            step 0 init


and MapEntry(key, value) =
    inherit AMapEntry()

    static member create(k, v) = MapEntry(k, v)

    interface IMapEntry with
        member this.key() = key
        member this.value() = value




and PersistentVector() =
    inherit APersistentVector()
