module SimpleHashMap1Tests

// TODO:  many of tese tests are identical to those in PersistentHashMapTests.
// Figure out how to consolidate.

open Expecto
open Clojure.Collections.Simple
open System.Collections
open Clojure.Collections
open System.Collections.Generic
open System


[<Tests>]
let basicSimpleHashMapCreateTests =
    testList
        "Basic SimpleHashMap create tests"
        [


          testCase "Create on empty list returns empty map"
          <| fun _ ->
              let a = ArrayList()
              let m = SimpleHashMap.create1 (a)

              Expect.equal (m.count ()) 0 "Empty map should have 0 count"

          testCase "Create on non-empty list returns non-empty map"
          <| fun _ ->
              let items: obj [] = [| 1; "a"; 2; "b" |]
              let a = ArrayList(items)
              let m = SimpleHashMap.create1 (a)

              Expect.equal (m.count ()) 2 "Count should match # dict entries"
              Expect.equal (m.valAt (1)) (upcast "a") "m[1]=a"
              Expect.equal (m.valAt (2)) (upcast "b") "m[2]=b"
              Expect.isTrue (m.containsKey (1)) "Check containsKey"
              Expect.isFalse (m.containsKey (3)) "Shouldn't contain some random key"

          testCase "Create on empty dictionary returns empty map"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              let m = SimpleHashMap.create (d)

              Expect.equal (m.count ()) 0 "Empty map should have 0 count"

          testCase "Create on non-empty dictionary creates correct map"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)

              Expect.equal (m.count ()) 2 "Count should match # dict entries"
              Expect.equal (m.valAt (1)) (upcast "a") "m[1]=a"
              Expect.equal (m.valAt (2)) (upcast "b") "m[2]=b"
              Expect.isTrue (m.containsKey (1)) "Check containsKey"
              Expect.isFalse (m.containsKey (3)) "Shouldn't contain some random key" ]


[<Tests>]
let basicSimpleHashMapAssocTests =
    testList
        "Basic SimpleHashMap Assoc tests"
        [

          testCase "containsKey on missing key fails"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)

              Expect.isFalse (m.containsKey (3)) "Should not contain key"

          testCase "containsKey on present key succeeds"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)

              Expect.isTrue (m.containsKey (1)) "Should contain key"
              Expect.isTrue (m.containsKey (2)) "Should contain key"

          testCase "containsKey not confused by a value"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)

              Expect.isFalse (m.containsKey ("a")) "Should not see value as a key"

          testCase "entryAt returns null on missing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)

              Expect.isNull (m.entryAt (3)) "Should have null entryAt"

          testCase "entryAt returns proper entry for existing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)
              let me = m.entryAt (1)

              Expect.equal (me.key ()) (upcast 1) "Should be the key"
              Expect.equal (me.value ()) (upcast "a") "Should be the value"


          testCase "valAt returns null on missing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)

              Expect.isNull (m.valAt (3)) "Should have null valAt"


          testCase "valAt returns value on existing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)

              Expect.equal (m.valAt (1)) (upcast "a") "Should have correct value"


          testCase "valAt2 returns notFound on missing  key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)

              Expect.equal (m.valAt (3, 99)) (upcast 99) "Should have not-found value"


          testCase "valAt2 returns value on existing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)

              Expect.equal (m.valAt (1, 99)) (upcast "a") "Should have correct value" ]

[<Tests>]
let basicSimpleHashMapPersistentCollectionTests =
    testList
        "Basic SimpleHashMap PersistentCollection tests"
        [

          testCase "count on empty is 0"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)
              let c = m.empty ()

              Expect.equal (c.count ()) 0 "Empty.count() = 0"

          testCase "count on non-empty returns count of entries"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)

              Expect.equal (m.count ()) 2 "Count of keys"

          testCase "seq on empty is null"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)
              let c = m.empty ()

              Expect.isNull (c.seq ()) "Seq on empty should be null"

          testCase "seq on non-empty iterates"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)
              let s = m.seq ()
              let me1 = s.first () :?> IMapEntry
              let me2 = s.next().first() :?> IMapEntry
              let last = s.next().next()

              Expect.equal (s.count ()) 2 "COunt of seq should be # of entries in map"
              Expect.equal (me1.value ()) (m.valAt (me1.key ())) "K/V pair should match map"
              Expect.equal (me2.value ()) (m.valAt (me2.key ())) "K/V pair should match map"
              Expect.notEqual (me1.key ()) (me2.key ()) "Should see different keys"
              Expect.isNull last "end of seq should be null" ]

[<Tests>]
let basicSimpleHashMapPersistentMapTests =
    testList
        "Basic SimpleHashMap tests"
        [

          testCase "assoc modifies value for existing key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m1 = SimpleHashMap.create (d)
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

              let m1 = SimpleHashMap.create (d)
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

              let m1 = SimpleHashMap.create (d)
              let f () = m1.assocEx (2, "c") |> ignore

              Expect.throwsT<InvalidOperationException> f "AssocEx throws on existing key"

          testCase "assocEx adds on new key"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m1 = SimpleHashMap.create (d)
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

              let m1 = SimpleHashMap.create (d)
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

              let m1 = SimpleHashMap.create (d)
              let m2 = m1.without (4)

              Expect.isTrue (m1 = m2) "No change"

          testCase "wihout on all keys yields empty map"
          <| fun _ ->

              let d: Dictionary<int, string> = Dictionary()
              d.[3] <- "a"
              d.[5] <- "b"
              d.[7] <- "c"

              let m1 = SimpleHashMap.create (d)
              let m2 = m1.without(3).without(5).without(7)

              Expect.equal (m2.count ()) 0 "Should be no entries remaining" ]

[<Tests>]
let aPersistentMapTests =
    testList
        "APersistentMap tests for SimpleHashMap"
        [

          testCase "Equiv on similar dictionary"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)

              Expect.isTrue (m.equiv (d)) "Equal on same dictionary"

          testCase "Equiv on different entry dictionary is false"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)
              d.[2] <- "c"

              Expect.isFalse (m.equiv (d)) "Equal on different dictionary"

          testCase "Equiv on extra entry dictionary is false"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = SimpleHashMap.create (d)
              d.[3] <- "c"

              Expect.isFalse (m.equiv (d)) "Equal on different dictionary"

          testCase "Hashcode based on value"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let m1 = SimpleHashMap.create (d)
              d.[2] <- "c"


              let m2 = SimpleHashMap.create (d)

              Expect.notEqual (m1.GetHashCode()) (m2.GetHashCode()) "Hash codes should differ"

          testCase "Associative.assoc works"
          <| fun _ ->
              let d: Dictionary<int, string> = Dictionary()
              d.[1] <- "a"
              d.[2] <- "b"

              let a = SimpleHashMap.create (d) :> Associative
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

              let m = SimpleHashMap.create (d)
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

              let m = SimpleHashMap.create (d)
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

              let m = SimpleHashMap.create (d)
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

              let m = SimpleHashMap.create (d)
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

              let m = SimpleHashMap.create (d)
              let c = m.cons (KeyValuePair(3, "c"))

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

              let m = SimpleHashMap.create (d)
              let c = m.cons (KeyValuePair(2, "c"))

              Expect.equal (m.count ()) 2 "Original has unchanged count"
              Expect.equal (m.valAt (1)) ("a" :> obj) "Original unchanged on untouched key"
              Expect.equal (m.valAt (2)) ("b" :> obj) "Original unchanged on untouched key"

              Expect.equal (c.count ()) 2 "Updated has higher count"
              Expect.equal (c.valAt (1)) ("a" :> obj) "Updated unchanged on untouched key"
              Expect.equal (c.valAt (2)) ("c" :> obj) "Updated has new value"

          testCase "cons on SimpleHashMap adds/repalces many"
          <| fun _ ->
              let d1: Dictionary<int, string> = Dictionary()
              d1.[1] <- "a"
              d1.[2] <- "b"

              let m1 = SimpleHashMap.create (d1)

              let d2: Dictionary<int, string> = Dictionary()
              d2.[2] <- "c"
              d2.[3] <- "d"

              let m2 = SimpleHashMap.create (d2)
              let m3 = m1.cons (m2)

              Expect.equal (m1.count ()) 2 "Original should have same count"
              Expect.equal (m2.count ()) 2 "Updater should have same count"
              Expect.equal (m3.count ()) 3 "Updated should have new count"

              Expect.equal (m1.valAt (1)) ("a" :> obj) "Original should be unchanged"
              Expect.equal (m1.valAt (2)) ("b" :> obj) "Original should be unchanged"
              Expect.isFalse (m1.containsKey (3)) "Original should be unchanged"

              Expect.equal (m3.valAt (1)) ("a" :> obj) "Updated should be unchanged on untouched key"
              Expect.equal (m3.valAt (2)) ("c" :> obj) "Updated should have updated key value"
              Expect.equal (m3.valAt (3)) ("d" :> obj) "Updated should have new key value" ]

type CollisionKey(id, factor) =
    override this.GetHashCode() = id % factor

    override this.Equals(o) =
        match o with
        | :? CollisionKey as ck -> ck.Id = this.Id
        | _ -> false

    member _.Id = id
    member _.Factor = factor

let testCollisions (numEntries: int) (numHashCodes: int): unit =

    // create map with entries  key=CollisionKey(i,_), value = i

    let mutable m = SimpleHashMap.Empty :> IPersistentMap
    for i = 0 to numEntries - 1 do
        m <- m.assoc (CollisionKey(i, numHashCodes), i)

    // Basic
    Expect.equal (m.count ()) numEntries "Should have the number of entries entered in the loop"

    // Check we have all the correct entries, but ggrabbing all the keys in the table
    // putting them in an array, sorting the array and making sure that the i-th entry = i.
    // This checks assoc and seq

    let a: int [] = Array.zeroCreate (m.count ())

    let rec step (i: int) (s: ISeq) =
        if not (isNull s) then
            let kv = s.first () :?> IMapEntry
            printfn "i = %i, k=%i" i (kv.key () :?> CollisionKey).Id
            a.[i] <- (kv.key () :?> CollisionKey).Id
            step (i + 1) (s.next ())

    step 0 (m.seq ())

    let b = Array.sort (a)
    for i = 0 to b.Length - 1 do
        Expect.equal b.[i] i "Key I"




    // check key enumerator

    let a: int [] = Array.zeroCreate (m.count ())
    let imek = (m :?> IMapEnumerable).keyEnumerator()

    let rec step (i: int) =
        if imek.MoveNext() then
            a.[i] <- (imek.Current :?> CollisionKey).Id
            step (i + 1)

    step 0

    let b = Array.sort (a)
    for i = 0 to b.Length - 1 do
        Expect.equal b.[i] i "Key I"

    // check value enumerator

    let a: int [] = Array.zeroCreate (m.count ())
    let imek = (m :?> IMapEnumerable).valEnumerator()

    let rec step (i: int) =
        if imek.MoveNext() then
            a.[i] <- (imek.Current :?> int)
            step (i + 1)

    step 0

    let b = Array.sort (a)
    for i = 0 to b.Length - 1 do
        Expect.equal b.[i] i "Val I"

    // Regular IEnumerable

    let a: int [] = Array.zeroCreate (m.count ())
    let mutable i = 0

    for kv in (m :> IEnumerable) do
        let id =
            ((kv :?> IMapEntry).key() :?> CollisionKey).Id
        a.[i] <- id
        i <- i + 1

    let b = Array.sort (a)
    for i = 0 to b.Length - 1 do
        Expect.equal b.[i] i "Key I"

    // IEnumerable<IMapEntry>

    let a: int [] = Array.zeroCreate (m.count ())
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

    let key =
        CollisionKey(numEntries + 1000, numHashCodes)

    let v = m.valAt (key)
    Expect.isNull v "Should not find value for key not in map"
    let kv = m.entryAt (key)
    Expect.isNull kv "Should not entry for key not in map"






[<Tests>]
let simpleHashMapCollisionTests =
    testList
        "SimpleHashMap collision tests"
        [ testCase "Collisions n m"
          <| fun _ ->
              testCollisions 100 10
              testCollisions 1000 100
              testCollisions 10000 100 ]





let doBigTest (numEntries: int) =
    printfn "Testing %i items." numEntries

    let rnd = Random()
    let dict = Dictionary<int, int>(numEntries)
    for i = 0 to numEntries - 1 do
        let r = rnd.Next()
        dict.[r] <- r

    let m = SimpleHashMap.create (dict)

    Expect.equal (m.count ()) (dict.Count) "Should have same number of entries"

    for key in dict.Keys do
        if not (m.containsKey (key)) then Console.WriteLine("HERE!")

        Expect.isTrue (m.containsKey (key)) "dictionary key should be in map"
        Expect.equal (m.valAt (key)) (upcast key) "Value should be same as key"

    let mutable s = m.seq ()

    while not (isNull s) do
        let entry = s.first () :?> IMapEntry
        Expect.isTrue (dict.ContainsKey(entry.key () :?> int)) "map key shoudl be in dictionary"
        s <- s.next ()

[<Tests>]
let bigSimpleHashMapTests =
    testList
        "big insertions into SimpleHashMap"
        [

          testCase "test for 100" <| fun _ -> doBigTest 100

          testCase "test for 1000"
          <| fun _ -> doBigTest 1000

          testCase "test for 2000"
          <| fun _ -> doBigTest 2000

          testCase "test for 3000"
          <| fun _ -> doBigTest 3000

          testCase "test for 4000"
          <| fun _ -> doBigTest 4000

          testCase "test for 5000"
          <| fun _ -> doBigTest 5000

          testCase "test for 6000"
          <| fun _ -> doBigTest 6000

          testCase "test for 7000"
          <| fun _ -> doBigTest 7000

          testCase "test for 8000"
          <| fun _ -> doBigTest 8000

          testCase "test for 9000"
          <| fun _ -> doBigTest 9000

          testCase "test for 10000"
          <| fun _ -> doBigTest 10000 ]
