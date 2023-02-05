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

        current

    interface ISeq with
        override this.first() = this.Current().first()
        override this.next() =
            if isNull next then
                next <- Cycle(all,this.Current(),null)

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
            match f.invoke(acc,s.first()) with
            | :? Reduced as red -> (red:>IDeref).deref()
            | nextAcc -> advance(); step nextAcc

        if advanceFirst then advance()
        step start 

    interface IReduce with
        member this.reduce(f) = 
            let s = this.Current()
            this.reducer(true,f,s.first(),s)

    interface IReduceInit with
        member this.reduce(f,v) = this.reducer(false,f,v,this.Current())


    interface IPending with
        member _.isRealized() = not <| isNull current


type Iterate private (meta:IPersistentMap, fn: IFn, prevSeed:obj, s:obj, n:ISeq) =
    inherit ASeq(meta)

    // fn -- never null

    [<VolatileField>]
    let mutable seed : obj = s  // lazily realized

    [<VolatileField>]
    let mutable next : ISeq = n  // cached

    static member val private UNREALIZED_SEED : obj = System.Object()

    new(fn,prevSeed,seed) = Iterate(null,fn,prevSeed,seed,null)

    static member create(f:IFn, seed:obj) :ISeq = Iterate(f,null,seed)

    interface ISeq with
        member _.first() =
            if obj.ReferenceEquals(seed,Iterate.UNREALIZED_SEED) then
                seed <- fn.invoke(prevSeed)

            seed

        member this.next() =
            if isNull next then
                next <- Iterate(fn,(this:>ISeq).first(),Iterate.UNREALIZED_SEED)

            next

    interface IObj with
        member this.withMeta(m) =
            if obj.ReferenceEquals(m,meta) then
                this
            else
                Iterate(m,fn,prevSeed,seed,next)

    member this.reducer(rf:IFn, start:obj, v:obj) =
        let rec step (acc:obj) (v:obj) =
            match rf.invoke(acc,v) with
            | :? Reduced as red -> (red:>IDeref).deref()
            | nextAcc -> step nextAcc (fn.invoke(v))

        step start v

    interface IReduce with
        member this.reduce(rf) = 
            let ff = (this:>ISeq).first()
            this.reducer(rf,ff,fn.invoke(ff))

    interface IReduceInit with
        member this.reduce(rf, start) = this.reducer(rf,start,(this:>ISeq).first())

    interface IPending with
        member _.isRealized() = seed <> Iterate.UNREALIZED_SEED
