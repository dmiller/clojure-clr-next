module Converters

open System
open System.Globalization

let convertToInt64TypeCode (o: obj) : int64 =
    match Type.GetTypeCode(o.GetType()) with
    | TypeCode.Byte -> int64 (o :?> Byte)
    | TypeCode.Char -> int64 (o :?> Char)
    | TypeCode.Decimal -> int64 (o :?> Decimal)
    | TypeCode.Double -> int64 (o :?> Double)
    | TypeCode.Int16 -> int64 (o :?> Int16)
    | TypeCode.Int32 -> int64 (o :?> Int32)
    | TypeCode.Int64 -> int64 (o :?> Int64)
    | TypeCode.SByte -> int64 (o :?> SByte)
    | TypeCode.Single -> int64 (o :?> Single)
    | TypeCode.UInt16 -> int64 (o :?> UInt16)
    | TypeCode.UInt32 -> int64 (o :?> UInt32)
    | TypeCode.UInt64 -> int64 (o :?> UInt64)
    | _ -> Convert.ToInt64(o, CultureInfo.InvariantCulture)


let convertToInt64CastingAlpha (o: obj) : int64 =
    match o with
    | :? Byte as b -> int64 (b)
    | :? Char as c -> int64 (c)
    | :? Decimal as d -> int64 (d)
    | :? Double as d -> int64 (d)
    | :? Int16 as i -> int64 (i)
    | :? Int32 as i -> int64 (i)
    | :? Int64 as i -> int64 (i)
    | :? SByte as s -> int64 (s)
    | :? Single as s -> int64 (s)
    | :? UInt16 as u -> int64 (u)
    | :? UInt32 as u -> int64 (u)
    | :? UInt64 as u -> int64 (u)
    | _ -> Convert.ToInt64(o, CultureInfo.InvariantCulture)

let convertToInt64CastingNasty (o: obj) : int64 =
    match o with
    | :? Byte as b -> int64 (b)
    | :? Char as c -> int64 (c)
    | :? Decimal as d -> int64 (d)
    | :? Int16 as i -> int64 (i)
    | :? SByte as s -> int64 (s)
    | :? Single as s -> int64 (s)
    | :? UInt16 as u -> int64 (u)
    | :? UInt32 as u -> int64 (u)
    | :? UInt64 as u -> int64 (u)
    | :? Int32 as i -> int64 (i)
    | :? Int64 as i -> int64 (i)
    | :? Double as d -> int64 (d)
    | _ -> Convert.ToInt64(o, CultureInfo.InvariantCulture)

let convertToInt64CastingNice (o: obj) : int64 =
    match o with
    | :? Int32 as i -> int64 (i)
    | :? Int64 as i -> int64 (i)
    | :? Double as d -> int64 (d)
    | :? Byte as b -> int64 (b)
    | :? Char as c -> int64 (c)
    | :? Decimal as d -> int64 (d)
    | :? Int16 as i -> int64 (i)
    | :? SByte as s -> int64 (s)
    | :? Single as s -> int64 (s)
    | :? UInt16 as u -> int64 (u)
    | :? UInt32 as u -> int64 (u)
    | :? UInt64 as u -> int64 (u)
    | _ -> Convert.ToInt64(o, CultureInfo.InvariantCulture)


let convertToInt64Directly (o: obj) : int64 =
    Convert.ToInt64(o, CultureInfo.InvariantCulture)


type Category =
    | SignedInteger = 0
    | UnsignedInteger = 1
    | Floating = 2
    | Decimal = 3
    | Other = 4


let categorizeByTypeCode (o: obj) : Category =
    match Type.GetTypeCode(o.GetType()) with

    | TypeCode.Single
    | TypeCode.Double -> Category.Floating

    | TypeCode.Int16
    | TypeCode.Int32
    | TypeCode.Int64
    | TypeCode.SByte -> Category.SignedInteger

    | TypeCode.Byte
    | TypeCode.UInt16
    | TypeCode.UInt32
    | TypeCode.UInt64 -> Category.UnsignedInteger

    | TypeCode.Decimal -> Category.Decimal

    | _ -> Category.Other


let categorizeByType (o: obj) =
    match o with

    | :? Single
    | :? Double -> Category.Floating

    | :? Int16
    | :? Int32
    | :? Int64
    | :? SByte -> Category.SignedInteger

    | :? Byte
    | :? UInt16
    | :? UInt32
    | :? UInt64 -> Category.UnsignedInteger

    | :? Decimal -> Category.Decimal

    | _ -> Category.Other
