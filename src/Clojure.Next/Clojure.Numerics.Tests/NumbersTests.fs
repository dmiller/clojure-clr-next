module NumbersTests

open Expecto
open Clojure.Numerics
open System.Numerics
open Clojure.BigArith





[<Tests>]
let testOpsSelect =
    testList
        "Test OpsType selection"
        [ testCase "obj->OpsType"
          <| fun _ ->
                let signedIntegerValues  : obj list  =  [ 97y; 97s; 97; 97L; ]
                let unsignedIntegerValues  : obj list  =  [  97uy;  97us; 97u; 97UL ]
                let floatValues : obj list = [ 97.2f; 97.2 ]
                let clrDecimalValues : obj list = [ 97.2M ]
                let otherNumericValues : (obj*OpsType) list = 
                    [ (BigInteger(1e300), OpsType.BigInteger);
                      (Ratio(BigInteger(1e200),BigInteger(1e300)), OpsType.Ratio)
                      (BigDecimal.Create(1.5e10), OpsType.BigDecimal)
                      (12.5M, OpsType.ClrDecimal)
                      ("123", OpsType.Long)
                    ]

                for v in signedIntegerValues do
                    Expect.equal (OpsSelector.ops(v)) OpsType.Long "Expect signed integers to map to Long"

                for v in unsignedIntegerValues do
                    Expect.equal (OpsSelector.ops(v)) OpsType.ULong "Expect unsigned integers to map to ULong"

                for v in floatValues do
                    Expect.equal (OpsSelector.ops(v)) OpsType.Double "Expect floats to map to Double"

                for v in clrDecimalValues do
                    Expect.equal (OpsSelector.ops(v)) OpsType.ClrDecimal "Expect CLR decimal to map to ClrDecimal"

                for (v,o) in otherNumericValues do
                    Expect.equal (OpsSelector.ops(v)) o "Expect other types to map properly"

          testCase "OpsType.combine"
          <| fun _ ->
                let L = OpsType.Long
                let UL = OpsType.ULong
                let D = OpsType.Double
                let R = OpsType.Ratio
                let BI = OpsType.BigInteger
                let BD = OpsType.BigDecimal
                let CD = OpsType.ClrDecimal

                let combinations : (OpsType * OpsType * OpsType) list =
                    [
                        (L, L, L)
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
                        (CD, CD, CD)
                        ]

                for (t1,t2,t3) in combinations do
                    Expect.equal (OpsSelector.combine(t1,t3)) t3 "Expect combination to match"

            ]
            