module PersistentTreeMapTests

open Expecto
open TestHelpers
open System.Collections
open System.Collections.Generic
open Clojure.Collections
open System


[<Tests>]
let basicPersistentTreeMapCreateTests =
    testList
        "Basic PersistentTreeMap create tests"
        [

          testCase "Create on empty dictionary returns empty map"
          <| fun _ ->  
            let d = Dictionary<int,string>()
            let m = PersistentTreeMap.Create(d)
            Expect.equal (m.count()) 0 "Empty map count should be 0"

          testCase "Create on simple dictionary returns a simple map"
          <| fun _ ->  
              let d = Dictionary<int,string>()
              d.[1] <- "a"
              d.[2] <- "b"

              let m = PersistentTreeMap.Create (d)

              Expect.equal (m.count ()) 2 "Count should match # dict entries"
              Expect.equal (m.valAt (1)) (upcast "a") "m[1]=a"
              Expect.equal (m.valAt (2)) (upcast "b") "m[2]=b"
              Expect.isTrue (m.containsKey (1)) "Check containsKey"
              Expect.isFalse (m.containsKey (3)) "Shouldn't contain some random key" 

          testCase "Create on empty ISeq returns empty map"
          <| fun _ ->  
                let a = ArrayList()
                let s = PersistentList.create(a).seq()
                let m = PersistentTreeMap.Create(s) :> IPersistentMap

                Expect.equal (m.count()) 0 "Empty map count should be 0"


          testCase "Create on simple ISeq returns a simple map"
          <| fun _ ->  
                let a = ArrayList([| 1 :> obj; "a"; 2 ; "b"|])
                let s = PersistentList.create(a).seq()
                let m = PersistentTreeMap.Create(s) :> IPersistentMap

                let m = PersistentTreeMap.Create (s) :> IPersistentMap

                Expect.equal (m.count ()) 2 "Count should match # dict entries"
                Expect.equal (m.valAt (1)) (upcast "a") "m[1]=a"
                Expect.equal (m.valAt (2)) (upcast "b") "m[2]=b"
                Expect.isTrue (m.containsKey (1)) "Check containsKey"
                Expect.isFalse (m.containsKey (3)) "Shouldn't contain some random key" 

          testCase "Default constructor returns empty map"
          <| fun _ ->  
                let m = PersistentTreeMap()

                Expect.equal ((m :> IPersistentMap).count()) 0 "Empty map count should be 0"
                Expect.isNull ((m :> IObj).meta()) "Empty map should have no meta"

        ]

        // TODO: tests on Associative, IPersistentMap, IPersistentCollection, ISeq/Sequable, Enumerators


let DoBigTest(numEntries: int) =

    Console.WriteLine($"Testing {numEntries} items for PersistentHashSet.")

    let rnd = Random()

    let dict = Dictionary<int, int>()
    for i in 0 .. numEntries - 1 do
        let key = rnd.Next()
        dict[key] <- key

    let m = PersistentTreeMap.Create(dict)

    Expect.equal (m.count()) (dict.Count) "Count should match # dict entries"

    for key in dict.Keys do
        Expect.isTrue (m.containsKey(key))  "Key should be in map"
        Expect.equal (m.valAt(key)) (upcast key) "Value should match key"

    let rec loop (s:ISeq) (count:int) =
        match s with
        | null -> count
        | _ -> 
            let entry = s.first() :?> IMapEntry
            Expect.isTrue (m.containsKey(entry.key()))  "Key should be in map"
            Expect.equal (m.valAt(entry.key())) (entry.key()) "Value should match key"
            loop (s.next()) (count+1)
    let count = loop (m.seq()) 0
    Expect.equal count (dict.Count) "Seq should see all the elements"



[<Tests>]
let PersistentTreeMapBigTests =
    testList
        "Basic PersistentTreeMap create tests"
        [         
          testCase "Do some big tests"
          <| fun _ ->  
                DoBigTest(100)
                DoBigTest(1000)
                DoBigTest(10000)
                DoBigTest(100000)

        ]
