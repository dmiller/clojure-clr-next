module BigIntegerExtensionsTests

open Expecto
open Clojure.BigArith
open System.Numerics
open System

// basic sanity tests

[<Tests>]
let bigIntegerRadixParseTests =
    testList
        "test radix parsing"
        [

          testCase "detect radix too small"
          <| fun _ ->
            Expect.throwsT<ArgumentOutOfRangeException> (fun () ->  BigIntegerExtensions.TryParse("0", 1) |> ignore) "Should throw on bad radix"

          testCase "detect radix too large"
          <| fun _ ->
            Expect.throwsT<ArgumentOutOfRangeException> (fun () ->  BigIntegerExtensions.TryParse("0", 37) |> ignore) "Should throw on bad radix"

          testCase "parses zero"
          <| fun _ ->
            let result = BigIntegerExtensions.TryParse("0", 10)
            Expect.isSome result "should parse successfully"
            Expect.equal result.Value BigInteger.Zero "should be zero"
    
          testCase "parses negative zero"
          <| fun _ ->
            let result = BigIntegerExtensions.TryParse("-0", 10)
            Expect.isSome result "should parse successfully"
            Expect.equal result.Value BigInteger.Zero "should be zero"    

          testCase "parses multiple zeros as zeros"
          <| fun _ ->
            let result = BigIntegerExtensions.TryParse("00000", 10)
            Expect.isSome result "should parse successfully"
            Expect.equal result.Value BigInteger.Zero "should be zero"

          testCase "parses multiple zeros with leading minus as zeros"
          <| fun _ ->
            let result = BigIntegerExtensions.TryParse("-00000", 10)
            Expect.isSome result "should parse successfully"
            Expect.equal result.Value BigInteger.Zero "should be zero"

          testCase "parse of muliple hyphens fails"
            <| fun _ ->
                let result = BigIntegerExtensions.TryParse("--", 10)
                Expect.isNone result "should not parse successfully"

                let result = BigIntegerExtensions.TryParse("-12-23", 10)
                Expect.isNone result "should not parse successfully"

                let result = BigIntegerExtensions.TryParse("--11223", 10)
                Expect.isNone result "should not parse successfully"
 
          testCase "parse sign-only fails"
            <| fun _ ->           
                let result = BigIntegerExtensions.TryParse("-", 10)
                Expect.isNone result "should not parse successfully"

                let result = BigIntegerExtensions.TryParse("+", 10)
                Expect.isNone result "should not parse successfully"

          testCase "parse on bogus char fails"
            <| fun _ ->           
                let result = BigIntegerExtensions.TryParse("123.56", 10)
                Expect.isNone result "should not parse successfully"

          testCase "parse on digit out of range fails"
            <| fun _ ->           
                let result = BigIntegerExtensions.TryParse("01010120101", 2)
                Expect.isNone result "should not parse successfully"

                let result = BigIntegerExtensions.TryParse("01234567875", 8)
                Expect.isNone result "should not parse successfully"

                let result = BigIntegerExtensions.TryParse("CabBaGe", 16)
                Expect.isNone result "should not parse successfully"
        
                let result = BigIntegerExtensions.TryParse("AAAAAAAAAAAAAAAAAAAAAAACabBaGe", 16)
                Expect.isNone result "should not parse successfully"

          testCase "parse short string succeeds"
            <| fun _ ->           
                let result = BigIntegerExtensions.TryParse("100", 2)
                Expect.isSome result "should parse successfully"
                Expect.equal result.Value (BigInteger(4)) "should be 4"

                let result = BigIntegerExtensions.TryParse("100", 10)
                Expect.isSome result "should parse successfully"
                Expect.equal result.Value (BigInteger(100)) "should be 100"
 
                let result = BigIntegerExtensions.TryParse("100", 16)
                Expect.isSome result "should parse successfully"
                Expect.equal result.Value (BigInteger(0x100)) "should be 0x100"

                let result = BigIntegerExtensions.TryParse("100", 36)
                Expect.isSome result "should parse successfully"
                Expect.equal result.Value (BigInteger(36*36)) "should be 36*36"

          testCase "parse long string succeeds"
            <| fun _ ->           

                let result = BigIntegerExtensions.TryParse("123456789012345678901234567890", 10)
                Expect.isSome result "should parse successfully"
                Expect.equal result.Value (BigInteger.Parse("123456789012345678901234567890")) "should be 123456789012345678901234567890"

                let result = BigIntegerExtensions.TryParse("-123456789012345678901234567890", 10)
                Expect.isSome result "should parse successfully"
                Expect.equal result.Value (BigInteger.Parse("-123456789012345678901234567890")) "should be -123456789012345678901234567890"

                let result = BigIntegerExtensions.TryParse("+123456789012345678901234567890", 10)
                Expect.isSome result "should parse successfully"
                Expect.equal result.Value (BigInteger.Parse("123456789012345678901234567890")) "should be 123456789012345678901234567890"

        
        
          ]
