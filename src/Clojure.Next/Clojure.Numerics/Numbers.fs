namespace Clojure.Numerics

open System
open Converters
open System.Numerics
open Clojure.BigArith


type OpsType =
    | Long = 0
    | Double = 1
    | Ratio = 2
    | BigInteger = 3
    | BigDecimal = 4
    | ULong = 5
    | ClrDecimal = 6

module OpsSelector =

    let selectorTable =
        array2D
            [| [| OpsType.Long
                  OpsType.Double
                  OpsType.Ratio
                  OpsType.BigInteger
                  OpsType.BigDecimal
                  OpsType.BigInteger
                  OpsType.ClrDecimal |]
               [| OpsType.Double
                  OpsType.Double
                  OpsType.Double
                  OpsType.Double
                  OpsType.Double
                  OpsType.Double
                  OpsType.Double |]
               [| OpsType.Ratio
                  OpsType.Double
                  OpsType.Ratio
                  OpsType.Ratio
                  OpsType.BigDecimal
                  OpsType.Ratio
                  OpsType.BigDecimal |]
               [| OpsType.BigInteger
                  OpsType.Double
                  OpsType.Ratio
                  OpsType.BigInteger
                  OpsType.BigDecimal
                  OpsType.BigInteger
                  OpsType.BigDecimal |]
               [| OpsType.BigDecimal
                  OpsType.Double
                  OpsType.BigDecimal
                  OpsType.BigDecimal
                  OpsType.BigDecimal
                  OpsType.BigDecimal
                  OpsType.BigDecimal |]
               [| OpsType.BigInteger
                  OpsType.Double
                  OpsType.Ratio
                  OpsType.BigInteger
                  OpsType.BigDecimal
                  OpsType.ULong
                  OpsType.ClrDecimal |]
               [| OpsType.ClrDecimal
                  OpsType.Double
                  OpsType.BigDecimal
                  OpsType.BigDecimal
                  OpsType.BigDecimal
                  OpsType.ClrDecimal
                  OpsType.ClrDecimal |] |]

    let ops (x: obj) : OpsType =
        match Type.GetTypeCode(x.GetType()) with
        | TypeCode.SByte
        | TypeCode.Int16
        | TypeCode.Int32
        | TypeCode.Int64 -> OpsType.Long


        | TypeCode.Byte
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64 -> OpsType.ULong

        | TypeCode.Single
        | TypeCode.Double -> OpsType.Double

        | TypeCode.Decimal -> OpsType.ClrDecimal

        | _ ->
            match x with
            | :? BigInt
            | :? BigInteger -> OpsType.BigInteger
            | :? Ratio -> OpsType.Ratio
            | :? BigDecimal -> OpsType.BigDecimal
            | _ -> OpsType.Long

    let combine (t1: OpsType, t2: OpsType) = selectorTable[int (t1), int (t2)]

type Ops =
    abstract isZero: x: obj -> bool
    abstract isPos: x: obj -> bool
    abstract isNeg: x: obj -> bool

    abstract add: x: obj * y: obj -> obj
    abstract addP: x: obj * y: obj -> obj
    abstract unchecked_add: x: obj * y: obj -> obj
    abstract multiply: x: obj * y: obj -> obj
    abstract multiplyP: x: obj * y: obj -> obj
    abstract unchecked_multiply: x: obj * y: obj -> obj
    abstract divide: x: obj * y: obj -> obj
    abstract quotient: x: obj * y: obj -> obj
    abstract remainder: x: obj * y: obj -> obj

    abstract equiv: x: obj * y: obj -> bool
    abstract lt: x: obj * y: obj -> bool
    abstract lte: x: obj * y: obj -> bool
    abstract gte: x: obj * y: obj -> bool

    abstract negate: x: obj -> obj
    abstract negateP: x: obj -> obj
    abstract unchecked_negate: x: obj -> obj
    abstract inc: x: obj -> obj
    abstract incP: x: obj -> obj
    abstract unchecked_inc: x: obj -> obj
    abstract dec: x: obj -> obj
    abstract decP: x: obj -> obj
    abstract unchecked_dec: x: obj -> obj

    abstract abs: x: obj -> obj

[<AbstractClass>]
type OpsP() =

    interface Ops with
        member this.add(x: obj, y: obj) : obj =
            raise (System.NotImplementedException())

        member this.dec(x: obj) : obj =
            raise (System.NotImplementedException())

        member this.divide(x: obj, y: obj) : obj =
            raise (System.NotImplementedException())

        member this.equiv(x: obj, y: obj) : bool =
            raise (System.NotImplementedException())

        member this.gte(x: obj, y: obj) : bool =
            raise (System.NotImplementedException())

        member this.inc(x: obj) : obj =
            raise (System.NotImplementedException())

        member this.isNeg(x: obj) : bool =
            raise (System.NotImplementedException())

        member this.isPos(x: obj) : bool =
            raise (System.NotImplementedException())

        member this.isZero(x: obj) : bool =
            raise (System.NotImplementedException())

        member this.lt(x: obj, y: obj) : bool =
            raise (System.NotImplementedException())

        member this.lte(x: obj, y: obj) : bool =
            raise (System.NotImplementedException())

        member this.multiply(x: obj, y: obj) : obj =
            raise (System.NotImplementedException())

        member this.negate(x: obj) : obj =
            raise (System.NotImplementedException())

        member this.quotient(x: obj, y: obj) : obj =
            raise (System.NotImplementedException())

        member this.remainder(x: obj, y: obj) : obj =
            raise (System.NotImplementedException())

        member this.abs(x: obj) : obj =
            raise (System.NotImplementedException())

        member this.addP(x, y) = (this :> Ops).add (x, y)
        member this.unchecked_add(x, y) = (this :> Ops).add (x, y)
        member this.multiplyP(x, y) = (this :> Ops).multiply (x, y)
        member this.unchecked_multiply(x, y) = (this :> Ops).multiply (x, y)
        member this.negateP(x) = (this :> Ops).negate (x)
        member this.unchecked_negate(x) = (this :> Ops).negate (x)
        member this.incP(x) = (this :> Ops).inc (x)
        member this.unchecked_inc(x) = (this :> Ops).inc (x)
        member this.decP(x) = (this :> Ops).dec (x)
        member this.unchecked_dec(x) = (this :> Ops).dec (x)


[<AbstractClass; Sealed>]
type Numbers() =

    static member LONG_OPS: Ops = LongOps()
    static member DOUBLE_OPS: Ops = DoubleOps()
    static member RATIO_OPS: Ops = RatioOps()
    static member BIGINT_OPS: Ops = BigIntOps()
    static member BIGDEC_OPS: Ops = BigDecimalOps()
    static member ULONG_OPS: Ops = ULongOps()
    static member CLRDEC_OPS: Ops = ClrDecimalOps()

    static member private opsImplTable: Ops array =
        [| Numbers.LONG_OPS
           Numbers.DOUBLE_OPS
           Numbers.RATIO_OPS
           Numbers.BIGINT_OPS
           Numbers.BIGDEC_OPS
           Numbers.ULONG_OPS
           Numbers.CLRDEC_OPS |]

    static member private IntOverflow() = OverflowException("integer overflow")
    static member private DecOverflow() = OverflowException("decimal overflow")

    static member IsNaN(x: obj) =
        match x with
        | :? float as f -> Double.IsFinite(f)
        | :? float32 as f -> Single.IsNaN(f)
        | _ -> false


    static member getOps(x: obj) =
        Numbers.opsImplTable[int (OpsSelector.ops (x))]

    static member getOps(x: obj, y: obj) =
        Numbers.opsImplTable[int (OpsSelector.combine (OpsSelector.ops (x), OpsSelector.ops (y)))]

    // isZero, isPos, isNeg

    static member isZero(x: obj) = Numbers.getOps(x).isZero (x)
    static member isZero(x: double) = x = 0.0
    static member isZero(x: int64) = x = 0L
    static member isZero(x: uint64) = x = 0UL
    static member isZero(x: decimal) = x = 0M

    static member isPos(x: obj) = Numbers.getOps(x).isPos (x)
    static member isPos(x: double) = x > 0.0
    static member isPos(x: int64) = x > 0L
    static member isPos(x: uint64) = x > 0UL
    static member isPos(x: decimal) = x > 0M

    static member isNeg(x: obj) = Numbers.getOps(x).isNeg (x)
    static member isNeg(x: double) = x < 0.0
    static member isNeg(x: int64) = x < 0L
    static member isNeg(_: uint64) = false
    static member isNeg(x: decimal) = x < 0M


    // add, addP, unchecked_add

    static member add(x: obj, y: obj) = Numbers.getOps(x, y).add (x, y)
    static member add(x: double, y: double) = x + y

    static member add(x: int64, y: int64) =
        let ret = x + y

        if (ret ^^^ x) < 0 && (ret ^^^ y) < 0 then
            raise <| Numbers.IntOverflow()
        else
            ret

    static member add(x: uint64, y: uint64) =
        if x > UInt64.MaxValue - y then
            raise <| Numbers.IntOverflow()
        else
            x + y

    static member add(x: decimal, y: decimal) =
        if x > 0m && y > 0m && x > Decimal.MaxValue - y then
            raise <| Numbers.DecOverflow()
        elif x < 0m && y < 0m && x < Decimal.MinValue - y then
            raise <| Numbers.DecOverflow()
        else
            (x + y) :> obj

    static member add(x: double, y: obj) = Numbers.add (x, convertToDouble (y))

    static member add(x: obj, y: double) = Numbers.add (convertToDouble (x), y)

    static member add(x: double, y: int64) = x + double (y)
    static member add(x: int64, y: double) = double (x) + y
    static member add(x: double, y: uint64) = x + double (y)
    static member add(x: uint64, y: double) = double (x) + y

    static member add(x: int64, y: obj) = Numbers.add (x :> obj, y)
    static member add(x: obj, y: int64) = Numbers.add (x, y :> obj)
    static member add(x: uint64, y: obj) = Numbers.add ((x :> obj), y)
    static member add(x: obj, y: uint64) = Numbers.add (x, y :> obj)

    // These are supposed to be non-promoting.
    // It is not clear what to do here.
    // Given that long is the default, let's just go that and hope.
    // Or maybe we shouldn't even bother.
    static member add(x: int64, y: uint64) = Numbers.add (x :> obj, y :> obj)
    static member add(x: uint64, y: int64) = Numbers.add (x :> obj, y :> obj)


    static member addP(x: obj, y: obj) = Numbers.getOps(x, y).addP (x, y)
    static member addP(x: double, y: double) = x + y

    static member addP(x: int64, y: int64) =
        let ret = x + y

        if (ret ^^^ x) < 0 && (ret ^^^ y) < 0 then
            Numbers.addP (x :> obj, y :> obj)
        else
            ret :> obj

    static member addP(x: uint64, y: uint64) =
        if x > UInt64.MaxValue - y then
            Numbers.addP (x :> obj, y :> obj)
        else
            (x + y) :> obj

    static member addP(x: decimal, y: decimal) =
        if x > 0m && y > 0m && x > Decimal.MaxValue - y then
            Numbers.addP (x :> obj, y :> obj)
        elif x < 0m && y < 0m && x < Decimal.MinValue - y then
            Numbers.addP (x :> obj, y :> obj)
        else
            (x + y) :> obj

    static member addP(x: double, y: obj) = Numbers.addP (x, convertToDouble (y))

    static member addP(x: obj, y: double) = Numbers.addP (convertToDouble (x), y)

    static member addP(x: double, y: int64) = x + double (y)
    static member addP(x: int64, y: double) = double (x) + y
    static member addP(x: double, y: uint64) = x + double (y)
    static member addP(x: uint64, y: double) = double (x) + y

    static member addP(x: int64, y: obj) = Numbers.addP (x :> obj, y)
    static member addP(x: obj, y: int64) = Numbers.addP (x, y :> obj)
    static member addP(x: uint64, y: obj) = Numbers.addP ((x :> obj), y)
    static member addP(x: obj, y: uint64) = Numbers.addP (x, y :> obj)

    static member addP(x: int64, y: uint64) = Numbers.addP (x :> obj, y :> obj)
    static member addP(x: uint64, y: int64) = Numbers.addP (x :> obj, y :> obj)

    static member unchecked_add(x: obj, y: obj) =
        Numbers.getOps(x, y).unchecked_add (x, y)

    static member unchecked_add(x: double, y: double) = Numbers.add (x, y)
    static member unchecked_add(x: int64, y: int64) = x + y
    static member unchecked_add(x: uint64, y: uint64) = x + y
    static member unchecked_add(x: decimal, y: decimal) = x + y
    static member unchecked_add(x: double, y: obj) = Numbers.add (x, y)
    static member unchecked_add(x: obj, y: double) = Numbers.add (x, y)
    static member unchecked_add(x: double, y: int64) = Numbers.add (x, y)
    static member unchecked_add(x: int64, y: double) = Numbers.add (x, y)
    static member unchecked_add(x: double, y: uint64) = Numbers.add (x, y)
    static member unchecked_add(x: uint64, y: double) = Numbers.add (x, y)

    static member unchecked_add(x: int64, y: obj) = Numbers.unchecked_add (x :> obj, y)
    static member unchecked_add(x: obj, y: int64) = Numbers.unchecked_add (x, y :> obj)
    static member unchecked_add(x: uint64, y: obj) = Numbers.unchecked_add ((x :> obj), y)
    static member unchecked_add(x: obj, y: uint64) = Numbers.unchecked_add (x, y :> obj)

    static member unchecked_add(x: int64, y: uint64) =
        Numbers.unchecked_add (x :> obj, y :> obj)

    static member unchecked_add(x: uint64, y: int64) =
        Numbers.unchecked_add (x :> obj, y :> obj)

    // unary minus, minusP, unchecked_minus

    static member minus(x: obj) = Numbers.getOps(x).negate (x)
    static member minus(x: double) = -x

    static member minus(x: int64) =
        if x = Int64.MinValue then
            raise <| Numbers.IntOverflow()
        else
            -x

    static member minus(x: uint64) =
        if x = 0UL then x 
        else raise <| ArithmeticException("Checked operation error: negation of non-zero unsigned")


    static member minus(x: decimal) = -x

    static member minusP(x: obj) = Numbers.getOps(x).negateP (x)
    static member minusP(x: double) = -x

    static member minusP(x: int64) =
        if x = Int64.MinValue then
            BigInt.fromBigInteger (- BigInteger(x)) :> obj
        else
            -x :> obj

    static member minusP(x: uint64) =
        if x = 0UL then
            x :> obj
        else
            BigInt.fromBigInteger (- BigInteger(x)) :> obj

    static member minusP(x: decimal) = -x

    static member unchecked_minus(x: obj) = Numbers.getOps(x).unchecked_negate (x)
    static member unchecked_minus(x: double) = Numbers.minus (x)
    static member unchecked_minus(x: int64) = -x

    static member unchecked_minus(x: uint64) =
        if x = 0UL then
            x
        else
            raise <| ArithmeticException("negation of non-zero unsigned")


    static member unchecked_minus(x: decimal) = -x



    // binary minus, minusP, unchecked_minus

    static member minus(x: obj, y: obj) =
        let yops = Numbers.getOps (y)
        let xyops = Numbers.getOps(x,y)
        // Only one Ops implementation does not support negate: ULong (except on arg 0UL)
        // Special case that one
        match yops, xyops with
        | :? ULongOps, :? ULongOps -> Numbers.minus(convertToULong(x),convertToULong(y)) :> obj
        | :? ULongOps, _ -> xyops.add(x,xyops.negate(y))
        | _ ->  Numbers.getOps(x, y).add (x, yops.negate (y))

    static member minus(x: double, y: double) = x - y

    static member minus(x: int64, y: int64) =
        let ret = x - y

        if (ret ^^^ x) < 0 && (ret ^^^ ~~~y) < 0 then
            raise <| Numbers.IntOverflow()
        else
            ret

    static member minus(x: uint64, y: uint64)  =
        if y > x then raise <| Numbers.IntOverflow() else x - y

    static member minus(x: decimal, y: decimal) =
        if x > 0m && y < 0m && x > Decimal.MaxValue + y then
            raise <| Numbers.DecOverflow()
        elif x < 0m && y > 0m && x < Decimal.MinValue + y then
            raise <| Numbers.DecOverflow()
        else
            (x - y) :> obj

    static member minus(x: double, y: obj) = Numbers.minus (x, convertToDouble (y))

    static member minus(x: obj, y: double) = Numbers.minus (convertToDouble (x), y)

    static member minus(x: double, y: int64) = x - double (y)
    static member minus(x: int64, y: double) = double (x) - y
    static member minus(x: double, y: uint64) = x - double (y)
    static member minus(x: uint64, y: double) = double (x) - y

    static member minus(x: int64, y: obj) = Numbers.minus (x :> obj, y)
    static member minus(x: obj, y: int64) = Numbers.minus (x, y :> obj)
    static member minus(x: uint64, y: obj) = Numbers.minus ((x :> obj), y)
    static member minus(x: obj, y: uint64) = Numbers.minus (x, y :> obj)
    static member minus(x: int64, y: uint64) = Numbers.minus (x :> obj, y :> obj)
    static member minus(x: uint64, y: int64) = Numbers.minus (x :> obj, y :> obj)


    static member minusP(x: obj, y: obj) =
        // the straightforward code:
        //      let yops = Numbers.getOps (y)
        //      let negativeY = yops.negateP (y)
        //      Numbers.getOps(x, negativeY).addP (x, negativeY)
        // causes perhaps unnecessary promotion when x,y are ULong.
        // Again, we need to special case that

        let yops = Numbers.getOps (y)
        let xyops = Numbers.getOps(x,y)
        // Only one Ops implementation does not support negate: ULong (except on arg 0UL)
        // Special case that one
        match yops, xyops with
        | :? ULongOps, :? ULongOps -> Numbers.minusP(convertToULong(x),convertToULong(y)) :> obj
        | :? ULongOps, _ -> xyops.addP(x,xyops.negateP(y))
        | _ ->  Numbers.getOps(x, y).addP (x, yops.negateP (y))

    static member minusP(x: double, y: double) = x - y

    static member minusP(x: int64, y: int64) =
        let ret = x - y

        if (ret ^^^ x) < 0 && (ret ^^^ ~~~y) < 0 then
            Numbers.minusP (x :> obj, y :> obj)
        else
            ret :> obj

    static member minusP(x: uint64, y: uint64) =
        if y > x then
            Numbers.BIGINT_OPS.addP (x, Numbers.BIGINT_OPS.negateP(y))
        else
            (x - y) :> obj

    static member minusP(x: decimal, y: decimal) =
        if x > 0m && y < 0m && x > Decimal.MaxValue + y then
            Numbers.minusP (x :> obj, y :> obj)
        elif x < 0m && y > 0m && x < Decimal.MinValue + y then
            Numbers.minusP (x :> obj, y :> obj)
        else
            (x - y) :> obj

    static member minusP(x: double, y: obj) = Numbers.minusP (x, convertToDouble (y))

    static member minusP(x: obj, y: double) = Numbers.minusP (convertToDouble (x), y)

    static member minusP(x: double, y: int64) = x - double (y)
    static member minusP(x: int64, y: double) = double (x) - y
    static member minusP(x: double, y: uint64) = x - double (y)
    static member minusP(x: uint64, y: double) = double (x) - y

    static member minusP(x: int64, y: obj) = Numbers.minusP (x :> obj, y)
    static member minusP(x: obj, y: int64) = Numbers.minusP (x, y :> obj)
    static member minusP(x: uint64, y: obj) = Numbers.minusP ((x :> obj), y)
    static member minusP(x: obj, y: uint64) = Numbers.minusP (x, y :> obj)

    static member minusP(x: int64, y: uint64) = Numbers.minusP (x :> obj, y :> obj)
    static member minusP(x: uint64, y: int64) = Numbers.minusP (x :> obj, y :> obj)

    static member unchecked_minus(x: obj, y: obj) =
        // once again, need special case ULongs
        let yops = Numbers.getOps (y)
        let xyops = Numbers.getOps(x,y)
        match yops, xyops with
        | :? ULongOps, :? ULongOps -> Numbers.unchecked_minus(convertToULong(x),convertToULong(y)) :> obj
        | :? ULongOps, _ -> xyops.unchecked_negate(x,xyops.unchecked_negate(y))
        | _ ->  Numbers.getOps(x, y).unchecked_add (x, yops.unchecked_negate (y))

        //let yops = Numbers.getOps (y)
        //match yops with
        //| :? ULongOps -> 
        //Numbers.getOps(x, y).unchecked_add (x, yops.unchecked_negate (y))

    static member unchecked_minus(x: double, y: double) = Numbers.minus (x, y)
    static member unchecked_minus(x: int64, y: int64) = x - y
    static member unchecked_minus(x: uint64, y: uint64) = x - y
    static member unchecked_minus(x: decimal, y: decimal) = x - y
    static member unchecked_minus(x: double, y: obj) = Numbers.minus (x, y)
    static member unchecked_minus(x: obj, y: double) = Numbers.minus (x, y)
    static member unchecked_minus(x: double, y: int64) = Numbers.minus (x, y)
    static member unchecked_minus(x: int64, y: double) = Numbers.minus (x, y)
    static member unchecked_minus(x: double, y: uint64) = Numbers.minus (x, y)
    static member unchecked_minus(x: uint64, y: double) = Numbers.minus (x, y)

    static member unchecked_minus(x: int64, y: obj) = Numbers.unchecked_minus (x :> obj, y)
    static member unchecked_minus(x: obj, y: int64) = Numbers.unchecked_minus (x, y :> obj)
    static member unchecked_minus(x: uint64, y: obj) = Numbers.unchecked_minus ((x :> obj), y)
    static member unchecked_minus(x: obj, y: uint64) = Numbers.unchecked_minus (x, y :> obj)

    static member unchecked_minus(x: int64, y: uint64) =
        Numbers.unchecked_minus (x :> obj, y :> obj)

    static member unchecked_minus(x: uint64, y: int64) =
        Numbers.unchecked_minus (x :> obj, y :> obj)

    // multiply, multiplyP, unchecked_multiply

    static member multiply(x: obj, y: obj) = Numbers.getOps(x, y).multiply (x, y)
    static member multiply(x: double, y: double) = x * y

    static member multiply(x: int64, y: int64) =
        if x = Int64.MinValue && y < 0 then
            raise <| Numbers.IntOverflow()
        else
            let ret = x * y

            if y <> 0 && ret / y <> x then
                raise <| Numbers.IntOverflow()
            else
                ret

    static member multiply(x: uint64, y: uint64) =
        let ret = x * y

        if y <> 0UL && ret / y <> x then
            raise <| Numbers.IntOverflow()
        else
            ret

    static member multiply(x: decimal, y: decimal) = x * y

    static member multiply(x: double, y: obj) =
        Numbers.multiply (x, convertToDouble (y))

    static member multiply(x: obj, y: double) =
        Numbers.multiply (convertToDouble (x), y)

    static member multiply(x: double, y: int64) = x * double (y)
    static member multiply(x: int64, y: double) = double (x) * y
    static member multiply(x: double, y: uint64) = x * double (y)
    static member multiply(x: uint64, y: double) = double (x) * y

    static member multiply(x: int64, y: obj) = Numbers.multiply (x :> obj, y)
    static member multiply(x: obj, y: int64) = Numbers.multiply (x, y :> obj)
    static member multiply(x: uint64, y: obj) = Numbers.multiply ((x :> obj), y)
    static member multiply(x: obj, y: uint64) = Numbers.multiply (x, y :> obj)
    static member multiply(x: int64, y: uint64) = Numbers.multiply (x :> obj, y :> obj)
    static member multiply(x: uint64, y: int64) = Numbers.multiply (x :> obj, y :> obj)


    static member multiplyP(x: obj, y: obj) = Numbers.getOps(x, y).multiplyP (x, y)
    static member multiplyP(x: double, y: double) = x * y

    static member multiplyP(x: int64, y: int64) =
        if x = Int64.MinValue && y < 0 then
            Numbers.multiplyP (x :> obj, y :> obj)
        else
            let ret = x * y

            if y <> 0 && ret / y <> x then
                Numbers.multiplyP (x :> obj, y :> obj)
            else
                ret :> obj

    static member multiplyP(x: uint64, y: uint64) =
        let ret = x * y

        if y <> 0UL && ret / y <> x then
            Numbers.multiplyP (x :> obj, y :> obj)
        else
            ret

    static member multiplyP(x: decimal, y: decimal) =
        try
            x * y :> obj
        with :? OverflowException ->
            Numbers.BIGDEC_OPS.multiply (x, y)

    static member multiplyP(x: double, y: obj) =
        Numbers.multiplyP (x, convertToDouble (y))

    static member multiplyP(x: obj, y: double) =
        Numbers.multiplyP (convertToDouble (x), y)

    static member multiplyP(x: double, y: int64) = x * double (y)
    static member multiplyP(x: int64, y: double) = double (x) * y
    static member multiplyP(x: double, y: uint64) = x * double (y)
    static member multiplyP(x: uint64, y: double) = double (x) * y

    static member multiplyP(x: int64, y: obj) = Numbers.multiplyP (x :> obj, y)
    static member multiplyP(x: obj, y: int64) = Numbers.multiplyP (x, y :> obj)
    static member multiplyP(x: uint64, y: obj) = Numbers.multiplyP ((x :> obj), y)
    static member multiplyP(x: obj, y: uint64) = Numbers.multiplyP (x, y :> obj)

    static member multiplyP(x: int64, y: uint64) = Numbers.multiplyP (x :> obj, y :> obj)
    static member multiplyP(x: uint64, y: int64) = Numbers.multiplyP (x :> obj, y :> obj)

    static member unchecked_multiply(x: obj, y: obj) =
        Numbers.getOps(x, y).unchecked_multiply (x, y)

    static member unchecked_multiply(x: double, y: double) = Numbers.multiply (x, y)
    static member unchecked_multiply(x: int64, y: int64) = x * y
    static member unchecked_multiply(x: uint64, y: uint64) = x * y
    static member unchecked_multiply(x: decimal, y: decimal) = x * y
    static member unchecked_multiply(x: double, y: obj) = Numbers.multiply (x, y)
    static member unchecked_multiply(x: obj, y: double) = Numbers.multiply (x, y)
    static member unchecked_multiply(x: double, y: int64) = Numbers.multiply (x, y)
    static member unchecked_multiply(x: int64, y: double) = Numbers.multiply (x, y)
    static member unchecked_multiply(x: double, y: uint64) = Numbers.multiply (x, y)
    static member unchecked_multiply(x: uint64, y: double) = Numbers.multiply (x, y)

    static member unchecked_multiply(x: int64, y: obj) =
        Numbers.unchecked_multiply (x :> obj, y)

    static member unchecked_multiply(x: obj, y: int64) =
        Numbers.unchecked_multiply (x, y :> obj)

    static member unchecked_multiply(x: uint64, y: obj) =
        Numbers.unchecked_multiply ((x :> obj), y)

    static member unchecked_multiply(x: obj, y: uint64) =
        Numbers.unchecked_multiply (x, y :> obj)

    static member unchecked_multiply(x: int64, y: uint64) =
        Numbers.unchecked_multiply (x :> obj, y :> obj)

    static member unchecked_multiply(x: uint64, y: int64) =
        Numbers.unchecked_multiply (x :> obj, y :> obj)


    // divide, quotient, remainder

    static member divide(x: obj, y: obj) =
        if Numbers.IsNaN(x) then
            x
        elif Numbers.IsNaN(y) then
            y
        else
            let yops = Numbers.getOps (y)

            if yops.isZero (y) then
                raise <| ArithmeticException("divide by zero")
            else
                Numbers.getOps(x, y).divide (x, y)

    static member divide(x: double, y: double) = x / y
    static member divide(x: int64, y: int64) = Numbers.divide (x :> obj, y :> obj)
    static member divide(x: uint64, y: uint64) = Numbers.divide (x :> obj, y :> obj)
    static member divide(x: decimal, y: decimal) = Numbers.divide (x :> obj, y :> obj)
    static member divide(x: double, y: obj) = x / convertToDouble (y)
    static member divide(x: obj, y: double) = convertToDouble (x) / y
    static member divide(x: double, y: int64) = x / double (x)
    static member divide(x: int64, y: double) = double (x) / y
    static member divide(x: double, y: uint64) = x / double (x)
    static member divide(x: uint64, y: double) = double (x) / y
    static member divide(x: int64, y: obj) = Numbers.divide (x :> obj, y)
    static member divide(x: obj, y: int64) = Numbers.divide (x, y :> obj)
    static member divide(x: uint64, y: obj) = Numbers.divide (x :> obj, y)
    static member divide(x: obj, y: uint64) = Numbers.divide (x, y :> obj)


    static member quotient(x: obj, y: obj) =
        let yops = Numbers.getOps (y)

        if yops.isZero (y) then
            raise <| ArithmeticException("Divide by zero")
        else
            Numbers.getOps(x, y).quotient (x, y)

    static member quotient(x: double, y: double) =
        if y = 0 then
            raise <| ArithmeticException("Divide by zero")
        else
            let q = x / y

            if q <= double (Int64.MaxValue) && q >= double (Int64.MinValue) then
                double (int64 (q))
            else
                double (BigDecimal.Create(q).ToBigInteger())

    static member quotient(x: decimal, y: decimal) =
        if y = 0M then
            raise <| ArithmeticException("Divide by zero")
        else
            let q = x / y

            if q <= decimal (Int64.MaxValue) && q >= decimal (Int64.MinValue) then
                decimal (int64 (q))
            else
                decimal (BigDecimal.Create(q).ToBigInteger())


    static member quotient(x: int64, y: int64) = x / y
    static member quotient(x: uint64, y: uint64) = x / y
    static member quotient(x: double, y: obj) = Numbers.quotient (x :> obj, y)
    static member quotient(x: obj, y: double) = Numbers.quotient (x, y :> obj)
    static member quotient(x: double, y: int64) = Numbers.quotient (x, double (y))
    static member quotient(x: int64, y: double) = Numbers.quotient (double (x), y)
    static member quotient(x: double, y: uint64) = Numbers.quotient (x, double (y))
    static member quotient(x: uint64, y: double) = Numbers.quotient (double (x), y)
    static member quotient(x: int64, y: obj) = Numbers.quotient (x :> obj, y)
    static member quotient(x: obj, y: int64) = Numbers.quotient (x, y :> obj)
    static member quotient(x: uint64, y: obj) = Numbers.quotient (x :> obj, y)
    static member quotient(x: obj, y: uint64) = Numbers.quotient (x, y :> obj)

    static member remainder(x: obj, y: obj) =
        let yops = Numbers.getOps (y)

        if yops.isZero (y) then
            raise <| ArithmeticException("Divide by zero")
        else
            Numbers.getOps(x, y).remainder (x, y)

    static member remainder(x: double, y: double) =
        if y = 0 then
            raise <| ArithmeticException("Divide by zero")
        else
            let q = x / y

            if q <= double (Int64.MaxValue) && q >= double (Int64.MinValue) then
                x - double (uint64 (q)) * y
            else
                // bigint quotient
                let bq = BigDecimal.Create(q).ToBigInteger()
                x - double (bq) * y

    static member remainder(x: int64, y: int64) = x % y
    static member remainder(x: uint64, y: uint64) = x % y
    static member remainder(x: decimal, y: decimal) = x % y
    static member remainder(x: double, y: obj) = Numbers.remainder (x :> obj, y)
    static member remainder(x: obj, y: double) = Numbers.remainder (x, y :> obj)
    static member remainder(x: double, y: int64) = Numbers.remainder (x, double (y))
    static member remainder(x: int64, y: double) = Numbers.remainder (double (x), y)
    static member remainder(x: double, y: uint64) = Numbers.remainder (x, double (y))
    static member remainder(x: uint64, y: double) = Numbers.remainder (double (x), y)
    static member remainder(x: int64, y: obj) = Numbers.remainder (x :> obj, y)
    static member remainder(x: obj, y: int64) = Numbers.remainder (x, y :> obj)
    static member remainder(x: uint64, y: obj) = Numbers.remainder (x :> obj, y)
    static member remainder(x: obj, y: uint64) = Numbers.remainder (x, y :> obj)


    // inc, incP, unchecked_in, dec, decP, unchecked_dec

    static member inc(x: obj) = Numbers.getOps(x).inc (x)
    static member inc(x: double) = x + 1.0

    static member inc(x: int64) =
        if x = Int64.MaxValue then
            raise <| Numbers.IntOverflow()
        else
            x + 1L

    static member inc(x: uint64) =
        if x = UInt64.MaxValue then
            raise <| Numbers.IntOverflow()
        else
            x + 1UL

    static member inc(x: decimal) = x + 1M

    static member incP(x: obj) = Numbers.getOps(x).incP (x)
    static member incP(x: double) = x + 1.0

    static member incP(x: int64) =
        if x = Int64.MaxValue then
            Numbers.BIGINT_OPS.inc (x :> obj)
        else
            x + 1L :> obj

    static member incP(x: uint64) =
        if x = UInt64.MaxValue then
            Numbers.BIGINT_OPS.inc (x :> obj)
        else
            x + 1UL :> obj

    static member incP(x: decimal) = Numbers.addP (x, 1M)

    static member unchecked_inc(x: obj) = Numbers.getOps(x).unchecked_inc (x)
    static member unchecked_inc(x: double) = Numbers.inc (x)
    static member unchecked_inc(x: int64) = x + 1L
    static member unchecked_inc(x: uint64) = x + 1UL
    static member unchecked_inc(x: decimal) = Numbers.inc (x)

    static member dec(x: obj) = Numbers.getOps(x).dec (x)
    static member dec(x: double) = x - 1.0

    static member dec(x: int64) =
        if x = Int64.MinValue then
            raise <| Numbers.IntOverflow()
        else
            x - 1L

    static member dec(x: uint64) =
        if x = UInt64.MinValue then
            raise <| Numbers.IntOverflow()
        else
            x - 1UL

    static member dec(x: decimal) = x - 1M

    static member decP(x: obj) = Numbers.getOps(x).decP (x)
    static member decP(x: double) = x - 1.0

    static member decP(x: int64) =
        if x = Int64.MinValue then
            Numbers.BIGINT_OPS.dec (x :> obj)
        else
            x - 1L :> obj

    static member decP(x: uint64) =
        if x = UInt64.MinValue then
            Numbers.BIGINT_OPS.dec (x :> obj)
        else
            x - 1UL :> obj

    static member decP(x: decimal) = Numbers.addP (x, -1M)

    static member unchecked_dec(x: obj) = Numbers.getOps(x).unchecked_dec (x)
    static member unchecked_dec(x: double) = Numbers.dec (x)
    static member unchecked_dec(x: int64) = x - 1L
    static member unchecked_dec(x: uint64) = x - 1UL
    static member unchecked_dec(x: decimal) = Numbers.dec (x)

    // equal, compare

    static member equal(x: obj, y: obj) =
        OpsSelector.ops (x) = OpsSelector.ops (y) && Numbers.getOps(x, y).equiv (x, y)

    static member compare(x: obj, y: obj) =
        let xyops = Numbers.getOps (x, y)

        if xyops.lt (x, y) then -1
        elif xyops.lt (y, x) then 1
        else 0

    // equiv, lt, lte, gt, gte

    static member equiv(x: obj, y: obj) = Numbers.getOps(x, y).equiv (x, y)
    static member equiv(x: double, y: double) = x = y
    static member equiv(x: int64, y: int64) = x = y
    static member equiv(x: uint64, y: uint64) = x = y
    static member equiv(x: decimal, y: decimal) = x = y
    static member equiv(x: double, y: obj) = x = convertToDouble (y)
    static member equiv(x: obj, y: double) = convertToDouble (x) = y
    static member equiv(x: double, y: int64) = x = double (y)
    static member equiv(x: int64, y: double) = double (x) = y
    static member equiv(x: double, y: uint64) = x = double (y)
    static member equiv(x: uint64, y: double) = double (x) = y
    static member equiv(x: int64, y: obj) = Numbers.equiv (x :> obj, y)
    static member equiv(x: obj, y: int64) = Numbers.equiv (x, y :> obj)
    static member equiv(x: uint64, y: obj) = Numbers.equiv ((x :> obj), y)
    static member equiv(x: obj, y: uint64) = Numbers.equiv (x, y :> obj)
    static member equiv(x: int64, y: uint64) = Numbers.equiv (x :> obj, y :> obj)
    static member equiv(x: uint64, y: int64) = Numbers.equiv (x :> obj, y :> obj)

    static member lt(x: obj, y: obj) = Numbers.getOps(x, y).lt (x, y)
    static member lt(x: double, y: double) = x < y
    static member lt(x: int64, y: int64) = x < y
    static member lt(x: uint64, y: uint64) = x < y
    static member lt(x: decimal, y: decimal) = x < y
    static member lt(x: double, y: obj) = x < convertToDouble (y)
    static member lt(x: obj, y: double) = convertToDouble (x) < y
    static member lt(x: double, y: int64) = x < double (y)
    static member lt(x: int64, y: double) = double (x) < y
    static member lt(x: double, y: uint64) = x < double (y)
    static member lt(x: uint64, y: double) = double (x) < y
    static member lt(x: int64, y: obj) = Numbers.lt (x :> obj, y)
    static member lt(x: obj, y: int64) = Numbers.lt (x, y :> obj)
    static member lt(x: uint64, y: obj) = Numbers.lt ((x :> obj), y)
    static member lt(x: obj, y: uint64) = Numbers.lt (x, y :> obj)
    static member lt(x: int64, y: uint64) = Numbers.lt (x :> obj, y :> obj)
    static member lt(x: uint64, y: int64) = Numbers.lt (x :> obj, y :> obj)

    static member lte(x: obj, y: obj) = Numbers.getOps(x, y).lte (x, y)
    static member lte(x: double, y: double) = x <= y
    static member lte(x: int64, y: int64) = x <= y
    static member lte(x: uint64, y: uint64) = x <= y
    static member lte(x: decimal, y: decimal) = x <= y
    static member lte(x: double, y: obj) = x <= convertToDouble (y)
    static member lte(x: obj, y: double) = convertToDouble (x) <= y
    static member lte(x: double, y: int64) = x <= double (y)
    static member lte(x: int64, y: double) = double (x) <= y
    static member lte(x: double, y: uint64) = x <= double (y)
    static member lte(x: uint64, y: double) = double (x) <= y
    static member lte(x: int64, y: obj) = Numbers.lte (x :> obj, y)
    static member lte(x: obj, y: int64) = Numbers.lte (x, y :> obj)
    static member lte(x: uint64, y: obj) = Numbers.lte ((x :> obj), y)
    static member lte(x: obj, y: uint64) = Numbers.lte (x, y :> obj)
    static member lte(x: int64, y: uint64) = Numbers.lte (x :> obj, y :> obj)
    static member lte(x: uint64, y: int64) = Numbers.lte (x :> obj, y :> obj)

    static member gt(x: obj, y: obj) = Numbers.getOps(x, y).lt (y, x)
    static member gt(x: double, y: double) = x > y
    static member gt(x: int64, y: int64) = x > y
    static member gt(x: uint64, y: uint64) = x > y
    static member gt(x: decimal, y: decimal) = x > y
    static member gt(x: double, y: obj) = x > convertToDouble (y)
    static member gt(x: obj, y: double) = convertToDouble (x) > y
    static member gt(x: double, y: int64) = x > double (y)
    static member gt(x: int64, y: double) = double (x) > y
    static member gt(x: double, y: uint64) = x < double (y)
    static member gt(x: uint64, y: double) = double (x) > y
    static member gt(x: int64, y: obj) = Numbers.gt (x :> obj, y)
    static member gt(x: obj, y: int64) = Numbers.gt (x, y :> obj)
    static member gt(x: uint64, y: obj) = Numbers.gt ((x :> obj), y)
    static member gt(x: obj, y: uint64) = Numbers.gt (x, y :> obj)
    static member gt(x: int64, y: uint64) = Numbers.gt (x :> obj, y :> obj)
    static member gt(x: uint64, y: int64) = Numbers.gt (x :> obj, y :> obj)

    static member gte(x: obj, y: obj) = Numbers.getOps(x, y).gte (x, y)
    static member gte(x: double, y: double) = x >= y
    static member gte(x: int64, y: int64) = x >= y
    static member gte(x: uint64, y: uint64) = x >= y
    static member gte(x: decimal, y: decimal) = x >= y
    static member gte(x: double, y: obj) = x >= convertToDouble (y)
    static member gte(x: obj, y: double) = convertToDouble (x) >= y
    static member gte(x: double, y: int64) = x >= double (y)
    static member gte(x: int64, y: double) = double (x) >= y
    static member gte(x: double, y: uint64) = x >= double (y)
    static member gte(x: uint64, y: double) = double (x) >= y
    static member gte(x: int64, y: obj) = Numbers.gte (x :> obj, y)
    static member gte(x: obj, y: int64) = Numbers.gte (x, y :> obj)
    static member gte(x: uint64, y: obj) = Numbers.gte ((x :> obj), y)
    static member gte(x: obj, y: uint64) = Numbers.gte (x, y :> obj)
    static member gte(x: int64, y: uint64) = Numbers.gte (x :> obj, y :> obj)
    static member gte(x: uint64, y: int64) = Numbers.gte (x :> obj, y :> obj)


    // min, max, abs

    static member max(x: obj, y: obj) =
        if Numbers.IsNaN(x) then x
        elif Numbers.IsNaN(y) then y
        elif Numbers.gt (x, y) then x
        else y

    static member max(x: double, y: double) = Math.Max(x, y)
    static member max(x: int64, y: int64) = Math.Max(x, y)
    static member max(x: uint64, y: uint64) = Math.Max(x, y)
    static member max(x: decimal, y: decimal) = Math.Max(x, y)

    static member max(x: double, y: obj) =
        if Numbers.IsNaN(x) then x :> obj
        elif Numbers.IsNaN(y) then y
        elif x > convertToDouble (y) then x
        else y

    static member max(x: obj, y: double) =
        if Numbers.IsNaN(x) then x
        elif Numbers.IsNaN(y) then y
        elif convertToDouble (x) > y then x
        else y

    static member max(x: double, y: int64) : obj =
        if Numbers.IsNaN(x) then x
        elif x > double (y) then x
        else y

    static member max(x: int64, y: double) : obj =
        if Numbers.IsNaN(y) then y
        elif double (x) > y then x
        else y

    static member max(x: double, y: uint64) : obj =
        if Numbers.IsNaN(x) then x
        elif x > double (y) then x
        else y

    static member max(x: uint64, y: double) : obj =
        if Numbers.IsNaN(y) then y
        elif double (x) > y then x
        else y

    static member max(x: int64, y: obj) =
        if Numbers.IsNaN(y) then y
        elif Numbers.gt (x, y) then x
        else y

    static member max(x: obj, y: int64) =
        if Numbers.IsNaN(x) then x
        elif Numbers.gt (x, y) then x
        else y

    static member max(x: uint64, y: obj) =
        if Numbers.IsNaN(y) then y
        elif Numbers.gt (x, y) then x
        else y

    static member max(x: obj, y: uint64) =
        if Numbers.IsNaN(x) then x
        elif Numbers.gt (x, y) then x
        else y

    static member max(x: int64, y: uint64) : obj = if Numbers.gt (x, y) then x else y

    static member max(x: uint64, y: int64) : obj = if Numbers.gt (x, y) then x else y


    static member min(x: obj, y: obj) =
        if Numbers.IsNaN(x) then x
        elif Numbers.IsNaN(y) then y
        elif Numbers.lt (x, y) then x
        else y

    static member min(x: double, y: double) = Math.Min(x, y)
    static member min(x: int64, y: int64) = Math.Min(x, y)
    static member min(x: uint64, y: uint64) = Math.Min(x, y)
    static member min(x: decimal, y: decimal) = Math.Min(x, y)

    static member min(x: double, y: obj) =
        if Numbers.IsNaN(x) then x :> obj
        elif Numbers.IsNaN(y) then y
        elif x < convertToDouble (y) then x
        else y

    static member min(x: obj, y: double) =
        if Numbers.IsNaN(x) then x
        elif Numbers.IsNaN(y) then y
        elif convertToDouble (x) < y then x
        else y

    static member min(x: double, y: int64) : obj =
        if Numbers.IsNaN(x) then x
        elif x < double (y) then x
        else y

    static member min(x: int64, y: double) : obj =
        if Numbers.IsNaN(y) then y
        elif double (x) < y then x
        else y

    static member min(x: double, y: uint64) : obj =
        if Numbers.IsNaN(x) then x
        elif x < double (y) then x
        else y

    static member min(x: uint64, y: double) : obj =
        if Numbers.IsNaN(y) then y
        elif double (x) < y then x
        else y

    static member min(x: int64, y: obj) =
        if Numbers.IsNaN(y) then y
        elif Numbers.lt (x, y) then x
        else y

    static member min(x: obj, y: int64) =
        if Numbers.IsNaN(x) then x
        elif Numbers.lt (x, y) then x
        else y

    static member min(x: uint64, y: obj) =
        if Numbers.IsNaN(y) then y
        elif Numbers.lt (x, y) then x
        else y

    static member min(x: obj, y: uint64) =
        if Numbers.IsNaN(x) then x
        elif Numbers.lt (x, y) then x
        else y

    static member min(x: int64, y: uint64) : obj = if Numbers.lt (x, y) then x else y

    static member min(x: uint64, y: int64) : obj = if Numbers.lt (x, y) then x else y

    static member abs(x: obj) = Numbers.getOps(x).abs (x)

    // int overloads for basic ops -- needed by the compiler and core.clj

    static member unchecked_int_add(x: int, y: int) = x + y
    static member unchecked_int_subtract(x: int, y: int) = x - y
    static member unchecked_int_negate(x: int) = -x
    static member unchecked_int_inc(x: int) = x + 1
    static member unchecked_int_dec(x: int) = x - 1
    static member unchecked_int_multiply(x: int, y: int) = x * y
    static member unchecked_int_divide(x: int, y: int) = x / y
    static member unchecked_int_remainder(x: int, y: int) = x % y


    // utility methods

    //[<WarnedBoxMath(false)>]
    static member ToBigInt(x: obj) =
        match OpsSelector.ops(x) with
        | OpsType.BigInteger ->
            match x with
                | :? BigInt as bi -> bi
                | :? BigInteger as bi -> BigInt.fromBigInteger (bi)
                | _ -> raise <| InvalidOperationException("Unkown BigInteger type")
        | OpsType.Long -> BigInt.fromLong(convertToLong(x))
        | OpsType.Double -> BigInt.fromBigInteger(BigInteger(convertToDouble(x)))
        | OpsType.ULong ->
            let ul = convertToULong(x)
            if ul <= uint64(Int64.MaxValue) then
                BigInt.fromLong(int64(ul))
            else
                BigInt.fromBigInteger(BigInteger(ul))
        | OpsType.Ratio -> 
            let r = x :?> Ratio
            BigInt.fromBigInteger(r.ToBigDecimal().ToBigInteger())
        | OpsType.ClrDecimal -> 
            let d = x :?> decimal
            BigInt.fromBigInteger(BigDecimal.Create(d).ToBigInteger())
        | OpsType.BigDecimal -> 
            let bd = x :?> BigDecimal
            BigInt.fromBigInteger(bd.ToBigInteger())
        | _ -> raise <| InvalidOperationException("Unkown numeric OpsType")
                

    //[<WarnedBoxMath(false)>]
    static member ToBigInteger(x: obj) =
        match OpsSelector.ops(x) with
        | OpsType.BigInteger ->
            match x with
            | :? BigInteger as bi -> bi
            | :? BigInt as bi -> bi.ToBigInteger()
                | _ -> raise <| InvalidOperationException("Unkown BigInteger type")
        | OpsType.Long -> BigInteger(convertToLong(x))
        | OpsType.Double -> BigInteger(convertToDouble(x))
        | OpsType.ULong -> BigInteger(convertToULong(x))
        | OpsType.Ratio -> 
            let r = x :?> Ratio
            r.ToBigDecimal().ToBigInteger()
        | OpsType.ClrDecimal -> 
            let d = x :?> decimal
            BigDecimal.Create(d).ToBigInteger()
        | OpsType.BigDecimal -> 
            let bd = x :?> BigDecimal
            bd.ToBigInteger()
        | _ -> raise <| InvalidOperationException("Unkown numeric OpsType")

    //[<WarnedBoxMath(false)>]
    static member ToBigDecimal(x: obj) =
        match x with
        | :? BigDecimal as bd -> bd
        | :? BigInt as bi ->
            match bi.Bipart with
            | Some b -> BigDecimal.Create(b)
            | None -> BigDecimal.Create(bi.Lpart)
        | :? BigInteger as bi -> BigDecimal.Create(bi)
        | :? double as d -> BigDecimal.Create(d)
        | :? float32 as f -> BigDecimal.Create(double (f))
        | :? Ratio as r -> Numbers.divide (BigDecimal.Create(r.Numerator), r.Denominator) :?> BigDecimal
        | :? decimal as d -> BigDecimal.Create(d)
        | _ -> BigDecimal.Create(convertToDouble (x))


    static member private BigIntegerTen = BigInteger(10)

    //[<WarnedBoxMath(false)>]
    static member ToRatio(x: obj) =
        match x with
        | :? Ratio as r -> r
        | :? BigDecimal as bd ->
            let exp = bd.Exponent

            if exp >= 0 then
                Ratio(bd.ToBigInteger(), BigInteger.One)
            else
                Ratio(bd.MovePointRight(-exp).ToBigInteger(), BigInteger.Pow(Numbers.BigIntegerTen, -exp))
        | _ -> Ratio(Numbers.ToBigInteger(x), BigInteger.One)

    //[<WarnedBoxMath(false)>]
    static member rationalize(x: obj) : obj =
        match x with
        | :? double as d -> Numbers.rationalize (BigDecimal.Create(d))
        | :? float32 as f -> Numbers.rationalize (BigDecimal.Create(double (f)))
        | :? BigDecimal as bd ->
            let exp = bd.Exponent

            if exp >= 0 then
                BigInt.fromBigInteger (bd.ToBigInteger())
            else
                Numbers.divide (bd.MovePointRight(-exp).ToBigInteger(), BigInteger.Pow(Numbers.BigIntegerTen, -exp))
        | _ -> x

    // BigInteger support

    //[<WarnedBoxMath(false)>]
    static member ReduceBigInt(x: BigInt) : obj =
        match x.Bipart with
        | Some bip -> bip
        | None -> x.Lpart

    static member BIDivide(n: BigInteger, d: BigInteger) : obj =
        if d.IsZero then
            raise <| ArithmeticException("Divide by zero")
        else
            let gcd = BigInteger.GreatestCommonDivisor(n, d)

            if gcd.IsZero then
                BigInt.ZERO
            else
                let n1 = n / gcd
                let d1 = d / gcd

                if d1.IsOne then
                    BigInt.fromBigInteger (n1)
                elif d1.Equals(BigInteger.MinusOne) then
                    BigInt.fromBigInteger (-n1)
                else
                    Ratio((if d1 < BigInteger.Zero then -n1 else n1), BigInteger.Abs(d1))


    // basic bit operations

    static member bitOpsCast(x: obj) =
        match x with
        | :? int64 as n -> n
        | :? int32 as n -> int64 (n)
        | :? int16 as n -> int64 (n)
        | :? byte as n -> int64 (n)
        | :? uint64 as n -> int64 (n)
        | :? uint32 as n -> int64 (n)
        | :? uint16 as n -> int64 (n)
        | :? sbyte as n -> int64 (n)
        | _ -> raise <| ArgumentException($"bit operations not supported for: {x.GetType()}")

    static member shiftLeftInt(x: int, n: int) = x <<< n

    static member shiftLeft(x: obj, n: obj) =
        Numbers.shiftLeft (Numbers.bitOpsCast (x), Numbers.bitOpsCast (n))

    static member shiftLeft(x: obj, n: int64) =
        Numbers.shiftLeft (Numbers.bitOpsCast (x), n)

    static member shiftLeft(x: int64, n: obj) =
        Numbers.shiftLeft (x, Numbers.bitOpsCast (n))

    static member shiftLeft(x: int64, n: int64) = Numbers.shiftLeft (x, int (n))
    static member shiftLeft(x: int64, n: int) = x <<< n

    static member shiftRightInt(x: int, n: int) = x >>> n

    static member shiftRight(x: obj, n: obj) =
        Numbers.shiftRight (Numbers.bitOpsCast (x), Numbers.bitOpsCast (n))

    static member shiftRight(x: obj, n: int64) =
        Numbers.shiftRight (Numbers.bitOpsCast (x), n)

    static member shiftRight(x: int64, n: obj) =
        Numbers.shiftRight (x, Numbers.bitOpsCast (n))

    static member shiftRight(x: int64, n: int64) = Numbers.shiftRight (x, int (n))
    static member shiftRight(x: int64, n: int) = x >>> n

    static member unsignedShiftRightInt(x: int, n: int) = int (uint (x) >>> n)

    static member unsignedShiftRight(x: obj, y: obj) =
        Numbers.unsignedShiftRight (Numbers.bitOpsCast (x), Numbers.bitOpsCast (y))

    static member unsignedShiftRight(x: obj, y: int64) =
        Numbers.unsignedShiftRight (Numbers.bitOpsCast (x), y)

    static member unsignedShiftRight(x: int64, y: obj) =
        Numbers.unsignedShiftRight (x, Numbers.bitOpsCast (y))

    static member unsignedShiftRight(x: int64, y: int64) = int64 (uint64 (x) >>> int (y))

    // bit operations

    static member Not(x: obj) = Numbers.Not(Numbers.bitOpsCast (x))
    static member Not(x: int64) = ~~~x

    static member And(x: obj, y: obj) =
        Numbers.And(Numbers.bitOpsCast (x), Numbers.bitOpsCast (y))

    static member And(x: obj, y: int64) = Numbers.And(Numbers.bitOpsCast (x), y)
    static member And(x: int64, y: obj) = Numbers.And(x, Numbers.bitOpsCast (y))
    static member And(x: int64, y: int64) = x &&& y

    static member Or(x: obj, y: obj) =
        Numbers.Or(Numbers.bitOpsCast (x), Numbers.bitOpsCast (y))

    static member Or(x: obj, y: int64) = Numbers.Or(Numbers.bitOpsCast (x), y)
    static member Or(x: int64, y: obj) = Numbers.Or(x, Numbers.bitOpsCast (y))
    static member Or(x: int64, y: int64) = x ||| y

    static member Xor(x: obj, y: obj) =
        Numbers.Xor(Numbers.bitOpsCast (x), Numbers.bitOpsCast (y))

    static member Xor(x: obj, y: int64) = Numbers.Xor(Numbers.bitOpsCast (x), y)
    static member Xor(x: int64, y: obj) = Numbers.Xor(x, Numbers.bitOpsCast (y))
    static member Xor(x: int64, y: int64) = x ^^^ y

    static member AndNot(x: obj, y: obj) =
        Numbers.AndNot(Numbers.bitOpsCast (x), Numbers.bitOpsCast (y))

    static member AndNot(x: obj, y: int64) =
        Numbers.AndNot(Numbers.bitOpsCast (x), y)

    static member AndNot(x: int64, y: obj) =
        Numbers.AndNot(x, Numbers.bitOpsCast (y))

    static member AndNot(x: int64, y: int64) = x &&& (~~~y)

    static member ClearBit(x: obj, y: obj) =
        Numbers.ClearBit(Numbers.bitOpsCast (x), Numbers.bitOpsCast (y))

    static member ClearBit(x: obj, y: int64) =
        Numbers.ClearBit(Numbers.bitOpsCast (x), y)

    static member ClearBit(x: int64, y: obj) =
        Numbers.ClearBit(x, Numbers.bitOpsCast (y))

    static member ClearBit(x: int64, y: int64) = Numbers.ClearBit(x, int (y))
    static member ClearBit(x: int64, y: int) = x &&& ~~~(1L <<< y)

    static member SetBit(x: obj, y: obj) =
        Numbers.SetBit(Numbers.bitOpsCast (x), Numbers.bitOpsCast (y))

    static member SetBit(x: obj, y: int64) =
        Numbers.SetBit(Numbers.bitOpsCast (x), y)

    static member SetBit(x: int64, y: obj) =
        Numbers.SetBit(x, Numbers.bitOpsCast (y))

    static member SetBit(x: int64, y: int64) = Numbers.SetBit(x, int (y))
    static member SetBit(x: int64, y: int) = x ||| ~~~(1L <<< y)

    static member FlipBit(x: obj, y: obj) =
        Numbers.FlipBit(Numbers.bitOpsCast (x), Numbers.bitOpsCast (y))

    static member FlipBit(x: obj, y: int64) =
        Numbers.FlipBit(Numbers.bitOpsCast (x), y)

    static member FlipBit(x: int64, y: obj) =
        Numbers.FlipBit(x, Numbers.bitOpsCast (y))

    static member FlipBit(x: int64, y: int64) = Numbers.FlipBit(x, int (y))
    static member FlipBit(x: int64, y: int) = x ^^^ ~~~(1L <<< y)

    static member TestBit(x: obj, y: obj) =
        Numbers.TestBit(Numbers.bitOpsCast (x), Numbers.bitOpsCast (y))

    static member TestBit(x: obj, y: int64) =
        Numbers.TestBit(Numbers.bitOpsCast (x), y)

    static member TestBit(x: int64, y: obj) =
        Numbers.TestBit(x, Numbers.bitOpsCast (y))

    static member TestBit(x: int64, y: int64) = Numbers.TestBit(x, int (y))
    static member TestBit(x: int64, y: int) = (x &&& (1L <<< y)) <> 0


    // Numericity

    // the following were originally in Util

    static member private IsNumericType(t: Type) =
        match Type.GetTypeCode(t) with
        | TypeCode.SByte
        | TypeCode.Byte
        | TypeCode.Int16
        | TypeCode.Int32
        | TypeCode.Int64
        | TypeCode.Double
        | TypeCode.Single
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64 -> true
        | _ ->
            match t with
            | x when x = typeof<BigInt> -> true
            | x when x = typeof<BigInteger> -> true
            | x when x = typeof<BigDecimal> -> true
            | x when x = typeof<Ratio> -> true
            | _ -> false

    static member private IsPrimitiveNumericType(t: Type) =
        match Type.GetTypeCode(t) with
        | TypeCode.SByte
        | TypeCode.Byte
        | TypeCode.Int16
        | TypeCode.Int32
        | TypeCode.Int64
        | TypeCode.Double
        | TypeCode.Single
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64 -> true
        | _ -> false

    static member private IsPrimitiveIntegerType(t: Type) =
        match Type.GetTypeCode(t) with
        | TypeCode.SByte
        | TypeCode.Byte
        | TypeCode.Int16
        | TypeCode.Int32
        | TypeCode.Int64
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64 -> true
        | _ -> false

    static member IsNumeric(o: obj) =
        match o with
        | null -> false
        | _ -> Numbers.IsNumericType(o.GetType())


    // hashing support
    // originally in Util

    //[WarnBoxedMath(false)]
    static member hasheqFrom(x: obj, xt: Type) =
        if
            Numbers.IsPrimitiveIntegerType(xt)
            || xt = typeof<BigInteger>
               && Numbers.lte (x, Int64.MaxValue)
               && Numbers.gte (x, Int64.MaxValue)
        then
            let lpart = convertToLong (x)
            Murmur3.HashLong(lpart)
        elif xt = typeof<BigDecimal> then
            // stripTrailingZeros() to make all numerically equal
            // BigDecimal values come out the same before calling
            // hashCode.  Special check for 0 because
            // stripTrailingZeros() does not do anything to values
            // equal to 0 with different scales.
            if Numbers.isZero (x) then
                BigDecimal.Zero.GetHashCode()
            else
                let tmp = (x :?> BigDecimal).StripTrailingZeros()
                tmp.GetHashCode()
        elif xt = typeof<float32> && x.Equals(-0.0f) then
            0
        else
            x.GetHashCode()

    static member hasheq(x: obj) =
        match x with
        | :? int64 as n -> Murmur3.HashLong(n)
        | :? double as d ->
            if d = -0.0 then
                0 // match 0.0
            else
                d.GetHashCode()
        | _ -> Numbers.hasheqFrom (x, x.GetType())


and [<Sealed>] LongOps() =

    static member gcd(u: int64, v: int64) =
        let rec step x y = if y <> 0L then step y (x % y) else x
        step u v

    interface Ops with

        member this.isNeg(x: obj) : bool = convertToLong (x) < 0L
        member this.isPos(x: obj) : bool = convertToLong (x) > 0L
        member this.isZero(x: obj) : bool = convertToLong (x) = 0L


        member this.add(x, y) : obj =
            Numbers.add (convertToLong (x), convertToLong (y))

        member this.addP(x: obj, y: obj) : obj =
            let lx = convertToLong (x)
            let ly = convertToLong (y)
            let ret = lx + ly

            if (ret ^^^ lx) < 0 && (ret ^^^ ly) < 0 then
                Numbers.BIGINT_OPS.add (x, y)
            else
                ret :> obj

        member this.unchecked_add(x: obj, y: obj) : obj =
            Numbers.unchecked_add (convertToLong (x), convertToLong (y))


        member this.multiply(x, y) : obj =
            Numbers.multiply (convertToLong (x), convertToLong (y))

        member this.multiplyP(x: obj, y: obj) : obj =
            let lx = convertToLong (x)
            let ly = convertToLong (y)

            if lx = Int64.MinValue && ly < 0 then
                Numbers.BIGINT_OPS.multiply (x, y)
            else
                let ret = lx * ly

                if ly <> 0 && ret / ly <> lx then
                    Numbers.BIGINT_OPS.multiply (x, y)
                else
                    ret :> obj

        member this.unchecked_multiply(x: obj, y: obj) : obj =
            Numbers.unchecked_multiply (convertToLong (x), convertToLong (y))

        member this.divide(x: obj, y: obj) : obj =
            let n = convertToLong (x)
            let v = convertToLong (y)
            let gcd1 = LongOps.gcd (n, v)

            if gcd1 = 0 then
                0
            else
                let n = n / gcd1
                let d = v / gcd1

                match d with
                | 1L -> n
                | _ when d < 0L -> Ratio(BigInteger(-n), BigInteger(-d))
                | _ -> Ratio(BigInteger(n), BigInteger(d))


        member this.quotient(x: obj, y: obj) : obj =
            (convertToLong (x) / convertToLong (y)) :> obj

        member this.remainder(x: obj, y: obj) : obj =
            (convertToLong (x) % convertToLong (y)) :> obj

        member this.equiv(x: obj, y: obj) : bool = convertToLong (x) = convertToLong (y)

        member this.lt(x: obj, y: obj) : bool = convertToLong (x) < convertToLong (y)

        member this.lte(x: obj, y: obj) : bool = convertToLong (x) <= convertToLong (y)

        member this.gte(x: obj, y: obj) : bool = convertToLong (x) >= convertToLong (y)


        member this.negate(x: obj) : obj = Numbers.minus (convertToLong (x))

        member this.negateP(x: obj) : obj =
            let lx = convertToLong (x)

            if lx > Int64.MinValue then
                -lx
            else
                BigInt.fromBigInteger (- BigInteger(lx))

        member this.unchecked_negate(x: obj) : obj =
            Numbers.unchecked_minus (convertToLong (x))


        member this.inc(x: obj) : obj = Numbers.inc (convertToLong (x))

        member this.incP(x: obj) : obj =
            let lx = convertToLong (x)

            if lx < Int64.MaxValue then
                (lx + 1L) :> obj
            else
                Numbers.BIGINT_OPS.inc (x)

        member this.unchecked_inc(x: obj) : obj =
            Numbers.unchecked_inc (convertToLong (x))


        member this.dec(x: obj) : obj = Numbers.dec (convertToLong (x))

        member this.decP(x: obj) : obj =
            let lx = convertToLong (x)

            if lx > Int64.MinValue then
                (lx - 1L) :> obj
            else
                Numbers.BIGINT_OPS.dec (x)

        member this.unchecked_dec(x: obj) : obj =
            Numbers.unchecked_dec (convertToLong (x))


        member this.abs(x: obj) : obj = Math.Abs(convertToLong (x))


and [<Sealed>] ULongOps() =


    static member gcd(u: uint64, v: uint64) =
        let rec step x y = if y <> 0UL then step y (x % y) else x
        step u v

    interface Ops with

        member this.isNeg(x: obj) : bool = convertToULong (x) < 0UL
        member this.isPos(x: obj) : bool = convertToULong (x) > 0UL
        member this.isZero(x: obj) : bool = convertToULong (x) = 0UL


        member this.add(x, y) : obj =
            Numbers.add (convertToULong (x), convertToULong (y))

        member this.addP(x: obj, y: obj) : obj =
            let ulx = convertToULong (x)
            let uly = convertToULong (y)

            if ulx > UInt64.MaxValue - uly then
                Numbers.BIGINT_OPS.add (x, y)
            else
                (ulx + uly) :> obj

        member this.unchecked_add(x: obj, y: obj) : obj =
            Numbers.unchecked_add (convertToULong (x), convertToULong (y))


        member this.multiply(x, y) : obj =
            Numbers.multiply (convertToULong (x), convertToULong (y))

        member this.multiplyP(x: obj, y: obj) : obj =
            let ulx = convertToULong (x)
            let uly = convertToULong (y)

            let ret = ulx * uly

            if uly <> 0UL && ret / uly <> ulx then
                Numbers.BIGINT_OPS.multiply (x, y)
            else
                ret

        member this.unchecked_multiply(x: obj, y: obj) : obj =
            Numbers.unchecked_multiply (convertToULong (x), convertToULong (y))


        member this.divide(x: obj, y: obj) : obj =
            let n = convertToULong (x)
            let v = convertToULong (y)
            let gcd1 = ULongOps.gcd (n, v)

            if gcd1 = 0UL then
                0UL
            else
                let n = n / gcd1
                let d = v / gcd1

                match d with
                | 1UL -> n
                | _ -> Ratio(BigInteger(n), BigInteger(d))


        member this.quotient(x: obj, y: obj) : obj =
            (convertToULong (x) / convertToULong (y)) :> obj

        member this.remainder(x: obj, y: obj) : obj =
            (convertToULong (x) % convertToULong (y)) :> obj

        member this.equiv(x: obj, y: obj) : bool = convertToULong (x) = convertToULong (y)

        member this.lt(x: obj, y: obj) : bool = convertToULong (x) < convertToULong (y)

        member this.lte(x: obj, y: obj) : bool =
            convertToULong (x) <= convertToULong (y)

        member this.gte(x: obj, y: obj) : bool =
            convertToULong (x) >= convertToULong (y)


        member this.negate(x: obj) : obj =
            let lx = convertToULong (x)

            if lx = 0UL then
                x
            else
                raise
                <| ArithmeticException("Checked operation error: negation of non-zero unsigned")

        member this.negateP(x: obj) : obj =
            let lx = convertToULong(x)
            if lx = 0UL then
                x
            else 
                BigInt.fromBigInteger (- BigInteger(lx))

        member this.unchecked_negate(x: obj) : obj =
            Numbers.unchecked_minus (convertToULong (x))


        member this.inc(x: obj) : obj = Numbers.inc (convertToULong (x))

        member this.incP(x: obj) : obj =
            let lx = convertToULong (x)

            if lx < UInt64.MaxValue then
                (lx + 1UL) :> obj
            else
                Numbers.BIGINT_OPS.inc (x)

        member this.unchecked_inc(x: obj) : obj =
            Numbers.unchecked_inc (convertToULong (x))


        member this.dec(x: obj) : obj = Numbers.dec (convertToULong (x))

        member this.decP(x: obj) : obj =
            let lx = convertToULong (x)

            if lx > 0UL then
                (lx - 1UL) :> obj
            else
                Numbers.BIGINT_OPS.dec (x)

        member this.unchecked_dec(x: obj) : obj =
            Numbers.unchecked_dec (convertToULong (x))


        member this.abs(x: obj) : obj = convertToULong (x)


and [<Sealed>] DoubleOps() =
    inherit OpsP()

    interface Ops with

        member this.isNeg(x: obj) : bool = convertToDouble (x) < 0.0
        member this.isPos(x: obj) : bool = convertToDouble (x) > 0.0
        member this.isZero(x: obj) : bool = convertToDouble (x) = 0.0

        member this.add(x, y) : obj =
            convertToDouble (x) + convertToDouble (y) :> obj

        member this.multiply(x, y) : obj =
            convertToDouble (x) * convertToDouble (y) :> obj

        member this.divide(x: obj, y: obj) : obj =
            convertToDouble (x) / convertToDouble (y) :> obj

        member this.quotient(x: obj, y: obj) : obj =
            Numbers.quotient (convertToDouble (x), convertToDouble (y))

        member this.remainder(x: obj, y: obj) : obj =
            Numbers.remainder (convertToDouble (x), convertToDouble (y))

        member this.equiv(x: obj, y: obj) : bool =
            convertToDouble (x) = convertToDouble (y)

        member this.lt(x: obj, y: obj) : bool =
            convertToDouble (x) < convertToDouble (y)

        member this.lte(x: obj, y: obj) : bool =
            convertToDouble (x) <= convertToDouble (y)

        member this.gte(x: obj, y: obj) : bool =
            convertToDouble (x) >= convertToDouble (y)

        member this.negate(x: obj) : obj = - convertToDouble(x)
        member this.inc(x: obj) : obj = (convertToDouble (x) + 1.0) :> obj
        member this.dec(x: obj) : obj = (convertToDouble (x) - 1.0) :> obj

        member this.abs(x: obj) : obj = Math.Abs(convertToDouble (x))

and [<Sealed>] RatioOps() =
    inherit OpsP()

    interface Ops with

        member this.isNeg(x: obj) : bool = (Numbers.ToRatio(x)).Numerator.Sign < 0
        member this.isPos(x: obj) : bool = (Numbers.ToRatio(x)).Numerator.Sign > 0
        member this.isZero(x: obj) : bool = (Numbers.ToRatio(x)).Numerator.Sign = 0

        member this.add(x, y) : obj =
            let rx = Numbers.ToRatio(x)
            let ry = Numbers.ToRatio(y)

            Numbers.divide (
                ry.Numerator * rx.Denominator + rx.Numerator * ry.Denominator,
                ry.Denominator * rx.Denominator
            )

        member this.multiply(x, y) : obj =
            let rx = Numbers.ToRatio(x)
            let ry = Numbers.ToRatio(y)
            Numbers.divide (ry.Numerator * rx.Numerator, ry.Denominator * rx.Denominator)

        member this.divide(x: obj, y: obj) : obj =
            let rx = Numbers.ToRatio(x)
            let ry = Numbers.ToRatio(y)
            Numbers.divide (ry.Denominator * rx.Numerator, ry.Numerator * rx.Denominator)

        member this.quotient(x: obj, y: obj) : obj =
            let rx = Numbers.ToRatio(x)
            let ry = Numbers.ToRatio(y)
            (rx.Numerator * ry.Denominator) / (rx.Denominator * ry.Numerator) :> obj

        member this.remainder(x: obj, y: obj) : obj =
            let rx = Numbers.ToRatio(x)
            let ry = Numbers.ToRatio(y)
            let q = (rx.Numerator * ry.Denominator) / (rx.Denominator * ry.Numerator)
            Numbers.minus (rx, Numbers.multiply (q, ry))

        member this.equiv(x: obj, y: obj) : bool =
            let rx = Numbers.ToRatio(x)
            let ry = Numbers.ToRatio(y)
            rx.Numerator.Equals(ry.Numerator) && rx.Denominator.Equals(ry.Denominator)

        member this.lt(x: obj, y: obj) : bool =
            let rx = Numbers.ToRatio(x)
            let ry = Numbers.ToRatio(y)
            rx.Numerator * ry.Denominator < ry.Numerator * rx.Denominator

        member this.lte(x: obj, y: obj) : bool =
            let rx = Numbers.ToRatio(x)
            let ry = Numbers.ToRatio(y)
            rx.Numerator * ry.Denominator <= ry.Numerator * rx.Denominator

        member this.gte(x: obj, y: obj) : bool =
            let rx = Numbers.ToRatio(x)
            let ry = Numbers.ToRatio(y)
            rx.Numerator * ry.Denominator >= ry.Numerator * rx.Denominator

        member this.negate(x: obj) : obj =
            let rx = Numbers.ToRatio(x)
            Ratio(-rx.Numerator, rx.Denominator)

        member this.inc(x: obj) : obj = Numbers.add (x, 1)
        member this.dec(x: obj) : obj = Numbers.add (x, -1)

        member this.abs(x: obj) : obj =
            let rx = Numbers.ToRatio(x)
            Ratio(BigInteger.Abs(rx.Numerator), rx.Denominator)

and [<Sealed>] ClrDecimalOps() =

    interface Ops with

        member this.isNeg(x: obj) : bool = convertToDecimal (x) < 0M
        member this.isPos(x: obj) : bool = convertToDecimal (x) > 0M
        member this.isZero(x: obj) : bool = convertToDecimal (x) = 0M


        member this.add(x, y) : obj =
            let dx = convertToDecimal (x)
            let dy = convertToDecimal (y)
            dx + dy :> obj

        member this.addP(x: obj, y: obj) : obj =
            let dx = convertToDecimal (x)
            let dy = convertToDecimal (y)
            try
                dx + dy :> obj
            with
            | :? OverflowException -> Numbers.BIGDEC_OPS.add(x,y)

        member this.unchecked_add(x: obj, y: obj) : obj =
            let dx = convertToDecimal (x)
            let dy = convertToDecimal (y)
            dx + dy :> obj           


        member this.multiply(x, y) : obj =
            let dx = convertToDecimal (x)
            let dy = convertToDecimal (y)
            dx * dy :> obj

        member this.multiplyP(x: obj, y: obj) : obj =
            let dx = convertToDecimal (x)
            let dy = convertToDecimal (y)
            try
                dx * dy :> obj
            with
            | :? OverflowException -> Numbers.BIGDEC_OPS.multiply(x,y)

        member this.unchecked_multiply(x: obj, y: obj) : obj =
            let dx = convertToDecimal (x)
            let dy = convertToDecimal (y)
            dx * dy :> obj


        member this.divide(x: obj, y: obj) : obj =
            let dx = convertToDecimal (x)
            let dy = convertToDecimal (y)
            try 
                dx / dy :> obj
            with 
            | :? OverflowException -> BigDecimal.Create(dx).Divide(BigDecimal.Create(dy))


        member this.quotient(x: obj, y: obj) : obj =
            let dx = convertToDecimal (x)
            let dy = convertToDecimal (y)
            Numbers.quotient(dx,dy)

        member this.remainder(x: obj, y: obj) : obj =
            let dx = convertToDecimal (x)
            let dy = convertToDecimal (y)
            Numbers.remainder(dx,dy)

        member this.equiv(x: obj, y: obj) : bool = convertToDecimal (x) = convertToDecimal (y)

        member this.lt(x: obj, y: obj) : bool = convertToDecimal (x) < convertToDecimal (y)

        member this.lte(x: obj, y: obj) : bool = convertToDecimal (x) <= convertToDecimal (y)

        member this.gte(x: obj, y: obj) : bool = convertToDecimal (x) >= convertToDecimal (y)


        member this.negate(x: obj) : obj = - convertToDecimal(x)

        member this.negateP(x: obj) : obj = - convertToDecimal(x)

        member this.unchecked_negate(x: obj) : obj = - convertToDecimal(x)

        member this.inc(x: obj) : obj = convertToDecimal(x) + 1M :> obj

        member this.incP(x: obj) : obj =
            let dx = convertToDecimal (x)
            try 
                dx + 1M :> obj
            with 
            | :? OverflowException -> Numbers.BIGDEC_OPS.inc(BigDecimal.Create(dx))

        member this.unchecked_inc(x: obj) : obj = convertToDecimal(x) + 1M :> obj

        member this.dec(x: obj) : obj = convertToDecimal(x) - 1M :> obj

        member this.decP(x: obj) : obj =
            let dx = convertToDecimal (x)
            try 
                dx - 1M :> obj
            with 
            | :? OverflowException -> Numbers.BIGDEC_OPS.dec(BigDecimal.Create(dx))

        member this.unchecked_dec(x: obj) : obj = convertToDecimal(x) + 1M :> obj

        member this.abs(x: obj) : obj = Math.Abs(convertToDecimal (x))


and [<Sealed>] BigIntOps() =
    inherit OpsP()

    interface Ops with

        member this.isNeg(x: obj) : bool =
            let bx = Numbers.ToBigInt(x)

            match bx.Bipart with
            | Some bi -> bi.Sign < 0
            | None -> bx.Lpart < 0L

        member this.isPos(x: obj) : bool =
            let bx = Numbers.ToBigInt(x)

            match bx.Bipart with
            | Some bi -> bi.Sign > 0
            | None -> bx.Lpart > 0L

        member this.isZero(x: obj) : bool =
            let bx = Numbers.ToBigInt(x)

            match bx.Bipart with
            | Some bi -> bi.IsZero
            | None -> bx.Lpart = 0L

        member this.add(x, y) : obj =
            (Numbers.ToBigInt(x)).add (Numbers.ToBigInt(y))

        member this.multiply(x, y) : obj =
            (Numbers.ToBigInt(x)).multiply (Numbers.ToBigInt(y))

        member this.divide(x: obj, y: obj) : obj =
            Numbers.BIDivide(Numbers.ToBigInteger(x), Numbers.ToBigInteger(y))

        member this.quotient(x: obj, y: obj) : obj =
            (Numbers.ToBigInt(x)).quotient (Numbers.ToBigInt(y))

        member this.remainder(x: obj, y: obj) : obj =
            (Numbers.ToBigInt(x)).remainder (Numbers.ToBigInt(y))

        member this.equiv(x: obj, y: obj) : bool =
            Numbers.ToBigInt(x).Equals(Numbers.ToBigInt(y))

        member this.lt(x: obj, y: obj) : bool =
            Numbers.ToBigInt(x).lt (Numbers.ToBigInt(y))

        member this.lte(x: obj, y: obj) : bool =
            Numbers.ToBigInteger(x).CompareTo(Numbers.ToBigInt(y)) <= 0

        member this.gte(x: obj, y: obj) : bool =
            Numbers.ToBigInteger(x).CompareTo(Numbers.ToBigInt(y)) >= 0

        member this.negate(x: obj) : obj =
            BigInt.fromBigInteger (- Numbers.ToBigInteger(x))

        member this.inc(x: obj) : obj =
            BigInt.fromBigInteger (Numbers.ToBigInteger(x) + BigInteger.One)

        member this.dec(x: obj) : obj =
            BigInt.fromBigInteger (Numbers.ToBigInteger(x) - BigInteger.One)

        member this.abs(x: obj) : obj =
            BigInt.fromBigInteger (BigInteger.Abs(Numbers.ToBigInteger(x)))


and [<Sealed>] BigDecimalOps() =
    inherit OpsP()

        // Eventually, need to modify this to pick up the value from RT.MathContextVar.deref()  -- TODO, TODO, TODO
        static member GetContext() : Context option = None

    interface Ops with

        member this.isNeg(x: obj) : bool = 
            let bx = x :?> BigDecimal  // In JVM code, they cast here instead of calling Numbers.ToBigDecimal 
            bx.IsNegative


        member this.isPos(x: obj) : bool = 
            let bx = x :?> BigDecimal  // In JVM code, they cast here instead of calling Numbers.ToBigDecimal 
            bx.IsPositive

        member this.isZero(x: obj) : bool = 
            let bx = x :?> BigDecimal  // In JVM code, they cast here instead of calling Numbers.ToBigDecimal 
            bx.IsZero


        member this.add(x, y) : obj =
            let bx = Numbers.ToBigDecimal(x)
            let by = Numbers.ToBigDecimal(y)
            let c = BigDecimalOps.GetContext()
            match c with
            | Some cx -> bx.Add(by,cx)
            | None -> bx.Add(by)

        member this.multiply(x, y) : obj =
            let bx = Numbers.ToBigDecimal(x)
            let by = Numbers.ToBigDecimal(y)
            let c = BigDecimalOps.GetContext()
            match c with
            | Some cx -> bx.Multiply(by,cx)
            | None -> bx.Multiply(by)

        member this.divide(x: obj, y: obj) : obj =
            let bx = Numbers.ToBigDecimal(x)
            let by = Numbers.ToBigDecimal(y)
            let c = BigDecimalOps.GetContext()
            match c with
            | Some cx -> bx.Divide(by,cx)
            | None -> bx.Divide(by)

        member this.quotient(x: obj, y: obj) : obj =
            let bx = Numbers.ToBigDecimal(x)
            let by = Numbers.ToBigDecimal(y)
            let c = BigDecimalOps.GetContext()
            match c with
            | Some cx -> bx.DivideInteger(by,cx)
            | None -> bx.DivideInteger(by)

        member this.remainder(x: obj, y: obj) : obj =
            let bx = Numbers.ToBigDecimal(x)
            let by = Numbers.ToBigDecimal(y)
            let c = BigDecimalOps.GetContext()
            match c with
            | Some cx -> bx.Mod(by,cx)
            | None -> bx.Mod(by)

        member this.equiv(x: obj, y: obj) : bool =
            (Numbers.ToBigDecimal(x):>IComparable<BigDecimal>).CompareTo(Numbers.ToBigDecimal(y)) = 0

        member this.lt(x: obj, y: obj) : bool =
            (Numbers.ToBigDecimal(x):>IComparable<BigDecimal>).CompareTo(Numbers.ToBigDecimal(y)) < 0

        member this.lte(x: obj, y: obj) : bool =
            (Numbers.ToBigDecimal(x):>IComparable<BigDecimal>).CompareTo(Numbers.ToBigDecimal(y)) >= 0

        member this.gte(x: obj, y: obj) : bool =
            (Numbers.ToBigDecimal(x):>IComparable<BigDecimal>).CompareTo(Numbers.ToBigDecimal(y)) >= 0

        member this.negate(x: obj) : obj = 
            let bx = Numbers.ToBigDecimal(x)
            let c = BigDecimalOps.GetContext()
            match c with
            | Some cx -> bx.Negate(cx)
            | None -> bx.Negate()

        member this.inc(x: obj) : obj =
            let bx = Numbers.ToBigDecimal(x)
            let c = BigDecimalOps.GetContext()
            match c with
            | Some cx -> bx.Add(BigDecimal.One,cx)
            | None -> bx.Add(BigDecimal.One)

        member this.dec(x: obj) : obj = 
            let bx = Numbers.ToBigDecimal(x)
            let c = BigDecimalOps.GetContext()
            match c with
            | Some cx -> bx.Subtract(BigDecimal.One,cx)
            | None -> bx.Subtract(BigDecimal.One)


        member this.abs(x: obj) : obj = 
            let bx = Numbers.ToBigDecimal(x)
            let c = BigDecimalOps.GetContext()
            match c with
            | Some cx -> bx.Abs(cx)
            | None -> bx.Abs()


and [<AbstractClass; Sealed>] OpsImpls =
    static member Long: Ops = LongOps()
    static member ULong = ULongOps()
    static member Double = DoubleOps()
    static member Ratio = RatioOps()
    static member BigInt = BigIntOps()
    static member BigDecimal = BigDecimalOps()
    static member ClrDecimal = ClrDecimalOps()
