module SimpleConsTests

open Expecto
open Clojure.Collections
open Clojure.Collections.Simple

let makeConsSeq (n: int) =
    let rec step i (s: ISeq) =
        if i < 0 then s else step (i - 1) (s.cons i)

    step (n - 1) (SimpleEmptySeq())

let x = makeConsSeq 3
let y = x.count ()

[<Tests>]
let simpleEmptySeqTests =
    testList
        "simpleEmptySeq"
        [

          testCase "simpleEmptySeq implements ISeq"
          <| fun _ ->
              let es = SimpleEmptySeq() :> ISeq
              Expect.isNull (es.first ()) "emptyseq.first() should be null"
              Expect.isNull (es.next ()) "emptyseq.next() should be null"
              Expect.equal (es.more ()) es "emptyseq.more should be itself"

              let c = es.cons (1)
              Expect.equal (c.first ()) (upcast 1) "emptyseq.cons.first should be new item"
              Expect.isNull (c.next ()) "emptyseq.cons.next should be null"


          testCase "simpleEmptySeq implements IPersistentCollection"
          <| fun _ ->
              let es = SimpleEmptySeq() :> IPersistentCollection

              Expect.equal (es.count ()) 0 "emptyseq.count should be 0"
              Expect.equal (es.empty ()) es "emptyseq.empty should be itself"
              Expect.isTrue (es.equiv (es)) "emptyseq should be equiv of itself"
              Expect.isFalse (es.equiv (es.cons (1))) "emtpyseq should not be equive to a seq with an element"

          testCase "simpleEmptySeq implements Seqable"
          <| fun _ ->
              let es = SimpleEmptySeq()
              Expect.isNull ((es :> Seqable).seq ()) "emptyseq.seq should be null" ]

[<Tests>]
let consTests =
    testList
        "simpleCons"
        [

          testCase "simpleCons implements ISeq"
          <| fun _ ->
              let c = makeConsSeq 4
              let mutable mc = c

              for i = 0 to 3 do
                  Expect.equal (mc.first ()) (upcast i) "ith element of c should be i"
                  mc <- mc.next ()

              let c2 = c.cons (10)
              Expect.equal (c2.first ()) (upcast 10) "cons.cons.first should be new item"
              Expect.equal (c2.next ()) c "cons.cons.next should be itself"


          testCase "simpleCons implements IPersistentCollection"
          <| fun _ ->
              let c = makeConsSeq 4
              Expect.equal (c.count ()) 4 "cons.count should be item count"
              Expect.equal (c.empty().GetType()) typeof<SimpleEmptySeq> "cons.empty should be an emptySeq"
              Expect.isTrue (c.equiv (c)) "cons should be equiv of itself"
              Expect.isFalse (c.equiv (c.next ())) "cons should not be equiv of its next"

          testCase "simpleCons implements Seqable"
          <| fun _ ->
              let c = makeConsSeq 4
              Expect.equal ((c :> Seqable).seq ()) c "cons.seq should be itself" ]
