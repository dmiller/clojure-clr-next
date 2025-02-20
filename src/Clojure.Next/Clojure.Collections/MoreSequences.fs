namespace Clojure.Collections

/// Repeats a value indefinitely or a fixed number of times.
type Repeat private (meta: IPersistentMap, _count: int64 option, _value: obj) =
    inherit ASeq(meta)

    // _count = NONE = indefinite repeat

    [<VolatileField>]
    let mutable _next: ISeq = null // cached

    /// Create a Repeat with null mutadata
    private new(c, v) = Repeat(null, c, v)

    /// Create a Repeat with null mutadata, infinite repeat
    private new(v) = Repeat(null, None, v)

    /// Create a Repeat on a value with infinite rpeats
    static member create(v: obj) : Repeat = Repeat(v)

    /// Create a Repeat on a value with a fixed number of repeats
    static member create(c: int64, v: obj) : ISeq =
        if c <= 0 then PersistentList.Empty else Repeat(Some c, v)

    interface ISeq with
        override _.first() = _value

        override this.next() =
            if isNull _next then
                match _count with
                | Some c when c > 1 -> _next <- Repeat(Some(c - 1L), _value)
                | None -> _next <- this
                | _ -> ()

            _next

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m ((this :> IMeta).meta ()) then
                this
            else
                Repeat(m, _count, _value)


    member this.reduceCounted (f: IFn) (idx: int64) (cnt: int64) (start: obj) =
        let rec loop (acc: obj) (i: int64) =
            if i >= cnt then
                acc
            else
                match f.invoke (acc, _value) with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | nextAcc -> loop nextAcc (i + 1L)

        loop start idx

    member this.reduceInfinite (f: IFn) (start: obj) =
        let rec loop (acc: obj) =
            match (f.invoke (acc, _value)) with
            | :? Reduced as red -> (red :> IDeref).deref ()
            | nextAcc -> loop nextAcc

        loop start

    interface IReduce with
        member this.reduce(f) =
            match _count with
            | Some c -> this.reduceCounted f 1L c _value
            | None -> this.reduceInfinite f _value

    interface IReduceInit with
        member this.reduce(f, start) =
            match _count with
            | Some c -> this.reduceCounted f 0 c start
            | None -> this.reduceInfinite f start

    interface Sequential

    interface IDrop with
        member this.drop(n) =
            match _count with
            | Some c ->
                let droppedCount = c - int64 (n)

                if droppedCount > 0 then
                    Repeat(Some droppedCount, _value)
                else
                    null
            | None -> this


/// Repeats a sequence indefinitely or a fixed number of times.
type Cycle private (meta: IPersistentMap, _all: ISeq, _prev: ISeq, current: ISeq, next: ISeq) =
    inherit ASeq(meta)

    // all - never null

    [<VolatileField>]
    let mutable _current: ISeq = current // lazily realized

    [<VolatileField>]
    let mutable _next: ISeq = next // cached

    /// Create a Cycle with null metadata
    private new(all, prev, current) = Cycle(null, all, prev, current, null)

    /// Create a Cycle from an ISeq
    static member create(vals: ISeq) : ISeq =
        if isNull vals then
            PersistentList.Empty
        else
            Cycle(vals, null, vals)

    member this.Current() =
        if isNull _current then
            let c = _prev.next ()
            _current <- if isNull c then _all else c

        _current

    interface ISeq with
        override this.first() = this.Current().first ()

        override this.next() =
            if isNull _next then
                _next <- Cycle(_all, this.Current(), null)

            _next

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m meta then
                this
            else
                Cycle(m, _all, _prev, _current, _next)


    member this.advance(s: ISeq) =
        match s.next () with
        | null -> _all
        | x -> x

    member this.reducer(f: IFn, startVal: obj, startSeq: ISeq) =
        let rec loop (acc: obj) (s: ISeq) =
            match f.invoke (acc, s.first ()) with
            | :? Reduced as red -> (red :> IDeref).deref ()
            | nextAcc -> loop nextAcc (this.advance s)

        loop startVal startSeq

    interface IReduce with
        member this.reduce(f) =
            let s = this.Current()
            this.reducer (f, s.first (), this.advance (s))

    interface IReduceInit with
        member this.reduce(f, v) = this.reducer (f, v, this.Current())


    interface IPending with
        member _.isRealized() = not <| isNull _current

/// Creates an iterator of repeated function calls, starting from a seed value.
type Iterate private (meta: IPersistentMap, _fn: IFn, prevSeed: obj, seed: obj, next: ISeq) =
    inherit ASeq(meta)

    // fn -- never null

    [<VolatileField>]
    let mutable _seed: obj = seed // lazily realized

    [<VolatileField>]
    let mutable _next: ISeq = next // cached

    static member val private _unrealizedSeed: obj = System.Object()

    /// Create an Iterate with null metadata
    new(fn, prevSeed, seed) = Iterate(null, fn, prevSeed, seed, null)

    /// Create an Iterate from an IFn and a seed
    static member create(f: IFn, seed: obj) : ISeq = Iterate(f, null, seed)

    interface ISeq with
        member _.first() =
            if LanguagePrimitives.PhysicalEquality _seed Iterate._unrealizedSeed then
                _seed <- _fn.invoke (prevSeed)

            _seed

        member this.next() =
            if isNull _next then
                _next <- Iterate(_fn, (this :> ISeq).first (), Iterate._unrealizedSeed)

            _next

    interface IObj with
        member this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m meta then
                this
            else
                Iterate(m, _fn, prevSeed, _seed, _next)

    member this.reducer(rf: IFn, start: obj, v: obj) =
        let rec loop (acc: obj) (v: obj) =
            match rf.invoke (acc, v) with
            | :? Reduced as red -> (red :> IDeref).deref ()
            | nextAcc -> loop nextAcc (_fn.invoke (v))

        loop start v

    interface IReduce with
        member this.reduce(rf) =
            let ff = (this :> ISeq).first ()
            this.reducer (rf, ff, _fn.invoke (ff))

    interface IReduceInit with
        member this.reduce(rf, start) =
            this.reducer (rf, start, (this :> ISeq).first ())

    interface IPending with
        member _.isRealized() = _seed <> Iterate._unrealizedSeed
