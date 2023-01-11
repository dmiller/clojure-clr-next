module SimpleRangeTests


open Expecto
open Clojure.Collections
open Clojure.Collections.Simple

[<Tests>]
let simpleRangeTests =
    testList
        "SimpleRange tests"
        [

          testCase "SimpleRange implements ISeq"
          <| fun _ ->
              let c = SimpleIntRange(0, 3) :> ISeq
              let mutable mc = c

              for i = 0 to 3 do
                  Expect.equal (mc.first ()) (upcast i) "ith element of this range should be i"
                  mc <- mc.next ()

              let c2 = c.cons (10)
              Expect.equal (c2.first ()) (upcast 10) "range.cons.first should be new item"
              Expect.equal (c2.next ()) c "range.cons.next should be itself"


          testCase "SimpleRange implements IPersistentCollection"
          <| fun _ ->
              let c = SimpleIntRange(0, 3)
              let pc = c :> IPersistentCollection
              Expect.equal (pc.count ()) 4 "range.count should be item count"
              Expect.equal (pc.empty().GetType()) typeof<SimpleEmptySeq> "range.empty should be an emptySeq"
              Expect.isTrue (pc.equiv (c)) "range should be equiv of itself"
              Expect.isFalse (pc.equiv ((c :> ISeq).next ())) "range should not be equiv of its next"

          testCase "SimpleRange implements Seqable"
          <| fun _ ->
              let c = SimpleIntRange(0, 3)
              Expect.equal ((c :> Seqable).seq ()) (upcast c) "cons.seq should be itself" ]
