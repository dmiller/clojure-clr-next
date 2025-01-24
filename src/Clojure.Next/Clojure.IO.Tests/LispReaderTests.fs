module LispReaderTests

open Expecto
open Clojure.IO
open Clojure.Numerics
open System.Numerics
open Clojure.BigArith
open System
open System.IO
open Clojure.Collections
open Clojure.Lib
open System.Text.RegularExpressions
open System.Collections.Generic


let TestDecimalMatch(inStr: string, bdStr: string) =
    let o1 = LispReader.matchNumber(inStr)
    Expect.isTrue o1.IsSome  "Should have parsed successfully"
    let d1 = o1.Value
    Expect.equal (d1.GetType()) typeof<BigDecimal>  "Should have type BigDecimal"
    Expect.equal d1 (BigDecimal.Parse(bdStr))  $"Should be {bdStr}"

let CreatePushbackReaderFromString(s: string) =
    let sr = new System.IO.StringReader(s)
    new PushbackTextReader(sr)

let ReadFromString(s: string) =
    let r = CreatePushbackReaderFromString(s)
    LispReader.read(r,true,null,false)

let CreateLNPBRFromString(s: string) =
    let sr = new System.IO.StringReader(s)
    new LineNumberingTextReader(sr)

let ReadFromStringNumbering(s: string) =
    let r = CreateLNPBRFromString(s)
    LispReader.read(r,true,null,false)

let ExpectNumberMatch (value: obj) (expected: obj) (t: Type) =
    Expect.equal (value.GetType()) t  "Should have correct type"
    Expect.equal value expected  "Should have correct value"

let ExpectSymbolMatch (value: obj) (nameSpace:string) (name: string) =
    Expect.equal (value.GetType()) typeof<Symbol>  "Should have type Symbol"
    let s = value :?> Symbol
    Expect.equal s.Namespace nameSpace  "Should have correct namespace"
    Expect.equal s.Name name  "Should have correct name"

let ExpectKeywordMatch (value: obj) (nameSpace:string) (name: string) =
    Expect.equal (value.GetType()) typeof<Keyword>  "Should have type Keyword"
    let s = value :?> Keyword
    Expect.equal s.Namespace nameSpace  "Should have correct namespace"
    Expect.equal s.Name name  "Should have correct name"

let ExpectStringMatch (actual: obj)  (expected: string) =
    Expect.equal (actual.GetType()) typeof<String>  "Should have type Keyword"
    let s = actual :?> String
    Expect.equal s expected  "Should have correct value"

let ExpectCharMatch (actual: obj)  (expected: char) =
    Expect.equal (actual.GetType()) typeof<Char>  "Should have type Keyword"
    let c = actual :?> Char
    Expect.equal c expected  "Should have correct value"

let ExpectIsInstanceOf (o: obj) (t: Type) =
    Expect.isTrue (t.IsAssignableFrom(o.GetType()))  $"Should be assignable to type {t.Name}"

let ExpectGensymMatch (value: obj) (nameSpace:string) (prefix: string) =
    Expect.equal (value.GetType()) typeof<Symbol>  "Should have type Symbol"
    let s = value :?> Symbol
    Expect.equal s.Namespace nameSpace  "Should have correct namespace"
    Expect.isTrue (s.Name.StartsWith(prefix)) "Should have correct prefix"

let ExpectFunctionMatch (actual: obj) (expected: ISeq) =
    
    // THis will only handle a form like:  (fn* [x y & z] (+ x y z))
    // Where the body is a single form which is a list.
    // We have to compare the actual args  against what we put in.
    //  Thus:
    //     Actual:   (fn# [P1__38  P2__39] (+ P1__38 P2__39))
    //     Expected: (fn# [P1__ P2))] (+ P1__ P2__))

    ExpectIsInstanceOf actual typeof<ISeq>

    let fn = actual :?> ISeq

    let actualName = RTSeq.first(fn)
    let expectedName = RTSeq.first(expected)
    
    Expect.equal actualName expectedName  "Function names should match"

    let actualArgs = RTSeq.second(fn)
    let expectedArgs = RTSeq.second(expected)

    ExpectIsInstanceOf actualArgs typeof<Seqable>
    ExpectIsInstanceOf expectedArgs typeof<Seqable>

    let actualArgList = (actualArgs :?> Seqable).seq()
    let expectedArgList = (expectedArgs :?> Seqable).seq()
    
    // Check args & build symbol map for args

    Expect.isTrue  ((isNull actualArgList && isNull expectedArgList) || (actualArgList.count() = expectedArgList.count()))  "Arg counts should match"
    let d = Dictionary<Symbol, Symbol>()

    let rec checkArgs (actual: ISeq) (expected: ISeq) =
        match actual with
        | null -> ()
        | _ ->
            let actualSym = RTSeq.first(actual) :?> Symbol
            let expectedSym = RTSeq.first(expected) :?> Symbol
            Expect.isTrue (actualSym.Name.StartsWith("p__"))  "Arg names should be of form p__N#"
            Expect.isTrue (actualSym.Name.EndsWith("#"))  "Arg names should be of form p__N#"
            d.Add(actualSym, expectedSym)
            checkArgs (actual.next()) (expected.next())

    if not <| isNull actualArgList then
        checkArgs actualArgList expectedArgList

    let actualBody = RTSeq.third(fn)
    let expectedBody = RTSeq.third(expected)

    ExpectIsInstanceOf actualBody typeof<ISeq>
    ExpectIsInstanceOf expectedBody typeof<ISeq>

    let actualBodyList = actualBody :?> ISeq
    let expectedBodyList = expectedBody :?> ISeq

    // Check body, with arg substitutaions

    let rec checkBody (actual: ISeq) (expected: ISeq) =
        match actual with
        | null -> ()
        | _ ->
            let actualForm = RTSeq.first(actual)
            let expectedForm = RTSeq.first(expected)
            match actualForm, expectedForm  with
            | (:? Symbol as actualFormSym), (:? Symbol as expectedFormSym) ->
                if d.ContainsKey(actualFormSym) then
                    Expect.equal (d.[actualFormSym]) expectedFormSym  "Arg names should match"
                else
                    Expect.equal actualForm expectedForm  "Forms should match"
            | _ -> Expect.equal     actualForm expectedForm  "Forms should match"


            checkBody (actual.next()) (expected.next())
    checkBody actualBodyList expectedBodyList
    
[<Tests>]
let MatchNumberTests =
    testList
        "LispReader.MatchNumber"
        [ 
        
          testCase "matchNumber matches zero, with optional sign"
          <| fun _ ->
            let o1 = LispReader.matchNumber "0"
            let o2 = LispReader.matchNumber "+0"
            let o3 = LispReader.matchNumber "-0"

            Expect.isTrue o1.IsSome  "Should have parsed successfully"
            Expect.isTrue o2.IsSome  "Should have parsed successfully"
            Expect.isTrue o3.IsSome  "Should have parsed successfully"

            let i1 = o1.Value
            let i2 = o2.Value
            let i3 = o3.Value

            Expect.equal (i1.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i1 0L  "Should be zero"
            Expect.equal (i2.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i2 0L  "Should be zero"
            Expect.equal (i3.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i3 0L  "Should be zero"

          testCase "matchNumber matches basic decimal integers"
          <| fun _ ->
            let o1 = LispReader.matchNumber "123"
            let o2 = LispReader.matchNumber "+123"
            let o3 = LispReader.matchNumber "-123"
            let o4 = LispReader.matchNumber "12345678901234567890"

            Expect.isTrue o1.IsSome  "Should have parsed successfully"
            Expect.isTrue o2.IsSome  "Should have parsed successfully"
            Expect.isTrue o3.IsSome  "Should have parsed successfully"
            Expect.isTrue o4.IsSome  "Should have parsed successfully"
            
            let i1 = o1.Value
            let i2 = o2.Value
            let i3 = o3.Value
            let i4 = o4.Value

            Expect.equal (i1.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i1 123L  "Should be 123"

            Expect.equal (i2.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i2 123L  "Should be 123"
            
            Expect.equal (i3.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i3 -123L  "Should be -123"
            
            Expect.equal (i4.GetType()) typeof<BigInt>  "Should have type int64"
            Expect.equal i4 (BigInt.fromBigInteger(BigInteger.Parse("12345678901234567890")))  "Should be 123"

          testCase "matchNumber matches basic hexidecimal integers"
          <| fun _ ->
            let o1 = LispReader.matchNumber "0X12A"
            let o2 = LispReader.matchNumber "+0xFFF"
            let o3 = LispReader.matchNumber "-0xFFFFFFFFFFFFFFFFFFFFFFFF"  

            Expect.isTrue o1.IsSome  "Should have parsed successfully"
            Expect.isTrue o2.IsSome  "Should have parsed successfully"
            Expect.isTrue o3.IsSome  "Should have parsed successfully"

            let i1 = o1.Value
            let i2 = o2.Value
            let i3 = o3.Value

            Expect.equal (i1.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i1 0X12AL  "Should be 0X12A"
            Expect.equal (i2.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i2 +0xFFFL  "Should be +0xFFF"
            Expect.equal (i3.GetType()) typeof<BigInt>  "Should have type BigInt"
            Expect.equal i3 (BigInt.fromBigInteger(BigIntegerExtensions.Parse("-FFFFFFFFFFFFFFFFFFFFFFFF", 16)))  "Should be zero"

          testCase "Reads basic octal integers, with optional sign"
          <| fun _ ->
            let o1 = LispReader.matchNumber "0123"
            let o2 = LispReader.matchNumber "+0123"
            let o3 = LispReader.matchNumber "-0123"  
            let o4 = LispReader.matchNumber "01234567012345670123456777"

            Expect.isTrue o1.IsSome  "Should have parsed successfully"
            Expect.isTrue o2.IsSome  "Should have parsed successfully"
            Expect.isTrue o3.IsSome  "Should have parsed successfully"
            Expect.isTrue o4.IsSome  "Should have parsed successfully"

            let i1 = o1.Value
            let i2 = o2.Value
            let i3 = o3.Value
            let i4 = o4.Value

            Expect.equal (i1.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i1 83L  "Should be 83"
            Expect.equal (i2.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i2 83L  "Should be +83"
            Expect.equal (i3.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i3 -83L  "Should be -83"
            Expect.equal (i4.GetType()) typeof<BigInt>  "Should have type BigInt"
            Expect.equal i4 (BigInt.fromBigInteger(BigIntegerExtensions.Parse("01234567012345670123456777", 8)))  "Should be something big"

          testCase "matchNumber reads integers in specified radix, with optional sign"
          <| fun _ ->
            let o1 = LispReader.matchNumber "2R1100"
            let o2 = LispReader.matchNumber "4R123"
            let o3 = LispReader.matchNumber "-4R123"  
            let o4 = LispReader.matchNumber "30R1234QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQAQ"

            Expect.isTrue o1.IsSome  "Should have parsed successfully"
            Expect.isTrue o2.IsSome  "Should have parsed successfully"
            Expect.isTrue o3.IsSome  "Should have parsed successfully"
            Expect.isTrue o4.IsSome  "Should have parsed successfully"

            let i1 = o1.Value
            let i2 = o2.Value
            let i3 = o3.Value
            let i4 = o4.Value

            Expect.equal (i1.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i1 12L  "Should be 12"
            Expect.equal (i2.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i2 27L  "Should be +27"
            Expect.equal (i3.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i3 -27L  "Should be -27"
            Expect.equal (i4.GetType()) typeof<BigInt>  "Should have type BigInt"
            Expect.equal i4 (BigInt.fromBigInteger(BigIntegerExtensions.Parse("1234QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQAQ", 30)))  "Should be something big"


          testCase "matchNumber matches floating point"
          <| fun _ ->
            let o1 = LispReader.matchNumber "123.7"
            let o2 = LispReader.matchNumber "-123.7E4"
            let o3 = LispReader.matchNumber "+1.237e4"  
            let o4 = LispReader.matchNumber "+1.237e-4"
            let o5 = LispReader.matchNumber "1.237e+4"
            let o6 = LispReader.matchNumber "1."
            let o7 = LispReader.matchNumber "1.e3"

            Expect.isTrue o1.IsSome  "Should have parsed successfully"
            Expect.isTrue o2.IsSome  "Should have parsed successfully"
            Expect.isTrue o3.IsSome  "Should have parsed successfully"
            Expect.isTrue o4.IsSome  "Should have parsed successfully"
            Expect.isTrue o5.IsSome  "Should have parsed successfully"
            Expect.isTrue o6.IsSome  "Should have parsed successfully"
            Expect.isTrue o7.IsSome  "Should have parsed successfully"            

            let i1 = o1.Value
            let i2 = o2.Value
            let i3 = o3.Value
            let i4 = o4.Value
            let i5 = o5.Value
            let i6 = o6.Value
            let i7 = o7.Value

            Expect.equal (i1.GetType()) typeof<float>  "Should have type float"
            Expect.equal i1 123.7  "Should be 123.7"
            Expect.equal (i2.GetType()) typeof<float>  "Should have type float"
            Expect.equal i2 -1237000.0  "Should be -1237000.0"
            Expect.equal (i3.GetType()) typeof<float>  "Should have type float"
            Expect.equal i3 12370.0  "Should be 12370.0"
            Expect.equal (i4.GetType()) typeof<float>  "Should have type float"
            Expect.equal i4 0.0001237  "Should be 0.0001237"
            Expect.equal (i5.GetType()) typeof<float>  "Should have type float"
            Expect.equal i5 12370.0  "Should be 12370.0"
            Expect.equal (i6.GetType()) typeof<float>  "Should have type float"
            Expect.equal i6 1.0  "Should be 1.0"
            Expect.equal (i7.GetType()) typeof<float>  "Should have type float"
            Expect.equal i7 1000.0  "Should be 1000.0"


          testCase "matchNumber matches BigDecimals"
          <| fun _ ->
            TestDecimalMatch("123.7M","123.7")
            TestDecimalMatch("-123.7E4M","-123.7E+4")
            TestDecimalMatch("+123.7E4M","123.7E4")
            TestDecimalMatch("0.0001234500M", "0.0001234500")
            TestDecimalMatch("123456789.987654321E-6M", "123.456789987654321")


          testCase "matchNumber matches ratios"
          <| fun _ ->
            let o1 = LispReader.matchNumber "12/1"
            let o2 = LispReader.matchNumber "12/4"
            let o3 = LispReader.matchNumber "12/5"  
            let o4 = LispReader.matchNumber "12345678900000/123456789"

            Expect.isTrue o1.IsSome  "Should have parsed successfully"
            Expect.isTrue o2.IsSome  "Should have parsed successfully"
            Expect.isTrue o3.IsSome  "Should have parsed successfully"
            Expect.isTrue o4.IsSome  "Should have parsed successfully"

            let i1 = o1.Value
            let i2 = o2.Value
            let i3 = o3.Value
            let i4 = o4.Value

            Expect.equal (i1.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i1 12L  "Should be 12"
            Expect.equal (i2.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i2 3L  "Should be 3"
            Expect.equal (i3.GetType()) typeof<Ratio>  "Should have type Ratio"
            Expect.equal i3  (Ratio(BigInteger(12),BigInteger(5)))  "Should be 12/5"
            Expect.equal (i4.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i4 100000L  "Should be 100000"

          testCase "matchNumber matches whole string"
          <| fun _ ->
            let o1 = LispReader.matchNumber " 123"
            let o2 = LispReader.matchNumber "123 "
            let o3 = LispReader.matchNumber " 12.3"  
            let o4 = LispReader.matchNumber "12.3 "
            let o5 = LispReader.matchNumber " 1/23"
            let o6 = LispReader.matchNumber "1/23 "

            Expect.isTrue o1.IsNone  "Should not have parsed successfully"
            Expect.isTrue o2.IsNone  "Should not have parsed successfully"
            Expect.isTrue o3.IsNone  "Should not have parsed successfully"
            Expect.isTrue o4.IsNone  "Should not have parsed successfully"
            Expect.isTrue o5.IsNone  "Should not have parsed successfully"
            Expect.isTrue o6.IsNone  "Should not have parsed successfully"
          
          testCase "matchNumber fails to match weird things"
          <| fun _ ->
            let o1 = LispReader.matchNumber "123a"
            let o2 = LispReader.matchNumber "0x123Z"
            let o3 = LispReader.matchNumber "12.4/24.2"  
            let o4 = LispReader.matchNumber "1.7M3"

            Expect.isTrue o1.IsNone  "Should not have parsed successfully"
            Expect.isTrue o2.IsNone  "Should not have parsed successfully"
            Expect.isTrue o3.IsNone  "Should not have parsed successfully"
            Expect.isTrue o4.IsNone  "Should not have parsed successfully"

            Expect.throwsT<FormatException> (fun () -> LispReader.matchNumber "10RAA" |> ignore)  "bad chars for given radix"

        ]

[<Tests>]
let EOFTests =
    testList
        "Testing EOF"
        [ 
        
          testCase "E0F value returned on EOF"
          <| fun _ ->
            let o = LispReader.read(CreatePushbackReaderFromString("    "), false, 7, false)
            Expect.equal o 7  "Should be EOF value (7)"

          testCase "E0F thrown on EOF"
          <| fun _ ->
            Expect.throwsT<EndOfStreamException> (fun _ -> LispReader.read(CreatePushbackReaderFromString("    "), true, 7, false) |> ignore) "should throw EOF exception"

        ]

[<Tests>]
let NumberReadTests =
    testList
        "Testing read on numbers"
        [ 
        
          testCase "read reads integers"
          <| fun _ ->
            let o1 = ReadFromString "123"
            let o2 = ReadFromString "+123"
            let o3 = ReadFromString "-123"
            let o4 = ReadFromString "12345678901234567890123456789"

            ExpectNumberMatch o1 123L typeof<int64>
            ExpectNumberMatch o2 123L typeof<int64>
            ExpectNumberMatch o3 -123L typeof<int64>
            ExpectNumberMatch o4 (BigInt.fromBigInteger(BigInteger.Parse("12345678901234567890123456789"))) typeof<BigInt>

          testCase "read reads floats"
          <| fun _ ->
            let o1 = ReadFromString "123.4"
            let o2 = ReadFromString "+123.4E4"
            let o3 = ReadFromString "-123.4E-2"

            ExpectNumberMatch o1 123.4 typeof<float>
            ExpectNumberMatch o2 123.4E4 typeof<float>
            ExpectNumberMatch o3 -123.4E-2 typeof<float>


          testCase "read reads ratios"
          <| fun _ ->
            let o1 = ReadFromString "123/456"
            let o2 = ReadFromString "-123/456"
            let o3 = ReadFromString "+123/456"

            ExpectNumberMatch o1 (Ratio(BigInteger(41),BigInteger(152))) typeof<Ratio>
            ExpectNumberMatch o2 (Ratio(BigInteger(-41),BigInteger(152))) typeof<Ratio>
            ExpectNumberMatch o3 (Ratio(BigInteger(41),BigInteger(152))) typeof<Ratio>

          testCase "read reads BigDecimals"
          <| fun _ ->
            let o1 = ReadFromString "123.7M"
            let o2 = ReadFromString "-123.7E4M"
            let o3 = ReadFromString "+123.7E4M"
            let o4 = ReadFromString "0.0001234500M"
            let o5 = ReadFromString "123456789.987654321E-6M"

            ExpectNumberMatch o1 (BigDecimal.Parse("123.7")) typeof<BigDecimal>
            ExpectNumberMatch o2 (BigDecimal.Parse("-123.7E+4")) typeof<BigDecimal>
            ExpectNumberMatch o3 (BigDecimal.Parse("123.7E4")) typeof<BigDecimal>
            ExpectNumberMatch o4 (BigDecimal.Parse("0.0001234500")) typeof<BigDecimal>
            ExpectNumberMatch o5 (BigDecimal.Parse("123.456789987654321")) typeof<BigDecimal>

        ]


[<Tests>]
let SpecialTokenTests =
    testList
        "Testing read on special tokens"
        [ 
        
          testCase "slash alone is slash"
          <| fun _ ->
            let o = ReadFromString "/"
            ExpectSymbolMatch o null "/"

          testCase "clojure.core slash is special"
          <| fun _ ->
            let o = ReadFromString "clojure.core//"
            ExpectSymbolMatch o "clojure.core" "/"
            
          testCase "true/false are boolean"
          <| fun _ ->
            let t = ReadFromString "true"
            let f = ReadFromString "false"
            Expect.equal (t.GetType()) typeof<bool>  "Should have type bool"
            Expect.equal (f.GetType()) typeof<bool>  "Should have type bool"
            Expect.equal t true  "Should be true"
            Expect.equal f false  "Should be false"

          testCase "nil should be null"
          <| fun _ ->
            let n = ReadFromString "nil"
            Expect.isNull n  "Should be null"

        ]

[<Tests>]
let SymbolTests =
    testList
        "Testing read on symbols"
        [ 
        
          testCase "symbols of various flavors"
          <| fun _ ->
            ExpectSymbolMatch (ReadFromString "abc") null "abc"                  // basic (no namesspace)
            ExpectSymbolMatch (ReadFromString "abc/def") "abc" "def"             // with namespace
            ExpectSymbolMatch (ReadFromString "abc/def/ghi") "abc/def" "ghi"     // multiple slashes allowed
            ExpectSymbolMatch (ReadFromString "a:b:c/d:e:f") "a:b:c" "d:e:f"     // multiple colons allows (if not doubles, not beginning or ending)

          testCase "bad symbols of various flavors"
          <| fun _ ->
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "abc:/def" |> ignore) "Namespace should not end with trailing colon"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "abc/def:" |> ignore) "Name should not end with trailing colon"        
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "def:" |> ignore)     "Name should not end with trailing colon"        
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "ab::de" |> ignore)   "Double colon is bad"     
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "abc/ab::de" |> ignore)   "Double colon is bad"  
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "ab::de/efg" |> ignore)   "Double colon is bad"  

          testCase "pipe escaping is fun!"
          <| fun _ ->
            ExpectSymbolMatch (ReadFromString "|ab(1 2)[1 2]{1 2}#{1 2}cd|") null "ab(1 2)[1 2]{1 2}#{1 2}cd"     // piping turns off special characters
            ExpectSymbolMatch (ReadFromString "ab|(1 2)[1 2]{1 2}#{1 2}|cd") null "ab(1 2)[1 2]{1 2}#{1 2}cd"     // piping turns off special characters, even with pipes inside
            ExpectSymbolMatch (ReadFromString "ab|(1 2)[1 2]|cd|{1 2}#{1 2}|ef") null "ab(1 2)[1 2]cd{1 2}#{1 2}ef"     // piping turns off special characters, even with multiple pipes inside
            ExpectSymbolMatch (ReadFromString "ab|cd||ef|gh||||") null "abcd|efgh|"     // pipe escapes self
            ExpectSymbolMatch (ReadFromString "ab|cd/ef|gh") null "abcd/efgh"     // piping eats slash
            ExpectSymbolMatch (ReadFromString "ab/cd|ef/gh|ij") "ab" "cdef/ghij"     // piping eats slash

            Expect.throwsT<EndOfStreamException> (fun _ -> ReadFromString "ab|cd|ef|gh" |> ignore) "Should throw EOF exception with unmatched pipe"

        ]

[<Tests>]
let KeywordTests =
    testList
        "Testing read on keywords"
        [ 
        
          testCase "keywords of various flavors"
          <| fun _ ->
            ExpectKeywordMatch (ReadFromString ":abc") null "abc"                  // basic (no namesspace)
            ExpectKeywordMatch (ReadFromString ":abc/def") "abc" "def"             // with namespace
            // TODO: (original) Add more tests dealing with  :: resolution
            ExpectKeywordMatch (ReadFromString "::abc") (((RTVar.CurrentNSVar :> IDeref).deref() :?> Namespace).Name.Name) "abc"       // double colon defaults to current namespace
            ExpectKeywordMatch (ReadFromString "::ab.cd") (((RTVar.CurrentNSVar :> IDeref).deref() :?> Namespace).Name.Name) "ab.cd"       // double colon defaults to current namespace (some confusion in C# tests in old code)
            ExpectKeywordMatch (ReadFromString ":1") null "1"                      // colon-digit is keyword

            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString ":abc:/def" |> ignore) "Namespace should not end with trailing colon"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString ":abc/def:" |> ignore) "Name should not end with trailing colon"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString ":def:" |> ignore)     "Name should not end with trailing colon"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString ":bar/" |> ignore)     "missing name is bad"
      

        ]


[<Tests>]
let StringTests =
    testList
        "Testing read on string"
        [ 
        
          testCase "string of various flavors"
          <| fun _ ->
            ExpectStringMatch (ReadFromString "\"abc\"") "abc"                  // basic 
            ExpectStringMatch (ReadFromString "\"\"") String.Empty             // empty string

            let chars1 = [|
                '"'; 'a'; 
                '\\'; 't'; 'b';
                '\\'; 'r'; 'c';
                '\\'; 'n'; 'd';
                '\\'; '\\'; 'e';
                '\\'; '"'; 'f';
                '\\'; 'b'; 'g';
                '\\'; 'f'; 'h'; '"' |]
            ExpectStringMatch (ReadFromString (String(chars1))) "a\tb\rc\nd\\e\"f\bg\fh"     // escaped characters



            Expect.throwsT<EndOfStreamException> (fun _ -> ReadFromString "\"abc" |> ignore) "Should throw EOF exception with unmatched quote"

            let chars2 = [|
                '"'; 'a'; 
                '\\'; 't'; 'b';
                '\\'; 'r'; 'c';
                '\\'; 'n'; 'd';
                '\\'  |]

            Expect.throwsT<EndOfStreamException> (fun _ -> ReadFromString (String(chars2)) |> ignore) "Should throw EOF exception with nothing after escape \\"

          testCase "strings with Unicode characters"
          <| fun _ ->

            let chars1 = [|
                '"'; 'a'; 
                '\\'; 'u'; '1'; '2'; 'C'; '4';
                'b'; '"' |]

            ExpectStringMatch (ReadFromString (String(chars1))) "a\u12C4b"     // unicode characters

            let chars2 = [|
                '"'; 'a'; 
                '\\'; 'u'; '1'; '2'; 'X'; '4';
                'b'; 'c'; '"' |]

            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString (String(chars2)) |> ignore) "Should throw EOF exception bad unicode character"

            let chars3 = [|
                '"'; 'a'; 
                '\\'; 'u'; '1'; '2'; 'A' |]

            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString (String(chars2)) |> ignore) "Should throw if EOF while reading Unicode character"

          testCase "strings with octal characters"
          <| fun _ ->

            let chars1 = [|
                '"'; 'a'; 
                '\\'; '1'; '2'; '4';
                'b'; '"' |]

            ExpectStringMatch (ReadFromString (String(chars1))) "aTb"     // octal characters (did hex/octal conversion)

            let chars2 = [|
                '"'; 'a'; 
                '\\'; '1'; '8'; '4';
                'b'; '"' |]

            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString (String(chars2)) |> ignore) "Should throw if bad octal character"

            let chars3= [|
                '"'; 'a'; 
                '\\'; '1'; '8' |]


            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString (String(chars3)) |> ignore) "Should throw if EOF while reading octal character"

            let chars4 = [|
                '"'; 'a'; 
                '\\'; '4'; '7'; '7';
                'b'; '"' |]

            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString (String(chars4)) |> ignore) "Should throw if octal value out of range"


        ]


[<Tests>]
let CharacterTests =
    testList
        "Testing read on characters"
        [ 
        
          testCase "basic and named characters"
          <| fun _ ->
            ExpectCharMatch (ReadFromString "\\a") 'a'                  // basic
            ExpectCharMatch (ReadFromString "\\a b") 'a'                // backslash yeils next character stopping at terminator

            ExpectCharMatch (ReadFromString "\\newline") '\n'           // named characters
            ExpectCharMatch (ReadFromString "\\return") '\r'            // named characters
            ExpectCharMatch (ReadFromString "\\space") ' '              // named characters
            ExpectCharMatch (ReadFromString "\\tab") '\t'               // named characters
            ExpectCharMatch (ReadFromString "\\formfeed") '\f'          // named characters
            ExpectCharMatch (ReadFromString "\\backspace") '\b'         // named characters


          testCase "unicode characters"
          <| fun _ ->
            ExpectCharMatch (ReadFromString "\\u0040") '\u0040'          // unicode characters
            ExpectCharMatch (ReadFromString "\\u12c4") '\u12c4'          // unicode characters

            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "\\u12C 4" |> ignore) "Should throw if EOF while reading Unicode character"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "\\uDAAA" |> ignore) "Should throw if Unicode in bad range"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "\\u12X4" |> ignore) "Should throw if bad Unicode digit"


          testCase "octal characters"
          <| fun _ ->
            ExpectCharMatch (ReadFromString "\\o124") 'T'          // octal characters
            ExpectCharMatch (ReadFromString "\\o12")  '\n'         // octal characters

            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "\\o184" |> ignore) "Should throw if bad octal character"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "\\o477" |> ignore) "Should throw if octal value out of range"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString "\\o0012 aa" |> ignore) "Should throw if too many octal digits"

        ]


[<Tests>]
let CommentTests =
    testList
        "Testing comments"
        [ 
        
          testCase "comments to end of line"
          <| fun _ ->
            ExpectNumberMatch (ReadFromString "  ; ignore me\n 123") 123L typeof<int64>      // semicolon to end of line
            ExpectNumberMatch (ReadFromString "  #! ignore me\n 123") 123L typeof<int64>      // #! to end of line

        ]

[<Tests>]
let DiscardTests =
    testList
        "Testing discard #_"
        [ 
        
          testCase "#_ ignores next form"
          <| fun _ ->
            ExpectNumberMatch (ReadFromString "#_ (1 2 3) 4") 4L typeof<int64>      // semicolon to end of line

          testCase "#_ ignores next form in list"
          <| fun _ ->
            let o1 = ReadFromString("( abc #_ (1 2 3) 12)")

            Expect.equal (o1.GetType()) typeof<PersistentList> "Should read a list"
            
            let pl = o1 :?> PersistentList

            Expect.equal ((pl :> IPersistentCollection).count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(pl)) null "abc"
            ExpectNumberMatch (RTSeq.second(pl)) 12L typeof<int64>
        ]



[<Tests>]
let ListTests =
    testList
        "Testing lists"
        [ 
        
          testCase "Basic list"
          <| fun _ ->
            let o1 = ReadFromString("(abc 12)")

            Expect.equal (o1.GetType()) typeof<PersistentList> "Should read a list"
            
            let pl = o1 :?> PersistentList

            Expect.equal ((pl :> IPersistentCollection).count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(pl)) null "abc"
            ExpectNumberMatch (RTSeq.second(pl)) 12L typeof<int64>

        
          testCase "Empty list"
          <| fun _ ->
            let o1 = ReadFromString("(  )")

            Expect.equal (o1.GetType()) typeof<Clojure.Collections.EmptyList> "Should read a list"
            
            let pl = o1 :?> IPersistentList

            Expect.equal ((pl :> IPersistentCollection).count()) 0 "Should have no elements"

          testCase "Nested list"
          <| fun _ ->
            let o1 = ReadFromString("(a (b c) d)")

            Expect.equal (o1.GetType()) typeof<PersistentList> "Should read a list"
            
            let pl = o1 :?> IPersistentList
      
            Expect.equal ((pl :> IPersistentCollection).count()) 3 "Should have three elements"

            ExpectSymbolMatch (RTSeq.first(pl)) null "a"

            let pl2 = RTSeq.second(pl) :?> IPersistentList
            Expect.equal ((pl2 :> IPersistentCollection).count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(pl2)) null "b"
            ExpectSymbolMatch (RTSeq.second(pl2)) null "c"

            ExpectSymbolMatch (RTSeq.third(pl)) null "d"

          testCase "Missing list terminator fails"
            <| fun _ ->
                Expect.throwsT<EndOfStreamException> (fun _ -> ReadFromString("(a b c") |> ignore) "Should throw EOF exception with unmatched paren"

          testCase "Gets line number for list"
          <| fun _ ->
            let o1 = ReadFromStringNumbering("\n\n (a b \n1 2)")

            Expect.isTrue ((typeof<IObj>).IsAssignableFrom(o1.GetType())) "Should read a list"
            let io = o1 :?> IObj
            let meta = RT0.meta(io)
            Expect.equal (meta.valAt(RTVar.LineKeyword)) 3 "Should have line number 3"
            let sourceSpanMap = meta.valAt(RTVar.SourceSpanKeyword) :?> IPersistentMap
            Expect.equal (sourceSpanMap.valAt(RTVar.StartLineKeyword)) 3 "Should have line number 3"
            Expect.equal (sourceSpanMap.valAt(RTVar.StartColumnKeyword)) 3 "Should have column number 3"
            Expect.equal (sourceSpanMap.valAt(RTVar.EndLineKeyword)) 4 "Should have line number 4"
            Expect.equal (sourceSpanMap.valAt(RTVar.EndColumnKeyword)) 5 "Should have column number 5"

        ]
        

[<Tests>]
let VectorTests =
    testList
        "Testing vectors"
        [ 
        
          testCase "Basic vector"
          <| fun _ ->
            let o1 = ReadFromString("[abc 12]")

            Expect.equal (o1.GetType()) typeof<PersistentVector> "Should read a vector"
            
            let pv = o1 :?> IPersistentVector

            Expect.equal ((pv :> IPersistentCollection).count()) 2 "Should have two elements"
            ExpectSymbolMatch (pv.nth(0)) null "abc"
            ExpectNumberMatch (pv.nth(1)) 12L typeof<int64>

        
          testCase "Empty vector"
          <| fun _ ->
            let o1 = ReadFromString("[  ]")

            Expect.equal (o1.GetType()) typeof<PersistentVector> "Should read a vector"
            
            let pv = o1 :?> IPersistentVector

            Expect.equal ((pv :> IPersistentCollection).count()) 0 "Should have no elements"

          testCase "Nested list in vector"
          <| fun _ ->
            let o1 = ReadFromString("[a (b c) d]")

            Expect.equal (o1.GetType()) typeof<PersistentVector> "Should read a vector"
            
            let pv = o1 :?> IPersistentVector
      
            Expect.equal ((pv :> IPersistentCollection).count()) 3 "Should have three elements"

            ExpectSymbolMatch (pv.nth(0)) null "a"

            let pv2 = pv.nth(1) :?> IPersistentList
            Expect.equal ((pv2 :> IPersistentCollection).count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(pv2)) null "b"
            ExpectSymbolMatch (RTSeq.second(pv2)) null "c"

            ExpectSymbolMatch (pv.nth(2)) null "d"


          testCase "Nested vector in vector"
          <| fun _ ->
            let o1 = ReadFromString("[a [b c] d]")

            Expect.equal (o1.GetType()) typeof<PersistentVector> "Should read a vector"
            
            let pv = o1 :?> IPersistentVector
      
            Expect.equal ((pv :> IPersistentCollection).count()) 3 "Should have three elements"

            ExpectSymbolMatch (pv.nth(0)) null "a"

            let pv2 = pv.nth(1) :?> IPersistentVector
            Expect.equal ((pv2 :> IPersistentCollection).count()) 2 "Should have two elements"
            ExpectSymbolMatch (pv2.nth(0)) null "b"
            ExpectSymbolMatch (pv2.nth(1)) null "c"

            ExpectSymbolMatch (pv.nth(2)) null "d"


          testCase "Missing vector terminator fails"
            <| fun _ ->
                Expect.throwsT<EndOfStreamException> (fun _ -> ReadFromString("[a b c") |> ignore) "Should throw EOF exception with unmatched bracket"

        ]

      

[<Tests>]
let MapTests =
    testList
        "Testing maps"
        [ 
        
          testCase "Basic map"
          <| fun _ ->
            let o1 = ReadFromString("{:a abc 14 12}")

            Expect.equal (o1.GetType()) typeof<PersistentArrayMap> "Should read a map"
            
            let pv = o1 :?> IPersistentMap

            Expect.equal ((pv :> IPersistentCollection).count()) 2 "Should have two elements"
            ExpectSymbolMatch (pv.valAt(Keyword.intern(null,"a"))) null "abc"
            ExpectNumberMatch (pv.valAt(14L)) 12L typeof<int64>

        
          testCase "Empty map"
          <| fun _ ->
            let o1 = ReadFromString("{  }")

            Expect.equal (o1.GetType()) typeof<PersistentArrayMap> "Should read a map"
            
            let pv = o1 :?> IPersistentMap

            Expect.equal ((pv :> IPersistentCollection).count()) 0 "Should have no elements"

 
          testCase "Odd number of elements terminator fails"
            <| fun _ ->
                Expect.throwsT<ArgumentException> (fun _ -> ReadFromString("{a b c}") |> ignore) "Should throw EOF exception with unmatched brace"


          testCase "Missing map terminator fails"
            <| fun _ ->
                Expect.throwsT<EndOfStreamException> (fun _ -> ReadFromString("{a b c d") |> ignore) "Should throw EOF exception with unmatched brace"

        ]


[<Tests>]
let SetTests =
    testList
        "Testing sets"
        [ 
        
          testCase "Basic set"
          <| fun _ ->
            let o1 = ReadFromString("#{abc 12}")

            Expect.equal (o1.GetType()) typeof<PersistentHashSet> "Should read a set"
            
            let pv = o1 :?> IPersistentSet

            Expect.equal ((pv :> IPersistentCollection).count()) 2 "Should have two elements"
            Expect.isTrue (pv.contains(Symbol.intern(null,"abc"))) "Should have abc"
            Expect.isTrue (pv.contains(12L)) "Should have 12"
            Expect.isFalse (pv.contains(13L)) "Should not have 13"

        
          testCase "Empty set"
          <| fun _ ->
            let o1 = ReadFromString("#{  }")

            Expect.equal (o1.GetType()) typeof<PersistentHashSet> "Should read a set"
            
            let pv = o1 :?> IPersistentSet

            Expect.equal ((pv :> IPersistentCollection).count()) 0 "Should have no elements"

          testCase "Missing set terminator fails"
            <| fun _ ->
                Expect.throwsT<EndOfStreamException> (fun _ -> ReadFromString("#{a b c d") |> ignore) "Should throw EOF exception with unmatched brace"

        ]



[<Tests>]
let UnmatchedDelimiterTests =
    testList
        "Testing unmatched delimiters"
        [ 
        
          testCase "Unmatched delimiters"
          <| fun _ ->
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString(")") |> ignore) "Should throw unmatched delimiter exception, naked )"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString("]") |> ignore) "Should throw unmatched delimiter exception, naked ]"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString("}") |> ignore) "Should throw unmatched delimiter exception, naked }"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString("( a b c }") |> ignore) "Mismatched ending delimiter"
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString("( a [b c) ") |> ignore) "Mismatched ending delimiter, nested"
        ]



[<Tests>]
let WrappingFormsTests =
    testList
        "Testing wrapping forms"
        [ 
        
          testCase "Quote wraps #1"
          <| fun _ ->
            let o1 = ReadFromString("'a")
        
            ExpectIsInstanceOf o1 typeof<ISeq>

            let seq = o1 :?> ISeq
            Expect.equal (seq.count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(seq)) null "quote"
            ExpectSymbolMatch (RTSeq.second(seq)) null "a"

          testCase "Quote wraps #2"
          <| fun _ ->
            let o1 = ReadFromString("'(a b c)")
        
            ExpectIsInstanceOf o1 typeof<ISeq>

            let seq = o1 :?> ISeq
            Expect.equal (seq.count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(seq)) null "quote"

            let item2 = RTSeq.second(seq)
            ExpectIsInstanceOf item2 typeof<IPersistentList>

            let pl2 = item2 :?> IPersistentList
            Expect.equal ((pl2 :> IPersistentCollection).count()) 3 "Should have three elements"
            ExpectSymbolMatch (RTSeq.first(pl2)) null "a"
            ExpectSymbolMatch (RTSeq.second(pl2)) null "b"
            ExpectSymbolMatch (RTSeq.third(pl2)) null "c"

        
          testCase "Deref wraps #1"
          <| fun _ ->
            let o1 = ReadFromString("@a")
        
            ExpectIsInstanceOf o1 typeof<ISeq>

            let seq = o1 :?> ISeq
            Expect.equal (seq.count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(seq)) "clojure.core" "deref"
            ExpectSymbolMatch (RTSeq.second(seq)) null "a"

          testCase "Deref wraps #2"
          <| fun _ ->
            let o1 = ReadFromString("@(a b c)")
        
            ExpectIsInstanceOf o1 typeof<ISeq>

            let seq = o1 :?> ISeq
            Expect.equal (seq.count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(seq)) "clojure.core" "deref"

            let item2 = RTSeq.second(seq)
            ExpectIsInstanceOf item2 typeof<IPersistentList>

            let pl2 = item2 :?> IPersistentList
            Expect.equal ((pl2 :> IPersistentCollection).count()) 3 "Should have three elements"
            ExpectSymbolMatch (RTSeq.first(pl2)) null "a"
            ExpectSymbolMatch (RTSeq.second(pl2)) null "b"
            ExpectSymbolMatch (RTSeq.third(pl2)) null "c"

        ]


[<Tests>]
let SyntaxQuoteTests =
    testList
        "Testing syntax-quote forms"
        [ 
        
          testCase "SQ on self-evaluating returns the thing"
          <| fun _ ->

            let o = ReadFromString("`:abc")
            ExpectKeywordMatch o null "abc"

            let o = ReadFromString("`123")
            ExpectNumberMatch o 123L typeof<int64>

            let o = ReadFromString("`\\a")
            ExpectCharMatch o 'a'

            let o = ReadFromString("`\"abc\"")
            ExpectStringMatch o "abc"

          testCase "SQ on special form quotes"
          <| fun _ ->

            let o = ReadFromString("`def")

            ExpectIsInstanceOf o typeof<ISeq>

            let seq = o :?> ISeq
            Expect.equal (seq.count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(seq)) null "quote"
            ExpectSymbolMatch (RTSeq.second(seq)) null "def"

            let o = ReadFromString("`fn*")
            ExpectIsInstanceOf o typeof<ISeq>

            let seq = o :?> ISeq
            Expect.equal (seq.count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(seq)) null "quote"
            ExpectSymbolMatch (RTSeq.second(seq)) null "fn*"


          testCase "SQ on regular symbol resolves"
          <| fun _ ->

            let o = ReadFromString("`abc")

            ExpectIsInstanceOf o typeof<ISeq>

            let seq = o :?> ISeq
            Expect.equal (seq.count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(seq)) null "quote"
            ExpectSymbolMatch (RTSeq.second(seq)) (((RTVar.CurrentNSVar :> IDeref).deref() :?> Namespace).Name.Name) "abc"   


          testCase "SQ on gensym generates"
          <| fun _ ->

            let o = ReadFromString("`abc#")

            ExpectIsInstanceOf o typeof<ISeq>

            let seq = o :?> ISeq
            Expect.equal (seq.count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(seq)) null "quote"
            ExpectGensymMatch (RTSeq.second(seq)) null "abc_"

            
          testCase "SQ on gensym sees same twice"
          <| fun _ ->

            let o = ReadFromString("`(abc# abc#)")

            ExpectIsInstanceOf o typeof<ISeq>
            // Return should be 
            //    (clojure/seq (clojure/concat (clojure/list (quote abc__N)) 
            //                                 (clojure/list (quote abc__N)))))

            let seq = o :?> ISeq
            Expect.equal (seq.count()) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(seq)) "clojure.core" "seq"

            let item2 = RTSeq.second(seq) 
            ExpectIsInstanceOf item2 typeof<ISeq>
            Expect.equal (RT0.count(item2)) 3 "Should have three elements"
            ExpectSymbolMatch (RTSeq.first(item2)) "clojure.core" "concat"

            let item22 = RTSeq.second(item2)
            ExpectIsInstanceOf item22 typeof<ISeq>
            Expect.equal (RT0.count(item22)) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(item22)) "clojure.core" "list"

            let item222 = RTSeq.second(item22)
            ExpectIsInstanceOf item222 typeof<ISeq>
            Expect.equal (RT0.count(item222)) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(item222)) null "quote"
            ExpectGensymMatch (RTSeq.second(item222)) null "abc_"

            let item23 = RTSeq.second(item2)
            ExpectIsInstanceOf item23 typeof<ISeq>
            Expect.equal (RT0.count(item23)) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(item23)) "clojure.core" "list"

            let item232 = RTSeq.second(item22)
            ExpectIsInstanceOf item232 typeof<ISeq>
            Expect.equal (RT0.count(item232)) 2 "Should have two elements"
            ExpectSymbolMatch (RTSeq.first(item222)) null "quote"
            ExpectGensymMatch (RTSeq.second(item232)) null "abc_"

            Expect.equal (RTSeq.second(item222)) (RTSeq.second(item232)) "Should be same gensym"

          testCase "SQ on map makes map"
          <| fun _ ->

            let o = ReadFromString("`{:a 1 :b 2}")
            //  (clojure/apply 
            //      clojure/hash-map 
            //         (clojure/seq 
            //             (clojure/concat (clojure/list :a) 
            //                             (clojure/list 1) 
            //                             (clojure/list :b) 
            //                             (clojure/list 2))))

            let expected = ReadFromString(
                "(clojure.core/apply 
                    clojure.core/hash-map 
                        (clojure.core/seq 
                            (clojure.core/concat (clojure.core/list :a) 
                                                 (clojure.core/list 1) 
                                                 (clojure.core/list :b) 
                                                 (clojure.core/list 2))))")

            ExpectIsInstanceOf o typeof<ISeq>
            Expect.equal o expected "Should be the same"


          testCase "SQ on vector makes vector"
          <| fun _ ->

            let o = ReadFromString("`[:b 2]")
            //  (clojure.core/apply 
            //      clojure.core/vector 
            //         (clojure.core/seq
            //             (clojure.core/concat (clojure.core/list :b) 
            //                                  (clojure.core/list 2))))
              
            let expected = ReadFromString(
                "(clojure.core/apply 
                     clojure.core/vector 
                        (clojure.core/seq 
                           (clojure.core/concat (clojure.core/list :b) 
                                                (clojure.core/list 2))))")
 
            ExpectIsInstanceOf o typeof<ISeq>
            Expect.equal o expected "Should be the same"

          testCase "SQ on set makes set"
          <| fun _ ->

            let o = ReadFromString("`#{:b 2}")
            //  (clojure.core/apply 
            //      clojure.core/hash-set 
            //         (clojure.core/seq
            //             (clojure.core/concat (clojure.core/list :b) 
            //                                  (clojure.core/list 2))))
              
            let expected = ReadFromString(
                "(clojure.core/apply 
                     clojure.core/hash-set 
                        (clojure.core/seq 
                           (clojure.core/concat (clojure.core/list 2) 
                                                (clojure.core/list :b))))")
           // The order the elements of the set are enumerated in are an implementation detail.
           // I just happen to know that for these two elements, they will occur in the order indicated here.
 
            ExpectIsInstanceOf o typeof<ISeq>
            Expect.equal o expected "Should be the same"

          testCase "SQ on list makes list"
          <| fun _ ->

            let o = ReadFromString("`(:b 2)")
            //   (clojure/seq (clojure/concat (clojure/list :b) 
            //                                (clojure/list 2))))
              
            let expected = ReadFromString(
                "(clojure.core/seq 
                           (clojure.core/concat (clojure.core/list :b) 
                                                (clojure.core/list 2)))")
           // The order the elements of the set are enumerated in are an implementation detail.
           // I just happen to know that for these two elements, they will occur in the order indicated here.
 
            ExpectIsInstanceOf o typeof<ISeq>
            Expect.equal o expected "Should be the same"


          testCase "Unquote standalone returns unquote list"
          <| fun _ ->

            let o = ReadFromString("~x")
              
            let expected = ReadFromString("(clojure.core/unquote x)")
 
            ExpectIsInstanceOf o typeof<ISeq>
            Expect.equal o expected "Should be the same"

          testCase "Unquote-splice standalone returns unquote-splie list"
          <| fun _ ->

            let o = ReadFromString("~@x")
              
            let expected = ReadFromString("(clojure.core/unquote-splicing x)")
 
            ExpectIsInstanceOf o typeof<ISeq>
            Expect.equal o expected "Should be the same"


          testCase "SQ on unquote dequotes"
          <| fun _ ->

            let o = ReadFromString("`(a ~b)")

            let nsName = ((RTVar.CurrentNSVar :> IDeref).deref() :?> Namespace).Name.Name
              
            let expected = ReadFromString($"(clojure.core/seq (clojure.core/concat (clojure.core/list (quote {nsName}/a)) (clojure.core/list b)))")
 
            ExpectIsInstanceOf o typeof<ISeq>
            Expect.equal o expected "Should be the same"


          testCase "SQ on unquote-splice not in list fails"
          <| fun _ ->
            Expect.throwsT<ArgumentException> (fun _ -> ReadFromString("`~@x") |> ignore) "Should throw if unquote-splice not in list"

          
          testCase "SQ on unquote-splice splices"
          <| fun _ ->

            let o = ReadFromString("`(a ~@b)")

            let nsName = ((RTVar.CurrentNSVar :> IDeref).deref() :?> Namespace).Name.Name
              
            let expected = ReadFromString($"(clojure.core/seq (clojure.core/concat (clojure.core/list (quote {nsName}/a)) b))")
 
            ExpectIsInstanceOf o typeof<ISeq>
            Expect.equal o expected "Should be the same"

          
          testCase "SQ on () returns empty list"
          <| fun _ ->

            let o = ReadFromString("`()")
              
            let expected = ReadFromString($"(clojure.core/list)")
 
            ExpectIsInstanceOf o typeof<ISeq>
            Expect.equal o expected "Should be the same"

        ]


[<Tests>]
let DispatchTests =
    testList
        "Testing dispatch"
        [ 
        
          testCase "#-dispatch on invalid char fails"
          <| fun _ ->

            Expect.throwsT<ArgumentException> (fun _ ->  ReadFromString("#1(1 2)") |> ignore) "Should fail on bad dispatch char"

        ]

[<Tests>]
let MetaTests =
    testList
        "Testing metadata"
        [ 
        
          testCase "Meta on improper metatdata fails"
          <| fun _ ->
            Expect.throwsT<ArgumentException> (fun _ ->  ReadFromString("^1(1 2)") |> ignore) "Should fail on bad metadata"

          testCase "Meta applied to non-IObj fails"
          <| fun _ ->
            Expect.throwsT<ArgumentException> (fun _ ->  ReadFromString("^{:a 1} 1") |> ignore) "Should fail on bad target"

          testCase "Meta applies hash meta-data as-is to target"
          <| fun _ ->          
            let o = ReadFromString("^{:a 1} (a b)")

            ExpectIsInstanceOf o typeof<IPersistentList>

            let meta = RT0.meta(o)
            let expectedTarget = ReadFromString("(a b)")
            let expectedMeta = ReadFromString("{:a 1}") :?> IPersistentMap

            Expect.equal meta expectedMeta "Should have the expected metadata"
            Expect.equal o expectedTarget "Should have the expected target"

          testCase "Meta applies keyword with value true to target"
          <| fun _ ->          
            let o = ReadFromString("^:c (a b)")

            ExpectIsInstanceOf o typeof<IPersistentList>

            let meta = RT0.meta(o)
            let expectedTarget = ReadFromString("(a b)")
            let expectedMeta = ReadFromString("{:c true}") :?> IPersistentMap

            Expect.equal meta expectedMeta "Should have the expected metadata"
            Expect.equal o expectedTarget "Should have the expected target"

          testCase "Meta applies string as :tag metadate with true to target"
          <| fun _ ->          
            let o = ReadFromString("^\"help\" (a b)")

            ExpectIsInstanceOf o typeof<IPersistentList>

            let meta = RT0.meta(o)
            let expectedTarget = ReadFromString("(a b)")
            let expectedMeta = ReadFromString("{:tag \"help\"}") :?> IPersistentMap

            Expect.equal meta expectedMeta "Should have the expected metadata"
            Expect.equal o expectedTarget "Should have the expected target"

          testCase "Meta applies Symbol as :tag metadate with true to target"
          <| fun _ ->          
            let o = ReadFromString("^x (a b)")

            ExpectIsInstanceOf o typeof<IPersistentList>

            let meta = RT0.meta(o)
            let expectedTarget = ReadFromString("(a b)")
            let expectedMeta = ReadFromString("{:tag x}") :?> IPersistentMap

            Expect.equal meta expectedMeta "Should have the expected metadata"
            Expect.equal o expectedTarget "Should have the expected target"


          testCase "Meta applies vector as :param-tags metadate with true to target"
          <| fun _ ->          
            let o = ReadFromString("^[t1 t2] (a b)")

            ExpectIsInstanceOf o typeof<IPersistentList>

            let meta = RT0.meta(o)
            let expectedTarget = ReadFromString("(a b)")
            let expectedMeta = ReadFromString("{:param-tags [t1 t2]}") :?> IPersistentMap

            Expect.equal meta expectedMeta "Should have the expected metadata"
            Expect.equal o expectedTarget "Should have the expected target"


          testCase "Meta adds file location information to  target metadata if available"
          <| fun _ ->          
            let o = ReadFromStringNumbering("\n\n^c (a b)")

            ExpectIsInstanceOf o typeof<IPersistentList>

            let meta = RT0.meta(o)
            let expectedTarget = ReadFromString("(a b)")
            let expectedMeta = ReadFromString("{:tag c :line 3 :column 2 :source-span {:start-line 3 :start-column 2 :end-line 3 :end-column 9}}") :?> IPersistentMap

            Expect.isTrue  (meta.equiv(expectedMeta)) "Should have the expected metadata"   // Note: use of equal here fails because of int vs long in line/column numbers.
            Expect.equal o expectedTarget "Should have the expected target"
        ]

[<Tests>]
let VarReaderTests =
    testList
        "Testing the Var reader"
        [ 
        
          testCase "var wraps Var"
          <| fun _ ->
            let o = ReadFromString("#'abc")

            let expected = ReadFromString("(var abc)")
            Expect.equal o expected "Should be the same"

        ]


[<Tests>]
let RegexReaderTests =
    testList
        "# \" generates regex"
        [ 
        
          testCase "var wraps Var"
          <| fun _ ->
            let o = ReadFromString("#\"abc\"")

            ExpectIsInstanceOf o typeof<Regex>
            let re = o :?> Regex
            Expect.equal (re.ToString()) "abc" "Should be the same"


          testCase "var throws EOF on missing close double-quote"
          <| fun _ ->
            Expect.throwsT<EndOfStreamException>(fun _ -> ReadFromString("#\abc") |> ignore) "Should throw EOF"

          testCase "var handles escape on double-quote"
          <| fun _ ->

            let chars = [|  '#'; '"'; 'a'; '\\'; '"'; 'b'; 'c'; '"' |]
            let o = ReadFromString(String(chars))

            ExpectIsInstanceOf o typeof<Regex>
            let re = o :?> Regex
            Expect.equal (re.ToString()) "a\\\"bc" "Should be the same"


        ]


[<Tests>]
let FnReaderTests =
    testList
        "function and arg readers"
        [ 
        
          ftestCase "#(...) with no args generates a no-arg function"
          <| fun _ ->
            let o = ReadFromString("#(+ 1 2)")

            let expected = ReadFromString("(fn* [] (+ 1 2))")

            Expect.equal o expected "should generate no-arg function"
            ExpectFunctionMatch o ((expected :?> Seqable).seq())

          ftestCase "#(...) with args generates function with enough args"
          <| fun _ ->
            let o = ReadFromString("#(+ %2 2)")

            let expected = ReadFromString("(fn* [x y] (+ y 2))")
            
            ExpectFunctionMatch o ((expected :?> Seqable).seq())


          ftestCase "#(...) with anon arg generates function with one arg"
          <| fun _ ->
            let o = ReadFromString("#(+ % 2)")

            let expected = ReadFromString("(fn* [x] (+ x 2))")
            
            ExpectFunctionMatch o ((expected :?> Seqable).seq())

          ftestCase "#(...) with anon arg and non-anon arg generates function with enough args"
          <| fun _ ->
            let o = ReadFromString("#(+ % %3)")

            let expected = ReadFromString("(fn* [x y z] (+ x z))")
            
            ExpectFunctionMatch o ((expected :?> Seqable).seq())

          ftestCase "Arg reader outside #(...) returns symbol as is"
          <| fun _ ->
            let o = ReadFromString("%2")

            ExpectIsInstanceOf o typeof<Symbol>
            Expect.equal (o :?> Symbol).Name "%2" "Should be the same"

          ftestCase "Arg reader followed by non-digit fails"
          <| fun _ ->
            Expect.throwsT<ArgumentException>(fun _ -> ReadFromString("#(+ %a 2)") |> ignore) "Should throw"

        ]

        // TODO: eval reader tests.  Need to figure out how to get the eval reader to work in a test context.