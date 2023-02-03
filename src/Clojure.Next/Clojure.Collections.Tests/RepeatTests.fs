module RepeatTests

open Expecto
open Clojure.Collections
open TestHelpers


[<Tests>]
let consTests =
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
