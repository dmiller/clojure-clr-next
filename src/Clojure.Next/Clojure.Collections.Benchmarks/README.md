# Clojure.Collections.Benchmarks

This project contains a set of benchmarks for the Clojure collections library. 
Typically we are comparing the performance of the original ClojureCLR C# implementation with the new ClojureCLR.Next F# implementation.

We've added various individual benchmarks over time.
Edit the `main` entry point in `Program.fs` to run the benchmark you are interested in.

Historically, I started with benchmarking the hash map implementations. Then backed up to the simpler PersistentVector implementations.
And then discovered that simple things like `Util.equiv` were the source of some performance issues.  In turn that let me to the `Numbers` implementation.
So let's start from the bottom up in the retelling.

## Numeric Benchmarks

I started looking at `Number.equal`. That led through a bunch of other things to benchmark -- see the Benchmarks project -- and looking at numeric conversions.
By the time I figured out all that, I had solved a performance issue with numeric conversions -- `Clojure.Numerics.Converters.convertToLong` et al.  
We ended up with a win.

| Method             | size    | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0      | Allocated | Alloc Ratio |
|------------------- |-------- |---------:|----------:|----------:|------:|--------:|----------:|----------:|------------:|
| FirstNumberConvert | 1000000 | 7.476 ms | 0.1469 ms | 0.1909 ms |  1.00 |    0.00 | 1375.0000 |  17.17 MB |        1.00 |
| NextNumberConvert  | 1000000 | 7.080 ms | 0.0283 ms | 0.0264 ms |  0.95 |    0.02 | 1375.0000 |  17.17 MB |        1.00 |


However, after all that work, we still weren't quite up to speed on `Number.equal`:

| Method                 | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------- |---------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| FirstNumberEqual | 17.66 ns | 0.344 ns | 0.338 ns |  1.00 |    0.00 | 0.0037 |      48 B |        1.00 |
| NextNumberEqual  | 19.44 ns | 0.282 ns | 0.264 ns |  1.10 |    0.03 | 0.0037 |      48 B |        1.00 |

We are talking here about less that 2ns per call.  Can we find it?

The call here is:

```F#
Clojure.Numerics.Numbers.equal( 1L, 2L)
```

which takes us to `Numbers.equal`:

```F#
static member equal(x: obj, y: obj) =
    OpsSelector.ops (x) = OpsSelector.ops (y) && Numbers.getOps(x, y).equiv (x, y)
```

I can guess by inspection:  that `=` is going to turn into a `LanguagePrimitives.generic something`.


```
.method public static 
	bool equal (
		object x,
		object y
	) cil managed 
{
	// Method begins at RVA 0x49b0
	// Header size: 1
	// Code size: 31 (0x1f)
	.maxstack 8

	// if (OpsSelector.ops(x) == OpsSelector.ops(y))
	IL_0000: ldarg.0
	IL_0001: call valuetype Clojure.Numerics.OpsType Clojure.Numerics.OpsSelector::ops(object)
	IL_0006: ldarg.1
	IL_0007: call valuetype Clojure.Numerics.OpsType Clojure.Numerics.OpsSelector::ops(object)
	IL_000c: bne.un.s IL_001d

	// return getOps(x, y).equiv(x, y);
	IL_000e: ldarg.0
	IL_000f: ldarg.1
	IL_0010: call class Clojure.Numerics.Ops Clojure.Numerics.Numbers::getOps(object, object)
	IL_0015: ldarg.0
	IL_0016: ldarg.1
	IL_0017: callvirt instance bool Clojure.Numerics.Ops::equiv(object, object)
	IL_001c: ret

	// return false;
	IL_001d: ldc.i4.0
	IL_001e: ret
} // end of method Numbers::equal
```

And I'm wrong.  We actually have `bne.un.s` -- can't do better than that.
So either the `getOps` or the `equiv` is slow.

Let's look at the C# version of `Numbers.equal`:

```C#
.method public hidebysig static 
	bool equal (
		object x,
		object y
	) cil managed 
{
	// Method begins at RVA 0x22084
	// Header size: 12
	// Code size: 47 (0x2f)
	.maxstack 3
	.locals init (
		[0] bool
	)

	// {
	IL_0000: nop
	// return category(x) == category(y) && ops(x).combine(ops(y)).equiv(x, y);
	IL_0001: ldarg.0
	IL_0002: call valuetype clojure.lang.Numbers/Category clojure.lang.Numbers::category(object)
	IL_0007: ldarg.1
	IL_0008: call valuetype clojure.lang.Numbers/Category clojure.lang.Numbers::category(object)
	IL_000d: bne.un.s IL_0029

	IL_000f: ldarg.0
	IL_0010: call class clojure.lang.Numbers/Ops clojure.lang.Numbers::ops(object)
	IL_0015: ldarg.1
	IL_0016: call class clojure.lang.Numbers/Ops clojure.lang.Numbers::ops(object)
	IL_001b: callvirt instance class clojure.lang.Numbers/Ops clojure.lang.Numbers/Ops::combine(class clojure.lang.Numbers/Ops)
	IL_0020: ldarg.0
	IL_0021: ldarg.1
	IL_0022: callvirt instance bool clojure.lang.Numbers/Ops::equiv(object, object)
	// (no C# code)
	IL_0027: br.s IL_002a

	IL_0029: ldc.i4.0

	IL_002a: stloc.0
	IL_002b: br.s IL_002d

	IL_002d: ldloc.0
	IL_002e: ret
} // end of method Numbers::equal
```

I had already done some work on `Clojure.Numerics.OpsSelector::ops(object)`, `Clojure.Numerics.Numbers::getOps(object, object)`, and `Clojure.Numerics.Ops::equiv(object, object)`. 
See the `Benchmarks` project.  But those were testing variant coding within F#, not against the C# versions.
It is hard to imagine the the C# `clojure.lang.Numbers::category(object)` is faster than the  F# `Clojure.Numerics.OpsSelector::ops(object)`  -- the former is a mess, the latter is a simple lookup.
Ditto for the ops/combine part -- in F#, it's a lookup in a 2D array.  The 'equiv' code is identical:  two calls to convert and a `ceq` IL instruction. And we now our convert call is faster.

Because some parts of this code is private on the C# side, we'll have to extract the relevant parts into some benchmark project and do our tests.  Back in a bit.

... and we're back.  Check out project `BabyNumbers.CSharp` for a very cutdown version of the C# `Numbers` code.

Let's dig in.


First, we do a simple comparison of C# `Numbers.category` vs F# `Numbers.getOps`.
They aren't identical -- `Numbers.category` maps an object to its 'category', which is an enum value.
`Numbers.getOps` actually returns an instance of `Ops` -- a class that implements the `Ops` interface.
The specific objects implementing the `Ops` interface (of types `LongOps`, `DoubleOps`, etc.) stand in for the category used in the C# code.
As long as they are being compared via `ceq` IL instruction, we should be fine.

However, we found this:

| Method        | inputType | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------- |---------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| FirstCategory | I32       | 1.187 ns | 0.0119 ns | 0.0111 ns |  1.00 |    0.00 |         - |          NA |
| NextOps       | I32       | 6.275 ns | 0.1219 ns | 0.1140 ns |  5.29 |    0.10 |         - |          NA |
|               |           |          |           |           |       | 
| FirstCategory | I64       | 1.181 ns | 0.0255 ns | 0.0238 ns |  1.00 |    0.00 |         - |          NA |
| NextOps       | I64       | 4.527 ns | 0.0289 ns | 0.0256 ns |  3.83 |    0.09 |         - |          NA |
|               |           |          |           |           |       | 
| FirstCategory | Dbl       | 1.408 ns | 0.0232 ns | 0.0217 ns |  1.00 |    0.00 |         - |          NA |
| NextOps       | Dbl       | 4.724 ns | 0.1266 ns | 0.1184 ns |  3.36 |    0.10 |         - |          NA |
|               |           |          |           |           |       | 
| FirstCategory | U64       | 1.673 ns | 0.0575 ns | 0.0639 ns |  1.00 |    0.00 |         - |          NA |
| NextOps       | U64       | 4.568 ns | 0.0688 ns | 0.0610 ns |  2.72 |    0.12 |         - |          NA |


A 3-5x performance hit.  Ouch.  A quick scan of the for `Numbers.getOps` shows that we are getting bitten by static initialization.
Apparently tiered compilation is not getting rid of this initialization checks that occur on every initialization.

What can we do?  Move the needed fields out of classes and into a module.  This will require some code restructuring, for sure.


First attempt:  Restrurcture thing into OpsImpl.  Recursive namespace, so the module could reference LongOps and other classes.
However, we end up with Lazy initialization, which is not what we want.


| Method        | inputType | Mean     | Error     | StdDev    | Ratio | RatioSD |
|-------------- |---------- |---------:|----------:|----------:|------:|--------:|
| FirstCategory | I32       | 1.220 ns | 0.0109 ns | 0.0085 ns |  1.00 |    0.00 |
| NextOps       | I32       | 5.564 ns | 0.1036 ns | 0.1018 ns |  4.56 |    0.09 |
|               |           |          |           |           |       |         |
| FirstCategory | I64       | 1.214 ns | 0.0227 ns | 0.0213 ns |  1.00 |    0.00 |
| NextOps       | I64       | 3.905 ns | 0.0348 ns | 0.0326 ns |  3.22 |    0.06 |
|               |           |          |           |           |       |         |
| FirstCategory | Dbl       | 1.493 ns | 0.0498 ns | 0.0466 ns |  1.00 |    0.00 |
| NextOps       | Dbl       | 4.112 ns | 0.0552 ns | 0.0517 ns |  2.76 |    0.09 |
|               |           |          |           |           |       |         |
| FirstCategory | U64       | 1.658 ns | 0.0167 ns | 0.0157 ns |  1.00 |    0.00 |
| NextOps       | U64       | 3.970 ns | 0.0450 ns | 0.0399 ns |  2.40 |    0.03 |

Second attempt:  use let bindings to initialize the fields.  Trying still to avoid static initialization fields.
It turns out that the let binding initialization looks okay.
_However_, a each reference to the let binding has the static initialization check.  So we are back to square one.

| Method        | inputType | Mean     | Error     | StdDev    | Ratio | RatioSD |
|-------------- |---------- |---------:|----------:|----------:|------:|--------:|
| FirstCategory | I32       | 1.193 ns | 0.0260 ns | 0.0217 ns |  1.00 |    0.00 |
| NextOps       | I32       | 5.351 ns | 0.0253 ns | 0.0237 ns |  4.49 |    0.08 |
|               |           |          |           |           |       |         |
| FirstCategory | I64       | 1.233 ns | 0.0196 ns | 0.0184 ns |  1.00 |    0.00 |
| NextOps       | I64       | 3.306 ns | 0.0414 ns | 0.0388 ns |  2.68 |    0.05 |
|               |           |          |           |           |       |         |
| FirstCategory | Dbl       | 1.376 ns | 0.0140 ns | 0.0131 ns |  1.00 |    0.00 |
| NextOps       | Dbl       | 3.410 ns | 0.0431 ns | 0.0403 ns |  2.48 |    0.04 |
|               |           |          |           |           |       |         |
| FirstCategory | U64       | 1.597 ns | 0.0233 ns | 0.0218 ns |  1.00 |    0.00 |
| NextOps       | U64       | 3.442 ns | 0.0386 ns | 0.0361 ns |  2.16 |    0.04 |

Moved the getOps code and related to the OpsSelector module.  This time I put the intialization into a separate module.
That module's init function must be called before any of the OpsSelector functions are called.

| Method        | inputType | Mean     | Error     | StdDev    | Ratio | RatioSD |
|-------------- |---------- |---------:|----------:|----------:|------:|--------:|
| FirstCategory | I32       | 1.282 ns | 0.0334 ns | 0.0296 ns |  1.00 |    0.00 |
| NextOps       | I32       | 4.655 ns | 0.0857 ns | 0.0802 ns |  3.63 |    0.10 |
|               |           |          |           |           |       |         |
| FirstCategory | I64       | 1.185 ns | 0.0206 ns | 0.0172 ns |  1.00 |    0.00 |
| NextOps       | I64       | 2.474 ns | 0.0704 ns | 0.0624 ns |  2.09 |    0.06 |
|               |           |          |           |           |       |         |
| FirstCategory | Dbl       | 1.402 ns | 0.0215 ns | 0.0179 ns |  1.00 |    0.00 |
| NextOps       | Dbl       | 2.615 ns | 0.0942 ns | 0.1008 ns |  1.84 |    0.07 |
|               |           |          |           |           |       |         |
| FirstCategory | U64       | 1.635 ns | 0.0224 ns | 0.0187 ns |  1.00 |    0.00 |
| NextOps       | U64       | 2.528 ns | 0.0459 ns | 0.0407 ns |  1.55 |    0.03 |


| Method        | inputType | Mean     | Error     | StdDev    | Ratio | RatioSD |
|-------------- |---------- |---------:|----------:|----------:|------:|--------:|
| FirstCategory | I32       | 1.195 ns | 0.0184 ns | 0.0144 ns |  1.00 |    0.00 |
| NextOps       | I32       | 3.133 ns | 0.0214 ns | 0.0179 ns |  2.62 |    0.04 |
|               |           |          |           |           |       |         |
| FirstCategory | I64       | 1.207 ns | 0.0193 ns | 0.0171 ns |  1.00 |    0.00 |
| NextOps       | I64       | 1.906 ns | 0.0517 ns | 0.0459 ns |  1.58 |    0.05 |
|               |           |          |           |           |       |         |
| FirstCategory | Dbl       | 1.426 ns | 0.0426 ns | 0.0399 ns |  1.00 |    0.00 |
| NextOps       | Dbl       | 1.825 ns | 0.0356 ns | 0.0315 ns |  1.28 |    0.03 |
|               |           |          |           |           |       |         |
| FirstCategory | U64       | 1.554 ns | 0.0505 ns | 0.0422 ns |  1.00 |    0.00 |
| NextOps       | U64       | 1.864 ns | 0.0598 ns | 0.0587 ns |  1.21 |    0.03 |





We are still 1.5-3.6x slower than the C# code.  But we are getting closer.
Then I realized I wasn't comparing apples to apples.  
The correct comparision is actually `Numbers.category` to `OpsSelector.ops` (instead of `OpsSelector.getOps`)

| Method        | inputType | Mean     | Error     | StdDev    | Ratio | RatioSD |
|-------------- |---------- |---------:|----------:|----------:|------:|--------:|
| FirstCategory | I32       | 1.195 ns | 0.0184 ns | 0.0144 ns |  1.00 |    0.00 |
| NextOps       | I32       | 3.133 ns | 0.0214 ns | 0.0179 ns |  2.62 |    0.04 |
|               |           |          |           |           |       |         |
| FirstCategory | I64       | 1.207 ns | 0.0193 ns | 0.0171 ns |  1.00 |    0.00 |
| NextOps       | I64       | 1.906 ns | 0.0517 ns | 0.0459 ns |  1.58 |    0.05 |
|               |           |          |           |           |       |         |
| FirstCategory | Dbl       | 1.426 ns | 0.0426 ns | 0.0399 ns |  1.00 |    0.00 |
| NextOps       | Dbl       | 1.825 ns | 0.0356 ns | 0.0315 ns |  1.28 |    0.03 |
|               |           |          |           |           |       |         |
| FirstCategory | U64       | 1.554 ns | 0.0505 ns | 0.0422 ns |  1.00 |    0.00 |
| NextOps       | U64       | 1.864 ns | 0.0598 ns | 0.0587 ns |  1.21 |    0.03 |

That saves an array lookup.  And we are now within 1.2-2.6x of the C# code.  I think we are done here.
For one thing, we have more types that we are considering.  
In the C#, only types  `Int32`, `Int64`, `Double`, `Single`, `Decimal`, `BigInt`, `Ratio` and `BigDecimal` are considered.
We include the signed and unsigned short and byte types along with `BigInteger`.

In the `Converter.Benchmark` project, we have a benchmark that compare categorizing the type of an object, basically what is being done here, by matching type vs matching type code
By switching to comparison against `Type` instead of `TypeCode`, we get significant speedup.
It should be noted that this is order-dependent.  I picked an order that I thought would serve best when dealing with unnknown types.

| Method        | inputType | Mean      | Error     | StdDev    | Ratio | RatioSD |
|-------------- |---------- |----------:|----------:|----------:|------:|--------:|
| FirstCategory | I32       | 1.2089 ns | 0.0290 ns | 0.0271 ns |  1.00 |    0.00 |
| NextOps       | I32       | 2.1974 ns | 0.0156 ns | 0.0146 ns |  1.82 |    0.04 |
|               |           |           |           |           |       |         |
| FirstCategory | I64       | 1.1973 ns | 0.0098 ns | 0.0087 ns |  1.00 |    0.00 |
| NextOps       | I64       | 0.5491 ns | 0.0213 ns | 0.0189 ns |  0.46 |    0.02 |
|               |           |           |           |           |       |         |
| FirstCategory | Dbl       | 1.4162 ns | 0.0154 ns | 0.0136 ns |  1.00 |    0.00 |
| NextOps       | Dbl       | 0.7587 ns | 0.0134 ns | 0.0125 ns |  0.54 |    0.01 |
|               |           |           |           |           |       |         |
| FirstCategory | U64       | 1.6379 ns | 0.0317 ns | 0.0296 ns |  1.00 |    0.00 |
| NextOps       | U64       | 3.4541 ns | 0.0547 ns | 0.0512 ns |  2.11 |    0.05 |


We are actually faster now in some cases.  I think we are done here.

Now on to `Numbers.equal`.  My original F# code looked like this:

```F#
static member equal(x: obj, y: obj) =
   OpsSelector.ops (x) = OpsSelector.ops (y) && OpsSelector.getOps2(x, y).equiv (x, y)
```

THis mostly matches the C# code.  However, we can make two optimizations.
One is to just get the `OpType` first -- we save two array lookups.
Then, if both operands have the same `OpType`, we can look up the `Ops` object using their common `OpType` -- the combination of the same two `OpType` values will yield the same `OpType`.
Thus we save another array lookup.

```F#
static member equal(x: obj, y: obj) =
    let xOpType = OpsSelector.ops (x)
    let yOpType = OpsSelector.ops (y)
    xOpType =  yOpType && OpsSelector.getOpsByType(xOpType).equiv (x, y)
```

And here are our first results:

| Method           | xInputType | yInputType | Mean      | Error     | StdDev    | Ratio | RatioSD |
|----------------- |----------- |----------- |----------:|----------:|----------:|------:|--------:|
| FirstNumberEqual | I32        | I32        | 14.388 ns | 0.2834 ns | 0.2651 ns |  1.00 |    0.00 |
| NextNumberEqual  | I32        | I32        |  7.004 ns | 0.0997 ns | 0.0932 ns |  0.49 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I32        | I64        | 14.162 ns | 0.1615 ns | 0.1511 ns |  1.00 |    0.00 |
| NextNumberEqual  | I32        | I64        |  8.550 ns | 0.0862 ns | 0.0764 ns |  0.60 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I32        | Dbl        |  3.118 ns | 0.0437 ns | 0.0409 ns |  1.00 |    0.00 |
| NextNumberEqual  | I32        | Dbl        |  3.911 ns | 0.0556 ns | 0.0520 ns |  1.25 |    0.03 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I32        | U64        | 19.433 ns | 0.2776 ns | 0.2461 ns |  1.00 |    0.00 | 
| NextNumberEqual  | I32        | U64        |  5.328 ns | 0.0360 ns | 0.0319 ns |  0.27 |    0.00 | 
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I64        | I32        | 13.103 ns | 0.1128 ns | 0.1000 ns |  1.00 |    0.00 |
| NextNumberEqual  | I64        | I32        |  8.460 ns | 0.0632 ns | 0.0591 ns |  0.65 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I64        | I64        | 13.158 ns | 0.1301 ns | 0.1153 ns |  1.00 |    0.00 |
| NextNumberEqual  | I64        | I64        |  5.620 ns | 0.0909 ns | 0.0851 ns |  0.43 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I64        | Dbl        |  3.075 ns | 0.0864 ns | 0.1093 ns |  1.00 |    0.00 |
| NextNumberEqual  | I64        | Dbl        |  3.927 ns | 0.0568 ns | 0.0532 ns |  1.26 |    0.03 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I64        | U64        | 21.353 ns | 0.3482 ns | 0.3871 ns |  1.00 |    0.00 | 
| NextNumberEqual  | I64        | U64        |  5.610 ns | 0.0806 ns | 0.0673 ns |  0.26 |    0.01 | 
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | Dbl        | I32        |  3.463 ns | 0.0894 ns | 0.0918 ns |  1.00 |    0.00 |
| NextNumberEqual  | Dbl        | I32        |  4.016 ns | 0.0458 ns | 0.0429 ns |  1.16 |    0.03 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | Dbl        | I64        |  3.460 ns | 0.0215 ns | 0.0190 ns |  1.00 |    0.00 |
| NextNumberEqual  | Dbl        | I64        |  3.974 ns | 0.0268 ns | 0.0250 ns |  1.15 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | Dbl        | Dbl        | 12.815 ns | 0.1173 ns | 0.1040 ns |  1.00 |    0.00 |
| NextNumberEqual  | Dbl        | Dbl        |  6.352 ns | 0.0465 ns | 0.0388 ns |  0.50 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | Dbl        | U64        |  3.339 ns | 0.0291 ns | 0.0272 ns |  1.00 |    0.00 |
| NextNumberEqual  | Dbl        | U64        |  5.434 ns | 0.1187 ns | 0.1111 ns |  1.63 |    0.03 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | U64        | I32        | 21.206 ns | 0.4490 ns | 0.7625 ns |  1.00 |    0.00 | 
| NextNumberEqual  | U64        | I32        |  5.561 ns | 0.0871 ns | 0.0815 ns |  0.26 |    0.01 | 
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | U64        | I64        | 21.978 ns | 0.2441 ns | 0.2283 ns |  1.00 |    0.00 | 
| NextNumberEqual  | U64        | I64        |  5.528 ns | 0.1107 ns | 0.1036 ns |  0.25 |    0.00 | 
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | U64        | Dbl        |  3.282 ns | 0.0471 ns | 0.0441 ns |  1.00 |    0.00 |
| NextNumberEqual  | U64        | Dbl        |  5.563 ns | 0.0972 ns | 0.0910 ns |  1.70 |    0.03 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | U64        | U64        | 13.404 ns | 0.1125 ns | 0.1053 ns |  1.00 |    0.00 |
| NextNumberEqual  | U64        | U64        | 11.198 ns | 0.0866 ns | 0.0810 ns |  0.84 |    0.01 |

Because this is susceptible to the order the types are tested, I moved the orderings around to see if we could get a better result, keeping in account what types I think are most likley to occure.
The results:

| Method           | xInputType | yInputType | Mean      | Error     | StdDev    | Ratio | RatioSD |
|----------------- |----------- |----------- |----------:|----------:|----------:|------:|--------:|
| FirstNumberEqual | I32        | I32        | 14.780 ns | 0.2606 ns | 0.2438 ns |  1.00 |    0.00 |
| NextNumberEqual  | I32        | I32        |  7.063 ns | 0.0986 ns | 0.0923 ns |  0.48 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I32        | I64        | 14.105 ns | 0.0760 ns | 0.0674 ns |  1.00 |    0.00 |
| NextNumberEqual  | I32        | I64        |  8.796 ns | 0.1038 ns | 0.0971 ns |  0.62 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I32        | Dbl        |  3.174 ns | 0.0677 ns | 0.0634 ns |  1.00 |    0.00 |
| NextNumberEqual  | I32        | Dbl        |  4.323 ns | 0.0935 ns | 0.0874 ns |  1.36 |    0.04 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I32        | U64        | 19.936 ns | 0.2866 ns | 0.2541 ns |  1.00 |    0.00 |
| NextNumberEqual  | I32        | U64        |  5.320 ns | 0.0962 ns | 0.0900 ns |  0.27 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I64        | I32        | 14.198 ns | 0.2820 ns | 0.2638 ns |  1.00 |    0.00 |
| NextNumberEqual  | I64        | I32        |  8.882 ns | 0.0588 ns | 0.0550 ns |  0.63 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I64        | I64        | 12.958 ns | 0.1419 ns | 0.1327 ns |  1.00 |    0.00 |
| NextNumberEqual  | I64        | I64        |  5.573 ns | 0.0944 ns | 0.0883 ns |  0.43 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I64        | Dbl        |  3.259 ns | 0.0856 ns | 0.0985 ns |  1.00 |    0.00 |
| NextNumberEqual  | I64        | Dbl        |  3.568 ns | 0.0532 ns | 0.0498 ns |  1.09 |    0.04 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | I64        | U64        | 19.926 ns | 0.2779 ns | 0.2463 ns |  1.00 |    0.00 |
| NextNumberEqual  | I64        | U64        |  5.374 ns | 0.0349 ns | 0.0326 ns |  0.27 |    0.00 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | Dbl        | I32        |  3.335 ns | 0.0241 ns | 0.0225 ns |  1.00 |    0.00 |
| NextNumberEqual  | Dbl        | I32        |  4.428 ns | 0.0753 ns | 0.0705 ns |  1.33 |    0.02 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | Dbl        | I64        |  3.379 ns | 0.0455 ns | 0.0403 ns |  1.00 |    0.00 |
| NextNumberEqual  | Dbl        | I64        |  3.458 ns | 0.0190 ns | 0.0169 ns |  1.02 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | Dbl        | Dbl        | 12.553 ns | 0.1045 ns | 0.0977 ns |  1.00 |    0.00 |
| NextNumberEqual  | Dbl        | Dbl        |  6.203 ns | 0.0607 ns | 0.0507 ns |  0.49 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | Dbl        | U64        |  3.174 ns | 0.0231 ns | 0.0205 ns |  1.00 |    0.00 |
| NextNumberEqual  | Dbl        | U64        |  5.529 ns | 0.0587 ns | 0.0549 ns |  1.74 |    0.02 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | U64        | I32        | 19.539 ns | 0.1167 ns | 0.0911 ns |  1.00 |    0.00 |
| NextNumberEqual  | U64        | I32        |  5.395 ns | 0.0607 ns | 0.0567 ns |  0.28 |    0.00 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | U64        | I64        | 20.614 ns | 0.3877 ns | 0.3626 ns |  1.00 |    0.00 |
| NextNumberEqual  | U64        | I64        |  5.268 ns | 0.0386 ns | 0.0361 ns |  0.26 |    0.01 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | U64        | Dbl        |  3.072 ns | 0.0831 ns | 0.0736 ns |  1.00 |    0.00 |
| NextNumberEqual  | U64        | Dbl        |  5.269 ns | 0.0443 ns | 0.0393 ns |  1.72 |    0.04 |
|                  |            |            |           |           |           |       |         |
| FirstNumberEqual | U64        | U64        | 13.180 ns | 0.1771 ns | 0.1656 ns |  1.00 |    0.00 |
| NextNumberEqual  | U64        | U64        | 10.779 ns | 0.0900 ns | 0.0798 ns |  0.82 |    0.01 |


The C# version does not handle unsigned types properly.  If we take those out of the mix, we get the following results:


| Method                 | xInputType | yInputType | Mean      | Error     | StdDev    | Ratio | RatioSD |
|----------------------- |----------- |----------- |----------:|----------:|----------:|------:|--------:|
| FirstNumberEqualOnLong | I32        | I32        | 14.512 ns | 0.2087 ns | 0.1953 ns |  1.00 |    0.00 |
| NextNumberEqualOnLong  | I32        | I32        |  6.848 ns | 0.0741 ns | 0.0657 ns |  0.47 |    0.01 |
|                        |            |            |           |           |           |       |         |
| FirstNumberEqualOnLong | I32        | I64        | 14.491 ns | 0.1598 ns | 0.1416 ns |  1.00 |    0.00 |
| NextNumberEqualOnLong  | I32        | I64        |  9.183 ns | 0.1326 ns | 0.1175 ns |  0.63 |    0.01 |
|                        |            |            |           |           |           |       |         |
| FirstNumberEqualOnLong | I32        | Dbl        |  2.674 ns | 0.0547 ns | 0.0512 ns |  1.00 |    0.00 |
| NextNumberEqualOnLong  | I32        | Dbl        |  4.111 ns | 0.0917 ns | 0.0858 ns |  1.54 |    0.04 |
|                        |            |            |           |           |           |       |         |
| FirstNumberEqualOnLong | I64        | I32        | 13.888 ns | 0.2715 ns | 0.2539 ns |  1.00 |    0.00 |
| NextNumberEqualOnLong  | I64        | I32        |  9.250 ns | 0.0864 ns | 0.0808 ns |  0.67 |    0.02 |
|                        |            |            |           |           |           |       |         |
| FirstNumberEqualOnLong | I64        | I64        | 13.692 ns | 0.1565 ns | 0.1464 ns |  1.00 |    0.00 |
| NextNumberEqualOnLong  | I64        | I64        |  5.333 ns | 0.0657 ns | 0.0615 ns |  0.39 |    0.00 |
|                        |            |            |           |           |           |       |         |
| FirstNumberEqualOnLong | I64        | Dbl        |  2.790 ns | 0.0641 ns | 0.0600 ns |  1.00 |    0.00 |
| NextNumberEqualOnLong  | I64        | Dbl        |  2.797 ns | 0.0775 ns | 0.0687 ns |  1.00 |    0.03 |
|                        |            |            |           |           |           |       |         |
| FirstNumberEqualOnLong | Dbl        | I32        |  2.941 ns | 0.0517 ns | 0.0483 ns |  1.00 |    0.00 |
| NextNumberEqualOnLong  | Dbl        | I32        |  4.053 ns | 0.0491 ns | 0.0435 ns |  1.38 |    0.02 |
|                        |            |            |           |           |           |       |         |
| FirstNumberEqualOnLong | Dbl        | I64        |  2.711 ns | 0.0637 ns | 0.0596 ns |  1.00 |    0.00 |
| NextNumberEqualOnLong  | Dbl        | I64        |  2.760 ns | 0.0710 ns | 0.0664 ns |  1.02 |    0.03 |
|                        |            |            |           |           |           |       |         |
| FirstNumberEqualOnLong | Dbl        | Dbl        | 12.366 ns | 0.2579 ns | 0.2759 ns |  1.00 |    0.00 |
| NextNumberEqualOnLong  | Dbl        | Dbl        |  5.930 ns | 0.1268 ns | 0.1187 ns |  0.48 |    0.01 |


The only place we have decreased performance is anything involving `Double`, except `Double` vs `Double`.  
And the benchmarking I did on the conversion code indicates that is a likely outcome.
I think we are done here.

## PeristentArrayMap.createWithCheck

Now we can turn our attention back to where we started: `PersistentArrayMap.createWithCheck`.
And miraculously, the changes made so far have moved us from 20-30% deficit to beating the C# version across the board:


| Method              | size | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |----- |-------------:|-----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| FirstCreatePAMCheck | 0    |     4.491 ns |  0.1191 ns |  0.1549 ns |  1.00 |    0.00 | 0.0031 |      40 B |        1.00 |
| NextCreatePAMCheck  | 0    |     4.290 ns |  0.0722 ns |  0.0676 ns |  0.95 |    0.04 | 0.0031 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |            |
| FirstCreatePAMCheck | 1    |     4.380 ns |  0.1124 ns |  0.0938 ns |  1.00 |    0.00 | 0.0031 |      40 B |        1.00 |
| NextCreatePAMCheck  | 1    |     4.290 ns |  0.0548 ns |  0.0458 ns |  0.98 |    0.03 | 0.0031 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |            |
| FirstCreatePAMCheck | 2    |    20.843 ns |  0.1481 ns |  0.1313 ns |  1.00 |    0.00 | 0.0030 |      40 B |        1.00 |
| NextCreatePAMCheck  | 2    |    17.334 ns |  0.1093 ns |  0.0913 ns |  0.83 |    0.01 | 0.0030 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |            |
| FirstCreatePAMCheck | 3    |    48.023 ns |  0.2910 ns |  0.2722 ns |  1.00 |    0.00 | 0.0030 |      40 B |        1.00 |
| NextCreatePAMCheck  | 3    |    39.711 ns |  0.8254 ns |  0.8476 ns |  0.83 |    0.02 | 0.0030 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |            |
| FirstCreatePAMCheck | 4    |    91.690 ns |  1.2266 ns |  1.1474 ns |  1.00 |    0.00 | 0.0030 |      40 B |        1.00 |
| NextCreatePAMCheck  | 4    |    68.231 ns |  0.3469 ns |  0.3076 ns |  0.74 |    0.01 | 0.0030 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |            |
| FirstCreatePAMCheck | 6    |   234.983 ns |  2.3130 ns |  2.1636 ns |  1.00 |    0.00 | 0.0029 |      40 B |        1.00 |
| NextCreatePAMCheck  | 6    |   160.788 ns |  1.1626 ns |  1.0307 ns |  0.68 |    0.01 | 0.0029 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |            |
| FirstCreatePAMCheck | 8    |   439.240 ns |  7.4447 ns |  6.9638 ns |  1.00 |    0.00 | 0.0029 |      40 B |        1.00 |
| NextCreatePAMCheck  | 8    |   320.191 ns |  4.0409 ns |  3.7798 ns |  0.73 |    0.01 | 0.0029 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |            |
| FirstCreatePAMCheck | 12   | 1,018.231 ns | 12.0738 ns | 11.2938 ns |  1.00 |    0.00 | 0.0019 |      40 B |        1.00 |
| NextCreatePAMCheck  | 12   |   742.523 ns |  3.2300 ns |  2.6972 ns |  0.73 |    0.01 | 0.0029 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |            |
| FirstCreatePAMCheck | 16   | 1,839.793 ns | 13.5378 ns | 12.6632 ns |  1.00 |    0.00 | 0.0019 |      40 B |        1.00 |
| NextCreatePAMCheck  | 16   | 1,310.127 ns | 13.7707 ns | 12.2074 ns |  0.71 |    0.01 | 0.0019 |      40 B |        1.00 |

This is not the most surprising result. 
My initial inspection of what was going on with `PersistentArrayMap.createWithCheck` method indicated the problem was the comparisons needed to check for duplicate keys in the input data.
Having fixed that code, we have fixed the problem here.

Turning next to `PersistentArrayMap.createAsIfByAssoc`, we have the following results:

| Method              | size | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |----- |-------------:|-----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| FirstCreatePAMAssoc | 0    |     4.513 ns |  0.0592 ns |  0.0554 ns |  1.00 |    0.00 | 0.0031 |      40 B |        1.00 |
| NextCreatePAMAssoc  | 0    |     4.725 ns |  0.1365 ns |  0.2425 ns |  1.04 |    0.05 | 0.0031 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |             |
| FirstCreatePAMAssoc | 1    |     6.269 ns |  0.1664 ns |  0.2332 ns |  1.00 |    0.00 | 0.0031 |      40 B |        1.00 |
| NextCreatePAMAssoc  | 1    |     5.301 ns |  0.1496 ns |  0.3155 ns |  0.85 |    0.06 | 0.0031 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |             |
| FirstCreatePAMAssoc | 2    |    20.922 ns |  0.2228 ns |  0.2084 ns |  1.00 |    0.00 | 0.0030 |      40 B |        1.00 |
| NextCreatePAMAssoc  | 2    |    20.625 ns |  0.4512 ns |  0.4431 ns |  0.99 |    0.02 | 0.0030 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |             |
| FirstCreatePAMAssoc | 3    |    53.324 ns |  1.1010 ns |  1.3106 ns |  1.00 |    0.00 | 0.0030 |      40 B |        1.00 |
| NextCreatePAMAssoc  | 3    |    48.782 ns |  0.3977 ns |  0.3525 ns |  0.92 |    0.02 | 0.0030 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |             |
| FirstCreatePAMAssoc | 4    |    97.056 ns |  1.4609 ns |  1.3665 ns |  1.00 |    0.00 | 0.0030 |      40 B |        1.00 |
| NextCreatePAMAssoc  | 4    |    91.544 ns |  1.0751 ns |  1.0057 ns |  0.94 |    0.02 | 0.0030 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |             |
| FirstCreatePAMAssoc | 6    |   236.195 ns |  2.7293 ns |  2.1308 ns |  1.00 |    0.00 | 0.0029 |      40 B |        1.00 |
| NextCreatePAMAssoc  | 6    |   231.746 ns |  4.6671 ns |  4.3656 ns |  0.98 |    0.02 | 0.0029 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |             |
| FirstCreatePAMAssoc | 8    |   440.636 ns |  3.5030 ns |  3.2767 ns |  1.00 |    0.00 | 0.0029 |      40 B |        1.00 |
| NextCreatePAMAssoc  | 8    |   416.489 ns |  3.9717 ns |  3.7151 ns |  0.95 |    0.01 | 0.0029 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |             |
| FirstCreatePAMAssoc | 12   | 1,014.288 ns | 11.3241 ns | 10.0385 ns |  1.00 |    0.00 | 0.0019 |      40 B |        1.00 |
| NextCreatePAMAssoc  | 12   |   973.523 ns |  6.6224 ns |  5.8706 ns |  0.96 |    0.01 | 0.0019 |      40 B |        1.00 |
|                     |      |              |            |            |       |         |        |           |             |
| FirstCreatePAMAssoc | 16   | 1,851.113 ns | 34.9233 ns | 40.2178 ns |  1.00 |    0.00 | 0.0019 |      40 B |        1.00 |
| NextCreatePAMAssoc  | 16   | 1,750.867 ns | 34.6263 ns | 39.8757 ns |  0.95 |    0.03 | 0.0019 |      40 B |        1.00 |


So the wins continue.  I can live with a 0.2 nsec difference in the 0 case.  The rest of the time we are winning.
