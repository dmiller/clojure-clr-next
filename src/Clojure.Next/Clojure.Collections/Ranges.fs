namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics

/// Signature for a function that checks if a value is within bounds.
type BoundsChecker = obj -> bool

/// An iterator for a range of numeric values.
type Range
    private
    (
        m: IPersistentMap,
        _start: obj,
        _end: obj,
        _step: obj,
        _boundsCheck: BoundsChecker,
        chunk: IChunk,
        chunkNext: ISeq
    ) =
    inherit ASeq(m)

    // Invariants guarantee this is never an empty or infinite seq
    //   assert(start != end && step != 0)

    [<VolatileField>]
    let mutable _chunk = chunk // lazy

    [<VolatileField>]
    let mutable _chinkNext = chunkNext // lazy

    [<VolatileField>]
    let mutable next: ISeq = null // cached

    [<Literal>]
    let _chunkSize = 32

    /// Check for stopping when the step is positive.
    static member PositiveStepCheck(endV: obj) = (fun (v: obj) -> Numbers.gte (v, endV))

    /// Check for stopping when the step is negative
    static member NegativeStepCheck(endV: obj) = (fun (v: obj) -> Numbers.lte (v, endV))

    /// Create Range with null metadata and no chunks
    private new(startV, endV, step, boundsCheck) = Range(null, startV, endV, step, boundsCheck, null, null)

    /// Create Range with null metatdata
    private new(startV, endV, step, boundsCheck, chunk, chunkNext) =
        Range(null, startV, endV, step, boundsCheck, chunk, chunkNext)

    /// Create a Range given the start, end, and step values.
    static member create(startV: obj, endV: obj, step: obj) : ISeq =
        if
            Numbers.isPos (step) && Numbers.gt (startV, endV)
            || Numbers.isNeg (step) && Numbers.gt (endV, startV)
            || Numbers.equiv (startV, endV)
        then
            PersistentList.Empty
        elif Numbers.isZero (step) then
            Repeat.create (startV)
        else
            Range(
                startV,
                endV,
                step,
                if Numbers.isPos (step) then
                    Range.PositiveStepCheck(endV)
                else
                    Range.NegativeStepCheck(endV)
            )

    /// Create a Range given the start and end values, with a step of 1.
    static member create(startV: obj, endV: obj) = Range.create (startV, endV, 1L)

    /// Create a Range given the end value, with a start of 0 and a step of 1.
    static member create(endV: obj) : ISeq =
        if Numbers.isPos (endV) then
            Range(0L, endV, 1L, Range.PositiveStepCheck(endV))
        else
            PersistentList.Empty


    member _.forceChunk() =
        match _chunk with
        | null ->
            let rec fillArray (v: obj) (arr: obj array) (idx: int) =
                if _boundsCheck (v) then
                    v, idx
                elif idx >= _chunkSize then
                    v, idx
                else
                    arr[idx] <- v
                    fillArray (Numbers.addP (v, _step)) arr (idx + 1)

            let arr: obj array = Array.zeroCreate _chunkSize
            let lastV, n = fillArray _start arr 0
            _chunk <- ArrayChunk(arr, 0, n)

            if not <| _boundsCheck (lastV) then
                _chinkNext <- Range(lastV, _end, _step, _boundsCheck)
            else
                ()
        | _ -> ()


    interface ISeq with
        override _.first() = _start

        override this.next() =
            match next with
            | null ->
                this.forceChunk ()

                if _chunk.count () > 1 then
                    let smallerChunk = _chunk.dropFirst ()

                    next <-
                        Range(
                            (this :> IMeta).meta (),
                            smallerChunk.nth (0),
                            _end,
                            _step,
                            _boundsCheck,
                            smallerChunk,
                            _chinkNext
                        )

                    next
                else
                    (this :> IChunkedSeq).chunkedNext ()
            | _ -> next

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m ((this :> IMeta).meta ()) then
                this
            else
                Range(m, _start, _end, _step, _boundsCheck, _chunk, _chinkNext)

    interface IChunkedSeq with
        member this.chunkedFirst() =
            this.forceChunk ()
            _chunk

        member this.chunkedNext() =
            (this :> IChunkedSeq).chunkedMore().seq ()

        member this.chunkedMore() =
            this.forceChunk ()

            if isNull _chinkNext then
                PersistentList.Empty
            else
                _chinkNext

    member this.reducer (f: IFn) (acc: obj) (v: obj) =
        let rec loop (acc: obj) (v: obj) =
            if _boundsCheck (v) then
                acc
            else
                match f.invoke (acc, v) with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | nextAcc -> loop nextAcc (Numbers.addP (v, _step))

        loop acc v

    interface IReduce with
        member this.reduce(f) =
            this.reducer f _start (Numbers.addP (_start, _step))


    interface IReduceInit with
        member this.reduce(f, start) = this.reducer f start _start

    interface IEnumerable<obj> with

        member this.GetEnumerator() =
            let generator (state: obj) : (obj * obj) option =
                if _boundsCheck (state) then
                    None
                else
                    Some(state, (Numbers.addP (state, _step)))

            let s = Seq.unfold generator _start
            s.GetEnumerator()

    interface IEnumerable with
        // the C# code has this virtual as 'new'. Why?
        override this.GetEnumerator() =
            (this :> IEnumerable<obj>).GetEnumerator()

/// Specialized chunk for use with LongRange
type internal LongChunk(start: int64, step: int64, count: int) =

    member _.first() = start

    interface Counted with
        member _.count() = count

    interface Indexed with
        // only used internally, no need to guard.
        member _.nth(i) = start + (int64 (i) * step) :> obj

        member _.nth(i, nf) =
            if i >= 0 && i < count then
                start + (int64 (i) * step) :> obj
            else
                nf

    interface IChunk with
        member _.dropFirst() =
            if count <= 1 then
                raise <| InvalidOperationException("dropFirst of empty chunk")
            else
                LongChunk(start + step, step, count - 1)

        member _.reduce(f, init) =
            let rec loop (acc: obj) (v: int64) (i: int64) =
                match acc with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | _ when i >= count -> acc
                | _ -> loop (f.invoke (acc, v)) (v + step) (i + 1L)

            loop init start 0

/// A range of long values.
type LongRange private (m: IPersistentMap, _start: int64, _end: int64, _step: int64, _count: int) =
    inherit ASeq(m)

    // Invariants guarantee this is never an empty or infinite seq
    //   assert(start != end && step != 0)

    new(start, iend, step, count) = LongRange(null, start, iend, step, count)

    static member rangeCount(startV: int64, endV: int64, step: int64) =
        // (1) count = ceiling ( (end - start) / step )
        // (2) ceiling(a/b) = (a+b+o)/b where o=-1 for positive stepping and +1 for negative stepping
        // thus: count = end - start + step + o / step
        // This is the original coding.  Just a cheap way to get checked arithmetic
        // (given that these all call the int64*int64->int64 versions)
        Numbers.add (Numbers.add (Numbers.minus (endV, startV), step), (if step > 0 then -1L else 1L))
        / step

    static member toIntExact(value: int64) : int32 = Checked.int32 (value)

    static member create(endV: int64) : ISeq =
        if endV <= 0 then
            PersistentList.Empty
        else
            try
                LongRange(0L, endV, 1L, LongRange.toIntExact (LongRange.rangeCount (0L, endV, 1L)))
            with :? OverflowException ->
                Range.create (endV)


    static member create(startV: int64, endV: int64) : ISeq =
        if startV >= endV then
            PersistentList.Empty
        else
            try
                LongRange(startV, endV, 1L, LongRange.toIntExact (LongRange.rangeCount (startV, endV, 1L)))
            with :? OverflowException ->
                Range.create (startV, endV)

    static member create(startV: int64, endV: int64, step: int64) : ISeq =
        if step > 0 then
            if endV <= startV then
                PersistentList.Empty
            else
                try
                    LongRange(startV, endV, step, LongRange.toIntExact (LongRange.rangeCount (startV, endV, step)))
                with :? OverflowException ->
                    Range.create (startV, endV, step)
        elif step < 0 then
            if endV >= startV then
                PersistentList.Empty
            else
                try
                    LongRange(startV, endV, step, LongRange.toIntExact (LongRange.rangeCount (startV, endV, step)))
                with :? OverflowException ->
                    Range.create (startV, endV, step)
        elif endV = startV then
            PersistentList.Empty
        else
            Repeat.create (startV)

    interface IObj with
        override this.withMeta(m) =
            if LanguagePrimitives.PhysicalEquality m ((this :> IMeta).meta ()) then
                this
            else
                LongRange(m, _start, _end, _step, _count)

    interface ISeq with
        override _.first() = _start

        override _.next() =
            if _count > 1 then
                LongRange(_start + _step, _end, _step, _count - 1)
            else
                null

    interface IPersistentCollection with
        override _.count() = _count

    interface IChunkedSeq with
        member _.chunkedFirst() = LongChunk(_start, _step, _count)
        member _.chunkedNext() = null
        member _.chunkedMore() = PersistentList.Empty

    interface IDrop with
        member this.drop(n) =
            match n with
            | _ when n <= 0 -> this
            | _ when n < _count -> LongRange(_start + (_step * int64 (n)), _end, _step, _count - n)
            | _ -> null


    member this.reducer (f: IFn) (acc: obj) (v: int64) (cnt: int) =
        let rec loop (acc: obj) (v: int64) (cnt: int) =
            if cnt <= 0 then
                acc
            else
                match f.invoke (acc, v) with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | nextAcc -> loop nextAcc (v + _step) (cnt - 1)

        loop acc v cnt

    interface IReduce with
        member this.reduce(f) =
            this.reducer f _start (_start + _step) (_count - 1)

    interface IReduceInit with
        member this.reduce(f, start) = this.reducer f start _start _count


    interface IEnumerable<obj> with
        override _.GetEnumerator() =
            let generator (next: int64, remaining: int64) : (obj * (int64 * int64)) option =
                if remaining > 0 then
                    Some(next, (next + _step, remaining - 1L))
                else
                    None

            let s = Seq.unfold generator (_start, _count)
            s.GetEnumerator()

    interface IEnumerable with
        override this.GetEnumerator() =
            (this :> IEnumerable<obj>).GetEnumerator()
