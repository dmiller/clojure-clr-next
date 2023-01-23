module Clojure.Numerics.Converters

open System
open System.Globalization

// I did some benchmarking on variations of these converters, comparing
//   (1) match on Type.GetTypeCode(o.GetType())
//   (2) match on type
//   (3) just call Convert.ToInt32 (or whatever)
// The (1) version was slowest, significantly.
// I did three variations of version (2) to test the effect of ordering of the match expressions.
//   (2a) clauses ordered by alphabetic name of type
//   (2b) clauses ordered to be as nasty to the test data as possible (put the types used at the bottom of the tests)
//   (2c) clauses ordered to be as nice to the test data as possible (opposite of (2b))
// I used (3) as the baseline.
//
// Restuls
//
//   |       Method |     Mean |     Error |    StdDev | Ratio | RatioSD |
//   |------------- |---------:|----------:|----------:|------:|--------:|
//   |     TypeCode | 1.795 ms | 0.0128 ms | 0.0107 ms |  1.42 |    0.02 |
//   | CastingAlpha | 1.133 ms | 0.0090 ms | 0.0084 ms |  0.90 |    0.01 |
//   | CastingNasty | 1.275 ms | 0.0128 ms | 0.0120 ms |  1.01 |    0.01 |
//   |  CastingNice | 1.075 ms | 0.0097 ms | 0.0090 ms |  0.85 |    0.01 |
//   |       Direct | 1.261 ms | 0.0160 ms | 0.0149 ms |  1.00 |    0.00 |
//
// Casting works best, so that is what you see below.
// One can try to guess what order will best, but that will vary depending on the workload of the program.
// Given the prevalence of long and double, I will put them at the top, followed by int.
// Leaving unsigned toward the bottom.
// Decimal also at the bottom.



let convertToInt (o: obj) : int =
    match o with
    | :? Int64 as i -> int (i)
    | :? Double as d -> int (d)
    | :? Int32 as i -> int (i)
    | :? Byte as b -> int (b)
    | :? Char as c -> int (c)
    | :? Int16 as i -> int (i)
    | :? SByte as s -> int (s)
    | :? Single as s -> int (s)
    | :? UInt16 as u -> int (u)
    | :? UInt32 as u -> int (u)
    | :? UInt64 as u -> int (u)
    | :? Decimal as d -> int (d)
    | _ -> Convert.ToInt32(o, CultureInfo.InvariantCulture)

let convertToUInt (o: obj) : uint =
    match o with
    | :? Int64 as i -> uint (i)
    | :? Double as d -> uint (d)
    | :? Int32 as i -> uint (i)
    | :? Byte as b -> uint (b)
    | :? Char as c -> uint (c)
    | :? Int16 as i -> uint (i)
    | :? SByte as s -> uint (s)
    | :? Single as s -> uint (s)
    | :? UInt16 as u -> uint (u)
    | :? UInt32 as u -> uint (u)
    | :? UInt64 as u -> uint (u)
    | :? Decimal as d -> uint (d)
    | _ -> Convert.ToUInt32(o, CultureInfo.InvariantCulture)

let convertToLong (o: obj) : int64 =
    match o with
    | :? Int64 as i -> int64 (i)
    | :? Double as d -> int64 (d)
    | :? Int32 as i -> int64 (i)
    | :? Byte as b -> int64 (b)
    | :? Char as c -> int64 (c)
    | :? Int16 as i -> int64 (i)
    | :? SByte as s -> int64 (s)
    | :? Single as s -> int64 (s)
    | :? UInt16 as u -> int64 (u)
    | :? UInt32 as u -> int64 (u)
    | :? UInt64 as u -> int64 (u)
    | :? Decimal as d -> int64 (d)
    | _ -> Convert.ToInt64(o, CultureInfo.InvariantCulture)

let convertToULong (o: obj) : uint64 =
    match o with
    | :? Int64 as i -> uint64 (i)
    | :? Double as d -> uint64 (d)
    | :? Int32 as i -> uint64 (i)
    | :? Byte as b -> uint64 (b)
    | :? Char as c -> uint64 (c)
    | :? Int16 as i -> uint64 (i)
    | :? SByte as s -> uint64 (s)
    | :? Single as s -> uint64 (s)
    | :? UInt16 as u -> uint64 (u)
    | :? UInt32 as u -> uint64 (u)
    | :? UInt64 as u -> uint64 (u)
    | :? Decimal as d -> uint64 (d)
    | _ -> Convert.ToUInt64(o, CultureInfo.InvariantCulture)

let convertToShort (o: obj) : int16 =
    match o with
    | :? Int64 as i -> int16 (i)
    | :? Double as d -> int16 (d)
    | :? Int32 as i -> int16 (i)
    | :? Byte as b -> int16 (b)
    | :? Char as c -> int16 (c)
    | :? Int16 as i -> int16 (i)
    | :? SByte as s -> int16 (s)
    | :? Single as s -> int16 (s)
    | :? UInt16 as u -> int16 (u)
    | :? UInt32 as u -> int16 (u)
    | :? UInt64 as u -> int16 (u)
    | :? Decimal as d -> int16 (d)
    | _ -> Convert.ToInt16(o, CultureInfo.InvariantCulture)

let convertToUShort (o: obj) : uint16 =
    match o with
    | :? Int64 as i -> uint16 (i)
    | :? Double as d -> uint16 (d)
    | :? Int32 as i -> uint16 (i)
    | :? Byte as b -> uint16 (b)
    | :? Char as c -> uint16 (c)
    | :? Int16 as i -> uint16 (i)
    | :? SByte as s -> uint16 (s)
    | :? Single as s -> uint16 (s)
    | :? UInt16 as u -> uint16 (u)
    | :? UInt32 as u -> uint16 (u)
    | :? UInt64 as u -> uint16 (u)
    | :? Decimal as d -> uint16 (d)
    | _ -> Convert.ToUInt16(o, CultureInfo.InvariantCulture)

let convertToSByte (o: obj) : sbyte =
    match o with
    | :? Int64 as i -> sbyte (i)
    | :? Double as d -> sbyte (d)
    | :? Int32 as i -> sbyte (i)
    | :? Byte as b -> sbyte (b)
    | :? Char as c -> sbyte (c)
    | :? Int16 as i -> sbyte (i)
    | :? SByte as s -> sbyte (s)
    | :? Single as s -> sbyte (s)
    | :? UInt16 as u -> sbyte (u)
    | :? UInt32 as u -> sbyte (u)
    | :? UInt64 as u -> sbyte (u)
    | :? Decimal as d -> sbyte (d)
    | _ -> Convert.ToSByte(o, CultureInfo.InvariantCulture)

let convertToByte (o: obj) : byte =
    match o with
    | :? Int64 as i -> byte (i)
    | :? Double as d -> byte (d)
    | :? Int32 as i -> byte (i)
    | :? Byte as b -> byte (b)
    | :? Char as c -> byte (c)
    | :? Int16 as i -> byte (i)
    | :? SByte as s -> byte (s)
    | :? Single as s -> byte (s)
    | :? UInt16 as u -> byte (u)
    | :? UInt32 as u -> byte (u)
    | :? UInt64 as u -> byte (u)
    | :? Decimal as d -> byte (d)
    | _ -> Convert.ToByte(o, CultureInfo.InvariantCulture)

let convertToFloat (o: obj) : float32 =
    match o with
    | :? Int64 as i -> float32 (i)
    | :? Double as d -> float32 (d)
    | :? Int32 as i -> float32 (i)
    | :? Byte as b -> float32 (b)
    | :? Char as c -> float32 (c)
    | :? Int16 as i -> float32 (i)
    | :? SByte as s -> float32 (s)
    | :? Single as s -> float32 (s)
    | :? UInt16 as u -> float32 (u)
    | :? UInt32 as u -> float32 (u)
    | :? UInt64 as u -> float32 (u)
    | :? Decimal as d -> float32 (d)
    | _ -> Convert.ToSingle(o, CultureInfo.InvariantCulture)

let convertToDouble (o: obj) : double =
    match o with
    | :? Int64 as i -> double (i)
    | :? Double as d -> double (d)
    | :? Int32 as i -> double (i)
    | :? Byte as b -> double (b)
    | :? Char as c -> double (c)
    | :? Int16 as i -> double (i)
    | :? SByte as s -> double (s)
    | :? Single as s -> double (s)
    | :? UInt16 as u -> double (u)
    | :? UInt32 as u -> double (u)
    | :? UInt64 as u -> double (u)
    | :? Decimal as d -> double (d)
    | _ -> Convert.ToDouble(o, CultureInfo.InvariantCulture)


let convertToDecimal (o: obj) : decimal =
    match o with
    | :? Int64 as i -> decimal (i)
    | :? Double as d -> decimal (d)
    | :? Int32 as i -> decimal (i)
    | :? Byte as b -> decimal (b)
    | :? Char as c -> decimal (c)
    | :? Int16 as i -> decimal (i)
    | :? SByte as s -> decimal (s)
    | :? Single as s -> decimal (s)
    | :? UInt16 as u -> decimal (u)
    | :? UInt32 as u -> decimal (u)
    | :? UInt64 as u -> decimal (u)
    | :? Decimal as d -> decimal (d)
    | _ -> Convert.ToDecimal(o, CultureInfo.InvariantCulture)

let convertToChar (o: obj) : char =
    match o with
    | :? Int64 as i -> char (i)
    | :? Double as d -> char (d)
    | :? Int32 as i -> char (i)
    | :? Byte as b -> char (b)
    | :? Char as c -> char (c)
    | :? Int16 as i -> char (i)
    | :? SByte as s -> char (s)
    | :? Single as s -> char (s)
    | :? UInt16 as u -> char (u)
    | :? UInt32 as u -> char (u)
    | :? UInt64 as u -> char (u)
    | :? Decimal as d -> char (d)
    | _ -> Convert.ToChar(o, CultureInfo.InvariantCulture)
