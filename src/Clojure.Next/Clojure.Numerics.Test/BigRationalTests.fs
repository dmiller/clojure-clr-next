module RationalTests

open Expecto
open Clojure.Numerics
open System.Numerics
open System

// basic sanity tests

[<Tests>]
let primaryConstructorTests =
    testList
        "normal form from basic constructor"
        [

          testCase "Same BigIntegers if GDC = 1"
          <| fun _ ->
              let n = BigInteger(10)
              let d = BigInteger(7)
              let r = Rational(n, d)
              Expect.equal r.Numerator n "should have same numerator"
              Expect.equal r.Denominator d "should have same denominator"

          testCase "Reduced BigIntegers if GDC <> 1"
          <| fun _ ->
              let n = BigInteger(4 * 3 * 5)
              let d = BigInteger(2 * 5 * 7 * 9)
              let nr = BigInteger(2)
              let dr = BigInteger(3 * 7)
              let r = Rational(n, d)
              Expect.equal r.Numerator nr "should have reduced numerator"
              Expect.equal r.Denominator dr "should have reduced denominator"

          testCase "Should have -/+ given +/- in numerator"
          <| fun _ ->
              let n = BigInteger(10)
              let d = BigInteger(77)
              let nn = BigInteger.Negate(n)
              let dn = BigInteger.Negate(d)
              let r1 = Rational(n, dn)
              Expect.equal r1.Numerator nn "Numerator should be negative"
              Expect.equal r1.Denominator d "Denominator should be positive"

          testCase "Should have -/+ given -/+ in numerator"
          <| fun _ ->
              let n = BigInteger(10)
              let d = BigInteger(77)
              let nn = BigInteger.Negate(n)
              let dn = BigInteger.Negate(d)
              let r1 = Rational(nn, d)
              Expect.equal r1.Numerator nn "Numerator should be negative"
              Expect.equal r1.Denominator d "Denominator should be positive"

          testCase "Should have +/+ given -/- in numerator"
          <| fun _ ->
              let n = BigInteger(10)
              let d = BigInteger(77)
              let nn = BigInteger.Negate(n)
              let dn = BigInteger.Negate(d)
              let r1 = Rational(nn, dn)
              Expect.equal r1.Numerator n "Numerator should be positive"
              Expect.equal r1.Denominator d "Denominator should be positive"

          testCase "Should fail if denominator is zero"
          <| fun _ ->
              Expect.throwsT<DivideByZeroException> (fun f ->
                  Rational(BigInteger.One, BigInteger.Zero)
                  |> ignore) "Fail if denominator is zero" ]


// create from int
// could do some property-based checking for the following


let simpleIntTest (i: int) =
    let bi = BigInteger(i)
    let r = Rational(i)
    Expect.equal r.Numerator bi ("Numerator should be BI of " + i.ToString())
    Expect.equal r.Denominator BigInteger.One "Denominator shoudld be BI of 1"


let createIntTests data =
    data
    |> List.map (fun i ->
        testCase (sprintf "creating from integer %i" i)
        <| fun _ -> simpleIntTest i)

let basicIntConstructionTests = [ 0; 1; -1; 5; -5 ]

[<Tests>]
let basicIntConstructionTestList =
    testList "basic int construction" (createIntTests basicIntConstructionTests)


// create from decimal

let simpleDecimalTest (d: decimal) (s: string) =
    let r: Rational = Rational(d)
    Expect.equal (r.ToString()) s "decimal?"

let createDecimalTests data =
    data
    |> List.map (fun (d, s) ->
        testCase (sprintf "creating from decimal %s" (d.ToString()))
        <| fun _ -> simpleDecimalTest d s)

let basicDecimalConstructionTests =
    [ (0.0M, "0/1")
      (0.1M, "1/10")
      (0.2M, "1/5")
      (0.12345M, "2469/20000")
      (1.2345M, "2469/2000")
      (12.345M, "2469/200")
      (123.45M, "2469/20")
      (1234.5M, "2469/2")
      (12345M, "12345/1")
      (123450M, "123450/1")
      (1234500M, "1234500/1") ]

[<Tests>]
let basicDecimalConstructionTestList =
    testList "basic decimal construction" (createDecimalTests basicDecimalConstructionTests)



let simpleParseTest (inStr: string) (outStr: string) =
    let r = Rational.Parse(inStr)
    Expect.equal (r.ToString()) outStr "representation should match"

let createParseTests data =
    data
    |> List.map (fun (inStr, outStr) ->
        testCase (sprintf "parsing %s as %s" inStr outStr)
        <| fun _ -> simpleParseTest inStr outStr)

let basicParseTests =
    [ ("0", "0/1")
      ("1", "1/1")
      ("2", "2/1")
      ("-1", "-1/1")
      ("-2", "-2/1")

      ("1/7", "1/7")
      ("2/7", "2/7")
      ("-3/7", "-3/7")
      ("2/4", "1/2")
      ("60/630", "2/21") ]

[<Tests>]
let basicParserTestList =
    testList "basic parsing" (createParseTests basicParseTests)


// Negate

let simpleNegateTest (inStr: string) (outStr: string) =
    let r = Rational.Parse(inStr).Negate()
    Expect.equal (r.ToString()) outStr "representation should match"

let createNegateTests data =
    data
    |> List.map (fun (inStr, outStr) ->
        testCase (sprintf "parsing %s as %s" inStr outStr)
        <| fun _ -> simpleNegateTest inStr outStr)

let basicNegateTests =
    [ ("0", "0/1")
      ("1", "-1/1")
      ("2", "-2/1")
      ("-1", "1/1")
      ("-2", "2/1")

      ("1/7", "-1/7")
      ("2/7", "-2/7")
      ("-3/7", "3/7")
      ("2/4", "-1/2")
      ("60/630", "-2/21") ]

[<Tests>]
let basicNegateTestList =
    testList "basic negation" (createNegateTests basicNegateTests)

// Abs

let simpleAbsTest (inStr: string) (outStr: string) =
    let r = Rational.Parse(inStr).Abs()
    Expect.equal (r.ToString()) outStr "representation should match"

let createAbsTests data =
    data
    |> List.map (fun (inStr, outStr) ->
        testCase (sprintf "parsing %s as %s" inStr outStr)
        <| fun _ -> simpleAbsTest inStr outStr)

let basicAbsTests =
    [ ("0", "0/1")
      ("1", "1/1")
      ("2", "2/1")
      ("-1", "1/1")
      ("-2", "2/1")

      ("1/7", "1/7")
      ("2/7", "2/7")
      ("-3/7", "3/7")
      ("2/4", "1/2")
      ("60/630", "2/21") ]

[<Tests>]
let basicAbsTestList =
    testList "basic absolute value" (createAbsTests basicAbsTests)


// Add

let simpleAddTest (lhs: string) (rhs: string) (result: string) =
    let r1 = Rational.Parse(lhs)
    let r2 = Rational.Parse(rhs)
    let r = r1 + r2
    Expect.equal (r.ToString()) result "sum should match"

let createAddTests data =
    data
    |> List.map (fun (lhs, rhs, result) ->
        testCase (sprintf "Adding %s to %s" lhs rhs)
        <| fun _ -> simpleAddTest lhs rhs result

        )

let basicAddTests =
    [ ("0", "1/2", "1/2")
      ("1/2", "0", "1/2")
      ("1/2", "1/2", "1/1")
      ("1/2", "1/4", "3/4")
      ("5/2", "2/3", "19/6")
      ("7/6", "9/4", "41/12")
      ("0", "-1/2", "-1/2")
      ("-1/2", "0", "-1/2")
      ("1/2", "-1/2", "0/1")
      ("-1/2", "1/4", "-1/4")
      ("-5/2", "-2/3", "-19/6")
      ("7/6", "-9/4", "-13/12") ]

[<Tests>]
let basicAddTestList =
    testList "basic addition" (createAddTests basicAddTests)


// Sub

let simpleSubTest (lhs: string) (rhs: string) (result: string) =
    let r1 = Rational.Parse(lhs)
    let r2 = Rational.Parse(rhs)
    let r = r1 - r2
    Expect.equal (r.ToString()) result "difference should match"

let createSubTests data =
    data
    |> List.map (fun (lhs, rhs, result) ->
        testCase (sprintf "subtracting %s from %s" rhs lhs)
        <| fun _ -> simpleSubTest lhs rhs result

        )

let basicSubTests =
    [ ("0", "1/2", "-1/2")
      ("1/2", "0", "1/2")
      ("1/2", "1/2", "0/1")
      ("1/2", "1/4", "1/4")
      ("5/2", "2/3", "11/6")
      ("7/6", "9/4", "-13/12")
      ("0", "-1/2", "1/2")
      ("-1/2", "0", "-1/2")
      ("1/2", "-1/2", "1/1")
      ("-1/2", "1/4", "-3/4")
      ("-5/2", "-2/3", "-11/6")
      ("7/6", "-9/4", "41/12") ]

[<Tests>]
let basicSubTestList =
    testList "basic subtration" (createSubTests basicSubTests)
