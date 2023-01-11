module FnTests

open Expecto
open Clojure.Collections
open Clojure.Collections.Simple


[<Tests>]
let boundedLengthTests =
    testList
        "AFn.boundedLength"
        [

          testCase "boundedLength on range, limit hit"
          <| fun _ ->
              let r = SimpleRange(0, 9) :> ISeq
              Expect.equal (AFn.boundedLength (r, 5)) 6 "Over limit should be bound+1"

          testCase "boundedLength on conses, limit hit"
          <| fun _ ->
              let c = SimpleCons.makeConsSeq 9
              Expect.equal (AFn.boundedLength (c, 5)) 6 "Over limit should be bound+1"

          testCase "boundedLength on range, seq end hit"
          <| fun _ ->
              let r = SimpleRange(0, 9) :> ISeq
              Expect.equal (AFn.boundedLength (r, 20)) (r.count ()) "under limit on range should be seq count"

          testCase "boundedLength on conses, seq end hit"
          <| fun _ ->
              let c = SimpleCons.makeConsSeq 2

              Expect.equal (AFn.boundedLength (c, 20)) (c.count ()) "under limit on conses should be seq count"

          testCase "boundedLength on null should be 0"
          <| fun _ -> Expect.equal (AFn.boundedLength (null, 20)) 0 "under limit on range should be seq count" ]


[<Tests>]
let seqLengthTests =
    testList
        "AFn.seqLength"
        [

          testCase "seqLength on range"
          <| fun _ ->
              let r = SimpleRange(0, 9) :> ISeq
              Expect.equal (AFn.seqLength r) 10 "seqLength should compute actual length"

          testCase "seqLength on conses"
          <| fun _ ->
              let c = SimpleCons.makeConsSeq 9
              Expect.equal (AFn.seqLength c) 9 "seqLength should compute actual length"


          testCase "seqLength on null should be 0"
          <| fun _ -> Expect.equal (AFn.seqLength null) 0 "seqLength should be 0 on null" ]

[<Tests>]
let seqToArrayTests =
    testList
        "AFn.seqToArray"
        [

          testCase "seqToArray on null returns 0-length array"
          <| fun _ -> Expect.equal (null |> AFn.seqToArray<obj> |> Array.length) 0 "Length should be 0"

          testCase "seqToArray on cons should return array with proper elements"
          <| fun _ ->
              let a =
                  SimpleCons.makeConsSeq 3 |> AFn.seqToArray<int>

              Expect.equal (Array.length a) 3 "Should have proper number of elements"
              Expect.equal a.[0] 0 "0-th element should be 0"
              Expect.equal a.[1] 1 "1-th element should be 1"
              Expect.equal a.[2] 2 "2-th element should be 2" ]


type TestFn() =
    inherit AFn()

    interface IFn with
        override x.invoke() = upcast "Zero"
        override x.invoke(arg1) = (unbox arg1) + 1 :> obj
        override x.invoke(arg1, arg2) = (unbox arg1) + (unbox arg2) :> obj

    interface IFnArity with
        override x.hasArity(n: int) =
            match n with
            | 0
            | 1
            | 2 -> true
            | _ -> false

let tf = TestFn()

[<Tests>]
let basicAFnTests =
    testList
        "AFn tests"
        [

          testCase "call invoke()"
          <| fun _ ->
              let result: string = downcast (tf :> IFn).invoke()
              Expect.equal result "Zero" "Call with no args"

          testCase "call invoke(a)"
          <| fun _ ->
              let i = 12
              let result: int = downcast (tf :> IFn).invoke(i)
              Expect.equal result (i + 1) "call with one arg should add 1"

          testCase "call invoke(a,b)"
          <| fun _ ->
              let i = 12
              let j = 30
              let result: int = downcast (tf :> IFn).invoke(i, j)
              Expect.equal result (i + j) "call with two args should add them"

          testCase "call invoke with too many args fails"
          <| fun _ ->
              let f () = (tf :> IFn).invoke(1, 2, 3, 4) |> ignore

              Expect.throwsT<ArityException> f "Does not accept four arguments"

          testCase "check hasArity"
          <| fun _ ->
              Expect.isFalse ((tf :> IFnArity).hasArity 3) "Does not have this arity"
              Expect.isTrue ((tf :> IFnArity).hasArity 0) "has this arity"
              Expect.isTrue ((tf :> IFnArity).hasArity 1) "has this arity"
              Expect.isTrue ((tf :> IFnArity).hasArity 2) "has this arity"

          testCase "applyToHelper works with null"
          <| fun _ ->
              let result: string = downcast AFn.applyToHelper (tf, null)
              Expect.equal result "Zero" "call with no args"

          testCase "applyToHelper works with non-empty sequence"
          <| fun _ ->
              let result: int =
                  downcast AFn.applyToHelper (tf, SimpleRange(10, 11))

              Expect.equal result 21 "call with no args"

          testCase "applyToHelper throws with too-long sequence"
          <| fun _ ->
              let f () =
                  AFn.applyToHelper (tf, SimpleRange(1, 5))
                  |> ignore

              Expect.throwsT<ArityException> f "too many args should throw" ]
