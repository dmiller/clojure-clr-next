# Converter.Benchmark

We benchmark two mechanisms in the heart of the `Clojure.Numerics.Numbers` edifice.


- How to categorize a value.
- How to convert a value to a numeric type.

The `Numbers` package implements arithmetic, comparison, and related operations on numeric types.
Ideally, when doing numeric operations, we know the type(s) of the operand(s) and can generate IL code that is specialized for the type(s) of the operand(s).
`Numbers` comes into play when we don't know the type of the operand(s) at compile time.
Here is how we define an operation such as addition:

```F#
    static member add(x: obj, y: obj) = Numbers.getOps(x, y).add (x, y)
```

The `getOps` function returns an object that implements `Ops` interface.
It is essentially a double-dispatch -- looking at the types of `x` and `y` we categorize each (say as integer and floating point) and then use the rules of contagion to determine the the proper operations set to use.
For example, if `x` is an integer and `y` is a floating point number, we use the floating point operations set.

Here is how we define the `add` method in the `LongOps` implementation.

```F#
member this.add(x, y) : obj = Numbers.add (convertToLong (x), convertToLong (y))
```

Thus we have several computations that require the type of the operand:

- Categorize the operand. The types used in Clojure are:

```F#
type OpsType =
    | Long = 0
    | Double = 1
    | Ratio = 2
    | BigInteger = 3
    | BigDecimal = 4
    | ULong = 5
    | ClrDecimal = 6
```

- Given an operand type or two operand types, determine the proper operation set to use.

- Convert the operand to the proper type, such as in the call to `convertToLong` above.

This benchmark compares two ways of categorizing a value and multiple ways of converting a value to a numeric type.
The middle computation, determining the operation set to use, is not benchmarked here. We benchmark approaches to that problem in a separate project, DispatchBenchmark.


# Categorizing a value

We compare two ways of categorizing a value:

- matching on the type code of the value's type
- matching directly on the type

We used a smaller set of categories for this benchmark: `Floating`, `SignedInteger`, `UnsignedInteger`, and `Other`.

I do not understand the results. The type test version is faster than the type code version which is surprising. 

Here is the code:

```F#

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
```


Here are the results of two runs.  One did a single call, the other looped 100,000 times.  (Trying to swamp out some per-loop overhead.)



| Method   | inputType | Mean     | Error     | StdDev    | Ratio | RatioSD |
|--------- |---------- |---------:|----------:|----------:|------:|--------:|
| TypeCode | I32       | 2.380 ns | 0.0647 ns | 0.0605 ns |  1.00 |    0.00 |
| Type     | I32       | 1.578 ns | 0.0199 ns | 0.0186 ns |  0.66 |    0.02 |
|          |           |          |           |           |       |         |
| TypeCode | I64       | 2.282 ns | 0.0392 ns | 0.0367 ns |  1.00 |    0.00 |
| Type     | I64       | 1.567 ns | 0.0150 ns | 0.0140 ns |  0.69 |    0.02 |
|          |           |          |           |           |       |         |
| TypeCode | Dbl       | 2.252 ns | 0.0700 ns | 0.0688 ns |  1.00 |    0.00 |
| Type     | Dbl       | 1.394 ns | 0.0473 ns | 0.0419 ns |  0.62 |    0.02 |
|          |           |          |           |           |       |         |
| TypeCode | Str       | 2.613 ns | 0.0455 ns | 0.0426 ns |  1.00 |    0.00 |
| Type     | Str       | 2.223 ns | 0.0639 ns | 0.1014 ns |  0.84 |    0.04 |


| Method   | inputType | Mean     | Error   | StdDev   | Ratio | RatioSD |
|--------- |---------- |---------:|--------:|---------:|------:|--------:|
| TypeCode | I32       | 200.8 us | 3.75 us |  4.46 us |  1.00 |    0.00 |
| Type     | I32       | 131.8 us | 2.53 us |  2.92 us |  0.66 |    0.02 |
|          |           |          |         |          |       |         |
| TypeCode | I64       | 196.6 us | 3.28 us |  3.07 us |  1.00 |    0.00 |
| Type     | I64       | 151.3 us | 2.99 us |  3.20 us |  0.77 |    0.02 |
|          |           |          |         |          |       |         |
| TypeCode | Dbl       | 206.2 us | 4.05 us |  5.12 us |  1.00 |    0.00 |
| Type     | Dbl       | 110.6 us | 2.03 us |  2.91 us |  0.54 |    0.02 |
|          |           |          |         |          |       |         |
| TypeCode | Str       | 226.0 us | 2.54 us |  2.37 us |  1.00 |    0.00 |
| Type     | Str       | 292.6 us | 5.82 us | 10.49 us |  1.30 |    0.04 |



`categorizeByTypeCode` is compiled to a `switch` IL op.
`categorizeByType` tests the value for being of the stated type by testing against each comparison type _in sequence_.

Presumably `categorizeByTypeCode` has results that are not dependent on the type of the input value, except perhaps for the default case that has to drop below the switch statement.
And that is pretty clear.  Using the second run, for the type codes that are explicitly listed, the variation in runs is small:  200.8 vs 196.6 vs 206.2. I think that variation is small enough to be discounted.
However, for the string values, which fall through the switch, it is non-trivially slower : 226.0.  However, per individial call (the first set of results), the string case is not slower.

The results for `categorizeByType` are dependent on the ordering of the types in the match expression.  
`Double` is highest in the tests, with `Int32` and `Int64` coming next.  Strings would require testing against all types before bottoming out in the default case at the end.
And the results are ordered in this way  110 < 131 < 151 < 292.  We have the same ordering in the other result set.


What I really don't understand is how a switch on type code in general is slower than a sequence of type tests.  
I would have expected the opposite. 
But it is clear here that except for strings, type testing beats switching.  But it is order-dependent.  We can guess that certain data types are more likely than others and place them first.
It's a guessing game.  Given that I would expect types in the 'Other' category to be infrequent, the other cases are will come out faster.


## Converting a value

I'm equally surprised here.

I tested conversion to `Int64`.  The input values were `Int32`, `Int64`, `Double`, and `String`.
The methods tested were:

- TypeCode: Match on type code and cast accordingly.
- Casting:  Matching on type.  There variations here:
    - Alpha: The specific types tested were listed in alphabetical order.
    - Nasty: The specific types tested were listed with the most common types _last_.
    - Nice: The specific types tested were listed with the most common types _first_.
- Direct:  Direct cast to the target type.

Here is the code.  I show only `CastingNice` -- the othe 'Casting' types differ only in the order of the clauses.

```F#
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
```

And some results.  Again I did a run with single calls and a run with 100,000 calls.

| Method       | inputType | Mean     | Error     | StdDev    | Ratio | RatioSD |
|------------- |---------- |---------:|----------:|----------:|------:|--------:|
| TypeCode     | I32       | 2.410 ns | 0.0696 ns | 0.0651 ns |  1.68 |    0.05 |
| CastingAlpha | I32       | 1.798 ns | 0.0197 ns | 0.0175 ns |  1.26 |    0.02 |
| CastingNasty | I32       | 2.131 ns | 0.0305 ns | 0.0285 ns |  1.49 |    0.03 |
| CastingNice  | I32       | 1.585 ns | 0.0165 ns | 0.0138 ns |  1.11 |    0.01 |
| Direct       | I32       | 1.431 ns | 0.0164 ns | 0.0137 ns |  1.00 |    0.00 |
|              |           |          |           |           |       |         |
| TypeCode     | I64       | 2.100 ns | 0.0234 ns | 0.0196 ns |  1.54 |    0.02 |
| CastingAlpha | I64       | 1.817 ns | 0.0252 ns | 0.0236 ns |  1.33 |    0.02 |
| CastingNasty | I64       | 2.345 ns | 0.0669 ns | 0.0687 ns |  1.72 |    0.06 |
| CastingNice  | I64       | 1.656 ns | 0.0262 ns | 0.0245 ns |  1.22 |    0.02 |
| Direct       | I64       | 1.364 ns | 0.0147 ns | 0.0130 ns |  1.00 |    0.00 |
|              |           |          |           |           |       |         |
| TypeCode     | Dbl       | 2.296 ns | 0.0319 ns | 0.0283 ns |  1.04 |    0.02 |
| CastingAlpha | Dbl       | 1.779 ns | 0.0178 ns | 0.0167 ns |  0.80 |    0.02 |
| CastingNasty | Dbl       | 2.541 ns | 0.0133 ns | 0.0124 ns |  1.15 |    0.02 |
| CastingNice  | Dbl       | 1.822 ns | 0.0147 ns | 0.0131 ns |  0.83 |    0.02 |
| Direct       | Dbl       | 2.212 ns | 0.0508 ns | 0.0475 ns |  1.00 |    0.00 |
|              |           |          |           |           |       |         |
| TypeCode     | Str       | 5.675 ns | 0.0651 ns | 0.0577 ns |  1.12 |    0.02 |
| CastingAlpha | Str       | 7.011 ns | 0.0366 ns | 0.0325 ns |  1.38 |    0.02 |
| CastingNasty | Str       | 7.028 ns | 0.0318 ns | 0.0248 ns |  1.38 |    0.02 |
| CastingNice  | Str       | 7.105 ns | 0.0356 ns | 0.0316 ns |  1.40 |    0.02 |
| Direct       | Str       | 5.064 ns | 0.0778 ns | 0.0727 ns |  1.00 |    0.00 |



| Method       | inputType | Mean     | Error    | StdDev   | Ratio | RatioSD |
|------------- |---------- |---------:|---------:|---------:|------:|--------:|
| TypeCode     | I32       | 218.5 us |  1.67 us |  1.56 us |  2.06 |    0.02 |
| CastingAlpha | I32       | 176.0 us |  2.01 us |  1.88 us |  1.66 |    0.02 |
| CastingNasty | I32       | 254.7 us |  2.37 us |  2.10 us |  2.41 |    0.02 |
| CastingNice  | I32       | 148.0 us |  1.43 us |  1.19 us |  1.40 |    0.01 |
| Direct       | I32       | 105.8 us |  0.44 us |  0.41 us |  1.00 |    0.00 |
|              |           |          |          |          |       |         |
| TypeCode     | I64       | 215.7 us |  1.82 us |  1.70 us |  1.99 |    0.05 |
| CastingAlpha | I64       | 203.4 us |  1.76 us |  1.56 us |  1.88 |    0.05 |
| CastingNasty | I64       | 278.0 us |  2.49 us |  2.08 us |  2.56 |    0.05 |
| CastingNice  | I64       | 153.2 us |  2.63 us |  2.46 us |  1.42 |    0.04 |
| Direct       | I64       | 107.9 us |  2.08 us |  2.32 us |  1.00 |    0.00 |
|              |           |          |          |          |       |         |
| TypeCode     | Dbl       | 216.7 us |  1.33 us |  1.25 us |  1.14 |    0.01 |
| CastingAlpha | Dbl       | 187.5 us |  1.63 us |  1.52 us |  0.98 |    0.01 |
| CastingNasty | Dbl       | 320.6 us |  5.04 us |  4.95 us |  1.68 |    0.04 |
| CastingNice  | Dbl       | 171.1 us |  1.56 us |  1.30 us |  0.90 |    0.01 |
| Direct       | Dbl       | 190.8 us |  1.76 us |  1.56 us |  1.00 |    0.00 |
|              |           |          |          |          |       |         |
| TypeCode     | Str       | 612.5 us |  3.12 us |  2.92 us |  1.33 |    0.03 |
| CastingAlpha | Str       | 760.6 us |  6.17 us |  5.78 us |  1.65 |    0.03 |
| CastingNasty | Str       | 745.5 us |  3.92 us |  3.67 us |  1.62 |    0.04 |
| CastingNice  | Str       | 775.5 us | 15.49 us | 21.20 us |  1.70 |    0.07 |
| Direct       | Str       | 461.4 us |  9.14 us |  9.39 us |  1.00 |    0.00 |

The only place where `Direct` is beat is several of the `Double` cases.
Apparently the method dispatch is faster in general than grabbing the type code and switching or doing sequential type testings.
I think we can handle the 0.5 nanosecond difference.

