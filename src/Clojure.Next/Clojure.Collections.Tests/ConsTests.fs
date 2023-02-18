module ConsTests

open Expecto
open System
open System.Collections
open Clojure.Collections
open TestHelpers


let makeConsChain (n: int) =
    let rec loop c i =
        if i = n then c else loop (Cons(i, c)) (i + 1)

    loop (Cons(0, null)) 1


[<Tests>]
let consTests =
    testList
        "ConsTests"
        [

          testCase "No-meta ctor has no meta"
          <| fun _ ->
              let c = Cons("abc", null)
              let m = (c :> IMeta).meta ()
              Expect.isNull m "Meta should be null"

          // defer until maps are available
          //testCase "Ctor w/ meta has meta"
          //<| fun _ ->
          //    let m = SimpleMap.makeSimpleMap 3
          //    let c = Cons(m, "abc", null)
          //    Expect.isTrue (Object.ReferenceEquals((c :> IMeta).meta(), m)) "Should get back same meta as put in"

          testCase "Cons.count works"
          <| fun _ ->
              for i = 1 to 5 do
                  Expect.equal ((makeConsChain i :> IPersistentCollection).count ()) i "Count value"

          testCase "Cons.seq returns self"
          <| fun _ ->
              let c = makeConsChain 3
              Expect.isTrue (Object.ReferenceEquals((c :> ISeq).seq (), c)) "Should be self"


          testCase "Cons.empty is empty"
          <| fun _ ->
              let c = makeConsChain 3
              let e = (c :> IPersistentCollection).empty ()
              Expect.equal (e.GetType()) (PersistentList.Empty.GetType()) "Empty should be an EmptyList"

          testCase "Cons.Equals"
          <| fun _ ->
              let c1 = makeConsChain 5
              let c2 = makeConsChain 5
              let c3 = makeConsChain 4
              let c4 = Cons(10, Cons(12, makeConsChain 4))
              let c5 = Cons(10, Cons(11, makeConsChain 4))
              Expect.isTrue (c1.Equals(c1)) "should equal itself"
              Expect.isTrue (c1.Equals(c2)) "should equal same sequence"
              Expect.isFalse (c1.Equals(c3)) "should not equal smaller sequence"
              Expect.isFalse (c3.Equals(c1)) "should not equal larger sequence"
              Expect.isFalse (c4.Equals(c5)) "should not equal sequence with non-matching entry"

          testCase "Cons.GetHashCode"
          <| fun _ ->
              let c1 = makeConsChain 5
              let c2 = makeConsChain 5
              let c3 = makeConsChain 4
              let c4 = Cons(3, Cons(4, makeConsChain 3))

              Expect.equal (c1.GetHashCode()) (c2.GetHashCode()) "Hash is on value"
              Expect.notEqual (c1.GetHashCode()) (c3.GetHashCode()) "Hash depends on content"
              Expect.notEqual (c1.GetHashCode()) (c4.GetHashCode()) "Hash depends on order" ]

// THese are really tests of ASeq functionality, using Cons as the testbed

[<Tests>]
let aseqTests =
    testList
        "ASeqTests"
        [

          testCase "ASeq ICollection.CopyTo"
          <| fun _ ->
              let ic = makeConsChain 5 :> ICollection


              let a = Array.zeroCreate<obj> 10
              let b = Array.zeroCreate<obj> 10
              ic.CopyTo(a, 0)
              ic.CopyTo(b, 2)

              let fcopynull () = ic.CopyTo(null, 0)
              let fcopysmall () = ic.CopyTo(Array.zeroCreate<obj> 2, 0)
              let fcopytoofar () = ic.CopyTo(Array.zeroCreate<obj> 10, 8)
              let fcopy2dim () = ic.CopyTo(Array2D.zeroCreate 8 8, 0)
              let fcopybadindex () = ic.CopyTo(Array.zeroCreate<obj> 10, -1)

              for i = 0 to 4 do
                  Expect.equal a.[i] (box (4 - i)) "Items copied"

              for i = 2 to 6 do
                  Expect.equal b.[i] (box (6 - i)) "Items copied at offset"

              Expect.throwsT<ArgumentNullException> fcopynull "Fails on copy to null array"
              Expect.throwsT<InvalidOperationException> fcopysmall "Fails on copy to too-small array"
              Expect.throwsT<InvalidOperationException> fcopytoofar "Fails on copy to too-small array"
              Expect.throwsT<ArgumentException> fcopy2dim "Fails on copy to multi-dim array"
              Expect.throwsT<ArgumentOutOfRangeException> fcopybadindex "Fails on copy with negative index"

          testCase "ASeq ICollection miscellaneous"
          <| fun _ ->
              let ic = makeConsChain 3 :> ICollection

              Expect.equal (ic.Count) 3 "Proper count"
              Expect.isTrue (ic.IsSynchronized) "should be synchronized"
              Expect.isTrue (Object.ReferenceEquals(ic, ic.SyncRoot)) "Sync root should be self"

          testCase "ASeq.GetEnumerator()"
          <| fun _ ->
              let ic = makeConsChain 3 :> ICollection

              let e = ic.GetEnumerator()
              Expect.isTrue (e.MoveNext()) "first move"
              Expect.equal (e.Current) (box 2) "first element"
              Expect.isTrue (e.MoveNext()) "second move"
              Expect.equal (e.Current) (box 1) "second element"
              Expect.isTrue (e.MoveNext()) "third move"
              Expect.equal (e.Current) (box 0) "third element"
              Expect.isFalse (e.MoveNext()) "last move should return false" ]

// We do some basic tests on SeqIterator using a Cons chain as the base.

let testThreeElementSeqEnumerator (e: IEnumerator) =

    let f () = e.Current |> ignore

    Expect.isTrue (e.MoveNext()) "first move"
    Expect.equal (e.Current) (box 2) "first element"
    Expect.isTrue (e.MoveNext()) "second move"
    Expect.equal (e.Current) (box 1) "second element"
    Expect.equal (e.Current) (box 1) "repeated current should get same element"
    Expect.isTrue (e.MoveNext()) "third move"
    Expect.equal (e.Current) (box 0) "third element"
    Expect.isFalse (e.MoveNext()) "last move should return false"
    Expect.throwsT<InvalidOperationException> f "Current on null should throw"


[<Tests>]
let seqIteratorTests =
    testList
        "SeqIteratorTests"
        [

          testCase "SeqIterator on null"
          <| fun _ ->
              let e = new SeqEnumerator(null) :> IEnumerator
              Expect.isFalse (e.MoveNext()) "Cannot MoveNext on null"

          testCase "SeqIterator on null has no current"
          <| fun _ ->
              let e = new SeqEnumerator(null) :> IEnumerator
              let f () = e.Current |> ignore
              Expect.throwsT<InvalidOperationException> f "Current on null should throw"

          testCase "SeqIterator iterates"
          <| fun _ ->
              let e = new SeqEnumerator(makeConsChain 3) :> IEnumerator

              testThreeElementSeqEnumerator e

          testCase "SeqIterator Reset fails"
          <| fun _ ->
              let e = new SeqEnumerator(makeConsChain 3) :> IEnumerator

              Expect.throwsT<NotSupportedException> (fun _ -> e.Reset()) "Reset not supported"

          ]


[<Tests>]
let consISeqTests =
    testList
        "cons.seq tests"
        [

          testCase "cons.seq has correct values"
          <| fun _ ->
              let c = makeConsChain 4
              let vals: obj list = [ 3; 2; 1; 0 ]
              verifyISeqContents c vals

          testCase "cons on cons.seq has correct values"
          <| fun _ ->
              let c = makeConsChain 4
              let d = (c :> ISeq).cons ("a")

              let vals: obj list = [ 3; 2; 1; 0 ]
              verifyISeqCons c "a" vals ]

// Defer until maps are available

//[<Tests>]
//let consIObjTests =
//    testList
//        "cons.iobj tests"
//        [

//        testCase "cons.iobj properties"
//        <| fun _ ->
//            let c1 = makeConsChain 4
//            let c2 = Cons(metaForSimpleTests, c1)


//            verifyNullMeta c1
//            verifyWithMetaHasCorrectMeta c1
//            verifyWithMetaReturnType c1 typeof<Cons>
//            verifyWithMetaNoChange c1 ]
