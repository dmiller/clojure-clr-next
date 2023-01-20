namespace Clojure.Numerics

open System
open Converters
open System.Numerics
open Clojure.BigArith


type NumericOpsType =
    | Long = 0
    | ULong = 1
    | BigInteger = 2
    | Ratio = 3
    | BigDecimal = 4
    | Double = 5

type Ops = 
    abstract isZero: x:obj -> bool
    abstract isPos: x:obj -> bool
    abstract isNeg: x:obj -> bool

    abstract add: x:obj * y:obj -> obj
    abstract addP: x:obj * y:obj -> obj
    abstract unchecked_add: x:obj * y:obj -> obj
    abstract multiply: x:obj * y:obj -> obj
    abstract multiplyP: x:obj * y:obj -> obj
    abstract unchecked_multiply: x:obj * y:obj -> obj
    abstract divide: x:obj * y:obj -> obj
    abstract quotient: x:obj * y:obj -> obj
    abstract remainder: x:obj * y:obj -> obj

    abstract equiv: x:obj * y:obj -> bool
    abstract lt: x:obj * y:obj -> bool
    abstract lte: x:obj * y:obj -> bool
    abstract gte: x:obj * y:obj -> bool

    abstract negate: x:obj -> obj
    abstract negateP: x:obj -> obj
    abstract unchecked_negate: x:obj -> obj
    abstract inc: x:obj -> obj
    abstract incP: x:obj -> obj
    abstract unchecked_inc: x:obj -> obj
    abstract dec: x:obj -> obj
    abstract decP: x:obj -> obj
    abstract unchecked_dec: x:obj -> obj

[<AbstractClass>]
type OpsP() = 

    interface Ops with
        member this.add(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.dec(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.divide(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.equiv(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.gte(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.inc(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.isNeg(x: obj): bool = 
            raise (System.NotImplementedException())
        member this.isPos(x: obj): bool = 
            raise (System.NotImplementedException())
        member this.isZero(x: obj): bool = 
            raise (System.NotImplementedException())
        member this.lt(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.lte(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.multiply(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.negate(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.quotient(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.remainder(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())

        member this.addP(x,y) = (this:>Ops).add(x,y)
        member this.unchecked_add(x,y) = (this:>Ops).add(x,y)
        member this.multiplyP(x,y) = (this:>Ops).multiply(x,y)
        member this.unchecked_multiply(x,y) = (this:>Ops).multiply(x,y)
        member this.negateP(x) = (this:>Ops).negate(x)
        member this.unchecked_negate(x) = (this:>Ops).negate(x)
        member this.incP(x) = (this:>Ops).inc(x)
        member this.unchecked_inc(x) = (this:>Ops).inc(x)
        member this.decP(x) = (this:>Ops).dec(x)
        member this.unchecked_dec(x) = (this:>Ops).dec(x)

type [<AbstractClass;Sealed >] Numbers =

    static member ops(x:obj) = 
        match (Type.GetTypeCode(x.GetType())) with
        | TypeCode.Byte
        | TypeCode.SByte
        | TypeCode.Int16
        | TypeCode.Int32
        | TypeCode.Int64 -> OpsImpls.Long
        | TypeCode.UInt16
        | TypeCode.UInt32
        | TypeCode.UInt64 -> OpsImpls.ULong
        | TypeCode.Single
        | TypeCode.Double -> OpsImpls.Double
        | TypeCode.Decimal -> OpsImpls.ClrDecimal
        | _ ->
            match x with
            | :? BigInt -> OpsImpls.BigInt
            | :? BigInteger -> OpsImpls.BigInt
            | :? Ratio -> OpsImpls.Ratio
            | :? BigDecimal -> OpsImpls.BigDecimal
            | _ -> OpsImpls.Long

   
    

and LongOps() =
    
    interface Ops with
        member this.add(x,y) = (convertToLong(x) + convertToLong(y)) :> obj // *** number.add
        member this.addP(x: obj, y: obj): obj = 
            let lx = convertToLong(x)
            let ly = convertToLong(y)
            let ret = lx+ly
            if ( ret ^^^ lx) < 0 && (ret ^^^ ly) < 0 then
                OpsImpls.BigInt.add(x,y)
            else
                ret :> obj
            
        member this.dec(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.decP(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.divide(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.equiv(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.gte(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.inc(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.incP(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.isNeg(x: obj): bool =  convertToLong(x) < 0L
        member this.isPos(x: obj): bool = convertToLong(x) > 0L
        member this.isZero(x: obj): bool = convertToLong(x) = 0L
        member this.lt(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.lte(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.multiply(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.multiplyP(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.negate(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.negateP(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.quotient(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.remainder(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.unchecked_add(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.unchecked_dec(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.unchecked_inc(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.unchecked_multiply(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.unchecked_negate(x: obj): obj = 
            raise (System.NotImplementedException())

and ULongOps() =

    interface Ops with
        member this.add(x,y) = 
              raise (System.NotImplementedException())
        member this.addP(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.dec(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.decP(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.divide(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.equiv(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.gte(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.inc(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.incP(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.isNeg(x: obj): bool = 
             raise (System.NotImplementedException())
        member this.isPos(x: obj): bool =
             raise (System.NotImplementedException())
        member this.isZero(x: obj): bool = 
             raise (System.NotImplementedException())
        member this.lt(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.lte(x: obj, y: obj): bool = 
            raise (System.NotImplementedException())
        member this.multiply(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.multiplyP(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.negate(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.negateP(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.quotient(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.remainder(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.unchecked_add(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.unchecked_dec(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.unchecked_inc(x: obj): obj = 
            raise (System.NotImplementedException())
        member this.unchecked_multiply(x: obj, y: obj): obj = 
            raise (System.NotImplementedException())
        member this.unchecked_negate(x: obj): obj = 
            raise (System.NotImplementedException())

and DoubleOps() = 
    inherit OpsP()

and RatioOps() =
    inherit OpsP()

and ClrDecimalOps() =
    inherit OpsP()

and BigIntOps() =
    inherit OpsP()

and BigDecimalOps() =
    inherit OpsP()

and [<AbstractClass;Sealed>]  OpsImpls = 
    static member Long  : Ops = LongOps()
    static member ULong = ULongOps()
    static member Double = DoubleOps()
    static member Ratio = RatioOps()
    static member BigInt = BigIntOps()
    static member BigDecimal = BigDecimalOps()
    static member ClrDecimal = ClrDecimalOps()

 
        
// |          Method |     Mean |   Error |   StdDev | Ratio | RatioSD |
// |---------------- |---------:|--------:|---------:|------:|--------:|
// |     TypeCombine | 349.9 us | 6.98 us | 10.23 us |  1.00 |    0.00 |
// |   LookupCombine | 178.9 us | 2.81 us |  2.63 us |  0.51 |    0.02 |
// | LookupCombine2D | 168.9 us | 2.04 us |  1.91 us |  0.48 |    0.02 |

//let combinerArray = Array2D.create 
