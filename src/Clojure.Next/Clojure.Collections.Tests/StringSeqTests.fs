module StringSeqTests

open Expecto
open Clojure.Collections
open System
open TestHelpers



[<Tests>]
let stringSeqConstructorTests =
    testList
        "StringSeq constructor tests"
        [

          testCase "Create on empty string yields null"
          <| fun _ ->
                let s = StringSeq.create(String.Empty)
                Expect.isNull s "Empty string should yield null"

          testCase "Create on non-empty string yields StringSeq"
          <| fun _ ->
                let s = StringSeq.create("abcde")
                Expect.isNotNull s "non-empty string should yield non-null"
                Expect.equal (s.GetType()) (typeof<StringSeq>) "non-empty string should yield StringSeq"

          testCase "StringSeq.count is initial string length"
          <| fun _ ->
                let s = StringSeq.create("abcde")
                Expect.equal ((s :>Counted).count()) 5 "count should be string length"

          testCase "StringSeq initial index is zero"
          <| fun _ ->
                let s = StringSeq.create("abcde")
                Expect.equal ((s:>IndexedSeq).index()) 0 "initial index should be zero"

          testCase "StringSeq index of rest is one"
          <| fun _ ->
                let s = StringSeq.create("abcde")
                let is = (s :> ISeq).next() 
                Expect.equal ((is :?> IndexedSeq).index()) 1 "index should be one"

        ]


[<Tests>]
let stringSeqMetaTests =
    testList
        "StringSeq IObj/IMeta tests"
        [
            testCase "IObj/IMeta work"
            <| fun _ ->
                let s1 = StringSeq.create("abcde")
                let s2 = (s1:>IObj).withMeta(metaForSimpleTests)

                verifyNullMeta s1
                verifyWithMetaHasCorrectMeta s2
                verifyWithMetaNoChange s1
                verifyWithMetaReturnType s1 typeof<StringSeq>

        ]


[<Tests>]
let stringSeqISeqTests =
    testList
        "StringSeq ISeq tests"
        [
            testCase "StringSeq has correct ISeq values"
            <| fun _ ->
                verifyISeqContents (StringSeq.create("abcde")) [ 'a'; 'b'; 'c'; 'd'; 'e' ]

            testCase "StringSeq with meta has correct ISeq values"
            <| fun _ ->
                let s = StringSeq.create("abcde")
                let sm = (s:>IObj).withMeta(metaForSimpleTests) :?> StringSeq
                verifyISeqContents sm [ 'a'; 'b'; 'c'; 'd'; 'e' ]


            testCase "StringSeq.rest preserves meta"
            <| fun _ ->
                let s = StringSeq.create("abcde")
                let sm = (s:>IObj).withMeta(metaForSimpleTests) :?> StringSeq
                verifyIseqRestMaintainsMeta sm

            testCase "StringSeq.rest preserves type"
            <| fun _ ->
                let s = StringSeq.create("abcde")
                let sm = (s:>IObj).withMeta(metaForSimpleTests) :?> StringSeq
                verifyISeqRestTypes sm typeof<StringSeq>

            testCase "StringSeq cons works"
            <| fun _ ->
                verifyISeqCons (StringSeq.create("abcde")) 12 [ 'a'; 'b'; 'c'; 'd'; 'e' ]


        ]

