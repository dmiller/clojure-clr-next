module FsharpTypeDispatch

open System

type TypeDispatch() =

    static member public IsNumericType(t: Type) =
        match Type.GetTypeCode(t) with
        | TypeCode.SByte
        | TypeCode.Byte
        | TypeCode.Int16
        | TypeCode.UInt16
        | TypeCode.Int32
        | TypeCode.UInt32
        | TypeCode.Int64
        | TypeCode.UInt64
        | TypeCode.Single
        | TypeCode.Double -> true
        | _ -> Type.op_Equality(t, typeof<System.Numerics.BigInteger>)

    static member public IsNumericType2(t: Type) =
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
        | _ -> t = typeof<System.Numerics.BigInteger>


    static member public IsNumericType3(t: Type) =
        let tc : uint =  uint (Type.GetTypeCode(t)) 
        tc  - 5u <= 9u ||   Type.op_Equality(t, typeof<System.Numerics.BigInteger>)


