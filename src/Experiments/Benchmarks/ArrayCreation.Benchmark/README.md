# ArrayCreation.Benchmark

This project is a benchmark for array creation in F#.

We include several methods from C#.  Those are defined in project ArraCreation.CSharp.

The methods are:

- CSharp:  `new object[n]`
- CSharpFixed:  `new object[32]`
- FSharpZeroCreate:  `Array.zeroCreate n`
- FSharpZeroCreateFixedDirect:  `Array.zeroCreate 32`
- FSharpCreate:  `Array.create n null`

## Results

| Method                      | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| CSharp                      |  8.823 ns | 0.2194 ns | 0.5663 ns |  0.98 |    0.05 | 0.0214 |     280 B |        1.00 |
| CSharpFixed                 |  7.459 ns | 0.1755 ns | 0.2219 ns |  0.86 |    0.03 | 0.0214 |     280 B |        1.00 |
| FSharpZeroCreate            |  8.519 ns | 0.1294 ns | 0.1147 ns |  1.00 |    0.00 | 0.0214 |     280 B |        1.00 |
| FSharpZeroCreateFixedDirect |  8.544 ns | 0.0988 ns | 0.0876 ns |  1.00 |    0.02 | 0.0214 |     280 B |        1.00 |
| FSharpCreate                | 38.454 ns | 0.7824 ns | 0.6936 ns |  4.52 |    0.11 | 0.0214 |     280 B |        1.00 |

## Analysis

We can see why `Array.zeroCreate` is the preferred method for F#.  It is essentially as fast as the C# code.

For the C#, there is a slight difference between having the size fixed in the `new` call versus having it as a variable.  
The difference in the IL is between `ldarg.0` and `ldc.i4.s 32`.
For whatever reason, the JITter can generate slightly faster code for the latter -- for C#.
The exact same difference exists in the F# IL code, but the JITter does not seem to take advantage of it.

At any rate, this benchmark relieved me of the fear that `Array.zeroCreate` was significantly slower than what C# could do.  It is not.
I can live with a nanosecond difference.

(The fear came originally from the fact that the IL for the C# used the `newarr` opcode, while the F# did not.  In fact the IL for the F# has an explicit call out:

```F#
call !!0[] [FSharp.Core]Microsoft.FSharp.Collections.ArrayModule::ZeroCreate<object>(int32)
```

But that does get optimized away, somehow.

