module PersistentHashMapTests

open Expecto
open Clojure.Collections
open TestHelpers
open System
open System.Collections
open System.Collections.Generic

// TODO:  many of tese tests are identical to those for PersistentHashMapTests.
// Figure out how to consolidate.
// I've marked the non-duplicate ones with a comment.


[<Tests>]
let basicPersistentHashMapCreateTests =
    testList
        "Basic PersistentHashMap create tests"
        [

          // Non-duplicate
          testCase "Create on empty list returns empty map"
          <| fun _ ->
              let a = ArrayList()

              let m = PersistentHashMap.create1 (a) :> IPersistentMap

              Expect.equal (m.count ()) 0 "Empty map should have 0 count"

          // Non-duplicate
          testCase "Create on non-empty list returns non-empty map"
          <| fun _ ->
              let items: obj[] = [| 1; "a"; 2; "b" |]
              let a = ArrayList(items)

              let m = PersistentHashMap.create1 (a) :> IPersistentMap

              Expect.equal (m.count ()) 2 "Count should match # dict entries"
              Expect.equal (m.valAt (1)) (upcast "a") "m[1]=a"
              Expect.equal (m.valAt (2)) (upcast "b") "m[2]=b"
              Expect.isTrue (m.containsKey (1)) "Check containsKey"
              Expect.isFalse (m.containsKey (3)) "Shouldn't contain some random key"

          // Non-duplicate
          testCase "Create on empty ISeq returns empty map"
          <| fun _ ->
              let items: obj[] = Array.empty
              let a = ArrayList(items)
              let s = PersistentList.create(a).seq ()

              let m = PersistentHashMap.create (s) :> IPersistentMap

              Expect.equal (m.count ()) 0 "Empty map should have 0 count"

          // Non-duplicate
          testCase "Create on ISeq returns map"
          <| fun _ ->
              let items: obj[] = [| 1; "a"; 2; "b" |]
              let a = ArrayList(items)
              let s = PersistentList.create(a).seq ()

              let m = PersistentHashMap.create (s) :> IPersistentMap

              Expect.equal (m.count ()) 2 "Count should match # dict entries"
              Expect.equal (m.valAt (1)) (upcast "a") "m[1]=a"
              Expect.equal (m.valAt (2)) (upcast "b") "m[2]=b"
              Expect.isTrue (m.containsKey (1)) "Check containsKey"
              Expect.isFalse (m.containsKey (3)) "Shouldn't contain some random key"

          // Non-duplicate
          testCase "Create on no args return empty map"
          <| fun _ ->
              let m = PersistentHashMap.create () :> IPersistentMap

              Expect.equal (m.count ()) 0 "Empty map should have 0 count"
              Expect.isNull ((m :?> IMeta).meta ()) "Empty map should have no meta"

          // Non-duplicate
          testCase "Create on args returns map"
          <| fun _ ->
              let m = PersistentHashMap.create (1, "a", 2, "b") :> IPersistentMap

              Expect.equal (m.count ()) 2 "Count should match # dict entries"
              Expect.equal (m.valAt (1)) (upcast "a") "m[1]=a"
              Expect.equal (m.valAt (2)) (upcast "b") "m[2]=b"
              Expect.isFalse (m.containsKey (3)) "Shouldn't contain some random key"

          // Non-duplicate
          testCase "Create on met no args return empty map"
          <| fun _ ->
              let meta = metaForSimpleTests

              let m = PersistentHashMap.create (meta :> IPersistentMap) :> IPersistentMap

              Expect.equal (m.count ()) 0 "Empty map should have 0 count"
              Expect.isTrue (Object.ReferenceEquals((m :?> IMeta).meta (), meta)) "Should have identical meta"

          // Non-duplicate
          testCase "Create on metaargs returns map"
          <| fun _ ->
              let meta = metaForSimpleTests
              let init: obj[] = [| 1; "a"; 2; "b" |]

              let m = PersistentHashMap.create (meta, init) :> IPersistentMap

              Expect.equal (m.count ()) 2 "Count should match # dict entries"
              Expect.equal (m.valAt (1)) (upcast "a") "m[1]=a"
              Expect.equal (m.valAt (2)) (upcast "b") "m[2]=b"
              Expect.isFalse (m.containsKey (3)) "Shouldn't contain some random key"
              Expect.isTrue (Object.ReferenceEquals((m :?> IMeta).meta (), meta)) "Should have identical meta" ]

[<Tests>]
let basicPersistentHashMapTests =
    testList
        "Basic PersistentHashMap Tests"
        [

          testCase "Create on empty dictionary returns empty map"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              let m = PersistentHashMap.createD2 (d)

              Expect.equal (m.count ()) 0 "Empty map should have 0 count"

          testCase "Create on non-empty dictionary creates correct map"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              Expect.equal (m.count ()) 2 "Count should match # dict entries"
              Expect.equal (m.valAt (1)) (upcast "a") "m[1]=a"
              Expect.equal (m.valAt (2)) (upcast "b") "m[2]=b"
              Expect.isTrue (m.containsKey (1)) "Check containsKey"
              Expect.isFalse (m.containsKey (3)) "Shouldn't contain some random key" ]

[<Tests>]
let basicPersistentHashMapAssocTests =
    testList
        "Basic P.H.M Assoc tests"
        [

          testCase "containsKey on missing key fails"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              Expect.isFalse (m.containsKey (3)) "Should not contain key"

          testCase "containsKey on present key succeeds"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              Expect.isTrue (m.containsKey (1)) "Should contain key"
              Expect.isTrue (m.containsKey (2)) "Should contain key"

          testCase "containsKey not confused by a value"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              Expect.isFalse (m.containsKey ("a")) "Should not see value as a key"

          testCase "entryAt returns null on missing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              Expect.isNull (m.entryAt (3)) "Should have null entryAt"


          testCase "entryAt returns proper entry for existing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let me = m.entryAt (1)

              Expect.equal (me.key ()) (upcast 1) "Should be the key"
              Expect.equal (me.value ()) (upcast "a") "Should be the value"


          testCase "valAt returns null on missing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              Expect.isNull (m.valAt (3)) "Should have null valAt"


          testCase "valAt returns value on existing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              Expect.equal (m.valAt (1)) (upcast "a") "Should have correct value"


          testCase "valAt2 returns notFound on missing  key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              Expect.equal (m.valAt (3, 99)) (upcast 99) "Should have not-found value"


          testCase "valAt2 returns value on existing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              Expect.equal (m.valAt (1, 99)) (upcast "a") "Should have correct value" ]

[<Tests>]
let basicPersistentHashMapPersistentCollectionTests =
    testList
        "Basic P.H.M PersistentCollection tests"
        [

          testCase "count on empty is 0"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let c = m.empty ()

              Expect.equal (c.count ()) 0 "Empty.count() = 0"

          testCase "count on non-empty returns count of entries"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              Expect.equal (m.count ()) 2 "Count of keys"

          testCase "seq on empty is null"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let c = m.empty ()

              Expect.isNull (c.seq ()) "Seq on empty should be null"

          testCase "seq on non-empty iterates"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let s = m.seq ()
              let me1 = s.first () :?> IMapEntry
              let me2 = s.next().first () :?> IMapEntry
              let last = s.next().next ()

              Expect.equal (s.count ()) 2 "COunt of seq should be # of entries in map"
              Expect.equal (me1.value ()) (m.valAt (me1.key ())) "K/V pair should match map"
              Expect.equal (me2.value ()) (m.valAt (me2.key ())) "K/V pair should match map"
              Expect.notEqual (me1.key ()) (me2.key ()) "Should see different keys"
              Expect.isNull last "end of seq should be null" ]

[<Tests>]
let basicPersistentHashMapPersistentMapTests =
    testList
        "Basic P.H.M PersistentMap tests"
        [

          testCase "assoc modifies value for existing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m1 = PersistentHashMap.createD2 (d)
              let m2 = m1.assoc (2, "c")

              Expect.equal (m1.count ()) 2 "Original map count unchanged"
              Expect.equal (m1.valAt (2)) (upcast "b") "Original map value unchanged"
              Expect.equal (m2.count ()) 2 "Count unchanged"
              Expect.equal (m2.valAt (2)) (upcast "c") "New map has updated value"

          testCase "assoc adds on new key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m1 = PersistentHashMap.createD2 (d)
              let m2 = m1.assoc (3, "c")

              Expect.equal (m1.count ()) 2 "Original map count unchanged"
              Expect.isFalse (m1.containsKey (3)) "Original map does not have new key"
              Expect.equal (m2.count ()) 3 "new map has Count update"
              Expect.equal (m2.valAt (3)) (upcast "c") "New map has new key/value"

          testCase "assocEx failes on exising key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m1 = PersistentHashMap.createD2 (d)
              let f () = m1.assocEx (2, "c") |> ignore

              Expect.throwsT<InvalidOperationException> f "AssocEx throws on existing key"

          testCase "assocEx adds on new key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m1 = PersistentHashMap.createD2 (d)
              let m2 = m1.assocEx (3, "c")

              Expect.equal (m1.count ()) 2 "Original map count unchanged"
              Expect.isFalse (m1.containsKey (3)) "Original map does not have new key"
              Expect.equal (m2.count ()) 3 "new map has Count update"
              Expect.equal (m2.valAt (3)) (upcast "c") "New map has new key/value"

          testCase "without on existing key removes it"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[3] <- "a"
              d.[5] <- "b"
              d.[7] <- "c"

              let m1 = PersistentHashMap.createD2 (d)
              let m2 = m1.without (5)

              Expect.equal (m1.count ()) 3 "Original map has original count"
              Expect.equal (m1.valAt (5)) (upcast "b") "original map still has original key/val"
              Expect.equal (m2.count ()) 2 "without reduces count"
              Expect.isFalse (m2.containsKey (5)) "without removes key/val"

          testCase "without on missing key returns original"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[3] <- "a"
              d.[5] <- "b"
              d.[7] <- "c"

              let m1 = PersistentHashMap.createD2 (d)
              let m2 = m1.without (4)

              Expect.isTrue (m1 = m2) "No change"

          testCase "wihout on all keys yields empty map"
          <| fun _ ->

              let d: Dictionary<int, string> = Dictionary()
              d.[3] <- "a"
              d.[5] <- "b"
              d.[7] <- "c"

              let m1 = PersistentHashMap.createD2 (d)
              let m2 = m1.without(3).without(5).without (7)

              Expect.equal (m2.count ()) 0 "Should be no entries remaining" ]

[<Tests>]
let aPersistentMapTests =
    testList
        "APersistentMap tests for PersistentHashMap"
        [

          testCase "Equiv on similar dictionary"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              Expect.isTrue (m.equiv (d)) "Equal on same dictionary"

          testCase "Equiv on different entry dictionary is false"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              d.[2] <- "c"

              Expect.isFalse (m.equiv (d)) "Equal on different dictionary"

          testCase "Equiv on extra entry dictionary is false"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              d.[3] <- "c"

              Expect.isFalse (m.equiv (d)) "Equal on different dictionary"

          testCase "Hashcode based on value"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m1 = PersistentHashMap.createD2 (d)

              d.[2] <- "c"
              let m2 = PersistentHashMap.createD2 (d)

              Expect.notEqual (m1.GetHashCode()) (m2.GetHashCode()) "Hash codes should differ"

          testCase "Associative.assoc works"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let a = PersistentHashMap.createD2 (d) :> Associative

              let a1 = a.assoc (3, "c")
              let a2 = a.assoc (2, "c")

              Expect.equal (a.count ()) 2 "Original assoc count unchnaged"
              Expect.equal (a1.count ()) 3 "Added assoc count increased"
              Expect.equal (a2.count ()) 2 "Updated assoc count increased"

              Expect.equal (a.valAt (1)) ("a" :> obj) "Original unchanged on untouched key"
              Expect.equal (a1.valAt (1)) ("a" :> obj) "Added unchanged on untouched key"
              Expect.equal (a2.valAt (1)) ("a" :> obj) "Updated unchanged on untouched key"

              Expect.equal (a.valAt (2)) ("b" :> obj) "Original unchanged on untouched key"
              Expect.equal (a1.valAt (2)) ("b" :> obj) "Added unchanged on untouched key"
              Expect.equal (a2.valAt (2)) ("c" :> obj) "Updated changed on untouched key"

              Expect.equal (a1.valAt (3)) ("c" :> obj) "Added has new key"
              Expect.isFalse (a.containsKey (3)) "Original should not have new key"
              Expect.isFalse (a.containsKey (3)) "Updated should not have new key"


          testCase "cons on IMapEntry adds new"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let c = m.cons (MapEntry(3, "c"))

              Expect.equal (m.count ()) 2 "Original has unchanged count"
              Expect.equal (m.valAt (1)) ("a" :> obj) "Original unchanged on untouched key"
              Expect.equal (m.valAt (2)) ("b" :> obj) "Original unchanged on untouched key"
              Expect.isFalse (m.containsKey (3)) "Original should not have new key"

              Expect.equal (c.count ()) 3 "Updated has higher count"
              Expect.equal (c.valAt (1)) ("a" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (2)) ("b" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (3)) ("c" :> obj) "Updated has new key"

          testCase "cons on IMapEntry replaces existing"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let c = m.cons (MapEntry(2, "c"))

              Expect.equal (m.count ()) 2 "Original has unchanged count"
              Expect.equal (m.valAt (1)) ("a" :> obj) "Original unchanged on untouched key"
              Expect.equal (m.valAt (2)) ("b" :> obj) "Original unchanged on untouched key"

              Expect.equal (c.count ()) 2 "Updated has higher count"
              Expect.equal (c.valAt (1)) ("a" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (2)) ("c" :> obj) "Updated has new value"


          testCase "cons on DictionaryEntry adds new"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let c = m.cons (DictionaryEntry(3, "c"))

              Expect.equal (m.count ()) 2 "Original has unchanged count"
              Expect.equal (m.valAt (1)) ("a" :> obj) "Original unchanged on untouched key"
              Expect.equal (m.valAt (2)) ("b" :> obj) "Original unchanged on untouched key"
              Expect.isFalse (m.containsKey (3)) "Original should not have new key"

              Expect.equal (c.count ()) 3 "Updated has higher count"
              Expect.equal (c.valAt (1)) ("a" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (2)) ("b" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (3)) ("c" :> obj) "Updated has new key"

          testCase "cons on DictionaryEntry replaces existing"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let c = m.cons (DictionaryEntry(2, "c"))

              Expect.equal (m.count ()) 2 "Original has unchanged count"
              Expect.equal (m.valAt (1)) ("a" :> obj) "Original unchanged on untouched key"
              Expect.equal (m.valAt (2)) ("b" :> obj) "Original unchanged on untouched key"

              Expect.equal (c.count ()) 2 "Updated has higher count"
              Expect.equal (c.valAt (1)) ("a" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (2)) ("c" :> obj) "Updated has new value"

          testCase "cons on KeyValuePair adds new"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let c = m.cons (MapEntry(3, "c"))

              Expect.equal (m.count ()) 2 "Original has unchanged count"
              Expect.equal (m.valAt (1)) ("a" :> obj) "Original unchanged on untouched key"
              Expect.equal (m.valAt (2)) ("b" :> obj) "Original unchanged on untouched key"
              Expect.isFalse (m.containsKey (3)) "Original should not have new key"

              Expect.equal (c.count ()) 3 "Updated has higher count"
              Expect.equal (c.valAt (1)) ("a" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (2)) ("b" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (3)) ("c" :> obj) "Updated has new key"

          testCase "cons on KeyValuePair replaces existing"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let c = m.cons (MapEntry(2, "c"))

              Expect.equal (m.count ()) 2 "Original has unchanged count"
              Expect.equal (m.valAt (1)) ("a" :> obj) "Original unchanged on untouched key"
              Expect.equal (m.valAt (2)) ("b" :> obj) "Original unchanged on untouched key"

              Expect.equal (c.count ()) 2 "Updated has higher count"
              Expect.equal (c.valAt (1)) ("a" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (2)) ("c" :> obj) "Updated has new value"

          testCase "cons on IPersistentVector adds new"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let c = m.cons (PersistentVector.create (3, "c"))

              Expect.equal (m.count ()) 2 "Original has unchanged count"
              Expect.equal (m.valAt (1)) ("a" :> obj) "Original unchanged on untouched key"
              Expect.equal (m.valAt (2)) ("b" :> obj) "Original unchanged on untouched key"
              Expect.isFalse (m.containsKey (3)) "Original should not have new key"

              Expect.equal (c.count ()) 3 "Updated has higher count"
              Expect.equal (c.valAt (1)) ("a" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (2)) ("b" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (3)) ("c" :> obj) "Updated has new key"

          testCase "cons on IPersistentVector replaces existing"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)
              let c = m.cons (PersistentVector.create (2, "c"))

              Expect.equal (m.count ()) 2 "Original has unchanged count"
              Expect.equal (m.valAt (1)) ("a" :> obj) "Original unchanged on untouched key"
              Expect.equal (m.valAt (2)) ("b" :> obj) "Original unchanged on untouched key"

              Expect.equal (c.count ()) 2 "Updated has higher count"
              Expect.equal (c.valAt (1)) ("a" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (2)) ("c" :> obj) "Updated has new value"

          testCase "cons on IPersistentVector replaces existing 2"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d)

              let f () =
                  m.cons (PersistentVector.create (2, "c", 3, "d")) |> ignore

              Expect.throwsT<ArgumentException> f "should fail on IPersisntetVector with incorrect number of entries"

          testCase "cons on IPersistentMap adds/repalces many"
          <| fun _ ->
              let d1: Dictionary<int, string> = Dictionary()
              d1.[1] <- "a"
              d1.[2] <- "b"

              let m1 = PersistentHashMap.createD2 (d1)

              let d2: Dictionary<int, string> = Dictionary()
              d2.[2] <- "c"
              d2.[3] <- "d"

              let m2 = PersistentHashMap.createD2 (d2)
              let m3 = m1.cons (m2)

              Expect.equal (m1.count ()) 2 "Original should have same count"
              Expect.equal (m2.count ()) 2 "Updater should have same count"
              Expect.equal (m3.count ()) 3 "Updated should have new count"

              Expect.equal (m1.valAt (1)) ("a" :> obj) "Original should be unchanged"
              Expect.equal (m1.valAt (2)) ("b" :> obj) "Original should be unchanged"
              Expect.isFalse (m1.containsKey (3)) "Original should be unchanged"

              Expect.equal (m3.valAt (1)) ("a" :> obj) "Updated should be unchanged on untouched key"
              Expect.equal (m3.valAt (2)) ("c" :> obj) "Updated should have updated key value"
              Expect.equal (m3.valAt (3)) ("d" :> obj) "Updated should have new key value"

          testCase "invoke(k) does valAt(k)"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let f = PersistentHashMap.createD2 (d) :?> IFn

              Expect.equal (f.invoke (1)) ("a" :> obj) "Does ValAt, finds key"
              Expect.isNull (f.invoke (7)) "Does ValAt, does not find key"

          testCase "invoke(k,nf) does valAt(k,nf)"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let f = PersistentHashMap.createD2 (d) :?> IFn

              Expect.equal (f.invoke (1, 99)) ("a" :> obj) "Does ValAt, finds key"
              Expect.equal (f.invoke (7, 99)) (99 :> obj) "Does ValAt, returns notFound value"


          testCase "Dictionary operations fail as necessary"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let id = PersistentHashMap.createD2 (d) :?> IDictionary

              let fadd () = id.Add(3, "c")
              let fclear () = id.Clear()
              let fremove () = id.Remove(1)

              Expect.throwsT<InvalidOperationException> fadd "add operation should fail"
              Expect.throwsT<InvalidOperationException> fclear "clear operation should fail"
              Expect.throwsT<InvalidOperationException> fremove "remove operation should fail"

          testCase "Dictionary operations success as necessary"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let id = PersistentHashMap.createD2 (d) :?> IDictionary

              Expect.isTrue (id.Contains(1)) "Finds existing key"
              Expect.isFalse (id.Contains(7)) "Does not find absent key"
              Expect.isTrue (id.IsFixedSize) "fixedSize is true"
              Expect.isTrue (id.IsReadOnly) "readOnly is true"
              Expect.equal (id.[2]) ("b" :> obj) "Indexing works on existing key"
              Expect.isNull (id.[7]) "Indexing is null on absent key"

          testCase "Dictionary Keys/Values work"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let id = PersistentHashMap.createD2 (d) :?> IDictionary

              let keys = id.Keys
              let vals = id.Values

              Expect.equal (keys.Count) 2 "Keys has correct count"
              Expect.equal (vals.Count) 2 "Values has correct count"

              let akeys: obj[] = Array.zeroCreate 2
              let avals: obj[] = Array.zeroCreate 2

              keys.CopyTo(akeys, 0)
              vals.CopyTo(avals, 0)

              Array.Sort(akeys)
              Array.Sort(avals)

              Expect.equal akeys.[0] (box 1) "first key"
              Expect.equal akeys.[1] (box 2) "second key"

              Expect.equal avals.[0] (upcast "a") "first val"
              Expect.equal avals.[1] (upcast "b") "second val"


          testCase "Dictionary enumerator works"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let id = PersistentHashMap.createD2 (d) :?> IDictionary

              let ie = id.GetEnumerator()

              Expect.isTrue (ie.MoveNext()) "Move to first element"
              let de1 = ie.Current :?> IMapEntry
              Expect.isTrue (ie.MoveNext()) "Move to second element"
              let de2 = ie.Current :?> IMapEntry
              Expect.isFalse (ie.MoveNext()) "Move past end"

              // Could be either order
              Expect.isTrue
                  (match
                      de1.key () :?> int32, de1.value () :?> string, de2.key () :?> int32, de2.value () :?> string
                   with
                   | 1, "a", 2, "b"
                   | 2, "b", 1, "a" -> true
                   | _ -> false)
                  "matched key/val pairs"


          testCase "ICollection goodies work"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let c = PersistentHashMap.createD2 (d) :?> ICollection

              Expect.isTrue (c.IsSynchronized) "should be synchronized"
              Expect.isTrue (Object.ReferenceEquals(c, c.SyncRoot)) "SyncRoot should be self"
              Expect.equal (c.Count) 2 "Count should be correct"

              let a: IMapEntry[] = Array.zeroCreate c.Count
              c.CopyTo(a, 0)

              // Could be either order
              Expect.isTrue
                  (match
                      a.[0].key () :?> int32,
                      a.[0].value () :?> string,
                      a.[1].key () :?> int32,
                      a.[1].value () :?> string
                   with
                   | 1, "a", 2, "b"
                   | 2, "b", 1, "a" -> true
                   | _ -> false)
                  "matched key/val pairs" ]


[<Tests>]
let PersistentHashMapIObjTests =
    testList
        "PersistentHashMap IObj tests"
        [

          testCase "Verify PersistentHashMap.IObj"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentHashMap.createD2 (d) :?> IObj
              let pm = m.withMeta (metaForSimpleTests)

              verifyNullMeta m
              verifyWithMetaHasCorrectMeta pm
              verifyWithMetaNoChange m
              verifyWithMetaReturnType m typeof<PersistentHashMap> ]


//  Make a type with a restricted set of hash code values to test hash collision logic

type CollisionKey(id, factor) =
    override this.GetHashCode() = id % factor

    override this.Equals(o) =
        match o with
        | :? CollisionKey as ck -> ck.Id = this.Id
        | _ -> false

    member _.Id = id
    member _.Factor = factor

let testCollisions (numEntries: int) (numHashCodes: int) : unit =

    // create map with entries  key=CollisionKey(i,_), value = i

    let mutable m = PersistentHashMap.Empty :> IPersistentMap

    for i = 0 to numEntries - 1 do
        m <- m.assoc (CollisionKey(i, numHashCodes), i)

    // Basic
    Expect.equal (m.count ()) numEntries "Should have the number of entries entered in the loop"

    // Check we have all the correct entries, but ggrabbing all the keys in the table
    // putting them in an array, sorting the array and making sure that the i-th entry = i.
    // This checks assoc and seq

    let a: int[] = Array.zeroCreate (m.count ())

    let rec step (i: int) (s: ISeq) =
        if not (isNull s) then
            let kv = s.first () :?> IMapEntry
            a.[i] <- (kv.key () :?> CollisionKey).Id
            step (i + 1) (s.next ())

    step 0 (m.seq ())

    let b = Array.sort (a)

    for i = 0 to b.Length - 1 do
        Expect.equal b.[i] i "Key I"




    // check key enumerator

    let a: int[] = Array.zeroCreate (m.count ())
    let imek = (m :?> IMapEnumerable).keyEnumerator ()

    let rec step (i: int) =
        if imek.MoveNext() then
            a.[i] <- (imek.Current :?> CollisionKey).Id
            step (i + 1)

    step 0

    let b = Array.sort (a)

    for i = 0 to b.Length - 1 do
        Expect.equal b.[i] i "Key I"

    // check value enumerator

    let a: int[] = Array.zeroCreate (m.count ())
    let imek = (m :?> IMapEnumerable).valEnumerator ()

    let rec step (i: int) =
        if imek.MoveNext() then
            a.[i] <- (imek.Current :?> int)
            step (i + 1)

    step 0

    let b = Array.sort (a)

    for i = 0 to b.Length - 1 do
        Expect.equal b.[i] i "Val I"

    // Regular IEnumerable

    let a: int[] = Array.zeroCreate (m.count ())
    let mutable i = 0

    for kv in (m :> IEnumerable) do
        let id = ((kv :?> IMapEntry).key () :?> CollisionKey).Id
        a.[i] <- id
        i <- i + 1

    let b = Array.sort (a)

    for i = 0 to b.Length - 1 do
        Expect.equal b.[i] i "Key I"

    // IEnumerable<IMapEntry>

    let a: int[] = Array.zeroCreate (m.count ())
    let mutable i = 0

    for kv in (m :> IEnumerable<IMapEntry>) do
        let id = (kv.key () :?> CollisionKey).Id
        a.[i] <- id
        i <- i + 1

    let b = Array.sort (a)

    for i = 0 to b.Length - 1 do
        Expect.equal b.[i] i "Key I"

    // Make sure key is associated with correct value
    for kv in (m :> IEnumerable<IMapEntry>) do
        let key = kv.key () :?> CollisionKey
        let value = kv.value () :?> int
        Expect.equal key.Id value "Value should be same as key.Id"

    for i = 0 to numEntries - 1 do
        let key = CollisionKey(i, numHashCodes)
        let v = m.valAt (key)
        Expect.isNotNull v "key should be found"
        Expect.equal (v :?> int) i "Value should be same key.id"
        let kv = m.entryAt (key)
        Expect.isNotNull kv "Key/Value shoudl be found"
        Expect.equal (kv.key ()) (upcast key) "Should find matching key"
        Expect.equal (kv.value () :?> int) i "Value should be same key.id"

    let key = CollisionKey(numEntries + 1000, numHashCodes)

    let v = m.valAt (key)
    Expect.isNull v "Should not find value for key not in map"
    let kv = m.entryAt (key)
    Expect.isNull kv "Should not entry for key not in map"






[<Tests>]
let collisionTests =
    testList
        "PersistentHashMap collision tests"
        [ testCase "Collisions 100 10" <| fun _ -> testCollisions 100 10
          testCase "Collisions 1000 10" <| fun _ -> testCollisions 1000 10
          testCase "Collisions 10000 10" <| fun _ -> testCollisions 10000 10
          testCase "Collisions 10000 100" <| fun _ -> testCollisions 10000 100 ]


let doBigTransientTest (numEntries: int) =
    printfn "Testing %i items." numEntries

    let rnd = Random(123)
    let dict = Dictionary<int, int>(numEntries)

    for i = 0 to numEntries - 1 do
        let r = rnd.Next()
        dict.[r] <- r

    let m = PersistentHashMap.createD2 (dict)

    Expect.equal (m.count ()) (dict.Count) "Should have same number of entries"

    for key in dict.Keys do
        Expect.isTrue (m.containsKey (key :> obj)) "dictionary key should be in map"
        Expect.equal (m.valAt (key)) (upcast key) "Value should be same as key"

    let mutable s = m.seq ()

    while not (isNull s) do
        let entry = s.first () :?> IMapEntry
        Expect.isTrue (dict.ContainsKey(entry.key () :?> int)) "map key shoudl be in dictionary"
        s <- s.next ()


let doBigAssocCreateTest (numEntries: int) =
    printfn "Testing %i items." numEntries

    let rnd = Random(123)
    let dict = Dictionary<int, int>(numEntries)
    let mutable m = PersistentHashMap.Empty :> IPersistentMap

    for i = 0 to numEntries - 1 do
        let r = rnd.Next()
        dict.[r] <- r
        m <- m.assoc(r,r)

    Expect.equal (m.count ()) (dict.Count) "Should have same number of entries"

    for key in dict.Keys do
        Expect.isTrue (m.containsKey (key :> obj)) "dictionary key should be in map"
        Expect.equal (m.valAt (key)) (upcast key) "Value should be same as key"

    let mutable s = m.seq ()

    while not (isNull s) do
        let entry = s.first () :?> IMapEntry
        Expect.isTrue (dict.ContainsKey(entry.key () :?> int)) "map key shoudl be in dictionary"
        s <- s.next ()

let doBigAssocUpdateTest (numEntries: int) =
    printfn "Testing %i items." numEntries

    let rnd = Random(123)
    let dict = Dictionary<int, int>(numEntries)

    for i = 0 to numEntries - 1 do
        let r = rnd.Next()
        dict.[r] <- r

    let mutable m = PersistentHashMap.createD2 (dict)
    for r in dict do
        m <- m.assoc(r.Key, r.Key+1)

    Expect.equal (m.count ()) (dict.Count) "Should have same number of entries"

    for key in dict.Keys do
        Expect.isTrue (m.containsKey (key :> obj)) "dictionary key should be in map"
        Expect.equal (m.valAt (key)) (upcast (key+1)) "Value should be same as key"

    let mutable s = m.seq ()

    while not (isNull s) do
        let entry = s.first () :?> IMapEntry
        Expect.isTrue (dict.ContainsKey(entry.key () :?> int)) "map key shoudl be in dictionary"
        s <- s.next ()


let doBigWithoutTest (numEntries: int) =
    printfn "Testing %i items." numEntries

    let rnd = Random(123)
    let dict = Dictionary<int, int>(numEntries)
    let keepers = List<int>()
    let withouts = List<int>()

    for i = 0 to numEntries - 1 do
        let r = rnd.Next()
        if not <| dict.ContainsKey(r) then 
            if i % 2 = 0 then keepers.Add(r) else withouts.Add(r)
            dict.[r] <- r

    let mutable m = PersistentHashMap.createD2 (dict)
    for r in withouts do
        m <- m.without(r)

    Expect.equal (m.count ()) (keepers.Count) "Should have same number of entries"

    for r in keepers do
        Expect.isTrue (m.containsKey (r :> obj)) "keepers key should be in map"
        Expect.equal (m.valAt (r)) (upcast r) "Value should be same as key"

    for r in withouts do
        Expect.isFalse (m.containsKey (r :> obj)) "withouts key should not be in map"



[<Tests>]
let bigPersistentHashMapTransientInsertionTests =
    testList
        "big insertions via transiency into PersistentHashMap"
        [ testCase "Transient insertion test for 100" <| fun _ -> doBigTransientTest 100
          testCase "Transient insertion test for 1000" <| fun _ -> doBigTransientTest 1000
          testCase "Transient insertion test for 10000" <| fun _ -> doBigTransientTest 10000
          testCase "Transient insertion test for 100000" <| fun _ -> doBigTransientTest 100000 ]

[<Tests>]
let bigPersistentHashMapAssocInsertTests =
    testList
        "big insertions via regular assoc into PersistentHashMap"
        [ testCase "Assoc insertion test for 100" <| fun _ -> doBigAssocCreateTest 100
          testCase "Assoc insertion test for 1000" <| fun _ -> doBigAssocCreateTest 1000
          testCase "Transient insertion test for 10000" <| fun _ -> doBigAssocCreateTest 10000
          testCase "Assoc insertion test for 100000" <| fun _ -> doBigAssocCreateTest 100000 ]

[<Tests>]
let bigPersistentHashMapAssocUpdateTests =
    testList
        "Updates via assoc into PersistentHashMap"
        [ testCase "Assoc update test for 100" <| fun _ -> doBigAssocUpdateTest 100
          testCase "Assoc update test for 1000" <| fun _ -> doBigAssocUpdateTest 1000
          testCase "Assoc update test for 10000" <| fun _ -> doBigAssocUpdateTest 10000
          testCase "Assoc update test for 100000" <| fun _ -> doBigAssocUpdateTest 100000 ]

[<Tests>]
let bigPersistentHashMapWithoutTests =
    testList
        "Updates via without into PersistentHashMap"
        [ testCase "Without update test for 10" <| fun _ -> doBigWithoutTest 10
          testCase "Without update test for 100" <| fun _ -> doBigWithoutTest 100
          testCase "Without update test for 1000" <| fun _ -> doBigWithoutTest 1000
          testCase "Without update test for 10000" <| fun _ -> doBigWithoutTest 10000
          testCase "Without update test for 100000" <| fun _ -> doBigWithoutTest 100000 ]


[<Tests>]
let myLittleTest =
    testList
        "Just do one thing"
        [ testCase "whatever"
          <| fun _ ->
                let mutable pv =
                    (Clojure.Collections.PersistentHashMap.Empty :> Clojure.Collections.IEditableCollection) 
                        .asTransient () :?> Clojure.Collections.ITransientAssociative

                pv <- pv.assoc(1L,1L)
                pv <- pv.assoc(2L, 2L)
                pv <- pv.assoc(3L, 3L)
                pv <- pv.assoc(4L, 4L)
                pv <- pv.assoc(5L, 5L)

                Expect.equal (pv.persistent().count()) 5 "Equal"



            ]

                