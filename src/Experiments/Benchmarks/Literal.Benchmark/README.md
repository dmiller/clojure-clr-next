# Literal.Benchmark

This is a benchmark for the different ways to access a literal or named constant value in F#.

My inspiration for this benchmark was realizing 
that I had left out a 'val' on a static member of a class
and that thereby accessing that member caused what I thought was the initialization code
to be run on every access.

Basically, I didn't understand the difference between a static member and a static val member.

Example:

```F#
module MyModule

let f() = System.Random.Shared.Next()

type MyClass() =

    static member StaticMember = f()
    
    member _.DoSomething i =
        MyClass.StaticMember + i
```

Key parts of the IL:

```
        .method public specialname static 
            int32 get_StaticMember () cil managed 
        {
            // Method begins at RVA 0x206c
            // Code size 8 (0x8)
            .maxstack 8

            IL_0000: tail.
            IL_0002: call int32 MyModule::f()
            IL_0007: ret
        } // end of method MyClass::get_StaticMember

        .method public hidebysig 
            instance int32 DoSomething (
                int32 i
            ) cil managed 
        {
            // Method begins at RVA 0x2078
            // Code size 8 (0x8)
            .maxstack 8

            IL_0000: call int32 MyModule/MyClass::get_StaticMember()
            IL_0005: ldarg.1
            IL_0006: add
            IL_0007: ret
        } // end of method MyClass::DoSomething

        // Properties
        .property int32 StaticMember()
        {
            .get int32 MyModule/MyClass::get_StaticMember()
        }

```

In Release mode, the JIT compiler will inline the call to `get_StaticMember` in the `DoSomething` method.

```
        .method public hidebysig 
            instance int32 DoSomething (
                int32 i
            ) cil managed 
        {
            // Method begins at RVA 0x207c
            // Code size 13 (0xd)
            .maxstack 8

            IL_0000: call class [System.Runtime]System.Random [System.Runtime]System.Random::get_Shared()
            IL_0005: callvirt instance int32 [System.Runtime]System.Random::Next()
            IL_000a: ldarg.1
            IL_000b: add
            IL_000c: ret
        } // end of method MyClass::DoSomething

```

One must remember to include the `val` keyword in the static member declaration to avoid this behavior.
However, one gets the special code used to control the order of static method initialization.

```
    .method public specialname static 
            int32 get_StaticMember () cil managed 
        {
            .custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
                01 00 00 00
            )
            .custom instance void [System.Runtime]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = (
                01 00 00 00
            )
            // Method begins at RVA 0x206c
            // Code size 23 (0x17)
            .maxstack 8

            IL_0000: volatile.
            IL_0002: ldsfld int32 MyModule/MyClass::init@5
            IL_0007: ldc.i4.1
            IL_0008: bge.s IL_0011

            IL_000a: call void [FSharp.Core]Microsoft.FSharp.Core.LanguagePrimitives/IntrinsicFunctions::FailStaticInit()
            IL_000f: br.s IL_0011

            IL_0011: ldsfld int32 MyModule/MyClass::StaticMember@
            IL_0016: ret
        } // end of method MyClass::get_StaticMember

```

or translated into C# (since that is sharplab.io generates):

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
        
I have seen it argued that tiered JIT compilation will eventually get rid of 
the overhead of the static initialization check, but I thought I'd test it for myself.



## The tests

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


## Analysis

Lookup of a static val member is slower then the other methods;  the other methods are roughly the same.

## Conclusion

Read it and weep.
