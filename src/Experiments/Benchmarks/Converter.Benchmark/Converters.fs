module Converters

open System
open System.Globalization

let convertToIntTypeCode (o: obj) : int =
    match Type.GetTypeCode(o.GetType()) with
    | TypeCode.Byte -> int (o :?> Byte)
    | TypeCode.Char -> int (o :?> Char)
    | TypeCode.Decimal -> int (o :?> Decimal)
    | TypeCode.Double -> int (o :?> Double)
    | TypeCode.Int16 -> int (o :?> Int16)
    | TypeCode.Int32 -> int (o :?> Int32)
    | TypeCode.Int64 -> int (o :?> Int64)
    | TypeCode.SByte -> int (o :?> SByte)
    | TypeCode.Single -> int (o :?> Single)
    | TypeCode.UInt16 -> int (o :?> UInt16)
    | TypeCode.UInt32 -> int (o :?> UInt32)
    | TypeCode.UInt64 -> int (o :?> UInt64)
    | _ -> Convert.ToInt32(o, CultureInfo.InvariantCulture)


let convertToIntCastingAlpha (o: obj) : int =
    match o with
    | :? Byte as b -> int (b)
    | :? Char as c -> int (c)
    | :? Decimal as d -> int (d)
    | :? Double as d -> int (d)
    | :? Int16 as i -> int (i)
    | :? Int32 as i -> int (i)
    | :? Int64 as i -> int (i)
    | :? SByte as s -> int (s)
    | :? Single as s -> int (s)
    | :? UInt16 as u -> int (u)
    | :? UInt32 as u -> int (u)
    | :? UInt64 as u -> int (u)
    | _ -> Convert.ToInt32(o, CultureInfo.InvariantCulture)

let convertToIntCastingNasty (o: obj) : int =
    match o with
    | :? Byte as b -> int (b)
    | :? Char as c -> int (c)
    | :? Decimal as d -> int (d)
    | :? Int16 as i -> int (i)
    | :? SByte as s -> int (s)
    | :? Single as s -> int (s)
    | :? UInt16 as u -> int (u)
    | :? UInt32 as u -> int (u)
    | :? UInt64 as u -> int (u)
    | :? Int32 as i -> int (i)
    | :? Int64 as i -> int (i)
    | :? Double as d -> int (d)
    | _ -> Convert.ToInt32(o, CultureInfo.InvariantCulture)

let convertToIntCastingNice (o: obj) : int =
    match o with
    | :? Int32 as i -> int (i)
    | :? Int64 as i -> int (i)
    | :? Double as d -> int (d)
    | :? Byte as b -> int (b)
    | :? Char as c -> int (c)
    | :? Decimal as d -> int (d)
    | :? Int16 as i -> int (i)
    | :? SByte as s -> int (s)
    | :? Single as s -> int (s)
    | :? UInt16 as u -> int (u)
    | :? UInt32 as u -> int (u)
    | :? UInt64 as u -> int (u)
    | _ -> Convert.ToInt32(o, CultureInfo.InvariantCulture)


let convertToIntDirectly (o: obj) : int =
    Convert.ToInt32(o, CultureInfo.InvariantCulture)


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
