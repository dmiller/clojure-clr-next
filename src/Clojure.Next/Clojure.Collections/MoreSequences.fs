namespace Clojure.Collections



type Repeat private (m:IPersistentMap, count: int64 option, value:obj) =
    inherit ASeq(m)

    // count = NONE = indefinite repeat

    [<VolatileField>] 
    let mutable next : ISeq = null  // cached

    private new(c,v) = Repeat(null,c,v)
    private new(v) = Repeat(null,None,v)

    static member create(v:obj) : Repeat = Repeat(v)
    static member create(c:int64, v:obj) : ISeq = 
        if c <= 0 then
            PersistentList.Empty
        else
            Repeat(Some c,v)

            
    interface ISeq with
        override _.first() = value
        override this.next() =
            if isNull next then
                match count with
                | Some c when c > 1 -> next <- Repeat(Some (c-1L),value)
                | None -> next <- this
                | _ -> ()
            else ()
            next

    interface IObj with 
        override this.withMeta(m) = 
            if obj.ReferenceEquals(m,(this:>IMeta).meta()) then 
                this
            else
                Repeat(m,count,value)


    member this.reduceCounted (f:IFn) (idx:int64) (cnt:int64) (start:obj) =
        let rec step (acc:obj) (i:int64) =
            match acc with
            | :? Reduced as red -> (red:>IDeref).deref()
            | _ when i >= cnt -> acc
            | _ -> step (f.invoke(acc,value)) (i+1L)
        step start idx

    member this.reduceInfinite (f:IFn) (start:obj) =
        let rec step (acc:obj) =
            match acc with
            | :? Reduced as red -> (red:>IDeref).deref()
            | _ -> step (f.invoke(acc,value))
        step start

    interface IReduce with
        member this.reduce(f) =
            match count with 
            | Some c -> this.reduceCounted f 1L c value
            | None -> this.reduceInfinite f value

    interface IReduceInit with
        member this.reduce(f,start) =
            match count with 
            | Some c -> this.reduceCounted f 0 c start
            | None -> this.reduceInfinite f start

    interface Sequential

    interface IDrop with
        member this.drop(n) =
            match count with
            | Some c ->
                let droppedCount = c - int64(n)
                if droppedCount > 0 then
                    Repeat(Some droppedCount,value)
                else
                    null
            | None -> this