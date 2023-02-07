module PersistentVectorTests

open Expecto
open TestHelpers
open Clojure.Collections
open Clojure.Numerics
open System

let pvFromRange (r: LongRange) =
    PersistentVector.create ((r :> Seqable).seq ())

let pvCount (v: PersistentVector) = (v :> Counted).count ()
let pvSeq (v: PersistentVector) = (v :> Seqable).seq ()

let pvCreateCount (n: int64) =
    pvFromRange (LongRange.create (n) :?> LongRange)

let pvCreateOffset (startV: int64, endV: int64) =
    PersistentVector.create ((LongRange.create (startV, endV)))

let rangeSeq (r: LongRange) = (r :> Seqable).seq ()


[<Tests>]
let rangeTests =
    testList
        "PersistentVectorTests"
        [

          testCase "Basic creation from an ISeq, checks also count and seq"
          <| fun _ ->

              let testForSize (n: int) =
                  let r = LongRange.create (n) :?> LongRange
                  let v = pvFromRange (r)
                  Expect.equal (pvCount (v)) n "Create from n has count n"
                  Expect.isTrue (compareISeqs (pvSeq (v)) (rangeSeq (r))) "matches creating range"

              // create from null
              let v0 = PersistentVector.create (null)
              Expect.equal (pvCount (v0)) 0 "Create from null has no items"
              Expect.isNull (pvSeq (v0)) "empty vector should have null seq"

              testForSize (1)
              testForSize (2)
              testForSize (32)
              testForSize (33)
              testForSize (64)
              testForSize (65)
              testForSize (100)
              testForSize (1000)

          testCase "PersistentVector.nth"
          <| fun _ ->

              let testForSize (n: int) =
                  let r = LongRange.create (n) :?> LongRange
                  let v = pvFromRange (r)

                  for i = 0 to n - 1 do
                      Expect.equal ((v :> Indexed).nth (i)) (int64 (i)) "i-th entry should be i"

                  Expect.equal ((v :> Indexed).nth (-1, -100)) -100 "index < 0 gives default"
                  Expect.equal ((v :> Indexed).nth (n, -100)) -100 "index >= size gives default"

                  Expect.throwsT<ArgumentOutOfRangeException>
                      (fun () -> (v :> Indexed).nth (n) |> ignore)
                      "index out of range throws"

                  Expect.throwsT<ArgumentOutOfRangeException>
                      (fun () -> (v :> Indexed).nth (-1) |> ignore)
                      "index out of range throws"

              testForSize (1)
              testForSize (2)
              testForSize (32)
              testForSize (33)
              testForSize (64)
              testForSize (65)
              testForSize (100)
              testForSize (1000)


          testCase "PersistentVector Stack ops"
          <| fun _ ->

              let v0 = PersistentVector.create (null)

              Expect.throwsT<InvalidOperationException>
                  (fun () -> (v0 :> IPersistentStack).pop () |> ignore)
                  "pop on empty throws"

              let v1 = pvCreateCount (1)
              Expect.equal (pvCount (v1)) 1 "1 for 1"
              Expect.equal (pvCount ((v1 :> IPersistentStack).pop () :?> PersistentVector)) 0 "pop from 1 leaves 0"

              Expect.throwsT<InvalidOperationException>
                  (fun () -> (v1 :> IPersistentStack).pop().pop () |> ignore)
                  "pop on empty throws"

              let testForSize (v: PersistentVector, n: int) =
                  Expect.equal (pvCount (v)) n "should have count n"

                  for i = 0 to n - 1 do
                      Expect.equal ((v :> Indexed).nth (i)) (int64 (i)) "i-th entry should be i"

              let rec stepPush (v: PersistentVector) (i: int64) (cnt: int) =
                  if i < cnt then
                      let newV = (v :> IPersistentVector).cons (i) :?> PersistentVector
                      Expect.equal (pvCount (newV)) (int (i + 1L)) "should have count n"
                      Expect.equal ((newV :> IPersistentStack).peek ()) i "Top should be new element"

                      for k = 0 to int (i - 1L) do
                          Expect.equal ((v :> Indexed).nth (k)) (int64 (k)) "k-th entry should be k"

                      stepPush newV (i + 1L) cnt
                  else
                      v

              let rec stepPop (v: PersistentVector) (i: int64) =
                  if i >= 0 then
                      let x = (v :> IPersistentStack).peek ()
                      Expect.equal x i "Top should be i"
                      let newV = (v :> IPersistentStack).pop () :?> PersistentVector
                      Expect.equal (pvCount (newV)) (int (i)) "should be smaller"
                      stepPop newV (i - 1L)
                  else
                      v

              let firstV = PersistentVector.create (null)
              let bigV = stepPush firstV 0 1000
              let finalV = stepPop bigV 999
              Expect.equal (pvCount (finalV)) 0 "should end up empty"

          testCase "PersistentVector is IFn"
          <| fun _ ->
              let r = LongRange.create (1000) :?> LongRange
              let v = pvFromRange (r)
              let vf = v :> IFn

              for i = 0 to 999 do
                  Expect.equal (vf.invoke (i)) (int64 i) "invoke indexes"

              Expect.throwsT<ArgumentOutOfRangeException>
                  (fun () -> vf.invoke (-1) |> ignore)
                  "index out of range throws"

              Expect.throwsT<ArgumentOutOfRangeException>
                  (fun () -> vf.invoke (9999) |> ignore)
                  "index out of range throws"


          ftestCase "Reduce tests"
          <| fun _ ->
              let adder =
                  { new AFn() with
                      member this.ToString() = ""
                    interface IFn with
                        member this.invoke() = -100
                        member this.invoke(x, y) = Numbers.addP (x, y)
                         }

              let adderStopsShort n =
                  { new AFn() with
                      member this.ToString() = ""
                    interface IFn with
                        member this.invoke(x, y) =
                            if Numbers.gte (y, n :> obj) then
                                Reduced(x)
                            else
                                Numbers.add (x, y) }


              let r100 = LongRange.create (100) :?> LongRange
              let r1000 = LongRange.create (1000) :?> LongRange
              let rr100 = r100 :> IReduce
              let rr1000 = r1000 :> IReduce
              let v100 = pvFromRange (r100)
              let v1000 = pvFromRange (r1000)
              let rv100 = v100 :> IReduce
              let rv1000 = v1000 :> IReduce

              let x =  (rv100.reduce (adder))

              Expect.equal (rv100.reduce (adder)) (rr100.reduce (adder)) "Add them all"
              Expect.equal (rv100.reduce (adder, 200)) (rr100.reduce (adder, 200)) "Add them all"

              Expect.equal (rv1000.reduce (adder)) (rr1000.reduce (adder)) "Add them all"
              Expect.equal (rv1000.reduce (adder, 200)) (rr1000.reduce (adder, 200)) "Add them all"

              Expect.equal
                  (rv1000.reduce ((adderStopsShort 100)))
                  (rr1000.reduce ((adderStopsShort 100)))
                  "Add first 100"

              Expect.equal ((PersistentVector.create(null):>IReduce).reduce (adder)) -100 "no items, no start"
              Expect.equal ((PersistentVector.create(null):>IReduce).reduce((adder),-999)) -999 "no items, no start"


          ]
