namespace Clojure.Collections

open System
open System.Collections
open Clojure.Numerics
open System.Runtime.CompilerServices

    // Some of the sequence functions from the original RT that need only PersistentList and Cons


// Because of the need to look before you leap (make sure one element exists)
// this is more complicated than the JVM version:  In JVM-land, you can hasNext before you move.

[<Sealed>]
type private ChunkEnumeratorSeqHelper(_iter : IEnumerator) =
    inherit AFn()

    [<Literal>]
    let chunkSize = 32

    static member chunkEnumeratorSeq(iter : IEnumerator) =
        if not <| iter.MoveNext() then null
        else ChunkEnumeratorSeqHelper.primedChunkEnumeratorSeq(iter)

    static member primedChunkEnumeratorSeq(iter : IEnumerator) =
        LazySeq(ChunkEnumeratorSeqHelper(iter))

    interface IFn with
        member this.invoke() =
            let arr = Array.zeroCreate(chunkSize)
            let mutable more = true;
            let mutable i = 0;
            while more && i < chunkSize do
                arr.[i] <- _iter.Current
                more <- _iter.MoveNext()
                i <- i + 1
            
            ChunkedCons(ArrayChunk(arr,0,i), if more then ChunkEnumeratorSeqHelper.primedChunkEnumeratorSeq(_iter) else null)

and [<AbstractClass;Sealed>]  RTSeq() =

    static do 
        RT0.setSeq(RTSeq.seq) 

    // TODO: Another candidate for protocols

    static member seq (coll:obj) : ISeq =
        match coll with
        | :? ASeq as a -> a
        | :? LazySeq as lseq -> (lseq :> Seqable).seq()
        | _ -> RTSeq.seqFrom(coll)
        
    static member private seqFrom(coll:obj) : ISeq =
        match coll with 
        | null -> null
        | :? Seqable as seq -> seq.seq()
        | _ when typeof<Array>.IsAssignableFrom(coll.GetType()) ->  ArraySeq.createFromObject(coll)
        | :? string as str -> StringSeq.create(str)
        | :? IEnumerable as ie -> ChunkEnumeratorSeqHelper.chunkEnumeratorSeq(ie.GetEnumerator())  // java: Iterable  -- reordered clauses so others take precedence.
        | _ -> failwithf "Don't know how to create ISeq from: %s" (coll.GetType().FullName)


    static member cons (x: obj, coll: obj) : ISeq =
        match coll with
        | null -> upcast PersistentList(x)
        | :? ISeq as s -> upcast Cons(x, s)
        | _ -> upcast Cons(x, RT0.seq (coll))

    static member meta (x:obj) = 
        match x with
        | :? IMeta as m -> m.meta()
        | _ -> null

    static member conj(coll:IPersistentCollection, x:obj) : IPersistentCollection =
        match coll with
        | null -> PersistentList(x)
        | _ -> coll.cons(x)

    static member next(x:obj) =
        let seq =
            match x with
            | :? ISeq as s -> s
            | _ -> RT0.seq(x)
        match seq with
        | null -> null
        | _ -> seq.next()

    static member more(x:obj) =
        let seq =
            match x with
            | :? ISeq as s -> s
            | _ -> RT0.seq(x)
        match seq with
        | null -> null
        | _ -> seq.more()

    static member first(x:obj) =
        let seq =
            match x with
            | :? ISeq as s -> s
            | _ -> RT0.seq(x)

        match seq with
        | null -> null
        | _ -> seq.first()

    static member second(x:obj) = RTSeq.first(RTSeq.next(x))
    static member third(x:obj) = RTSeq.first(RTSeq.next(RTSeq.next(x)))
    static member fourth(x:obj) = RTSeq.first(RTSeq.next(RTSeq.next(RTSeq.next(x))))

    static member peek(x:obj) = 
        match x with
        | null -> null
        | _ -> (x :?> IPersistentStack).peek()

    static member pop(x:obj) = 
        match x with
        | null -> null
        | _ -> (x :?> IPersistentStack).pop()

    static member listStar(arg1, rest) = RTSeq.cons(arg1, rest)
    static member listStar(arg1, arg2, rest) = RTSeq.cons(arg1, RTSeq.cons(arg2, rest))
    static member listStar(arg1, arg2, arg3, rest) = RTSeq.cons(arg1, RTSeq.cons(arg2, RTSeq.cons(arg3, rest)))
    static member listStar(arg1, arg2, arg3, arg4, rest) = RTSeq.cons(arg1, RTSeq.cons(arg2, RTSeq.cons(arg3, RTSeq.cons(arg4, rest))))
    static member listStar(arg1, arg2, arg3, arg4, arg5, rest) = RTSeq.cons(arg1, RTSeq.cons(arg2, RTSeq.cons(arg3, RTSeq.cons(arg4, RTSeq.cons(arg5, rest)))))

    static member list0() = null
    static member list(arg1) = PersistentList(arg1)
    static member list(arg1, arg2) = RTSeq.listStar(arg1,arg2,null)
    static member list(arg1, arg2, arg3) = RTSeq.listStar(arg1,arg2,arg3,null)
    static member list(arg1, arg2, arg3, arg4) = RTSeq.listStar(arg1,arg2,arg3,arg4,null)
    static member list(arg1, arg2, arg3, arg4, arg5) = RTSeq.listStar(arg1,arg2,arg3,arg4,arg5,null)

    static member assoc(coll:obj, key:obj, value: obj) =
        match coll with
        | null -> coll
        | _ -> (coll :?> IPersistentMap).assoc(key,value)

    static member dissoc(coll:obj, key:obj) =
        match coll with
        | null -> coll
        | _ -> (coll :?> IPersistentMap).without(key)

        

and [<Sealed; AllowNullLiteral>] LazySeq private (m1, fn1, s1) =
    inherit Obj(m1)
    let mutable fn: IFn = fn1
    let mutable s: ISeq = s1
    let mutable sv: obj = null

    private new(m1: IPersistentMap, s1: ISeq) = LazySeq(m1, null, s1)
    new(fn: IFn) = LazySeq(null, fn, null)

    override this.GetHashCode() =
        match (this :> ISeq).seq () with
        | null -> 1
        | s -> Hashing.hash s


    override this.Equals(o: obj) =
        match (this :> ISeq).seq (), o with
        | null, :? Sequential
        | null, :? IList -> RT0.seq (o) = null
        | null, _ -> false
        | s, _ -> s.Equals(o)

    interface IObj with
        override this.withMeta(meta: IPersistentMap) =
            if LanguagePrimitives.PhysicalEquality ((this :> IMeta).meta ()) meta then
                this :> IObj
            else
                LazySeq(meta, (this :> ISeq).seq ()) :> IObj

    member _.sval() : obj =
        if not (isNull fn) then
            sv <- fn.invoke ()
            fn <- null

        match sv with
        | null -> upcast s
        | _ -> sv


    interface Seqable with

        [<MethodImpl(MethodImplOptions.Synchronized)>]
        override this.seq() =

            this.sval () |> ignore

            if not (isNull sv) then

                let rec getNext (x: obj) =
                    match x with
                    | :? LazySeq as ls -> getNext (ls.sval ())
                    | _ -> x

                let ls = sv
                sv <- null
                s <- RT0.seq (getNext ls)

            s


    interface IPersistentCollection with
        member _.count() =
            let rec countAux (s: ISeq) (acc: int) : int =
                match s with
                | null -> acc
                | _ -> countAux (s.next ()) (acc + 1)

            countAux s 0

        member this.cons(o) = upcast (this :> ISeq).cons (o)
        member _.empty() = upcast PersistentList.Empty

        member this.equiv(o) =
            match (this :> ISeq).seq () with
            | null ->
                match o with
                | :? IList
                | :? Sequential -> RT0.seq (o) = null
                | _ -> false
            | s -> s.equiv (o)

    interface ISeq with
        member this.first() =
            (this :> ISeq).seq () |> ignore
            if isNull s then null else s.first ()

        member this.next() =
            (this :> ISeq).seq () |> ignore
            if isNull s then null else s.next ()

        member this.more() =
            (this :> ISeq).seq () |> ignore

            if isNull s then upcast PersistentList.Empty else s.more ()

        member this.cons(o: obj) : ISeq = RTSeq.cons (o, (this :> ISeq).seq ())

    interface IPending with
        member _.isRealized() = isNull fn

    interface IHashEq with
        member this.hasheq() = Hashing.hashOrdered (this)

    interface IEnumerable with
        member this.GetEnumerator() = upcast new SeqEnumerator(this)

    interface IList with
        member _.Add(_) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Clear() =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Insert(i, v) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.Remove(v) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.RemoveAt(i) =
            raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member _.IsFixedSize = true
        member _.IsReadOnly = true

        member this.Item
            with get index =
                if index < 0 then
                    raise <| ArgumentOutOfRangeException("index", "Index must be non-negative")

                let rec loop i (s: ISeq) =
                    if i = index then
                        s.first ()
                    elif isNull s then
                        raise <| ArgumentOutOfRangeException("index", "Index past end of list")
                    else
                        loop (i + 1) (s.next ())

                loop 0 this // TODO: See IndexOf. Should this be called on x or x.seq() ??  Check original Java code.
            and set _ _ = raise <| InvalidOperationException("Cannot modify an immutable sequence")

        member this.IndexOf(v) =
            let rec loop i (s: ISeq) =
                if isNull s then -1
                else if Util.equiv (s.first (), v) then i
                else loop (i + 1) (s.next ())

            loop 0 ((this :> ISeq).seq ())

        member this.Contains(v) =
            let rec loop (s: ISeq) =
                if isNull s then false
                else if Util.equiv (s.first (), v) then true
                else loop (s.next ())

            loop ((this :> ISeq).seq ())

    interface ICollection with
        member this.Count = (this :> IPersistentCollection).count ()
        member _.IsSynchronized = true
        member this.SyncRoot = upcast this

        member this.CopyTo(arr: Array, idx) =
            if isNull arr then
                raise <| ArgumentNullException("array")

            if idx < 0 then
                raise <| ArgumentOutOfRangeException("arrayIndex", "must be non-negative")

            if arr.Rank <> 1 then
                raise <| ArgumentException("Array must be 1-dimensional")

            if idx >= arr.Length then
                raise <| ArgumentException("index", "must be less than the length")

            if (this :> IPersistentCollection).count () > arr.Length - idx then
                raise
                <| InvalidOperationException("Not enough available space from index to end of the array.")

            let rec loop (i: int) (s: ISeq) =
                if not (isNull s) then
                    arr.SetValue(s.first (), i)
                    loop (i + 1) (s.next ())

            loop idx (this :> ISeq)

