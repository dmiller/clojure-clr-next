module RepeatTests

open Expecto
open TestHelpers
open Clojure.Collections
open Clojure.Numerics

[<Tests>]
let repeatTests =
    testList
        "RepeatTests"
        [

          testCase "Infinite repeat goes on for a while"
          <| fun _ ->

              let r = Repeat.create (7) :> ISeq
              Expect.equal (r.first ()) 7 "Item #0"
              Expect.equal (r.next().first ()) 7 "Item #1"
              Expect.equal (r.next().next().first ()) 7 "Item #2"
              Expect.equal (r.next().next().next().first ()) 7 "Item #3"


          testCase "Finite repeat goes just long enough"
          <| fun _ ->
              verifyISeqContents (Repeat.create(0, 7).seq ()) []
              verifyISeqContents (Repeat.create(1, 7).seq ()) [ 7 ]
              verifyISeqContents (Repeat.create(2, 7).seq ()) [ 7; 7 ]
              verifyISeqContents (Repeat.create(5, 7).seq ()) [ 7; 7; 7; 7; 7 ]

          testCase "Finite repeat has counts"
          <| fun _ ->
              Expect.equal (Repeat.create(0, 7).count ()) 0 "0 length"
              Expect.equal (Repeat.create(1, 7).count ()) 1 "1 length"
              Expect.equal (Repeat.create(2, 7).count ()) 2 "2 length"
              Expect.equal (Repeat.create(5, 7).count ()) 5 "5 length"

          testCase "Finite repeat drops properly"
          <| fun _ ->
              let r = Repeat.create (5, 7)
              Expect.equal (r.count ()) 5 "just check"

              let d = r :?> IDrop
              Expect.equal (((d.drop (0)) :?> IPersistentCollection).count ()) 5 "drop 0"
              Expect.equal (((d.drop (1)) :?> IPersistentCollection).count ()) 4 "drop 1"
              Expect.equal (((d.drop (2)) :?> IPersistentCollection).count ()) 3 "drop 2"
              Expect.equal (((d.drop (4)) :?> IPersistentCollection).count ()) 1 "drop 4"
              Expect.isNull (d.drop (5)) "drop all is null"
              Expect.isNull (d.drop (6)) "drop more than all is null"


          testCase "Finite reduce tests"
          <| fun _ ->
              let adder =
                  { new AFn() with
                      member this.ToString() = ""
                    interface IFn with
                        member this.invoke(x, y) = (x :?> int) + (y :?> int) :> obj

                         }

              let adderStopsShort n =
                  { new AFn() with
                      member this.ToString() = ""
                    interface IFn with
                        member this.invoke(x, y) =
                            let acc = x :?> int
                            if acc >= n then Reduced(1000) else acc + (y :?> int) :> obj }


              let r = Repeat.create (5, 7)
              let rr = r :?> IReduce
              Expect.equal (rr.reduce (adder)) 35 "Add them all"
              Expect.equal (rr.reduce (adderStopsShort 20)) 1000 "Add some"
              Expect.equal (rr.reduce(adder, 20)) 55 "Add them all, head start"
              Expect.equal (rr.reduce((adderStopsShort 30),20)) 1000 "Add some, head start"

          testCase "Infinte reduce tests"
          <| fun _ ->
              let adderStopsShort n =
                  { new AFn() with
                      member this.ToString() = ""
                    interface IFn with
                        member this.invoke(x, y) =
                            let acc = x :?> int
                            if acc >= n then Reduced(acc) else acc + (y :?> int) :> obj }

              let r = Repeat.create (7)
              let rr = r :> IReduce
              Expect.equal (rr.reduce (adderStopsShort 20)) 21 "Add some"
              Expect.equal (rr.reduce (adderStopsShort 50)) 56 "Add some"
          ]


[<Tests>]
let iterateTests =
    testList
        "IterateTests"
        [

          testCase "Iterate goes on for a while"
          <| fun _ ->
            let inc = 
                { new AFn() with
                    member this.ToString() = "a"
                  interface IFn with
                    member this.invoke(arg1) = Numbers.incP(arg1) 
                    }
            let iter = Iterate.create(inc,0L)
            Expect.sequenceEqual (takeEager 0 iter) [] "No elements -> empty seq" 
            Expect.sequenceEqual (takeEager 1 iter) [ 0L ]  "enough elements"
            Expect.sequenceEqual (takeEager 2 iter) [ 0L; 1L ]  "enough elements"
            Expect.sequenceEqual (takeEager 5 iter) [ 0L; 1L; 2L; 3L; 4L ]  "enough elements"


          ftestCase "Iterate reduces"
          <| fun _ ->
            let inc = 
                { new AFn() with
                    member this.ToString() = "a"
                  interface IFn with
                    member this.invoke(arg1) = Numbers.incP(arg1) 
                    }
            let adderStopsShort n =
                  { new AFn() with
                      member this.ToString() = ""
                    interface IFn with
                        member this.invoke(x, y) =
                            if Numbers.gte(y ,n:>obj) then Reduced(x) else Numbers.add(x,y) }

            let iter = Iterate.create(inc,0L) :?> IReduce
            Expect.equal (iter.reduce (adderStopsShort 10)) 45L "Add them up for a while"
            Expect.equal (iter.reduce ((adderStopsShort 10),100L)) 145L "Add them up for a while"


            ]


[<Tests>]
let cycleTests =
    testList
        "Cycle Tests"
        [

          testCase "Cycle goes on for a while"
          <| fun _ ->
            ////let c0 = Cycle.create(null)
            ////Expect.equal c0 PersistentList.Empty "null seq should yield PersistentList.Empty"

            ////let c1 = Cycle.create(LongRange.create(1))
            ////Expect.sequenceEqual (takeEager 3 c1) [ 0L; 0L; 0L ] "same"
            
            let c3 = Cycle.create(LongRange.create(3))
            Expect.sequenceEqual (takeEager 5 c3) [ 0L; 1L; 2L; 0L; 1L ] "same"


          ftestCase "Cycle reduces"
          <| fun _ ->
            let adderStopsShort n =
                  { new AFn() with
                      member this.ToString() = ""
                    interface IFn with
                        member this.invoke(x, y) =
                            if Numbers.gte(y ,n:>obj) then Reduced(x) else Numbers.add(x,y) }

            let iter = Cycle.create(LongRange.create(100)) :?> IReduce
            Expect.equal (iter.reduce (adderStopsShort 10)) 45L "Add them up for a while"
            Expect.equal (iter.reduce ((adderStopsShort 10),100L)) 145L "Add them up for a while"


            ]