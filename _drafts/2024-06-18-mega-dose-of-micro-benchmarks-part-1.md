---
layout: post
title: A mega-dose of micro-benchmarks, Part 1 -- Setting the stage   
date: 2024-06-18 10:36:00 -0500
categories: general
---

I finally have time to work on ClojureCLR.Next again.  Thing had been busy.

One concern that I have had is that the performance of the new ClojureCLR might not be as good as the old one. Being relatively new to F#, I don't know all the little pitfalls that can cause performance problems.  Before this recent hiatus, I had been working on hash maps implementations and had been checking performance.  Some things looked okay, some not.  To get back in the groove of things, I decided to focus on something simple: creating a `PersistentArrayMap`.

You may want to review two previous posts:

- [This map is the territory]({{site.baseurl}}{% post_url 2023-01-11 %}) discusses the interfaces that a map must implement and presents a naive implementation of a map.
- [A road to maps]({{site.baseurl}}{% post_url 2023-02-03-a-road-to-maps %}) discuses the various classes that are involved in getting to implementations of `PersistentVector`, `PersistentHashMap`, and company.  (There are four articles here on `PersistentVector` -- we won't need them at this time.)


# `PersistentArrayMap`

`PersistentArrayMap` is a simple map that uses an array to store the keys and values.  The keys are stored in the even indexes and the values in the odd indexes.  For immutability, the array is never modified.  When we perform an operation such as deleting or adding an key/value pair, a new object holding a new array is createed.

`PersistentArrayMap` performs key lookup via linear search in the array.  This is not efficient.  This data structure is intended to be used only for small maps.  The idea is that linear search will be efficient up to certain size.  After that, we switch to a data structure (`PersistentHashMap`) that has larger memory footprint but faster access time.  (The threshold in Clojure(JVM) and ClojureCLR is set at 16 entries.)  When you add a key/value pair to a `PersistentArrayMap` and cause the size to move above the threshold, the operation will return a `PersistentHashMap`.  Thus, you need to develop both maps in order to deliver. 


The implementation of `PersistentArrayMap` is straightforward.  The data structure holds an array (and a map for metadata, if needed).
There are some little oddities in there.  For example, because Clojure keywords are commonly used as keys, there is some special case code to speed things up for that case.  But no need to go into details here.  What I'd like to focus on is just one thing: creating a `PersistentArrayMap` from an array of alternating key/value pairs.  Here is the code I originally wrote:




```F#
    static member createWithCheck(init: obj array) =
        for i in 0 .. 2 .. init.Length-1 do
            for j in i + 2 .. 2 .. init.Length - 1  do
                if PersistentArrayMap.equalKey (init[i], init[j]) then
                    raise <| ArgumentException($"Duplicate key: {init[i]}")
                    
        PersistentArrayMap(init)
```



Yes, it is an O(n^2) algorithm:  We check each key against the keys following it in the array.  This is okay.  Only for small `n`.

What is not okay is that my first benchmark showed this running 20-30% slower than the C# ClojureCLR version.  Now, granted, times on the C# range from 4ns to 1, 100 ns, but we createa _lot_ of maps.  I was worried that things I was doing incorrectly in F# were going to bite me even more later on.  So I decided to investigate.

Let's look at the code all the way down.

```F#
    static member internal equalKey(k1: obj, k2: obj) =
        PersistentArrayMap.keywordCheck (k1) && k1 = k2 || Util.equiv (k1, k2)
```

`keywordCheck` checks to see if its argument is a keyword.  We don't have keywords yet, so I reduced this to:

```F#
     static member internal equalKey(k1: obj, k2: obj) = Util.equiv (k1, k2)
```

Here is `Util.equiv`:

```F#
let equiv (k1: obj, k2: obj) =
    if Object.ReferenceEquals(k1, k2) then
        true
    elif isNull k1 then
        false
    elif Numbers.IsNumeric k1 && Numbers.IsNumeric k2 then
        Numbers.equal (k1, k2)
    elif k1 :? IPersistentCollection || k2 :? IPersistentCollection then
        pcequiv (k1, k2)
    else
        k1.Equals(k2)
```

Given that my tests used integer keys, we can skip looking at `pcquiv` -- the guard clause will have been false.  However, we certainly will be going into the numeric tests. `Numbers.IsNumeric` at least is reasonably self-contained:

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

`Numbers.equal` is not so self-contained:


```F#
    static member equal(x: obj, y: obj) =
        OpsSelector.ops (x) = OpsSelector.ops (y) && Numbers.getOps(x, y).equiv (x, y)

```

`OpsSelector.ops` is a simple function that returns the `Ops` object for a given object's type.


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


```F#
    static member getOps(x: obj, y: obj) =
        Numbers.opsImplTable[int (OpsSelector.combine (OpsSelector.ops (x), OpsSelector.ops (y)))]
```
`Number.getOps` does a table lookup in a 2D array (that's the call to `combine`), based on the `OpsType` of its arguments, then looks up a operation implementation object.
In our case, we comparing integers to integers, so we will end up with an instance of the `LongOps` class.  Here is the `equiv` method of `LongOps`:

```F#
        member this.equiv(x: obj, y: obj) : bool = convertToLong (x) = convertToLong (y)
```

Makes sense: we have two objects to be compared using integer equality.  Convert them to longs and compare!

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

And we've finally bottomed out.  All this code just to compare two (integer) keys for equality.

In the subsequent posts of this series, I'll dig into the individual benchmarks and code improvements.  I will not attempt to cover them in historical order -- I was all over the place tracking things down.

There are many pitfalls in doing micro-benchmarking.  I'm sure I'm aware of only a few.
Fortunately, I had a comparison point in the C# ClojureCLR code. I started at the top, worked my way through calls to isolate where there were performance differences, compare the generated IL code (and occasionally assembler code), and try to figure out an improvement in the F#.  Once I had a improvement showing in the microbenchmark, I would move back up the call tree and see if there was an improvement overall.

Onward!
