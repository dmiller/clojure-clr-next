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

          ftestCase "strings with octal characters"
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

