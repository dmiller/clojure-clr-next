module SimpleMapTests

open Expecto
open Clojure.Collections
open Clojure.Collections.Simple
open System
open System.Collections
open System.Collections.Generic

let makeSimpleMap(n: int) =
    let keys = seq { for c in 'a' .. 'z' -> box c } |> Seq.take n |> Seq.toList
    let vals = seq { for c in 'A' .. 'Z' -> box c } |> Seq.take n |> Seq.toList
    SimpleMap(keys, vals)

let tryBadCreate() =
    SimpleMap([1;2],[3]) |> ignore

[<Tests>]
let simpleMapTests =
    testList
        "SimpleMap tests"
        [

          testCase "SimpleMap.empty"
          <| fun _ ->
              let sm = makeSimpleMap 5
              let em = (sm :> IPersistentCollection).empty ()
              Expect.equal (em.count ()) 0 "Empty should have no elements"

          testCase "simpleMap.count"
          <| fun _ ->
              let sm5 = makeSimpleMap 5
              let sm0 = SimpleMap([], [])
              Expect.equal ((sm5 :> IPersistentCollection).count ()) 5 "size 5 map"
              Expect.equal ((sm0 :> IPersistentCollection).count ()) 0 "size 0 map"

          testCase "simpleMap.valAt(_)"
          <| fun _ ->
              let sm5 = makeSimpleMap 5
              let sm5Val = (sm5 :> ILookup).valAt ('b')
              let sm5Missing = (sm5 :> ILookup).valAt (99)
              Expect.equal sm5Val (box 'B') "found key has correct value"
              Expect.isNull sm5Missing "missing key gets null"

          testCase "simpleMap.valAt(_,_)"
          <| fun _ ->
              let sm5 = makeSimpleMap 5
              let sm5Val = (sm5 :> ILookup).valAt ('b', 2000)
              let sm5Missing = (sm5 :> ILookup).valAt (99, 2000)
              Expect.equal sm5Val (box 'B') "found key has correct value"
              Expect.equal sm5Missing (box 2000) "missing key gets null"

          testCase "simpleMap.cons"
          <| fun _ ->
              let sm5 = makeSimpleMap 5

              let mod5 = (sm5 :> IPersistentMap).cons {Key ='b'; Value = "BBB"}

              let sm5Val = (sm5 :> ILookup).valAt ('b')
              let modVal = (mod5 :> ILookup).valAt ('b')
              Expect.equal sm5Val (box 'B') "Original has correct value"
              Expect.equal modVal ("BBB" :> obj) "Modified has new value"

          testCase "simpleMap.equiv"
          <| fun _ ->
              let sm5a = makeSimpleMap 5
              let sm5b = makeSimpleMap 5
              let sm5c = (sm5b :> IPersistentMap).without ('b') // remove a key/val pai

              let sm5d = ((sm5b :> IPersistentMap).without ('b')).assoc ('b', 'B') // change order of key/val lists

              let sm5e = (sm5b :> IPersistentMap).assoc ('b', "BBB") // change value of a key

              let sm4 = makeSimpleMap 4
              let sm0 = makeSimpleMap 0
              Expect.isTrue ((sm5a :> IPersistentCollection).equiv (sm5a)) "Should be equiv to itself"
              Expect.isTrue ((sm5a :> IPersistentCollection).equiv (sm5b)) "Should be equiv to a duplicate"

              Expect.isFalse
                  ((sm5a :> IPersistentCollection).equiv (sm5c))
                  "Should not be equiv to one with missing value"

              Expect.isFalse
                  ((sm5a :> IPersistentCollection).equiv (sm4))
                  "Should not be equiv to one with missing value"

              Expect.isFalse
                  ((sm4 :> IPersistentCollection).equiv (sm5a))
                  "Should not be equiv to one with missing value"

              Expect.isTrue
                  ((sm5a :> IPersistentCollection).equiv (sm5d))
                  "Should be equiv to one with keys/vals in different order"

              Expect.isFalse
                  ((sm5a :> IPersistentCollection).equiv (sm5e))
                  "Should not be equiv to one with a differnt value"

              Expect.isFalse ((sm5a :> IPersistentCollection).equiv (sm0)) "Non-empty should not be equiv to empty"
              Expect.isFalse ((sm0 :> IPersistentCollection).equiv (sm5a)) "Empty should not be equiv to non-empty"
              Expect.isTrue ((sm0 :> IPersistentCollection).equiv (sm0)) "SEmpty ould be equiv to itself" 
              
          testCase "Throws exception on unequal size keys/vals lists"
          <| fun _ ->
                Expect.throwsT<ArgumentException>  tryBadCreate "unequal-sized keys/vals lists should throw exception on create"

          testCase "without works anywhere in the list" 
          <| fun _ ->
              let sm5a = makeSimpleMap 5
              let sm4a = (sm5a :> IPersistentMap).without('a')
              let sm4b = (sm5a :> IPersistentMap).without('b')
              let sm4c = (sm5a :> IPersistentMap).without('c')
              let sm4d = (sm5a :> IPersistentMap).without('d')
              let sm4e = (sm5a :> IPersistentMap).without('e')

              Expect.isFalse (sm4a.containsKey('a')) "Should be able to remove first element"
              Expect.isFalse (sm4b.containsKey('b')) "Should be able to remove second element"
              Expect.isFalse (sm4c.containsKey('c')) "Should be able to remove third element"
              Expect.isFalse (sm4d.containsKey('d')) "Should be able to remove fourth element"
              Expect.isFalse (sm4e.containsKey('e')) "Should be able to remove last element"

          testCase "assoc works anywhere in the list" 
          <| fun _ ->
              let sm5 = makeSimpleMap 5
              let sm5a = (sm5 :> IPersistentMap).assoc('a',1)
              let sm5b = (sm5 :> IPersistentMap).assoc('b',2)
              let sm5c = (sm5 :> IPersistentMap).assoc('c',3)
              let sm5d = (sm5 :> IPersistentMap).assoc('d',4)
              let sm5e = (sm5 :> IPersistentMap).assoc('e',5)

              Expect.equal (sm5a.valAt('a')) 1 "Should be able to assoc first element"
              Expect.equal (sm5b.valAt('b')) 2 "Should be able to assoc second element"
              Expect.equal (sm5c.valAt('c')) 3 "Should be able to assoc third element"
              Expect.equal (sm5d.valAt('d')) 4 "Should be able to assoc fourth element"
              Expect.equal (sm5e.valAt('e')) 5 "Should be able to assoc last element"

          testCase "typed enumerator works"
          <| fun _ ->
              let sm5a = makeSimpleMap 5
              let e = (sm5a :> IEnumerable<IMapEntry>)
              let kvs = [ { Key='a'; Value='A'}; { Key='b'; Value='B'}; { Key='c'; Value='C'}; { Key='d'; Value='D'}; { Key='e'; Value='E'} ]

              for x in e do
                Expect.contains kvs (x:?>SimpleMapEntry) "Should contain"

          testCase "untyped enumerator works"
          <| fun _ ->
              let sm5a = makeSimpleMap 5
              let e = (sm5a :> IEnumerable)
              let kvs = [ { Key='a'; Value='A'}; { Key='b'; Value='B'}; { Key='c'; Value='C'}; { Key='d'; Value='D'}; { Key='e'; Value='E'} ]

              for x in e do
                Expect.contains kvs (x:?>SimpleMapEntry) "Should contain"
                
        ]
            
              