module LispReaderTests

open Expecto
open Clojure.IO
open Clojure.Numerics
open System.Numerics


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
            Expect.isTrue o3.IsSome  "Should have parsed successfully"
            
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

          testCase "Reads basic hexidecimal integers, with optional sign"
          <| fun _ ->
            let o1 = LispReader.matchNumber "0X12A"
            let o2 = LispReader.matchNumber "+0xFFF"
            //let o3 = LispReader.matchNumber "-0xFFFFFFFFFFFFFFFFFFFFFFFF"  // TODO: after we have radix-parsing of BigInteger

            let i1 = o1.Value
            let i2 = o2.Value
            //let i3 = o3.Value

            Expect.equal (i1.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i1 0X12AL  "Should be 0X12A"
            Expect.equal (i2.GetType()) typeof<int64>  "Should have type int64"
            Expect.equal i2 +0xFFFL  "Should be +0xFFF"
            //Expect.equal (i3.GetType()) typeof<int64>  "Should have type int64"
            //Expect.equal i3 0L  "Should be zero"
        ]

