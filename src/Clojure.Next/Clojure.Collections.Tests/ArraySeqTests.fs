module ArraySeqTests

open Expecto
open Clojure.Collections
open Clojure.Numerics
open TestHelpers

let adder =
    { new AFn() with
        member this.ToString() = ""
      interface IFn with
        member this.invoke() = -100
        member this.invoke(x, y) = Numbers.addP (x, y)
    }

//let adderStopsShort n =
//    { new AFn() with
//        member this.ToString() = ""
//      interface IFn with
//        member this.invoke(x, y) =
//            if Numbers.gte (y, n :> obj) then
//                Reduced(x)
//            else
//                Numbers.add (x, y) 
//    }

[<Tests>]
let arraySeqConstructorTests =
    testList
        "ArraySeq constructor tests"
        [

          testCase "Create with no arg yields null"
          <| fun _ ->
                let s = ArraySeq.create()
                Expect.isNull s "No arg to create should yield null"

          testCase "Create with array yields non-null"
          <| fun _ ->
                let s = ArraySeq.create([| 1; 2; 3 |])
                Expect.isNotNull s "Array arg create should yield non-null"


          testCase "Create on null yields null"
          <| fun _ ->
                let s = ArraySeq.create(null)
                Expect.isNull s "No arg to create should yield null"

          testCase "Create with array has no meta"
          <| fun _ ->
                let s = ArraySeq.create([| 1; 2; 3 |])
                Expect.isNull ((s :> IObj).meta()) "Array arg create should have no metadata"

          testCase "Create with array and in-range index is not null"
          <| fun _ ->
                let s = ArraySeq.create([| 1; 2; 3 |], 1)
                Expect.isNotNull s "Array arg create should yield non-null"

          testCase "Create with array and high index is  null"
          <| fun _ ->
                let s = ArraySeq.create([| 1; 2; 3 |], 10)
                Expect.isNull s "Array arg create with bad index should yield null"

          testCase "Create with array and negative index is  null"
          <| fun _ ->
                let s = ArraySeq.create([| 1; 2; 3 |], -10)
                Expect.isNull s "Array arg create with bad index should yield null"

          testCase "Create with array and index  has no meta"
          <| fun _ ->
                let s = ArraySeq.create([| 1; 2; 3 |], 1)
                Expect.isNull ((s :> IObj).meta()) "Array arg create should have no metadata"

        ]

[<Tests>]
let arraySeqCollectionTests =
    testList
        "ArraySeq collection tests"
        [

          testCase "ArraySeq has correct count"
          <| fun _ ->
                let s = ArraySeq.create([| 1; 2; 3 |])
                Expect.equal ((s :> Counted).count()) 3 "Count should be 3"


          testCase "ArraySeq created with index has correct count"
          <| fun _ ->
                let s = ArraySeq.create([| 1; 2; 3 |] ,1)
                Expect.equal ((s :> Counted).count()) 2 "Count should be 2"

        ]


[<Tests>]
let arraySeqReduceTests =
    testList
        "ArraySeq reduce tests"
        [
          testCase "ArraySeq.reduce with no start value"
          <| fun _ ->
                let s = ArraySeq.create([| 2; 3; 4 |])
                let ret = (s :> IReduce).reduce(adder)
                Expect.equal (ret :?> int64) 9L "Reduce should be 9"


          testCase "ArraySeq.reduce with  start value"
          <| fun _ ->
                let s = ArraySeq.create([| 2; 3; 4 |])
                let ret = (s :> IReduceInit).reduce(adder,20)
                Expect.equal (ret :?> int64) 29L "Reduce should be 29"
        ]


[<Tests>]
let arraySeqISeqTests =
    testList
        "ArraySeq ISeq tests"
        [
          testCase "ArraySeq standard constructor has correct elements"
          <| fun _ ->
                let a0 = [| 2 :> obj; 3; 4 |]
                let s0 = ArraySeq.create(a0)
                verifyISeqContents s0 (a0 |> Array.toList)

          testCase "ArraySeq constructor with index has correct elements"
          <| fun _ ->
                let a0 = [| 2 :> obj; 3; 4 |]
                let s0 = ArraySeq.create(a0, 1)
                verifyISeqContents s0 (a0 |> Array.toList |> List.tail)

          testCase "ArraySeq conses"
          <| fun _ ->
                let a0 = [| 2 :> obj; 3; 4 |]
                let s0 = ArraySeq.create(a0)
                verifyISeqCons s0  12 (a0 |> Array.toList)

          testCase "ArraySeq with with index conses"
          <| fun _ ->
                let a0 = [| 2 :> obj; 3; 4 |]
                let s0 = ArraySeq.create(a0, 1)
                verifyISeqCons s0 12 (a0 |> Array.toList |> List.tail)

        ]


[<Tests>]
let arraySeqMetaTests =
    testList
        "ArraySeq IObj/IMeta tests"
        [
            testCase "IObj/IMeta work"
            <| fun _ ->
                let a = [| 2 :> obj; 3; 4 |]
                let s1 = ArraySeq.create(a)
                let s2 = s1.withMeta(null)

                verifyNullMeta s1
                verifyWithMetaHasCorrectMeta s2
                verifyWithMetaNoChange s1
                verifyWithMetaReturnType s1 typeof<ArraySeq_object>

        ]