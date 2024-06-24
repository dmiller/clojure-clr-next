---
layout: post
title: A mega-dose of micro-benchmarks, Part 2 -- by the numbers   
date: 2024-06-18 00:00:00 -0500
categories: general
---

Improving the numerics code for type mapping, operations lookup, and conversions.

Refer to the preceding post, [A mega-dose of micro-benchmarks, Part 1 -- Setting the stage]({{site.baseurl}}{% post_url 2024-06-18-mega-dose-of-micro-benchmarks-part-1 %}), for the context of the code we are looking at here.

Any performance improvements in the code handling numeric operations is going to affect more than just `PersistentArrayMap.createWithCheck`, so it's worth taking a look.

##  Who's got my number?

Let's start with `IsNumeric`:

```F#
    static member IsNumeric(o: obj) =
        match o with
        | null -> false
        | _ -> Numbers.IsNumericType(o.GetType())

      
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

```

What is the most efficient way to test for the primitive types?  The use of `match` on `TypeCode` here was taken from the Microsoft Dynamic Language Runtime code. (In C#, that's a `switch` statement, of course.)  However, I have also seen code that worked with explicit type checking.  One has to back that up to `IsNumeric`:

```F#
  static member private IsNumeric(o: obj) =
        match o with
        | null -> false
        | :> SByte
        | :> Byte
        ...
```

This change seemed to make no real difference here, so I left it alone.

Next, I looked at

```F#
  match t with
            | x when x = typeof<BigInt> -> true
            ...
```

The `=` here translates to (decompiling to C#):

```C#
LanguagePrimitives.HashCompare.GenericEqualityIntrinsic(t, typeof(BigInt))
```

which is very slow.  We could spend a lot of time talking about how `=` compiles in F#, but it would take us too far afield.  There are circumstances under which that call gets optimized to something much faster, but not here.  (I think is because `Type` is not sealed.)  In the C# code, we get this IL:

```
IL_0010: ldarg.0
IL_0011: ldtoken [System.Runtime.Numerics]System.Numerics.BigInteger
IL_0016: call class [System.Runtime]System.Type [System.Runtime]System.Type::GetTypeFromHandle(valuetype [System.Runtime]System.RuntimeTypeHandle)
IL_001b: call bool [System.Runtime]System.Type::op_Equality(class [System.Runtime]System.Type, class [System.Runtime]System.Type)
IL_0020: ret
```
 In other words a call to `System.Type.op_Equality`.

And it makes a big difference:

| Method                        | size    | Mean      | Ratio | Testing                         |
|------------------------------ |-------- |----------:|------:|---------------------------------|
| CSharpEquals                  | 1000    |  2.370 ms |  1.00 | `t1 == t2`                      |
| FSharpEquals                  | 1000    | 21.019 ms |  8.87 | `t1 = t2`                       |
| FSharpEquals2                 | 1000    |  6.368 ms |  2.69 | `t1.Equals(t2)`                 |
| FSharpEqualsOp                | 1000    |  2.324 ms |  0.98 | `Type.op_Equality(t1,t2)`       |
| FSharpRefEquals               | 1000    |  2.614 ms |  1.10 | `Object.ReferenceEquals(t1,t2)` |


So we get a boost by moving from `=` to hand-coding `Type.op_Equality`.

We end up with


```F#
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
        | _ -> System.Type.op_Equality(t, typeof<Clojure.Numerics.BigInt>) 
            || System.Type.op_Equality(t, typeof<System.Numerics.BigInteger>)
            || System.Type.op_Equality(t, typeof<Clojure.BigArith.BigDecimal>)
            || System.Type.op_Equality(t, typeof<Clojure.Numerics.Ratio>)
```

## I'm a convert

Let's dig into `Numbers.equal`:

```F#
    static member equal(x: obj, y: obj) =
        OpsSelector.ops (x) = OpsSelector.ops (y) && Numbers.getOps(x, y).equiv (x, y)
```

The first place I looked was at the `.equiv` call at the end.
I was using integers for keys, so we were comparing integers to integers.  I know in this case that the `.getOps` call will retrieve an instance of the `LongOps` class.  Here is the definition of `LongOps.equiv`:

```F#
        member this.equiv(x: obj, y: obj) : bool = convertToLong (x) = convertToLong (y)
```

`convertToLong` is in the `Converters` module:

```F#
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
```

Here, we use matching by `:?` which translates to a series of checks using the `isinst` opcode.
The speed will be dependent on the order in which the types are tested, so I benchmarked different orders against different input types.  One could also match on `TypeCode` as we did in `IsNumericType`.  Finally, one could just call `Convert.ToInt64` and not try to be clever.  

 I did runs with single calls.  I did runs with 100,000 calls.  I used different data types for input.
 Here is a sample run:

| Method       | inputType | Mean     | StdDev    | Ratio | RatioSD |
|------------- |---------- |---------:|----------:|------:|--------:|
| TypeCode     | I32       | 2.410 ns | 0.0651 ns |  1.68 |    0.05 |
| CastingAlpha | I32       | 1.798 ns | 0.0175 ns |  1.26 |    0.02 |
| CastingNasty | I32       | 2.131 ns | 0.0285 ns |  1.49 |    0.03 |
| CastingNice  | I32       | 1.585 ns | 0.0138 ns |  1.11 |    0.01 |
| Direct       | I32       | 1.431 ns | 0.0137 ns |  1.00 |    0.00 |
|              |           |          |           |       |         |
| TypeCode     | I64       | 2.100 ns | 0.0196 ns |  1.54 |    0.02 |
| CastingAlpha | I64       | 1.817 ns | 0.0236 ns |  1.33 |    0.02 |
| CastingNasty | I64       | 2.345 ns | 0.0687 ns |  1.72 |    0.06 |
| CastingNice  | I64       | 1.656 ns | 0.0245 ns |  1.22 |    0.02 |
| Direct       | I64       | 1.364 ns | 0.0130 ns |  1.00 |    0.00 |
|              |           |          |           |       |         |
| TypeCode     | Dbl       | 2.296 ns | 0.0283 ns |  1.04 |    0.02 |
| CastingAlpha | Dbl       | 1.779 ns | 0.0167 ns |  0.80 |    0.02 |
| CastingNasty | Dbl       | 2.541 ns | 0.0124 ns |  1.15 |    0.02 |
| CastingNice  | Dbl       | 1.822 ns | 0.0131 ns |  0.83 |    0.02 |
| Direct       | Dbl       | 2.212 ns | 0.0475 ns |  1.00 |    0.00 |
|              |           |          |           |       |         |
| TypeCode     | Str       | 5.675 ns | 0.0577 ns |  1.12 |    0.02 |
| CastingAlpha | Str       | 7.011 ns | 0.0325 ns |  1.38 |    0.02 |
| CastingNasty | Str       | 7.028 ns | 0.0248 ns |  1.38 |    0.02 |
| CastingNice  | Str       | 7.105 ns | 0.0316 ns |  1.40 |    0.02 |
| Direct       | Str       | 5.064 ns | 0.0727 ns |  1.00 |    0.00 |

The differences between the Castingxxx tests are the ordering of the match cases.

The only place where `Direct` is beat is several of the `Double` cases.
Apparently the method dispatch in the `Convert.ToInt64`is faster in general than grabbing the type code and switching or doing sequential type testings.  

I contemplated special casing `Double` before calling `Convert.ToInt64` but I figured I could live with a 0.39 nanosecond hit on doubles.

So `convertToLong` gets simplified to:

```F#
let convertToLong (o: obj) : int64 = Convert.ToInt64(o, CultureInfo.InvariantCulture)
```

With the additional bonus of being inline-able, saving a call!  (Which may be where the savings actually occurs.)


## Solving a problem with dispatch

Continuing with

```F#
    static member equal(x: obj, y: obj) =
        OpsSelector.ops (x) = OpsSelector.ops (y) && Numbers.getOps(x, y).equiv (x, y)
```

Can we improve the categorization of the types or the table lookups?

```F#
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
```

It turns out that moving from `TypeCode` to `:?` (`isinst` in IL) is a small win.   The results are order-dependent, so I may undo this change:

```F#
    let ops (x: obj) : OpsType =
        match x with
        | :? Int64 -> OpsType.Long
        | :? Double -> OpsType.Double

        | :? Int32 -> OpsType.Long
        | :? Single -> OpsType.Double

        | :? BigInt
        | :? BigInteger -> OpsType.BigInteger

        | :? SByte
        | :? Int16  -> OpsType.Long

        | :? Ratio -> OpsType.Ratio

        | :? Byte
        | :? UInt16
        | :? UInt32
        | :? UInt64 -> OpsType.ULong

        | :? BigDecimal -> OpsType.BigDecimal

        | :? Decimal -> OpsType.ClrDecimal

        | _ -> OpsType.Long
```

As for `getOps`, I started with:

```F#
    static member getOps(x: obj, y: obj) =
        Numbers.opsImplTable[int (OpsSelector.combine (OpsSelector.ops (x), OpsSelector.ops (y)))]
```

`OpsSelector.combine` is just a lookup in a 2D array -- hard to improve on that.
And we use that result to do another array lookup -- hard to improve on that.

_Au contraire_!

`Numbers.opsImplTable` was defined as a `static member private` in the `Numbers` class.
F# puts in a bunch of sequencing checks in the code to initialize static fields.
I've read suggestions that tiered compilation would eventually get rid of the checks, but my tests did not show that.

Here is what the code looks like for a static member reference (decompiled from the IL  into C#):

```C#
        public static int StaticMember
        {
            [CompilerGenerated]
            [DebuggerNonUserCode]
            get
            {
                if (init@5 < 1)
                {
                    LanguagePrimitives.IntrinsicFunctions.FailStaticInit();
                }
                return StaticMember@;
            }
        }
```

I ran a benchmark comparing various kinds of lookups of a value:

The things being compared are:

- NoLookup:  just a loop that does no lookup, constant is embedded in the loop
- LiteralLookup:  a loop that uses a marked `[<literal>]` value
- StaticValLookup:  a loop that looks up a static val member of a class
- NonstaticValLookup:  a loop that looks up a non-static val member of class
- GetLetLookup:  a loop that looks up a let member of a class

# The results

| Method             | size | Mean       | Error    | StdDev   | Ratio | RatioSD |
|------------------- |----- |-----------:|---------:|---------:|------:|--------:|
| NoLookup           | 1000 |   256.7 ns |  1.01 ns |  0.90 ns |  1.00 |    0.00 |
| LiteralLookup      | 1000 |   261.6 ns |  2.75 ns |  2.29 ns |  1.02 |    0.01 |
| StaticValLookup    | 1000 |   311.1 ns |  6.21 ns |  5.81 ns |  1.21 |    0.02 |
| NonstaticValLookup | 1000 |   269.6 ns |  3.94 ns |  3.69 ns |  1.05 |    0.02 |
| GetLetLookup       | 1000 |   262.5 ns |  5.15 ns |  7.55 ns |  1.04 |    0.03 |
|                    |      |            |          |          |       |         |
| NoLookup           | 5000 | 1,273.7 ns | 12.81 ns | 11.98 ns |  1.00 |    0.00 |
| LiteralLookup      | 5000 | 1,267.9 ns | 12.64 ns | 11.82 ns |  1.00 |    0.01 |
| StaticValLookup    | 5000 | 1,508.1 ns | 19.07 ns | 17.84 ns |  1.18 |    0.02 |
| NonstaticValLookup | 5000 | 1,263.5 ns | 14.06 ns | 11.74 ns |  0.99 |    0.01 |
| GetLetLookup       | 5000 | 1,311.2 ns | 23.93 ns | 22.39 ns |  1.03 |    0.02 |

We take a hit on the static val due just to the initialization checks.  Now, we are down in the sub-nanosecond range here, but it did have an effect on the performance of `PersistentArrayMap.createWithCheck`.

I struggled to find a way to get rid of the checks.  I moved code into modules, but I had to make the modules recursive with `Numbers`, `LongOps` and friends because of inherent circularity that I could not figure out how to decouple.

Eventually, I did move the `getOps` code into its own separate module.  However, I have to fill one of its tables with instances of `LongOps` and friends, so I added an initialization function that now must be called prior to working with `Numbers`.   When the dust settles, I may go back and see if there is another way to do this, or if I have reduced coupling enough for the remaining static initialization checks to have minimal impact.

I found some other minor tweaks. The final code is:

```F#

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

   // leaving out the rest of the table

    let ops (x: obj) : OpsType =
        match x with
        | :? Int64 -> OpsType.Long
        | :? Double -> OpsType.Double

        | :? Int32 -> OpsType.Long
        | :? Single -> OpsType.Double

        | :? BigInt
        | :? BigInteger -> OpsType.BigInteger

        | :? SByte
        | :? Int16  -> OpsType.Long

        | :? Ratio -> OpsType.Ratio

        | :? Byte
        | :? UInt16
        | :? UInt32
        | :? UInt64 -> OpsType.ULong

        | :? BigDecimal -> OpsType.BigDecimal

        | :? Decimal -> OpsType.ClrDecimal

        | _ -> OpsType.Long

    let combine (t1: OpsType, t2: OpsType) = selectorTable[int (t1), int (t2)]

    let opsImplTable: Ops array = Array.zeroCreate 7

    let getOps(x: obj) = opsImplTable[ops (x) |> int]
    let getOps2(x: obj, y: obj) = opsImplTable[combine( ops (x), ops (y)) |> int]

    let getOpsFromType(t: OpsType) = opsImplTable[int t]
    let getOpsFromType2(t1: OpsType, t2: OpsType) = opsImplTable[combine(t1, t2) |> int]

    let getBigIntOps = opsImplTable[int OpsType.BigInteger]
    let getBigDecOps = opsImplTable[int OpsType.BigDecimal]

module Initializer =
    let init() =
        OpsSelector.opsImplTable[OpsType.Long |> int] <- LongOps()
        OpsSelector.opsImplTable[OpsType.ULong |> int] <- ULongOps()
        OpsSelector.opsImplTable[OpsType.Double |> int] <- DoubleOps()
        OpsSelector.opsImplTable[OpsType.Ratio |> int] <- RatioOps()
        OpsSelector.opsImplTable[OpsType.BigInteger |> int] <- BigIntOps()
        OpsSelector.opsImplTable[OpsType.BigDecimal |> int] <- BigDecimalOps()
        OpsSelector.opsImplTable[OpsType.ClrDecimal |> int] <- ClrDecimalOps()    
```

>  There must have been a problem in my original benchmark of `ops` and my conclusions on the impact of static initializations.  I have since redone that work
and provided  a [corrigendum]]({{site.baseurl}}{% post_url 2024-06-24-corrigendum-static-initialization %}).



Does it matter?  I did a mockup of the C# code -- I couldn't access the code from the C# assembly because the things I needed to test were private.  The C# equivalant of the `ops` call is called `category`.  Before making any changes:

| Method        | inputType | Mean     | Ratio |
|-------------- |---------- |---------:|------:|
| FirstCategory | I32       | 1.187 ns |  1.00 |
| NextOps       | I32       | 6.275 ns |  5.29 |
|               |           |          |       |
| FirstCategory | I64       | 1.181 ns |  1.00 |
| NextOps       | I64       | 4.527 ns |  3.83 |
|               |           |          |       |
| FirstCategory | Dbl       | 1.408 ns |  1.00 |
| NextOps       | Dbl       | 4.724 ns |  3.36 |
|               |           |          |       |
| FirstCategory | U64       | 1.673 ns |  1.00 |
| NextOps       | U64       | 4.568 ns |  2.72 |

The static initializer overhead was costing us 2-5X!

With the changes to the type lookup and the static intialization (and one other little change to avoid an array lookup, better matching the C#), I ended up with:

| Method        | inputType | Mean      | Ratio |
|-------------- |---------- |----------:|------:|
| FirstCategory | I32       | 1.2089 ns |  1.00 |
| NextOps       | I32       | 2.1974 ns |  1.82 |
|               |           |           |       |
| FirstCategory | I64       | 1.1973 ns |  1.00 |
| NextOps       | I64       | 0.5491 ns |  0.46 |
|               |           |           |       |
| FirstCategory | Dbl       | 1.4162 ns |  1.00 |
| NextOps       | Dbl       | 0.7587 ns |  0.54 |
|               |           |           |       |
| FirstCategory | U64       | 1.6379 ns |  1.00 |
| NextOps       | U64       | 3.4541 ns |  2.11 |


I'm not surprised U64 takes a hit -- it is at the bottom of sequence of type tests.
Not sure about I32.  


I made one last little change to `Numbers.equal`, going from

```F#
static member equal(x: obj, y: obj) =
   OpsSelector.ops (x) = OpsSelector.ops (y) && OpsSelector.getOps(x, y).equiv (x, y)
```
to

```F#
static member equal(x: obj, y: obj) =
    let xOpType = OpsSelector.ops (x)
    let yOpType = OpsSelector.ops (y)
    xOpType =  yOpType && OpsSelector.getOpsByType(xOpType).equiv (x, y)
```

This avoids doing the type-to-category lookup twice.  And we end up with a win over the C# code:

| Method                 | xInputType | yInputType | Mean      | Ratio |
|----------------------- |----------- |----------- |----------:|------:|
| FirstNumberEqual | I32        | I32        | 14.512 ns |  1.00 |
| NextNumberEqual  | I32        | I32        |  6.848 ns |  0.47 |
|                  |            |            |           |       |
| FirstNumberEqual | I32        | I64        | 14.491 ns |  1.00 |
| NextNumberEqual  | I32        | I64        |  9.183 ns |  0.63 |
|                  |            |            |           |       |
| FirstNumberEqual | I32        | Dbl        |  2.674 ns |  1.00 |
| NextNumberEqual  | I32        | Dbl        |  4.111 ns |  1.54 |
|                  |            |            |           |       |
| FirstNumberEqual | I64        | I32        | 13.888 ns |  1.00 |
| NextNumberEqual  | I64        | I32        |  9.250 ns |  0.67 |
|                  |            |            |           |       |
| FirstNumberEqual | I64        | I64        | 13.692 ns |  1.00 |
| NextNumberEqual  | I64        | I64        |  5.333 ns |  0.39 |
|                  |            |            |           |       |
| FirstNumberEqual | I64        | Dbl        |  2.790 ns |  1.00 |
| NextNumberEqual  | I64        | Dbl        |  2.797 ns |  1.00 |
|                  |            |            |           |       |
| FirstNumberEqual | Dbl        | I32        |  2.941 ns |  1.00 |
| NextNumberEqual  | Dbl        | I32        |  4.053 ns |  1.38 |
|                  |            |            |           |       |
| FirstNumberEqual | Dbl        | I64        |  2.711 ns |  1.00 |
| NextNumberEqual  | Dbl        | I64        |  2.760 ns |  1.02 |
|                  |            |            |           |       |
| FirstNumberEqual | Dbl        | Dbl        | 12.366 ns |  1.00 |
| NextNumberEqual  | Dbl        | Dbl        |  5.930 ns |  0.48 |

At least if don't compare `Int32`s and `Double`s too often. 

And that's it for the numbers.

In the [next post]({{site.baseurl}}{% post_url 2024-06-18-mega-dose-of-micro-benchmarks-part-3 %}), we look at the topmost level of the code, `PersistentArrayMap.createWithCheck`.  We'll see if we can get a win there.

