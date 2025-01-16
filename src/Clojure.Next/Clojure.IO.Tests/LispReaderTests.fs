module LispReaderTests

open Expecto
open Clojure.IO
open Clojure.Numerics
open System.Numerics
open Clojure.BigArith
open System


let TestDecimalMatch(inStr: string, bdStr: string) =
    let o1 = LispReader.matchNumber(inStr)
    Expect.isTrue o1.IsSome  "Should have parsed successfully"
    let d1 = o1.Value
    Expect.equal (d1.GetType()) typeof<BigDecimal>  "Should have type BigDecimal"
    Expect.equal d1 (BigDecimal.Parse(bdStr))  $"Should be {bdStr}"


[<Tests>]
let MatchNumberTests =
    testList
        "LispReader.MatchNumber"
        [ 
        
          testCase "Reads zero, with optional sign"
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

          testCase "Reads basic decimal integers, with optional sign"
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

          ftestCase "Reads basic hexidecimal integers, with optional sign"
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

          ftestCase "Reads basic octal integers, with optional sign"
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

          ftestCase "Reads integers in specified radix, with optional sign"
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


          ftestCase "Reads floating point"
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


          ftestCase "Reads BigDecimals"
          <| fun _ ->
            TestDecimalMatch("123.7M","123.7")
            TestDecimalMatch("-123.7E4M","-123.7E+4")
            TestDecimalMatch("+123.7E4M","123.7E4")
            TestDecimalMatch("0.0001234500M", "0.0001234500")
            TestDecimalMatch("123456789.987654321E-6M", "123.456789987654321")


          ftestCase "Reads Ratios"
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

          ftestCase "matchNumber matches whole string"
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
          
          ftestCase "matchNumber fails to match weird things"
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

