namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics


[<AbstractClass>]
type APersistentMap() =
    inherit AFn()

    let mutable hasheq : int option = None

    override this.ToString() = RTPrint.printString(this)
    override this.Equals(o) = APersistentMap.mapEquals(this,o)
    override this.GetHashCode() = 
        match hasheq with
        | Some h -> h
        | None ->
            let h = Hashing.hashUnordered(this);
            hasheq <- Some h
            h


    static member mapEquals(m1: IPersistentMap, o: obj) =
        match o with 
        | _ when obj.ReferenceEquals(m1,o) -> true
        | :? IDictionary as d ->
            if d.Count <> m1.count() then
                false
            else
                let rec loop (s:ISeq) =
                    if isNull s then
                        true
                    else
                        let me = s.first() :?> IMapEntry
                        if not <| d.Contains(me.key()) || not <| Util.equals(me.value(),d[me.key()]) then
                            false
                        else
                            loop (s.next())
                loop (m1.seq())
        | _ -> false

    interface IHashEq with
        member this.hasheq() = this.GetHashCode();


    interface ILookup with
        member _.valAt(k) = raise <| NotImplementedException("Derived classes must implement ILookup.valAt(key)")
        member _.valAt(k,nf) = raise <| NotImplementedException("Derived classes must implement ILookup.valAt(key,notFound")

    interface Associative with
        member _.containsKey(k) = raise <| NotImplementedException("Derived classes must implement Associative.containsKey(key)")
        member _.entryAt(i) = raise <| NotImplementedException("Derived classes must implement Associative.entryAt(key)")
        member this.assoc(k,v) = (this:>IPersistentMap).assoc(k,v)

    interface Seqable with
        member _.seq() = raise <| NotImplementedException("Derived classes must implement Seqable.seq()")

    interface IPersistentCollection with
        member this.cons(o) = (this:>IPersistentMap).cons(o)
        member this.count() = (this:>IPersistentMap).count()
        member _.empty() = raise <| NotImplementedException("Derived classes must implement IPersistentCollection.empty()")
        member this.equiv(o) = 
            match o with
            | :? IPersistentMap when not (o  :? MapEquivalence) -> false
            | :? IDictionary as d ->
                if d.Count <> (this:>IPersistentMap).count() then
                    false
                else
                    let rec loop (s:ISeq) =
                        if isNull s then
                            true
                        else
                            let me = s.first() :?> IMapEntry
                            if not <| d.Contains(me.key()) || not <| Util.equiv(me.value(),d[me.key()]) then
                                false
                            else
                                loop (s.next())
                    loop ((this:>Seqable).seq())
            | _ -> false

    interface IMeta with
        member _.meta() = raise <| NotImplementedException("Derived classes must implement IMeta.meta(meta)")

    interface IObj with
        member _.withMeta(meta) = raise <| NotImplementedException("Derived classes must implement IObj.withMeta(meta)")

    interface Counted with
        member this.count() = (this:>IPersistentMap).count()

    interface IPersistentMap with
        member _.assoc(k,v) = raise <| NotImplementedException("Derived classes must implement IPersistentMap.assoc(key,value)")
        member _.assocEx(k,v) = raise <| NotImplementedException("Derived classes must implement IPersistentMap.assocEx(key,value)")
        member _.without(k) = raise <| NotImplementedException("Derived classes must implement IPersistentMap.without(key)")
        member _.count() = raise <| NotImplementedException("Derived classes must implement IPersistentMap.count()")
        member this.cons(o) =
            match o with
            | :? IMapEntry as me -> (this:>IPersistentMap).assoc(me.key(),me.value())
            | :? DictionaryEntry as de -> (this:>IPersistentMap).assoc(de.Key,de.Value)
            | :? KeyValuePair<'t1,'t2> as kvp -> (this:>IPersistentMap).assoc(kvp.Key,kvp.Value)
            | :? IPersistentVector as v ->
                if v.count() = 2 then
                    (this:>IPersistentMap).assoc(v.nth(0),v.nth(1))
                else
                    raise <| ArgumentException("Vector arg to map cons must be a pair")
            | _ ->
                let rec loop (s:ISeq) (m:IPersistentMap) =
                    if isNull s then m
                    else
                        let me = s.first() :?> IMapEntry
                        loop (s.next()) (m.assoc(me.key(),me.value()))
                loop (RT0.seq(o)) this

    interface IFn with
        override this.invoke(a1) = (this:>ILookup).valAt(a1)
        override this.invoke(a1,a2) = (this:>ILookup).valAt(a1,a2)



//        #region IDictionary<Object, Object>, IDictionary Members

//        public void Add(KeyValuePair<object, object> item)
//        {
//            throw new InvalidOperationException("Cannot modify an immutable map");
//        }

//        public void Add(object key, object value)
//        {
//            throw new InvalidOperationException("Cannot modify an immutable map");
//        }

//        public void Clear()
//        {
//            throw new InvalidOperationException("Cannot modify an immutable map");
//        }

//        public bool Contains(KeyValuePair<object, object> item)
//        {
//            if (!TryGetValue(item.Key, out object value))
//                return false;

//            if (value == null)
//                return item.Value == null;

//            return value.Equals(item.Value);
//        }

//        public bool ContainsKey(object key)
//        {
//            return containsKey(key);
//        }

//        public bool Contains(object key)
//        {
//            return this.containsKey(key);
//        }


//        public virtual IEnumerator<KeyValuePair<object, object>> GetEnumerator()
//        {
//            for (ISeq s = seq(); s != null; s = s.next())
//            {
//                IMapEntry entry = (IMapEntry)s.first();
//                yield return new KeyValuePair<object, object>(entry.key(), entry.val());
//            }
//        }

//        IEnumerator IEnumerable.GetEnumerator()
//        {
//            return ((IEnumerable<IMapEntry>)this).GetEnumerator();
//        }

//        IEnumerator<IMapEntry> IEnumerable<IMapEntry>.GetEnumerator()
//        {
//            for (ISeq s = seq(); s != null; s = s.next())
//                yield return (IMapEntry)s.first();
//        }

//        IDictionaryEnumerator IDictionary.GetEnumerator()
//        {
//            return new MapEnumerator(this);
//        }

//        public bool IsFixedSize
//        {
//            get { return true; }
//        }

//        public bool IsReadOnly
//        {
//            get { return true; }
//        }


//        public ICollection<object> Keys
//        {
//            get { return KeySeq.create(seq()); }
//        }

//        ICollection IDictionary.Keys
//        {
//            get { return KeySeq.create(seq()); }
//        }


//        public bool Remove(KeyValuePair<object, object> item)
//        {
//            throw new InvalidOperationException("Cannot modify an immutable map");
//        }

//        public bool Remove(object key)
//        {
//            throw new InvalidOperationException("Cannot modify an immutable map");
//        }

//        void IDictionary.Remove(object key)
//        {
//            throw new InvalidOperationException("Cannot modify an immutable map");
//        }

//        public ICollection<object> Values
//        {
//            get { return ValSeq.create(seq()); }
//        }

//        ICollection IDictionary.Values
//        {
//            get { return ValSeq.create(seq()); }
//        }

//        public object this[object key]
//        {
//            get
//            {
//                return valAt(key);
//            }
//            set
//            {
//                throw new InvalidOperationException("Cannot modify an immutable map");
//            }
//        }

//        static readonly object _missingValue = new object();

//        public bool TryGetValue(object key, out object value)
//        {
//            object found = valAt(key, _missingValue);
//            if ( found == _missingValue)
//            {
//                value = null;
//                return false;
//            }

//            value = found;
//            return true;
//        }

//        #endregion

//        #region ICollection Members

//        public void CopyTo(KeyValuePair<object, object>[] array, int arrayIndex)
//        {
//        }

//        public void CopyTo(Array array, int index)
//        {
//            ISeq s = seq();
//            if (s != null)
//                ((ICollection)s).CopyTo(array, index);
//        }

//        public int Count
//        {
//            get { return count(); }
//        }

//        public bool IsSynchronized
//        {
//            get { return true; }
//        }

//        public object SyncRoot
//        {
//            get { return this; }
//        }

//        #endregion


//        /// <summary>
//        /// Implements a sequence across the keys of map.
//        /// </summary>
//        [Serializable]
//        public sealed class KeySeq : ASeq, IEnumerable
//        {
//            #region Data

//            readonly ISeq _seq;
//            readonly IEnumerable _enumerable;

//            #endregion

//            #region C-tors & factory methods

//            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//            static public KeySeq create(ISeq seq)
//            {
//                if (seq == null)
//                    return null;
//                return new KeySeq(seq, null);
//            }


//            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//            static public KeySeq createFromMap(IPersistentMap map)
//            {
//                if (map == null)
//                    return null;
//                ISeq seq = map.seq();
//                if (seq == null)
//                    return null;
//                return new KeySeq(seq, map);
//            }

//            private KeySeq(ISeq seq, IEnumerable enumerable)
//            {
//                _seq = seq;
//                _enumerable = enumerable;
//            }

//            private KeySeq(IPersistentMap meta, ISeq seq, IEnumerable enumerable)
//                : base(meta)
//            {
//                _seq = seq;
//                _enumerable = enumerable;
//            }

//            #endregion

//            #region ISeq members

//            public override object first()
//            {
//                object entry = _seq.first();

//                if (entry is IMapEntry me)
//                    return me.key();
//                else if (entry is DictionaryEntry de)
//                    return de.Key;
//                throw new InvalidCastException("Cannot convert hashtable entry to IMapEntry or DictionaryEntry");
//            }

//            public override ISeq next()
//            {
//                return create(_seq.next());
//            }

//            #endregion

//            #region IObj methods

//            public override IObj withMeta(IPersistentMap meta)
//            {
//                if (_meta == meta)
//                    return this;

//                return new KeySeq(meta, _seq, _enumerable);
//            }

//            #endregion

//            #region IEnumerable members

//            IEnumerator<Object> KeyIteratorT(IEnumerable enumerable)
//            {
//                foreach (Object item in enumerable)
//                    yield return ((IMapEntry)item).key();
//            }

//            public override IEnumerator<object> GetEnumerator()
//            {
//                if (_enumerable == null)
//                    return base.GetEnumerator();

//                if (_enumerable is IMapEnumerableTyped<Object, Object> imit)
//                    return (IEnumerator<object>)imit.tkeyEnumerator();


//                if (_enumerable is IMapEnumerable imi)
//                    return (IEnumerator<object>)imi.keyEnumerator();

//                return KeyIteratorT(_enumerable);
//            }

//            /// <summary>
//            /// Returns an enumerator that iterates through a collection.
//            /// </summary>
//            /// <returns>A <see cref="SeqEnumerator">SeqEnumerator</see> that iterates through the sequence.</returns>
//            IEnumerator IEnumerable.GetEnumerator()
//            {
//                return GetEnumerator();
//            }

//            #endregion

//        }

//        /// <summary>
//        /// Implements a sequence across the values of a map.
//        /// </summary>
//        [Serializable]
//        public sealed class ValSeq : ASeq, IEnumerable
//        {
//            #region Data

//            readonly ISeq _seq;
//            readonly IEnumerable _enumerable;

//            #endregion

//            #region C-tors & factory methods

//            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//            static public ValSeq create(ISeq seq)
//            {
//                if (seq == null)
//                    return null;
//                return new ValSeq(seq, null);
//            }


//            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//            static public ValSeq createFromMap(IPersistentMap map)
//            {
//                if (map == null)
//                    return null;
//                ISeq seq = map.seq();
//                if (seq == null)
//                    return null;
//                return new ValSeq(seq, map);
//            }

//            private ValSeq(ISeq seq, IEnumerable enumerable)
//            {
//                _seq = seq;
//                _enumerable = enumerable;
//            }

//            private ValSeq(IPersistentMap meta, ISeq seq, IEnumerable enumerable)
//                : base(meta)
//            {
//                _seq = seq;
//                _enumerable = enumerable;
//            }

//            #endregion

//            #region ISeq members

//            public override object first()
//            {
//                object entry = _seq.first();

//                {
//                    if (entry is IMapEntry me)
//                        return me.val();
//                }

//                if (entry is DictionaryEntry de)
//                    return de.Value;

//                throw new InvalidCastException("Cannot convert hashtable entry to IMapEntry or DictionaryEntry");
//            }

//            public override ISeq next()
//            {
//                return create(_seq.next());
//            }

//            #endregion

//            #region IObj methods

//            public override IObj withMeta(IPersistentMap meta)
//            {
//                if (_meta == meta)
//                    return this;

//                return new ValSeq(meta, _seq, _enumerable);
//            }

//            #endregion

//            #region IEnumerable members

//            IEnumerator<Object> KeyIteratorT(IEnumerable enumerable)
//            {
//                foreach (Object item in enumerable)
//                    yield return ((IMapEntry)item).val();
//            }

//            public override IEnumerator<object> GetEnumerator()
//            {
//                if (_enumerable == null)
//                    return base.GetEnumerator();

//                if (_enumerable is IMapEnumerableTyped<Object, Object> imit)
//                    return (IEnumerator<object>)imit.tvalEnumerator();


//                if (_enumerable is IMapEnumerable imi)
//                    return (IEnumerator<object>)imi.valEnumerator();

//                return KeyIteratorT(_enumerable);
//            }

//            #endregion

//            /// <summary>
//            /// Returns an enumerator that iterates through a collection.
//            /// </summary>
//            /// <returns>A <see cref="SeqEnumerator">SeqEnumerator</see> that iterates through the sequence.</returns>
//            IEnumerator IEnumerable.GetEnumerator()
//            {
//                return GetEnumerator();
//            }
//        }

//        #endregion
//    }
//}
