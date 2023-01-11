module SimpleStringSeqTests

open Expecto
open Clojure.Collections
open Clojure.Collections.Simple

[<Tests>]
let simpleRangeTests =
    testList
        "SimpleStringSeq tests"
        [

          testCase "SimpleStringSeq implements ISeq"
          <| fun _ ->
              let c = SimpleStringSeq.create("abc")
              let mutable mc = c

              Expect.equal (mc.first()) (upcast 'a') "first element"
              Expect.equal (mc.next().first()) (upcast 'b') "second element"
              Expect.equal (mc.next().next().first()) (upcast 'c') "third element"
              Expect.isNull (mc.next().next().next()) "no more elements"

              let c2 = c.cons (10)
              Expect.equal (c2.first ()) (upcast 10) "range.cons.first should be new item"
              Expect.equal (c2.next ()) c "range.cons.next should be itself"


          testCase "SimpleStringSeq implements IPersistentCollection"
          <| fun _ ->
              let c = SimpleStringSeq.create("abc")
              let pc = c :> IPersistentCollection
              Expect.equal (pc.count ()) 3 "count should be string length"
              Expect.equal (pc.empty().GetType()) typeof<SimpleEmptySeq> ".empty should be an emptySeq"
              Expect.isTrue (pc.equiv (c)) "range should be equiv of itself"
              Expect.isFalse (pc.equiv (c.next ())) "range should not be equiv of its next"

          testCase "SimpleStringSeq implements Seqable"
          <| fun _ ->
              let c = SimpleStringSeq.create("abc")
              Expect.equal ((c :> Seqable).seq ()) c "cons.seq should be itself" 

          testCase "SimpleStringSeq on empty sring returns null"
          <| fun _ ->
              let c = SimpleStringSeq.create("")
              Expect.isNull c "creating on an empty string returns null"

          ]