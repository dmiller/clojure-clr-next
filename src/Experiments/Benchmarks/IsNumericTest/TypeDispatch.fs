
module TypeDispatch

open System

type TypeDispatch() =

    static member public IsNumericType(t: Type) =
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
        | _ -> System.Type.op_Equality(t, typeof<System.Numerics.BigInteger>)

    static member IsNumeric(o: obj) =
        match o with
        | null -> false
        | _ -> TypeDispatch.IsNumericType(o.GetType())
