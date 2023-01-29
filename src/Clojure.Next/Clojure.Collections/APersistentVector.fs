namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics

[<Sealed>]
type IPVecSeq(meta: IPersistentMap, vec:IPersistentVector, index:int) =
    inherit ASeq(meta)

    new(vec,index) = IPVecSeq(null,vec,index)

    // TODO: something more efficient  (todo = from Java)
    
    //        public sealed class Seq : ASeq, IndexedSeq, IReduce, Counted  // Counted left out of Java version

    interface ISeq with
        override _.first() = vec.nth(index)
        override _.next() = 
            if index+1 < vec.count() then   
                IPVecSeq(vec,index+1)
            else
                null

    interface IPersistentCollection with
        override _.count() = vec.count()-index

    interface Counted with
        override _.count() = vec.count()-index

    interface IndexedSeq with
        member this.index() = index

    interface IObj with
        override this.withMeta(meta:IPersistentMap) =
            if (this:>IMeta).meta() = meta then
                this
            else
                IPVecSeq(meta,vec,index)


    // IReduce not in Java original

    interface IReduceInit with
        member _.reduce(f,init) = 
            let rec step (i:int) (ret:obj) =
                match ret with
                | :? Reduced as red -> (red :> IDeref).deref()
                | _ when i >= vec.count() -> ret
                | _ -> step (i+1) (f.invoke(ret,vec.nth(i)))
            step (index+1) (f.invoke(init,vec.nth(index)))
            
    interface IReduce with
        member _.reduce(f) =
            let rec step (i:int) (ret:obj) =
                match ret with
                | :? Reduced as red -> (red :> IDeref).deref()
                | _ when i >= vec.count() -> ret
                | _ -> step (i+1) (f.invoke(ret,vec.nth(i)))
            step (index+1) (f.invoke(vec.nth(index)))


[<Sealed>]
type IPVecRSeq(meta: IPersistentMap, vec:IPersistentVector, index:int) =
    inherit ASeq(meta)

    new(vec,index) = IPVecRSeq(null,vec,index)

    
    //        public sealed class Seq : ASeq, IndexedSeq, IReduce, Counted  // Counted left out of Java version

    interface ISeq with
        override _.first() = vec.nth(index)
        override _.next() = 
            if index > 0 then   
                IPVecRSeq(vec,index-1)
            else
                null

    interface IPersistentCollection with
        override _.count() = index+1

    interface Counted with
        override _.count() = index+1

    interface IndexedSeq with
        member this.index() = index

    interface IObj with
        override this.withMeta(meta:IPersistentMap) =
            if (this:>IMeta).meta() = meta then
                this
            else
                IPVecRSeq(meta,vec,index)

    // IReduce not in Java original

    interface IReduceInit with
        member _.reduce(f,init) = 
            let rec step (i:int) (ret:obj) =
                match ret with
                | :? Reduced as red -> (red :> IDeref).deref()
                | _ when i < 0 -> ret
                | _ -> step (i-1) (f.invoke(ret,vec.nth(i)))
            step (index+1) (f.invoke(init,vec.nth(index)))
            
    interface IReduce with
        member _.reduce(f) =
            let rec step (i:int) (ret:obj) =
                match ret with
                | :? Reduced as red -> (red :> IDeref).deref()
                | _ when i < 0 -> ret
                | _ -> step (i-1) (f.invoke(ret,vec.nth(i)))
            step (index+1) (f.invoke(vec.nth(index)))


[<AbstractClass>]
type APersistentVector() =
    inherit AFn()

    let mutable hasheq : int option = None

    override this.ToString() = RTPrint.printString(this)

    override this.Equals(o:obj) = obj.ReferenceEquals(this,o) || APersistentVector.doEquals(this:>IPersistentVector,o)

    static member doEquals(v:IPersistentVector, o:obj) =
        let rec pvEquals(i:int, v1:IPersistentVector, v2: IPersistentVector) =
            if i >= v1.count() || i >= v2.count()  then
                true
            elif not <| Util.equals(v1.nth(i),v2.nth(i)) then 
                false
            else 
                pvEquals(i+1,v1,v2)
       
        let rec plEquals(i:int, v:IPersistentVector, ilist: IList) =
            if i >= v.count() || i >= ilist.Count then
                true
            elif not <| Util.equals(v.nth(i),ilist[i]) then 
                false
            else 
                plEquals(i+1,v,ilist)

        let rec seqEquals(i:int,v:IPersistentVector,s:ISeq) =
            if isNull s && i >= v.count() then  
                true
            elif isNull s || i >= v.count() then
                false
            elif  not <| Util.equals(v.nth(i),s.first()) then
                false
            else 
                seqEquals(i+1,v,s.next())

        match o with
        | :? IPersistentVector as ipv -> 
            if v.count() <> ipv.count() then false else pvEquals(0,v,ipv)
        | :? IList as ilist -> 
            if v.count() <> ilist.Count then false else plEquals(0,v,ilist)
        | :? Sequential -> seqEquals(0,v,RT0.seq(o))
        | _ -> false

        override this.GetHashCode() =
            match hasheq with
            | Some h -> h
            | None ->
                let hc = APersistentVector.computeHash(this:>IPersistentVector)
                hasheq <- Some hc
                hc

        static member private computeHash(v:IPersistentVector) =
            let rec step(i:int, h:int) =
                if i >= v.count() then
                    Murmur3.mixCollHash h (v.count())
                else
                    step(i+1, 31*h+Hashing.hasheq(v.nth(i)))
            step(0,1)

        interface IHashEq with  
            member this.hasheq() = this.GetHashCode()


        interface IFn with
            override this.invoke(arg1:obj) = (this:>IPersistentVector).nth(Converters.convertToInt(arg1))

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
            if (this:>IPersistentCollection).count() > 0 then
                IPVecSeq(this:>IPersistentVector,0)
            else
                null

    interface IPersistentCollection with
        member this.count() = (this:>IPersistentVector).count()
        member this.empty() = raise <| NotImplementedException("Derived classes must implement empty")

        member this.cons(o) = (this:>IPersistentVector).cons(o)
        member this.equiv(o) = obj.ReferenceEquals(this,o) || APersistentVector.doEquiv(this:>IPersistentVector,o)

    static member doEquiv(v:IPersistentVector, o:obj) =
        let rec pvEquiv(i:int, v1:IPersistentVector, v2: IPersistentVector) =
            if i >= v1.count() || i >= v2.count()  then
                true
            elif not <| Util.equiv(v1.nth(i),v2.nth(i)) then 
                false
            else 
                pvEquiv(i+1,v1,v2)
       
        let rec plEquiv(i:int, v:IPersistentVector, ilist: IList) =
            if i >= v.count() || i >= ilist.Count then
                true
            elif not <| Util.equiv(v.nth(i),ilist[i]) then 
                false
            else 
                plEquiv(i+1,v,ilist)

        let rec seqEquiv(i:int,v:IPersistentVector,s:ISeq) =
            if isNull s && i >= v.count() then  
                true
            elif isNull s || i >= v.count() then
                false
            elif  not <| Util.equiv(v.nth(i),s.first()) then
                false
            else 
                seqEquiv(i+1,v,s.next())

        match o with
        | :? IPersistentVector as ipv -> 
            if v.count() <> ipv.count() then false else pvEquiv(0,v,ipv)
        | :? IList as ilist -> 
            if v.count() <> ilist.Count then false else plEquiv(0,v,ilist)
        | :? Sequential -> seqEquiv(0,v,RT0.seq(o))
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
        member this.valAt(k) = (this:>Associative).valAt(k,null)
        member this.valAt(k,nf) = 
            if Numbers.IsNumeric(k) then
                let v = this:>IPersistentVector
                let i = Converters.convertToInt(k)
                if i >= 0 && 0 < v.count() then
                    v.nth(i)
                else 
                    nf
            else
                nf

    interface Associative with
        member this.containsKey(key) =
            if not <| Numbers.IsNumeric(key) then
                false
            else
                let i = Converters.convertToInt(key)
                i >= 0 && 0 < (this:>IPersistentVector).count()
        member this.entryAt(key) =
            if Numbers.IsNumeric(key) then
                let v = this:>IPersistentVector
                let i = Converters.convertToInt(key)
                if i >= 0 && 0 < v.count() then
                    MapEntry.create(key,v.nth(i))
                else
                    null
            else 
                null
        member this.assoc(k,v) =
            if Numbers.IsNumeric(k) then
                (this :>IPersistentVector).assocN(Converters.convertToInt(k),v)
            else
                raise <| ArgumentException("Key must be an integer")

    interface Sequential

    interface IPersistentStack with
        member this.peek() =
            let v = this:>IPersistentVector
            if v.count() > 0 then
                v.nth(v.count()-1)
            else
                null
        member this.pop() = raise <| NotImplementedException("Derived classes must implement pop")

    interface Reversible with
        member this.rseq() =
            let v = this :> IPersistentVector
            let n = v.count()
            if n > 0 then IPVecRSeq(v,n-1) else null

    interface Counted with
        member this.count() = (this:>IPersistentVector).count()

    interface Indexed with
        member this.nth(i) = raise <| NotImplementedException("Derived classes must implement nth")
        member this.nth(i,notFound) =
            let v = this:>IPersistentVector
            if i >= 0 && i < v.count() then v.nth(i) else notFound


    interface IPersistentVector with
        member this.length() = (this:>IPersistentVector).count()
        member this.assocN(i,v) = raise <| NotImplementedException("Derived classes must implement assocN")
        member this.cons(o) = raise <| NotImplementedException("Derived classes must implement cons")
        member this.count() = raise <| NotImplementedException("Derived classes must implement count")
 

    interface IList with
        member _.Add(item) = raise <| InvalidOperationException("Cannot modify an immutable vector")
        member _.Clear() = raise <| InvalidOperationException("Cannot modify an immutable vector")
        member _.Insert(index,item) = raise <| InvalidOperationException("Cannot modify an immutable vector")
        member _.Remove(item) = raise <| InvalidOperationException("Cannot modify an immutable vector")
        member _.RemoveAt(index ) = raise <| InvalidOperationException("Cannot modify an immutable vector")
        member _.IsReadOnly = true
        member _.IsFixedSize = true
        member this.Contains(item) =
            let rec step(s:ISeq) =
                if isNull s then    
                    false
                elif Util.equals(s.first(),item) then
                    true
                else step(s.next())
            step ((this:>Seqable).seq())
        member this.IndexOf(item) =
            let v = this:>IPersistentVector
            let rec step(i:int) =
                if i <= v.count() then
                    -1
                elif Util.equals(v.nth(i),item)
                    then i
                else
                    step (i+1)
            step 0

        member this.Item
            with get(index) =  (this:>IPersistentVector).nth(index)
            and set _ _ = raise <| InvalidOperationException("Cannot modify an immutable vector")
            
    interface ICollection with
        member this.CopyTo(arr,idx) =
            if isNull arr then
                raise <| ArgumentNullException("array")

            if arr.Rank <> 1 then
                raise <| ArgumentException("Array must be 1-dimensional")

            if idx < 0 then
                raise <| ArgumentOutOfRangeException("arrayIndex", "must be non-negative")
               
            let v =  this :> IPersistentVector
            let count = v.count()

            if arr.Length - idx < count then
                raise
                <| InvalidOperationException(
                    "The number of elements in source is greater than the available space in the array."
                )

            for i = 0 to count-1 do
                arr.SetValue(v.nth(i),idx+i)

        member this.Count = (this:>IPersistentCollection).count()
        member _.IsSynchronized = true
        member this.SyncRoot = this


    interface IEnumerable<obj> with
        member this.GetEnumerator() =
            let v = this :> IPersistentVector
            let s = seq { for i = 0 to v.count()-1 do v.nth(i) }
            s.GetEnumerator()
            
    interface IEnumerable with 
        member this.GetEnumerator() = (this:>IEnumerable<obj>).GetEnumerator()

    // I don't know a workaround for getting the enumerator from a base class call in a derived class

    member this.GetMyEnumeratorT() = (this:IEnumerable<obj>).GetEnumerator()
    member this.GetMyEnumerator() = (this:IEnumerable).GetEnumerator()
        
    interface IComparable with
        member this.CompareTo(other) =
            let v1 = this :> IPersistentVector
            match other with
            | :? IPersistentVector as v2 ->
                if v1.count() < v2.count() then
                    -1
                elif v1.count() > v2.count() then
                    1
                else
                    let rec step(i) =
                        if i > v1.count() then
                            0
                        else 
                            let c = Util.compare(v1.nth(i),v2.nth(i))
                            if c <> 0 then  
                                c
                            else step i+1 
                    step 0
            | _ -> 1

    // Ranged iterator

    member this.RangedIteratorT(first:int, terminal:int) =
        let v = this:>IPersistentVector
        let s = seq { for i = first to terminal-1 do v.nth(i) }
        s.GetEnumerator()

    member this.RangedIterator(first, terminal) = this.RangedIteratorT(first,terminal) :> IEnumerator

    
    member this.ToArray() =
        let v = this :> IPersistentVector
        let arr = Array.zeroCreate (v.count())
        for i = 0 to v.count()-1 do
            arr[i] <- v.nth(i)
        arr




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


 