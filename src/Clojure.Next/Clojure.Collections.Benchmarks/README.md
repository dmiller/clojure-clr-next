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
| FirstNumberEqualOnLong | 17.66 ns | 0.344 ns | 0.338 ns |  1.00 |    0.00 | 0.0037 |      48 B |        1.00 |
| NextNumberEqualOnLong  | 19.44 ns | 0.282 ns | 0.264 ns |  1.10 |    0.03 | 0.0037 |      48 B |        1.00 |

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
|               |           |          |           |           |       |         |           |             |
| FirstCategory | I64       | 1.181 ns | 0.0255 ns | 0.0238 ns |  1.00 |    0.00 |         - |          NA |
| NextOps       | I64       | 4.527 ns | 0.0289 ns | 0.0256 ns |  3.83 |    0.09 |         - |          NA |
|               |           |          |           |           |       |         |           |             |
| FirstCategory | Dbl       | 1.408 ns | 0.0232 ns | 0.0217 ns |  1.00 |    0.00 |         - |          NA |
| NextOps       | Dbl       | 4.724 ns | 0.1266 ns | 0.1184 ns |  3.36 |    0.10 |         - |          NA |
|               |           |          |           |           |       |         |           |             |
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

