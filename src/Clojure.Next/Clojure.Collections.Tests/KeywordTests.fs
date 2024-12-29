module KeywordTests


open Expecto

open TestHelpers
open Clojure.Collections
open System.Collections
open System

[<Tests>]
let KeywordTests =

    testList
        "Keyword tests"
        [
            testCase "Intern creates keyword based on symbol"
            <| fun _ ->
                let sym = Symbol.intern("def","abc")
                let kw = Keyword.intern(sym)
                Expect.equal kw.Name  sym.Name "Name should be same"
                Expect.equal kw.Namespace  sym.Namespace "Namespace should be same"


            testCase "Intern returns same keyword for equal symbols"
            <| fun _  ->
                let sym1 = Symbol.intern("def","abc")
                let sym2 = Symbol.intern("def","abc")
                let kw1 = Keyword.intern(sym1)
                let kw2 = Keyword.intern(sym2)
                Expect.isTrue (LanguagePrimitives.PhysicalEquality  kw1 kw2) "Should be identical keyword"

            testCase "Intern2 creates keyword based on symbol"
            <| fun _ ->
                let kw = Keyword.intern("def","abc")
                Expect.equal kw.Name  "abc" "Name should be abc"
                Expect.equal kw.Namespace  "def" "Namespace should be def"

            testCase "Intern2 returns same keyword for equal inputs"
            <| fun _  ->
                let kw1 = Keyword.intern("def","abc")
                let kw2 = Keyword.intern("def","abc")
                Expect.isTrue (LanguagePrimitives.PhysicalEquality  kw1 kw2) "Should be identical keyword"

            testCase "ToString returns string with : prefix"
            <| fun _ ->
                let kw = Keyword.intern("def","abc")
                Expect.equal (kw.ToString()) ":def/abc" "ToString should be :def/abc"

            testCase "Equals on identity is true"
            <| fun _ ->
                let kw = Keyword.intern("def","abc")
                Expect.isTrue (kw.Equals(kw)) "Should be equal"

            testCase "Equals on non-keyword is false"
            <| fun _ ->
                let sym = Symbol.intern("def","abc")
                let kw = Keyword.intern(sym)
                Expect.isFalse (kw.Equals(sym)) "Should not be equal"
                Expect.isFalse (kw.Equals("abc")) "Should not be equal"

            testCase "hashcode depends on value"
            <| fun _ ->
                let kw1 = Keyword.intern("def","abc")
                let kw2 = Keyword.intern("def","abc")
                let kw3 = Keyword.intern("def","ab")
                let kw4 = Keyword.intern("de","abc")
                Expect.equal (kw1.GetHashCode())  (kw2.GetHashCode()) "Should have same hashcode"
                Expect.notEqual (kw1.GetHashCode()) (kw3.GetHashCode()) "Should have different hashcode"
                Expect.notEqual (kw1.GetHashCode()) (kw4.GetHashCode()) "Should have different hashcode"

            testCase "name and namespace come from the symbol"
            <| fun _ ->
                let sym1 = Symbol.intern("def","abc")
                let kw1 = Keyword.intern(sym1)
                let sym2 = Symbol.intern("abc")
                let kw2 = Keyword.intern(sym2)

                Expect.equal kw1.Name  sym1.Name "Name should be same"
                Expect.equal kw1.Namespace  sym1.Namespace "Namespace should be same"
                Expect.equal kw2.Name  sym2.Name "Name should be same"
                Expect.equal kw2.Namespace  sym2.Namespace "Namespace should be same"


            testCase "Invoke2 indexes on its first arg"
            <| fun _ ->
                let kw1 = Keyword.intern("abc")
                let kw2 = Keyword.intern("abc")
                let kw3 = Keyword.intern("ab")

                let  dict : IDictionary = new Hashtable()
                dict[kw1] <- 7
                dict["abc"] <- 8
                dict["ab"] <- 9

                Expect.equal ((kw1:>IFn).invoke(dict)) 7 "Find kw1 in dictionary"
                Expect.equal ((kw2:>IFn).invoke(dict)) 7 "Find sym with same name in dictionary"
                Expect.isNull ((kw3:>IFn).invoke(dict)) "Dont find absent key"


            testCase "Invoke3 indexes on its first arg, returns notFound value if absent"
            <| fun _ ->
                let kw1 = Keyword.intern("abc")
                let kw2 = Keyword.intern("abc")
                let kw3 = Keyword.intern("ab")

                let  dict : IDictionary = new Hashtable()
                dict[kw1] <- 7
                dict["abc"] <- 8
                dict["ab"] <- 9

                Expect.equal ((kw1:>IFn).invoke(dict,20)) 7 "Find kw1 in dictionary"
                Expect.equal ((kw2:>IFn).invoke(dict,20)) 7 "Find sym with same name in dictionary"
                Expect.equal ((kw3:>IFn).invoke(dict,20)) 20 "Return notfound value for absent key"

            testCase "Invoke on wrong number of args fails"
            <| fun _ ->
                let kw1 = Keyword.intern("abc")
                let f1() = (kw1:>IFn).invoke() |> ignore
                let f2() = (kw1:>IFn).invoke(1,2,3) |> ignore
                Expect.throwsT<ArityException> f1 "Should throw on invoke()"
                Expect.throwsT<ArityException> f2 "Should throw on invoke(1,2,3)"


            testCase "CompareTo on non-keyword fails"
            <| fun _ ->
                let kw1 = Keyword.intern("abc")
                let f1() = (kw1:>IComparable).CompareTo("abc") |> ignore
                Expect.throwsT<ArgumentException> f1 "Should throw on CompareTo(\"abc\")"

            testCase "CompareTo equal keyword should be 0"
            <| fun _ ->
                let kw1 = Keyword.intern("abc")
                let kw2 = Keyword.intern("abc")
                Expect.equal ((kw1:>IComparable).CompareTo(kw2)) 0 "Should be 0"

            testCase "CompareTo different keyword should be -1 or 1"
            <| fun _ ->
                let kw1 = Keyword.intern("abc")
                let kw2 = Keyword.intern("ab")
                let kw3 = Keyword.intern("def", "abc")
                let kw4 = Keyword.intern("de", "abc")
                Expect.equal ((kw1:>IComparable).CompareTo(kw2)) 1 "Should be 1"
                Expect.equal ((kw1:>IComparable).CompareTo(kw3)) -1 "Should be -1"
                Expect.equal ((kw1:>IComparable).CompareTo(kw4)) -1 "Should be -1"
                Expect.equal ((kw3:>IComparable).CompareTo(kw4)) 1 "Should be 1"

  
        ]
