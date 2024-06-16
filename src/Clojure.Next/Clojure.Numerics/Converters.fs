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
// My first benchmark results indicated that CastingNice was fastest.
//
//   |       Method |     Mean |     Error |    StdDev | Ratio | RatioSD |
//   |------------- |---------:|----------:|----------:|------:|--------:|
//   |     TypeCode | 1.795 ms | 0.0128 ms | 0.0107 ms |  1.42 |    0.02 |
//   | CastingAlpha | 1.133 ms | 0.0090 ms | 0.0084 ms |  0.90 |    0.01 |
//   | CastingNasty | 1.275 ms | 0.0128 ms | 0.0120 ms |  1.01 |    0.01 |
//   |  CastingNice | 1.075 ms | 0.0097 ms | 0.0090 ms |  0.85 |    0.01 |
//   |       Direct | 1.261 ms | 0.0160 ms | 0.0149 ms |  1.00 |    0.00 |
//
// However, later, I simplified the tests and ran them again.
// I also separated the tests out by type so that slower types were not blurring the results.

// | Method       | inputType | Mean     | Error     | StdDev    | Ratio | RatioSD |
// |------------- |---------- |---------:|----------:|----------:|------:|--------:|
// | TypeCode     | I32       | 2.410 ns | 0.0696 ns | 0.0651 ns |  1.68 |    0.05 |
// | CastingAlpha | I32       | 1.798 ns | 0.0197 ns | 0.0175 ns |  1.26 |    0.02 |
// | CastingNasty | I32       | 2.131 ns | 0.0305 ns | 0.0285 ns |  1.49 |    0.03 |
// | CastingNice  | I32       | 1.585 ns | 0.0165 ns | 0.0138 ns |  1.11 |    0.01 |
// | Direct       | I32       | 1.431 ns | 0.0164 ns | 0.0137 ns |  1.00 |    0.00 |
// |              |           |          |           |           |       |         |
// | TypeCode     | I64       | 2.100 ns | 0.0234 ns | 0.0196 ns |  1.54 |    0.02 |
// | CastingAlpha | I64       | 1.817 ns | 0.0252 ns | 0.0236 ns |  1.33 |    0.02 |
// | CastingNasty | I64       | 2.345 ns | 0.0669 ns | 0.0687 ns |  1.72 |    0.06 |
// | CastingNice  | I64       | 1.656 ns | 0.0262 ns | 0.0245 ns |  1.22 |    0.02 |
// | Direct       | I64       | 1.364 ns | 0.0147 ns | 0.0130 ns |  1.00 |    0.00 |
// |              |           |          |           |           |       |         |
// | TypeCode     | Dbl       | 2.296 ns | 0.0319 ns | 0.0283 ns |  1.04 |    0.02 |
// | CastingAlpha | Dbl       | 1.779 ns | 0.0178 ns | 0.0167 ns |  0.80 |    0.02 |
// | CastingNasty | Dbl       | 2.541 ns | 0.0133 ns | 0.0124 ns |  1.15 |    0.02 |
// | CastingNice  | Dbl       | 1.822 ns | 0.0147 ns | 0.0131 ns |  0.83 |    0.02 |
// | Direct       | Dbl       | 2.212 ns | 0.0508 ns | 0.0475 ns |  1.00 |    0.00 |
// |              |           |          |           |           |       |         |
// | TypeCode     | Str       | 5.675 ns | 0.0651 ns | 0.0577 ns |  1.12 |    0.02 |
// | CastingAlpha | Str       | 7.011 ns | 0.0366 ns | 0.0325 ns |  1.38 |    0.02 |
// | CastingNasty | Str       | 7.028 ns | 0.0318 ns | 0.0248 ns |  1.38 |    0.02 |
// | CastingNice  | Str       | 7.105 ns | 0.0356 ns | 0.0316 ns |  1.40 |    0.02 |
// | Direct       | Str       | 5.064 ns | 0.0778 ns | 0.0727 ns |  1.00 |    0.00 |
// 
// Now it appears that direct conversion wins across the board.
// There were two changes in the benchmark code -- 
//    (a) separating by type
//    (b) making single calls versus multiple calls in a loop.
// I'm going to preserve the older converter code, but switch to the direct conversion in shipped code.
//
// We might be getting some wins by Direct being inlined.

let convertToInt (o: obj) : int = Convert.ToInt32(o, CultureInfo.InvariantCulture)
let convertToUInt (o: obj) : uint = Convert.ToUInt32(o, CultureInfo.InvariantCulture)
let convertToLong (o: obj) : int64 = Convert.ToInt64(o, CultureInfo.InvariantCulture)
let convertToULong (o: obj) : uint64 = Convert.ToUInt64(o, CultureInfo.InvariantCulture)
let convertToShort (o: obj) : int16 = Convert.ToInt16(o, CultureInfo.InvariantCulture)
let convertToUShort (o: obj) : uint16 = Convert.ToUInt16(o, CultureInfo.InvariantCulture)
let convertToSByte (o: obj) : sbyte = Convert.ToSByte(o, CultureInfo.InvariantCulture)
let convertToByte (o: obj) : byte = Convert.ToByte(o, CultureInfo.InvariantCulture)
let convertToFloat (o: obj) : float32 = Convert.ToSingle(o, CultureInfo.InvariantCulture)
let convertToDouble (o: obj) : double = Convert.ToDouble(o, CultureInfo.InvariantCulture)
let convertToDecimal (o: obj) : decimal = Convert.ToDecimal(o, CultureInfo.InvariantCulture)
let convertToChar (o: obj) : char =  Convert.ToChar(o, CultureInfo.InvariantCulture)


/////////////////////////////////////////////////////////////////////////////////////////
//
//  the old code
//
/////////////////////////////////////////////////////////////////////////////////////////

//let convertToInt (o: obj) : int =
//    match o with
//    | :? Int64 as i -> int (i)
//    | :? Double as d -> int (d)
//    | :? Int32 as i -> int (i)
//    | :? Byte as b -> int (b)
//    | :? Char as c -> int (c)
//    | :? Int16 as i -> int (i)
//    | :? SByte as s -> int (s)
//    | :? Single as s -> int (s)
//    | :? UInt16 as u -> int (u)
//    | :? UInt32 as u -> int (u)
//    | :? UInt64 as u -> int (u)
//    | :? Decimal as d -> int (d)
//    | _ -> 

//let convertToUInt (o: obj) : uint =
//    match o with
//    | :? Int64 as i -> uint (i)
//    | :? Double as d -> uint (d)
//    | :? Int32 as i -> uint (i)
//    | :? Byte as b -> uint (b)
//    | :? Char as c -> uint (c)
//    | :? Int16 as i -> uint (i)
//    | :? SByte as s -> uint (s)
//    | :? Single as s -> uint (s)
//    | :? UInt16 as u -> uint (u)
//    | :? UInt32 as u -> uint (u)
//    | :? UInt64 as u -> uint (u)
//    | :? Decimal as d -> uint (d)
//    | _ -> Convert.ToUInt32(o, CultureInfo.InvariantCulture)

//let convertToLong (o: obj) : int64 =
//    match o with
//    | :? Int64 as i -> int64 (i)
//    | :? Double as d -> int64 (d)
//    | :? Int32 as i -> int64 (i)
//    | :? Byte as b -> int64 (b)
//    | :? Char as c -> int64 (c)
//    | :? Int16 as i -> int64 (i)
//    | :? SByte as s -> int64 (s)
//    | :? Single as s -> int64 (s)
//    | :? UInt16 as u -> int64 (u)
//    | :? UInt32 as u -> int64 (u)
//    | :? UInt64 as u -> int64 (u)
//    | :? Decimal as d -> int64 (d)
//    | _ -> Convert.ToInt64(o, CultureInfo.InvariantCulture)

//let convertToULong (o: obj) : uint64 =
//    match o with
//    | :? Int64 as i -> uint64 (i)
//    | :? Double as d -> uint64 (d)
//    | :? Int32 as i -> uint64 (i)
//    | :? Byte as b -> uint64 (b)
//    | :? Char as c -> uint64 (c)
//    | :? Int16 as i -> uint64 (i)
//    | :? SByte as s -> uint64 (s)
//    | :? Single as s -> uint64 (s)
//    | :? UInt16 as u -> uint64 (u)
//    | :? UInt32 as u -> uint64 (u)
//    | :? UInt64 as u -> uint64 (u)
//    | :? Decimal as d -> uint64 (d)
//    | _ -> Convert.ToUInt64(o, CultureInfo.InvariantCulture)

//let convertToShort (o: obj) : int16 =
//    match o with
//    | :? Int64 as i -> int16 (i)
//    | :? Double as d -> int16 (d)
//    | :? Int32 as i -> int16 (i)
//    | :? Byte as b -> int16 (b)
//    | :? Char as c -> int16 (c)
//    | :? Int16 as i -> int16 (i)
//    | :? SByte as s -> int16 (s)
//    | :? Single as s -> int16 (s)
//    | :? UInt16 as u -> int16 (u)
//    | :? UInt32 as u -> int16 (u)
//    | :? UInt64 as u -> int16 (u)
//    | :? Decimal as d -> int16 (d)
//    | _ -> Convert.ToInt16(o, CultureInfo.InvariantCulture)

//let convertToUShort (o: obj) : uint16 =
//    match o with
//    | :? Int64 as i -> uint16 (i)
//    | :? Double as d -> uint16 (d)
//    | :? Int32 as i -> uint16 (i)
//    | :? Byte as b -> uint16 (b)
//    | :? Char as c -> uint16 (c)
//    | :? Int16 as i -> uint16 (i)
//    | :? SByte as s -> uint16 (s)
//    | :? Single as s -> uint16 (s)
//    | :? UInt16 as u -> uint16 (u)
//    | :? UInt32 as u -> uint16 (u)
//    | :? UInt64 as u -> uint16 (u)
//    | :? Decimal as d -> uint16 (d)
//    | _ -> Convert.ToUInt16(o, CultureInfo.InvariantCulture)

//let convertToSByte (o: obj) : sbyte =
//    match o with
//    | :? Int64 as i -> sbyte (i)
//    | :? Double as d -> sbyte (d)
//    | :? Int32 as i -> sbyte (i)
//    | :? Byte as b -> sbyte (b)
//    | :? Char as c -> sbyte (c)
//    | :? Int16 as i -> sbyte (i)
//    | :? SByte as s -> sbyte (s)
//    | :? Single as s -> sbyte (s)
//    | :? UInt16 as u -> sbyte (u)
//    | :? UInt32 as u -> sbyte (u)
//    | :? UInt64 as u -> sbyte (u)
//    | :? Decimal as d -> sbyte (d)
//    | _ -> Convert.ToSByte(o, CultureInfo.InvariantCulture)

//let convertToByte (o: obj) : byte =
//    match o with
//    | :? Int64 as i -> byte (i)
//    | :? Double as d -> byte (d)
//    | :? Int32 as i -> byte (i)
//    | :? Byte as b -> byte (b)
//    | :? Char as c -> byte (c)
//    | :? Int16 as i -> byte (i)
//    | :? SByte as s -> byte (s)
//    | :? Single as s -> byte (s)
//    | :? UInt16 as u -> byte (u)
//    | :? UInt32 as u -> byte (u)
//    | :? UInt64 as u -> byte (u)
//    | :? Decimal as d -> byte (d)
//    | _ -> Convert.ToByte(o, CultureInfo.InvariantCulture)

//let convertToFloat (o: obj) : float32 =
//    match o with
//    | :? Int64 as i -> float32 (i)
//    | :? Double as d -> float32 (d)
//    | :? Int32 as i -> float32 (i)
//    | :? Byte as b -> float32 (b)
//    | :? Char as c -> float32 (c)
//    | :? Int16 as i -> float32 (i)
//    | :? SByte as s -> float32 (s)
//    | :? Single as s -> float32 (s)
//    | :? UInt16 as u -> float32 (u)
//    | :? UInt32 as u -> float32 (u)
//    | :? UInt64 as u -> float32 (u)
//    | :? Decimal as d -> float32 (d)
//    | _ -> Convert.ToSingle(o, CultureInfo.InvariantCulture)

//let convertToDouble (o: obj) : double =
//    match o with
//    | :? Int64 as i -> double (i)
//    | :? Double as d -> double (d)
//    | :? Int32 as i -> double (i)
//    | :? Byte as b -> double (b)
//    | :? Char as c -> double (c)
//    | :? Int16 as i -> double (i)
//    | :? SByte as s -> double (s)
//    | :? Single as s -> double (s)
//    | :? UInt16 as u -> double (u)
//    | :? UInt32 as u -> double (u)
//    | :? UInt64 as u -> double (u)
//    | :? Decimal as d -> double (d)
//    | _ -> Convert.ToDouble(o, CultureInfo.InvariantCulture)


//let convertToDecimal (o: obj) : decimal =
//    match o with
//    | :? Int64 as i -> decimal (i)
//    | :? Double as d -> decimal (d)
//    | :? Int32 as i -> decimal (i)
//    | :? Byte as b -> decimal (b)
//    | :? Char as c -> decimal (c)
//    | :? Int16 as i -> decimal (i)
//    | :? SByte as s -> decimal (s)
//    | :? Single as s -> decimal (s)
//    | :? UInt16 as u -> decimal (u)
//    | :? UInt32 as u -> decimal (u)
//    | :? UInt64 as u -> decimal (u)
//    | :? Decimal as d -> decimal (d)
//    | _ -> Convert.ToDecimal(o, CultureInfo.InvariantCulture)

//let convertToChar (o: obj) : char =
//    match o with
//    | :? Int64 as i -> char (i)
//    | :? Double as d -> char (d)
//    | :? Int32 as i -> char (i)
//    | :? Byte as b -> char (b)
//    | :? Char as c -> char (c)
//    | :? Int16 as i -> char (i)
//    | :? SByte as s -> char (s)
//    | :? Single as s -> char (s)
//    | :? UInt16 as u -> char (u)
//    | :? UInt32 as u -> char (u)
//    | :? UInt64 as u -> char (u)
//    | :? Decimal as d -> char (d)
//    | _ -> Convert.ToChar(o, CultureInfo.InvariantCulture)
