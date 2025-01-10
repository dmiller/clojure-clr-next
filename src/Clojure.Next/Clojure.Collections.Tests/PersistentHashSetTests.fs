module PersistentHashSetTests

open Expecto
open TestHelpers
open System.Collections
open Clojure.Collections
open System
open System.Collections.Generic


[<Tests>]
let basicPersistentHashSetCreateTests =
    testList
        "Basic PersistentHashSet create tests"
        [

          testCase "Create on empty list returns empty set"
          <| fun _ ->
              let a = ArrayList()

              let phs = PersistentHashSet.createWithCheck (a) :> IPersistentSet

              Expect.equal (phs.count ()) 0 "Empty set should have 0 count"


          testCase "Create on non-empty list returns non-empty set"
          <| fun _ ->
              let items: obj[] = [| 1; "a" |]
              let a = ArrayList(items)

              let phs = PersistentHashSet.createWithCheck (a) :> IPersistentSet

              Expect.equal (phs.count ()) 2 "Count should match # dict entries"
              Expect.isTrue (phs.contains (1)) "Should contain input element"
              Expect.isTrue (phs.contains ("a")) "Should contain input element"
              Expect.isFalse (phs.contains (3)) "Shouldn't contain some random key"


          testCase "Create on empty ISeq returns empty set"
          <| fun _ ->
              let items = [| |]
              let a = ArrayList(items)
              let s = PersistentList.create(a).seq()
              let phs = PersistentHashSet.createWithCheck(s) :> IPersistentSet

              Expect.equal (phs.count ()) 0 "Empty map should have 0 count"


          testCase "Create on ISeq returns  set"
          <| fun _ ->
              let items = [| 1 :> obj; "a" |]
              let a = ArrayList(items)
              let s = PersistentList.create(a).seq()
              let phs = PersistentHashSet.createWithCheck(s) :> IPersistentSet

              Expect.equal (phs.count ()) 2 "Count should match # dict entries"
              Expect.isTrue (phs.contains (1)) "Should contain input element"
              Expect.isTrue (phs.contains ("a")) "Should contain input element"
              Expect.isFalse (phs.contains (3)) "Shouldn't contain some random key"


          testCase "Create on no arg returns empty set"
          <| fun _ ->

              let phs = PersistentHashSet.createWithCheck() :> IPersistentSet

              Expect.equal (phs.count ()) 0 "Empty set should have 0 count"


          testCase "Create on param array returns non-empty set"
          <| fun _ ->
              let phs = PersistentHashSet.createWithCheck ( 1, "a") :> IPersistentSet

              Expect.equal (phs.count ()) 2 "Count should match # dict entries"
              Expect.isTrue (phs.contains (1)) "Should contain input element"
              Expect.isTrue (phs.contains ("a")) "Should contain input element"
              Expect.isFalse (phs.contains (3)) "Shouldn't contain some random key"

 

          testCase "createWithCheck with duplicate keys throws"
          <| fun _ ->
              
              Expect.throwsT<ArgumentException> (fun () -> PersistentHashSet.createWithCheck ( 1, "a",  1, "c")  |> ignore) "creeteCheck on param arg should throw on duplicate key"

              let items = [| 1 :> obj; "a" ; 1 ; "b"|]
              let a = ArrayList(items)
              let s = PersistentList.create(a).seq()

              Expect.throwsT<ArgumentException> (fun () -> PersistentHashSet.createWithCheck (s)  |> ignore) "creeteCheck on seq should throw on duplicate key"
              Expect.throwsT<ArgumentException> (fun () -> PersistentHashSet.createWithCheck (a)  |> ignore) "creeteCheck on ILIst should throw on duplicate key"

        ]

[<Tests>]
let PersistentHashSetIObjTests =
    testList
        "PersistentHashSet IObj tests"
        [

          testCase "Verify PersistentHashMap.IObj"
          <| fun _ ->

              let phs = PersistentHashSet.createWithCheck ("a", "b") :> IObj
              let phsm = phs.withMeta (metaForSimpleTests)

              verifyNullMeta phs
              verifyWithMetaHasCorrectMeta phsm
              verifyWithMetaNoChange phs
              verifyWithMetaReturnType phs typeof<PersistentHashSet> ]


// TODO: big tests? 
