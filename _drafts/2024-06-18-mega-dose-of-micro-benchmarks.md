---
layout: post
title: A mega-dose of micro-benchmarks
date: 2024-06-18 10:36:00 -0500
categories: general
---

# A mega-dose of micro-benchmarks

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
What is not okay is that my first benchmark showed this running 20-30% slower than the C# ClojureCLR version.  Now, granted, times are down in the two-digit nanosecond range, but I was worried that things I was doing incorrectly in F# were going to bite me even more later on.  So I decided to investigate.

[I'm not going to cover my investigations in historical order. Too many blind alleys and weird turns.  I'll just try to make sense of it in the aftermath.] 

## It matters how you iterate

I looked at the IL generated from the code above, and found it untidy.
In fact, one version actually iterated down a range enumerator.  
I got some very misleading results in the benchmarks.

I compared various forms of iteration:  `for i = 0 to n`, `for i in 0 .. n`, and manual iteration with mutable index variable.  The results were ... interesting.   Under .Net 6.0:

| Method               | size    | Mean           | Error        | StdDev       | Ratio | RatioSD |
|--------------------- |-------- |---------------:|-------------:|-------------:|------:|--------:|
| InNoStep             | 1000    |       254.3 ns |      3.04 ns |      2.69 ns |  1.00 |    0.00 |
| ForToIteration       | 1000    |       255.4 ns |      2.30 ns |      2.04 ns |  1.00 |    0.01 |
| ManualIterationStep1 | 1000    |       255.8 ns |      2.44 ns |      2.29 ns |  1.01 |    0.01 |
| InStep1              | 1000    |       253.1 ns |      2.83 ns |      2.65 ns |  1.00 |    0.01 |
| ManualIterationStep2 | 1000    |       251.9 ns |      1.73 ns |      1.53 ns |  0.99 |    0.01 |
| InStep2              | 1000    |     3,737.7 ns |     37.92 ns |     35.47 ns | 14.71 |    0.20 |
|                      |         |                |              |              |       |         |
| InNoStep             | 1000000 |   249,367.3 ns |    941.15 ns |    734.79 ns |  1.00 |    0.00 |
| ForToIteration       | 1000000 |   246,770.3 ns |    935.36 ns |    781.07 ns |  0.99 |    0.00 |
| ManualIterationStep1 | 1000000 |   249,436.2 ns |  3,010.22 ns |  2,668.48 ns |  1.00 |    0.01 |
| InStep1              | 1000000 |   249,051.1 ns |  2,111.38 ns |  1,974.99 ns |  1.00 |    0.01 |
| ManualIterationStep2 | 1000000 |   249,295.9 ns |  2,038.12 ns |  1,906.46 ns |  1.00 |    0.01 |
| InStep2              | 1000000 | 3,184,726.6 ns | 21,995.74 ns | 18,367.44 ns | 12.77 |    0.09 |

Test `InStep2` is a `for i in 0 .. 2 .. size*2-1`  Those multiples are crazy.

Under .Net 8.0:


| Method               | size    | Mean           | Error        | StdDev      | Ratio | RatioSD |
|--------------------- |-------- |---------------:|-------------:|------------:|------:|--------:|
| InNoStep             | 1000    |       264.3 ns |      5.19 ns |     7.44 ns |  1.00 |    0.00 |
| ForToIteration       | 1000    |       253.6 ns |      1.74 ns |     1.62 ns |  0.95 |    0.03 |
| ManualIterationStep1 | 1000    |       254.5 ns |      3.11 ns |     2.76 ns |  0.95 |    0.03 |
| InStep1              | 1000    |       256.2 ns |      2.91 ns |     2.58 ns |  0.96 |    0.04 |
| ManualIterationStep2 | 1000    |       254.6 ns |      2.95 ns |     2.76 ns |  0.95 |    0.04 |
| InStep2              | 1000    |     1,121.8 ns |      5.44 ns |     4.83 ns |  4.20 |    0.15 |
|                      |         |                |              |             |       |         |
| InNoStep             | 1000000 |   246,060.1 ns |  1,137.05 ns | 1,063.60 ns |  1.00 |    0.00 |
| ForToIteration       | 1000000 |   249,332.2 ns |  1,831.00 ns | 1,712.72 ns |  1.01 |    0.01 |
| ManualIterationStep1 | 1000000 |   252,724.2 ns |  4,297.67 ns | 4,020.05 ns |  1.03 |    0.02 |
| InStep1              | 1000000 |   245,521.5 ns |  1,853.53 ns | 1,547.78 ns |  1.00 |    0.00 |
| ManualIterationStep2 | 1000000 |   246,447.9 ns |  2,110.00 ns | 1,761.95 ns |  1.00 |    0.01 |
| InStep2              | 1000000 | 1,073,703.8 ns | 10,416.10 ns | 9,233.60 ns |  4.36 |    0.05 |

Not as bad, but still bad.

The F# source is:

```F#

    member this.InStep2() = 
        let doubleSize = 2*this.size
        let mutable i : int =0
        for iter in 0 .. 2 .. doubleSize do
            i <- i + 17
        i
```

I looked at the debug IL using ILSpy (and decompiled back into C#):

```C#
public int InStep2()
{
	int doubleSize = 2 * size;
	int i = 0;
	IEnumerable<int> enumerable = Operators.OperatorIntrinsics.RangeInt32(0, 2, doubleSize);
	foreach (int iter in enumerable)
	{
		i += 17;
	}
	return i;
}
```

Pretty much the same under both, so they changed something in the enumerable, I guess.

The weird thing is, when I use [sharplab.io](https://sharplab.io), I get something completely different:

```C#
        public int InStep2()
        {
            int num = 2 * size@;
            int num2 = 0;
            ulong num3 = ((num >= 0) ? ((ulong)(int)((uint)(num - 0) / 2u) + 1uL) : 0);
            ulong num4 = 0uL;
            int num5 = 0;
            while (num4 < num3)
            {
                num2 += 17;
                num5 += 2;
                num4++;
            }
            return num2;
        }
```

This should run just fine.  What is going on? 

At any rate, I changed the iteration code to use mutable indexes and manual stepping and got a percentage point or two.

```F#
   static member createWithCheck(init: obj array) =
        let mutable i = 0;
        while i < init.Length do
            let mutable j = i + 2
            while j < init.Length do
                if PersistentArrayMap.equalKey (init[i], init[j]) then
                    raise <| ArgumentException($"Duplicate key: {init[i]}")
                j <- j + 2
            i <- i + 2

        PersistentArrayMap(init)
```

## Equality

THere is not much else `createWithCheck` other than therepeated calls to `PersistentArrayMap.equalKey`.  This should be defined as

```F#
    static member internal equalKey(k1: obj, k2: obj) =
        //PersistentArrayMap.keywordCheck (k1) && k1 = k2 || Util.equiv (k1, k2)
```
 where `keywordCheck` checks to see if its argument is a keyword.  We don't have keywords yet, so I reduced this to:

```F#
     static member internal equalKey(k1: obj, k2: obj) =
        Util.equiv (k1, k2)
```

where

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

What could go wrong?

In my benchmark data, not having keywords available, I used integers for keys.  Thus all calls to `equiv` were going through `Numbers.IsNumeric` and `Numbers.equal`.  Let's dig into the code for those and their callees:

Starting with `IsNumeric`:

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

There are several things to test here.  First, what is the most efficient way to test for the primitive types?  The use of `TypeCode` here was taken from the Microsoft Dynamic Language Runtime code.  However, I have also seen code that worked with explicit type checking:






```F#
    static member equal(x: obj, y: obj) =
        OpsSelector.ops (x) = OpsSelector.ops (y) && Numbers.getOps(x, y).equiv (x, y)

```
