---
layout: post
title: A mega-dose of micro-benchmarks, Part 3 -- Finishing touches   
date: 2024-06-18 10:36:00 -0500
categories: general
---

Refer to the preceding posts

- [A mega-dose of micro-benchmarks, Part 1 -- Setting the stage]({{site.baseurl}}{% post_url 2024-06-18-mega-does-of-micro-benchmarks-part-1 %})
- [A mega-dose of micro-benchmarks, Part 2 -- By the numbers]({{site.baseurl}}{% post_url 2024-06-18-mega-does-of-micro-benchmarks-part-2 %})

for context.


We started all this work here:

```F#
    static member createWithCheck(init: obj array) =
        for i in 0 .. 2 .. init.Length-1 do
            for j in i + 2 .. 2 .. init.Length - 1  do
                if PersistentArrayMap.equalKey (init[i], init[j]) then
                    raise <| ArgumentException($"Duplicate key: {init[i]}")
                    
        PersistentArrayMap(init)
```

In Part 2, we dealt with the numeric comparison code underneath the `equalKey` call.
What's left?


## It matters how you iterate


I looked at the IL generated from the code above, and found it untidy.
Worse than untidy.  It was using an enumerator to iterate over a `Range`!

So I decided to benchmark.  Simple looping.

The constructs are: 

- InNoStep - `for iter in 0 .. this.size do ... `
- ForToIteration -`for iter = 0 to this.size-1 do ... `
- ManualIterationStep1 - manual iteration with an mutable index varialb.e

```F#
let mutable iter = 0
while iter < this.size do
    ...
    iter <- iter + 1
```


- InStep1 - `for iter in 0 .. 1 .. this.size do ... `

- ManualIterationStep2 - manual iteration with step size 2

```F#
let mutable iter = 0
while iter < doubleSize do
    ...
    iter <- iter + 2
```

- InStep2  - `for iter in 0 .. 2 .. doubleSize do ... `


The results varied from .Net 6.0 to .Net 8.0.  Here are the results for .Net 6.0:

| Method               | size    | Mean           | Ratio |
|--------------------- |-------- |---------------:|------:|
| InNoStep             | 1000    |       254.3 ns |  1.00 |
| ForToIteration       | 1000    |       255.4 ns |  1.00 |
| ManualIterationStep1 | 1000    |       255.8 ns |  1.01 |
| InStep1              | 1000    |       253.1 ns |  1.00 |
| ManualIterationStep2 | 1000    |       251.9 ns |  0.99 |
| InStep2              | 1000    |     3,737.7 ns | 14.71 |
|                      |         |                |       |
| InNoStep             | 1000000 |   249,367.3 ns |  1.00 |
| ForToIteration       | 1000000 |   246,770.3 ns |  0.99 |
| ManualIterationStep1 | 1000000 |   249,436.2 ns |  1.00 |
| InStep1              | 1000000 |   249,051.1 ns |  1.00 |
| ManualIterationStep2 | 1000000 |   249,295.9 ns |  1.00 |
| InStep2              | 1000000 | 3,184,726.6 ns | 12.77 |


Under .Net 8.0:

| Method               | size    | Mean           | Ratio |
|--------------------- |-------- |---------------:|------:|
| InNoStep             | 1000    |       264.3 ns |  1.00 |
| ForToIteration       | 1000    |       253.6 ns |  0.95 |
| ManualIterationStep1 | 1000    |       254.5 ns |  0.95 |
| InStep1              | 1000    |       256.2 ns |  0.96 |
| ManualIterationStep2 | 1000    |       254.6 ns |  0.95 |
| InStep2              | 1000    |     1,121.8 ns |  4.20 |
|                      |         |                |       |
| InNoStep             | 1000000 |   246,060.1 ns |  1.00 |
| ForToIteration       | 1000000 |   249,332.2 ns |  1.01 |
| ManualIterationStep1 | 1000000 |   252,724.2 ns |  1.03 |
| InStep1              | 1000000 |   245,521.5 ns |  1.00 |
| ManualIterationStep2 | 1000000 |   246,447.9 ns |  1.00 |
| InStep2              | 1000000 | 1,073,703.8 ns |  4.36 |

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

This should run just fine.  What is going on?   I'm going to have to hit the F# forums on this one.

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

And that's about it.  The code went from a 20-30% slowdown (it might have been worse than that, actually) to:

- __FirstCreatePAMCheck__ - this is the call to `PersistentArrayMap.createWithCheck` in the C# code
- __NextCreatePAMCheck__ - this is the call to `PersistentArrayMap.createWithCheck` in the F# code

The size colulmn indicates the size of the input array.  This carries us from an empty map up to the maximum size we will use `PersistentArrayMap` for.  


| Method              | size | Mean         | Ratio |
|-------------------- |----- |-------------:|------:|
| FirstCreatePAMCheck | 0    |     4.491 ns |  1.00 |
| NextCreatePAMCheck  | 0    |     4.290 ns |  0.95 |
|                     |      |              |       |
| FirstCreatePAMCheck | 1    |     4.380 ns |  1.00 |
| NextCreatePAMCheck  | 1    |     4.290 ns |  0.98 |
|                     |      |              |       |
| FirstCreatePAMCheck | 2    |    20.843 ns |  1.00 |
| NextCreatePAMCheck  | 2    |    17.334 ns |  0.83 |
|                     |      |              |       |
| FirstCreatePAMCheck | 3    |    48.023 ns |  1.00 |
| NextCreatePAMCheck  | 3    |    39.711 ns |  0.83 |
|                     |      |              |       |
| FirstCreatePAMCheck | 4    |    91.690 ns |  1.00 |
| NextCreatePAMCheck  | 4    |    68.231 ns |  0.74 |
|                     |      |              |       |
| FirstCreatePAMCheck | 6    |   234.983 ns |  1.00 |
| NextCreatePAMCheck  | 6    |   160.788 ns |  0.68 |
|                     |      |              |       |
| FirstCreatePAMCheck | 8    |   439.240 ns |  1.00 |
| NextCreatePAMCheck  | 8    |   320.191 ns |  0.73 |
|                     |      |              |       |
| FirstCreatePAMCheck | 12   | 1,018.231 ns |  1.00 |
| NextCreatePAMCheck  | 12   |   742.523 ns |  0.73 |
|                     |      |              |       |
| FirstCreatePAMCheck | 16   | 1,839.793 ns |  1.00 |
| NextCreatePAMCheck  | 16   | 1,310.127 ns |  0.71 |

Better across the board.

One caveat.  We had removed a call to check for keyword keys.
I'm not quite ready to implement `Keyword` yet, but I can substitute a check for a different type.
