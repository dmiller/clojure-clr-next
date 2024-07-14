namespace Clojure.Collections

open System

// A persistent queue. (Conses onto rear, peeks/pops from front.)
// See Okasaki's Batched Queues.
// This differs in that it uses a PersistentVector as the rear, which is in-order,
// so no reversing or suspensions required for persistent use.</para>


type PersistentQueue(meta: IPersistentMap, cnt: int, front: ISeq, rear: IPersistentVector ) =
    inherit Obj(meta)

    let mutable hashCode = 0
    let mutable hasheq = 0

    static member Empty = PersistentQueue(null,0,null,null)

    override this.Equals(obj) =
        match obj with
        | :? Sequential as s ->
            let ms = RT0.seq(obj)
            let rec loop(s: ISeq, ms: ISeq) =
                match s with
                | null -> isNull ms
                | _ when isNull ms -> false
                | _ when not(Util.equals(s.first(), ms.first())) -> false
                | _ -> loop(s.next(), ms.next())
            loop((this :> Seqable).seq(), ms)
        | _ -> false

    override this.GetHashCode() =
        if hashCode = 0 then
            let mutable hash = 1
            for s in (this :> Seqable).seq() do
                hash <- 31 * hash + (if isNull (s.first()) then 0 else Util.hasheq(s.first()))
            hashCode <- hash
        hashCode

    interface IObj with
        member this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality meta m then
                this
            else
                PersistentQueue(m, cnt, front, rear)

    interface IPersistentStack with
        member this.peek() = RTSeq.first(front)
        member this.pop() =
            if isNull front then
                this
            else
                let f1 = RTSeq.next(front)
                if isNull f1 then
                    PersistentQueue(meta, cnt-1, RT0.seq(rear), null)
                else
                    PersistentQueue(meta, cnt - 1, f1, rear)
    
    interface Counted with
        member this.count() = cnt

    interface IPersistentCollection with
        member this.count() = cnt
        member this.cons(o) =
                if isNull front then
                    PersistentQueue(meta, cnt + 1, RTSeq.list1(o), null)
                else
                    PersistentQueue(meta, cnt + 1, front, (if isNull rear then PersistentVector.EMPTY  :> IPersistentVector else rear).cons(o))
        member this.empty() = (PersistentQueue.Empty :> IObj).withMeta(meta) :?> IPersistentCollection
        member this.equiv(o) =
            match o with
            | :? Sequential as s ->
                let ms = RT0.seq(o)
                let rec loop(s: ISeq, ms: ISeq) =
                    match s with
                    | null -> isNull ms
                    | _ when isNull ms -> false
                    | _ when not(Util.equiv(s.first(), ms.first())) -> false
                    | _ -> loop(s.next(), ms.next())
                loop((this :> Seqable).seq(), ms)
            | _ -> false
        member this.seq() = 
            if isNull front then
                null
            else
                PQSeq(front, rear)

    interface ICollection with
        member this.Add(item) = raise <| InvalidOperationException("Cannot modify immutable queue")
        member this.Clear() = raise <| InvalidOperationException("Cannot modify immutable queue")
        member this.Contains(item) =

            for element in (this :> IEnumerable) do
                if Util.equals(element, item) then
                    true
            false



        member this.CopyTo(array, arrayIndex) =
            


        member this.CopyTo(array, arrayIndex) =
            let i = ref arrayIndex
            for s in (this :> Seqable) do
                array.SetValue(s, !i)
                i := !i + 1
        member this.IsSynchronized = true
        member this.SyncRoot = this

(*


    public class PersistentQueue : Obj, IPersistentList, ICollection, ICollection<Object>, Counted, IHashEq
    {




        #endregion

        #region ICollection Members



        public bool Contains(object item)
        {
            foreach (object element in this)
                if (Util.Equals(element, item))
                    return true;
            return false;
        }

        public void CopyTo(object[] array, int arrayIndex)
        {
            int i = arrayIndex;
            ISeq s;
            for (s = _f; s != null; s = s.next(), i++)
                array[i] = s.first();

            for (s = _r.seq(); s != null; s = s.next(), i++)
                array[i] = s.first();
        }

        public void CopyTo(Array array, int index)
        {
            int i = index;
            ISeq s;
            for (s = _f; s != null; s = s.next(), i++)
                array.SetValue(s.first(), i);

            for (s = _r.seq(); s != null; s = s.next(), i++)
                array.SetValue(s.first(), i);
        }

        public int Count
        {
            get { return count(); }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public bool IsSynchronized
        {
            get { return true; }
        }

        public object SyncRoot
        {
            get { return true; }
        }

        public bool Remove(object item)
        {
            throw new InvalidOperationException("Cannot modify immutable queue");
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator<object> GetEnumerator()
        {
            ISeq s;
            for (s = _f; s != null; s = s.next())
                yield return s.first();

            if (_r != null)
            {
                for (s = _r.seq(); s != null; s = s.next())
                    yield return s.first();
            }
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            ISeq s;
            for (s = _f; s != null; s = s.next())
                yield return s.first();

            if (_r != null)
            {
                for (s = _r.seq(); s != null; s = s.next())
                    yield return s.first();
            }
        }

        #endregion

        #region IHashEq members

        public int hasheq()
        {
            int cached = _hasheq;
            if (cached == 0)
            {
                //int hash = 1;
                //for (ISeq s = seq(); s != null; s = s.next())
                //{
                //    hash = 31 * hash + Util.hasheq(s.first());
                //}
                //_hasheq = hash;
                _hasheq = cached = Murmur3.HashOrdered(this);
            }
            return cached;
        }
        #endregion

        /// <summary>
        /// Represents an <see cref="ISeq">ISeq</see> over a <see cref="PersistentQueue">PersistentQueue</see>.
        /// </summary>
        [Serializable]
        sealed class Seq : ASeq
        {
            #region Data

            /// <summary>
            /// The front elements.
            /// </summary>
            private readonly ISeq _f;

            /// <summary>
            /// The rear elements.
            /// </summary>
            private readonly ISeq _rseq;

            #endregion

            #region C-tors & factory methods

            /// <summary>
            /// Initializes a <see cref="Seq">PersistentQueue.Seq</see> from given front and rear elements.
            /// </summary>
            /// <param name="f">The front elements.</param>
            /// <param name="rseq">The rear elements.</param>
            internal Seq(ISeq f, ISeq rseq)
            {
                _f = f;
                _rseq = rseq;
            }

            /// <summary>
            /// Initializes a <see cref="Seq">PersistentQueue.Seq</see> from given metadata and front and rear elements.
            /// </summary>
            /// <param name="meta">The metadata to attach.</param>
            /// <param name="f">The front elements.</param>
            /// <param name="rseq">The rear elements.</param>
            internal Seq(IPersistentMap meta, ISeq f, ISeq rseq)
                : base(meta)
            {
                _f = f;
                _rseq = rseq;
            }

            #endregion

            #region IObj members

            /// <summary>
            /// Create a copy with new metadata.
            /// </summary>
            /// <param name="meta">The new metadata.</param>
            /// <returns>A copy of the object with new metadata attached.</returns>
            public override IObj withMeta(IPersistentMap meta)
            {
                if (_meta == meta)
                    return this;

                return new Seq(meta, _f, _rseq);
            }

            #endregion

            #region IPersistentCollection members

            /// <summary>
            /// Gets the number of items in the collection.
            /// </summary>
            /// <returns>The number of items in the collection.</returns>
            public override int count()
            {
                return RT.count(_f) + RT.count(_rseq);
            }

            #endregion

            #region ISeq members

            /// <summary>
            /// Gets the first item.
            /// </summary>
            /// <returns>The first item.</returns>
            public override object first()
            {
                return _f.first();
            }

            /// <summary>
            /// Return a seq of the items after the first.  Calls <c>seq</c> on its argument.  If there are no more items, returns nil."
            /// </summary>
            /// <returns>A seq of the items after the first, or <c>nil</c> if there are no more items.</returns>
            public override ISeq next()
            {
                ISeq f1 = _f.next();
                ISeq r1 = _rseq;
                if (f1 == null)
                {
                    if (_rseq == null)
                        return null;
                    f1 = _rseq;
                    r1 = null;
                }
                return new Seq(f1, r1);
            }



            #endregion
        }


    }
}


*)