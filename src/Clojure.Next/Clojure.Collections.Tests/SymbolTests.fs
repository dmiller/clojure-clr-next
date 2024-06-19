module SymbolTests

open Expecto
open Clojure.Collections
open System.Collections
open System
open TestHelpers

[<Tests>]
let SymbolTests =
    testList
        "Symbol tests"
        [
            testCase "Intern2 creates symbol with no namespace or meta"
            <| fun _ ->
                let sym = Symbol.intern(null,"abc")
                Expect.equal sym.Name  "abc" "Name should be abc"
                Expect.isNull sym.Namespace "Namespace should be null"
                Expect.isNull ((sym:>IMeta).meta()) "Meta should be null"

            testCase "Intern1 creates symbol with namespace but not meta"
            <| fun _ ->
                let sym = Symbol.intern("abc/def")
                Expect.equal sym.Name  "def" "Name should be def"
                Expect.equal sym.Namespace "abc" "Namespace should be abc"
                Expect.isNull ((sym:>IMeta).meta()) "Meta should be null"


            testCase "Symbol.ToString() with no namespace should be just name"
            <| fun _ ->
                let sym = Symbol.intern("abc")
                Expect.equal (sym.ToString()) "abc" "ToString should be abc"

            testCase "Symbol.ToString() with  namespace should be just namespace/name"
            <| fun _ ->
                let sym = Symbol.intern("def", "abc")
                Expect.equal (sym.ToString()) "def/abc" "ToString should be def/abc"

            testCase "Equals on identity is true"
            <| fun _ ->
                let sym = Symbol.intern("def", "abc")
                Expect.isTrue (sym.Equals(sym)) "Should be equal"

            testCase "Equals on non-symbol is false"
            <| fun _ ->
                let sym = Symbol.intern("def", "abc")
                Expect.isFalse (sym.Equals("abc")) "Should not be equal"

            testCase "Equals on different symbol is false"
            <| fun _ ->
                let sym1 = Symbol.intern("abc")
                let sym2 = Symbol.intern("ab")
                let sym3 = Symbol.intern("def", "abc")
                let sym4 = Symbol.intern("de", "abc")
                Expect.isFalse (sym1.Equals(sym2)) "Should not be equal"
                Expect.isFalse (sym1.Equals(sym3)) "Should not be equal"
                Expect.isFalse (sym1.Equals(sym4)) "Should not be equal"
                Expect.isFalse (sym3.Equals(sym4)) "Should not be equal"

            testCase "Hashcode depends on names"
            <| fun _ ->
                let sym1 = Symbol.intern("abc")
                let sym2 = Symbol.intern("abc")
                let sym3 = Symbol.intern("def", "abc")
                let sym4 = Symbol.intern("def", "abc")
                let sym5 = Symbol.intern("ab")
                let sym6 = Symbol.intern("de", "abc")
                Expect.equal (sym1.GetHashCode()) (sym2.GetHashCode()) "Should be equal"
                Expect.equal (sym3.GetHashCode()) (sym4.GetHashCode()) "Should be equal"
                Expect.notEqual (sym1.GetHashCode()) (sym3.GetHashCode()) "Should not be equal"
                Expect.notEqual (sym1.GetHashCode()) (sym5.GetHashCode()) "Should not be equal"
                Expect.notEqual (sym3.GetHashCode()) (sym6.GetHashCode()) "Should not be equal"

            testCase "Named interface works"
            <| fun _ ->
                let named1 = Symbol.intern("abc") :> Named
                let named2 = Symbol.intern("def", "abc") :> Named
                Expect.isNull (named1.getNamespace()) "Namespace should be null"
                Expect.equal (named1.getName()) "abc" "Name should be abc"
                Expect.equal (named2.getNamespace()) "def" "Namespace should be def"
                Expect.equal (named2.getName()) "abc" "Name should be abc"


            testCase "Invoke2 indexes on its first arg"
            <| fun _ ->
                let sym1 = Symbol.intern("abc")
                let sym2 = Symbol.intern("abc")
                let sym3 = Symbol.intern("ab")

                let  dict : IDictionary = new Hashtable()
                dict[sym1] <- 7
                dict["abc"] <- 8
                dict["ab"] <- 9

                Expect.equal ((sym1:>IFn).invoke(dict)) 7 "Find sym1 in dictionary"
                Expect.equal ((sym2:>IFn).invoke(dict)) 7 "Find sym with same name in dictionary"
                Expect.isNull ((sym3:>IFn).invoke(dict)) "Dont find absent key"


            testCase "Invoke3 indexes on its first arg, returns notFound value if absent"
            <| fun _ ->
                let sym1 = Symbol.intern("abc")
                let sym2 = Symbol.intern("abc")
                let sym3 = Symbol.intern("ab")

                let  dict : IDictionary = new Hashtable()
                dict[sym1] <- 7
                dict["abc"] <- 8
                dict["ab"] <- 9

                Expect.equal ((sym1:>IFn).invoke(dict,20)) 7 "Find sym1 in dictionary"
                Expect.equal ((sym2:>IFn).invoke(dict,20)) 7 "Find sym with same name in dictionary"
                Expect.equal ((sym3:>IFn).invoke(dict,20)) 20 "Return notfound value for absent key"

            testCase "Invoke on wrong number of args fails"
            <| fun _ ->
                let sym1 = Symbol.intern("abc")
                let f1() = (sym1:>IFn).invoke() |> ignore
                let f2() = (sym1:>IFn).invoke(1,2,3) |> ignore
                Expect.throwsT<ArityException> f1 "Should throw on invoke()"
                Expect.throwsT<ArityException> f2 "Should throw on invoke(1,2,3)"


            testCase "CompareTo on non-symbol fails"
            <| fun _ ->
                let sym1 = Symbol.intern("abc")
                let f1() = (sym1:>IComparable).CompareTo("abc") |> ignore
                Expect.throwsT<ArgumentException> f1 "Should throw on CompareTo(\"abc\")"

            testCase "CompareTo equal symbol should be 0"
            <| fun _ ->
                let sym1 = Symbol.intern("abc")
                let sym2 = Symbol.intern("abc")
                Expect.equal ((sym1:>IComparable).CompareTo(sym2)) 0 "Should be 0"

            testCase "CompareTo different symbol should be -1 or 1"
            <| fun _ ->
                let sym1 = Symbol.intern("abc")
                let sym2 = Symbol.intern("ab")
                let sym3 = Symbol.intern("def", "abc")
                let sym4 = Symbol.intern("de", "abc")
                Expect.equal ((sym1:>IComparable).CompareTo(sym2)) 1 "Should be 1"
                Expect.equal ((sym1:>IComparable).CompareTo(sym3)) -1 "Should be -1"
                Expect.equal ((sym1:>IComparable).CompareTo(sym4)) -1 "Should be -1"
                Expect.equal ((sym3:>IComparable).CompareTo(sym4)) 1 "Should be 1"

            testCase "CompareTo: Null NS less than non-null NS"
            <| fun _ ->
                let sym1 = Symbol.intern("abc")
                let sym2 = Symbol.intern("def", "abc")
                Expect.equal ((sym1:>IComparable).CompareTo(sym2)) -1 "Should be -1"

            testCase "CompareTo: dissimilar namespace compare on namespace"
            <| fun _ ->
                let sym1 = Symbol.intern("a", "abc")
                let sym2 = Symbol.intern("b", "abc")
                Expect.equal ((sym1:>IComparable).CompareTo(sym2)) -1 "Should be -1"

            testCase "IObj/IMeta work"
            <| fun _ ->
                let sym1 = Symbol.intern("abc")
                let sym2 = (sym1:>IObj).withMeta(metaForSimpleTests)

                verifyNullMeta sym1
                verifyWithMetaHasCorrectMeta sym2
                verifyWithMetaNoChange sym1
                verifyWithMetaReturnType sym1 typeof<Symbol>

        ]
