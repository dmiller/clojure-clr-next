module PersistentVectorTests

open Expecto
open TestHelpers
open Clojure.Collections
open Clojure.Numerics
open System
open System.Collections

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
let persistentVectorTests =
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
              let v0 = PersistentVector.create (null:>ISeq)
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

              let v0 = PersistentVector.create (null:>ISeq)

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

              let firstV = PersistentVector.create (null:>ISeq)
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


          testCase "Reduce tests"
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

              Expect.equal ((PersistentVector.create(null:ISeq):>IReduce).reduce (adder)) -100 "no items, no start"
              Expect.equal ((PersistentVector.create(null:ISeq):>IReduce).reduce((adder),-999)) -999 "no items, no start"



          testCase "create tests"
          <| fun _ ->

              let r5 = LongRange.create (5) :?> LongRange
              let v5 = PersistentVector.create(0L,1L,2L,3L,4L)

              Expect.equal (pvCount (v5)) 5 "Create from multiple args has count n"
              Expect.isTrue (compareISeqs (pvSeq (v5)) (rangeSeq (r5))) "create from multiple args has correct values"

              let e100 = (seq { for i in 0 .. 99 -> int64(i)}) :> IEnumerable
              let r100 = LongRange.create (100) :?> LongRange
              let v100 = PersistentVector.create1(e100)
              Expect.equal (pvCount(v100)) 100 "create from IEnumerable has correct count"
              Expect.isTrue (compareISeqs (pvSeq (v100)) (rangeSeq (r100))) "create from IEnumerable has correct values"

              let vr100 = PersistentVector.create(r100 :> IReduceInit)
              Expect.equal (pvCount(vr100)) 100 "create from IEnumerable has correct count"
              Expect.isTrue (compareISeqs (pvSeq (vr100)) (rangeSeq (r100))) "create from IEnumerable has correct values"
        
        
          testCase "assocN tests"
          <| fun _ ->
                
              let r0to99 = LongRange.create (100) :?> LongRange
              let r0to100 = LongRange.create (101) :?> LongRange
              let r1to100 = LongRange.create(1,101) :?> LongRange
              let v100 = pvFromRange(r0to99)

              let mutable mv100 = v100

              for i in 0 .. 99 do
                  mv100 <- (mv100 :> IPersistentVector).assocN(i,int64(i+1)) :?> PersistentVector

              Expect.equal (pvCount(mv100)) 100 "assocN on existing has correct count"
              Expect.isTrue (compareISeqs (pvSeq (mv100)) (rangeSeq (r1to100))) "after assocN on existing has correct values"
 

              let v101 = (v100 :> IPersistentVector).assocN(100,100L) :?> PersistentVector
              Expect.equal (pvCount(v101)) 101 "assocN to push has correct count"
              Expect.isTrue (compareISeqs (pvSeq (v101)) (rangeSeq (r0to100))) "after assocN to push has correct values"
              
              Expect.throwsT<ArgumentOutOfRangeException> (fun () ->  (v100 :> IPersistentVector).assocN(1000,100L) |> ignore ) "assocN with index too large throws"
    
          testCase "cons tests"
          <| fun _ ->
                
              let r100 = LongRange.create (100) :?> LongRange

              let mutable mv100 = PersistentVector.EMPTY

              for i in 0 .. 99 do
                  mv100 <- (mv100 :> IPersistentVector).cons(int64(i)) :?> PersistentVector

              Expect.equal (pvCount(mv100)) 100 "cons'd PV has correct count"
              Expect.isTrue (compareISeqs (pvSeq (mv100)) (rangeSeq (r100))) "cons'd PV has correct values"
  
          testCase "pop tests"
          <| fun _ ->
                
              let r500 = LongRange.create (500) :?> LongRange
              let r1000 = LongRange.create (1000) :?> LongRange

              let mutable mv1000 = pvFromRange(r1000) 
              for i in 0 .. 499 do
                mv1000 <- (mv1000 :>IPersistentStack).pop() :?> PersistentVector

              Expect.equal (pvCount(mv1000)) 500 "pop'd PV has correct count"     
              Expect.isTrue (compareISeqs (pvSeq (mv1000)) (rangeSeq (r500))) "pop'd PV has correct values"
 
              for i in 0 .. 499 do
                mv1000 <- (mv1000 :>IPersistentStack).pop() :?> PersistentVector

              Expect.equal (pvCount(mv1000)) 0 "fully pop'd PV has correct count"  

              Expect.throwsT<InvalidOperationException> (fun _ -> (mv1000 :>IPersistentStack).pop() |> ignore) "pop of empty vector throws"


  
          testCase "drop tests"
          <| fun _ ->

                let r100 = LongRange.create(100) :?> LongRange
                let v100 = pvFromRange(r100)

                let vDropNeg = (v100:>IDrop).drop(-10)
                Expect.equal vDropNeg v100 "Drop neg should return original"

                let s50 = (v100:>IDrop).drop(50) :?> ISeq
                let r50to99 = LongRange.create(50,100) :?> LongRange

                Expect.equal (s50.count()) 50 "Dropped PV should have correct count"
                Expect.isTrue (compareISeqs s50 (rangeSeq r50to99)) "Dropped PV should have correct values"


          testCase "persistent test"
          <| fun _ ->
          
                let r100 = LongRange.create(100) :?> LongRange
                let v100 = pvFromRange(r100)

                let t100 = (v100 :> IEditableCollection).asTransient()
                t100.persistent() |> ignore
                Expect.throwsT<InvalidOperationException> (fun () -> (t100:?>Counted).count() |> ignore) "can's use transient after persistent!"


            


          ]
