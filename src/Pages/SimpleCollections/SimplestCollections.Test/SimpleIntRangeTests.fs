module SimpleIntRangeTests


open Expecto
open Clojure.Collections
open Clojure.Collections.Simple

[<Tests>]
let simpleRangeTests =
    testList
        "SimpleRange tests"
        [

          testCase "SimpleIntRange implements ISeq"
          <| fun _ ->
              let c = SimpleIntRange.create(0, 3)
              let mutable mc = c

              for i = 0 to 3 do
                  Expect.equal (mc.first ()) (upcast i) "ith element of this range should be i"
                  mc <- mc.next ()

              let c2 = c.cons (10)
              Expect.equal (c2.first ()) (upcast 10) "range.cons.first should be new item"
              Expect.equal (c2.next ()) c "range.cons.next should be itself"


          testCase "SimpleIntRange implements IPersistentCollection"
          <| fun _ ->
              let c = SimpleIntRange.create(0, 3)
              let pc = c :> IPersistentCollection
              Expect.equal (pc.count ()) 4 "range.count should be item count"
              Expect.equal (pc.empty().GetType()) typeof<SimpleEmptySeq> "range.empty should be an emptySeq"
              Expect.isTrue (pc.equiv (c)) "range should be equiv of itself"
              Expect.isFalse (pc.equiv (c.next ())) "range should not be equiv of its next"

          testCase "SimpleIntRange implements Seqable"
          <| fun _ ->
              let c = SimpleIntRange.create(0, 3)
              Expect.equal ((c :> Seqable).seq ()) c "cons.seq should be itself" 

          testCase "SimpleIntRange on empty range creates a SimpleEmptySeq"
          <| fun _ ->
              let c = SimpleIntRange.create(3, 0)
              Expect.isTrue  (c.GetType() = typeof<SimpleEmptySeq>) "creating an cmpty range"

          ]