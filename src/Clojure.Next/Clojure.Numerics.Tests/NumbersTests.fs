module NumbersTests

open Expecto
open Clojure.Numerics
open System.Numerics
open Clojure.BigArith
open System





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
    ftestList
        "Test add variations"
        [ testCase "basic addition works"
          <| fun _ ->
              let r1 = Ratio(BigInteger(2), BigInteger(3))
              let r2 = Ratio(BigInteger(4), BigInteger(5))
              let r3 = Ratio(BigInteger(22), BigInteger(15))

              let bi1 = BigInteger(1e10)
              let bi2 = BigInteger(2e10)
              let bi3 = BigInt.fromBigInteger(BigInteger(3e10))

              let bd1 = BigDecimal.Create("11111111111111111.1111111111111")
              let bd2 = BigDecimal.Create("22222222222222222.2222222222222")
              let bd3 = BigDecimal.Create("33333333333333333.3333333333333")

              let biMVp1 = BigInt.fromBigInteger(BigInteger(Int64.MaxValue)+BigInteger.One)
              let biUMVp1 = BigInt.fromBigInteger(BigInteger(UInt64.MaxValue)+BigInteger.One)
              let bdMVp1 = BigDecimal.Create(Decimal.MaxValue) + BigDecimal.Create(1)


              Expect.equal (Numbers.add (1L, 2L)) 3L "1+2=3"
              Expect.equal (Numbers.add (1UL, 2UL)) 3UL "1+2=3 unsigned"
              Expect.floatClose Accuracy.medium (Numbers.add (1.2, 2.3)) 3.5 "1.2+2.3=3.5"
              Expect.equal (Numbers.add (r1, r2)) r3 "2/3+4/5=22/15"
              Expect.equal (Numbers.add (bi1, bi2)) bi3 "1+2=3 bigint"
              Expect.equal (Numbers.add (bd1, bd2)) bd3 "1+2=3 bigdec"
              Expect.equal (Numbers.add (1.2M, 2.3M)) 3.5M "1.2M+2.3M=3.5M"

              // Check indirect calls on primitive args
              Expect.equal (Numbers.add (1L:>obj, 2L:>obj)) 3L "1+2=3"
              Expect.equal (Numbers.add (1UL:>obj, 2UL:>obj)) 3UL "1+2=3 unsigned"
              Expect.floatClose Accuracy.medium ((Numbers.add (1.2:>obj, 2.3:>obj)):?>float) 3.5 "1.2+2.3=3.5"
              Expect.equal (Numbers.add (1.2M:>obj, 2.3M:>obj)) 3.5M "1.2M+2.3M=3.5M"

              // check edge cases
              Expect.throwsT<OverflowException> (fun () -> Numbers.add(Int64.MaxValue,1L) |> ignore) "add overflows"
              Expect.throwsT<OverflowException> (fun () -> Numbers.add(Int64.MaxValue:>obj,1L:>obj) |> ignore) "add overflows"
              Expect.throwsT<OverflowException> (fun () -> Numbers.add(UInt64.MaxValue,1UL) |> ignore) "add overflows"
              Expect.throwsT<OverflowException> (fun () -> Numbers.add(UInt64.MaxValue:>obj,1UL:>obj) |> ignore) "add overflows"
              Expect.throwsT<OverflowException> (fun () -> Numbers.add(Decimal.MaxValue,1M) |> ignore) "add overflows"
              Expect.throwsT<OverflowException> (fun () -> Numbers.add(Decimal.MaxValue:>obj,1M:>obj) |> ignore) "add overflows"

              Expect.equal (Numbers.addP (1L, 2L)) 3L "1+2=3"
              Expect.equal (Numbers.addP (1UL, 2UL)) 3UL "1+2=3 unsigned"
              Expect.floatClose Accuracy.medium (Numbers.addP (1.2, 2.3)) 3.5 "1.2+2.3=3.5"
              Expect.equal (Numbers.addP (r1, r2)) r3 "2/3+4/5=22/15"
              Expect.equal (Numbers.addP (bi1, bi2)) bi3 "1+2=3 bigint"
              Expect.equal (Numbers.addP (bd1, bd2)) bd3 "1+2=3 bigdec"
              Expect.equal (Numbers.addP (1.2M, 2.3M)) 3.5M "1.2M+2.3M=3.5M"

              // Check indirect calls on primitive args
              Expect.equal (Numbers.add (1L:>obj, 2L:>obj)) 3L "1+2=3"
              Expect.equal (Numbers.add (1UL:>obj, 2UL:>obj)) 3UL "1+2=3 unsigned"
              Expect.floatClose Accuracy.medium ((Numbers.add (1.2:>obj, 2.3:>obj)):?>float) 3.5 "1.2+2.3=3.5"
              Expect.equal (Numbers.add (1.2M:>obj, 2.3M:>obj)) 3.5M "1.2M+2.3M=3.5M"

              // Edge cases should promote
              Expect.equal (Numbers.addP(Int64.MaxValue,1L)) biMVp1 "add overflows"
              Expect.equal (Numbers.addP(Int64.MaxValue:>obj,1L:>obj)) biMVp1 "add overflows"
              Expect.equal (Numbers.addP(UInt64.MaxValue,1UL)) biUMVp1 "add overflows"
              Expect.equal (Numbers.addP(UInt64.MaxValue:>obj,1UL:>obj)) biUMVp1 "add overflows"
              Expect.equal (Numbers.addP(Decimal.MaxValue,1M)) bdMVp1 "add overflows"
              Expect.equal (Numbers.addP(Decimal.MaxValue:>obj,1M:>obj)) bdMVp1 "add overflows"

          ]
