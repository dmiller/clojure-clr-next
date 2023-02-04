namespace Clojure.Collections




type Repeat private (m: IPersistentMap, count: int64 option, value: obj) =
    inherit ASeq(m)

    // count = NONE = indefinite repeat

    [<VolatileField>]
    let mutable next: ISeq = null // cached

    private new(c, v) = Repeat(null, c, v)
    private new(v) = Repeat(null, None, v)

    static member create(v: obj) : Repeat = Repeat(v)

    static member create(c: int64, v: obj) : ISeq =
        if c <= 0 then PersistentList.Empty else Repeat(Some c, v)


    interface ISeq with
        override _.first() = value

        override this.next() =
            if isNull next then
                match count with
                | Some c when c > 1 -> next <- Repeat(Some(c - 1L), value)
                | None -> next <- this
                | _ -> ()
            else
                ()

            next

    interface IObj with
        override this.withMeta(m) =
            if obj.ReferenceEquals(m, (this :> IMeta).meta ()) then
                this
            else
                Repeat(m, count, value)


    member this.reduceCounted (f: IFn) (idx: int64) (cnt: int64) (start: obj) =
        let rec step (acc: obj) (i: int64) =
            if i >= cnt then
                acc
            else
                match f.invoke (acc, value) with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | nextAcc -> step nextAcc (i + 1L)

        step start idx

    member this.reduceInfinite (f: IFn) (start: obj) =
        let rec step (acc: obj) =
            match (f.invoke (acc, value)) with
            | :? Reduced as red -> (red :> IDeref).deref ()
            | nextAcc -> step nextAcc

        step start

    interface IReduce with
        member this.reduce(f) =
            match count with
            | Some c -> this.reduceCounted f 1L c value
            | None -> this.reduceInfinite f value

    interface IReduceInit with
        member this.reduce(f, start) =
            match count with
            | Some c -> this.reduceCounted f 0 c start
            | None -> this.reduceInfinite f start

    interface Sequential

    interface IDrop with
        member this.drop(n) =
            match count with
            | Some c ->
                let droppedCount = c - int64 (n)

                if droppedCount > 0 then
                    Repeat(Some droppedCount, value)
                else
                    null
            | None -> this



type Cycle private (meta:IPersistentMap, all:ISeq, prev:ISeq, c:ISeq, n:ISeq) = 
    inherit ASeq(meta)

    // all - never null
    
    [<VolatileField>]
    let mutable current : ISeq = c   // lazily realized

    [<VolatileField>]
    let mutable next : ISeq = n  // cached
    
    private new(all,prev,current) = Cycle(null,all,prev,current,null)

    static member create(vals:ISeq) : ISeq =
        if isNull vals then
            PersistentList.Empty
        else
            Cycle(vals,null,vals)

    member this.Current() =
        if isNull current then
            let c = prev.next()
            current <- if isNull c then all else c
        else 
            ()

        current

    interface ISeq with
        override this.first() = this.Current().first()
        override this.next() =
            if isNull next then
                next <- Cycle(all,this.Current(),null)
            else
                ()
            next

    interface IObj with
        override this.withMeta(m) = 
            if obj.ReferenceEquals(m,meta) then 
                this
            else
                Cycle(m, all,prev,current,next)

    member this.reducer(advanceFirst:bool, f:IFn, start:obj, startSeq:ISeq) =
        let mutable s = startSeq
        let advance() =
            s <-  match s.next() with
                  | null -> all
                  | x -> x

        let rec step( acc: obj) =
            match f.invoke(s.first()) with
            | :? Reduced as red -> (red:>IDeref).deref()
            | nextAcc -> advance(); step nextAcc

        if advanceFirst then advance() else ()
        step start 

    interface IReduce with
        member this.reduce(f) = 
            let s = this.Current()
            this.reducer(true,f,s.first(),s)

    interface IReduceInit with
        member this.reduce(f,v) = this.reducer(false,f,v,this.Current())


    interface IPending with
        member _.isRealized() = not <| isNull current


type Iterate private (meta:IPersistentMap, fn: IFn, prevSeed:obj, see:obj, next:I)

IPersistentMap meta, IFn f, Object prevSeed, Object seed, ISeq next)
//            :base(meta)



//namespace clojure.lang
//{
//    public class Iterate : ASeq, IReduce, IPending
//    {
//        #region Data

//        static readonly Object UNREALIZED_SEED = new Object();
//        readonly IFn _f;      // never null
//        readonly Object _prevSeed;
//        volatile Object _seed; // lazily realized
//        volatile ISeq _next;  // cached

//        #endregion

//        #region Ctors and factories

//        Iterate(IFn f, Object prevSeed, Object seed)
//        {
//            _f = f;
//            _prevSeed = prevSeed;
//            _seed = seed;
//        }

//        private Iterate(IPersistentMap meta, IFn f, Object prevSeed, Object seed, ISeq next)
//            :base(meta)
//        {
//            _f = f;
//            _prevSeed = prevSeed;
//            _seed = seed;
//            _next = next;
//        }

//        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ClojureJVM name match")]
//        public static ISeq create(IFn f, Object seed)
//        {
//            return new Iterate(f, null, seed);
//        }

//        #endregion

//        #region ISeq

//        public override object first()
//        {
//            if (_seed == UNREALIZED_SEED)
//                _seed = _f.invoke(_prevSeed);

//            return _seed;
//        }

//        public override ISeq next()
//        {
//            if (_next == null)
//            {
//                _next = new Iterate(_f, first(), UNREALIZED_SEED);
//            }
//            return _next;
//        }

//        #endregion

//        #region IObj

//        public override IObj withMeta(IPersistentMap meta)
//        {
//            if (_meta == meta)
//                return this;

//            return new Iterate(meta, _f, _prevSeed, _seed, _next);
//        }

//        #endregion

//        #region IReduce


//        public object reduce(IFn rf)
//        {
//            Object ff = first();
//            Object ret = ff;
//            Object v = _f.invoke(ff);
//            while (true)
//            {
//                ret = rf.invoke(ret, v);
//                if (RT.isReduced(ret))
//                    return ((IDeref)ret).deref();
//                v = _f.invoke(v);
//            }
//        }


//        public object reduce(IFn rf, object start)
//        {
//            Object ret = start;
//            Object v = first();
//            while (true)
//            {
//                ret = rf.invoke(ret, v);
//                if (RT.isReduced(ret))
//                    return ((IDeref)ret).deref();
//                v = _f.invoke(v);
//            }

//        }

//        #endregion

//        #region IPending methods

//        public bool isRealized()
//        {
//            return _seed != UNREALIZED_SEED;
//        }

//        #endregion
//    }
//}
