---
layout: post
title: Persistent vectors, Part 4 -- Performance
date: 2023-02-18 15:00:00 -0500
categories: general
---

This is the first data structure we've worked on where performance can reasonably be tested.  How does the new F# version of the code compare to the original C#?  Where can performance be improved?

I used BenchmarkDotNet to generate the comparisons below.  It makes benchmarking fun.

 I want to make sure I'm not making egregiously stupid errors in writing the new F# code, a concern given my lack of experience in the language.  I also wonder about the performance differences of certain constructs in F#.  

## Transiency

The first test compares creation of `PersistentVector`s via transiency.  That is probably the simplest code we can look at.  I compared to the ClojureCLR C# code (notated as _First_) against the ClojureCLR.Next F# code (notated as _Next_ for a variety of vector sizes.  Sample benchmark code:

```F#
[<Benchmark>]
member this.NextTransientConj() =
    let mutable pv =
        (Clojure.Collections.PersistentVector.EMPTY :> Clojure.Collections.IEditableCollection)
            .asTransient ()

    for i in 0 .. this.size do
        pv <- pv.conj (i)

    pv.persistent ()
```
I'll leave out some of the details from the BenchmarkDotNet output, but you'll get the idea.


|             Method |   size |           Mean |  Ratio |  Allocated | Alloc Ratio |
|------------------- |-------:|---------------:|-------:|-----------:|------------:|
| FirstTransientConj |    100 |       909.5 ns |   1.00 |    4.95 KB |        1.00 |
|  NextTransientConj |    100 |       951.3 ns |   1.05 |    5.36 KB |        1.08 |
| FirstTransientConj |   1000 |     8,502.6 ns |   1.00 |   43.14 KB |        1.00 |
|  NextTransientConj |   1000 |     8,191.6 ns |   0.96 |   43.55 KB |        1.01 |
| FirstTransientConj |  10000 |   110,798.0 ns |   1.00 |  510.69 KB |        1.00 |
|  NextTransientConj |  10000 |   109,310.5 ns |   0.99 |  511.09 KB |        1.00 |
| FirstTransientConj | 100000 | 2,524,123.1 ns |   1.00 | 5831.35 KB |        1.00 |
|  NextTransientConj | 100000 | 2,596,691.2 ns |   1.03 | 5831.75 KB |        1.00 |

I've seen a few percentage points swing between successive runs.  We can call this a dead heat.

While we're on transients, I did do a comparision of transient vs non-transient versions in the F# code only.  

|            Method |   size |            Mean |  Ratio |    Allocated | Alloc Ratio |
|------------------ |------- |----------------:|-------:|-------------:|------------:|
| NextTransientConj |     10 |        238.9 ns |   1.00 |      1.47 KB |        1.00 |
|          NextCons |     10 |        321.7 ns |   1.35 |      2.04 KB |        1.39 |
| NextTransientConj |    100 |      1,262.7 ns |   1.00 |      5.36 KB |        1.00 |
|          NextCons |    100 |      3,348.6 ns |   2.67 |     24.16 KB |        4.51 |
| NextTransientConj |   1000 |     11,410.0 ns |   1.00 |     43.55 KB |        1.00 |
|          NextCons |   1000 |     34,894.5 ns |   3.08 |    240.71 KB |        5.53 |
| NextTransientConj |  10000 |    143,966.6 ns |   1.00 |    511.09 KB |        1.00 |
|          NextCons |  10000 |    572,406.2 ns |   3.97 |   2494.45 KB |        4.88 |
| NextTransientConj | 100000 |  3,120,521.6 ns |   1.00 |   5831.75 KB |        1.00 |
|          NextCons | 100000 | 13,182,321.8 ns |   4.21 |  25679.83 KB |        4.40 |

No surprise that transients win. The benchmark on the [transients reference page](https://clojure.org/reference/transients) shows a similar ratio. (73.7 vs 19.7 = 3.74 ratio).

## Cons

The situation is not quite as good for `cons`, but not enough to make one overly concerned.
The benchmark is almost identical to the one for `conj`, just not using transients:

```F#
[<Benchmark>]
member this.NextCons() =
    let mutable pv =
        Clojure.Collections.PersistentVector.EMPTY :> Clojure.Collections.IPersistentVector

    for i in 0 .. this.size do
        pv <- pv.cons (i)

    pv
```


|    Method |   size |          Mean | Ratio |    Allocated | Alloc Ratio |
|---------- |------- |--------------:|------:|-------------:|------------:|
| FirstCons |    100 |      2.409 us |  1.00 |     23.76 KB |        1.00 |
|  NextCons |    100 |      2.594 us |  1.07 |     24.16 KB |        1.02 |
| FirstCons |   1000 |     24.471 us |  1.00 |     240.3 KB |        1.00 |
|  NextCons |   1000 |     26.581 us |  1.09 |    240.71 KB |        1.00 |
| FirstCons |  10000 |    410.706 us |  1.00 |   2494.05 KB |        1.00 |
|  NextCons |  10000 |    438.763 us |  1.07 |   2494.45 KB |        1.00 |
| FirstCons | 100000 | 12,322.450 us |  1.00 |  25679.42 KB |        1.00 |
|  NextCons | 100000 | 12,389.234 us |  1.01 |  25679.82 KB |        1.00 |

I did come up with one hypothesis to explain some of the difference.  If you look at the code for `cons`, the major work is done in methods `pushTail` and `newPath`.  They are very simple.  I looked at the IL generated from both the F# and the C# code.  The IL is almost identical.  With one exception.  Both methods create little arrays and hook them together.  The code to create an array in C#

```C#
object[] array = new object[_tail.length+1];
```

Once you have the size on the stack, the array allocation is just one IL instruction:

```
newarr [netstandard]System.Object
```

In the F# code we have

```F#
object[] newTail = ArrayModule.ZeroCreate<object>(tail.Length + 1);
```

We are told this is the preferred mechanizm for creating an array.  That translates into

```
call !!0[] [FSharp.Core]Microsoft.FSharp.Collections.ArrayModule::ZeroCreate<object>(int32)
```

Does that make a difference?   I looked at the assembler generated by the JIT (thanks to [sharplab.io](https://sharplab.io)).  I'm not an expert in reading the assembler generated for .NET code, but that `call` translates into a jump to a subroutine rather than a full-blown .NET method call.  And you can find the source code for `ArrayModule::ZeroCreate`:

```F#
let zeroCreate count =
    if count < 0 then
        invalidArgInputMustBeNonNegative "count" count

    Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked count
```

The code for `zeroCreateUnchecked` is set up to directly emit a `newarr` instruction when compiled.

So is the argument check plus the subroutine jump enough to make a difference over just executing `newarr`?  Yep.
I tested calls to a C# method that just calls `new object[n]` and an F# method that just calls `Array.zeroCreate n`.
The results, please.

| Method |     Mean |    Error |   StdDev | Ratio | RatioSD |     Gen0 |   Gen1 | Allocated | Alloc Ratio |
|------- |---------:|---------:|---------:|------:|--------:|---------:|-------:|----------:|------------:|
| CSharp | 89.70 us | 1.754 us | 1.802 us |  1.00 |    0.00 | 214.2334 | 0.1221 |   2.67 MB |        1.00 |
| FSharp | 96.70 us | 1.306 us | 1.020 us |  1.07 |    0.02 | 214.2334 | 0.1221 |   2.67 MB |        1.00 |

That 7% difference would not account for all the difference in the `cons` benchmark, given all the other operations, but it is a factor.  I have no idea how to get rid of this difference.


## Nth

With `nth` the initial results were more concerning.  Sample code:

```F#
[<Benchmark>]
member this.NextNth() =
    let pv = this.nextVec :> Clojure.Collections.Indexed
    let mutable acc: obj = null

    for i in 0 .. (this.size - 1) do
        acc <- pv.nth (i)

    acc
```

In this code, `this.nextVec` would be initialized during test setup to be a vector of the size being tested.  The benchmark calls `nth` on every item in the vector.  The results:

|   Method |   size |          Mean | Ratio |
|--------- |-------:|--------------:|------:|
| FirstNth |     10 |      34.32 ns |  1.00 |
|  NextNth |     10 |      31.59 ns |  0.92 |
| FirstNth |    100 |     385.30 ns |  1.00 |
|  NextNth |    100 |     504.02 ns |  1.31 |
| FirstNth |   1000 |   3,747.78 ns |  1.00 |
|  NextNth |   1000 |   4,966.74 ns |  1.33 |
| FirstNth |  10000 |  51,141.67 ns |  1.00 |
|  NextNth |  10000 |  68,569.16 ns |  1.34 |
| FirstNth | 100000 | 632,040.21 ns |  1.00 |
|  NextNth | 100000 | 747,017.44 ns |  1.18 |

The F# code is faster for smaller vectors -- that work is all done in the tail array, not in the index tree.
As soon as we hit the index tree, performance declines dramatically.

I compared the code at both the source level and the IL level.  The code for `nth` itself is essentially identical.
The difference comes in `arrayFor`.  The C# version:

```C#
object[] ArrayFor(int i) 
{
    if (i >= 0 && i < _cnt)
    {
        if (i >= tailoff())
            return _tail;
        Node node = _root;
        for (int level = _shift; level > 0; level -= 5)
            node = (Node)node.Array[(i >> level) & 0x01f];
        return node.Array;
    }
    throw new ArgumentOutOfRangeException("i");
}
```

The F# version:

```F#
member this.arrayFor(i) =
    if 0 <= i && i < cnt then
        if i >= this.tailoff () then
            tail
        else
            let rec loop (node: PVNode) level =
                if level <= 0 then
                    node.Array
                else
                    let newNode = node.Array[(i >>> level) &&& 0x1f] :?> PVNode
                    loop newNode (level - 5)
            loop root shift
    else
        raise <| ArgumentOutOfRangeException("i")
```

I used the standard trick of changing a loop with assignment in the body into a recursive looping function.
Tail recursive code such as this is converted efficiently into a loop by the F# compiler. 
The code looks almost identical to the loop code in the C# version.
With one difference.  I loaded the F# IL into ILSpy and had it write it back out as C#:

```C#
public object[] arrayFor(int i)
{
	if (0 <= i && i < cnt)
	{
		if (i >= tailoff())
		{
			return tail;
		}
		return $PersistentVector.step@658-35(i, root, shift);
	}
	ArgumentOutOfRangeException ex = new ArgumentOutOfRangeException("i");
	throw ex;
}
```

The loop is off in that internal function.  That loop is not inlined.  We pay  the overhead of a call.
And that is enough.   

I benchmarked loop vs recursive function code in F# when I first started writing code.  I wanted to do the things stylishly.
To be clear, some loops in F# can't be written directly as loops in F# -- breaking out of loops early is not supported.  But some could be translated more directly by using mutable bindings.
My benchmarking showed that generally the compiler generates very efficient looping code.  If you look at `$PersistentVector.step@658-35`, that code is essentially the same as the C# loop code.  However, the loop call usually cannot be inlined, so there is the overhead of calling it.  That overhead is nominal provided either (a) the effort within each iteration is significant or (b) the number of iterations is very large.   In this code, neither is true.  Each iteration does almost nothing.  And the number of iterations, even with large vector sizes, is very small -- tree depth is not going be more than three.  Under these circumstances, we feel the cost of the function call.

I did a benchmark just on the function call itself when the loop does almost nothing.  It is consistent with the findings here.

The solution to our performance problem:  just write a mutating loop in F#.

```F#
member this.arrayFor(i) =
    if 0 <= i && i < cnt then
        if i >= this.tailoff () then
            tail
        else
            let mutable node = root
            let mutable sh = shift

            while sh > 0 do
                node <- node.Array[(i >>> sh) &&& 0x1f] :?> PVNode
                sh <- sh-5
            node.Array
    else
        raise <| ArgumentOutOfRangeException("i")
```

The effect is notable:

|   Method |   size |          Mean |  Ratio | 
|--------- |-------:|--------------:|-------:|
| FirstNth |     10 |      34.01 ns |   1.00 | 
|  NextNth |     10 |      31.01 ns |   0.91 | 
| FirstNth |    100 |     385.56 ns |   1.00 | 
|  NextNth |    100 |     396.11 ns |   1.03 | 
| FirstNth |   1000 |   3,763.27 ns |   1.00 |
|  NextNth |   1000 |   3,891.33 ns |   1.03 |
| FirstNth |  10000 |  50,540.77 ns |   1.00 |
|  NextNth |  10000 |  56,087.60 ns |   1.11 |
| FirstNth | 100000 | 628,617.81 ns |   1.00 |
|  NextNth | 100000 | 600,595.01 ns |   0.95 |


One needs to be careful.  Sometimes purity of construct will get you into trouble.
But most of what I am doing so far is gaining much cleaner code (to my mind) with no significant performance hit.
Onward.  With caution.

# End note

In the second post in this series, I promised a benchmark on the relative performance of three ways of computing the array index at given level in the tree.

```F#
    static member doMod(i) =
        let im1 = i - 1
        im1 - (im1 % 32)     

    static member doDiv(i) =
        (( i - 1) / 32) *  32
            
    static member doShift(i) =
        ((i-1) >>> 5) <<< 5
```
 
 I did say the results were not going to be a surprise.


|     Method |     Mean |    Error |   StdDev | Ratio | RatioSD |
|----------- |---------:|---------:|---------:|------:|--------:|
|      ModOp | 56.75 us | 1.111 us | 0.984 us |  1.00 |    0.00 |
|      DivOp | 48.54 us | 0.751 us | 0.703 us |  0.86 |    0.02 |
|   BitShift | 31.78 us | 0.424 us | 0.376 us |  0.56 |    0.01 |

With that, time to leave the bench and head for the beach.