module SimpleCollectionsTests

open Expecto
open Clojure.Collections
open Clojure.Collections.Simple


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
              let es =
                  SimpleEmptySeq() :> IPersistentCollection

              Expect.equal (es.count ()) 0 "emptyseq.count should be 0"
              Expect.equal (es.empty ()) es "emptyseq.empty should be itself"
              Expect.isTrue (es.equiv (es)) "emptyseq should be equiv of itself"
              Expect.isFalse (es.equiv (es.cons (1))) "emtpyseq should not be equive to a seq with an element"

          testCase "simpleEmptySeq implements Seqable"
          <| fun _ ->
              let es = SimpleEmptySeq()
              Expect.isNull ((es :> Seqable).seq()) "emptyseq.seq should be null" ]

[<Tests>]
let consTests =
    testList
        "simpleCons"
        [

          testCase "simpleCons implements ISeq"
          <| fun _ ->
              let c = SimpleCons.makeConsSeq 4
              let mutable mc = c
              for i = 0 to 3 do
                  Expect.equal (mc.first ()) (upcast i) "ith element of c should be i"
                  mc <- mc.next ()

              let c2 = c.cons (10)
              Expect.equal (c2.first ()) (upcast 10) "cons.cons.first should be new item"
              Expect.equal (c2.next ()) c "cons.cons.next should be itself"


          testCase "simpleCons implements IPersistentCollection"
          <| fun _ ->
              let c = SimpleCons.makeConsSeq 4
              Expect.equal (c.count ()) 4 "cons.count should be item count"
              Expect.equal (c.empty().GetType()) typeof<SimpleEmptySeq> "cons.empty should be an emptySeq"
              Expect.isTrue (c.equiv (c)) "cons should be equiv of itself"
              Expect.isFalse (c.equiv (c.next ())) "cons should not be equiv of its next"

          testCase "simpleCons implements Seqable"
          <| fun _ ->
              let c = SimpleCons.makeConsSeq 4
              Expect.equal ((c :> Seqable).seq()) c "cons.seq should be itself" ]


[<Tests>]
let simpleRangeTests =
    testList
        "SimpleRange tests"
        [

          testCase "SimpleRange implements ISeq"
          <| fun _ ->
              let c = SimpleRange(0, 3) :> ISeq
              let mutable mc = c
              for i = 0 to 3 do
                  Expect.equal (mc.first ()) (upcast i) "ith element of this range should be i"
                  mc <- mc.next ()

              let c2 = c.cons (10)
              Expect.equal (c2.first ()) (upcast 10) "range.cons.first should be new item"
              Expect.equal (c2.next ()) c "range.cons.next should be itself"


          testCase "SimpleRange implements IPersistentCollection"
          <| fun _ ->
              let c = SimpleRange(0, 3)
              let pc = c :> IPersistentCollection
              Expect.equal (pc.count ()) 4 "range.count should be item count"
              Expect.equal (pc.empty().GetType()) typeof<SimpleEmptySeq> "range.empty should be an emptySeq"
              Expect.isTrue (pc.equiv (c)) "range should be equiv of itself"
              Expect.isFalse (pc.equiv ((c :> ISeq).next())) "range should not be equiv of its next"

          testCase "SimpleRange implements Seqable"
          <| fun _ ->
              let c = SimpleRange(0, 3)
              Expect.equal ((c :> Seqable).seq()) (upcast c) "cons.seq should be itself" ]


[<Tests>]
let simpleMapTests =
    testList
        "SimpleMap tests"
        [

          testCase "SimpleMap.empty"
          <| fun _ ->
              let sm = SimpleMap.makeSimpleMap 5
              let em = (sm :> IPersistentCollection).empty()
              Expect.equal (em.count ()) 0 "Empty should have no elements"

          testCase "simpleMap.count"
          <| fun _ ->
              let sm5 = SimpleMap.makeSimpleMap 5
              let sm0 = SimpleMap([], [])
              Expect.equal ((sm5 :> IPersistentCollection).count()) 5 "size 5 map"
              Expect.equal ((sm0 :> IPersistentCollection).count()) 0 "size 0 map"

          testCase "simpleMap.valAt(_)"
          <| fun _ ->
              let sm5 = SimpleMap.makeSimpleMap 5
              let sm5Val = (sm5 :> ILookup).valAt('b')
              let sm5Missing = (sm5 :> ILookup).valAt(99)
              Expect.equal sm5Val (box 'B') "found key has correct value"
              Expect.isNull sm5Missing "missing key gets null"

          testCase "simpleMap.valAt(_,_)"
          <| fun _ ->
              let sm5 = SimpleMap.makeSimpleMap 5
              let sm5Val = (sm5 :> ILookup).valAt('b', 2000)
              let sm5Missing = (sm5 :> ILookup).valAt(99, 2000)
              Expect.equal sm5Val (box 'B') "found key has correct value"
              Expect.equal sm5Missing (box 2000) "missing key gets null"

          testCase "simpleMap.cons"
          <| fun _ ->
              let sm5 = SimpleMap.makeSimpleMap 5

              let mod5 =
                  (sm5 :> IPersistentMap)
                      .cons(SimpleMapEntry('b', "BBB"))

              let sm5Val = (sm5 :> ILookup).valAt('b')
              let modVal = (mod5 :> ILookup).valAt('b')
              Expect.equal sm5Val (box 'B') "Original has correct value"
              Expect.equal modVal ("BBB" :> obj) "Modified has new value"

          testCase "simpleMap.equiv"
          <| fun _ ->
              let sm5a = SimpleMap.makeSimpleMap 5
              let sm5b = SimpleMap.makeSimpleMap 5
              let sm5c = (sm5b :> IPersistentMap).without('b') // remove a key/val pai

              let sm5d =
                  ((sm5b :> IPersistentMap).without('b'))
                      .assoc('b', 'B') // change order of key/val lists

              let sm5e =
                  (sm5b :> IPersistentMap).assoc('b', "BBB") // change value of a key

              let sm4 = SimpleMap.makeSimpleMap 4
              let sm0 = SimpleMap.makeSimpleMap 0
              Expect.isTrue ((sm5a :> IPersistentCollection).equiv(sm5a)) "Should be equiv to itself"
              Expect.isTrue ((sm5a :> IPersistentCollection).equiv(sm5b)) "Should be equiv to a duplicate"

              Expect.isFalse
                  ((sm5a :> IPersistentCollection).equiv(sm5c))
                  "Should not be equiv to one with missing value"

              Expect.isFalse
                  ((sm5a :> IPersistentCollection).equiv(sm4))
                  "Should not be equiv to one with missing value"

              Expect.isFalse
                  ((sm4 :> IPersistentCollection).equiv(sm5a))
                  "Should not be equiv to one with missing value"

              Expect.isTrue
                  ((sm5a :> IPersistentCollection).equiv(sm5d))
                  "Should be equiv to one with keys/vals in different order"

              Expect.isFalse
                  ((sm5a :> IPersistentCollection).equiv(sm5e))
                  "Should not be equiv to one with a differnt value"

              Expect.isFalse ((sm5a :> IPersistentCollection).equiv(sm0)) "Non-empty should not be equiv to empty"
              Expect.isFalse ((sm0 :> IPersistentCollection).equiv(sm5a)) "Empty should not be equiv to non-empty"
              Expect.isTrue ((sm0 :> IPersistentCollection).equiv(sm0)) "SEmpty ould be equiv to itself" ]
