module RangeTests

open Expecto
open TestHelpers
open Clojure.Collections
open Clojure.Numerics
open System.Numerics
open System.Collections



[<Tests>]
let rangeTests =
    testList
        "RangeTests"
        [

          testCase "Basic tests from Clojure tests"
          <| fun _ ->

              verifyISeqContents (Range.create(1).seq ()) [ 0L ]
              verifyISeqContents (Range.create(5).seq ()) [ 0L; 1L; 2L; 3L; 4L ]
              Expect.isNull (Range.create(-1).seq ()) "No elements"
              verifyISeqContents (Range.create(2.5).seq ()) [ 0L; 1L; 2L ]
              verifyISeqContents (Range.create(Ratio(BigInteger(7), BigInteger(3))).seq ()) [ 0L; 1L; 2L ]
              verifyISeqContents (Range.create(0L, 3).seq ()) [ 0L; 1L; 2L ]

              Expect.isNull (Range.create(0L, 0).seq ()) "No elements"
              Expect.isNull (Range.create(0L, -3).seq ()) "No elements"
              verifyISeqContents (Range.create(3L, 6).seq ()) [ 3L; 4L; 5L ]
              verifyISeqContents (Range.create(3L, 4).seq ()) [ 3L ]
              Expect.isNull (Range.create(3L, 3).seq ()) "No elements"
              Expect.isNull (Range.create(3L, 1).seq ()) "No elements"
              Expect.isNull (Range.create(3L, 0).seq ()) "No elements"
              Expect.isNull (Range.create(3L, -2).seq ()) "No elements"
              verifyISeqContents (Range.create(-2L, 5).seq ()) [ -2L; -1L; 0L; 1L; 2L; 3L; 4L ]
              verifyISeqContents (Range.create(-2L, 0).seq ()) [ -2L; -1L ]
              verifyISeqContents (Range.create(-2L, -1).seq ()) [ -2L ]
              Expect.isNull (Range.create(-2L, -2).seq ()) "No elements"
              Expect.isNull (Range.create(-2L, -5).seq ()) "No elements"

              Expect.equal (Range.create(3, 9, 0).GetType()) typeof<Repeat> "0 step yields Repeat"
              Expect.equal (Range.create(9, 3, 0).GetType()) typeof<Repeat> "0 step yields Repeat"

              Expect.isNull (Range.create(0, 0, 0).seq ()) "No elements"
              verifyISeqContents (Range.create(3L, 9, 1).seq ()) [ 3L; 4L; 5L; 6L; 7L; 8L ]
              verifyISeqContents (Range.create(3L, 9, 2).seq ()) [ 3L; 5L; 7L ]
              verifyISeqContents (Range.create(3L, 9, 3).seq ()) [ 3L; 6L ]
              verifyISeqContents (Range.create(3L, 9, 10).seq ()) [ 3L ]
              Expect.isNull (Range.create(3L, 9, -1).seq ()) "No elements"

              Expect.isNull (Range.create(10L, 10, -1).seq ()) "No elements"
              verifyISeqContents (Range.create(10L, 9, -1).seq ()) [ 10L ]
              verifyISeqContents (Range.create(10L, 8, -1).seq ()) [ 10L; 9L ]
              verifyISeqContents (Range.create(10L, 7, -1).seq ()) [ 10L; 9L; 8L ]
              verifyISeqContents (Range.create(10L, 0, -2).seq ()) [ 10L; 8L; 6L; 4L; 2L ]

              let mr (x: int, y: int) : Ratio = Ratio(BigInteger(x), BigInteger(y))

              verifyISeqContents
                  (Range.create(mr (1, 2), 5, mr (1, 3)).seq ())
                  [ mr (1, 2)
                    mr (5, 6)
                    mr (7, 6)
                    mr (3, 2)
                    mr (11, 6)
                    mr (13, 6)
                    mr (5, 2)
                    mr (17, 6)
                    mr (19, 6)
                    mr (7, 2)
                    mr (23, 6)
                    mr (25, 6)
                    mr (9, 2)
                    mr (29, 6) ]

              verifyISeqContents (Range.create(0.5,8,1.2).seq()) [ 0.5; 1.7; 2.9; 4.1; 5.3; 6.5; 7.7]
              verifyISeqContents (Range.create(0.5,-4,-2).seq()) [0.5; -1.5; -3.5]


          testCase "Reduce tests from Clojure tests"
          <| fun _ ->
              let adder =
                  { new AFn() with
                      member this.ToString() = ""
                    interface IFn with
                        member this.invoke(x, y) = Numbers.addP(x,y)

                         }

              let adderStopsShort n =
                  { new AFn() with
                      member this.ToString() = ""
                    interface IFn with
                        member this.invoke(x, y) =
                            if Numbers.gte(y ,n:>obj) then Reduced(x) else Numbers.add(x,y) }
              let r100 = Range.create(100) :?> IReduce
              let r1000 = Range.create(1000) :?> IReduce

              Expect.equal (r100.reduce (adder)) 4950L "Add them all"
              Expect.equal (r100.reduce (adder, 200)) 5150L "Add them all"

              Expect.equal (r1000.reduce((adderStopsShort 100))) 4950L "Add first 100"

              
          testCase "Enumerator"
          <| fun _ ->
            let r5 = Range.create(5):?>IEnumerable
            let e5 = r5.GetEnumerator()
            let l5 = Seq.cast<obj> r5

            Expect.sequenceEqual l5 [ 0L; 1L; 2L; 3L; 4L] "Same sequence"


          ]



[<Tests>]
let longRangeTests =
    ftestList
        "LongRangeTests"
        [

          testCase "Basic tests from Clojure tests"
          <| fun _ ->

              verifyISeqContents (LongRange.create(1).seq ()) [ 0L ]
              verifyISeqContents (LongRange.create(5).seq ()) [ 0L; 1L; 2L; 3L; 4L ]
              Expect.isNull (LongRange.create(-1).seq ()) "No elements"
              verifyISeqContents (LongRange.create(0L, 3).seq ()) [ 0L; 1L; 2L ]

              Expect.isNull (LongRange.create(0L, 0).seq ()) "No elements"
              Expect.isNull (LongRange.create(0L, -3).seq ()) "No elements"
              verifyISeqContents (LongRange.create(3L, 6).seq ()) [ 3L; 4L; 5L ]
              verifyISeqContents (LongRange.create(3L, 4).seq ()) [ 3L ]
              Expect.isNull (LongRange.create(3L, 3).seq ()) "No elements"
              Expect.isNull (LongRange.create(3L, 1).seq ()) "No elements"
              Expect.isNull (LongRange.create(3L, 0).seq ()) "No elements"
              Expect.isNull (LongRange.create(3L, -2).seq ()) "No elements"
              verifyISeqContents (LongRange.create(-2L, 5).seq ()) [ -2L; -1L; 0L; 1L; 2L; 3L; 4L ]
              verifyISeqContents (LongRange.create(-2L, 0).seq ()) [ -2L; -1L ]
              verifyISeqContents (LongRange.create(-2L, -1).seq ()) [ -2L ]
              Expect.isNull (LongRange.create(-2L, -2).seq ()) "No elements"
              Expect.isNull (LongRange.create(-2L, -5).seq ()) "No elements"

              Expect.equal (LongRange.create(3, 9, 0).GetType()) typeof<Repeat> "0 step yields Repeat"
              Expect.equal (LongRange.create(9, 3, 0).GetType()) typeof<Repeat> "0 step yields Repeat"

              Expect.isNull (LongRange.create(0, 0, 0).seq ()) "No elements"
              verifyISeqContents (LongRange.create(3L, 9, 1).seq ()) [ 3L; 4L; 5L; 6L; 7L; 8L ]
              verifyISeqContents (LongRange.create(3L, 9, 2).seq ()) [ 3L; 5L; 7L ]
              verifyISeqContents (LongRange.create(3L, 9, 3).seq ()) [ 3L; 6L ]
              verifyISeqContents (LongRange.create(3L, 9, 10).seq ()) [ 3L ]
              Expect.isNull (LongRange.create(3L, 9, -1).seq ()) "No elements"

              Expect.isNull (LongRange.create(10L, 10, -1).seq ()) "No elements"
              verifyISeqContents (LongRange.create(10L, 9, -1).seq ()) [ 10L ]
              verifyISeqContents (LongRange.create(10L, 8, -1).seq ()) [ 10L; 9L ]
              verifyISeqContents (LongRange.create(10L, 7, -1).seq ()) [ 10L; 9L; 8L ]
              verifyISeqContents (LongRange.create(10L, 0, -2).seq ()) [ 10L; 8L; 6L; 4L; 2L ]


          testCase "Reduce tests from Clojure tests"
          <| fun _ ->
              let adder =
                  { new AFn() with
                      member this.ToString() = ""
                    interface IFn with
                        member this.invoke(x, y) = Numbers.addP(x,y)

                         }

              let adderStopsShort n =
                  { new AFn() with
                      member this.ToString() = ""
                    interface IFn with
                        member this.invoke(x, y) =
                            if Numbers.gte(y ,n:>obj) then Reduced(x) else Numbers.add(x,y) }
              let r100 = LongRange.create(100) :?> IReduce
              let r1000 = LongRange.create(1000) :?> IReduce

              Expect.equal (r100.reduce (adder)) 4950L "Add them all"
              Expect.equal (r100.reduce (adder, 200)) 5150L "Add them all"

              Expect.equal (r1000.reduce((adderStopsShort 100))) 4950L "Add first 100"


              
          testCase "Enumerator"
          <| fun _ ->
            let r5 = LongRange.create(5):?>IEnumerable
            let e5 = r5.GetEnumerator()
            let l5 = Seq.cast<obj> r5

            Expect.sequenceEqual l5 [ 0L; 1L; 2L; 3L; 4L] "Same sequence"


          ]

       


