module NumbersTests

open Expecto
open Clojure.Numerics
open System.Numerics
open Clojure.BigArith
open System
open System.Text


[<Tests>]
let testOpsSelect =
    testList
        "Test OpsType selection"
        [ testCase "obj->OpsType"
          <| fun _ ->
              let signedIntegerValues: obj list = [ 97y; 97s; 97; 97L ]
              let unsignedIntegerValues: obj list = [ 97uy; 97us; 97u; 97UL ]
              let floatValues: obj list = [ 97.2f; 97.2 ]
              let clrDecimalValues: obj list = [ 97.2M ]

              let otherNumericValues: (obj * OpsType) list =
                  [ (BigInteger(1e300), OpsType.BigInteger)
                    (Ratio(BigInteger(1e200), BigInteger(1e300)), OpsType.Ratio)
                    (BigDecimal.Create(1.5e10), OpsType.BigDecimal)
                    (12.5M, OpsType.ClrDecimal)
                    ("123", OpsType.Long) ]

              for v in signedIntegerValues do
                  Expect.equal (OpsSelector.ops (v)) OpsType.Long "Expect signed integers to map to Long"

              for v in unsignedIntegerValues do
                  Expect.equal (OpsSelector.ops (v)) OpsType.ULong "Expect unsigned integers to map to ULong"

              for v in floatValues do
                  Expect.equal (OpsSelector.ops (v)) OpsType.Double "Expect floats to map to Double"

              for v in clrDecimalValues do
                  Expect.equal (OpsSelector.ops (v)) OpsType.ClrDecimal "Expect CLR decimal to map to ClrDecimal"

              for (v, o) in otherNumericValues do
                  Expect.equal (OpsSelector.ops (v)) o "Expect other types to map properly"

          testCase "OpsType.combine"
          <| fun _ ->
              let L = OpsType.Long
              let UL = OpsType.ULong
              let D = OpsType.Double
              let R = OpsType.Ratio
              let BI = OpsType.BigInteger
              let BD = OpsType.BigDecimal
              let CD = OpsType.ClrDecimal

              let combinations: (OpsType * OpsType * OpsType) list =
                  [ (L, L, L)
                    (L, D, D)
                    (L, R, R)
                    (L, BI, BI)
                    (L, BD, BD)
                    (L, UL, BI)
                    (L, CD, CD)

                    (D, L, D)
                    (D, D, D)
                    (D, R, D)
                    (D, BI, D)
                    (D, BD, D)
                    (D, UL, D)
                    (D, CD, D)

                    (R, L, R)
                    (R, D, D)
                    (R, R, R)
                    (R, BI, R)
                    (R, BD, BD)
                    (R, UL, R)
                    (R, CD, BD)

                    (BI, L, BI)
                    (BI, D, D)
                    (BI, R, R)
                    (BI, BI, BI)
                    (BI, BD, BD)
                    (BI, UL, BI)
                    (BI, CD, BD)

                    (BD, L, BD)
                    (BD, D, D)
                    (BD, R, BD)
                    (BD, BI, BD)
                    (BD, BD, BD)
                    (BD, UL, BD)
                    (BD, CD, BD)

                    (UL, L, BI)
                    (UL, D, D)
                    (UL, R, R)
                    (UL, BI, BI)
                    (UL, BD, BD)
                    (UL, UL, UL)
                    (UL, CD, CD)

                    (CD, L, CD)
                    (CD, D, D)
                    (CD, R, BD)
                    (CD, BI, BD)
                    (CD, BD, BD)
                    (CD, UL, CD)
                    (CD, CD, CD) ]

              for (t1, t2, t3) in combinations do
                  Expect.equal (OpsSelector.combine (t1, t3)) t3 "Expect combination to match"

          ]


[<Tests>]
let testAdd =
    testList
        "Test arithmetic operations"
        [ testCase "addition"
          <| fun _ ->
              let r1 = Ratio(BigInteger(2), BigInteger(3))
              let r2 = Ratio(BigInteger(4), BigInteger(5))
              let r3 = Ratio(BigInteger(22), BigInteger(15))

              let bi1 = BigInteger(1e10)
              let bi2 = BigInteger(2e10)
              let bi3 = BigInt.fromBigInteger (BigInteger(3e10))

              let bd1 = BigDecimal.Create("11111111111111111.1111111111111")
              let bd2 = BigDecimal.Create("22222222222222222.2222222222222")
              let bd3 = BigDecimal.Create("33333333333333333.3333333333333")

              let biMVp1 = BigInt.fromBigInteger (BigInteger(Int64.MaxValue) + BigInteger.One)
              let biUMVp1 = BigInt.fromBigInteger (BigInteger(UInt64.MaxValue) + BigInteger.One)
              let bdMVp1 = BigDecimal.Create(Decimal.MaxValue) + BigDecimal.Create(1)

              // add

              Expect.equal (Numbers.add (1L, 2L)) 3L "1+2=3"
              Expect.equal (Numbers.add (1UL, 2UL)) 3UL "1+2=3 unsigned"
              Expect.floatClose Accuracy.medium (Numbers.add (1.2, 2.3)) 3.5 "1.2+2.3=3.5"
              Expect.equal (Numbers.add (r1, r2)) r3 "2/3+4/5=22/15"
              Expect.equal (Numbers.add (bi1, bi2)) bi3 "1+2=3 bigint"
              Expect.equal (Numbers.add (bd1, bd2)) bd3 "1+2=3 bigdec"
              Expect.equal (Numbers.add (1.2M, 2.3M)) 3.5M "1.2M+2.3M=3.5M"

              // Check indirect calls on primitive args
              Expect.equal (Numbers.add (1L :> obj, 2L :> obj)) 3L "1+2=3"
              Expect.equal (Numbers.add (1UL :> obj, 2UL :> obj)) 3UL "1+2=3 unsigned"
              Expect.floatClose Accuracy.medium ((Numbers.add (1.2 :> obj, 2.3 :> obj)) :?> float) 3.5 "1.2+2.3=3.5"
              Expect.equal (Numbers.add (1.2M :> obj, 2.3M :> obj)) 3.5M "1.2M+2.3M=3.5M"

              // check edge cases
              Expect.throwsT<OverflowException> (fun () -> Numbers.add (Int64.MaxValue, 1L) |> ignore) "add overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.add (Int64.MaxValue :> obj, 1L :> obj) |> ignore)
                  "add overflows"

              Expect.throwsT<OverflowException> (fun () -> Numbers.add (UInt64.MaxValue, 1UL) |> ignore) "add overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.add (UInt64.MaxValue :> obj, 1UL :> obj) |> ignore)
                  "add overflows"

              Expect.throwsT<OverflowException> (fun () -> Numbers.add (Decimal.MaxValue, 1M) |> ignore) "add overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.add (Decimal.MaxValue :> obj, 1M :> obj) |> ignore)
                  "add overflows"

              // addP

              Expect.equal (Numbers.addP (1L, 2L)) 3L "1+2=3"
              Expect.equal (Numbers.addP (1UL, 2UL)) 3UL "1+2=3 unsigned"
              Expect.floatClose Accuracy.medium (Numbers.addP (1.2, 2.3)) 3.5 "1.2+2.3=3.5"
              Expect.equal (Numbers.addP (r1, r2)) r3 "2/3+4/5=22/15"
              Expect.equal (Numbers.addP (bi1, bi2)) bi3 "1+2=3 bigint"
              Expect.equal (Numbers.addP (bd1, bd2)) bd3 "1+2=3 bigdec"
              Expect.equal (Numbers.addP (1.2M, 2.3M)) 3.5M "1.2M+2.3M=3.5M"

              // Check indirect calls on primitive args
              Expect.equal (Numbers.addP (1L :> obj, 2L :> obj)) 3L "1+2=3"
              Expect.equal (Numbers.addP (1UL :> obj, 2UL :> obj)) 3UL "1+2=3 unsigned"
              Expect.floatClose Accuracy.medium ((Numbers.addP (1.2 :> obj, 2.3 :> obj)) :?> float) 3.5 "1.2+2.3=3.5"
              Expect.equal (Numbers.addP (1.2M :> obj, 2.3M :> obj)) 3.5M "1.2M+2.3M=3.5M"

              // Edge cases should promote
              Expect.equal (Numbers.addP (Int64.MaxValue, 1L)) biMVp1 "addP promotes"
              Expect.equal (Numbers.addP (Int64.MaxValue :> obj, 1L :> obj)) biMVp1 "addP promotes"
              Expect.equal (Numbers.addP (UInt64.MaxValue, 1UL)) biUMVp1 "addP promotes"
              Expect.equal (Numbers.addP (UInt64.MaxValue :> obj, 1UL :> obj)) biUMVp1 "addP promotes"
              Expect.equal (Numbers.addP (Decimal.MaxValue, 1M)) bdMVp1 "addP promotes"
              Expect.equal (Numbers.addP (Decimal.MaxValue :> obj, 1M :> obj)) bdMVp1 "addP promotes"

              // unchecked_add

              Expect.equal (Numbers.unchecked_add (1L, 2L)) 3L "1+2=3"
              Expect.equal (Numbers.unchecked_add (1UL, 2UL)) 3UL "1+2=3 unsigned"
              Expect.floatClose Accuracy.medium (Numbers.unchecked_add (1.2, 2.3)) 3.5 "1.2+2.3=3.5"
              Expect.equal (Numbers.unchecked_add (r1, r2)) r3 "2/3+4/5=22/15"
              Expect.equal (Numbers.unchecked_add (bi1, bi2)) bi3 "1+2=3 bigint"
              Expect.equal (Numbers.unchecked_add (bd1, bd2)) bd3 "1+2=3 bigdec"
              Expect.equal (Numbers.unchecked_add (1.2M, 2.3M)) 3.5M "1.2M+2.3M=3.5M"

              // Check indirect calls on primitive args
              Expect.equal (Numbers.unchecked_add (1L :> obj, 2L :> obj)) 3L "1+2=3"
              Expect.equal (Numbers.unchecked_add (1UL :> obj, 2UL :> obj)) 3UL "1+2=3 unsigned"

              Expect.floatClose
                  Accuracy.medium
                  ((Numbers.unchecked_add (1.2 :> obj, 2.3 :> obj)) :?> float)
                  3.5
                  "1.2+2.3=3.5"

              Expect.equal (Numbers.unchecked_add (1.2M :> obj, 2.3M :> obj)) 3.5M "1.2M+2.3M=3.5M"

              // Edge cases should promote
              Expect.equal (Numbers.unchecked_add (Int64.MaxValue, 1L)) Int64.MinValue "unchecked_add wraps around"

              Expect.equal
                  (Numbers.unchecked_add (Int64.MaxValue :> obj, 1L :> obj))
                  Int64.MinValue
                  "unchecked_add wraps around"

              Expect.equal (Numbers.unchecked_add (UInt64.MaxValue, 1UL)) 0UL "unchecked_add wraps around"
              Expect.equal (Numbers.unchecked_add (UInt64.MaxValue :> obj, 1UL :> obj)) 0UL "unchecked_add wraps around"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.unchecked_add (Decimal.MaxValue, 1M) |> ignore)
                  "unchecked_add on decimal overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.unchecked_add (Decimal.MaxValue :> obj, 1M :> obj) |> ignore)
                  "unchecked_add on decimal overflows"

          testCase "multiplication"
          <| fun _ ->
              let r1 = Ratio(BigInteger(2), BigInteger(3))
              let r2 = Ratio(BigInteger(4), BigInteger(5))
              let r3 = Ratio(BigInteger(8), BigInteger(15))

              let bi1 = BigInteger(2e10)
              let bi2 = BigInt.fromBigInteger (BigInteger(3e10))
              let bi3 = BigInt.fromBigInteger (BigInteger(6e20))

              let bd1 = BigDecimal.Create("1.2")
              let bd2 = BigDecimal.Create("2.3")
              let bd3 = BigDecimal.Create("2.76")

              //  9223372036854775807 * 2
              // 18446744073709551614
              let biMVm2 = BigInt.fromBigInteger (BigInteger(Int64.MaxValue) * BigInteger(2))

              // 18446744073709551615 * 2 =
              // 36893488147419103230
              let biUMVm2 = BigInt.fromBigInteger (BigInteger(UInt64.MaxValue) * BigInteger(2))

              //  79228162514264337593543950335 * 2 =
              // 158456325028528675187087900670
              let bdMVm2 = BigDecimal.Create("158456325028528675187087900670")

              let biNeg2 = BigInt.fromLong (-2)

              // multiply

              Expect.equal (Numbers.multiply (2L, 3L)) 6L "2*3=6"
              Expect.equal (Numbers.multiply (2UL, 3UL)) 6UL "2*3=6 unsigned"
              Expect.floatClose Accuracy.medium (Numbers.multiply (1.2, 2.3)) 2.76 "1.2*2.3=2.76"
              Expect.equal (Numbers.multiply (r1, r2)) r3 "2/3*4/5=8/15"
              Expect.equal (Numbers.multiply (bi1, bi2)) bi3 "2*3=6 bigint"
              Expect.equal (Numbers.multiply (bd1, bd2)) bd3 "2*3=6 bigdec"
              Expect.equal (Numbers.multiply (1.2M, 2.3M)) 2.76M "1.2*2.3=2.76 M"

              // Check indirect calls on primitive args
              Expect.equal (Numbers.multiply (2L :> obj, 3L :> obj)) 6L "2*3=6"
              Expect.equal (Numbers.multiply (2UL :> obj, 3UL :> obj)) 6UL "2*3=6 unsigned"

              Expect.floatClose
                  Accuracy.medium
                  ((Numbers.multiply (1.2 :> obj, 2.3 :> obj)) :?> float)
                  2.76
                  "1.2*2.3=2.76"

              Expect.equal (Numbers.multiply (1.2M :> obj, 2.3M :> obj)) 2.76M "1.2*2.3=2.76 M"

              // check edge cases
              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.multiply (Int64.MaxValue, 2L) |> ignore)
                  "multiply overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.multiply (Int64.MaxValue :> obj, 2L :> obj) |> ignore)
                  "multiply overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.multiply (UInt64.MaxValue, 2UL) |> ignore)
                  "multiply overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.multiply (UInt64.MaxValue :> obj, 2UL :> obj) |> ignore)
                  "multiply overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.multiply (Decimal.MaxValue, 2M) |> ignore)
                  "multiply overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.multiply (Decimal.MaxValue :> obj, 2M :> obj) |> ignore)
                  "multiply overflows"

              // multiplyP

              Expect.equal (Numbers.multiplyP (2L, 3L)) 6L "2*3=6"
              Expect.equal (Numbers.multiplyP (2UL, 3UL)) 6UL "2*3=6 unsigned"
              Expect.floatClose Accuracy.medium (Numbers.multiplyP (1.2, 2.3)) 2.76 "1.2*2.3=2.76"
              Expect.equal (Numbers.multiplyP (r1, r2)) r3 "2/3*4/5=8/15"
              Expect.equal (Numbers.multiplyP (bi1, bi2)) bi3 "2*3=6 bigint"
              Expect.equal (Numbers.multiplyP (bd1, bd2)) bd3 "2*3=6 bigdec"
              Expect.equal (Numbers.multiplyP (1.2M, 2.3M)) 2.76M "1.2*2.3=2.76 M"


              // Check indirect calls on primitive args
              Expect.equal (Numbers.multiplyP (2L :> obj, 3L :> obj)) 6L "2*3=6"
              Expect.equal (Numbers.multiplyP (2UL :> obj, 3UL :> obj)) 6UL "2*3=6 unsigned"

              Expect.floatClose
                  Accuracy.medium
                  ((Numbers.multiplyP (1.2 :> obj, 2.3 :> obj)) :?> float)
                  2.76
                  "1.2*2.3=2.76"

              Expect.equal (Numbers.multiplyP (1.2M :> obj, 2.3M :> obj)) 2.76M "1.2*2.3=2.76 M"

              // Edge cases should promote
              Expect.equal (Numbers.multiplyP (Int64.MaxValue, 2L)) biMVm2 "multiplyP promotes"
              Expect.equal (Numbers.multiplyP (Int64.MaxValue :> obj, 2L :> obj)) biMVm2 "multiplyP promotes"
              Expect.equal (Numbers.multiplyP (UInt64.MaxValue, 2UL)) biUMVm2 "multiplyP promotes"
              Expect.equal (Numbers.multiplyP (UInt64.MaxValue :> obj, 2UL :> obj)) biUMVm2 "multiplyP promotes"
              Expect.equal (Numbers.multiplyP (Decimal.MaxValue, 2M)) bdMVm2 "multiplyP promotes"
              Expect.equal (Numbers.multiplyP (Decimal.MaxValue :> obj, 2M :> obj)) bdMVm2 "multiplyP promotes"

              // unchecked_multiply

              Expect.equal (Numbers.unchecked_multiply (2L, 3L)) 6L "2*3=6"
              Expect.equal (Numbers.unchecked_multiply (2UL, 3UL)) 6UL "2*3=6 unsigned"
              Expect.floatClose Accuracy.medium (Numbers.unchecked_multiply (1.2, 2.3)) 2.76 "1.2*2.3=2.76"
              Expect.equal (Numbers.unchecked_multiply (r1, r2)) r3 "2/3+4/5=8/15"
              Expect.equal (Numbers.unchecked_multiply (bi1, bi2)) bi3 "2*3=6 bigint"
              Expect.equal (Numbers.unchecked_multiply (bd1, bd2)) bd3 "2*3=6 bigdec"
              Expect.equal (Numbers.unchecked_multiply (1.2M, 2.3M)) 2.76M "1.2*2.3=2.76 M"

              // Check indirect calls on primitive args
              Expect.equal (Numbers.unchecked_multiply (2L :> obj, 3L :> obj)) 6L "2*3=6"
              Expect.equal (Numbers.unchecked_multiply (2UL :> obj, 3UL :> obj)) 6UL "2*3=6 unsigned"

              Expect.floatClose
                  Accuracy.medium
                  ((Numbers.unchecked_multiply (1.2 :> obj, 2.3 :> obj)) :?> float)
                  2.76
                  "1.2*2.3=2.76"

              Expect.equal (Numbers.unchecked_multiply (1.2M :> obj, 2.3M :> obj)) 2.76M "1.2*2.3=2.76 M"

              // Edge cases should promote
              Expect.equal (Numbers.unchecked_multiply (Int64.MaxValue, 2L)) -2L "unchecked_multiply wraps around"

              Expect.equal
                  (Numbers.unchecked_multiply (Int64.MaxValue :> obj, 2L :> obj))
                  -2L
                  "unchecked_multiply wraps around"

              Expect.equal
                  (Numbers.unchecked_multiply (UInt64.MaxValue, 2UL))
                  (UInt64.MaxValue - 1UL)
                  "unchecked_multiply wraps around"

              Expect.equal
                  (Numbers.unchecked_multiply (UInt64.MaxValue :> obj, 2UL :> obj))
                  ((UInt64.MaxValue - 1UL) :> obj)
                  "unchecked_multiply wraps around"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.unchecked_multiply (Decimal.MaxValue, 2M) |> ignore)
                  "unchecked_multiply on decimal overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.unchecked_multiply (Decimal.MaxValue :> obj, 2M :> obj) |> ignore)
                  "unchecked_multiply on decimal overflows"

          testCase "subtraction"
          <| fun _ ->
              let r1 = Ratio(BigInteger(22), BigInteger(15))
              let r2 = Ratio(BigInteger(4), BigInteger(5))
              let r3 = Ratio(BigInteger(2), BigInteger(3))


              let bi3 = BigInt.fromBigInteger (BigInteger(1e10))
              let bi2 = BigInteger(2e10)
              let bi1 = BigInt.fromBigInteger (BigInteger(3e10))

              let bd3 = BigDecimal.Create("11111111111111111.1111111111111")
              let bd2 = BigDecimal.Create("22222222222222222.2222222222222")
              let bd1 = BigDecimal.Create("33333333333333333.3333333333333")

              let biMVm1 = BigInt.fromBigInteger (BigInteger(Int64.MinValue) - BigInteger.One)
              let biUMVm1 = BigInt.fromBigInteger (BigInteger(UInt64.MinValue) - BigInteger.One)
              let bdMVm1 = BigDecimal.Create(Decimal.MinValue) - BigDecimal.Create(1)

              // minus

              Expect.equal (Numbers.minus (3L, 2L)) 1L "3-2=1"
              Expect.equal (Numbers.minus (3UL, 2UL)) 1UL "3-2=1 unsigned"
              Expect.floatClose Accuracy.medium (Numbers.minus (3.5, 2.3)) 1.2 "3.5-2.3=1.2"
              Expect.equal (Numbers.minus (r1, r2)) r3 "22/15-4/5=2/3"
              Expect.equal (Numbers.minus (bi1, bi2)) bi3 "3-2=1 bigint"
              Expect.equal (Numbers.minus (bd1, bd2)) bd3 "3-2=1 bigdec"
              Expect.equal (Numbers.minus (3.5M, 2.3M)) 1.2M "3.5-2.3=1.2 M"

              // Check indirect calls on primitive args
              Expect.equal (Numbers.minus (3L :> obj, 2L :> obj)) 1L "3-2=1"
              Expect.equal (Numbers.minus (3UL :> obj, 2UL :> obj)) 1UL "3-2=1 unsigned"
              Expect.floatClose Accuracy.medium ((Numbers.minus (3.5 :> obj, 2.3 :> obj)) :?> float) 1.2 "3.5-2.3=1.2"
              Expect.equal (Numbers.minus (3.5M :> obj, 2.3M :> obj)) 1.2M "3.5-2.3=1.2 M"

              // check edge cases
              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.minus (Int64.MinValue, 1L) |> ignore)
                  "minus overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.minus (Int64.MinValue :> obj, 1L :> obj) |> ignore)
                  "minus overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.minus (UInt64.MinValue, 1UL) |> ignore)
                  "minus overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.minus (UInt64.MinValue :> obj, 1UL :> obj) |> ignore)
                  "minus overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.minus (Decimal.MinValue, 1M) |> ignore)
                  "minus overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.minus (Decimal.MinValue :> obj, 1M :> obj) |> ignore)
                  "minus overflows"

              // minusP

              Expect.equal (Numbers.minusP (3L, 2L)) 1L "3-2=1"
              Expect.equal (Numbers.minusP (3UL, 2UL)) 1UL "3-2=1 unsigned"
              Expect.floatClose Accuracy.medium (Numbers.minusP (3.5, 2.3)) 1.2 "3.5-2.3=1.2"
              Expect.equal (Numbers.minusP (r1, r2)) r3 "22/15-4/5=2/3"
              Expect.equal (Numbers.minusP (bi1, bi2)) bi3 "3-2=1 bigint"
              Expect.equal (Numbers.minusP (bd1, bd2)) bd3 "3-2=1 bigdec"
              Expect.equal (Numbers.minusP (3.5M, 2.3M)) 1.2M "3.5-2.3=1.2 M"

              // Check indirect calls on primitive args
              Expect.equal (Numbers.minusP (3L :> obj, 2L :> obj)) 1L "3-2=1"
              Expect.equal (Numbers.minusP (3UL :> obj, 2UL :> obj)) 1UL "3-2=1 unsigned"
              Expect.floatClose Accuracy.medium ((Numbers.minusP (3.5 :> obj, 2.3 :> obj)) :?> float) 1.2 "3.5-2.3=1.2"
              Expect.equal (Numbers.minusP (3.5M :> obj, 2.3M :> obj)) 1.2M "3.5-2.3=1.2 M"


              // Edge cases should promote
              Expect.equal (Numbers.minusP (Int64.MinValue, 1L)) biMVm1 "minusP promotes"
              Expect.equal (Numbers.minusP (Int64.MinValue :> obj, 1L :> obj)) biMVm1 "minusP promotes"
              Expect.equal (Numbers.minusP (UInt64.MinValue, 1UL)) biUMVm1 "minusP promotes"
              Expect.equal (Numbers.minusP (UInt64.MinValue :> obj, 1UL :> obj)) biUMVm1 "minusP promotes"
              Expect.equal (Numbers.minusP (Decimal.MinValue, 1M)) bdMVm1 "minusP promotes"
              Expect.equal (Numbers.minusP (Decimal.MinValue :> obj, 1M :> obj)) bdMVm1 "minusP promotes"

              // unchecked_minus


              Expect.equal (Numbers.unchecked_minus (3L, 2L)) 1L "3-2=1"
              Expect.equal (Numbers.unchecked_minus (3UL, 2UL)) 1UL "3-2=1 unsigned"
              Expect.floatClose Accuracy.medium (Numbers.unchecked_minus (3.5, 2.3)) 1.2 "3.5-2.3=1.2"
              Expect.equal (Numbers.unchecked_minus (r1, r2)) r3 "22/15-4/5=2/3"
              Expect.equal (Numbers.unchecked_minus (bi1, bi2)) bi3 "3-2=1 bigint"
              Expect.equal (Numbers.unchecked_minus (bd1, bd2)) bd3 "3-2=1 bigdec"
              Expect.equal (Numbers.unchecked_minus (3.5M, 2.3M)) 1.2M "3.5-2.3=1.2 M"


              // Check indirect calls on primitive args
              Expect.equal (Numbers.unchecked_minus (3L :> obj, 2L :> obj)) 1L "3-2=1"
              Expect.equal (Numbers.unchecked_minus (3UL :> obj, 2UL :> obj)) 1UL "3-2=1 unsigned"

              Expect.floatClose
                  Accuracy.medium
                  ((Numbers.unchecked_minus (3.5 :> obj, 2.3 :> obj)) :?> float)
                  1.2
                  "3.5-2.3=1.2"

              Expect.equal (Numbers.unchecked_minus (3.5M :> obj, 2.3M :> obj)) 1.2M "3.5-2.3=1.2 M"

              // Edge cases should promote
              Expect.equal (Numbers.unchecked_minus (Int64.MinValue, 1L)) Int64.MaxValue "unchecked_minus wraps around"

              Expect.equal
                  (Numbers.unchecked_minus (Int64.MinValue :> obj, 1L :> obj))
                  Int64.MaxValue
                  "unchecked_minus wraps around"

              Expect.equal (Numbers.unchecked_minus (0UL, 1UL)) UInt64.MaxValue "unchecked_minus wraps around"

              Expect.equal
                  (Numbers.unchecked_minus (0UL :> obj, 1UL :> obj))
                  UInt64.MaxValue
                  "unchecked_minus wraps around"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.unchecked_minus (Decimal.MinValue, 1M) |> ignore)
                  "unchecked_minus on decimal overflows"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.unchecked_minus (Decimal.MinValue :> obj, 1M :> obj) |> ignore)
                  "unchecked_minus on decimal overflows"


          testCase "unary minus"
          <| fun _ ->

              let biMinVm = BigInt.fromBigInteger (- BigInteger(Int64.MinValue))
              let bi = BigInteger(1e30)
              let bim = BigInteger(-1e30)
              let bi2 = BigInteger(3e50)
              let r = Ratio(bi, bi2)
              let rm = Ratio(bim, bi2)
              let bd = BigDecimal.Create("111111111111.11111111111")
              let bdm = BigDecimal.Create("-111111111111.11111111111")
              let bbi = BigInt.fromBigInteger (bi)
              let bbim = BigInt.fromBigInteger (bim)

              Expect.equal (Numbers.minus (12.0)) -12.0 "12 -> -12 D"
              Expect.equal (Numbers.minus (12L)) -12L "12 -> -12 L"
              Expect.equal (Numbers.minus (0UL)) 0UL "0 -> 0 UL"
              Expect.equal (Numbers.minus (12M)) -12M " 12 -> -12 M)"
              Expect.throwsT<ArithmeticException> (fun () -> Numbers.minus (12UL) |> ignore) "Can't negate UL"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.minus (Int64.MinValue) |> ignore)
                  "Can't negate L -inf"

              Expect.equal (Numbers.minus (12.0 :> obj)) -12.0 "12 -> -12 D"
              Expect.equal (Numbers.minus (12L :> obj)) -12L "12 -> -12 L"
              Expect.equal (Numbers.minus (0UL :> obj)) 0UL "0 -> 0 UL"
              Expect.equal (Numbers.minus (12M :> obj)) -12M " 12 -> -12 M)"
              Expect.throwsT<ArithmeticException> (fun () -> Numbers.minus (12UL :> obj) |> ignore) "Can't negate UL"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.minus (Int64.MinValue :> obj) |> ignore)
                  "Can't negate L -inf"

              Expect.equal (Numbers.minusP (12.0)) -12.0 "12 -> -12 D"
              Expect.equal (Numbers.minusP (12L)) -12L "12 -> -12 L"
              Expect.equal (Numbers.minusP (0UL)) 0UL "0 -> 0 UL"
              Expect.equal (Numbers.minusP (12M)) -12M " 12 -> -12 M)"
              Expect.equal (Numbers.minusP (Int64.MinValue)) biMinVm "-inf(L) -> BI"
              Expect.equal (Numbers.minusP (12UL)) (BigInt.fromLong (-12)) "12 UL -> -12 BI"

              Expect.equal (Numbers.minusP (12.0 :> obj)) -12.0 "12 -> -12 D"
              Expect.equal (Numbers.minusP (12L :> obj)) -12L "12 -> -12 L"
              Expect.equal (Numbers.minusP (0UL :> obj)) 0UL "0 -> 0 UL"
              Expect.equal (Numbers.minusP (12M :> obj)) -12M " 12 -> -12 M"
              Expect.equal (Numbers.minusP (Int64.MinValue :> obj)) biMinVm "-inf(L) -> BI"
              Expect.equal (Numbers.minusP (12UL :> obj)) (BigInt.fromLong (-12)) "12 UL -> -12 BI"

              Expect.equal (Numbers.unchecked_minus (12.0)) -12.0 "12 -> -12 D"
              Expect.equal (Numbers.unchecked_minus (12L)) -12L "12 -> -12 L"
              Expect.equal (Numbers.unchecked_minus (0UL)) 0UL "0 -> 0 UL"
              Expect.equal (Numbers.unchecked_minus (12M)) -12M " 12 -> -12 M)"
              Expect.throwsT<ArithmeticException> (fun () -> Numbers.unchecked_minus (12UL) |> ignore) "Can't negate UL"
              Expect.equal (Numbers.unchecked_minus (Int64.MinValue)) Int64.MinValue "Can't negate L -inf"

              Expect.equal (Numbers.unchecked_minus (12.0 :> obj)) -12.0 "12 -> -12 D"
              Expect.equal (Numbers.unchecked_minus (12L :> obj)) -12L "12 -> -12 L"
              Expect.equal (Numbers.unchecked_minus (0UL :> obj)) 0UL "0 -> 0 UL"
              Expect.equal (Numbers.unchecked_minus (12M :> obj)) -12M " 12 -> -12 M)"

              Expect.throwsT<ArithmeticException>
                  (fun () -> Numbers.unchecked_minus (12UL :> obj) |> ignore)
                  "Can't negate UL"

              Expect.equal (Numbers.unchecked_minus (Int64.MinValue) :> obj) Int64.MinValue "Can't negate L -inf"

              Expect.equal (Numbers.minus (bbi)) bbim "BI minus"
              Expect.equal (Numbers.minus (r)) rm "Ration minus"
              Expect.equal (Numbers.minus (bd)) bdm "BD minus"

              Expect.equal (Numbers.minusP (bbi)) bbim "BI minus"
              Expect.equal (Numbers.minusP (r)) rm "Ration minus"
              Expect.equal (Numbers.minusP (bd)) bdm "BD minus"

              Expect.equal (Numbers.unchecked_minus (bbi)) bbim "BI minus"
              Expect.equal (Numbers.unchecked_minus (r)) rm "Ration minus"
              Expect.equal (Numbers.unchecked_minus (bd)) bdm "BD minus"

          testCase "inc"
          <| fun _ ->

              let biMaxVp1 = BigInt.fromBigInteger (BigInteger(Int64.MaxValue)+BigInteger.One)
              let biUMaxVp1 = BigInt.fromBigInteger (BigInteger(UInt64.MaxValue)+BigInteger.One)
 
              Expect.equal (Numbers.inc 12.0) 13.0 "12 -> 13 D"
              Expect.equal (Numbers.inc 12L) 13L "12 -> 13 L"
              Expect.equal (Numbers.inc 12UL) 13UL "12 -> 13 UL"
              Expect.equal (Numbers.inc 12M) 13M " 12 -> 13 M)"
 
              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.inc (Int64.MaxValue) |> ignore)
                  "Can't inc max"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.inc (UInt64.MaxValue) |> ignore)
                  "Can't inc max"

              Expect.equal (Numbers.inc (12.0 :> obj)) 13.0 "12 -> 13 D"
              Expect.equal (Numbers.inc (12L :> obj)) 13L "12 -> 13 L"
              Expect.equal (Numbers.inc (12UL :> obj)) 13UL "12 -> 13 UL"
              Expect.equal (Numbers.inc (12M :> obj)) 13M " 12 -> 13 M)"
 
              Expect.throwsT<OverflowException>
                  (fun () -> (Numbers.inc (Int64.MaxValue :> obj)) |> ignore )
                  "Can't inc max"

              Expect.throwsT<OverflowException>
                  (fun () -> (Numbers.inc (UInt64.MaxValue :> obj)) |> ignore)
                  "Can't inc max"


              Expect.equal (Numbers.incP 12.0) 13.0 "12 -> 13 D"
              Expect.equal (Numbers.incP 12L) 13L "12 -> 13 L"
              Expect.equal (Numbers.incP 12UL) 13UL "12 -> 13 UL"
              Expect.equal (Numbers.incP 12M) 13M " 12 -> 13 M)"

              Expect.equal (Numbers.incP Int64.MaxValue) biMaxVp1 "inc MaxValue -> bigint"
              Expect.equal (Numbers.incP UInt64.MaxValue) biUMaxVp1 "inc MaxValue -> bigint"
 
 
              Expect.equal (Numbers.incP (12.0 :> obj)) 13.0 "12 -> 13 D"
              Expect.equal (Numbers.incP (12L :> obj)) 13L "12 -> 13 L"
              Expect.equal (Numbers.incP (12UL :> obj)) 13UL "12 -> 13 UL"
              Expect.equal (Numbers.incP (12M :> obj)) 13M " 12 -> 13 M)"

              Expect.equal (Numbers.incP (Int64.MaxValue  :> obj)) biMaxVp1 "inc MaxValue -> bigint"
              Expect.equal (Numbers.incP (UInt64.MaxValue :> obj)) biUMaxVp1 "inc MaxValue -> bigint"
 

              Expect.equal (Numbers.unchecked_inc 12.0) 13.0 "12 -> 13 D"
              Expect.equal (Numbers.unchecked_inc 12L) 13L "12 -> 13 L"
              Expect.equal (Numbers.unchecked_inc 12UL) 13UL "12 -> 13 UL"
              Expect.equal (Numbers.unchecked_inc 12M) 13M " 12 -> 13 M)"

              Expect.equal (Numbers.unchecked_inc Int64.MaxValue) Int64.MinValue "inc MaxValue -> overflow"
              Expect.equal (Numbers.unchecked_inc UInt64.MaxValue) 0UL "inc MaxValue -> overflow"
 
 
              Expect.equal (Numbers.unchecked_inc (12.0 :> obj)) 13.0 "12 -> 13 D"
              Expect.equal (Numbers.unchecked_inc (12L :> obj)) 13L "12 -> 13 L"
              Expect.equal (Numbers.unchecked_inc (12UL :> obj)) 13UL "12 -> 13 UL"
              Expect.equal (Numbers.unchecked_inc (12M :> obj)) 13M " 12 -> 13 M)"

              Expect.equal (Numbers.unchecked_inc (Int64.MaxValue  :> obj)) Int64.MinValue "inc MaxValue -> overflow"
              Expect.equal (Numbers.unchecked_inc (UInt64.MaxValue :> obj)) 0UL "inc MaxValue -> overflow"

              let b = BigInteger(1e30)
              let bp1 = b + BigInteger.One
              let b7 = BigInteger(7)

              let r = Ratio(b,b7)
              let rp1 = Ratio(b+b7,b7)

              let bd = BigDecimal.Create("111111111111.11111111111")
              let bdp1 = BigDecimal.Create("111111111112.11111111111")

              Expect.equal (Numbers.inc b) (BigInt.fromBigInteger(bp1)) "BI inc"
              Expect.equal (Numbers.inc r) rp1 "Ratio inc"
              Expect.equal (Numbers.inc bd) bdp1 "BD inc"

              Expect.equal (Numbers.incP b) (BigInt.fromBigInteger(bp1)) "BI inc"
              Expect.equal (Numbers.incP r) rp1 "Ratio inc"
              Expect.equal (Numbers.incP bd) bdp1 "BD inc"

              Expect.equal (Numbers.unchecked_inc b) (BigInt.fromBigInteger(bp1)) "BI inc"
              Expect.equal (Numbers.unchecked_inc r) rp1 "Ratio inc"
              Expect.equal (Numbers.unchecked_inc bd) bdp1 "BD inc"

          testCase "dec"
          <| fun _ ->

              let biMinVm1 = BigInt.fromBigInteger (BigInteger(Int64.MinValue)-BigInteger.One)
              let biUMinVm1 = BigInt.fromBigInteger (BigInteger(UInt64.MinValue)-BigInteger.One)
 
              Expect.equal (Numbers.dec 12.0) 11.0 "12 -> 11 D"
              Expect.equal (Numbers.dec 12L) 11L "12 -> 11 L"
              Expect.equal (Numbers.dec 12UL) 11UL "12 -> 11 UL"
              Expect.equal (Numbers.dec 12M) 11M " 12 -> 11 M)"
 
              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.dec (Int64.MinValue) |> ignore)
                  "Can't dec min"

              Expect.throwsT<OverflowException>
                  (fun () -> Numbers.dec (UInt64.MinValue) |> ignore)
                  "Can't dec min"

              Expect.equal (Numbers.dec (12.0 :> obj)) 11.0 "12 -> 11 D"
              Expect.equal (Numbers.dec (12L :> obj)) 11L "12 -> 11 L"
              Expect.equal (Numbers.dec (12UL :> obj)) 11UL "12 -> 11 UL"
              Expect.equal (Numbers.dec (12M :> obj)) 11M " 12 -> 11 M)"
 
              Expect.throwsT<OverflowException>
                  (fun () -> (Numbers.dec (Int64.MinValue :> obj)) |> ignore )
                  "Can't dec min"

              Expect.throwsT<OverflowException>
                  (fun () -> (Numbers.dec (UInt64.MinValue :> obj)) |> ignore)
                  "Can't dec min"


              Expect.equal (Numbers.decP 12.0) 11.0 "12 -> 11 D"
              Expect.equal (Numbers.decP 12L) 11L "12 -> 11 L"
              Expect.equal (Numbers.decP 12UL) 11UL "12 -> 11 UL"
              Expect.equal (Numbers.decP 12M) 11M " 12 -> 11 M)"

              Expect.equal (Numbers.decP Int64.MinValue) biMinVm1 "dec MinValue -> bigint"
              Expect.equal (Numbers.decP UInt64.MinValue) biUMinVm1 "dec MinValue -> bigint"
 
 
              Expect.equal (Numbers.decP (12.0 :> obj)) 11.0 "12 -> 11 D"
              Expect.equal (Numbers.decP (12L :> obj)) 11L "12 -> 11 L"
              Expect.equal (Numbers.decP (12UL :> obj)) 11UL "12 -> 11 UL"
              Expect.equal (Numbers.decP (12M :> obj)) 11M " 12 -> 11 M)"

              Expect.equal (Numbers.decP (Int64.MinValue :> obj)) biMinVm1 "dec MinValue -> bigint"
              Expect.equal (Numbers.decP (UInt64.MinValue :> obj)) biUMinVm1 "dec MinValue -> bigint"
 

              Expect.equal (Numbers.unchecked_dec 12.0) 11.0 "12 -> 11 D"
              Expect.equal (Numbers.unchecked_dec 12L) 11L "12 -> 11 L"
              Expect.equal (Numbers.unchecked_dec 12UL) 11UL "12 -> 11 UL"
              Expect.equal (Numbers.unchecked_dec 12M) 11M " 12 -> 11 M)"

              Expect.equal (Numbers.unchecked_dec Int64.MinValue) Int64.MaxValue "dec MaxValue -> overflow"
              Expect.equal (Numbers.unchecked_dec 0UL) UInt64.MaxValue "dec MaxValue -> overflow"
 
 
              Expect.equal (Numbers.unchecked_dec (12.0 :> obj)) 11.0 "12 -> 11 D"
              Expect.equal (Numbers.unchecked_dec (12L :> obj)) 11L "12 -> 11 L"
              Expect.equal (Numbers.unchecked_dec (12UL :> obj)) 11UL "12 -> 11 UL"
              Expect.equal (Numbers.unchecked_dec (12M :> obj)) 11M " 12 -> 11 M)"

              Expect.equal (Numbers.unchecked_dec (Int64.MinValue :> obj)) Int64.MaxValue "dec MaxValue -> overflow"
              Expect.equal (Numbers.unchecked_dec (0UL :> obj)) UInt64.MaxValue "dec MaxValue -> overflow"

              let b = BigInteger(1e30)
              let bm1 = b - BigInteger.One
              let b7 = BigInteger(7)

              let r = Ratio(b,b7)
              let rp1 = Ratio(b-b7,b7)

              let bd = BigDecimal.Create("111111111111.11111111111")
              let bdp1 = BigDecimal.Create("111111111110.11111111111")

              Expect.equal (Numbers.dec b) (BigInt.fromBigInteger(bm1)) "BI dec"
              Expect.equal (Numbers.dec r) rp1 "Ratio dec"
              Expect.equal (Numbers.dec bd) bdp1 "BD dec"

              Expect.equal (Numbers.decP b) (BigInt.fromBigInteger(bm1)) "BI dec"
              Expect.equal (Numbers.decP r) rp1 "Ratio dec"
              Expect.equal (Numbers.decP bd) bdp1 "BD dec"

              Expect.equal (Numbers.unchecked_dec b) (BigInt.fromBigInteger(bm1)) "BI dec"
              Expect.equal (Numbers.unchecked_dec r) rp1 "Ratio dec"
              Expect.equal (Numbers.unchecked_dec bd) bdp1 "BD dec"

          testCase "isPos, isZero, isNeg"
          <| fun _ ->

              let snums: obj array array =
                  [| [| 2y; 0y; -2y |]
                     [| 2s; 0s; -2s |]
                     [| 2; 0; -2 |]
                     [| 2L; 0L; -2L |]
                     [| 2M; 0M; -2M |]
                     [| BigInteger(2); BigInteger.Zero; BigInteger(-2) |]
                     [| BigDecimal.Create(2); BigDecimal.Zero; BigDecimal.Create(-2) |]
                     [| Ratio(BigInteger(2), BigInteger(1))
                        Ratio(BigInteger(0), BigInteger(1))
                        Ratio(BigInteger(-2), BigInteger(1)) |] |]

              let unums: obj array array =
                  [| [| 2uy :> obj; 0uy |]; [| 2us :> obj; 0us |]; [| 2u; 0u |]; [| 2UL; 0UL |] |]

              for a in snums do
                  let vs = Array.map (fun v -> Numbers.isPos (v :> obj)) a
                  Expect.equal vs [| true; false; false |] "Checking parity"

              for a in snums do
                  let vs = Array.map (fun v -> Numbers.isZero (v :> obj)) a
                  Expect.equal vs [| false; true; false |] "Checking parity"

              for a in snums do
                  let vs = Array.map (fun v -> Numbers.isNeg (v :> obj)) a
                  Expect.equal vs [| false; false; true |] "Checking parity"

              for a in unums do
                  let vs = Array.map (fun v -> Numbers.isPos (v :> obj)) a
                  Expect.equal vs [| true; false |] "Checking parity"

              for a in unums do
                  let vs = Array.map (fun v -> Numbers.isZero (v :> obj)) a
                  Expect.equal vs [| false; true |] "Checking parity"

              for a in unums do
                  let vs = Array.map (fun v -> Numbers.isNeg (v :> obj)) a
                  Expect.equal vs [| false; false |] "Checking parity"

          testCase "divide by zero"
          <| fun _ ->
              let lf () = Numbers.divide (2L, 0L) |> ignore
              let ulf () = Numbers.divide (2UL, 0UL) |> ignore

              let decf () = Numbers.divide (2M, 0M) |> ignore

              let rf () =
                  Numbers.divide (Ratio(BigInteger(2), BigInteger.One), BigInteger.Zero) |> ignore

              let bif () =
                  Numbers.divide (BigInteger.One, BigInteger.Zero) |> ignore

              let bdf () =
                  Numbers.divide (BigDecimal.One, BigDecimal.Zero) |> ignore

              Expect.throwsT<ArithmeticException> lf "throws on divide by zero"
              Expect.throwsT<ArithmeticException> ulf "throws on divide by zero"
              Expect.throwsT<ArithmeticException> decf "throws on divide by zero"
              Expect.throwsT<ArithmeticException> rf "throws on divide by zero"
              Expect.throwsT<ArithmeticException> bif "throws on divide by zero"
              Expect.throwsT<ArithmeticException> bdf "throws on divide by zero"

              let df () = Numbers.divide (2.0, 0.0)
              Expect.equal (df ()) Double.PositiveInfinity "guess who doesn'tthrows on divide by zero"

          testCase "divide - regular cases"
          <| fun _ ->

              Expect.floatClose Accuracy.medium (Numbers.divide (3.0, 1.5)) 2.0 "3.0/1.5"
              Expect.equal (Numbers.divide (3.0M, 1.5M)) 2.0M "3.0/1.5 M"

              Expect.equal
                  (Numbers.divide (BigDecimal.Create("3.0"), BigDecimal.Create("1.5")))
                  (BigDecimal.Create("2"))
                  "3.0/1.5 = 2 BigDec"

              Expect.equal (Numbers.divide (6L, 2L)) 3L "6/2 L"
              Expect.equal (Numbers.divide (6UL, 2UL)) 3UL "6/2 UL"

              let r5d2 = Ratio(BigInteger(5), BigInteger(2))
              Expect.equal (Numbers.divide (5L, 2L)) r5d2 "5/2 L"
              Expect.equal (Numbers.divide (5UL, 2UL)) r5d2 "5/2 UL"

              // Primitives in through obj interface
              Expect.floatClose Accuracy.medium (Numbers.divide (3.0, 1.5)) 2.0 "3.0/1.5"
              Expect.equal (Numbers.divide (3.0M :> obj, 1.5M :> obj)) 2.0M "3.0/1.5 M"
              Expect.equal (Numbers.divide (6L :> obj, 2L :> obj)) 3L "6/2 L"
              Expect.equal (Numbers.divide (6UL :> obj, 2UL :> obj)) 3UL "6/2 UL"
              Expect.equal (Numbers.divide (5L :> obj, 2L :> obj)) r5d2 "5/2 L"
              Expect.equal (Numbers.divide (5UL :> obj, 2UL :> obj)) r5d2 "5/2 UL"

          testCase "divide - ratios"
          <| fun _ ->
              let r45 = Ratio(BigInteger(4), BigInteger(5))
              let r23 = Ratio(BigInteger(2), BigInteger(3))
              let r43 = Ratio(BigInteger(4), BigInteger(3))
              let r65 = Ratio(BigInteger(6), BigInteger(5))
              let bi2 = BigInt.fromLong (2)

              Expect.equal (Numbers.divide (r45, r23)) r65 "Regular ratio"
              Expect.equal (Numbers.divide (r43, r23)) bi2 "Ratio reduces"

          testCase "divide - NaN"
          <| fun _ ->

              Expect.isTrue (Double.IsNaN(Numbers.divide (2.0, Double.NaN))) "Nan in, Nan out"
              Expect.isTrue (Double.IsNaN(Numbers.divide (Double.NaN, 2.0))) "Nan in, Nan out"
              Expect.isTrue (Double.IsNaN(Numbers.divide (2UL, Double.NaN))) "Nan in, Nan out"
              Expect.isTrue (Double.IsNaN(Numbers.divide (Double.NaN, 2UL))) "Nan in, Nan out"

          testCase "quotient"
          <| fun _ ->
              // these examples taken directly from the Clojure testing code

              let r12 = Ratio(BigInteger(1), BigInteger(2))
              let r23 = Ratio(BigInteger(2), BigInteger(3))
              let bi4 = BigInteger(4)
              let bi1 = BigInteger.One

              Expect.equal (Numbers.quotient (4L, 2L)) 2L "x"
              Expect.equal (Numbers.quotient (3L, 2L)) 1L "x"
              Expect.equal (Numbers.quotient (6L, 4L)) 1L "x"
              Expect.equal (Numbers.quotient (0L, 5L)) 0L "x"

              Expect.equal (Numbers.quotient (2L, r12)) bi4 "x"
              Expect.equal (Numbers.quotient (r23, r12)) bi1 "x"
              Expect.equal (Numbers.quotient (1L, r23)) bi1 "x"

              Expect.equal (Numbers.quotient (4.0, 2.0)) 2.0 "x"
              Expect.equal (Numbers.quotient (4.5, 2.0)) 2.0 "x"

              Expect.equal (Numbers.quotient (42L, 5L)) 8L "x"
              Expect.equal (Numbers.quotient (42L, -5L)) -8L "x"
              Expect.equal (Numbers.quotient (-42L, 5L)) -8L "x"
              Expect.equal (Numbers.quotient (-42L, -5L)) 8L "x"

              Expect.equal (Numbers.quotient (9L, 3L)) 3L "x"
              Expect.equal (Numbers.quotient (9L, -3L)) -3L "x"
              Expect.equal (Numbers.quotient (-9L, 3L)) -3L "x"
              Expect.equal (Numbers.quotient (-9L, -3L)) 3L "x"

              Expect.equal (Numbers.quotient (2L, 5L)) 0L "x"
              Expect.equal (Numbers.quotient (2L, -5L)) 0L "x"
              Expect.equal (Numbers.quotient (-2L, 5L)) 0L "x"
              Expect.equal (Numbers.quotient (-2L, -5L)) 0L "x"

              Expect.equal (Numbers.quotient (0L, 3L)) 0L "x"
              Expect.equal (Numbers.quotient (0L, -3L)) 0L "x"


              // and some extra tests
              Expect.equal (Numbers.quotient (4UL, 2UL)) 2UL "x"
              Expect.equal (Numbers.quotient (3UL, 2UL)) 1UL "x"
              Expect.equal (Numbers.quotient (6UL, 4UL)) 1UL "x"
              Expect.equal (Numbers.quotient (0UL, 5UL)) 0UL "x"

              Expect.equal (Numbers.quotient (4.0M, 2.0M)) 2.0M "x"
              Expect.equal (Numbers.quotient (4.5M, 2.0M)) 2.0M "x"

              Expect.throwsT<ArithmeticException> (fun () -> (Numbers.quotient (1.0, 0.0)) |> ignore) "throws"
              Expect.throwsT<ArithmeticException> (fun () -> (Numbers.quotient (1.0M, 0.0M)) |> ignore) "throws"



          testCase "remainder"
          <| fun _ ->
              // these examples taken directly from the Clojure testing code

              let r12 = Ratio(BigInteger(1), BigInteger(2))
              let r23 = Ratio(BigInteger(2), BigInteger(3))
              let r16 = Ratio(BigInteger(1), BigInteger(6))
              let r13 = Ratio(BigInteger(1), BigInteger(3))
              let bi4 = BigInteger(4)
              let bi0 = BigInt.fromLong(0)


              Expect.equal (Numbers.remainder (4L, 2L)) 0L "x"
              Expect.equal (Numbers.remainder (3L, 2L)) 1L "x"
              Expect.equal (Numbers.remainder (6L, 4L)) 2L "x"
              Expect.equal (Numbers.remainder (0L, 5L)) 0L "x"

              Expect.equal (Numbers.remainder (2L, r12)) bi0 "x"
              Expect.equal (Numbers.remainder (r23, r12)) r16 "x"
              Expect.equal (Numbers.remainder (1L, r23)) r13 "x"

              Expect.equal (Numbers.remainder (4.0, 2.0)) 0.0 "x"
              Expect.equal (Numbers.remainder (4.5, 2.0)) 0.5 "x"

              Expect.equal (Numbers.remainder (42L, 5L)) 2L "x"
              Expect.equal (Numbers.remainder (42L, -5L)) 2L "x"
              Expect.equal (Numbers.remainder (-42L, 5L)) -2L "x"
              Expect.equal (Numbers.remainder (-42L, -5L)) -2L "x"

              Expect.equal (Numbers.remainder (9L, 3L)) 0L "x"
              Expect.equal (Numbers.remainder (9L, -3L)) 0L "x"
              Expect.equal (Numbers.remainder (-9L, 3L)) 0L "x"
              Expect.equal (Numbers.remainder (-9L, -3L)) 0L "x"

              Expect.equal (Numbers.remainder (2L, 5L)) 2L "x"
              Expect.equal (Numbers.remainder (2L, -5L)) 2L "x"
              Expect.equal (Numbers.remainder (-2L, 5L)) -2L "x"
              Expect.equal (Numbers.remainder (-2L, -5L)) -2L "x"

              Expect.equal (Numbers.remainder (0L, 3L)) 0L "x"
              Expect.equal (Numbers.remainder (0L, -3L)) 0L "x"


              // and some extra tests
              Expect.equal (Numbers.remainder (4UL, 2UL)) 0UL "x"
              Expect.equal (Numbers.remainder (3UL, 2UL)) 1UL "x"
              Expect.equal (Numbers.remainder (6UL, 4UL)) 2UL "x"
              Expect.equal (Numbers.remainder (0UL, 5UL)) 0UL "x"

              Expect.equal (Numbers.remainder (4.0M, 2.0M)) 0.0M "x"
              Expect.equal (Numbers.remainder (4.5M, 2.0M)) 0.5M "x"

              Expect.throwsT<ArithmeticException> (fun () -> (Numbers.remainder (1.0, 0.0)) |> ignore) "throws"
              Expect.throwsT<ArithmeticException> (fun () -> (Numbers.quotient (1.0M, 0.0M)) |> ignore) "throws"



          testCase "test bit-shift-left"
          <| fun _ ->
              // these examples taken directly from the Clojure testing code

              Expect.equal (Numbers.shiftLeft(0b1,1)) 0b10 "1 <<< 1 = 10"
              Expect.equal (Numbers.shiftLeft(0b1,2)) 0b100 "1 <<< 2 = 100"
              Expect.equal (Numbers.shiftLeft(0b1,3)) 0b1000 "1 <<< 3 = 1000"
              Expect.equal (Numbers.shiftLeft(0b00010111,1)) 0b00101110 "0010111 <<< 1 = 00101110"
              Expect.equal (Numbers.shiftLeft(0b10,-1)) 0b0 "10 <<< -1 = 0"  // truncated to least 6-bits, 16  ???
              Expect.equal (Numbers.shiftLeft(0b1,32)) 0x100000000L "1 <<< 1 = 10"
              Expect.equal (Numbers.shiftLeft(0b1,10000)) 0x10000 "1 <<< 10000 = ? " // truncated to least 6-bits, 16  ???

              
          testCase "test bit-shift-right"
          <| fun _ ->
              // these examples taken directly from the Clojure testing code

              Expect.equal (Numbers.shiftRight(0b1,1)) 0b0 "1 >>> 1 = 0"
              Expect.equal (Numbers.shiftRight(0b100,1)) 0b010 "100 >>> 1 = 010"
              Expect.equal (Numbers.shiftRight(0b100,2)) 0b001 "100 >>> 2 = 001"
              Expect.equal (Numbers.shiftRight(0b100,3)) 0b000 "100 >>> 3 = 000"

              Expect.equal (Numbers.shiftRight(0b00010111,1)) 0b001011 "0010111 >>> 1 = 001011"
              Expect.equal (Numbers.shiftRight(0b10,-1)) 0b0 "10 >>> -1 = 0"  // truncated to least 6-bits, 16  ???
              Expect.equal (Numbers.shiftRight(0x100000000L,32)) 1 "1 >>> 1 = 10"
              Expect.equal (Numbers.shiftRight(0x10000,10000)) 1 "1 >>> 10000 = ? " // truncated to least 6-bits, 16  ???
              Expect.equal (Numbers.shiftRight(-0b10,1)) -1 "1 >>> 10000 = ? " 


          testCase "test unsigned-bit-shift-right"
          <| fun _ ->
              // these examples taken directly from the Clojure testing code

              Expect.equal (Numbers.unsignedShiftRight(0b1,1)) 0b0 "1 >>> 1 = 0"
              Expect.equal (Numbers.unsignedShiftRight(0b100,1)) 0b010 "100 >>> 1 = 010"
              Expect.equal (Numbers.unsignedShiftRight(0b100,2)) 0b001 "100 >>> 2 = 001"
              Expect.equal (Numbers.unsignedShiftRight(0b100,3)) 0b000 "100 >>> 3 = 000"

              Expect.equal (Numbers.unsignedShiftRight(0b00010111,1)) 0b001011 "0010111 >>> 1 = 001011"
              Expect.equal (Numbers.unsignedShiftRight(0b10,-1)) 0b0 "10 >>> -1 = 0"  // truncated to least 6-bits, 16  ???
              Expect.equal (Numbers.unsignedShiftRight(0x100000000L,32)) 1 "1 >>> 1 = 10"
              Expect.equal (Numbers.unsignedShiftRight(0x10000,10000)) 1 "1 >>> 10000 = ? " // truncated to least 6-bits, 16  ???
              Expect.equal (Numbers.unsignedShiftRight(-0b10,1)) (Int64.MaxValue) "1 >>> 10000 = ? " 

          testCase "test test-bit-clear"
          <| fun _ ->
              // these examples taken directly from the Clojure testing code

              Expect.equal (Numbers.ClearBit(0b1111,1)) 0b1101 "1111 clear 1 = 1101" 
              Expect.equal (Numbers.ClearBit(0b1101,1)) 0b1101 "1101 clear 1 = 1101" 

          testCase "test test-bit-set"
          <| fun _ ->
              // these examples taken directly from the Clojure testing code

              Expect.equal (Numbers.SetBit(0b1111,1)) 0b1111 "1111 set 1 = 1111" 
              Expect.equal (Numbers.SetBit(0b1101,1)) 0b1111 "1101 set 1 = 1111" 


          testCase "test test-bit-flip"
          <| fun _ ->
              // these examples taken directly from the Clojure testing code

              Expect.equal (Numbers.FlipBit(0b1111,1)) 0b1101 "1111 flip 1 = 1101" 
              Expect.equal (Numbers.FlipBit(0b1101,1)) 0b1111 "1101 flip 1 = 1111" 


          testCase "test test-bit-test"
          <| fun _ ->
              // these examples taken directly from the Clojure testing code

              Expect.isTrue (Numbers.TestBit(0b1111,1))  "1111 test 1 = true" 
              Expect.isFalse (Numbers.TestBit(0b1101,1))  "1101 test 1 = false" 


          testCase "test equiv"
          <| fun _ ->

              let i1 : obj = 1
              let l1 : obj = 1L
              let ul1 : obj = 1UL
              let d1 : obj = 1.0
              
              Expect.isTrue (Numbers.equiv(i1, l1)) "1 (int) equiv 1 (long)"

          ]
