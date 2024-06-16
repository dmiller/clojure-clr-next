# TypeDispatch.Benchmark

This is similar to the `DispatchBenchmark`.

I was comparing C# ClojureCLR to F# ClojureCLR.Next code in the general of area of the `equiv`.
That code tests for whether a value is 'numeric'.  The C# code is rather simple:

```C#
    public static bool IsNumeric(object o)
    {
        return o != null && IsNumericType(o.GetType());
    }

    public static bool IsNumericType(Type type)
    {
        switch (Type.GetTypeCode(type))
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Double:
            case TypeCode.Single:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                return true;
            default: 
                return type == typeof(BigInteger);
        }
```

(Note: in the actual code, more types are tested in the default case.)

The translation to F# is easy:

```F#
    static member public IsNumeric (o:obj) = 
    	o <> null && IsNumericType(o.GetType())
   
    static member public IsNumericType2(t: Type) =
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
        | _ -> t = typeof<System.Numerics.BigInteger>
```

Except that there are already several mistakes.  
The first is that the wrong test for `null` is used in `IsNumeric`.  
See benchmark `NullTesting.Benchmark` for details on that.
Once the correct test is used, the IL generated for the F# and C# versions of `IsNumeric` are identical; 
we need not consider it further.

The second mistake is that the test `t = typeof<System.Numerics.BigInteger>` is inefficient.
In C#, that test compiles in IL to a call to `Type.op_Equality`.
In F#, that test compiles in IL to a call to `LanguagePrimitives.HashCompare.GenericEqualityIntrinsic`.
The latter is not fast.  See benchmark `TypeEquality.Benchmark` for details on that.

So I decided to directly encode the comparison:

```F#
	static member public IsNumericType(t: Type) =
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
        | _ -> Type.op_Equality(t, typeof<System.Numerics.BigInteger>)
```

The C# compiler and the F# compiler handle the `switch`/`match` differently.
Because the set of values explicitly listed from the `TypeCode` enumeration is contiguous, the C# compiler checks to see if the type code is within the range using numeric comparison.
The F# compiler uses the `switch` op in the IL.

In this case, one can directly code what the C# compiler does:

```F#
    static member public IsNumericType3(t: Type) =
        let tc : uint =  uint (Type.GetTypeCode(t)) 
        tc  - 5u <= 9u ||   Type.op_Equality(t, typeof<System.Numerics.BigInteger>)
```

## Results

I ran the tests separately on `int`, `long`, `double`, `BigInteger`, and `string` types.
One would expect for the first three types to have the same performance because they are all handled in the explicit cases.
`BigInteger` should come in next, and `String` should be the slowest, because it has to fail the type code match _and_ go through the `Type.op_Equality` test.

Or so I thought.  It appears failing the switch is faster than succeeding.  

What is clear:  The F# approach with the `match` compiling to the `switch` op is faster than the C# approach.  
I am surprised.  (Especially since in my initial tests I thought it was slower.) 
Note that `Type3`, which mimics the IL the C# compiler generates -- the generated IL is identical -- matches the C# performance almost exactly.
So the C# compiler's choice here is suboptimal.

I'm not sure why `Type2` is slower than `Type` in the F# code for `Int32`, `Int64` and `Double`.  
We shouldn't be hitting the explicit type comparison (with `BigInteger`).  
But notice that `Type2` is a _lot_ slower for `BigInteger` and `String` due to the explicit type comparison, as one would expect.



| Method                     | inputType | Mean      | Error     | StdDev    | Ratio | RatioSD |
|--------------------------- |---------- |----------:|----------:|----------:|------:|--------:|
| CSharpDirectIsNumericType  | I32       | 1.3905 ns | 0.0525 ns | 0.0439 ns |  1.00 |    0.00 |
| FSharpDirectIsNumericType  | I32       | 0.7536 ns | 0.0149 ns | 0.0139 ns |  0.54 |    0.02 |
| FSharpDirectIsNumericType2 | I32       | 3.0225 ns | 0.0163 ns | 0.0152 ns |  2.17 |    0.07 |
| FSharpDirectIsNumericType3 | I32       | 1.3543 ns | 0.0101 ns | 0.0095 ns |  0.98 |    0.03 |
|                            |           |           |           |           |       |         |
| CSharpDirectIsNumericType  | I64       | 1.3870 ns | 0.0199 ns | 0.0186 ns |  1.00 |    0.00 |
| FSharpDirectIsNumericType  | I64       | 0.7570 ns | 0.0117 ns | 0.0104 ns |  0.55 |    0.01 |
| FSharpDirectIsNumericType2 | I64       | 1.7864 ns | 0.0073 ns | 0.0061 ns |  1.29 |    0.02 |
| FSharpDirectIsNumericType3 | I64       | 1.4193 ns | 0.0199 ns | 0.0176 ns |  1.02 |    0.02 |
|                            |           |           |           |           |       |         |
| CSharpDirectIsNumericType  | Dbl       | 1.4206 ns | 0.0245 ns | 0.0229 ns |  1.00 |    0.00 |
| FSharpDirectIsNumericType  | Dbl       | 0.8308 ns | 0.0411 ns | 0.0615 ns |  0.62 |    0.04 |
| FSharpDirectIsNumericType2 | Dbl       | 1.8437 ns | 0.0481 ns | 0.0450 ns |  1.30 |    0.05 |
| FSharpDirectIsNumericType3 | Dbl       | 1.4487 ns | 0.0166 ns | 0.0155 ns |  1.02 |    0.02 |
|                            |           |           |           |           |       |         |
| CSharpDirectIsNumericType  | Big       | 1.3447 ns | 0.0197 ns | 0.0184 ns |  1.00 |    0.00 |
| FSharpDirectIsNumericType  | Big       | 0.7186 ns | 0.0081 ns | 0.0076 ns |  0.53 |    0.01 |
| FSharpDirectIsNumericType2 | Big       | 7.7251 ns | 0.0658 ns | 0.0616 ns |  5.75 |    0.08 |
| FSharpDirectIsNumericType3 | Big       | 1.3833 ns | 0.0159 ns | 0.0124 ns |  1.03 |    0.01 |
|                            |           |           |           |           |       |         |
| CSharpDirectIsNumericType  | Str       | 1.4488 ns | 0.0234 ns | 0.0207 ns |  1.00 |    0.00 |
| FSharpDirectIsNumericType  | Str       | 0.7403 ns | 0.0400 ns | 0.0476 ns |  0.52 |    0.03 |
| FSharpDirectIsNumericType2 | Str       | 7.6861 ns | 0.1140 ns | 0.1066 ns |  5.30 |    0.08 |
| FSharpDirectIsNumericType3 | Str       | 1.4159 ns | 0.0386 ns | 0.0361 ns |  0.98 |    0.03 |


I had some concern that tiered compilation might be affecting the results with the flood of `int32` values coming at the start.
So I ran a separate benchmark where there are five calls grouped together, one for each type. 
The results are similar.

| Method                     | Mean      | Error     | StdDev    | Ratio | RatioSD |
|--------------------------- |----------:|----------:|----------:|------:|--------:|
| CSharpDirectIsNumericType  |  6.189 ns | 0.1483 ns | 0.2030 ns |  1.00 |    0.00 |
| FSharpDirectIsNumericType  |  4.270 ns | 0.0451 ns | 0.0421 ns |  0.70 |    0.02 |
| FSharpDirectIsNumericType2 | 26.047 ns | 0.1251 ns | 0.1170 ns |  4.28 |    0.11 |
| FSharpDirectIsNumericType3 |  6.153 ns | 0.0879 ns | 0.0779 ns |  1.01 |    0.02 |


