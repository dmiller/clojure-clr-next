namespace Clojure.Collections

open System
open System.Collections
open System.Collections.Generic
open Clojure.Numerics

type BoundsChecker = obj -> bool

type Range
    private
    (
        m: IPersistentMap,
        startV: obj,
        endV: obj,
        step: obj,
        boundsCheck: BoundsChecker,
        ichunk: IChunk,
        ichunkNext: ISeq
    ) =
    inherit ASeq(m)

    // Invariants guarantee this is never an empty or infinite seq
    //   assert(start != end && step != 0)

    [<VolatileField>]
    let mutable chunk = ichunk // lazy

    [<VolatileField>]
    let mutable chunkNext = ichunkNext // lazy

    [<VolatileField>]
    let mutable next: ISeq = null // cached

    static member val CHUNK_SIZE = 32

    static member PositiveStepCheck(endV: obj) = (fun (v: obj) -> Numbers.gte (v, endV))
    static member NegativeStepCheck(endV: obj) = (fun (v: obj) -> Numbers.lte (v, endV))

    private new(startV, endV, step, boundsCheck) = Range(null, startV, endV, step, boundsCheck, null, null)

    private new(startV, endV, step, boundsCheck, chunk, chunkNext) =
        Range(null, startV, endV, step, boundsCheck, chunk, chunkNext)


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

    static member create(startV: obj, endV: obj) = Range.create (startV, endV, 1L)

    static member create(endV: obj) : ISeq =
        if Numbers.isPos (endV) then
            Range(0L, endV, 1L, Range.PositiveStepCheck(endV))
        else
            PersistentList.Empty


    member _.forceChunk() =
        match chunk with
        | null ->
            let rec fillArray (v: obj) (arr: obj array) (idx: int) =
                if boundsCheck (v) then
                    v, idx
                elif idx >= Range.CHUNK_SIZE then
                    v, idx
                else
                    arr[idx] <- v
                    fillArray (Numbers.addP (v, step)) arr (idx + 1)

            let arr: obj array = Array.zeroCreate Range.CHUNK_SIZE
            let lastV, n = fillArray startV arr 0
            chunk <- ArrayChunk(arr, 0, n)

            if not <| boundsCheck (lastV) then
                chunkNext <- Range(lastV, endV, step, boundsCheck)
            else
                ()
        | _ -> ()


    interface ISeq with
        override _.first() = startV

        override this.next() =
            match next with
            | null ->
                this.forceChunk ()

                if chunk.count () > 1 then
                    let smallerChunk = chunk.dropFirst ()

                    next <-
                        Range(
                            (this :> IMeta).meta (),
                            smallerChunk.nth (0),
                            endV,
                            step,
                            boundsCheck,
                            smallerChunk,
                            chunkNext
                        )

                    next
                else
                    (this :> IChunkedSeq).chunkedNext ()
            | _ -> next

    interface IObj with
        override this.withMeta(m) =
            if obj.ReferenceEquals(m, (this :> IMeta).meta ()) then
                this
            else
                Range(m, startV, endV, step, boundsCheck, chunk, chunkNext)

    interface IChunkedSeq with
        member this.chunkedFirst() =
            this.forceChunk ()
            chunk

        member this.chunkedNext() =
            (this :> IChunkedSeq).chunkedMore().seq ()

        member this.chunkedMore() =
            this.forceChunk ()
            if isNull chunkNext then PersistentList.Empty else chunkNext

    member this.reducer (f: IFn) (acc: obj) (v: obj) =
        let rec loop (acc: obj) (v: obj) =
            if boundsCheck (v) then
                acc
            else
                match f.invoke (acc, v) with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | nextAcc -> loop nextAcc (Numbers.addP (v, step))

        loop acc v

    interface IReduce with
        member this.reduce(f) =
            this.reducer f startV (Numbers.addP (startV, step))


    interface IReduceInit with
        member this.reduce(f, start) = this.reducer f start startV

    interface IEnumerable<obj> with

        member this.GetEnumerator() =
            let generator (state: obj) : (obj * obj) option =
                if boundsCheck (state) then
                    None
                else
                    Some(state, (Numbers.addP (state, step)))

            let s = Seq.unfold generator startV
            s.GetEnumerator()

    interface IEnumerable with
        // the C# code has this virtual as 'new'. Why?
        override this.GetEnumerator() =
            (this :> IEnumerable<obj>).GetEnumerator()

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


type LongRange private (m: IPersistentMap, startV: int64, endV: int64, step: int64, count: int) =
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
            if obj.ReferenceEquals(m, (this :> IMeta).meta ()) then
                this
            else
                LongRange(m, startV, endV, step, count)

    interface ISeq with
        override _.first() = startV

        override _.next() =
            if count > 1 then
                LongRange(startV + step, endV, step, count - 1)
            else
                null

    interface IPersistentCollection with
        override _.count() = count

    interface IChunkedSeq with
        member _.chunkedFirst() = LongChunk(startV, step, count)
        member _.chunkedNext() = null
        member _.chunkedMore() = PersistentList.Empty

    interface IDrop with
        member this.drop(n) =
            match n with
            | _ when n <= 0 -> this
            | _ when n < count -> LongRange(startV + (step * int64 (n)), endV, step, count - n)
            | _ -> null


    member this.reducer (f: IFn) (acc: obj) (v: int64) (cnt: int) =
        let rec loop (acc: obj) (v: int64) (cnt: int) =
            if cnt <= 0 then
                acc
            else
                match f.invoke (acc, v) with
                | :? Reduced as red -> (red :> IDeref).deref ()
                | nextAcc -> loop nextAcc (v + step) (cnt - 1)

        loop acc v cnt

    interface IReduce with
        member this.reduce(f) =
            this.reducer f startV (startV + step) (count - 1)

    interface IReduceInit with
        member this.reduce(f, start) = this.reducer f start startV count


    interface IEnumerable<obj> with
        override _.GetEnumerator() =
            let generator (next: int64, remaining: int64) : (obj * (int64 * int64)) option =
                if remaining > 0 then
                    Some(next, (next + step, remaining - 1L))
                else
                    None

            let s = Seq.unfold generator (startV, count)
            s.GetEnumerator()

    interface IEnumerable with
        override this.GetEnumerator() =
            (this :> IEnumerable<obj>).GetEnumerator()
