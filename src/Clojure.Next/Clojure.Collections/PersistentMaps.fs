namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics




[<AbstractClass>]
type APersistentMap() =
    inherit AFn()

    let mutable hasheq: int option = None

    override this.ToString() = RTPrint.printString (this)
    override this.Equals(o) = APersistentMap.mapEquals (this, o)

    override this.GetHashCode() =
        match hasheq with
        | Some h -> h
        | None ->
            let h = Hashing.hashUnordered (this)
            hasheq <- Some h
            h


    static member mapEquals(m1: IPersistentMap, o: obj) =
        match o with
        | _ when obj.ReferenceEquals(m1, o) -> true
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

                loop (RT0.seq (o)) this

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

            if not <| isNull s then
                (s :?> ICollection).CopyTo(arr, idx)


    interface IDictionary with
        member _.IsFixedSize = true
        member _.IsReadOnly = true
        member this.Contains(key) = (this:>Associative).containsKey(key)
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



//    public class PersistentArrayMap : APersistentMap, IObj, IEditableCollection, IMapEnumerable, IMapEnumerableTyped<Object,Object>, IEnumerable, IEnumerable<IMapEntry>, IKVReduce, IDrop

type PersistentArrayMap private (meta:IPersistentMap, arr: obj array) =
    inherit APersistentMap()

    new(arr) = PersistentArrayMap(null,arr)
    new() = PersistentArrayMap(null,Array.zeroCreate 0)

    

    static member val private hashTableThreshold : int = 16

    static member val public Empty = PersistentArrayMap()

    
    // In the original this had Keyword as a special case.
    // I'm not ready for that yet.
    // So I'll put in a mutable static to be set up later during initialization.
    // Sigh.
    static member val keywordCheck : obj -> bool  =  (fun _ -> false) with get, set


    interface IObj with
        override this.withMeta(m) = 
            if obj.ReferenceEquals(m,meta) then this else PersistentArrayMap(m,arr)

    interface IMeta with
        override _.meta() = meta

    interface ILookup with
        override this.valAt(key) = (this:>ILookup).valAt(key,null)
        override this.valAt(key,nf) =
            let idx = this.indexOfKey(key)
            if idx >= 0 then arr[idx+1] else nf

    member this.indexOfObject(key:obj) =
        let ep = Util.getEquivPred(key)
        let rec loop (i:int) =
            if i >= arr.Length then -1
            elif ep(key,arr[i]) then i
            else loop (i+2)
        loop 0

    member this.indexOfKey(key:obj) =
        if PersistentArrayMap.keywordCheck(key) then
            let rec loop (i:int) =
                if i >= arr.Length then -1
                elif key = arr[i] then i
                else loop (i+2)
            loop 0
        else
            this.indexOfObject(key)

    static member equalKey(k1:obj, k2:obj) =
        PersistentArrayMap.keywordCheck(k1) && k1 = k2 || Util.equiv(k1,k2)

    interface Associative with
        override this.containsKey(k) = this.indexOfKey(k) >= 0
        override this.entryAt(k) =
            let i = this.indexOfKey(k)
            if i >= 0 then
                MapEntry.create(arr[i],arr[i+1]) :> IMapEntry
            else
                null

    interface Seqable with
        override this.seq() = if arr.Length > 0 then PersistentArrayMapSeq(arr,0) else null        

    interface IPersistentCollection with
        override this.empty() = (PersistentArrayMap.Empty :> IObj).withMeta(meta) :?> IPersistentCollection

    interface IPersistentMap with
        override _.count() = arr.Length/2
        override this.assoc(k,v) = 
            let i = this.indexOfKey(i)
            if i >= 0 then
                if arr[i+1] = v then
                    this
                else
                    let newArray = arr.Clone() :?> obj array
                    newArray[i+1] <- v
                    PersistentArrayMap.create(newArray)
            elif arr.Length >= PersistentArrayMap.hashTableThreshold then
                this.createHashTree(arr).assoc(k,v)
            else
                let newArray : obj array = Array.zeroCreate arr.Length + 2
                if arr.Length > 0 then Array.Copy(arr,0,newArray,0,array.Length)
                newArray[newArray.Length-2] <- k
                newArray[newArray.Length-1] <- v
                PersistentArrayMap.create(newArray)
        override this.assocEx(k,v) =
            let i = this.indexOfKey(k)
            if i >= 0 then
                raise <| InvalidOperationException("Key already present")
            else
                (this:>IPersistentMap).assoc(k,v)
        override this.without(k) =
            let i = this.indexOfKey(k)
            if i < 0 then
                this
            else
                // key exists, remove
                let newLen = arr.Length-2
                if newLen = 0 then
                    (this:>IPersistentCollection).empty() :?> IPersistentMap
                else
                    let newArray : obj array = Array.zeroCreate newLen
                    Array.Copy(arr,0,newArray,0,i)
                    Array.Copy(arr,i+2,newArray,i,newLen-i)
                    PersistentArrayMap.create(newArray)


    member this.createHashTree(init : obj array ) = PersistentHashMap.create(meta,init)

    interface IEditableCollection with
        member _.asTransient() = TransientArrayMap(arr)

    interface IKVReduce with
        member this.kvreduce(f,init) =
            let rec loop (acc:obj) (i:int) =
                if i >= arr.Length then acc
                else
                    match f.invoke(acc,arr[i],arr[i+1]) with
                    | :? Reduced as red -> (red:>IDeref).deref()
                    | newAcc -> loop newAcc (i+2)
            loop init 0        
                    
    interface IDrop with
        member this.drop(n) =
            if arr.Length > 0 then
                ((this:>Seqable).seq() :?> PersistentArrayMapSeq).drop(n)
            else
                null


    interface IMapEnumerable with
        member this.keyEnumerator() = (this:>IMapEnumerableTyped<obj,obj>).tkeyEnumerator()
        member this.valEnumerator() = (this:>IMapEnumerableTyped<obj,obj>).tvalEnumerator()

    interface IMapEnumerableTyped<obj,obj> with
        member this.tkeyEnumerator() = (seq { for i in 0 .. arr.Length .. 2 -> arr[i] }).GetEnumerator()
        member this.tkeyEnumerator() = (seq { for i in 0 .. arr.Length .. 2 -> arr[i+1] }).GetEnumerator()

    interface IEnumerable with
        member this.GetEnumerator() = (this:>IEnumerable<IMapEntry>).GetEnumerator()

    interface IEnumerable<IMapEntry> with
        member this.GetEnumerator() = (seq { for i in 0 .. arr.Length .. 2 -> (MapEntry.create(arr[i],arr[i+1]) :> IMapEntry) }).GetEnumerator()

    interface IEnumerable<KeyValuePair<obj,obj>> with
        member this.GetEnumerator() = (seq { for i in 0 .. arr.Length .. 2 -> KeyValuePair(arr[i],arr[i+1]) }).GetEnumerator()



//        #region C-tors and factory methods

//        /// <summary>
//        /// Create a <see cref="PersistentArrayMap">PersistentArrayMap</see> (if small enough, else create a <see cref="PersistentHashMap">PersistentHashMap</see>.
//        /// </summary>
//        /// <param name="other">The BCL map to initialize from</param>
//        /// <returns>A new persistent map.</returns>
//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        public static IPersistentMap create(IDictionary other)
//        {
//            ITransientMap ret = (ITransientMap)EMPTY.asTransient();
//            foreach (DictionaryEntry de in other)
//                ret = ret.assoc(de.Key, de.Value);
//            return ret.persistent();

//        }

//        /// <summary>
//        /// Create a <see cref="PersistentArrayMap">PersistentArrayMap</see> with new data but same metadata as the current object.
//        /// </summary>
//        /// <param name="init">The new key/value array</param>
//        /// <returns>A new <see cref="PersistentArrayMap">PersistentArrayMap</see>.</returns>
//        /// <remarks>The array is used directly.  Do not modify externally or immutability is sacrificed.</remarks>
//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        PersistentArrayMap create(params object[] init)
//        {
//            return new PersistentArrayMap(meta(), init);
//        }


//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        public static PersistentArrayMap createWithCheck(Object[] init)
//        {
//            for (int i = 0; i < init.Length; i += 2)
//            {
//                for (int j = i + 2; j < init.Length; j += 2)
//                {
//                    if (EqualKey(init[i], init[j]))
//                        throw new ArgumentException("Duplicate key: " + init[i]);
//                }
//            }
//            return new PersistentArrayMap(init);
//        }


//        // This method attempts to find resue [sic] the given array as the basis for an array map as quickly as possible.
//        // If a trailing element exists in the array or it contains duplicate keys then it delegates to the complex path.
//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        public static PersistentArrayMap createAsIfByAssoc(Object[] init)
//        {
//            bool complexPath, hasTrailing;
//            complexPath = hasTrailing = ((init.Length & 1) == 1);

//            for (int i = 0; i < init.Length && !complexPath; i += 2)
//            {
//                for (int j = 0; j < i; j += 2)
//                {
//                    if (EqualKey(init[i], init[j]))
//                    {
//                        complexPath = true;
//                        break;
//                    }
//                }
//            }

//            if (complexPath) return createAsIfByAssocComplexPath(init, hasTrailing);

//            return new PersistentArrayMap(init);
//        }

//        private static object[] GrowSeedArray(Object[] seed, IPersistentCollection trailing)
//        {
//            ISeq extraKVs = trailing.seq();
//            int seedCount = seed.Length - 1;
//            Array.Resize(ref seed, seedCount + trailing.count() * 2);
//            for (int i = seedCount; extraKVs != null; extraKVs = extraKVs.next(), i += 2)
//            {
//                IMapEntry e = (MapEntry)extraKVs.first();
//                seed[i] = e.key();
//                seed[i + 1] = e.val();
//            }

//            return seed;
//        }

//        // This method handles the default case of an array containing alternating key/value pairs.
//        // It will reallocate a smaller init array if duplicate keys are found.
//        //
//        // If a trailing element is found then will attempt to add it to the resulting map as if by conj.
//        // NO guarantees about the order of the keys in the trailing element are made.
//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]
//        private static PersistentArrayMap createAsIfByAssocComplexPath(object[] init, bool hasTrailing)
//        {
//            if (hasTrailing)
//            {
//                IPersistentCollection trailing = PersistentArrayMap.EMPTY.cons(init[init.Length-1]);
//                init = GrowSeedArray(init, trailing);
//            }


//            // ClojureJVM says: If this looks like it is doing busy-work, it is because it
//            // is achieving these goals: O(n^2) run time like
//            // createWithCheck(), never modify init arg, and only
//            // allocate memory if there are duplicate keys.
//            int n = 0;
//            for (int i = 0; i < init.Length; i += 2)
//            {
//                bool duplicateKey = false;
//                for (int j = 0; j < i; j += 2)
//                {
//                    if (EqualKey(init[i], init[j]))
//                    {
//                        duplicateKey = true;
//                        break;
//                    }
//                }
//                if (!duplicateKey)
//                    n += 2;
//            }
//            if (n < init.Length)
//            {
//                // Create a new shorter array with unique keys, and
//                // the last value associated with each key.  To behave
//                // like assoc, the first occurrence of each key must
//                // be used, since its metadata may be different than
//                // later equal keys.
//                Object[] nodups = new Object[n];
//                int m = 0;
//                for (int i = 0; i < init.Length; i += 2)
//                {
//                    bool duplicateKey = false;
//                    for (int j = 0; j < m; j += 2)
//                    {
//                        if (EqualKey(init[i], nodups[j]))
//                        {
//                            duplicateKey = true;
//                            break;
//                        }
//                    }
//                    if (!duplicateKey)
//                    {
//                        int j;
//                        for (j = init.Length - 2; j >= i; j -= 2)
//                        {
//                            if (EqualKey(init[i], init[j]))
//                            {
//                                break;
//                            }
//                        }
//                        nodups[m] = init[i];
//                        nodups[m + 1] = init[j + 1];
//                        m += 2;
//                    }
//                }
//                if (m != n)
//                    throw new ArgumentException("Internal error: m=" + m);
//                init = nodups;
//            }
//            return new PersistentArrayMap(init);
//        }
       

//        #region TransientArrayMap class

//        sealed class TransientArrayMap : ATransientMap
//        {
//            #region Data

//            volatile int _len;
//            readonly object[] _array;
            
//            [NonSerialized] volatile Thread _owner;

//            #endregion

//            #region Ctors


//            public TransientArrayMap(object[] array)
//            {
//                _owner = Thread.CurrentThread;
//                _array = new object[Math.Max(HashtableThreshold, array.Length)];
//                Array.Copy(array, _array, array.Length);
//                _len = array.Length;
//            }

//            #endregion

//            #region

//            /// <summary>
//            /// Gets the index of the key in the array.
//            /// </summary>
//            /// <param name="key">The key to search for.</param>
//            /// <returns>The index of the key if found; -1 otherwise.</returns>
//            private int IndexOfKey(object key)
//            {
//                for (int i = 0; i < _len; i += 2)
//                    if (EqualKey(_array[i], key))
//                        return i;
//                return -1;
//            }

//            protected override void EnsureEditable()
//            {
//                if (_owner == null )
//                    throw new InvalidOperationException("Transient used after persistent! call");
//            }

//            protected override ITransientMap doAssoc(object key, object val)
//            {
//                int i = IndexOfKey(key);
//                if (i >= 0) //already have key,
//                {
//                    if (_array[i + 1] != val) //no change, no op
//                        _array[i + 1] = val;
//                }
//                else //didn't have key, grow
//                {
//                    if (_len >= _array.Length)
//                        return ((ITransientMap)PersistentHashMap.create(_array).asTransient()).assoc(key, val);
//                    _array[_len++] = key;
//                    _array[_len++] = val;
//                }
//                return this;
//            }


//            protected override ITransientMap doWithout(object key)
//            {
//                int i = IndexOfKey(key);
//                if (i >= 0) //have key, will remove
//                {
//                    if (_len >= 2)
//                    {
//                        _array[i] = _array[_len - 2];
//                        _array[i + 1] = _array[_len - 1];
//                    }
//                    _len -= 2;
//                }
//                return this;
//            }

//            protected override object doValAt(object key, object notFound)
//            {
//                int i = IndexOfKey(key);
//                if (i >= 0)
//                    return _array[i + 1];
//                return notFound;
//            }

//            protected override int doCount()
//            {
//                return _len / 2;
//            }

//            protected override IPersistentMap doPersistent()
//            {
//                EnsureEditable();
//                _owner = null;
//                object[] a = new object[_len];
//                Array.Copy(_array, a, _len);
//                return new PersistentArrayMap(a);
//            }

//            #endregion
//        }

//        #endregion


// ====================================================================

//        /// <summary>
//        /// Internal class providing an <see cref="ISeq">ISeq</see> 
//        /// for <see cref="PersistentArrayMap">PersistentArrayMap</see>s.
//        /// </summary>
//        [Serializable]
//        protected class Seq : ASeq, Counted, IReduce, IDrop, IEnumerable
//        {
//            #region Data

//            /// <summary>
//            /// The array to iterate over.
//            /// </summary>
//            private readonly object[] _array;

//            /// <summary>
//            /// Current index position in the array.
//            /// </summary>
//            private readonly int _i;

//            #endregion

//            #region C-tors & factory methods

//            /// <summary>
//            /// Initialize the sequence to a given array and index.
//            /// </summary>
//            /// <param name="array">The array being sequenced over.</param>
//            /// <param name="i">The current index.</param>
//            public Seq(object[] array, int i)
//            {
//                _array = array;
//                _i = i;
//            }

//            /// <summary>
//            /// Initialize the sequence with given metatdata and array/index.
//            /// </summary>
//            /// <param name="meta">The metadata to attach.</param>
//            /// <param name="array">The array being sequenced over.</param>
//            /// <param name="i">The current index.</param>
//            public Seq(IPersistentMap meta, object[] array, int i)
//                : base(meta)
//            {
//                _array = array;
//                _i = i;
//            }

//            #endregion

//            #region ISeq members

//            /// <summary>
//            /// Gets the first item.
//            /// </summary>
//            /// <returns>The first item.</returns>
//            public override object first()
//            {
//                return MapEntry.create(_array[_i], _array[_i + 1]);
//            }

//            /// <summary>
//            /// Return a seq of the items after the first.  Calls <c>seq</c> on its argument.  If there are no more items, returns nil."
//            /// </summary>
//            /// <returns>A seq of the items after the first, or <c>nil</c> if there are no more items.</returns>
//            public override ISeq next()
//            {
//                return _i + 2 < _array.Length
//                    ? new Seq(_array, _i + 2)
//                    : null;
//            }

//            #endregion

//            #region IPersistentCollection members
//            /// <summary>
//            /// Gets the number of items in the collection.
//            /// </summary>
//            /// <returns>The number of items in the collection.</returns>
//            public override int count()
//            {
//                return (_array.Length - _i) / 2;
//            }

//            #endregion

//            #region IObj members

//            /// <summary>
//            /// Create a copy with new metadata.
//            /// </summary>
//            /// <param name="meta">The new metadata.</param>
//            /// <returns>A copy of the object with new metadata attached.</returns>
//            public override IObj withMeta(IPersistentMap meta)
//            {
//                if (_meta == meta)
//                    return this;

//                return new Seq(meta, _array, _i);
//            }

//            #endregion

//            #region IReduce members

//            public object reduce(IFn f)
//            {
//                if (_i < _array.Length)
//                {
//                    Object acc = MapEntry.create(_array[_i], _array[_i + 1]);
//                    for (int j = _i + 2; j < _array.Length; j += 2)
//                    {
//                        acc = f.invoke(acc, MapEntry.create(_array[j], _array[j + 1]));
//                        if (RT.isReduced(acc))
//                            return ((IDeref)acc).deref();
//                    }
//                    return acc;
//                }
//                else
//                    return f.invoke();
//            }

//            public object reduce(IFn f, object start)
//            {
//                Object acc = start;
//                for (int j = _i; j < _array.Length; j += 2)
//                {
//                    acc = f.invoke(acc, MapEntry.create(_array[j], _array[j + 1]));
//                    if (RT.isReduced(acc))
//                        return ((IDeref)acc).deref();
//                }
//                return acc;
//            }

//            #endregion

//            #region IDrop members

//            public Sequential drop(int n)
//            {
//                if (n < count())
//                    return new Seq(_array, _i + 2 * n);
//                else
//                    return null;
//            }

//            #endregion

//            #region IEnumerable

//            public override IEnumerator<object> GetEnumerator()
//            {
//                for (int j=_i; j < _array.Length; j+=2 )
//                    yield return MapEntry.create(_array[j], _array[j+1]);
//            }

//            IEnumerator IEnumerable.GetEnumerator()
//            {
//                return GetEnumerator();
//            }
//            #endregion
//        }
